using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public struct WaterLiliesTrackingSample
{
    public bool headsetPresenceAvailable;
    public bool headsetUserPresent;
    public bool headPoseAvailable;
    public Vector3 headPosition;
    public Quaternion headRotation;
    public Vector3 headVelocity;
    public float headAngularVelocityDegPerSecond;
    public bool gazeAvailable;
    public Vector3 gazeOrigin;
    public Vector3 gazeDirection;
    public bool gazeHit;
    public Vector3 gazeHitPoint;
    public bool gazeOnPainting;
}

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Tracking Sampler")]
public sealed class WaterLiliesTrackingSampler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform _headTransform;
    [SerializeField] Collider _paintingCollider;
    [SerializeField] Renderer _paintingRenderer;

    [Header("Gaze")]
    [SerializeField] bool _useXrEyesData = true;
    [Tooltip("Use Meta/OVR eye-gaze transforms when XR Eyes data is not exposed. The Quest Pro Building Blocks create objects named '[BuildingBlock] Eye Gaze Left/Right'.")]
    [SerializeField] bool _useGazeTransforms = true;
    [SerializeField] Transform _centerEyeGazeTransform;
    [SerializeField] Transform _leftEyeGazeTransform;
    [SerializeField] Transform _rightEyeGazeTransform;
    [SerializeField] bool _fallbackToHeadForward = true;
    [SerializeField, Min(0.1f)] float _gazeMaxDistanceMeters = 12f;
    [SerializeField] LayerMask _gazeLayerMask = ~0;

    readonly List<InputDevice> _headDevices = new List<InputDevice>();
    readonly List<InputDevice> _eyeDevices = new List<InputDevice>();
    Vector3 _lastHeadPosition;
    Quaternion _lastHeadRotation = Quaternion.identity;
    double _lastSampleRealtime = double.NaN;
    bool _hasLastHeadPose;

    void Reset()
    {
        AutoFindHeadTransform();
    }

    void Awake()
    {
        if (_headTransform == null)
        {
            AutoFindHeadTransform();
        }

        if (_useGazeTransforms && !HasAnyGazeTransform())
        {
            AutoFindGazeTransforms();
        }
    }

    public void BindPainting(GameObject painting)
    {
        if (painting == null)
        {
            return;
        }

        if (_paintingCollider == null)
        {
            _paintingCollider = painting.GetComponentInChildren<Collider>(true);
        }

        if (_paintingRenderer == null)
        {
            _paintingRenderer = painting.GetComponentInChildren<Renderer>(true);
        }
    }

    public WaterLiliesTrackingSample Capture()
    {
        if (_headTransform == null)
        {
            AutoFindHeadTransform();
        }

        if (_useGazeTransforms && !HasAnyGazeTransform())
        {
            AutoFindGazeTransforms();
        }

        var sample = new WaterLiliesTrackingSample();
        CaptureHeadsetPresence(ref sample);
        CaptureHeadPose(ref sample);
        CaptureGaze(ref sample);
        return sample;
    }

    public bool TryGetHeadsetPresence(out bool userPresent)
    {
        userPresent = false;
        _headDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, _headDevices);
        for (var i = 0; i < _headDevices.Count; i++)
        {
            if (!_headDevices[i].isValid)
            {
                continue;
            }

            if (_headDevices[i].TryGetFeatureValue(CommonUsages.userPresence, out userPresent))
            {
                return true;
            }
        }

        return false;
    }

    void CaptureHeadsetPresence(ref WaterLiliesTrackingSample sample)
    {
        sample.headsetPresenceAvailable = TryGetHeadsetPresence(out sample.headsetUserPresent);
    }

    void CaptureHeadPose(ref WaterLiliesTrackingSample sample)
    {
        if (_headTransform == null)
        {
            return;
        }

        var now = Time.realtimeSinceStartupAsDouble;
        sample.headPoseAvailable = true;
        sample.headPosition = _headTransform.position;
        sample.headRotation = _headTransform.rotation;

        if (_hasLastHeadPose && !double.IsNaN(_lastSampleRealtime))
        {
            var dt = Mathf.Max(0.0001f, (float)(now - _lastSampleRealtime));
            sample.headVelocity = (sample.headPosition - _lastHeadPosition) / dt;
            sample.headAngularVelocityDegPerSecond = Quaternion.Angle(_lastHeadRotation, sample.headRotation) / dt;
        }

        _lastHeadPosition = sample.headPosition;
        _lastHeadRotation = sample.headRotation;
        _lastSampleRealtime = now;
        _hasLastHeadPose = true;
    }

    void CaptureGaze(ref WaterLiliesTrackingSample sample)
    {
        if (_useXrEyesData && TryGetXrEyeGaze(sample.headPosition, out var origin, out var direction, out var fixationPoint))
        {
            sample.gazeAvailable = true;
            sample.gazeOrigin = origin;
            sample.gazeDirection = direction;
            sample.gazeHit = true;
            sample.gazeHitPoint = fixationPoint;
            sample.gazeOnPainting = IsPointOnPainting(fixationPoint) || RayHitsPainting(origin, direction, out sample.gazeHitPoint);
            return;
        }

        if (_useGazeTransforms && TryGetTransformGaze(out origin, out direction))
        {
            sample.gazeAvailable = true;
            sample.gazeOrigin = origin;
            sample.gazeDirection = direction;
            sample.gazeOnPainting = RayHitsPainting(origin, direction, out var transformHitPoint);
            sample.gazeHit = sample.gazeOnPainting;
            sample.gazeHitPoint = transformHitPoint;
            return;
        }

        if (!_fallbackToHeadForward || _headTransform == null)
        {
            return;
        }

        sample.gazeAvailable = true;
        sample.gazeOrigin = _headTransform.position;
        sample.gazeDirection = _headTransform.forward.normalized;
        sample.gazeOnPainting = RayHitsPainting(sample.gazeOrigin, sample.gazeDirection, out var headForwardHitPoint);
        sample.gazeHit = sample.gazeOnPainting;
        sample.gazeHitPoint = headForwardHitPoint;
    }

    bool TryGetXrEyeGaze(Vector3 fallbackOrigin, out Vector3 origin, out Vector3 direction, out Vector3 fixationPoint)
    {
        origin = fallbackOrigin;
        direction = _headTransform != null ? _headTransform.forward.normalized : Vector3.forward;
        fixationPoint = Vector3.zero;

        _eyeDevices.Clear();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, _eyeDevices);
        for (var i = 0; i < _eyeDevices.Count; i++)
        {
            if (!_eyeDevices[i].isValid)
            {
                continue;
            }

            if (!_eyeDevices[i].TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyes))
            {
                continue;
            }

            if (!eyes.TryGetFixationPoint(out fixationPoint))
            {
                continue;
            }

            if (_headTransform != null)
            {
                origin = _headTransform.position;
            }

            direction = fixationPoint - origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                continue;
            }

            direction.Normalize();
            return true;
        }

        return false;
    }

    bool TryGetTransformGaze(out Vector3 origin, out Vector3 direction)
    {
        origin = Vector3.zero;
        direction = Vector3.zero;

        if (_centerEyeGazeTransform != null)
        {
            origin = _centerEyeGazeTransform.position;
            direction = _centerEyeGazeTransform.forward.normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        var count = 0;
        AccumulateGazeTransform(_leftEyeGazeTransform, ref origin, ref direction, ref count);
        AccumulateGazeTransform(_rightEyeGazeTransform, ref origin, ref direction, ref count);
        if (count == 0 || direction.sqrMagnitude <= 0.0001f)
        {
            return false;
        }

        origin /= count;
        direction.Normalize();
        return true;
    }

    static void AccumulateGazeTransform(Transform gazeTransform, ref Vector3 origin, ref Vector3 direction, ref int count)
    {
        if (gazeTransform == null)
        {
            return;
        }

        origin += gazeTransform.position;
        direction += gazeTransform.forward;
        count++;
    }

    bool RayHitsPainting(Vector3 origin, Vector3 direction, out Vector3 hitPoint)
    {
        hitPoint = origin + direction * _gazeMaxDistanceMeters;

        if (_paintingCollider != null)
        {
            var ray = new Ray(origin, direction);
            if (_paintingCollider.Raycast(ray, out var hit, _gazeMaxDistanceMeters))
            {
                hitPoint = hit.point;
                return true;
            }
        }

        if (_paintingRenderer != null)
        {
            var ray = new Ray(origin, direction);
            if (_paintingRenderer.bounds.IntersectRay(ray, out var distance) && distance <= _gazeMaxDistanceMeters)
            {
                hitPoint = origin + direction * distance;
                return true;
            }
        }

        if (Physics.Raycast(origin, direction, out var physicsHit, _gazeMaxDistanceMeters, _gazeLayerMask, QueryTriggerInteraction.Ignore))
        {
            hitPoint = physicsHit.point;
            if (_paintingCollider != null)
            {
                return physicsHit.collider == _paintingCollider ||
                       physicsHit.collider.transform.IsChildOf(_paintingCollider.transform);
            }

            if (_paintingRenderer != null)
            {
                return physicsHit.collider.transform.IsChildOf(_paintingRenderer.transform);
            }
        }

        return false;
    }

    bool IsPointOnPainting(Vector3 point)
    {
        if (_paintingCollider != null)
        {
            return _paintingCollider.bounds.Contains(point);
        }

        return _paintingRenderer != null && _paintingRenderer.bounds.Contains(point);
    }

    void AutoFindHeadTransform()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _headTransform = mainCamera.transform;
        }
    }

    bool HasAnyGazeTransform()
    {
        return _centerEyeGazeTransform != null ||
               _leftEyeGazeTransform != null ||
               _rightEyeGazeTransform != null;
    }

    void AutoFindGazeTransforms()
    {
        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            var candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            if (_leftEyeGazeTransform == null && candidate.name.Contains("Eye Gaze Left"))
            {
                _leftEyeGazeTransform = candidate;
            }
            else if (_rightEyeGazeTransform == null && candidate.name.Contains("Eye Gaze Right"))
            {
                _rightEyeGazeTransform = candidate;
            }
            else if (_centerEyeGazeTransform == null && candidate.name.Contains("Eye Gaze Center"))
            {
                _centerEyeGazeTransform = candidate;
            }
        }
    }
}
