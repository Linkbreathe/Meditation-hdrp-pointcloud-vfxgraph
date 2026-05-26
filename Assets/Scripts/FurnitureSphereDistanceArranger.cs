using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Layout/Furniture Sphere Distance Arranger")]
public sealed class FurnitureSphereDistanceArranger : MonoBehaviour
{
    enum CppCenterMode
    {
        BoundsCenter,
        AveragePosition,
        CppPivot
    }

    enum FurnitureCenterMode
    {
        RendererBoundsCenter,
        Pivot
    }

    enum SphereLayoutMode
    {
        CurvedRectangle,
        Arc,
        PreserveCurrentDirections
    }

    enum ArcPlane
    {
        CppLocalYZ,
        CppLocalXZ,
        CppLocalXY,
        WorldYZ,
        WorldXZ,
        WorldXY
    }

    [Header("Scene References")]
    [SerializeField] Transform _cppGroup;
    [SerializeField] Transform _furnitureGroup;

    [Header("Layout")]
    [SerializeField, Min(0f)] float _targetDistance = 5f;
    [SerializeField] SphereLayoutMode _sphereLayoutMode = SphereLayoutMode.CurvedRectangle;
    [SerializeField] CppCenterMode _cppCenterMode = CppCenterMode.BoundsCenter;
    [SerializeField] FurnitureCenterMode _furnitureCenterMode = FurnitureCenterMode.RendererBoundsCenter;
    [SerializeField] ArcPlane _layoutPlane = ArcPlane.CppLocalYZ;
    [SerializeField] string _sphereNamePrefix = "Sphere";
    [SerializeField] bool _includeInactiveSpheres = true;
    [SerializeField] bool _centerFurnitureWhenArranging = true;
    [SerializeField] bool _arrangeOnStart = true;
    [SerializeField] bool _maintainDistanceDuringPlay = true;
    [SerializeField] bool _drawDistanceGizmo = true;

    [Header("Curved Rectangle")]
    [SerializeField, Min(1)] int _surfaceColumns = 9;
    [SerializeField, Min(1)] int _surfaceRows = 6;
    [SerializeField, Min(0.01f)] float _surfaceHorizontalSpacing = 0.35f;
    [SerializeField, Min(0.01f)] float _surfaceVerticalSpacing = 0.35f;
    [SerializeField] float _surfaceCenterYawDegrees;
    [SerializeField] float _surfaceCenterPitchDegrees;
    [SerializeField] bool _flipSurfaceDirection;
    [SerializeField] bool _compressSurfaceOnlyWhenDistanceIsTooSmall = true;
    [SerializeField] bool _sortSurfaceByCurrentLocalPosition = true;

    [Header("Arc")]
    [SerializeField, Range(0f, 360f)] float _arcAngleDegrees = 180f;
    [SerializeField] float _arcCenterAngleDegrees = 180f;
    [SerializeField] bool _sortSpheresByCurrentAngle = true;

    readonly List<Transform> _spheres = new List<Transform>();

    public float targetDistance
    {
        get => _targetDistance;
        set => _targetDistance = Mathf.Max(0f, value);
    }

    [ContextMenu("Arrange Furniture And Spheres")]
    public void ArrangeNow()
    {
        if (!TryCollectSpheres(_spheres, true))
        {
            return;
        }

        Vector3 cppCenter = CalculateCppCenter(_spheres);

        if (_centerFurnitureWhenArranging)
        {
            MoveFurnitureCenterTo(cppCenter);
        }

        ArrangeSpheres(GetFurnitureCenter(), _spheres);
    }

    [ContextMenu("Capture Current Average Distance")]
    public void CaptureCurrentAverageDistance()
    {
        if (!TryCollectSpheres(_spheres, true))
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(this, "Capture Current CPP Sphere Distance");
        }
#endif

        Vector3 furnitureCenter = GetFurnitureCenter();
        float totalDistance = 0f;

        for (int i = 0; i < _spheres.Count; i++)
        {
            totalDistance += Vector3.Distance(_spheres[i].position, furnitureCenter);
        }

        _targetDistance = totalDistance / _spheres.Count;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    void Reset()
    {
        _cppGroup = transform;
        FindDefaultFurnitureGroup();
    }

    void Awake()
    {
        if (_cppGroup == null)
        {
            _cppGroup = transform;
        }
    }

    void Start()
    {
        if (Application.isPlaying && _arrangeOnStart)
        {
            ArrangeNow();
        }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying || !_maintainDistanceDuringPlay)
        {
            return;
        }

        if (TryCollectSpheres(_spheres, false))
        {
            ArrangeSpheres(GetFurnitureCenter(), _spheres);
        }
    }

    void OnValidate()
    {
        _targetDistance = Mathf.Max(0f, _targetDistance);
        _surfaceColumns = Mathf.Max(1, _surfaceColumns);
        _surfaceRows = Mathf.Max(1, _surfaceRows);
        _surfaceHorizontalSpacing = Mathf.Max(0.01f, _surfaceHorizontalSpacing);
        _surfaceVerticalSpacing = Mathf.Max(0.01f, _surfaceVerticalSpacing);
        _arcAngleDegrees = Mathf.Clamp(_arcAngleDegrees, 0f, 360f);

        if (_cppGroup == null)
        {
            _cppGroup = transform;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!_drawDistanceGizmo || _furnitureGroup == null)
        {
            return;
        }

        Gizmos.color = new Color(0.15f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireSphere(GetFurnitureCenter(), _targetDistance);
    }

    bool TryCollectSpheres(List<Transform> results, bool logWarnings)
    {
        results.Clear();

        if (_cppGroup == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"{nameof(FurnitureSphereDistanceArranger)} needs a CPP group.", this);
            }

            return false;
        }

        if (_furnitureGroup == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning($"{nameof(FurnitureSphereDistanceArranger)} needs a furniture group.", this);
            }

            return false;
        }

        Transform[] candidates = _cppGroup.GetComponentsInChildren<Transform>(_includeInactiveSpheres);

        for (int i = 0; i < candidates.Length; i++)
        {
            Transform candidate = candidates[i];

            if (candidate == _cppGroup || candidate == _furnitureGroup || candidate.IsChildOf(_furnitureGroup))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(_sphereNamePrefix) &&
                !candidate.name.StartsWith(_sphereNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(candidate);
        }

        if (results.Count == 0 && logWarnings)
        {
            Debug.LogWarning($"{nameof(FurnitureSphereDistanceArranger)} found no sphere transforms under {_cppGroup.name}.", this);
        }

        return results.Count > 0;
    }

    Vector3 CalculateCppCenter(IReadOnlyList<Transform> spheres)
    {
        if (_cppCenterMode == CppCenterMode.CppPivot)
        {
            return _cppGroup.position;
        }

        if (_cppCenterMode == CppCenterMode.AveragePosition)
        {
            Vector3 total = Vector3.zero;

            for (int i = 0; i < spheres.Count; i++)
            {
                total += spheres[i].position;
            }

            return total / spheres.Count;
        }

        Bounds bounds = new Bounds(spheres[0].position, Vector3.zero);

        for (int i = 1; i < spheres.Count; i++)
        {
            bounds.Encapsulate(spheres[i].position);
        }

        return bounds.center;
    }

    Vector3 GetFurnitureCenter()
    {
        if (_furnitureCenterMode == FurnitureCenterMode.RendererBoundsCenter &&
            TryGetRendererBounds(_furnitureGroup, out Bounds bounds))
        {
            return bounds.center;
        }

        return _furnitureGroup.position;
    }

    void MoveFurnitureCenterTo(Vector3 targetCenter)
    {
        Vector3 delta = targetCenter - GetFurnitureCenter();

        if (delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        RecordTransform(_furnitureGroup, "Center Furniture Group");
        _furnitureGroup.position += delta;
        MarkDirty(_furnitureGroup);
    }

    void ArrangeSpheres(Vector3 center, List<Transform> spheres)
    {
        if (_sphereLayoutMode == SphereLayoutMode.CurvedRectangle)
        {
            ArrangeOnCurvedRectangle(center, spheres);
        }
        else if (_sphereLayoutMode == SphereLayoutMode.Arc)
        {
            ArrangeOnArc(center, spheres);
        }
        else
        {
            EnforceDistanceFrom(center, spheres);
        }
    }

    void ArrangeOnCurvedRectangle(Vector3 center, List<Transform> spheres)
    {
        GetLayoutAxes(out Vector3 horizontalAxis, out Vector3 verticalAxis);

        if (_sortSurfaceByCurrentLocalPosition)
        {
            SortSpheresForSurface(spheres, center, horizontalAxis, verticalAxis);
        }

        int columns = Mathf.Max(1, _surfaceColumns);
        int rows = Mathf.Max(1, _surfaceRows);

        if (columns * rows < spheres.Count)
        {
            rows = Mathf.CeilToInt(spheres.Count / (float)columns);
        }

        Vector3 surfaceCenterDirection = Vector3.Cross(horizontalAxis, verticalAxis).normalized;

        if (_flipSurfaceDirection)
        {
            surfaceCenterDirection = -surfaceCenterDirection;
        }

        Quaternion surfaceYaw = Quaternion.AngleAxis(_surfaceCenterYawDegrees, verticalAxis);
        surfaceCenterDirection = surfaceYaw * surfaceCenterDirection;
        horizontalAxis = surfaceYaw * horizontalAxis;

        Quaternion surfacePitch = Quaternion.AngleAxis(_surfaceCenterPitchDegrees, horizontalAxis);
        surfaceCenterDirection = surfacePitch * surfaceCenterDirection;
        verticalAxis = surfacePitch * verticalAxis;

        float maxHorizontalOffset = (columns - 1) * _surfaceHorizontalSpacing * 0.5f;
        float maxVerticalOffset = (rows - 1) * _surfaceVerticalSpacing * 0.5f;
        float maxTangentOffset = Mathf.Sqrt(maxHorizontalOffset * maxHorizontalOffset + maxVerticalOffset * maxVerticalOffset);
        float tangentScale = 1f;

        if (_compressSurfaceOnlyWhenDistanceIsTooSmall && maxTangentOffset > _targetDistance * 0.98f)
        {
            tangentScale = _targetDistance * 0.98f / maxTangentOffset;
        }

        for (int i = 0; i < spheres.Count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            float horizontalOffset = (column - (columns - 1) * 0.5f) * _surfaceHorizontalSpacing * tangentScale;
            float verticalOffset = ((rows - 1) * 0.5f - row) * _surfaceVerticalSpacing * tangentScale;
            float tangentMagnitudeSquared = horizontalOffset * horizontalOffset + verticalOffset * verticalOffset;
            float normalOffset = Mathf.Sqrt(Mathf.Max(0f, _targetDistance * _targetDistance - tangentMagnitudeSquared));
            Vector3 targetOffset = surfaceCenterDirection * normalOffset +
                                   horizontalAxis * horizontalOffset +
                                   verticalAxis * verticalOffset;

            MoveSphereTo(spheres[i], center + targetOffset);
        }
    }

    void ArrangeOnArc(Vector3 center, List<Transform> spheres)
    {
        GetLayoutAxes(out Vector3 axisA, out Vector3 axisB);

        if (_sortSpheresByCurrentAngle)
        {
            spheres.Sort((first, second) =>
            {
                float firstAngle = GetProjectedAngle(first.position - center, axisA, axisB);
                float secondAngle = GetProjectedAngle(second.position - center, axisA, axisB);
                int angleComparison = firstAngle.CompareTo(secondAngle);

                return angleComparison != 0
                    ? angleComparison
                    : string.CompareOrdinal(first.name, second.name);
            });
        }

        float span = Mathf.Clamp(_arcAngleDegrees, 0f, 360f);
        float startAngle = _arcCenterAngleDegrees - span * 0.5f;
        float angleStep = GetArcAngleStep(span, spheres.Count);

        for (int i = 0; i < spheres.Count; i++)
        {
            float angleDegrees = startAngle + angleStep * i;
            Vector3 direction = GetDirectionOnArc(axisA, axisB, angleDegrees);
            MoveSphereTo(spheres[i], center + direction * _targetDistance);
        }
    }

    void EnforceDistanceFrom(Vector3 center, IReadOnlyList<Transform> spheres)
    {
        for (int i = 0; i < spheres.Count; i++)
        {
            Transform sphere = spheres[i];
            Vector3 direction = sphere.position - center;

            if (direction.sqrMagnitude <= 0.000001f)
            {
                direction = GetFallbackDirection(i, spheres.Count);
            }

            MoveSphereTo(sphere, center + direction.normalized * _targetDistance);
        }
    }

    void MoveSphereTo(Transform sphere, Vector3 targetPosition)
    {
        if ((sphere.position - targetPosition).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        RecordTransform(sphere, "Arrange CPP Sphere Distance");
        sphere.position = targetPosition;
        MarkDirty(sphere);
    }

    void SortSpheresForSurface(List<Transform> spheres, Vector3 center, Vector3 horizontalAxis, Vector3 verticalAxis)
    {
        spheres.Sort((first, second) =>
        {
            Vector3 firstOffset = first.position - center;
            Vector3 secondOffset = second.position - center;
            float firstVertical = Vector3.Dot(firstOffset, verticalAxis);
            float secondVertical = Vector3.Dot(secondOffset, verticalAxis);
            int verticalComparison = secondVertical.CompareTo(firstVertical);

            if (verticalComparison != 0)
            {
                return verticalComparison;
            }

            float firstHorizontal = Vector3.Dot(firstOffset, horizontalAxis);
            float secondHorizontal = Vector3.Dot(secondOffset, horizontalAxis);
            int horizontalComparison = firstHorizontal.CompareTo(secondHorizontal);

            return horizontalComparison != 0
                ? horizontalComparison
                : string.CompareOrdinal(first.name, second.name);
        });
    }

    void GetLayoutAxes(out Vector3 axisA, out Vector3 axisB)
    {
        Transform reference = _cppGroup != null ? _cppGroup : transform;

        switch (_layoutPlane)
        {
            case ArcPlane.CppLocalXZ:
                axisA = reference.forward;
                axisB = reference.right;
                break;
            case ArcPlane.CppLocalXY:
                axisA = reference.right;
                axisB = reference.up;
                break;
            case ArcPlane.WorldYZ:
                axisA = Vector3.forward;
                axisB = Vector3.up;
                break;
            case ArcPlane.WorldXZ:
                axisA = Vector3.forward;
                axisB = Vector3.right;
                break;
            case ArcPlane.WorldXY:
                axisA = Vector3.right;
                axisB = Vector3.up;
                break;
            default:
                axisA = reference.forward;
                axisB = reference.up;
                break;
        }

        axisA.Normalize();
        axisB = Vector3.ProjectOnPlane(axisB, axisA).normalized;

        if (axisB.sqrMagnitude <= 0.000001f)
        {
            axisB = Vector3.up;
        }
    }

    static float GetProjectedAngle(Vector3 offset, Vector3 axisA, Vector3 axisB)
    {
        float a = Vector3.Dot(offset, axisA);
        float b = Vector3.Dot(offset, axisB);
        return Mathf.Atan2(b, a);
    }

    static Vector3 GetDirectionOnArc(Vector3 axisA, Vector3 axisB, float angleDegrees)
    {
        float angleRadians = angleDegrees * Mathf.Deg2Rad;
        return Mathf.Cos(angleRadians) * axisA + Mathf.Sin(angleRadians) * axisB;
    }

    static float GetArcAngleStep(float span, int count)
    {
        if (count <= 1)
        {
            return 0f;
        }

        return span >= 359.999f ? span / count : span / (count - 1);
    }

    static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    static Vector3 GetFallbackDirection(int index, int count)
    {
        float angle = count <= 1 ? 0f : Mathf.PI * 2f * index / count;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    void FindDefaultFurnitureGroup()
    {
        GameObject furnitureObject = GameObject.Find("furnitures");

        if (furnitureObject != null)
        {
            _furnitureGroup = furnitureObject.transform;
        }
    }

    static void RecordTransform(Transform target, string undoName)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(target, undoName);
        }
#endif
    }

    static void MarkDirty(Transform target)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(target);
        }
#endif
    }
}
