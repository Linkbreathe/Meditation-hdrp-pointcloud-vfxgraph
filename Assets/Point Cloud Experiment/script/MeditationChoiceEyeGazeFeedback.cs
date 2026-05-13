using System;
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

    public int hoveredIndex => _hoveredIndex;
    public float hoverDuration => _hoverDuration;
    public float dwellProgress => Mathf.Clamp01(_hoverDuration / Mathf.Max(0.05f, _dwellShrinkSeconds));
    public string lastInvalidGazeReason => _lastInvalidGazeReason;

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
        RestoreAllOrbs();
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
        var previousHoveredIndex = _hoveredIndex;
        var gazedIndex = TryGetGazedOrbIndex(out _);
        var heldByGrace = false;

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

    void ApplyTransformScale(OrbTarget orb, OrbRuntimeState state, float dwellAmount, float deltaTime)
    {
        var target = orb != null ? orb.target : null;
        if (target == null)
        {
            return;
        }

        var shrink = Mathf.Lerp(1f, _minimumHoveredScale, Mathf.Clamp01(dwellAmount));
        var targetScale = state.baseLocalScale * shrink;
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

        if (state.hasBaseSize)
        {
            visualEffect.SetFloat(_sizeProperty, state.baseSize * shrink);
        }

        if (state.hasBaseSpawnRate)
        {
            visualEffect.SetFloat(
                _spawnRateProperty,
                Mathf.Lerp(state.baseSpawnRate, state.baseSpawnRate * _spawnRateMultiplier, strength));
        }

        if (state.hasBaseTrailsSpawnRate)
        {
            visualEffect.SetFloat(
                _trailsSpawnRateProperty,
                Mathf.Lerp(state.baseTrailsSpawnRate, state.baseTrailsSpawnRate * _trailsSpawnRateMultiplier, strength));
        }

        if (state.hasBaseTrailsLifeTime)
        {
            visualEffect.SetFloat(
                _trailsLifeTimeProperty,
                Mathf.Lerp(state.baseTrailsLifeTime, state.baseTrailsLifeTime * _trailsLifeTimeMultiplier, strength));
        }

        if (state.hasBaseColor)
        {
            var targetColor = Color.Lerp(state.baseColor, Color.white, _hoverColorBrighten * strength);
            visualEffect.SetVector4(_colorProperty, targetColor);
        }
    }

    void RestoreAllOrbs()
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
                visualEffect.SetVector4(_colorProperty, state.baseColor);
            }
        }
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
