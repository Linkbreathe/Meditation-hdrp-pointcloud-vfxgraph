using UnityEngine;
using UnityEngine.VFX;

[ExecuteAlways]
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
[RequireComponent(typeof(VisualEffect))]
[AddComponentMenu("Point Cloud/Gaze VFX Disturbance Driver")]
public sealed class GazeVfxDisturbanceDriver : MonoBehaviour
{
    public const string DefaultGazeWorldHitProperty = "Gaze Hit Position";
    public const string DefaultGazeLocalHitProperty = "Gaze Hit Local Position";
    public const string DefaultGazeRadiusProperty = "Gaze Radius";
    public const string DefaultGazeStrengthProperty = "Gaze Strength";
    public const string DefaultGazeActiveProperty = "Gaze Active";

    const string DefaultRightEyeGazeName = "[BuildingBlock] Eye Gaze Right";
    const string DefaultLeftEyeGazeName = "[BuildingBlock] Eye Gaze Left";
    const string DefaultCenterEyeName = "CenterEyeAnchor";
    const float MinimumRadius = 0.001f;
    const float RayDirectionEpsilon = 0.0001f;

    public enum GazeSourceMode
    {
        SingleTransform,
        AverageEyes
    }

    public enum LocalPlaneNormal
    {
        X,
        Y,
        Z
    }

    public enum RegionStrengthMode
    {
        ConstantInsideRegion,
        FadeAtOuterEdge
    }

    [Header("References")]
    [SerializeField] VisualEffect _visualEffect;
    [SerializeField] Transform _targetTransform;

    [Header("Gaze Source")]
    [SerializeField] GazeSourceMode _gazeSourceMode = GazeSourceMode.AverageEyes;
    [Tooltip("Used by Single Transform mode and as the fallback gaze transform.")]
    [SerializeField] Transform _gazeTransform;
    [SerializeField] Transform _leftGazeTransform;
    [SerializeField] Transform _rightGazeTransform;
    [Tooltip("Origin for Average Eyes mode. Use CenterEyeAnchor for Meta eye gaze.")]
    [SerializeField] Transform _rayOriginTransform;
    [SerializeField] bool _autoFindGazeTransform = true;
    [SerializeField] string _preferredGazeObjectName = DefaultRightEyeGazeName;
    [SerializeField] string _fallbackGazeObjectName = DefaultLeftEyeGazeName;
    [SerializeField] string _leftGazeObjectName = DefaultLeftEyeGazeName;
    [SerializeField] string _rightGazeObjectName = DefaultRightEyeGazeName;
    [SerializeField] string _rayOriginObjectName = DefaultCenterEyeName;
    [SerializeField] string _cameraFallbackName = DefaultCenterEyeName;

    [Header("Target Region")]
    [SerializeField] LocalPlaneNormal _paintingPlaneNormal = LocalPlaneNormal.Z;
    [SerializeField] Vector3 _regionCenterLocal = Vector3.zero;
    [Tooltip("Large hit area on the painting plane. Make this cover the whole artwork.")]
    [SerializeField, Min(MinimumRadius)] float _regionRadiusLocal = 5f;
    [Tooltip("Soft fade only near the outer edge of the hit area.")]
    [SerializeField, Min(0f)] float _softEdgeLocal = 0.5f;
    [Tooltip("Small VFX mask radius around the gaze hit. This is sent to the VFX Graph as Gaze Radius.")]
    [SerializeField, Min(MinimumRadius)] float _brushRadiusLocal = 1f;
    [SerializeField, Min(0.01f)] float _maxRayDistance = 80f;
    [SerializeField] RegionStrengthMode _regionStrengthMode = RegionStrengthMode.ConstantInsideRegion;

    [Header("Response")]
    [SerializeField, Min(0.01f)] float _attackSpeed = 22f;
    [SerializeField, Min(0.01f)] float _releaseSpeed = 10f;
    [Tooltip("Higher values follow gaze faster. Set to 0 to disable hit-position smoothing.")]
    [SerializeField, Min(0f)] float _hitPositionSmoothingSpeed = 30f;
    [SerializeField] bool _useUnscaledTime;

    [Header("Local VFX Properties")]
    [SerializeField] string _gazeWorldHitProperty = DefaultGazeWorldHitProperty;
    [SerializeField] string _gazeLocalHitProperty = DefaultGazeLocalHitProperty;
    [SerializeField] string _gazeRadiusProperty = DefaultGazeRadiusProperty;
    [SerializeField] string _gazeStrengthProperty = DefaultGazeStrengthProperty;
    [SerializeField] string _gazeActiveProperty = DefaultGazeActiveProperty;

    [Header("Existing Pearl Lady Fallback")]
    [SerializeField] bool _driveExistingGlobalControls = true;
    [SerializeField] string _particleIntensityProperty = MonaLisaVfxController.DefaultParticleIntensityProperty;
    [SerializeField] string _particleFrequencyProperty = MonaLisaVfxController.DefaultParticleFrequencyProperty;
    [SerializeField] string _particleDragProperty = MonaLisaVfxController.DefaultParticleDragProperty;
    [SerializeField, Min(0f)] float _baseParticleIntensity = 0.01f;
    [SerializeField, Min(0f)] float _disturbedParticleIntensity = 0.16f;
    [SerializeField, Min(0f)] float _baseParticleFrequency = 0.01f;
    [SerializeField, Min(0f)] float _disturbedParticleFrequency = 0.2f;
    [SerializeField, Min(0f)] float _baseParticleDrag = 1f;
    [SerializeField, Min(0f)] float _disturbedParticleDrag = 0.25f;

    [Header("Diagnostics")]
    [SerializeField] bool _drawGizmos = true;

    float _currentStrength;
    Vector3 _lastWorldHit;
    Vector3 _lastLocalHit;
    bool _hasLastHit;
    Ray _lastGazeRay;
    bool _hasLastGazeRay;

    public float currentStrength => _currentStrength;
    public bool hasLastHit => _hasLastHit;
    public Vector3 lastWorldHit => _lastWorldHit;
    public Vector3 lastLocalHit => _lastLocalHit;
    public bool hasLastGazeRay => _hasLastGazeRay;
    public Ray lastGazeRay => _lastGazeRay;

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        _targetTransform = transform;
        FindGazeReferences();
    }

    void OnEnable()
    {
        EnsureReferences();
    }

    void OnValidate()
    {
        _regionRadiusLocal = Mathf.Max(MinimumRadius, _regionRadiusLocal);
        _softEdgeLocal = Mathf.Max(0f, _softEdgeLocal);
        _brushRadiusLocal = Mathf.Max(MinimumRadius, _brushRadiusLocal);
        _maxRayDistance = Mathf.Max(0.01f, _maxRayDistance);
        _attackSpeed = Mathf.Max(0.01f, _attackSpeed);
        _releaseSpeed = Mathf.Max(0.01f, _releaseSpeed);
        _hitPositionSmoothingSpeed = Mathf.Max(0f, _hitPositionSmoothingSpeed);
        _baseParticleIntensity = Mathf.Max(0f, _baseParticleIntensity);
        _disturbedParticleIntensity = Mathf.Max(0f, _disturbedParticleIntensity);
        _baseParticleFrequency = Mathf.Max(0f, _baseParticleFrequency);
        _disturbedParticleFrequency = Mathf.Max(0f, _disturbedParticleFrequency);
        _baseParticleDrag = Mathf.Max(0f, _baseParticleDrag);
        _disturbedParticleDrag = Mathf.Max(0f, _disturbedParticleDrag);
        EnsureReferences();
    }

    void LateUpdate()
    {
        EnsureReferences();
        if (_visualEffect == null)
        {
            return;
        }

        var deltaTime = GetDeltaTime();
        var targetStrength = 0f;
        if (TryEvaluateGazeHit(out var worldHit, out var localHit, out var hitStrength))
        {
            StoreHit(worldHit, localHit, deltaTime);
            targetStrength = hitStrength;
        }

        var speed = targetStrength > _currentStrength ? _attackSpeed : _releaseSpeed;
        _currentStrength = Mathf.MoveTowards(_currentStrength, targetStrength, speed * deltaTime);

        ApplyToVisualEffect(_currentStrength);
    }

    public bool TryEvaluateGazeHit(out Vector3 worldHit, out Vector3 localHit, out float strength)
    {
        worldHit = default;
        localHit = default;
        strength = 0f;

        if (_targetTransform == null || !TryGetGazeRay(out var ray))
        {
            return false;
        }

        _lastGazeRay = ray;
        _hasLastGazeRay = true;

        var planePoint = _targetTransform.TransformPoint(_regionCenterLocal);
        var planeNormal = GetWorldPlaneNormal();
        var plane = new Plane(planeNormal, planePoint);

        if (!plane.Raycast(ray, out var distance) || distance < 0f || distance > _maxRayDistance)
        {
            return false;
        }

        worldHit = ray.GetPoint(distance);
        localHit = _targetTransform.InverseTransformPoint(worldHit);

        var localDistance = GetPlanarDistance(localHit, _regionCenterLocal, _paintingPlaneNormal);
        if (localDistance > _regionRadiusLocal)
        {
            return false;
        }

        strength = CalculateRegionStrength(localDistance);
        return strength > 0f;
    }

    void StoreHit(Vector3 worldHit, Vector3 localHit, float deltaTime)
    {
        if (!_hasLastHit || _hitPositionSmoothingSpeed <= 0f)
        {
            _lastLocalHit = localHit;
            _lastWorldHit = worldHit;
        }
        else
        {
            var t = 1f - Mathf.Exp(-_hitPositionSmoothingSpeed * deltaTime);
            _lastLocalHit = Vector3.Lerp(_lastLocalHit, localHit, t);
            _lastWorldHit = _targetTransform != null
                ? _targetTransform.TransformPoint(_lastLocalHit)
                : Vector3.Lerp(_lastWorldHit, worldHit, t);
        }

        _hasLastHit = true;
    }

    float CalculateRegionStrength(float localDistance)
    {
        if (_regionStrengthMode == RegionStrengthMode.ConstantInsideRegion)
        {
            return 1f;
        }

        var innerRadius = Mathf.Max(0f, _regionRadiusLocal - _softEdgeLocal);
        var strength = localDistance <= innerRadius
            ? 1f
            : 1f - Mathf.InverseLerp(innerRadius, _regionRadiusLocal, localDistance);
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(strength));
    }

    void EnsureReferences()
    {
        if (_visualEffect == null)
        {
            _visualEffect = GetComponent<VisualEffect>();
        }

        if (_targetTransform == null)
        {
            _targetTransform = transform;
        }

        if (_autoFindGazeTransform)
        {
            FindGazeReferences();
        }
    }

    void FindGazeReferences()
    {
        if (_gazeSourceMode == GazeSourceMode.AverageEyes)
        {
            if (_leftGazeTransform == null)
            {
                _leftGazeTransform = FindTransformByName(_leftGazeObjectName);
            }

            if (_rightGazeTransform == null)
            {
                _rightGazeTransform = FindTransformByName(_rightGazeObjectName);
            }

            if (_rayOriginTransform == null)
            {
                _rayOriginTransform = FindTransformByName(_rayOriginObjectName);
            }
        }

        if (_gazeTransform == null)
        {
            _gazeTransform = FindTransformByName(_preferredGazeObjectName);
        }

        if (_gazeTransform == null)
        {
            _gazeTransform = FindTransformByName(_fallbackGazeObjectName);
        }

        if (_rayOriginTransform == null)
        {
            _rayOriginTransform = FindTransformByName(_cameraFallbackName);
        }

        if (_gazeTransform == null && Camera.main != null)
        {
            _gazeTransform = Camera.main.transform;
        }
    }

    bool TryGetGazeRay(out Ray ray)
    {
        if (_gazeSourceMode == GazeSourceMode.AverageEyes
            && TryGetAverageEyesRay(out ray))
        {
            return true;
        }

        if (_gazeTransform == null)
        {
            ray = default;
            return false;
        }

        ray = new Ray(_gazeTransform.position, _gazeTransform.forward);
        return ray.direction.sqrMagnitude > RayDirectionEpsilon;
    }

    bool TryGetAverageEyesRay(out Ray ray)
    {
        var directionSum = Vector3.zero;
        var positionSum = Vector3.zero;
        var eyeCount = 0;

        AddEyeSample(_leftGazeTransform, ref directionSum, ref positionSum, ref eyeCount);
        AddEyeSample(_rightGazeTransform, ref directionSum, ref positionSum, ref eyeCount);

        if (eyeCount == 0 || directionSum.sqrMagnitude <= RayDirectionEpsilon)
        {
            ray = default;
            return false;
        }

        var origin = _rayOriginTransform != null
            ? _rayOriginTransform.position
            : positionSum / eyeCount;
        ray = new Ray(origin, directionSum.normalized);
        return true;
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

    void ApplyToVisualEffect(float strength)
    {
        var active = strength > 0.001f;
        if (_hasLastHit)
        {
            TrySetVector3(_gazeWorldHitProperty, _lastWorldHit);
            TrySetVector3(_gazeLocalHitProperty, _lastLocalHit);
        }

        TrySetFloat(_gazeRadiusProperty, _brushRadiusLocal);
        TrySetFloat(_gazeStrengthProperty, strength);
        TrySetBool(_gazeActiveProperty, active);

        if (!_driveExistingGlobalControls)
        {
            return;
        }

        TrySetFloat(
            _particleIntensityProperty,
            Mathf.Lerp(_baseParticleIntensity, _disturbedParticleIntensity, strength));
        TrySetFloat(
            _particleFrequencyProperty,
            Mathf.Lerp(_baseParticleFrequency, _disturbedParticleFrequency, strength));
        TrySetFloat(
            _particleDragProperty,
            Mathf.Lerp(_baseParticleDrag, _disturbedParticleDrag, strength));
    }

    bool TrySetFloat(string propertyName, float value)
    {
        if (string.IsNullOrEmpty(propertyName) || !_visualEffect.HasFloat(propertyName))
        {
            return false;
        }

        _visualEffect.SetFloat(propertyName, value);
        return true;
    }

    bool TrySetVector3(string propertyName, Vector3 value)
    {
        if (string.IsNullOrEmpty(propertyName) || !_visualEffect.HasVector3(propertyName))
        {
            return false;
        }

        _visualEffect.SetVector3(propertyName, value);
        return true;
    }

    bool TrySetBool(string propertyName, bool value)
    {
        if (string.IsNullOrEmpty(propertyName) || !_visualEffect.HasBool(propertyName))
        {
            return false;
        }

        _visualEffect.SetBool(propertyName, value);
        return true;
    }

    float GetDeltaTime()
    {
        if (!Application.isPlaying)
        {
            return 1f / 60f;
        }

        return _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    Vector3 GetWorldPlaneNormal()
    {
        return _targetTransform.TransformDirection(GetLocalAxis(_paintingPlaneNormal)).normalized;
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

    static Vector3 GetLocalAxis(LocalPlaneNormal normal)
    {
        switch (normal)
        {
            case LocalPlaneNormal.X:
                return Vector3.right;
            case LocalPlaneNormal.Y:
                return Vector3.up;
            default:
                return Vector3.forward;
        }
    }

    static float GetPlanarDistance(Vector3 localPoint, Vector3 localCenter, LocalPlaneNormal normal)
    {
        switch (normal)
        {
            case LocalPlaneNormal.X:
                return Vector2.Distance(
                    new Vector2(localPoint.y, localPoint.z),
                    new Vector2(localCenter.y, localCenter.z));
            case LocalPlaneNormal.Y:
                return Vector2.Distance(
                    new Vector2(localPoint.x, localPoint.z),
                    new Vector2(localCenter.x, localCenter.z));
            default:
                return Vector2.Distance(
                    new Vector2(localPoint.x, localPoint.y),
                    new Vector2(localCenter.x, localCenter.y));
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!_drawGizmos)
        {
            return;
        }

        EnsureReferences();

        if (_targetTransform != null)
        {
            Gizmos.color = new Color(0.1f, 0.85f, 1f, 0.35f);
            DrawLocalCircleGizmo(_regionCenterLocal, _regionRadiusLocal);
        }

        if (TryGetGazeRay(out var ray))
        {
            Gizmos.color = _currentStrength > 0.001f ? Color.cyan : new Color(1f, 1f, 1f, 0.35f);
            Gizmos.DrawRay(ray.origin, ray.direction * Mathf.Min(_maxRayDistance, 12f));
        }

        if (_hasLastHit)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_lastWorldHit, 0.05f);

            if (_targetTransform != null)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.8f);
                DrawLocalCircleGizmo(_lastLocalHit, _brushRadiusLocal);
            }
        }
    }

    void DrawLocalCircleGizmo(Vector3 localCenter, float radius)
    {
        const int segmentCount = 48;
        var previous = TransformLocalCirclePoint(localCenter, radius, segmentCount - 1);
        for (var i = 0; i < segmentCount; i++)
        {
            var current = TransformLocalCirclePoint(localCenter, radius, i);
            Gizmos.DrawLine(previous, current);
            previous = current;
        }
    }

    Vector3 TransformLocalCirclePoint(Vector3 localCenter, float radius, int index)
    {
        const int segmentCount = 48;
        var angle = index / (float)segmentCount * Mathf.PI * 2f;
        var localOffset = Vector3.zero;

        switch (_paintingPlaneNormal)
        {
            case LocalPlaneNormal.X:
                localOffset.y = Mathf.Cos(angle) * radius;
                localOffset.z = Mathf.Sin(angle) * radius;
                break;
            case LocalPlaneNormal.Y:
                localOffset.x = Mathf.Cos(angle) * radius;
                localOffset.z = Mathf.Sin(angle) * radius;
                break;
            default:
                localOffset.x = Mathf.Cos(angle) * radius;
                localOffset.y = Mathf.Sin(angle) * radius;
                break;
        }

        return _targetTransform.TransformPoint(localCenter + localOffset);
    }
}
