using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Natural Vortex Modulator")]
public sealed class WaterLiliesNaturalVortexModulator : MonoBehaviour
{
    const string DefaultIntensityProperty = WaterLiliesVfxController.DefaultIntensityProperty;
    const string DefaultFrequencyProperty = WaterLiliesVfxController.DefaultFrequencyProperty;

    static readonly string[] IntensityAliases =
    {
        WaterLiliesVfxController.VisibleIntensityProperty,
        "ParticleIntensity",
        "Intensity",
        "intensity"
    };

    static readonly string[] FrequencyAliases =
    {
        WaterLiliesVfxController.VisibleFrequencyProperty,
        "ParticleFrequency",
        "Frequency",
        "frequency"
    };

    [Header("VFX Binding")]
    [SerializeField] VisualEffect _visualEffect;
    [SerializeField] MonaLisaVfxController _legacyVfxController;

    [Header("Natural Texture")]
    [SerializeField] bool _modulationEnabled = true;
    [Tooltip("How much the natural water texture breathes around the condition intensity. Keep small for formal experiments.")]
    [SerializeField, Range(0f, 0.35f)] float _intensityDepth = 0.12f;
    [Tooltip("How much the natural water texture breathes around the condition frequency. Keep small for formal experiments.")]
    [SerializeField, Range(0f, 0.35f)] float _frequencyDepth = 0.08f;
    [Tooltip("Slow whole-surface drift, in seconds.")]
    [SerializeField, Min(0.1f)] float _largeScaleSeconds = 17f;
    [Tooltip("Mid-scale local variation, in seconds.")]
    [SerializeField, Min(0.1f)] float _mediumScaleSeconds = 7f;
    [Tooltip("Fine shimmer variation, in seconds.")]
    [SerializeField, Min(0.1f)] float _fineScaleSeconds = 2.5f;
    [SerializeField, Min(0f)] float _smoothingSeconds = 1.2f;
    [SerializeField] int _seed = 1439;

    [Header("VFX Property Names")]
    [SerializeField] string _intensityProperty = DefaultIntensityProperty;
    [SerializeField] string _frequencyProperty = DefaultFrequencyProperty;
    [SerializeField] bool _logMissingProperties = true;

    float _baseIntensity;
    float _baseFrequency;
    float _appliedIntensity;
    float _appliedFrequency;
    double _templateStartedRealtime = double.NaN;
    double _lastApplyRealtime = double.NaN;
    bool _hasBaseParameters;
    bool _missingPropertyWarningIssued;

    public bool modulationEnabled => _modulationEnabled;
    public bool hasAppliedParameters => _hasBaseParameters;
    public float baseIntensity => _baseIntensity;
    public float baseFrequency => _baseFrequency;
    public float appliedIntensity => _appliedIntensity;
    public float appliedFrequency => _appliedFrequency;
    public double templateElapsedSeconds => GetTemplateElapsedSeconds(Time.realtimeSinceStartupAsDouble);
    public int seed => _seed;
    public float intensityDepth => _intensityDepth;
    public float frequencyDepth => _frequencyDepth;
    public float largeScaleSeconds => _largeScaleSeconds;
    public float mediumScaleSeconds => _mediumScaleSeconds;
    public float fineScaleSeconds => _fineScaleSeconds;

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        _legacyVfxController = GetComponent<MonaLisaVfxController>();
    }

    void Awake()
    {
        ResolveReferences();
    }

    void OnValidate()
    {
        _intensityDepth = Mathf.Clamp(_intensityDepth, 0f, 0.35f);
        _frequencyDepth = Mathf.Clamp(_frequencyDepth, 0f, 0.35f);
        _largeScaleSeconds = Mathf.Max(0.1f, _largeScaleSeconds);
        _mediumScaleSeconds = Mathf.Max(0.1f, _mediumScaleSeconds);
        _fineScaleSeconds = Mathf.Max(0.1f, _fineScaleSeconds);
        _smoothingSeconds = Mathf.Max(0f, _smoothingSeconds);
        _missingPropertyWarningIssued = false;
        ResolveReferences();
    }

    void Update()
    {
        if (_modulationEnabled)
        {
            ApplyAt(Time.realtimeSinceStartupAsDouble, false);
        }
    }

    public void Bind(VisualEffect visualEffect, MonaLisaVfxController legacyVfxController = null)
    {
        _visualEffect = visualEffect;
        _legacyVfxController = legacyVfxController != null
            ? legacyVfxController
            : visualEffect != null ? visualEffect.GetComponent<MonaLisaVfxController>() : _legacyVfxController;
        _missingPropertyWarningIssued = false;
    }

    public void SetModulationEnabled(bool enabled)
    {
        _modulationEnabled = enabled;
        if (!enabled && _hasBaseParameters)
        {
            _appliedIntensity = _baseIntensity;
            _appliedFrequency = _baseFrequency;
            ApplyParticleControls(_appliedIntensity, _appliedFrequency, true);
        }
    }

    public void SetBaseParameters(
        float intensity,
        float frequency,
        bool warnAboutMissingProperties,
        bool resetTemplatePhase = false)
    {
        var realtime = Time.realtimeSinceStartupAsDouble;
        if (resetTemplatePhase)
        {
            ResetTemplatePhaseAt(realtime);
        }

        _baseIntensity = Mathf.Max(0f, intensity);
        _baseFrequency = Mathf.Max(0f, frequency);
        _hasBaseParameters = true;

        if (!_modulationEnabled)
        {
            _appliedIntensity = _baseIntensity;
            _appliedFrequency = _baseFrequency;
            ApplyParticleControls(_appliedIntensity, _appliedFrequency, warnAboutMissingProperties);
            return;
        }

        ApplyAt(realtime, true, warnAboutMissingProperties);
    }

    public void ApplyNow(bool warnAboutMissingProperties = true)
    {
        ApplyAt(Time.realtimeSinceStartupAsDouble, true, warnAboutMissingProperties);
    }

    public void ResetTemplatePhase()
    {
        var realtime = Time.realtimeSinceStartupAsDouble;
        ResetTemplatePhaseAt(realtime);
        if (_hasBaseParameters)
        {
            ApplyAt(realtime, true, false);
        }
    }

    public void ResetTemplatePhaseAt(double realtime)
    {
        _templateStartedRealtime = Math.Max(0.0, realtime);
        _lastApplyRealtime = double.NaN;
    }

    public double GetTemplateElapsedSeconds(double realtime)
    {
        if (double.IsNaN(_templateStartedRealtime))
        {
            return double.NaN;
        }

        return Math.Max(0.0, realtime - _templateStartedRealtime);
    }

    public void Configure(
        float intensityDepth,
        float frequencyDepth,
        float largeScaleSeconds,
        float mediumScaleSeconds,
        float fineScaleSeconds,
        int seed)
    {
        _intensityDepth = Mathf.Clamp(intensityDepth, 0f, 0.35f);
        _frequencyDepth = Mathf.Clamp(frequencyDepth, 0f, 0.35f);
        _largeScaleSeconds = Mathf.Max(0.1f, largeScaleSeconds);
        _mediumScaleSeconds = Mathf.Max(0.1f, mediumScaleSeconds);
        _fineScaleSeconds = Mathf.Max(0.1f, fineScaleSeconds);
        _seed = seed;
    }

    public static float EvaluateFractalEnvelope(
        double timeSeconds,
        int seed,
        float largeScaleSeconds,
        float mediumScaleSeconds,
        float fineScaleSeconds,
        float phaseOffset)
    {
        var time = (float)Math.Max(0.0, timeSeconds);
        var large = NoiseSigned(seed, phaseOffset, time / Mathf.Max(0.1f, largeScaleSeconds));
        var medium = NoiseSigned(seed + 7919, phaseOffset + 3.17f, time / Mathf.Max(0.1f, mediumScaleSeconds));
        var fine = NoiseSigned(seed + 15443, phaseOffset + 9.31f, time / Mathf.Max(0.1f, fineScaleSeconds));
        return Mathf.Clamp((large * 0.56f) + (medium * 0.30f) + (fine * 0.14f), -1f, 1f);
    }

    public static float ApplyDepth(float baseValue, float depth, float envelope)
    {
        var clampedDepth = Mathf.Clamp(depth, 0f, 0.35f);
        return Mathf.Max(0f, baseValue * (1f + clampedDepth * Mathf.Clamp(envelope, -1f, 1f)));
    }

    void ApplyAt(
        double realtime,
        bool snapToTarget,
        bool warnAboutMissingProperties = false)
    {
        if (!_hasBaseParameters)
        {
            return;
        }

        if (double.IsNaN(_templateStartedRealtime))
        {
            _templateStartedRealtime = realtime;
        }

        var targetIntensity = _baseIntensity;
        var targetFrequency = _baseFrequency;
        if (_modulationEnabled && _baseFrequency > 0f)
        {
            var templateElapsed = GetTemplateElapsedSeconds(realtime);
            var intensityEnvelope = EvaluateFractalEnvelope(
                templateElapsed,
                _seed,
                _largeScaleSeconds,
                _mediumScaleSeconds,
                _fineScaleSeconds,
                0.0f);
            var frequencyEnvelope = EvaluateFractalEnvelope(
                templateElapsed + 11.0,
                _seed + 37,
                _largeScaleSeconds * 1.19f,
                _mediumScaleSeconds * 0.91f,
                _fineScaleSeconds * 1.07f,
                5.0f);
            targetIntensity = ApplyDepth(_baseIntensity, _intensityDepth, intensityEnvelope);
            targetFrequency = ApplyDepth(_baseFrequency, _frequencyDepth, frequencyEnvelope);
        }

        if (snapToTarget || double.IsNaN(_lastApplyRealtime) || _smoothingSeconds <= 0f)
        {
            _appliedIntensity = targetIntensity;
            _appliedFrequency = targetFrequency;
        }
        else
        {
            var dt = Mathf.Max(0f, (float)(realtime - _lastApplyRealtime));
            var blend = 1f - Mathf.Exp(-dt / Mathf.Max(0.001f, _smoothingSeconds));
            _appliedIntensity = Mathf.Lerp(_appliedIntensity, targetIntensity, blend);
            _appliedFrequency = Mathf.Lerp(_appliedFrequency, targetFrequency, blend);
        }

        _lastApplyRealtime = realtime;
        ApplyParticleControls(_appliedIntensity, _appliedFrequency, warnAboutMissingProperties);
    }

    void ResolveReferences()
    {
        if (_visualEffect == null)
        {
            _visualEffect = GetComponent<VisualEffect>();
        }

        if (_legacyVfxController == null)
        {
            _legacyVfxController = GetComponent<MonaLisaVfxController>();
        }
    }

    void ApplyParticleControls(float intensity, float frequency, bool warnAboutMissingProperties)
    {
        ResolveReferences();

        if (_legacyVfxController != null)
        {
            _legacyVfxController.SetAnimatedParticleControls(intensity, frequency);
        }

        if (_visualEffect == null || _visualEffect.visualEffectAsset == null)
        {
            return;
        }

        var missing = warnAboutMissingProperties && _logMissingProperties && !_missingPropertyWarningIssued
            ? new List<string>()
            : null;

        TrySetFloat(_intensityProperty, IntensityAliases, intensity, missing);
        TrySetFloat(_frequencyProperty, FrequencyAliases, frequency, missing);

        if (missing == null || missing.Count == 0)
        {
            return;
        }

        _missingPropertyWarningIssued = true;
        Debug.LogWarning(
            "[WaterLiliesNaturalVortexModulator] Missing exposed VFX float properties: " +
            string.Join(", ", missing) +
            ". The natural texture layer can only modulate exposed intensity/frequency properties.",
            this);
    }

    void TrySetFloat(string propertyName, string[] aliases, float value, List<string> missingProperties)
    {
        if (TrySetFloatByName(propertyName, value))
        {
            return;
        }

        for (var i = 0; i < aliases.Length; i++)
        {
            if (string.Equals(propertyName, aliases[i], StringComparison.Ordinal))
            {
                continue;
            }

            if (TrySetFloatByName(aliases[i], value))
            {
                return;
            }
        }

        if (missingProperties != null)
        {
            missingProperties.Add(string.IsNullOrEmpty(propertyName) ? "(empty)" : propertyName);
        }
    }

    bool TrySetFloatByName(string propertyName, float value)
    {
        if (string.IsNullOrEmpty(propertyName) || _visualEffect == null || !_visualEffect.HasFloat(propertyName))
        {
            return false;
        }

        _visualEffect.SetFloat(propertyName, value);
        return true;
    }

    static float NoiseSigned(int seed, float phaseOffset, float time)
    {
        var seedA = Mathf.Repeat(seed * 0.013451f + phaseOffset * 2.371f, 1024f);
        var seedB = Mathf.Repeat(seed * 0.021731f + phaseOffset * 5.113f, 1024f);
        return (Mathf.PerlinNoise(seedA + time, seedB + time * 0.37f) * 2f) - 1f;
    }
}
