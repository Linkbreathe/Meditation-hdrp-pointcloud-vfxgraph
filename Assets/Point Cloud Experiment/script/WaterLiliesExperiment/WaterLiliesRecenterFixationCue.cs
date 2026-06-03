using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Recenter Fixation Cue")]
public sealed class WaterLiliesRecenterFixationCue : MonoBehaviour
{
    const string AnchorName = "WaterLilies_RecenterFixationCross";

    [Header("Target")]
    [SerializeField] Transform _viewer;
    [SerializeField] bool _autoResolveViewer = true;

    [Header("Appearance")]
    [SerializeField, Min(0.25f)] float _distanceMeters = 2f;
    [SerializeField, Min(0.02f)] float _sizeMeters = 0.16f;
    [SerializeField, Min(0.001f)] float _lineWidthMeters = 0.014f;
    [SerializeField, Min(1f)] float _outlineWidthMultiplier = 2.1f;
    [SerializeField] Color _lineColor = new Color(1f, 1f, 1f, 0.96f);
    [SerializeField] Color _outlineColor = new Color(0f, 0f, 0f, 0.88f);

    GameObject _anchor;
    LineRenderer _horizontalOutline;
    LineRenderer _verticalOutline;
    LineRenderer _horizontalLine;
    LineRenderer _verticalLine;
    Material _lineMaterial;
    Material _outlineMaterial;
    bool _visible;

    public bool visible => _visible;

    public void SetViewer(Transform viewer)
    {
        if (_viewer == viewer)
        {
            return;
        }

        _viewer = viewer;
        UpdatePose();
    }

    public void SetVisible(bool visible)
    {
        if (_visible == visible)
        {
            if (_visible)
            {
                UpdatePose();
                UpdateActive();
            }

            return;
        }

        _visible = visible;
        EnsureCue();
        UpdatePose();
        UpdateActive();
    }

    void Awake()
    {
        EnsureCue();
        UpdateActive();
    }

    void LateUpdate()
    {
        if (!_visible)
        {
            return;
        }

        if (_viewer == null && _autoResolveViewer)
        {
            _viewer = ResolveViewerTransform();
        }

        EnsureCue();
        ApplySettings();
        UpdatePose();
        UpdateActive();
    }

    void OnDisable()
    {
        if (_anchor != null)
        {
            _anchor.SetActive(false);
        }
    }

    void OnDestroy()
    {
        DestroyMaterial(_lineMaterial);
        DestroyMaterial(_outlineMaterial);
    }

    void EnsureCue()
    {
        if (_anchor == null)
        {
            _anchor = new GameObject(AnchorName);
            _anchor.hideFlags = HideFlags.HideAndDontSave;
            _anchor.transform.SetParent(transform, false);
        }

        EnsureMaterials();
        _horizontalOutline = EnsureLine("Horizontal Outline", _horizontalOutline, _outlineMaterial, 0);
        _verticalOutline = EnsureLine("Vertical Outline", _verticalOutline, _outlineMaterial, 0);
        _horizontalLine = EnsureLine("Horizontal Line", _horizontalLine, _lineMaterial, 1);
        _verticalLine = EnsureLine("Vertical Line", _verticalLine, _lineMaterial, 1);
        ApplySettings();
    }

    LineRenderer EnsureLine(string lineName, LineRenderer line, Material material, int sortingOrder)
    {
        if (line != null)
        {
            return line;
        }

        var lineObject = new GameObject(lineName);
        lineObject.hideFlags = HideFlags.HideAndDontSave;
        lineObject.transform.SetParent(_anchor.transform, false);
        line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 2;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = sortingOrder;
        if (material != null)
        {
            line.material = material;
        }

        return line;
    }

    void EnsureMaterials()
    {
        if (_lineMaterial == null)
        {
            _lineMaterial = CreateCueMaterial(_lineColor, 5000);
        }

        if (_outlineMaterial == null)
        {
            _outlineMaterial = CreateCueMaterial(_outlineColor, 4999);
        }
    }

    void ApplySettings()
    {
        var half = Mathf.Max(0.01f, _sizeMeters * 0.5f);
        var width = Mathf.Max(0.001f, _lineWidthMeters);
        var outlineWidth = Mathf.Max(width, width * Mathf.Max(1f, _outlineWidthMultiplier));

        ConfigureLine(_horizontalOutline, new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), outlineWidth, _outlineColor);
        ConfigureLine(_verticalOutline, new Vector3(0f, -half, 0f), new Vector3(0f, half, 0f), outlineWidth, _outlineColor);
        ConfigureLine(_horizontalLine, new Vector3(-half, 0f, 0f), new Vector3(half, 0f, 0f), width, _lineColor);
        ConfigureLine(_verticalLine, new Vector3(0f, -half, 0f), new Vector3(0f, half, 0f), width, _lineColor);
    }

    static void ConfigureLine(LineRenderer line, Vector3 start, Vector3 end, float width, Color color)
    {
        if (line == null)
        {
            return;
        }

        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    void UpdatePose()
    {
        if (_anchor == null)
        {
            return;
        }

        if (_viewer == null && _autoResolveViewer)
        {
            _viewer = ResolveViewerTransform();
        }

        if (_viewer == null)
        {
            return;
        }

        _anchor.transform.SetParent(_viewer, false);
        _anchor.transform.localPosition = Vector3.forward * Mathf.Max(0.25f, _distanceMeters);
        _anchor.transform.localRotation = Quaternion.identity;
        _anchor.transform.localScale = Vector3.one;
    }

    void UpdateActive()
    {
        if (_anchor != null)
        {
            _anchor.SetActive(_visible && _viewer != null);
        }
    }

    static Material CreateCueMaterial(Color color, int renderQueue)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (shader == null)
        {
            shader = Shader.Find("UI/Default");
        }

        if (shader == null)
        {
            return null;
        }

        var material = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            color = color,
            renderQueue = renderQueue
        };
        return material;
    }

    static Transform ResolveViewerTransform()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && transforms[i].name == "CenterEyeAnchor")
            {
                return transforms[i];
            }
        }

        return null;
    }

    static void DestroyMaterial(Material material)
    {
        if (material == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(material);
        }
        else
        {
            DestroyImmediate(material);
        }
    }
}
