using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(VisualEffect))]
[AddComponentMenu("Point Cloud/Mona Lisa VFX Controller")]
public sealed class MonaLisaVfxController : MonoBehaviour
{
    public const string DefaultSpawnRateProperty = "System Spawn - rate";
    public const string DefaultParticleIntensityProperty = "System update particle - intensity";
    public const string DefaultParticleDragProperty = "System update particle - drag";
    public const string DefaultParticleFrequencyProperty = "System update particle - frequency";
    public const string DefaultCubeSmoothnessProperty = "System hdrp lit cube - smoothness";
    public const string DefaultCubeMetallicProperty = "System hdrp lit cube - metallic";
    public const string VisibleSpawnRateProperty = "System Spawn - Rate";
    public const string VisibleParticleIntensityProperty = "System update particle - Intensity";
    public const string VisibleParticleDragProperty = "System update particle - Drag";
    public const string VisibleParticleFrequencyProperty = "System update particle - Frequency";
    public const string VisibleCubeSmoothnessProperty = "System hdrp lit cube - Smoothness";
    public const string VisibleCubeMetallicProperty = "System hdrp lit cube - Metallic";

    const string LegacySpawnRateProperty = "SpawnRate";
    const string LegacyParticleIntensityProperty = "ParticleIntensity";
    const string LegacyParticleFrequencyProperty = "ParticleFrequency";
    const string LegacyCubeSmoothnessProperty = "CubeSmoothness";
    const string LegacyCubeMetallicProperty = "CubeMetallic";
    const string MissingPropertyName = "(empty property name)";

    static readonly string[] SpawnRatePropertyAliases =
    {
        VisibleSpawnRateProperty
    };

    static readonly string[] ParticleIntensityPropertyAliases =
    {
        VisibleParticleIntensityProperty
    };

    static readonly string[] ParticleDragPropertyAliases =
    {
        VisibleParticleDragProperty
    };

    static readonly string[] ParticleFrequencyPropertyAliases =
    {
        VisibleParticleFrequencyProperty
    };

    static readonly string[] CubeSmoothnessPropertyAliases =
    {
        VisibleCubeSmoothnessProperty
    };

    static readonly string[] CubeMetallicPropertyAliases =
    {
        VisibleCubeMetallicProperty
    };

    [Header("VFX Binding")]
    [SerializeField] VisualEffect _visualEffect;

    [Header("Controls")]
    [SerializeField, Min(0f)] float _spawnRate = 180000f;
    [SerializeField, Min(0f)] float _particleIntensity = 0.01f;
    [SerializeField, Min(0f)] float _particleDrag = 1f;
    [SerializeField, Min(0f)] float _particleFrequency = 0.01f;
    [SerializeField, Range(0f, 1f)] float _cubeSmoothness = 0.8f;
    [SerializeField, Range(0f, 1f)] float _cubeMetallic = 0.7f;

    [Header("VFX Property Names")]
    [SerializeField] string _spawnRateProperty = DefaultSpawnRateProperty;
    [SerializeField] string _particleIntensityProperty = DefaultParticleIntensityProperty;
    [SerializeField] string _particleDragProperty = DefaultParticleDragProperty;
    [SerializeField] string _particleFrequencyProperty = DefaultParticleFrequencyProperty;
    [SerializeField] string _cubeSmoothnessProperty = DefaultCubeSmoothnessProperty;
    [SerializeField] string _cubeMetallicProperty = DefaultCubeMetallicProperty;

    [Header("Runtime")]
    [SerializeField] bool _applyContinuously = true;
    [SerializeField] bool _logMissingProperties = true;

    bool _missingPropertyWarningIssued;

    public float spawnRate
    {
        get => _spawnRate;
        set
        {
            _spawnRate = Mathf.Max(0f, value);
            Apply();
        }
    }

    public float particleIntensity
    {
        get => _particleIntensity;
        set
        {
            _particleIntensity = Mathf.Max(0f, value);
            Apply();
        }
    }

    public float particleFrequency
    {
        get => _particleFrequency;
        set
        {
            _particleFrequency = Mathf.Max(0f, value);
            Apply();
        }
    }

    public float particleDrag
    {
        get => _particleDrag;
        set
        {
            _particleDrag = Mathf.Max(0f, value);
            Apply();
        }
    }

    public float cubeSmoothness
    {
        get => _cubeSmoothness;
        set
        {
            _cubeSmoothness = Mathf.Clamp01(value);
            Apply();
        }
    }

    public float cubeMetallic
    {
        get => _cubeMetallic;
        set
        {
            _cubeMetallic = Mathf.Clamp01(value);
            Apply();
        }
    }

    public void SetControls(
        float spawnRate,
        float particleIntensity,
        float particleFrequency,
        float cubeSmoothness,
        float cubeMetallic)
    {
        SetControls(spawnRate, particleIntensity, _particleDrag, particleFrequency, cubeSmoothness, cubeMetallic);
    }

    public void SetControls(
        float spawnRate,
        float particleIntensity,
        float particleDrag,
        float particleFrequency,
        float cubeSmoothness,
        float cubeMetallic)
    {
        _spawnRate = Mathf.Max(0f, spawnRate);
        _particleIntensity = Mathf.Max(0f, particleIntensity);
        _particleDrag = Mathf.Max(0f, particleDrag);
        _particleFrequency = Mathf.Max(0f, particleFrequency);
        _cubeSmoothness = Mathf.Clamp01(cubeSmoothness);
        _cubeMetallic = Mathf.Clamp01(cubeMetallic);
        Apply();
    }

    public void SetAnimatedParticleControls(float particleIntensity, float particleFrequency)
    {
        _particleIntensity = Mathf.Clamp01(particleIntensity);
        _particleFrequency = Mathf.Clamp01(particleFrequency);
        Apply();
    }

    [ContextMenu("Apply To VFX")]
    public void Apply()
    {
        ApplyToVisualEffect(true);
    }

    [ContextMenu("Reset Property Names")]
    public void ResetPropertyNames()
    {
        _spawnRateProperty = DefaultSpawnRateProperty;
        _particleIntensityProperty = DefaultParticleIntensityProperty;
        _particleDragProperty = DefaultParticleDragProperty;
        _particleFrequencyProperty = DefaultParticleFrequencyProperty;
        _cubeSmoothnessProperty = DefaultCubeSmoothnessProperty;
        _cubeMetallicProperty = DefaultCubeMetallicProperty;
        _missingPropertyWarningIssued = false;
        Apply();
    }

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        ResetPropertyNames();
    }

    void OnEnable()
    {
        EnsureVisualEffect();
        MigrateLegacyPropertyNames();
        Apply();
    }

    void OnValidate()
    {
        _spawnRate = Mathf.Max(0f, _spawnRate);
        _particleIntensity = Mathf.Max(0f, _particleIntensity);
        _particleDrag = Mathf.Max(0f, _particleDrag);
        _particleFrequency = Mathf.Max(0f, _particleFrequency);
        _cubeSmoothness = Mathf.Clamp01(_cubeSmoothness);
        _cubeMetallic = Mathf.Clamp01(_cubeMetallic);
        _missingPropertyWarningIssued = false;

        EnsureVisualEffect();
        MigrateLegacyPropertyNames();
        Apply();
    }

    void Update()
    {
        if (_applyContinuously)
        {
            ApplyToVisualEffect(false);
        }
    }

    void EnsureVisualEffect()
    {
        if (_visualEffect == null)
        {
            _visualEffect = GetComponent<VisualEffect>();
        }
    }

    void ApplyToVisualEffect(bool warnAboutMissingProperties)
    {
        EnsureVisualEffect();
        if (_visualEffect == null || _visualEffect.visualEffectAsset == null)
        {
            return;
        }

        var missingProperties = warnAboutMissingProperties &&
                                _logMissingProperties &&
                                !_missingPropertyWarningIssued
            ? new List<string>()
            : null;

        TrySetFloat(_spawnRateProperty, SpawnRatePropertyAliases, _spawnRate, missingProperties);
        TrySetFloat(_particleIntensityProperty, ParticleIntensityPropertyAliases, _particleIntensity, missingProperties);
        TrySetFloat(_particleDragProperty, ParticleDragPropertyAliases, _particleDrag, missingProperties);
        TrySetFloat(_particleFrequencyProperty, ParticleFrequencyPropertyAliases, _particleFrequency, missingProperties);
        TrySetFloat(_cubeSmoothnessProperty, CubeSmoothnessPropertyAliases, _cubeSmoothness, missingProperties);
        TrySetFloat(_cubeMetallicProperty, CubeMetallicPropertyAliases, _cubeMetallic, missingProperties);

        if (missingProperties == null || missingProperties.Count == 0)
        {
            return;
        }

        _missingPropertyWarningIssued = true;
        Debug.LogWarning(
            "MonaLisaVfxController could not find these exposed VFX float properties: " +
            string.Join(", ", missingProperties) +
            ". C# can only set exposed Blackboard property reference names. Naming a VFX context or block is not enough; add exposed Float properties with these names and connect them to the matching Spawn, Update, and Output inputs.",
            this);
    }

    void TrySetFloat(string propertyName, string[] aliases, float value, List<string> missingProperties)
    {
        if (TrySetFloatByName(propertyName, value))
        {
            return;
        }

        if (aliases != null)
        {
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
        }

        if (missingProperties != null)
        {
            missingProperties.Add(GetMissingPropertyDescription(propertyName, aliases));
        }
    }

    bool TrySetFloatByName(string propertyName, float value)
    {
        if (string.IsNullOrEmpty(propertyName) || !_visualEffect.HasFloat(propertyName))
        {
            return false;
        }

        _visualEffect.SetFloat(propertyName, value);
        return true;
    }

    static string GetMissingPropertyDescription(string propertyName, string[] aliases)
    {
        var name = string.IsNullOrEmpty(propertyName) ? MissingPropertyName : propertyName;
        if (aliases == null || aliases.Length == 0)
        {
            return name;
        }

        return name + " (also tried: " + string.Join(", ", aliases) + ")";
    }

    void MigrateLegacyPropertyNames()
    {
        if (_spawnRateProperty == LegacySpawnRateProperty)
        {
            _spawnRateProperty = DefaultSpawnRateProperty;
        }

        if (_particleIntensityProperty == LegacyParticleIntensityProperty)
        {
            _particleIntensityProperty = DefaultParticleIntensityProperty;
        }

        if (string.IsNullOrEmpty(_particleDragProperty))
        {
            _particleDragProperty = DefaultParticleDragProperty;
        }

        if (_particleFrequencyProperty == LegacyParticleFrequencyProperty)
        {
            _particleFrequencyProperty = DefaultParticleFrequencyProperty;
        }

        if (_cubeSmoothnessProperty == LegacyCubeSmoothnessProperty)
        {
            _cubeSmoothnessProperty = DefaultCubeSmoothnessProperty;
        }

        if (_cubeMetallicProperty == LegacyCubeMetallicProperty)
        {
            _cubeMetallicProperty = DefaultCubeMetallicProperty;
        }
    }
}
