using System.Collections.Generic;
using UnityEngine;

public enum RDKStimulusRenderMode
{
    TransformObjects,
    DrawMeshInstanced
}

[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK Stimulus Manager")]
public sealed class RDKStimulusManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RDKExperimentConfig _config;
    [SerializeField] Transform _stimulusRoot;
    [SerializeField] Transform _viewerTransform;
    [SerializeField] GameObject _pointPrefab;

    [Header("Point Source")]
    [SerializeField] string _pointNamePrefix = "Sphere";
    [SerializeField] bool _includeInactivePoints = true;
    [SerializeField] bool _hideUnusedPoints = true;

    [Header("Rendering")]
    [SerializeField] RDKStimulusRenderMode _renderMode = RDKStimulusRenderMode.DrawMeshInstanced;
    [SerializeField] Mesh _instancedMesh;
    [SerializeField] Material _instancedMaterial;
    [SerializeField] bool _disableSourceRenderersWhenInstanced = true;

    readonly List<Transform> _points = new List<Transform>();
    readonly List<int> _shuffleBuffer = new List<int>();
    Vector2[] _positions;
    Vector2[] _randomDirections;
    bool[] _coherentDots;
    Matrix4x4[] _instanceMatrices;
    readonly Matrix4x4[] _drawBatchMatrices = new Matrix4x4[1023];
    int _activePointCount;
    int _coherentDotCount;
    int _randomDotCount;
    float _nextRandomDirectionRefreshTime;
    Vector2 _coherentDirection;
    bool _stimulusRunning;
    bool _stimulusVisible;

    public int activePointCount => _activePointCount;
    public int coherentDotCount => _coherentDotCount;
    public int randomDotCount => _randomDotCount;
    public int cachedPointCount => _points.Count;
    public Transform stimulusRoot => _stimulusRoot;
    public Transform viewerTransform => _viewerTransform;

    void Reset()
    {
        _stimulusRoot = transform;
        AutoFindViewer();
    }

    void Awake()
    {
        if (_stimulusRoot == null)
        {
            _stimulusRoot = transform;
        }

        if (_viewerTransform == null)
        {
            AutoFindViewer();
        }

        CachePoints();
    }

    void Update()
    {
        if (!_stimulusRunning || _config == null)
        {
            return;
        }

        UpdateStimulus(Time.deltaTime);
    }

    void LateUpdate()
    {
        DrawInstancedIfNeeded();
    }

    public void Configure(RDKExperimentConfig config)
    {
        _config = config;
    }

    public void SetViewerTransform(Transform viewer)
    {
        if (viewer != null && _viewerTransform != viewer)
        {
            _viewerTransform = viewer;
            Debug.Log($"[RDKStimulusManager] Viewer set to {_viewerTransform.name}.", this);
        }
    }

    [ContextMenu("Cache CPP Points")]
    public void CachePoints()
    {
        _points.Clear();
        Transform root = _stimulusRoot != null ? _stimulusRoot : transform;
        Transform[] candidates = root.GetComponentsInChildren<Transform>(_includeInactivePoints);

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];

            if (candidate == root)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(_pointNamePrefix) && !candidate.name.StartsWith(_pointNamePrefix))
            {
                continue;
            }

            _points.Add(candidate);
        }

        CacheInstancingAssets();
    }

    public void PrepareTrial(RDKTrialPlan trial)
    {
        if (_config == null)
        {
            Debug.LogWarning("[RDKStimulusManager] Missing config.", this);
            return;
        }

        if (_stimulusRoot == null)
        {
            _stimulusRoot = transform;
        }

        if (_points.Count == 0)
        {
            CachePoints();
        }

        EnsurePointCount(_config.pointCount);
        CacheInstancingAssets();
        _activePointCount = Mathf.Min(_config.pointCount, _points.Count);
        if (_activePointCount <= 0)
        {
            Debug.LogWarning("[RDKStimulusManager] No stimulus points are available. Check CPP children and point name prefix.", this);
            return;
        }

        EnsureBuffers(_activePointCount);

        _coherentDotCount = trial.motionDirection == RDKMotionDirection.None
            ? 0
            : Mathf.RoundToInt(_activePointCount * Mathf.Clamp01(trial.motionCoherence));
        _coherentDotCount = Mathf.Clamp(_coherentDotCount, 0, _activePointCount);
        _randomDotCount = _activePointCount - _coherentDotCount;
        _coherentDirection = ResolveCoherentDirection(trial.motionDirection);

        if (_config.recenterPanelBeforeEachTrial)
        {
            RecenterPanelInFrontOfViewer();
        }

        InitializeDotRoles();
        InitializeDotPositions();
        ApplyPointVisibility();
        ApplyAllPointPositions();
        SetStimulusVisible(false);
        Debug.Log($"[RDKStimulusManager] Prepared trial with {_activePointCount} dots. Viewer={(_viewerTransform != null ? _viewerTransform.name : "none")}.", this);
    }

    public void BeginStimulus()
    {
        if (_activePointCount <= 0)
        {
            Debug.LogWarning("[RDKStimulusManager] BeginStimulus ignored because no active points are prepared.", this);
            return;
        }

        _stimulusRunning = true;
        _stimulusVisible = true;
        _nextRandomDirectionRefreshTime = Time.time + _config.randomDirectionRefreshSeconds;
        SetStimulusVisible(true);
        Debug.Log($"[RDKStimulusManager] Stimulus visible with {_activePointCount} dots.", this);
    }

    public void EndStimulus()
    {
        _stimulusRunning = false;
        _stimulusVisible = false;
        SetStimulusVisible(false);
    }

    public void RecenterPanelInFrontOfViewer()
    {
        if (_stimulusRoot == null || _viewerTransform == null || _config == null)
        {
            return;
        }

        Vector3 forward = _viewerTransform.forward;
        if (_config.useHeadYawOnly)
        {
            Vector3 yawForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (yawForward.sqrMagnitude > 0.0001f)
            {
                forward = yawForward.normalized;
            }
        }

        _stimulusRoot.position = _viewerTransform.position;
        _stimulusRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void UpdateStimulus(float deltaTime)
    {
        if (_config.randomDirectionRefreshSeconds > 0f && Time.time >= _nextRandomDirectionRefreshTime)
        {
            RefreshRandomDirections();
            _nextRandomDirectionRefreshTime = Time.time + _config.randomDirectionRefreshSeconds;
        }

        Vector2 window = _config.observationWindowMeters;
        float halfWidth = window.x * 0.5f;
        float halfHeight = window.y * 0.5f;
        float distance = _config.dotSpeedMetersPerSecond * deltaTime;

        for (int i = 0; i < _activePointCount; i++)
        {
            Vector2 direction = _coherentDots[i] ? _coherentDirection : _randomDirections[i];
            _positions[i] += direction * distance;
            bool wrapped = WrapPosition(ref _positions[i], halfWidth, halfHeight);

            if (wrapped && !_coherentDots[i] && _config.randomizeDirectionOnWrap)
            {
                _randomDirections[i] = RandomUnitVector2();
            }

            ApplyPointPosition(i);
        }
    }

    void EnsurePointCount(int requiredCount)
    {
        if (_points.Count >= requiredCount || _pointPrefab == null || _stimulusRoot == null)
        {
            return;
        }

        while (_points.Count < requiredCount)
        {
            GameObject point = Instantiate(_pointPrefab, _stimulusRoot);
            point.name = _pointNamePrefix + " (" + _points.Count + ")";
            _points.Add(point.transform);
        }
    }

    void EnsureBuffers(int count)
    {
        if (_positions == null || _positions.Length != count)
        {
            _positions = new Vector2[count];
            _randomDirections = new Vector2[count];
            _coherentDots = new bool[count];
        }

        if (_instanceMatrices == null || _instanceMatrices.Length != count)
        {
            _instanceMatrices = new Matrix4x4[count];
        }
    }

    void InitializeDotRoles()
    {
        _shuffleBuffer.Clear();
        for (int i = 0; i < _activePointCount; i++)
        {
            _shuffleBuffer.Add(i);
            _coherentDots[i] = false;
        }

        for (int i = _shuffleBuffer.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            int temp = _shuffleBuffer[i];
            _shuffleBuffer[i] = _shuffleBuffer[swapIndex];
            _shuffleBuffer[swapIndex] = temp;
        }

        for (int i = 0; i < _coherentDotCount; i++)
        {
            _coherentDots[_shuffleBuffer[i]] = true;
        }
    }

    void InitializeDotPositions()
    {
        Vector2 window = _config.observationWindowMeters;
        for (int i = 0; i < _activePointCount; i++)
        {
            _positions[i] = new Vector2(
                Random.Range(-window.x * 0.5f, window.x * 0.5f),
                Random.Range(-window.y * 0.5f, window.y * 0.5f));
            _randomDirections[i] = RandomUnitVector2();
        }
    }

    void ApplyPointVisibility()
    {
        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i] == null)
            {
                continue;
            }

            bool active = i < _activePointCount || !_hideUnusedPoints;
            _points[i].gameObject.SetActive(active);
            if (i < _activePointCount)
            {
                _points[i].localScale = Vector3.one * _config.pointSizeMeters;
                SetPointRenderersEnabled(_points[i], _renderMode == RDKStimulusRenderMode.TransformObjects);
            }
        }
    }

    void ApplyAllPointPositions()
    {
        for (int i = 0; i < _activePointCount; i++)
        {
            ApplyPointPosition(i);
        }
    }

    void ApplyPointPosition(int index)
    {
        if (index < 0 || index >= _points.Count || _points[index] == null || _config == null)
        {
            return;
        }

        Vector2 position = _positions[index];
        float distance = _config.panelDistanceMeters;
        Vector3 localPosition;

        if (_config.useCurvedSurface)
        {
            float tangentMagnitudeSquared = position.sqrMagnitude;
            float normal = Mathf.Sqrt(Mathf.Max(0f, distance * distance - tangentMagnitudeSquared));
            localPosition = new Vector3(position.x, position.y, normal);
        }
        else
        {
            localPosition = new Vector3(position.x, position.y, distance);
        }

        _points[index].localPosition = localPosition;

        if (_renderMode == RDKStimulusRenderMode.DrawMeshInstanced && _instanceMatrices != null && index < _instanceMatrices.Length)
        {
            _instanceMatrices[index] = Matrix4x4.TRS(
                _points[index].position,
                _points[index].rotation,
                Vector3.one * _config.pointSizeMeters);
        }
    }

    void SetStimulusVisible(bool visible)
    {
        for (int i = 0; i < _activePointCount && i < _points.Count; i++)
        {
            if (_points[i] != null)
            {
                _points[i].gameObject.SetActive(visible);
                if (_renderMode == RDKStimulusRenderMode.DrawMeshInstanced)
                {
                    SetPointRenderersEnabled(_points[i], false);
                }
            }
        }
    }

    void DrawInstancedIfNeeded()
    {
        if (_renderMode != RDKStimulusRenderMode.DrawMeshInstanced ||
            !_stimulusVisible ||
            _instancedMesh == null ||
            _instancedMaterial == null ||
            _instanceMatrices == null ||
            _activePointCount <= 0)
        {
            return;
        }

        int drawn = 0;
        while (drawn < _activePointCount)
        {
            int batchCount = Mathf.Min(1023, _activePointCount - drawn);
            for (int i = 0; i < batchCount; i++)
            {
                _drawBatchMatrices[i] = _instanceMatrices[drawn + i];
            }

            Graphics.DrawMeshInstanced(_instancedMesh, 0, _instancedMaterial, _drawBatchMatrices, batchCount, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, gameObject.layer);
            drawn += batchCount;
        }
    }

    void CacheInstancingAssets()
    {
        if (_points.Count == 0)
        {
            return;
        }

        if (_instancedMesh == null)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                MeshFilter meshFilter = _points[i] != null ? _points[i].GetComponent<MeshFilter>() : null;
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    _instancedMesh = meshFilter.sharedMesh;
                    break;
                }
            }
        }

        if (_instancedMaterial == null)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                MeshRenderer meshRenderer = _points[i] != null ? _points[i].GetComponent<MeshRenderer>() : null;
                if (meshRenderer != null && meshRenderer.sharedMaterial != null)
                {
                    _instancedMaterial = meshRenderer.sharedMaterial;
                    _instancedMaterial.enableInstancing = true;
                    break;
                }
            }
        }
        else
        {
            _instancedMaterial.enableInstancing = true;
        }

        if (_disableSourceRenderersWhenInstanced && _renderMode == RDKStimulusRenderMode.DrawMeshInstanced)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                SetPointRenderersEnabled(_points[i], false);
            }
        }
    }

    static void SetPointRenderersEnabled(Transform point, bool enabled)
    {
        if (point == null)
        {
            return;
        }

        Renderer[] renderers = point.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = enabled;
        }
    }

    void RefreshRandomDirections()
    {
        for (int i = 0; i < _activePointCount; i++)
        {
            if (!_coherentDots[i])
            {
                _randomDirections[i] = RandomUnitVector2();
            }
        }
    }

    static bool WrapPosition(ref Vector2 position, float halfWidth, float halfHeight)
    {
        bool wrapped = false;

        if (position.x > halfWidth)
        {
            position.x = -halfWidth;
            wrapped = true;
        }
        else if (position.x < -halfWidth)
        {
            position.x = halfWidth;
            wrapped = true;
        }

        if (position.y > halfHeight)
        {
            position.y = -halfHeight;
            wrapped = true;
        }
        else if (position.y < -halfHeight)
        {
            position.y = halfHeight;
            wrapped = true;
        }

        return wrapped;
    }

    static Vector2 ResolveCoherentDirection(RDKMotionDirection direction)
    {
        if (direction == RDKMotionDirection.Left)
        {
            return Vector2.left;
        }

        if (direction == RDKMotionDirection.Right)
        {
            return Vector2.right;
        }

        return Vector2.zero;
    }

    static Vector2 RandomUnitVector2()
    {
        Vector2 value = Random.insideUnitCircle;
        return value.sqrMagnitude <= 0.0001f ? Vector2.right : value.normalized;
    }

    void AutoFindViewer()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _viewerTransform = mainCamera.transform;
        }
    }
}
