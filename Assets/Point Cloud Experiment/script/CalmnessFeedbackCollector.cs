using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

[DefaultExecutionOrder(1240)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Calmness Feedback Collector")]
public sealed class CalmnessFeedbackCollector : MonoBehaviour
{
    const string RightEyeGazeName = "[BuildingBlock] Eye Gaze Right";
    const string LeftEyeGazeName = "[BuildingBlock] Eye Gaze Left";
    const string CenterEyeAnchorName = "CenterEyeAnchor";
    const float RayDirectionEpsilon = 0.0001f;
    const int OptionCount = 4;

    [Header("Preview")]
    [SerializeField] bool _showPreviewOnStart = true;
    [SerializeField, Min(1f)] float _previewTimeoutSeconds = 120f;
    [SerializeField] string _previewPaintingId = "calmness_feedback_preview";
    [SerializeField] CalmnessFeedbackOptionView[] _optionViews;

    [Header("Gaze")]
    [SerializeField] bool _autoFindReferences = true;
    [SerializeField] Transform _leftGazeTransform;
    [SerializeField] Transform _rightGazeTransform;
    [SerializeField] Transform _centerEyeTransform;

    [Header("Placement")]
    [SerializeField, Min(0.5f)] float _distanceMeters = 1.8f;
    [SerializeField] float _verticalAngleDegrees = -10f;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] float _dwellSeconds = 1.2f;
    [SerializeField, Min(0.01f)] float _fadeSeconds = 0.5f;
    [SerializeField, Min(0.01f)] float _selectionPulseSeconds = 0.3f;

    Transform _feedbackRoot;
    FeedbackOption[] _options;
    Coroutine _routine;
    Action<CalmnessFeedbackResult> _onCompleted;
    string _paintingId;
    int _paintingIndex;
    float _totalViewDurationSec;
    float _shownAt;
    float _hoverStartedAt;
    float _firstGazeEntryAt;
    float _hoverDuration;
    int _hoveredIndex = -1;
    bool _hasFirstGazeEntry;
    bool _isCollecting;
    bool _wasFallbackTriggerPressed;

    static CalmnessFeedbackCollector _instance;
    public static CalmnessFeedbackCollector Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<CalmnessFeedbackCollector>();
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject("CalmnessFeedbackCollector");
            _instance = gameObject.AddComponent<CalmnessFeedbackCollector>();
            return _instance;
        }
    }

    public bool isCollecting => _isCollecting;

    public void ShowAndCollect(
        string paintingId,
        int paintingIndex,
        float totalViewDurationSec,
        float timeoutSeconds,
        Action<CalmnessFeedbackResult> onCompleted)
    {
        EnsureUi();
        CancelActiveCollection(false);

        _paintingId = paintingId;
        _paintingIndex = paintingIndex;
        _totalViewDurationSec = totalViewDurationSec;
        _onCompleted = onCompleted;
        _shownAt = Time.time;
        _hoveredIndex = -1;
        _hoverDuration = 0f;
        _hasFirstGazeEntry = false;
        _firstGazeEntryAt = 0f;
        _isCollecting = true;

        PositionInFrontOfViewer();
        SetGroupAlpha(0f);
        ResetOptions();
        if (_feedbackRoot != null)
        {
            _feedbackRoot.gameObject.SetActive(true);
        }
        _routine = StartCoroutine(CollectRoutine(Mathf.Max(0.01f, timeoutSeconds)));
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            return;
        }

        _instance = this;
        EnsureUi();
    }

    void Start()
    {
        if (_showPreviewOnStart && !_isCollecting)
        {
            ShowAndCollect(
                _previewPaintingId,
                -1,
                0f,
                _previewTimeoutSeconds,
                CalmnessFeedbackLogger.Log);
        }
    }

    void OnDisable()
    {
        CancelActiveCollection(false);
    }

    void EnsureUi()
    {
        AutoFindOptionViews();
        if (_options != null && _options.Length == OptionCount)
        {
            return;
        }

        _options = new FeedbackOption[OptionCount];
        for (var i = 0; i < OptionCount; i++)
        {
            var optionView = _optionViews != null && i < _optionViews.Length ? _optionViews[i] : null;
            if (optionView == null)
            {
                optionView = CreateDefaultOptionView(i);
            }

            optionView.EnsureVisuals();
            _options[i] = new FeedbackOption(optionView);
        }
    }

    void AutoFindOptionViews()
    {
        if (_optionViews != null && _optionViews.Length >= OptionCount)
        {
            return;
        }

        var foundViews = GetComponentsInChildren<CalmnessFeedbackOptionView>(true);
        if (foundViews == null || foundViews.Length == 0)
        {
            return;
        }

        Array.Sort(foundViews, (left, right) => left.level.CompareTo(right.level));
        _optionViews = foundViews;
        _feedbackRoot = ResolveFeedbackRoot(foundViews);
    }

    CalmnessFeedbackOptionView CreateDefaultOptionView(int index)
    {
        if (_feedbackRoot == null)
        {
            var rootObject = new GameObject("CalmnessFeedbackOptions");
            rootObject.transform.SetParent(transform, false);
            _feedbackRoot = rootObject.transform;
        }

        var optionObject = new GameObject("0" + (index + 1) + "_" + CalmnessFeedbackScale.GetLabel(index + 1));
        optionObject.transform.SetParent(_feedbackRoot, false);
        optionObject.transform.localPosition = GetOptionPosition(index);
        var optionView = optionObject.AddComponent<CalmnessFeedbackOptionView>();
        optionView.SetDefaultLevel(index + 1);
        return optionView;
    }

    static Transform ResolveFeedbackRoot(CalmnessFeedbackOptionView[] optionViews)
    {
        if (optionViews == null || optionViews.Length == 0 || optionViews[0] == null)
        {
            return null;
        }

        var parent = optionViews[0].transform.parent;
        return parent != null ? parent : optionViews[0].transform;
    }

    IEnumerator CollectRoutine(float timeoutSeconds)
    {
        yield return FadeTo(1f, _fadeSeconds);

        var deadline = Time.time + timeoutSeconds;
        while (_isCollecting && Time.time < deadline)
        {
            UpdateHover(Time.deltaTime);
            if (_hoveredIndex >= 0 && _hoverDuration >= _dwellSeconds)
            {
                yield return CompleteSelection(_hoveredIndex);
                yield break;
            }

            if (_hoveredIndex >= 0 && WasFallbackConfirmPressed())
            {
                yield return CompleteSelection(_hoveredIndex);
                yield break;
            }

            yield return null;
        }

        if (_isCollecting)
        {
            CompleteTimedOut();
            yield return FadeTo(0f, _fadeSeconds);
            if (_feedbackRoot != null)
            {
                _feedbackRoot.gameObject.SetActive(false);
            }
        }
    }

    void UpdateHover(float deltaTime)
    {
        var index = TryGetGazedOptionIndex(out _);
        if (index >= 0 && !_hasFirstGazeEntry)
        {
            _hasFirstGazeEntry = true;
            _firstGazeEntryAt = Mathf.Max(0f, Time.time - _shownAt);
        }

        if (index != _hoveredIndex)
        {
            _hoveredIndex = index;
            _hoverStartedAt = Time.time;
            _hoverDuration = 0f;
        }
        else if (_hoveredIndex >= 0)
        {
            _hoverDuration = Mathf.Max(0f, Time.time - _hoverStartedAt);
        }
        else
        {
            _hoverDuration = 0f;
        }

        for (var i = 0; i < _options.Length; i++)
        {
            var progress = i == _hoveredIndex ? Mathf.Max(0.06f, Mathf.Clamp01(_hoverDuration / _dwellSeconds)) : 0f;
            _options[i].SetProgress(progress);
            _options[i].SetHighlighted(i == _hoveredIndex, deltaTime);
        }
    }

    IEnumerator CompleteSelection(int index)
    {
        _isCollecting = false;
        var option = _options[index];
        var result = CalmnessFeedbackResult.Create(
            _paintingId,
            _paintingIndex,
            option.level,
            _hoverDuration,
            false,
            _firstGazeEntryAt,
            _hasFirstGazeEntry,
            _totalViewDurationSec);

        _onCompleted?.Invoke(result);
        yield return PulseOption(option);
        yield return FadeTo(0f, _fadeSeconds);
        if (_feedbackRoot != null)
        {
            _feedbackRoot.gameObject.SetActive(false);
        }
    }

    void CompleteTimedOut()
    {
        _isCollecting = false;
        var result = CalmnessFeedbackResult.Create(
            _paintingId,
            _paintingIndex,
            0,
            _hoverDuration,
            true,
            _firstGazeEntryAt,
            _hasFirstGazeEntry,
            _totalViewDurationSec);

        _onCompleted?.Invoke(result);
    }

    IEnumerator PulseOption(FeedbackOption option)
    {
        var duration = Mathf.Max(0.01f, _selectionPulseSeconds);
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var pulse = Mathf.Sin(progress * Mathf.PI);
            option.SetPulseScale(Mathf.Lerp(1f, 1.15f, pulse));
            yield return null;
        }

        option.SetPulseScale(1f);
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        var startAlpha = _options != null && _options.Length > 0 ? _options[0].groupAlpha : 0f;
        var elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            SetGroupAlpha(Mathf.Lerp(startAlpha, targetAlpha, SmoothStep01(progress)));
            yield return null;
        }

        SetGroupAlpha(targetAlpha);
    }

    void CancelActiveCollection(bool completeAsTimeout)
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        if (completeAsTimeout && _isCollecting)
        {
            CompleteTimedOut();
        }

        _isCollecting = false;
        if (_feedbackRoot != null && Application.isPlaying)
        {
            _feedbackRoot.gameObject.SetActive(false);
        }
    }

    void ResetOptions()
    {
        for (var i = 0; i < _options.Length; i++)
        {
            _options[i].ResetVisuals();
        }
    }

    void SetGroupAlpha(float alpha)
    {
        if (_options == null)
        {
            return;
        }

        for (var i = 0; i < _options.Length; i++)
        {
            _options[i].SetGroupAlpha(alpha);
        }
    }

    void PositionInFrontOfViewer()
    {
        var viewer = GetViewerTransform();
        if (viewer == null)
        {
            return;
        }

        var forward = viewer.forward.sqrMagnitude > RayDirectionEpsilon
            ? viewer.forward.normalized
            : Vector3.forward;
        var up = Vector3.up;
        var distance = Mathf.Max(0.5f, _distanceMeters);
        var verticalOffset = Mathf.Tan(_verticalAngleDegrees * Mathf.Deg2Rad) * distance;

        if (_feedbackRoot == null)
        {
            return;
        }

        _feedbackRoot.position = viewer.position + forward * distance + up * verticalOffset;
        _feedbackRoot.rotation = Quaternion.LookRotation(forward, up);
    }

    int TryGetGazedOptionIndex(out Vector3 worldHit)
    {
        worldHit = default;
        if (!TryGetGazeRay(out var ray))
        {
            return -1;
        }

        if (_feedbackRoot == null)
        {
            return -1;
        }

        var plane = new Plane(_feedbackRoot.forward, _feedbackRoot.position);
        if (!plane.Raycast(ray, out var distance) || distance < 0f || distance > _distanceMeters * 3f)
        {
            return -1;
        }

        worldHit = ray.GetPoint(distance);
        for (var i = 0; i < _options.Length; i++)
        {
            if (_options[i].ContainsWorldPoint(worldHit))
            {
                return i;
            }
        }

        return -1;
    }

    bool TryGetGazeRay(out Ray ray)
    {
        if (_autoFindReferences && HasMissingReferences())
        {
            AutoFindReferences();
        }

        var directionSum = Vector3.zero;
        var positionSum = Vector3.zero;
        var eyeCount = 0;
        AddEyeSample(_leftGazeTransform, ref directionSum, ref positionSum, ref eyeCount);
        AddEyeSample(_rightGazeTransform, ref directionSum, ref positionSum, ref eyeCount);

        if (eyeCount > 0 && directionSum.sqrMagnitude > RayDirectionEpsilon)
        {
            var origin = _centerEyeTransform != null ? _centerEyeTransform.position : positionSum / eyeCount;
            ray = new Ray(origin, directionSum.normalized);
            return true;
        }

        var viewer = GetViewerTransform();
        if (viewer == null || viewer.forward.sqrMagnitude <= RayDirectionEpsilon)
        {
            ray = default;
            return false;
        }

        ray = new Ray(viewer.position, viewer.forward.normalized);
        return true;
    }

    void AutoFindReferences()
    {
        if (_leftGazeTransform == null)
        {
            _leftGazeTransform = FindTransformByName(LeftEyeGazeName);
        }

        if (_rightGazeTransform == null)
        {
            _rightGazeTransform = FindTransformByName(RightEyeGazeName);
        }

        if (_centerEyeTransform == null)
        {
            _centerEyeTransform = FindTransformByName(CenterEyeAnchorName);
        }
    }

    bool HasMissingReferences()
    {
        return _leftGazeTransform == null || _rightGazeTransform == null || _centerEyeTransform == null;
    }

    Transform GetViewerTransform()
    {
        if (_autoFindReferences && _centerEyeTransform == null)
        {
            AutoFindReferences();
        }

        if (_centerEyeTransform != null)
        {
            return _centerEyeTransform;
        }

        return Camera.main != null ? Camera.main.transform : null;
    }

    bool WasFallbackConfirmPressed()
    {
        var pressed = Input.GetKeyDown(KeyCode.Space) ||
                      Input.GetKeyDown(KeyCode.Return) ||
                      Input.GetMouseButtonDown(0) ||
                      IsControllerTriggerPressedThisFrame();
        return pressed;
    }

    bool IsControllerTriggerPressedThisFrame()
    {
        var pressed = false;
        TryReadTriggerButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, ref pressed);
        TryReadTriggerButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, ref pressed);

        var down = pressed && !_wasFallbackTriggerPressed;
        _wasFallbackTriggerPressed = pressed;
        return down;
    }

    static void TryReadTriggerButton(InputDeviceCharacteristics characteristics, ref bool pressed)
    {
        var devices = new System.Collections.Generic.List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(characteristics, devices);
        for (var i = 0; i < devices.Count; i++)
        {
            if (devices[i].TryGetFeatureValue(CommonUsages.triggerButton, out var triggerButton) && triggerButton)
            {
                pressed = true;
                return;
            }
        }
    }

    static void AddEyeSample(
        Transform eyeTransform,
        ref Vector3 directionSum,
        ref Vector3 positionSum,
        ref int eyeCount)
    {
        if (eyeTransform == null)
        {
            return;
        }

        var direction = eyeTransform.forward;
        if (direction.sqrMagnitude <= RayDirectionEpsilon)
        {
            return;
        }

        directionSum += direction.normalized;
        positionSum += eyeTransform.position;
        eyeCount++;
    }

    static Transform FindTransformByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        var gameObject = GameObject.Find(objectName);
        return gameObject != null ? gameObject.transform : null;
    }

    static Vector3 GetOptionPosition(int index)
    {
        var x = -0.45f + index * 0.3f;
        var y = index == 0 || index == OptionCount - 1 ? 0.026f : 0f;
        return new Vector3(x, y, 0f);
    }

    static float SmoothStep01(float progress)
    {
        var t = Mathf.Clamp01(progress);
        return t * t * (3f - 2f * t);
    }

    sealed class FeedbackOption
    {
        readonly CalmnessFeedbackOptionView _view;

        public FeedbackOption(CalmnessFeedbackOptionView view)
        {
            _view = view;
        }

        public int level => _view != null ? _view.level : 0;
        public float groupAlpha => _view != null ? _view.groupAlpha : 0f;

        public void ResetVisuals()
        {
            _view?.ResetVisuals();
        }

        public void SetProgress(float progress)
        {
            _view?.SetProgress(progress);
        }

        public void SetHighlighted(bool highlighted, float deltaTime)
        {
            _view?.SetHighlighted(highlighted, deltaTime);
        }

        public void SetGroupAlpha(float alpha)
        {
            _view?.SetGroupAlpha(alpha);
        }

        public void SetPulseScale(float scale)
        {
            _view?.SetPulseScale(scale);
        }

        public bool ContainsWorldPoint(Vector3 worldPoint)
        {
            return _view != null && _view.ContainsWorldPoint(worldPoint);
        }
    }
}

public sealed class DwellArcGraphic : MaskableGraphic
{
    [Range(0f, 1f)] public float progress;
    [Min(1f)] public float thickness = 4f;

    float _lastProgress = -1f;

    void LateUpdate()
    {
        if (!Mathf.Approximately(_lastProgress, progress))
        {
            _lastProgress = progress;
            SetVerticesDirty();
        }
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var clampedProgress = Mathf.Clamp01(progress);
        if (clampedProgress <= 0f || color.a <= 0f)
        {
            return;
        }

        var rect = rectTransform.rect;
        var outerRadius = Mathf.Min(rect.width, rect.height) * 0.5f;
        var innerRadius = Mathf.Max(0f, outerRadius - thickness);
        var center = rect.center;
        var segmentCount = Mathf.Max(3, Mathf.CeilToInt(64f * clampedProgress));
        var angleSpan = 360f * clampedProgress;
        var vertexColor = color;

        for (var i = 0; i <= segmentCount; i++)
        {
            var t = i / (float)segmentCount;
            var angle = (90f - angleSpan * t) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            vh.AddVert(center + direction * outerRadius, vertexColor, Vector2.zero);
            vh.AddVert(center + direction * innerRadius, vertexColor, Vector2.zero);
        }

        for (var i = 0; i < segmentCount; i++)
        {
            var vertex = i * 2;
            vh.AddTriangle(vertex, vertex + 1, vertex + 2);
            vh.AddTriangle(vertex + 2, vertex + 1, vertex + 3);
        }
    }
}
