using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[DefaultExecutionOrder(1235)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Meditation Choice Eye Gaze Feedback")]
public sealed class MeditationChoiceEyeGazeFeedback : MonoBehaviour
{
    const string RightEyeGazeName = "[BuildingBlock] Eye Gaze Right";
    const string LeftEyeGazeName = "[BuildingBlock] Eye Gaze Left";
    const string CenterEyeAnchorName = "CenterEyeAnchor";
    const float RayDirectionEpsilon = 0.0001f;

    public enum GazeRayEvaluationMode
    {
        IndependentEyeRays,
        SingleEyeRay
    }

    public enum SingleEyeRaySource
    {
        RightEye,
        LeftEye,
        HighestConfidence
    }

    [System.Serializable]
    public sealed class OrbTarget
    {
        public string label;
        public Transform target;
        public VisualEffect visualEffect;
        [Min(0f)] public float hitRadiusMeters;
    }

    sealed class OrbRuntimeState
    {
        public Vector3 baseLocalScale = Vector3.one;
        public bool baseActiveSelf = true;
        public float strength;
        public bool hasBaseSize;
        public float baseSize = 1f;
        public bool hasBaseSpawnRate;
        public float baseSpawnRate;
        public bool hasBaseTrailsSpawnRate;
        public float baseTrailsSpawnRate;
        public bool hasBaseTrailsLifeTime;
        public float baseTrailsLifeTime;
        public bool hasBaseColor;
        public Color baseColor = Color.white;
    }

    struct GazeRaySample
    {
        public Ray ray;
        public float confidence;

        public GazeRaySample(Ray ray, float confidence)
        {
            this.ray = ray;
            this.confidence = confidence;
        }
    }

    public sealed class SelectionResult
    {
        public readonly int orbIndex;
        public readonly string label;
        public readonly float dwellSeconds;
        public readonly float effectSeconds;
        public readonly float triggeredRealtime;
        public readonly int triggeredFrame;
        public readonly float completedRealtime;
        public readonly int frame;

        public SelectionResult(
            int orbIndex,
            string label,
            float dwellSeconds,
            float effectSeconds,
            float triggeredRealtime,
            int triggeredFrame,
            float completedRealtime,
            int frame)
        {
            this.orbIndex = orbIndex;
            this.label = label;
            this.dwellSeconds = dwellSeconds;
            this.effectSeconds = effectSeconds;
            this.triggeredRealtime = triggeredRealtime;
            this.triggeredFrame = triggeredFrame;
            this.completedRealtime = completedRealtime;
            this.frame = frame;
        }
    }

    [Header("References")]
    [SerializeField] bool _autoFindReferences = true;
    [SerializeField] OVREyeGaze _leftEyeGaze;
    [SerializeField] OVREyeGaze _rightEyeGaze;
    [SerializeField] Transform _leftGazeTransform;
    [SerializeField] Transform _rightGazeTransform;
    [SerializeField] Transform _centerEyeTransform;

    [Header("Eye Tracking")]
    [SerializeField] bool _requestEyeTrackingPermission = true;
    [SerializeField] bool _startEyeTrackingIfNeeded = true;
    [SerializeField] bool _requireEyeTrackingEnabled = true;
    [SerializeField, Range(0f, 1f)] float _minimumEyeConfidence = 0.5f;
    [SerializeField] bool _allowHeadFallbackWhenEyeTrackingInvalid;
    [SerializeField] bool _logInvalidEyeTracking = true;
    [SerializeField, Min(0.25f)] float _startRetryInterval = 2f;
    [SerializeField, Min(0.1f)] float _invalidLogInterval = 2f;

    [Header("Gaze Hit Evaluation")]
    [SerializeField] GazeRayEvaluationMode _gazeRayEvaluationMode = GazeRayEvaluationMode.IndependentEyeRays;
    [SerializeField] SingleEyeRaySource _singleEyeRaySource = SingleEyeRaySource.HighestConfidence;
    [SerializeField, Min(0f)] float _hoverLossGraceSeconds = 0.6f;

    [Header("Orb Targets")]
    [SerializeField] bool _autoCollectChildOrbs = true;
    [SerializeField] OrbTarget[] _orbs;
    [SerializeField, Min(0.01f)] float _defaultHitRadiusMeters = 5f;
    [SerializeField, Min(0f)] float _autoHitRadiusScale = 0.35f;
    [SerializeField, Min(0.01f)] float _maxGazeDistanceMeters = 100f;

    [Header("Sustained Gaze Response")]
    [SerializeField, Min(0.05f)] float _dwellShrinkSeconds = 1.8f;
    [SerializeField, Range(0.05f, 1f)] float _minimumHoveredScale = 0.55f;
    [SerializeField, Min(0.01f)] float _attackSpeed = 5f;
    [SerializeField, Min(0.01f)] float _releaseSpeed = 4f;
    [SerializeField] bool _useUnscaledTime;

    [Header("Selection Burst")]
    [SerializeField, Min(0.05f)] float _selectionDwellSeconds = 2f;
    [SerializeField, Min(0.05f)] float _selectionEffectSeconds = 2f;
    [SerializeField, Min(1f)] float _selectionBurstScaleMultiplier = 1.35f;
    [SerializeField, Min(1f)] float _selectionBurstVfxSizeMultiplier = 1.6f;
    [SerializeField, Min(0f)] float _selectionBurstSpawnRateMultiplier = 3f;
    [SerializeField, Min(0f)] float _selectionBurstTrailsSpawnRateMultiplier = 3f;
    [SerializeField, Min(0f)] float _selectionBurstTrailsLifeTimeMultiplier = 1.5f;
    [SerializeField] float _restoredColorIntensity = 0f;
    [SerializeField] bool _choiceInputEnabled = true;

    [Header("Choice Prompt Breathing")]
    [SerializeField, Min(1f)] float _choiceBreathScaleMultiplier = 1.16f;
    [SerializeField, Min(1f)] float _choiceBreathVfxSizeMultiplier = 1.35f;
    [SerializeField, Min(1f)] float _choiceBreathSpawnRateMultiplier = 1.65f;
    [SerializeField, Min(1f)] float _choiceBreathTrailsSpawnRateMultiplier = 1.45f;

    [Header("VFX Properties")]
    [SerializeField] string _sizeProperty = "Size";
    [SerializeField] string _spawnRateProperty = "SpawnRate";
    [SerializeField] string _trailsSpawnRateProperty = "TrailsSpawnRate";
    [SerializeField] string _trailsLifeTimeProperty = "TrailsLifeTime";
    [SerializeField] string _colorProperty = "Color";
    [SerializeField, Range(0.05f, 1f)] float _hoveredVfxSizeMultiplier = 0.65f;
    [SerializeField, Min(0f)] float _spawnRateMultiplier = 1.5f;
    [SerializeField, Min(0f)] float _trailsSpawnRateMultiplier = 1.8f;
    [SerializeField, Min(0f)] float _trailsLifeTimeMultiplier = 1.25f;
    [SerializeField, Range(0f, 1f)] float _hoverColorBrighten = 0.35f;

    OrbRuntimeState[] _states;
    int _hoveredIndex = -1;
    float _hoverDuration;
    float _hoverLossDuration;
    float _nextStartAttemptTime;
    float _nextInvalidLogTime;
    string _lastInvalidGazeReason;
    Action<string> _permissionGrantedCallback;
    int _selectionEffectIndex = -1;
    float _selectionEffectElapsed;
    float _selectionDwellDuration;
    float _selectionTriggeredRealtime = float.NaN;
    int _selectionTriggeredFrame = -1;
    bool _selectionEffectActive;
    bool _waitingForGazeReleaseAfterSelection;
    bool _colorIntensityOverrideActive;
    float _currentColorIntensity;
    float _choiceBreathAmount;
    Coroutine _choicePromptBreathRoutine;

    public int hoveredIndex => _hoveredIndex;
    public float hoverDuration => _hoverDuration;
    public float dwellProgress => Mathf.Clamp01(_hoverDuration / Mathf.Max(0.05f, _dwellShrinkSeconds));
    public bool selectionEffectActive => _selectionEffectActive;
    public int selectionEffectIndex => _selectionEffectIndex;
    public bool choiceInputEnabled => _choiceInputEnabled;
    public float currentColorIntensity => _colorIntensityOverrideActive ? _currentColorIntensity : 0f;
    public string lastInvalidGazeReason => _lastInvalidGazeReason;

    public event Action<SelectionResult> SelectionCompleted;

    void Awake()
    {
        _permissionGrantedCallback = HandlePermissionGranted;
        EnsureSetup();
    }

    void OnEnable()
    {
        if (_permissionGrantedCallback == null)
        {
            _permissionGrantedCallback = HandlePermissionGranted;
        }

        OVRPermissionsRequester.PermissionGranted -= _permissionGrantedCallback;
        OVRPermissionsRequester.PermissionGranted += _permissionGrantedCallback;

        EnsureSetup();
        RequestPermissionIfNeeded();
        StartEyeTrackingIfNeeded();
    }

    void OnDisable()
    {
        OVRPermissionsRequester.PermissionGranted -= _permissionGrantedCallback;
        StopChoicePromptBreath();
        _selectionEffectActive = false;
        _selectionEffectIndex = -1;
        _selectionEffectElapsed = 0f;
        _selectionDwellDuration = 0f;
        _selectionTriggeredRealtime = float.NaN;
        _selectionTriggeredFrame = -1;
        _waitingForGazeReleaseAfterSelection = false;
        _colorIntensityOverrideActive = false;
        _choiceBreathAmount = 0f;
        RestoreAllOrbs(false);
    }

    void OnValidate()
    {
        _defaultHitRadiusMeters = Mathf.Max(0.01f, _defaultHitRadiusMeters);
        _autoHitRadiusScale = Mathf.Max(0f, _autoHitRadiusScale);
        _maxGazeDistanceMeters = Mathf.Max(0.01f, _maxGazeDistanceMeters);
        _dwellShrinkSeconds = Mathf.Max(0.05f, _dwellShrinkSeconds);
        _minimumHoveredScale = Mathf.Clamp(_minimumHoveredScale, 0.05f, 1f);
        _attackSpeed = Mathf.Max(0.01f, _attackSpeed);
        _releaseSpeed = Mathf.Max(0.01f, _releaseSpeed);
        _selectionDwellSeconds = Mathf.Max(0.05f, _selectionDwellSeconds);
        _selectionEffectSeconds = Mathf.Max(0.05f, _selectionEffectSeconds);
        _selectionBurstScaleMultiplier = Mathf.Max(1f, _selectionBurstScaleMultiplier);
        _selectionBurstVfxSizeMultiplier = Mathf.Max(1f, _selectionBurstVfxSizeMultiplier);
        _selectionBurstSpawnRateMultiplier = Mathf.Max(0f, _selectionBurstSpawnRateMultiplier);
        _selectionBurstTrailsSpawnRateMultiplier = Mathf.Max(0f, _selectionBurstTrailsSpawnRateMultiplier);
        _selectionBurstTrailsLifeTimeMultiplier = Mathf.Max(0f, _selectionBurstTrailsLifeTimeMultiplier);
        _choiceBreathScaleMultiplier = Mathf.Max(1f, _choiceBreathScaleMultiplier);
        _choiceBreathVfxSizeMultiplier = Mathf.Max(1f, _choiceBreathVfxSizeMultiplier);
        _choiceBreathSpawnRateMultiplier = Mathf.Max(1f, _choiceBreathSpawnRateMultiplier);
        _choiceBreathTrailsSpawnRateMultiplier = Mathf.Max(1f, _choiceBreathTrailsSpawnRateMultiplier);
        _minimumEyeConfidence = Mathf.Clamp01(_minimumEyeConfidence);
        _startRetryInterval = Mathf.Max(0.25f, _startRetryInterval);
        _invalidLogInterval = Mathf.Max(0.1f, _invalidLogInterval);
        _hoverLossGraceSeconds = Mathf.Max(0f, _hoverLossGraceSeconds);
    }

    void LateUpdate()
    {
        EnsureSetup();
        StartEyeTrackingIfNeeded();

        var deltaTime = GetDeltaTime();
        if (_selectionEffectActive)
        {
            UpdateSelectionEffect(deltaTime);
            return;
        }

        if (!_choiceInputEnabled)
        {
            _hoveredIndex = -1;
            _hoverDuration = 0f;
            _hoverLossDuration = 0f;
            ApplyOrbFeedback(deltaTime);
            return;
        }

        var previousHoveredIndex = _hoveredIndex;
        var gazedIndex = TryGetGazedOrbIndex(out _);
        var heldByGrace = false;

        if (_waitingForGazeReleaseAfterSelection)
        {
            if (gazedIndex < 0)
            {
                _waitingForGazeReleaseAfterSelection = false;
            }
            else
            {
                gazedIndex = -1;
            }
        }

        if (gazedIndex >= 0)
        {
            _hoveredIndex = gazedIndex;
            _hoverLossDuration = 0f;
        }
        else if (previousHoveredIndex >= 0 && _hoverLossGraceSeconds > 0f)
        {
            _hoverLossDuration += deltaTime;
            if (_hoverLossDuration <= _hoverLossGraceSeconds)
            {
                _hoveredIndex = previousHoveredIndex;
                heldByGrace = true;
            }
            else
            {
                _hoveredIndex = -1;
            }
        }
        else
        {
            _hoveredIndex = -1;
        }

        if (_hoveredIndex >= 0)
        {
            _hoverDuration = previousHoveredIndex == _hoveredIndex
                ? _hoverDuration + (heldByGrace ? 0f : deltaTime)
                : 0f;
        }
        else
        {
            _hoverDuration = 0f;
            _hoverLossDuration = 0f;
        }

        if (_hoveredIndex >= 0 && _hoverDuration >= _selectionDwellSeconds)
        {
            BeginSelectionEffect(_hoveredIndex);
            ApplySelectionEffect();
            return;
        }

        ApplyOrbFeedback(deltaTime);
    }

    void EnsureSetup()
    {
        if (_autoFindReferences && HasMissingGazeReferences())
        {
            AutoFindGazeReferences();
        }

        if (_autoCollectChildOrbs && (_orbs == null || _orbs.Length == 0))
        {
            AutoCollectOrbs();
        }

        if (_orbs == null)
        {
            _orbs = System.Array.Empty<OrbTarget>();
        }

        if (_states == null || _states.Length != _orbs.Length)
        {
            CacheRuntimeStates();
        }
    }

    void AutoCollectOrbs()
    {
        var visualEffects = GetComponentsInChildren<VisualEffect>(true);
        if (visualEffects == null || visualEffects.Length == 0)
        {
            _orbs = System.Array.Empty<OrbTarget>();
            return;
        }

        var targets = new System.Collections.Generic.List<OrbTarget>(visualEffects.Length);
        for (var i = 0; i < visualEffects.Length; i++)
        {
            var visualEffect = visualEffects[i];
            if (visualEffect == null)
            {
                continue;
            }

            var objectName = visualEffect.gameObject.name;
            if (objectName.IndexOf("Orb", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            targets.Add(new OrbTarget
            {
                label = objectName,
                target = visualEffect.transform,
                visualEffect = visualEffect,
                hitRadiusMeters = 0f
            });
        }

        _orbs = targets.ToArray();
    }

    void CacheRuntimeStates()
    {
        _states = new OrbRuntimeState[_orbs.Length];
        for (var i = 0; i < _orbs.Length; i++)
        {
            var state = new OrbRuntimeState();
            var target = _orbs[i] != null ? _orbs[i].target : null;
            var visualEffect = ResolveVisualEffect(_orbs[i]);

            if (target != null)
            {
                state.baseLocalScale = target.localScale;
                state.baseActiveSelf = target.gameObject.activeSelf;
            }

            if (visualEffect != null)
            {
                CacheVfxState(visualEffect, state);
            }

            _states[i] = state;
        }
    }

    void CacheVfxState(VisualEffect visualEffect, OrbRuntimeState state)
    {
        if (HasFloat(visualEffect, _sizeProperty))
        {
            state.hasBaseSize = true;
            state.baseSize = visualEffect.GetFloat(_sizeProperty);
        }

        if (HasFloat(visualEffect, _spawnRateProperty))
        {
            state.hasBaseSpawnRate = true;
            state.baseSpawnRate = visualEffect.GetFloat(_spawnRateProperty);
        }

        if (HasFloat(visualEffect, _trailsSpawnRateProperty))
        {
            state.hasBaseTrailsSpawnRate = true;
            state.baseTrailsSpawnRate = visualEffect.GetFloat(_trailsSpawnRateProperty);
        }

        if (HasFloat(visualEffect, _trailsLifeTimeProperty))
        {
            state.hasBaseTrailsLifeTime = true;
            state.baseTrailsLifeTime = visualEffect.GetFloat(_trailsLifeTimeProperty);
        }

        if (HasVector4(visualEffect, _colorProperty))
        {
            state.hasBaseColor = true;
            state.baseColor = visualEffect.GetVector4(_colorProperty);
        }
    }

    int TryGetGazedOrbIndex(out Vector3 worldHit)
    {
        worldHit = default;
        if (!TryGetGazeRaySamples(
                out var leftSample,
                out var hasLeftSample,
                out var rightSample,
                out var hasRightSample))
        {
            return -1;
        }

        if (_gazeRayEvaluationMode == GazeRayEvaluationMode.SingleEyeRay)
        {
            if (!TrySelectSingleEyeRaySample(
                    leftSample,
                    hasLeftSample,
                    rightSample,
                    hasRightSample,
                    out var singleSample))
            {
                return -1;
            }

            return TryGetGazedOrbIndexFromSamples(singleSample, true, default, false, out worldHit);
        }

        return TryGetGazedOrbIndexFromSamples(
            leftSample,
            hasLeftSample,
            rightSample,
            hasRightSample,
            out worldHit);
    }

    int TryGetGazedOrbIndexFromSamples(
        GazeRaySample firstSample,
        bool hasFirstSample,
        GazeRaySample secondSample,
        bool hasSecondSample,
        out Vector3 worldHit)
    {
        worldHit = default;
        var bestIndex = -1;
        var bestHitCount = 0;
        var bestDistance = float.MaxValue;
        var bestDistanceAlongRay = float.MaxValue;

        for (var i = 0; i < _orbs.Length; i++)
        {
            var target = _orbs[i] != null ? _orbs[i].target : null;
            if (target == null)
            {
                continue;
            }

            var radius = ResolveHitRadius(_orbs[i], target);
            var hitCount = 0;
            var distanceToCenterSum = 0f;
            var distanceAlongRaySum = 0f;
            var worldHitSum = Vector3.zero;

            AccumulateOrbHit(
                firstSample,
                hasFirstSample,
                target,
                radius,
                ref hitCount,
                ref distanceToCenterSum,
                ref distanceAlongRaySum,
                ref worldHitSum);
            AccumulateOrbHit(
                secondSample,
                hasSecondSample,
                target,
                radius,
                ref hitCount,
                ref distanceToCenterSum,
                ref distanceAlongRaySum,
                ref worldHitSum);

            if (hitCount == 0)
            {
                continue;
            }

            var averageDistanceToCenter = distanceToCenterSum / hitCount;
            var averageDistanceAlongRay = distanceAlongRaySum / hitCount;
            if (hitCount < bestHitCount)
            {
                continue;
            }

            if (hitCount == bestHitCount)
            {
                if (averageDistanceToCenter > bestDistance)
                {
                    continue;
                }

                if (Mathf.Approximately(averageDistanceToCenter, bestDistance) &&
                    averageDistanceAlongRay >= bestDistanceAlongRay)
                {
                    continue;
                }
            }

            bestHitCount = hitCount;
            bestDistance = averageDistanceToCenter;
            bestDistanceAlongRay = averageDistanceAlongRay;
            bestIndex = i;
            worldHit = worldHitSum / hitCount;
        }

        return bestIndex;
    }

    void AccumulateOrbHit(
        GazeRaySample sample,
        bool hasSample,
        Transform target,
        float radius,
        ref int hitCount,
        ref float distanceToCenterSum,
        ref float distanceAlongRaySum,
        ref Vector3 worldHitSum)
    {
        if (!hasSample)
        {
            return;
        }

        if (!TryGetRaySphereDistance(
                sample.ray,
                target.position,
                radius,
                out var distanceAlongRay,
                out var distanceToCenter))
        {
            return;
        }

        if (distanceAlongRay > _maxGazeDistanceMeters)
        {
            return;
        }

        hitCount++;
        distanceToCenterSum += distanceToCenter;
        distanceAlongRaySum += distanceAlongRay;
        worldHitSum += sample.ray.GetPoint(distanceAlongRay);
    }

    static bool TryGetRaySphereDistance(
        Ray ray,
        Vector3 center,
        float radius,
        out float distanceAlongRay,
        out float distanceToCenter)
    {
        var originToCenter = center - ray.origin;
        distanceAlongRay = Vector3.Dot(originToCenter, ray.direction.normalized);
        if (distanceAlongRay < 0f)
        {
            distanceToCenter = float.MaxValue;
            return false;
        }

        var closestPoint = ray.origin + ray.direction.normalized * distanceAlongRay;
        distanceToCenter = Vector3.Distance(closestPoint, center);
        return distanceToCenter <= Mathf.Max(0.01f, radius);
    }

    float ResolveHitRadius(OrbTarget orb, Transform target)
    {
        if (orb != null && orb.hitRadiusMeters > 0f)
        {
            return orb.hitRadiusMeters;
        }

        var scale = target != null ? target.lossyScale : Vector3.one;
        var largestScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Max(_defaultHitRadiusMeters, largestScale * _autoHitRadiusScale);
    }

    bool TryGetGazeRaySamples(
        out GazeRaySample leftSample,
        out bool hasLeftSample,
        out GazeRaySample rightSample,
        out bool hasRightSample)
    {
        leftSample = default;
        rightSample = default;
        hasLeftSample = false;
        hasRightSample = false;

        if (_autoFindReferences && HasMissingGazeReferences())
        {
            AutoFindGazeReferences();
        }

        if (_requireEyeTrackingEnabled && !IsEyeTrackingRuntimeUsable(out var runtimeReason))
        {
            RecordInvalidGaze(runtimeReason);
            return false;
        }

        hasLeftSample = TryCreateEyeRaySample(_leftEyeGaze, _leftGazeTransform, out leftSample);
        hasRightSample = TryCreateEyeRaySample(_rightEyeGaze, _rightGazeTransform, out rightSample);

        if (hasLeftSample || hasRightSample)
        {
            return true;
        }

        if (_requireEyeTrackingEnabled || !_allowHeadFallbackWhenEyeTrackingInvalid)
        {
            RecordInvalidGaze(BuildNoConfidentEyeSampleReason());
            return false;
        }

        RecordInvalidGaze(BuildNoConfidentEyeSampleReason() + "; using viewer forward fallback");
        if (TryGetViewerRay(out var viewerRay))
        {
            leftSample = new GazeRaySample(viewerRay, 1f);
            rightSample = new GazeRaySample(viewerRay, 1f);
            hasLeftSample = true;
            hasRightSample = true;
            return true;
        }

        return false;
    }

    bool TrySelectSingleEyeRaySample(
        GazeRaySample leftSample,
        bool hasLeftSample,
        GazeRaySample rightSample,
        bool hasRightSample,
        out GazeRaySample selectedSample)
    {
        selectedSample = default;

        switch (_singleEyeRaySource)
        {
            case SingleEyeRaySource.LeftEye:
                if (!hasLeftSample)
                {
                    return false;
                }

                selectedSample = leftSample;
                return true;

            case SingleEyeRaySource.HighestConfidence:
                if (hasLeftSample && hasRightSample)
                {
                    selectedSample = leftSample.confidence >= rightSample.confidence
                        ? leftSample
                        : rightSample;
                    return true;
                }

                if (hasLeftSample)
                {
                    selectedSample = leftSample;
                    return true;
                }

                if (hasRightSample)
                {
                    selectedSample = rightSample;
                    return true;
                }

                return false;

            default:
                if (!hasRightSample)
                {
                    return false;
                }

                selectedSample = rightSample;
                return true;
        }
    }

    bool TryGetViewerRay(out Ray ray)
    {
        var viewer = _centerEyeTransform != null
            ? _centerEyeTransform
            : Camera.main != null ? Camera.main.transform : null;
        if (viewer == null || viewer.forward.sqrMagnitude <= RayDirectionEpsilon)
        {
            ray = default;
            return false;
        }

        ray = new Ray(viewer.position, viewer.forward.normalized);
        return true;
    }

    bool TryCreateEyeRaySample(
        OVREyeGaze eyeGaze,
        Transform eyeTransform,
        out GazeRaySample sample)
    {
        sample = default;
        if (eyeGaze == null || !eyeGaze.enabled || eyeGaze.Confidence < _minimumEyeConfidence)
        {
            return false;
        }

        var gazeTransform = eyeGaze.transform != null ? eyeGaze.transform : eyeTransform;
        if (gazeTransform == null)
        {
            return false;
        }

        var direction = gazeTransform.forward;
        if (direction.sqrMagnitude <= RayDirectionEpsilon)
        {
            return false;
        }

        sample = new GazeRaySample(
            new Ray(gazeTransform.position, direction.normalized),
            eyeGaze.Confidence);
        return true;
    }

    void AutoFindGazeReferences()
    {
        var leftGazeObject = GameObject.Find(LeftEyeGazeName);
        var rightGazeObject = GameObject.Find(RightEyeGazeName);

        if (_leftEyeGaze == null && leftGazeObject != null)
        {
            _leftEyeGaze = leftGazeObject.GetComponent<OVREyeGaze>();
        }

        if (_rightEyeGaze == null && rightGazeObject != null)
        {
            _rightEyeGaze = rightGazeObject.GetComponent<OVREyeGaze>();
        }

        if (_leftGazeTransform == null)
        {
            _leftGazeTransform = leftGazeObject != null ? leftGazeObject.transform : FindTransformByName(LeftEyeGazeName);
        }

        if (_rightGazeTransform == null)
        {
            _rightGazeTransform = rightGazeObject != null ? rightGazeObject.transform : FindTransformByName(RightEyeGazeName);
        }

        if (_centerEyeTransform == null)
        {
            _centerEyeTransform = FindTransformByName(CenterEyeAnchorName);
        }
    }

    bool HasMissingGazeReferences()
    {
        return _leftEyeGaze == null ||
               _rightEyeGaze == null ||
               _leftGazeTransform == null ||
               _rightGazeTransform == null ||
               _centerEyeTransform == null;
    }

    void RequestPermissionIfNeeded()
    {
        if (!_requestEyeTrackingPermission || !Application.isPlaying)
        {
            return;
        }

        if (OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking))
        {
            return;
        }

        OVRPermissionsRequester.Request(new List<OVRPermissionsRequester.Permission>
        {
            OVRPermissionsRequester.Permission.EyeTracking
        });
    }

    void StartEyeTrackingIfNeeded()
    {
        if (!_startEyeTrackingIfNeeded || !Application.isPlaying)
        {
            return;
        }

        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking) ||
            !OVRPlugin.eyeTrackingSupported ||
            OVRPlugin.eyeTrackingEnabled)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < _nextStartAttemptTime)
        {
            return;
        }

        _nextStartAttemptTime = now + _startRetryInterval;
        var started = OVRPlugin.StartEyeTracking();
        if (!started)
        {
            RecordInvalidGaze("OVRPlugin.StartEyeTracking returned false");
        }
    }

    void HandlePermissionGranted(string permissionId)
    {
        if (permissionId != OVRPermissionsRequester.GetPermissionId(OVRPermissionsRequester.Permission.EyeTracking))
        {
            return;
        }

        _nextStartAttemptTime = 0f;
        StartEyeTrackingIfNeeded();
    }

    bool IsEyeTrackingRuntimeUsable(out string reason)
    {
        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking))
        {
            reason = "eye tracking permission is not granted";
            return false;
        }

        if (!OVRPlugin.eyeTrackingSupported)
        {
            reason = "OVRPlugin reports eye tracking is not supported";
            return false;
        }

        if (!OVRPlugin.eyeTrackingEnabled)
        {
            reason = "OVRPlugin eye tracking is not enabled";
            return false;
        }

        reason = null;
        return true;
    }

    string BuildNoConfidentEyeSampleReason()
    {
        return "no confident OVREyeGaze sample; left=" +
               DescribeEyeSample(_leftEyeGaze) +
               ", right=" +
               DescribeEyeSample(_rightEyeGaze) +
               ", minimumConfidence=" +
               _minimumEyeConfidence.ToString("0.00");
    }

    static string DescribeEyeSample(OVREyeGaze eyeGaze)
    {
        if (eyeGaze == null)
        {
            return "missing";
        }

        if (!eyeGaze.enabled)
        {
            return "disabled";
        }

        return eyeGaze.Confidence.ToString("0.000");
    }

    void RecordInvalidGaze(string reason)
    {
        _lastInvalidGazeReason = reason;
        if (!_logInvalidEyeTracking || !Application.isPlaying)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < _nextInvalidLogTime)
        {
            return;
        }

        _nextInvalidLogTime = now + _invalidLogInterval;
        Debug.LogWarning("[MeditationChoiceEyeGazeFeedback] Gaze ignored: " + reason, this);
    }

    public void SetChoiceInputEnabled(bool enabled)
    {
        _choiceInputEnabled = enabled;
        if (enabled)
        {
            return;
        }

        _hoveredIndex = -1;
        _hoverDuration = 0f;
        _hoverLossDuration = 0f;
    }

    public void SetOrbColorIntensity(float colorIntensity)
    {
        StopChoicePromptBreath();
        ApplyOrbColorIntensity(colorIntensity, true);
    }

    public void BeginChoicePrompt(
        float promptColorIntensity,
        float breathInSeconds,
        float breathOutSeconds,
        float breathMinimumAmount)
    {
        EnsureSetup();
        StopChoicePromptBreath();
        _choiceBreathAmount = 0f;
        SetChoiceInputEnabled(true);
        _choicePromptBreathRoutine = StartCoroutine(ChoicePromptBreathRoutine(
            promptColorIntensity,
            Mathf.Max(0.1f, breathInSeconds),
            Mathf.Max(0.1f, breathOutSeconds),
            Mathf.Clamp01(breathMinimumAmount)));
    }

    public void BeginChoicePrompt(float promptColorIntensity, int flashCount, float flashStepSeconds)
    {
        BeginChoicePrompt(promptColorIntensity, 1.1f, 1.55f, 0f);
    }

    public void CancelChoicePrompt(float normalColorIntensity)
    {
        StopChoicePromptBreath();
        SetChoiceInputEnabled(false);
        ApplyOrbColorIntensity(normalColorIntensity, true);
    }

    IEnumerator ChoicePromptBreathRoutine(
        float promptColorIntensity,
        float breathInSeconds,
        float breathOutSeconds,
        float breathMinimumAmount)
    {
        var lowIntensity = Mathf.Lerp(_restoredColorIntensity, promptColorIntensity, breathMinimumAmount);
        var fromIntensity = _colorIntensityOverrideActive ? _currentColorIntensity : _restoredColorIntensity;

        while (true)
        {
            yield return ApplyChoiceBreathTransition(fromIntensity, promptColorIntensity, _choiceBreathAmount, 1f, breathInSeconds);
            yield return ApplyChoiceBreathTransition(promptColorIntensity, lowIntensity, 1f, 0f, breathOutSeconds);
            fromIntensity = lowIntensity;
        }
    }

    IEnumerator ApplyChoiceBreathTransition(
        float fromIntensity,
        float toIntensity,
        float fromBreathAmount,
        float toBreathAmount,
        float duration)
    {
        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            var t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            var eased = t * t * (3f - 2f * t);
            _choiceBreathAmount = Mathf.Lerp(fromBreathAmount, toBreathAmount, eased);
            ApplyOrbColorIntensity(Mathf.Lerp(fromIntensity, toIntensity, eased), false);
            yield return null;
        }

        _choiceBreathAmount = toBreathAmount;
        ApplyOrbColorIntensity(toIntensity, false);
    }

    void StopChoicePromptBreath()
    {
        if (_choicePromptBreathRoutine == null)
        {
            return;
        }

        StopCoroutine(_choicePromptBreathRoutine);
        _choicePromptBreathRoutine = null;
        _choiceBreathAmount = 0f;
    }

    void ApplyOrbColorIntensity(float colorIntensity, bool restoreAllOrbs)
    {
        _currentColorIntensity = colorIntensity;
        _colorIntensityOverrideActive = true;
        if (restoreAllOrbs)
        {
            RestoreAllOrbs(true);
        }
        else
        {
            ApplyOrbFeedback(0f);
        }
    }

    void ApplyOrbFeedback(float deltaTime)
    {
        if (_states == null)
        {
            return;
        }

        for (var i = 0; i < _orbs.Length; i++)
        {
            var state = _states[i];
            if (state == null)
            {
                continue;
            }

            var targetStrength = i == _hoveredIndex ? 1f : 0f;
            var speed = targetStrength > state.strength ? _attackSpeed : _releaseSpeed;
            state.strength = Mathf.MoveTowards(state.strength, targetStrength, speed * deltaTime);

            var dwellAmount = i == _hoveredIndex ? dwellProgress : 0f;
            ApplyTransformScale(_orbs[i], state, dwellAmount, deltaTime);
            ApplyVisualEffect(_orbs[i], state, dwellAmount);
        }
    }

    void BeginSelectionEffect(int selectedIndex)
    {
        StopChoicePromptBreath();
        _selectionEffectActive = true;
        _selectionEffectIndex = selectedIndex;
        _selectionEffectElapsed = 0f;
        _selectionDwellDuration = _hoverDuration;
        _selectionTriggeredRealtime = Time.realtimeSinceStartup;
        _selectionTriggeredFrame = Time.frameCount;
        _choiceInputEnabled = false;
        _waitingForGazeReleaseAfterSelection = false;
        _hoveredIndex = -1;
        _hoverDuration = 0f;
        _hoverLossDuration = 0f;

        if (_states == null)
        {
            return;
        }

        for (var i = 0; i < _states.Length; i++)
        {
            if (_states[i] != null)
            {
                _states[i].strength = 0f;
            }
        }
    }

    void UpdateSelectionEffect(float deltaTime)
    {
        _selectionEffectElapsed += deltaTime;
        if (_selectionEffectElapsed >= _selectionEffectSeconds)
        {
            CompleteSelectionEffect();
            return;
        }

        ApplySelectionEffect();
    }

    void ApplySelectionEffect()
    {
        if (_orbs == null || _states == null)
        {
            return;
        }

        var progress = Mathf.Clamp01(_selectionEffectElapsed / Mathf.Max(0.05f, _selectionEffectSeconds));
        var burstAmount = Mathf.Sin(progress * Mathf.PI);

        for (var i = 0; i < _orbs.Length && i < _states.Length; i++)
        {
            var orb = _orbs[i];
            var state = _states[i];
            if (orb == null || state == null)
            {
                continue;
            }

            if (i == _selectionEffectIndex)
            {
                SetOrbActive(orb, true);
                ApplySelectedOrbBurst(orb, state, burstAmount);
            }
            else
            {
                ApplyHiddenOrbDuringSelection(orb, state);
            }
        }
    }

    void ApplySelectedOrbBurst(OrbTarget orb, OrbRuntimeState state, float burstAmount)
    {
        if (orb.target != null)
        {
            var scaleMultiplier = Mathf.Lerp(1f, _selectionBurstScaleMultiplier, burstAmount);
            orb.target.localScale = state.baseLocalScale * scaleMultiplier;
        }

        var visualEffect = ResolveVisualEffect(orb);
        if (visualEffect == null)
        {
            return;
        }

        if (state.hasBaseSize)
        {
            visualEffect.SetFloat(
                _sizeProperty,
                state.baseSize * Mathf.Lerp(1f, _selectionBurstVfxSizeMultiplier, burstAmount));
        }

        if (state.hasBaseSpawnRate)
        {
            visualEffect.SetFloat(
                _spawnRateProperty,
                Mathf.Lerp(state.baseSpawnRate, state.baseSpawnRate * _selectionBurstSpawnRateMultiplier, burstAmount));
        }

        if (state.hasBaseTrailsSpawnRate)
        {
            visualEffect.SetFloat(
                _trailsSpawnRateProperty,
                Mathf.Lerp(
                    state.baseTrailsSpawnRate,
                    state.baseTrailsSpawnRate * _selectionBurstTrailsSpawnRateMultiplier,
                    burstAmount));
        }

        if (state.hasBaseTrailsLifeTime)
        {
            visualEffect.SetFloat(
                _trailsLifeTimeProperty,
                Mathf.Lerp(
                    state.baseTrailsLifeTime,
                    state.baseTrailsLifeTime * _selectionBurstTrailsLifeTimeMultiplier,
                    burstAmount));
        }

        if (state.hasBaseColor)
        {
            visualEffect.SetVector4(_colorProperty, Color.Lerp(GetRestoredBaseColor(state), Color.white, burstAmount));
        }
    }

    void ApplyHiddenOrbDuringSelection(OrbTarget orb, OrbRuntimeState state)
    {
        var visualEffect = ResolveVisualEffect(orb);
        if (visualEffect != null)
        {
            if (state.hasBaseSize)
            {
                visualEffect.SetFloat(_sizeProperty, 0f);
            }

            if (state.hasBaseSpawnRate)
            {
                visualEffect.SetFloat(_spawnRateProperty, 0f);
            }

            if (state.hasBaseTrailsSpawnRate)
            {
                visualEffect.SetFloat(_trailsSpawnRateProperty, 0f);
            }
        }

        SetOrbActive(orb, false);
    }

    void CompleteSelectionEffect()
    {
        var completedIndex = _selectionEffectIndex;
        var completedLabel = GetOrbLabel(completedIndex);
        var completedEffectSeconds = _selectionEffectElapsed;
        var triggeredRealtime = float.IsNaN(_selectionTriggeredRealtime)
            ? Time.realtimeSinceStartup
            : _selectionTriggeredRealtime;
        var triggeredFrame = _selectionTriggeredFrame > 0 ? _selectionTriggeredFrame : Time.frameCount;
        _selectionEffectActive = false;
        _selectionEffectIndex = -1;
        _selectionEffectElapsed = 0f;
        _hoveredIndex = -1;
        _hoverDuration = 0f;
        _hoverLossDuration = 0f;
        _waitingForGazeReleaseAfterSelection = true;
        _choiceInputEnabled = false;
        _currentColorIntensity = _restoredColorIntensity;
        _colorIntensityOverrideActive = true;

        if (_states != null)
        {
            for (var i = 0; i < _states.Length; i++)
            {
                if (_states[i] != null)
                {
                    _states[i].strength = 0f;
                }
            }
        }

        RestoreAllOrbs(true);
        SelectionCompleted?.Invoke(new SelectionResult(
            completedIndex,
            completedLabel,
            _selectionDwellDuration,
            completedEffectSeconds,
            triggeredRealtime,
            triggeredFrame,
            Time.realtimeSinceStartup,
            Time.frameCount));
        _selectionDwellDuration = 0f;
        _selectionTriggeredRealtime = float.NaN;
        _selectionTriggeredFrame = -1;
    }

    void ApplyTransformScale(OrbTarget orb, OrbRuntimeState state, float dwellAmount, float deltaTime)
    {
        var target = orb != null ? orb.target : null;
        if (target == null)
        {
            return;
        }

        var shrink = Mathf.Lerp(1f, _minimumHoveredScale, Mathf.Clamp01(dwellAmount));
        var breathScale = Mathf.Lerp(1f, _choiceBreathScaleMultiplier, Mathf.Clamp01(_choiceBreathAmount));
        var targetScale = state.baseLocalScale * shrink * breathScale;
        var t = 1f - Mathf.Exp(-Mathf.Max(0.01f, _attackSpeed + _releaseSpeed) * deltaTime);
        target.localScale = Vector3.Lerp(target.localScale, targetScale, t);
    }

    void ApplyVisualEffect(OrbTarget orb, OrbRuntimeState state, float dwellAmount)
    {
        var visualEffect = ResolveVisualEffect(orb);
        if (visualEffect == null)
        {
            return;
        }

        var strength = Mathf.Clamp01(state.strength);
        var shrink = Mathf.Lerp(1f, _hoveredVfxSizeMultiplier, Mathf.Clamp01(dwellAmount));
        var baseColor = GetRestoredBaseColor(state);
        var breathAmount = Mathf.Clamp01(_choiceBreathAmount);
        var breathSize = Mathf.Lerp(1f, _choiceBreathVfxSizeMultiplier, breathAmount);
        var breathSpawnRate = Mathf.Lerp(1f, _choiceBreathSpawnRateMultiplier, breathAmount);
        var breathTrailsSpawnRate = Mathf.Lerp(1f, _choiceBreathTrailsSpawnRateMultiplier, breathAmount);

        if (state.hasBaseSize)
        {
            visualEffect.SetFloat(_sizeProperty, state.baseSize * shrink * breathSize);
        }

        if (state.hasBaseSpawnRate)
        {
            visualEffect.SetFloat(
                _spawnRateProperty,
                Mathf.Lerp(state.baseSpawnRate, state.baseSpawnRate * _spawnRateMultiplier, strength) * breathSpawnRate);
        }

        if (state.hasBaseTrailsSpawnRate)
        {
            visualEffect.SetFloat(
                _trailsSpawnRateProperty,
                Mathf.Lerp(state.baseTrailsSpawnRate, state.baseTrailsSpawnRate * _trailsSpawnRateMultiplier, strength) * breathTrailsSpawnRate);
        }

        if (state.hasBaseTrailsLifeTime)
        {
            visualEffect.SetFloat(
                _trailsLifeTimeProperty,
                Mathf.Lerp(state.baseTrailsLifeTime, state.baseTrailsLifeTime * _trailsLifeTimeMultiplier, strength));
        }

        if (state.hasBaseColor)
        {
            var targetColor = Color.Lerp(baseColor, Color.white, _hoverColorBrighten * strength);
            visualEffect.SetVector4(_colorProperty, targetColor);
        }
    }

    void RestoreAllOrbs(bool useRestoredColorIntensity)
    {
        if (_orbs == null || _states == null)
        {
            return;
        }

        for (var i = 0; i < _orbs.Length && i < _states.Length; i++)
        {
            var orb = _orbs[i];
            var state = _states[i];
            if (orb == null || state == null)
            {
                continue;
            }

            if (orb.target != null)
            {
                orb.target.gameObject.SetActive(state.baseActiveSelf);
                orb.target.localScale = state.baseLocalScale;
            }

            var visualEffect = ResolveVisualEffect(orb);
            if (visualEffect == null)
            {
                continue;
            }

            if (state.hasBaseSize)
            {
                visualEffect.SetFloat(_sizeProperty, state.baseSize);
            }

            if (state.hasBaseSpawnRate)
            {
                visualEffect.SetFloat(_spawnRateProperty, state.baseSpawnRate);
            }

            if (state.hasBaseTrailsSpawnRate)
            {
                visualEffect.SetFloat(_trailsSpawnRateProperty, state.baseTrailsSpawnRate);
            }

            if (state.hasBaseTrailsLifeTime)
            {
                visualEffect.SetFloat(_trailsLifeTimeProperty, state.baseTrailsLifeTime);
            }

            if (state.hasBaseColor)
            {
                visualEffect.SetVector4(
                    _colorProperty,
                    useRestoredColorIntensity ? ApplyColorIntensity(state.baseColor, _currentColorIntensity) : state.baseColor);
            }
        }
    }

    void SetOrbActive(OrbTarget orb, bool active)
    {
        var target = orb != null ? orb.target : null;
        if (target == null || target.gameObject.activeSelf == active)
        {
            return;
        }

        target.gameObject.SetActive(active);
    }

    string GetOrbLabel(int index)
    {
        if (_orbs == null || index < 0 || index >= _orbs.Length || _orbs[index] == null)
        {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(_orbs[index].label))
        {
            return _orbs[index].label;
        }

        return _orbs[index].target != null ? _orbs[index].target.gameObject.name : string.Empty;
    }

    Color GetRestoredBaseColor(OrbRuntimeState state)
    {
        if (state == null)
        {
            return Color.white;
        }

        return _colorIntensityOverrideActive
            ? ApplyColorIntensity(state.baseColor, _currentColorIntensity)
            : state.baseColor;
    }

    static Color ApplyColorIntensity(Color color, float intensity)
    {
        var multiplier = Mathf.Pow(2f, intensity);
        return new Color(
            color.r * multiplier,
            color.g * multiplier,
            color.b * multiplier,
            color.a);
    }

    static VisualEffect ResolveVisualEffect(OrbTarget orb)
    {
        if (orb == null)
        {
            return null;
        }

        if (orb.visualEffect != null)
        {
            return orb.visualEffect;
        }

        return orb.target != null ? orb.target.GetComponent<VisualEffect>() : null;
    }

    static bool HasFloat(VisualEffect visualEffect, string propertyName)
    {
        return visualEffect != null &&
               !string.IsNullOrEmpty(propertyName) &&
               visualEffect.HasFloat(propertyName);
    }

    static bool HasVector4(VisualEffect visualEffect, string propertyName)
    {
        return visualEffect != null &&
               !string.IsNullOrEmpty(propertyName) &&
               visualEffect.HasVector4(propertyName);
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

    float GetDeltaTime()
    {
        if (!Application.isPlaying)
        {
            return 1f / 60f;
        }

        return _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
