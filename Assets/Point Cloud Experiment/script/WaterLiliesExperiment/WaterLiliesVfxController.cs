using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies VFX Controller")]
public sealed class WaterLiliesVfxController : MonoBehaviour
{
    public const string DefaultIntensityProperty = "System update particle - intensity";
    public const string DefaultFrequencyProperty = "System update particle - frequency";
    public const string VisibleIntensityProperty = "System update particle - Intensity";
    public const string VisibleFrequencyProperty = "System update particle - Frequency";

    static readonly string[] IntensityAliases = { VisibleIntensityProperty, "ParticleIntensity", "Intensity", "intensity" };
    static readonly string[] FrequencyAliases = { VisibleFrequencyProperty, "ParticleFrequency", "Frequency", "frequency" };

    [Header("VFX Binding")]
    [SerializeField] VisualEffect _visualEffect;
    [Tooltip("Existing controller on the Water Lilies object. When assigned, this script updates it so its continuous Apply loop keeps experiment values.")]
    [SerializeField] MonaLisaVfxController _legacyVfxController;
    [SerializeField] WaterLiliesNaturalVortexModulator _naturalModulator;

    [Header("VFX Property Names")]
    [SerializeField] string _intensityProperty = DefaultIntensityProperty;
    [SerializeField] string _frequencyProperty = DefaultFrequencyProperty;

    [Header("Natural Texture Modulation")]
    [Tooltip("Opt-in layer that gently modulates the existing VFX noise field so swirls feel more like water texture than fixed targets.")]
    [SerializeField] bool _enableNaturalTextureModulation;
    [SerializeField] bool _autoCreateNaturalTextureModulator = true;

    [Header("Hold / Baseline")]
    [SerializeField, Min(0f)] float _freezeFrequency = 0f;
    [SerializeField] bool _stopVisualEffectWhenFrozen;
    [SerializeField] bool _disableLegacyAutoAnimators = true;
    [SerializeField] bool _logMissingProperties = true;

    float _currentIntensity;
    float _currentFrequency;
    bool _missingPropertyWarningIssued;

    public float targetIntensity => _currentIntensity;
    public float targetFrequency => _currentFrequency;
    public float currentIntensity => NaturalModulationIsDriving() ? _naturalModulator.appliedIntensity : _currentIntensity;
    public float currentFrequency => NaturalModulationIsDriving() ? _naturalModulator.appliedFrequency : _currentFrequency;
    public bool naturalTextureModulationEnabled => NaturalModulationIsDriving();
    public double naturalTextureTemplateElapsedSeconds => NaturalModulationIsDriving() ? _naturalModulator.templateElapsedSeconds : double.NaN;
    public int naturalTextureSeed => _naturalModulator != null ? _naturalModulator.seed : -1;
    public float naturalTextureIntensityDepth => _naturalModulator != null ? _naturalModulator.intensityDepth : float.NaN;
    public float naturalTextureFrequencyDepth => _naturalModulator != null ? _naturalModulator.frequencyDepth : float.NaN;
    public float naturalTextureLargeScaleSeconds => _naturalModulator != null ? _naturalModulator.largeScaleSeconds : float.NaN;
    public float naturalTextureMediumScaleSeconds => _naturalModulator != null ? _naturalModulator.mediumScaleSeconds : float.NaN;
    public float naturalTextureFineScaleSeconds => _naturalModulator != null ? _naturalModulator.fineScaleSeconds : float.NaN;
    public VisualEffect visualEffect => _visualEffect;

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        _legacyVfxController = GetComponent<MonaLisaVfxController>();
    }

    void Awake()
    {
        ResolveReferences();
        ResolveNaturalModulator(false);
        DisableLegacyAutoAnimators();
    }

    void OnValidate()
    {
        _freezeFrequency = Mathf.Max(0f, _freezeFrequency);
        _missingPropertyWarningIssued = false;
        ResolveReferences();
        ResolveNaturalModulator(false);
    }

    public void Bind(VisualEffect visualEffect, MonaLisaVfxController legacyVfxController = null)
    {
        _visualEffect = visualEffect;
        _legacyVfxController = legacyVfxController != null ? legacyVfxController : visualEffect != null ? visualEffect.GetComponent<MonaLisaVfxController>() : null;
        _missingPropertyWarningIssued = false;
        ResolveNaturalModulator(false);
        DisableLegacyAutoAnimators();
    }

    [ContextMenu("Enable Natural Texture Modulation")]
    public void EnableNaturalTextureModulation()
    {
        _enableNaturalTextureModulation = true;
        ResolveNaturalModulator(true);
        ApplyParameters(_currentIntensity, _currentFrequency, true);
    }

    [ContextMenu("Disable Natural Texture Modulation")]
    public void DisableNaturalTextureModulation()
    {
        _enableNaturalTextureModulation = false;
        if (_naturalModulator != null)
        {
            _naturalModulator.SetModulationEnabled(false);
        }

        ApplyParameters(_currentIntensity, _currentFrequency, true);
    }

    public void ApplyBaseline(WaterLiliesExperimentConfig config)
    {
        var intensity = config != null ? config.baselineIntensity : 0f;
        var frequency = config != null ? config.baselineFrequency : 0f;
        ApplyParameters(intensity, frequency, true);
    }

    public void ApplyAdaptation(WaterLiliesExperimentConfig config)
    {
        if (config == null)
        {
            ApplyParameters(WaterLiliesLevelValues.DefaultLow, WaterLiliesLevelValues.DefaultLow, true);
            return;
        }

        ApplyParameters(
            config.intensityValues.Get(WaterLiliesParameterLevel.Low),
            config.frequencyValues.Get(WaterLiliesParameterLevel.Low),
            true);
    }

    public void ApplyCondition(WaterLiliesResolvedCondition condition)
    {
        ApplyParameters(condition.intensityValue, condition.frequencyValue, true, true);
    }

    public void FreezeFormalStimulus()
    {
        ApplyParameters(_currentIntensity, _freezeFrequency, true);
        if (_stopVisualEffectWhenFrozen && _visualEffect != null)
        {
            _visualEffect.Stop();
        }
    }

    public void ApplyParameters(float intensity, float frequency, bool warnAboutMissingProperties)
    {
        ApplyParameters(intensity, frequency, warnAboutMissingProperties, false);
    }

    public void ResetNaturalTextureTemplate()
    {
        if (!_enableNaturalTextureModulation)
        {
            return;
        }

        ResolveNaturalModulator(false);
        if (_naturalModulator != null)
        {
            _naturalModulator.ResetTemplatePhase();
        }
    }

    void ApplyParameters(
        float intensity,
        float frequency,
        bool warnAboutMissingProperties,
        bool resetNaturalTextureTemplate)
    {
        _currentIntensity = Mathf.Max(0f, intensity);
        _currentFrequency = Mathf.Max(0f, frequency);

        ResolveReferences();
        if (TryApplyNaturalModulation(warnAboutMissingProperties, resetNaturalTextureTemplate))
        {
            return;
        }

        if (_legacyVfxController != null)
        {
            _legacyVfxController.SetAnimatedParticleControls(_currentIntensity, _currentFrequency);
        }

        if (_visualEffect == null || _visualEffect.visualEffectAsset == null)
        {
            return;
        }

        if (!_visualEffect.enabled)
        {
            _visualEffect.enabled = true;
        }

        if (_stopVisualEffectWhenFrozen && _currentFrequency > 0f)
        {
            _visualEffect.Play();
        }

        var missing = warnAboutMissingProperties && _logMissingProperties && !_missingPropertyWarningIssued
            ? new List<string>()
            : null;

        TrySetFloat(_intensityProperty, IntensityAliases, _currentIntensity, missing);
        TrySetFloat(_frequencyProperty, FrequencyAliases, _currentFrequency, missing);

        if (missing == null || missing.Count == 0)
        {
            return;
        }

        _missingPropertyWarningIssued = true;
        Debug.LogWarning(
            "[WaterLiliesVfxController] Missing exposed VFX float properties: " +
            string.Join(", ", missing) +
            ". The Water Lilies VFX Graph must expose float properties for intensity and frequency.",
            this);
    }

    bool TryApplyNaturalModulation(bool warnAboutMissingProperties, bool resetNaturalTextureTemplate)
    {
        if (!_enableNaturalTextureModulation)
        {
            if (_naturalModulator != null)
            {
                _naturalModulator.SetModulationEnabled(false);
            }

            return false;
        }

        ResolveNaturalModulator(true);
        if (_naturalModulator == null)
        {
            return false;
        }

        if (_visualEffect != null)
        {
            if (!_visualEffect.enabled)
            {
                _visualEffect.enabled = true;
            }

            if (_stopVisualEffectWhenFrozen && _currentFrequency > 0f)
            {
                _visualEffect.Play();
            }
        }

        _naturalModulator.Bind(_visualEffect, _legacyVfxController);
        _naturalModulator.SetModulationEnabled(true);
        _naturalModulator.SetBaseParameters(
            _currentIntensity,
            _currentFrequency,
            warnAboutMissingProperties,
            resetNaturalTextureTemplate);
        return true;
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

    void ResolveNaturalModulator(bool createIfAllowed)
    {
        if (_naturalModulator == null)
        {
            _naturalModulator = GetComponent<WaterLiliesNaturalVortexModulator>();
        }

        if (_naturalModulator == null && createIfAllowed && _autoCreateNaturalTextureModulator)
        {
            _naturalModulator = gameObject.AddComponent<WaterLiliesNaturalVortexModulator>();
        }

        if (_naturalModulator != null)
        {
            _naturalModulator.Bind(_visualEffect, _legacyVfxController);
        }
    }

    bool NaturalModulationIsDriving()
    {
        return _enableNaturalTextureModulation &&
               _naturalModulator != null &&
               _naturalModulator.modulationEnabled &&
               _naturalModulator.hasAppliedParameters;
    }

    void DisableLegacyAutoAnimators()
    {
        if (!_disableLegacyAutoAnimators)
        {
            return;
        }

        var animators = GetComponents<StarryNightRhoneVfxAutoAnimator>();
        for (var i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].enabled = false;
            }
        }
    }

    void TrySetFloat(string propertyName, string[] aliases, float value, List<string> missingProperties)
    {
        if (TrySetFloatByName(propertyName, value))
        {
            return;
        }

        for (var i = 0; i < aliases.Length; i++)
        {
            if (string.Equals(propertyName, aliases[i]))
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
}
