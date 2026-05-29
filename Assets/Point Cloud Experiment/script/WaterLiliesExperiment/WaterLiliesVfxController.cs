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

    [Header("VFX Property Names")]
    [SerializeField] string _intensityProperty = DefaultIntensityProperty;
    [SerializeField] string _frequencyProperty = DefaultFrequencyProperty;

    [Header("Hold / Baseline")]
    [SerializeField, Min(0f)] float _freezeFrequency = 0f;
    [SerializeField] bool _stopVisualEffectWhenFrozen;
    [SerializeField] bool _disableLegacyAutoAnimators = true;
    [SerializeField] bool _logMissingProperties = true;

    float _currentIntensity;
    float _currentFrequency;
    bool _missingPropertyWarningIssued;

    public float currentIntensity => _currentIntensity;
    public float currentFrequency => _currentFrequency;
    public VisualEffect visualEffect => _visualEffect;

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        _legacyVfxController = GetComponent<MonaLisaVfxController>();
    }

    void Awake()
    {
        ResolveReferences();
        DisableLegacyAutoAnimators();
    }

    void OnValidate()
    {
        _freezeFrequency = Mathf.Max(0f, _freezeFrequency);
        _missingPropertyWarningIssued = false;
        ResolveReferences();
    }

    public void Bind(VisualEffect visualEffect, MonaLisaVfxController legacyVfxController = null)
    {
        _visualEffect = visualEffect;
        _legacyVfxController = legacyVfxController != null ? legacyVfxController : visualEffect != null ? visualEffect.GetComponent<MonaLisaVfxController>() : null;
        _missingPropertyWarningIssued = false;
        DisableLegacyAutoAnimators();
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
        ApplyParameters(condition.intensityValue, condition.frequencyValue, true);
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
        _currentIntensity = Mathf.Max(0f, intensity);
        _currentFrequency = Mathf.Max(0f, frequency);

        ResolveReferences();
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
