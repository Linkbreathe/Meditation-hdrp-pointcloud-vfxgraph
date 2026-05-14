using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Calmness Feedback Option View")]
public sealed class CalmnessFeedbackOptionView : MonoBehaviour
{
    const int ArcSegments = 64;
    const float SpritePixelsPerUnit = 100f;
    const float TextDepthOffset = -0.003f;
    const float ArcDepthOffset = -0.006f;
    const float GlowDepthOffset = 0.004f;

    [SerializeField, Range(CalmnessFeedbackScale.MinimumLevel, CalmnessFeedbackScale.MaximumLevel)]
    int _level = CalmnessFeedbackScale.MinimumLevel;
    [SerializeField] string _label = "\u6d9f\u6f2a";
    [SerializeField] Color _color = new Color(0.91f, 0.53f, 0.29f, 1f);
    [SerializeField] Vector2 _sizeMeters = new Vector2(0.22f, 0.11f);
    [SerializeField, Min(0.001f)] float _arcThicknessMeters = 0.008f;
    [SerializeField, Min(0.001f)] float _glowPaddingMeters = 0.035f;

    [Header("Generated Visuals")]
    [SerializeField] Transform _visualRoot;
    [SerializeField] SpriteRenderer _glowRenderer;
    [SerializeField] SpriteRenderer _backgroundRenderer;
    [SerializeField] TextMeshPro _labelText;
    [SerializeField] LineRenderer _arcRenderer;

    static Sprite _whiteSprite;
    static Material _spriteMaterial;

    public int level => Mathf.Clamp(_level, CalmnessFeedbackScale.MinimumLevel, CalmnessFeedbackScale.MaximumLevel);
    public string label => string.IsNullOrEmpty(_label) ? CalmnessFeedbackScale.GetLabel(level) : _label;
    public Color color => _color;
    public Vector2 sizeMeters => new Vector2(
        Mathf.Max(0.01f, _sizeMeters.x),
        Mathf.Max(0.01f, _sizeMeters.y));
    public float groupAlpha { get; private set; } = 1f;
    public float dwellProgress { get; private set; }
    public bool isHighlighted { get; private set; }

    public void SetDefaultLevel(int level)
    {
        _level = Mathf.Clamp(level, CalmnessFeedbackScale.MinimumLevel, CalmnessFeedbackScale.MaximumLevel);
        _label = CalmnessFeedbackScale.GetLabel(_level);
        _color = CalmnessFeedbackScale.GetColor(_level);
        EnsureVisuals();
        ApplyStaticVisuals();
    }

    public void EnsureVisuals()
    {
        if (_visualRoot == null)
        {
            _visualRoot = GetOrCreateChild("Visuals");
        }

        if (_glowRenderer == null)
        {
            _glowRenderer = GetOrCreateSpriteRenderer("Hover Glow");
        }

        if (_backgroundRenderer == null)
        {
            _backgroundRenderer = GetOrCreateSpriteRenderer("Background");
        }

        if (_labelText == null)
        {
            _labelText = GetOrCreateText("Label");
        }

        if (_arcRenderer == null)
        {
            _arcRenderer = GetOrCreateLineRenderer("Dwell Arc");
        }

        ApplyStaticVisuals();
        ApplyDynamicVisuals(0f);
    }

    public bool ContainsWorldPoint(Vector3 worldPoint)
    {
        var localPoint = transform.InverseTransformPoint(worldPoint);
        var halfSize = sizeMeters * 0.5f;
        return Mathf.Abs(localPoint.x) <= halfSize.x &&
               Mathf.Abs(localPoint.y) <= halfSize.y;
    }

    public void ResetVisuals()
    {
        dwellProgress = 0f;
        isHighlighted = false;
        transform.localScale = Vector3.one;
        ApplyDynamicVisuals(1f);
    }

    public void SetGroupAlpha(float alpha)
    {
        groupAlpha = Mathf.Clamp01(alpha);
        ApplyDynamicVisuals(1f);
    }

    public void SetProgress(float progress)
    {
        dwellProgress = Mathf.Clamp01(progress);
        ApplyArc();
    }

    public void SetHighlighted(bool highlighted, float deltaTime)
    {
        isHighlighted = highlighted;
        var speed = Mathf.Max(0.01f, deltaTime) * 5f;
        var targetScale = highlighted ? 1.18f : 1f;
        transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one * targetScale, speed);
        ApplyDynamicVisuals(deltaTime);
    }

    public void SetPulseScale(float scale)
    {
        transform.localScale = Vector3.one * Mathf.Max(0.01f, scale);
    }

    void Reset()
    {
        EnsureVisuals();
    }

    void OnEnable()
    {
        EnsureVisuals();
    }

    void OnValidate()
    {
        _level = Mathf.Clamp(_level, CalmnessFeedbackScale.MinimumLevel, CalmnessFeedbackScale.MaximumLevel);
        _sizeMeters.x = Mathf.Max(0.01f, _sizeMeters.x);
        _sizeMeters.y = Mathf.Max(0.01f, _sizeMeters.y);
        _arcThicknessMeters = Mathf.Max(0.001f, _arcThicknessMeters);
        _glowPaddingMeters = Mathf.Max(0.001f, _glowPaddingMeters);
        if (string.IsNullOrEmpty(_label))
        {
            _label = CalmnessFeedbackScale.GetLabel(_level);
        }

        EnsureVisuals();
    }

    Transform GetOrCreateChild(string childName)
    {
        var child = transform.Find(childName);
        if (child != null)
        {
            return child;
        }

        var childObject = new GameObject(childName);
        childObject.transform.SetParent(transform, false);
        return childObject.transform;
    }

    SpriteRenderer GetOrCreateSpriteRenderer(string childName)
    {
        var child = GetOrCreateChild(childName);
        var renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            renderer = child.gameObject.AddComponent<SpriteRenderer>();
        }

        renderer.sprite = GetWhiteSprite();
        renderer.sharedMaterial = GetSpriteMaterial();
        return renderer;
    }

    TextMeshPro GetOrCreateText(string childName)
    {
        var child = GetOrCreateChild(childName);
        var text = child.GetComponent<TextMeshPro>();
        if (text == null)
        {
            text = child.gameObject.AddComponent<TextMeshPro>();
        }

        return text;
    }

    LineRenderer GetOrCreateLineRenderer(string childName)
    {
        var child = GetOrCreateChild(childName);
        var line = child.GetComponent<LineRenderer>();
        if (line == null)
        {
            line = child.gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.loop = false;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.TransformZ;
        line.sharedMaterial = GetSpriteMaterial();
        return line;
    }

    void ApplyStaticVisuals()
    {
        if (_visualRoot != null)
        {
            _visualRoot.localPosition = Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
            _visualRoot.localScale = Vector3.one;
        }

        ApplySpriteTransform(_backgroundRenderer, sizeMeters, Vector3.zero);
        ApplySpriteTransform(
            _glowRenderer,
            sizeMeters + Vector2.one * _glowPaddingMeters,
            new Vector3(0f, 0f, GlowDepthOffset));

        if (_labelText != null)
        {
            _labelText.text = label;
            _labelText.alignment = TextAlignmentOptions.Center;
            _labelText.fontSize = 0.045f;
            _labelText.enableAutoSizing = true;
            _labelText.fontSizeMin = 0.022f;
            _labelText.fontSizeMax = 0.052f;
            _labelText.rectTransform.sizeDelta = sizeMeters;
            _labelText.transform.localPosition = new Vector3(0f, 0f, TextDepthOffset);
            _labelText.transform.localRotation = Quaternion.identity;
            _labelText.transform.localScale = Vector3.one;
        }

        if (_arcRenderer != null)
        {
            _arcRenderer.widthMultiplier = _arcThicknessMeters;
        }

        ApplyDynamicVisuals(1f);
        ApplyArc();
    }

    void ApplySpriteTransform(SpriteRenderer renderer, Vector2 targetSize, Vector3 localPosition)
    {
        if (renderer == null || renderer.sprite == null)
        {
            return;
        }

        renderer.transform.localPosition = localPosition;
        renderer.transform.localRotation = Quaternion.identity;
        var spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            targetSize.x / Mathf.Max(0.001f, spriteSize.x),
            targetSize.y / Mathf.Max(0.001f, spriteSize.y),
            1f);
    }

    void ApplyDynamicVisuals(float deltaTime)
    {
        var speed = Mathf.Max(0.01f, deltaTime) * 5f;
        SetRendererAlpha(_backgroundRenderer, isHighlighted ? 0.95f : 0.58f, speed);
        SetRendererAlpha(_glowRenderer, isHighlighted ? 0.34f : 0f, speed);

        if (_labelText != null)
        {
            var labelColor = _labelText.color;
            labelColor = Color.Lerp(labelColor, new Color(1f, 1f, 1f, (isHighlighted ? 1f : 0.92f) * groupAlpha), speed);
            _labelText.color = labelColor;
        }

        ApplyArc();
    }

    void SetRendererAlpha(SpriteRenderer renderer, float targetAlpha, float speed)
    {
        if (renderer == null)
        {
            return;
        }

        var rendererColor = _color;
        if (renderer == _glowRenderer)
        {
            rendererColor = Color.Lerp(_color, Color.white, 0.25f);
        }

        var current = renderer.color;
        rendererColor.a = Mathf.MoveTowards(current.a, targetAlpha * groupAlpha, speed);
        renderer.color = rendererColor;
    }

    void ApplyArc()
    {
        if (_arcRenderer == null)
        {
            return;
        }

        var progress = Mathf.Clamp01(dwellProgress);
        if (progress <= 0f || groupAlpha <= 0f)
        {
            _arcRenderer.positionCount = 0;
            return;
        }

        var halfSize = sizeMeters * 0.5f + Vector2.one * 0.015f;
        var radius = Mathf.Min(halfSize.x, halfSize.y);
        var count = Mathf.Max(3, Mathf.CeilToInt(ArcSegments * progress));
        _arcRenderer.positionCount = count + 1;
        _arcRenderer.widthMultiplier = _arcThicknessMeters;

        var arcColor = _color;
        arcColor.a = (isHighlighted ? 0.9f : 0.6f) * groupAlpha;
        _arcRenderer.startColor = arcColor;
        _arcRenderer.endColor = arcColor;

        for (var i = 0; i <= count; i++)
        {
            var t = i / (float)count;
            var angle = (90f - 360f * progress * t) * Mathf.Deg2Rad;
            _arcRenderer.SetPosition(
                i,
                new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, ArcDepthOffset));
        }
    }

    static Sprite GetWhiteSprite()
    {
        if (_whiteSprite != null)
        {
            return _whiteSprite;
        }

        _whiteSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
            new Vector2(0.5f, 0.5f),
            SpritePixelsPerUnit);
        return _whiteSprite;
    }

    static Material GetSpriteMaterial()
    {
        if (_spriteMaterial != null)
        {
            return _spriteMaterial;
        }

        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        _spriteMaterial = shader != null ? new Material(shader) : null;
        if (_spriteMaterial != null)
        {
            _spriteMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        return _spriteMaterial;
    }
}
