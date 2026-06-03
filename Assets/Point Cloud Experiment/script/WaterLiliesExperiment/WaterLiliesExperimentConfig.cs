using System;
using System.Collections.Generic;
using UnityEngine;

public enum WaterLiliesExperimentMode
{
    Pilot,
    Formal
}

public enum WaterLiliesParameterLevel
{
    Low,
    Medium,
    High
}

public enum WaterLiliesExperimentPhase
{
    Idle,
    Baseline,
    Adaptation,
    ConditionPrepare,
    RecenterStabilization,
    PreConditionBaseline,
    ConditionStartCue,
    ConditionViewing,
    QuestionnaireBreak,
    Rest,
    Finished,
    Aborted
}

[Serializable]
public struct WaterLiliesLevelValues
{
    public const float DefaultLow = 0.15f;
    public const float DefaultMedium = 0.4f;
    public const float DefaultHigh = 0.65f;

    [Min(0f)] public float low;
    [Min(0f)] public float medium;
    [Min(0f)] public float high;

    public WaterLiliesLevelValues(float low, float medium, float high)
    {
        this.low = low;
        this.medium = medium;
        this.high = high;
    }

    public static WaterLiliesLevelValues CreateDefault()
    {
        return new WaterLiliesLevelValues(DefaultLow, DefaultMedium, DefaultHigh);
    }

    public float Get(WaterLiliesParameterLevel level)
    {
        switch (level)
        {
            case WaterLiliesParameterLevel.Low:
                return Mathf.Max(0f, low);
            case WaterLiliesParameterLevel.Medium:
                return Mathf.Max(0f, medium);
            case WaterLiliesParameterLevel.High:
                return Mathf.Max(0f, high);
            default:
                return Mathf.Max(0f, medium);
        }
    }

    public void Normalize()
    {
        low = Mathf.Max(0f, low);
        medium = Mathf.Max(0f, medium);
        high = Mathf.Max(0f, high);
    }
}

[Serializable]
public sealed class WaterLiliesConditionDefinition
{
    public string conditionId = "C1";
    public WaterLiliesParameterLevel intensityLevel = WaterLiliesParameterLevel.Low;
    public WaterLiliesParameterLevel frequencyLevel = WaterLiliesParameterLevel.Low;
    [Tooltip("Use the global condition duration unless this is enabled.")]
    public bool overrideDurationSeconds;
    [Min(1f)] public float durationSeconds = 90f;

    public WaterLiliesConditionDefinition()
    {
    }

    public WaterLiliesConditionDefinition(
        string conditionId,
        WaterLiliesParameterLevel intensityLevel,
        WaterLiliesParameterLevel frequencyLevel)
    {
        this.conditionId = conditionId;
        this.intensityLevel = intensityLevel;
        this.frequencyLevel = frequencyLevel;
    }

    public void Normalize(float fallbackDurationSeconds)
    {
        if (string.IsNullOrWhiteSpace(conditionId))
        {
            conditionId = "C?";
        }

        durationSeconds = Mathf.Max(1f, durationSeconds <= 0f ? fallbackDurationSeconds : durationSeconds);
    }
}

[Serializable]
public sealed class WaterLiliesDurationProfile
{
    [SerializeField, Min(1f)] float _baselineSeconds = 90f;
    [SerializeField, Min(1f)] float _adaptationSeconds = 60f;
    [SerializeField, Min(1f)] float _conditionSeconds = 90f;
    [SerializeField, Min(0f)] float _preConditionBaselineSeconds = 12f;
    [SerializeField, Min(0f)] float _preConditionBaselineAnalysisSeconds = 6f;
    [SerializeField, Min(0f)] float _questionnaireMinimumSeconds;
    [SerializeField, Min(1f)] float _recenterSeconds = 7f;
    [SerializeField, Min(0f)] float _restSeconds = 30f;
    [SerializeField, Min(1)] int _restEveryConditionCount = 3;

    public WaterLiliesDurationProfile()
    {
    }

    public WaterLiliesDurationProfile(
        float baselineSeconds,
        float adaptationSeconds,
        float conditionSeconds,
        float questionnaireMinimumSeconds,
        float recenterSeconds,
        float restSeconds,
        int restEveryConditionCount,
        float preConditionBaselineSeconds = 12f,
        float preConditionBaselineAnalysisSeconds = 6f)
    {
        _baselineSeconds = baselineSeconds;
        _adaptationSeconds = adaptationSeconds;
        _conditionSeconds = conditionSeconds;
        _preConditionBaselineSeconds = preConditionBaselineSeconds;
        _preConditionBaselineAnalysisSeconds = preConditionBaselineAnalysisSeconds;
        _questionnaireMinimumSeconds = questionnaireMinimumSeconds;
        _recenterSeconds = recenterSeconds;
        _restSeconds = restSeconds;
        _restEveryConditionCount = restEveryConditionCount;
        Normalize();
    }

    public float baselineSeconds => Mathf.Max(1f, _baselineSeconds);
    public float adaptationSeconds => Mathf.Max(1f, _adaptationSeconds);
    public float conditionSeconds => Mathf.Max(1f, _conditionSeconds);
    public float preConditionBaselineSeconds => Mathf.Max(0f, _preConditionBaselineSeconds);
    public float preConditionBaselineAnalysisSeconds => Mathf.Min(
        preConditionBaselineSeconds,
        Mathf.Max(0f, _preConditionBaselineAnalysisSeconds));
    public float questionnaireMinimumSeconds => Mathf.Max(0f, _questionnaireMinimumSeconds);
    public float recenterSeconds => Mathf.Max(1f, _recenterSeconds);
    public float restSeconds => Mathf.Max(0f, _restSeconds);
    public int restEveryConditionCount => Mathf.Max(1, _restEveryConditionCount);

    public static WaterLiliesDurationProfile CreateDefault()
    {
        return new WaterLiliesDurationProfile(90f, 60f, 90f, 0f, 7f, 30f, 3);
    }

    public void CopyFrom(WaterLiliesDurationProfile source)
    {
        if (source == null)
        {
            return;
        }

        _baselineSeconds = source.baselineSeconds;
        _adaptationSeconds = source.adaptationSeconds;
        _conditionSeconds = source.conditionSeconds;
        _preConditionBaselineSeconds = source.preConditionBaselineSeconds;
        _preConditionBaselineAnalysisSeconds = source.preConditionBaselineAnalysisSeconds;
        _questionnaireMinimumSeconds = source.questionnaireMinimumSeconds;
        _recenterSeconds = source.recenterSeconds;
        _restSeconds = source.restSeconds;
        _restEveryConditionCount = source.restEveryConditionCount;
        Normalize();
    }

    public void Normalize()
    {
        _baselineSeconds = Mathf.Max(1f, _baselineSeconds);
        _adaptationSeconds = Mathf.Max(1f, _adaptationSeconds);
        _conditionSeconds = Mathf.Max(1f, _conditionSeconds);
        _preConditionBaselineSeconds = Mathf.Max(0f, _preConditionBaselineSeconds);
        _preConditionBaselineAnalysisSeconds = Mathf.Min(
            _preConditionBaselineSeconds,
            Mathf.Max(0f, _preConditionBaselineAnalysisSeconds));
        _questionnaireMinimumSeconds = Mathf.Max(0f, _questionnaireMinimumSeconds);
        _recenterSeconds = Mathf.Max(1f, _recenterSeconds);
        _restSeconds = Mathf.Max(0f, _restSeconds);
        _restEveryConditionCount = Mathf.Max(1, _restEveryConditionCount);
    }
}

public struct WaterLiliesResolvedCondition
{
    public string conditionId;
    public WaterLiliesParameterLevel intensityLevel;
    public WaterLiliesParameterLevel frequencyLevel;
    public float intensityValue;
    public float frequencyValue;
    public float durationSeconds;
}

[CreateAssetMenu(menuName = "Water Lilies Experiment/Config", fileName = "WaterLiliesExperimentConfig")]
public sealed class WaterLiliesExperimentConfig : ScriptableObject
{
    [Header("Session")]
    [SerializeField] string _participantId = "P001";
    [SerializeField] WaterLiliesExperimentMode _mode = WaterLiliesExperimentMode.Pilot;

    [Header("Fixed Painting")]
    [SerializeField] string _targetPaintingObjectName = "5_Water_Lilies";
    [SerializeField] bool _isolateTargetPainting = true;
    [SerializeField] bool _disableLegacyPaintingControllers = true;

    [Header("Durations")]
    [SerializeField, HideInInspector, Min(1f)] float _baselineSeconds = 90f;
    [SerializeField, HideInInspector, Min(1f)] float _adaptationSeconds = 60f;
    [SerializeField, HideInInspector, Min(1f)] float _conditionSeconds = 90f;
    [SerializeField, HideInInspector, Min(0f)] float _preConditionBaselineSeconds = 12f;
    [SerializeField, HideInInspector, Min(0f)] float _preConditionBaselineAnalysisSeconds = 6f;
    [SerializeField, HideInInspector, Min(0f)] float _questionnaireMinimumSeconds;
    [SerializeField, HideInInspector, Min(1f)] float _recenterSeconds = 7f;
    [SerializeField, HideInInspector, Min(0f)] float _restSeconds = 30f;
    [SerializeField, HideInInspector, Min(1)] int _restEveryConditionCount = 3;
    [SerializeField, HideInInspector] bool _durationProfilesMigrated;
    [SerializeField] WaterLiliesDurationProfile _formalDurations = WaterLiliesDurationProfile.CreateDefault();
    [SerializeField] WaterLiliesDurationProfile _pilotDurations = WaterLiliesDurationProfile.CreateDefault();

    [Header("Parameter Tables")]
    [SerializeField] WaterLiliesLevelValues _intensityValues = WaterLiliesLevelValues.CreateDefault();
    [SerializeField] WaterLiliesLevelValues _frequencyValues = WaterLiliesLevelValues.CreateDefault();
    [SerializeField, Min(0f)] float _baselineIntensity = 0.01f;
    [SerializeField, Min(0f)] float _baselineFrequency;

    [Header("Conditions")]
    [SerializeField] WaterLiliesConditionDefinition[] _conditions = CreateDefaultConditions();
    [SerializeField] string[] _conditionOrder = CreateDefaultConditionOrder();

    [Header("Control")]
    [SerializeField] bool _autoStart;
    [SerializeField] bool _requireManualQuestionnaireContinue = true;
    [Tooltip("When the operator or headset presence sensor marks the headset as removed, the questionnaire break cannot end until headset_worn is recorded.")]
    [SerializeField] bool _requireHeadsetWornBeforeQuestionnaireContinue = true;
    [Tooltip("Require each questionnaire break to include both headset_removed and headset_worn before Continue can advance. Disable for editor-only dry runs.")]
    [SerializeField] bool _requireHeadsetCycleBeforeQuestionnaireContinue = true;
    [SerializeField] bool _autoDetectHeadsetPresence = true;
    [SerializeField] bool _allowPilotSkipShortcut = true;

    [Header("Logging")]
    [SerializeField] bool _writeCsv = true;
    [SerializeField] bool _writeJsonLines = true;
    [SerializeField] string _logFolderName = "water_lilies_vfx_experiment";
    [SerializeField] bool _useExternalLogRoot;
    [SerializeField] string _externalLogRootPath = "";
    [SerializeField, Min(0.02f)] float _sampleIntervalSeconds = 0.1f;

    public string participantId => string.IsNullOrWhiteSpace(_participantId) ? "P001" : _participantId.Trim();
    public WaterLiliesExperimentMode mode => _mode;
    public string targetPaintingObjectName => string.IsNullOrWhiteSpace(_targetPaintingObjectName) ? "5_Water_Lilies" : _targetPaintingObjectName.Trim();
    public bool isolateTargetPainting => _isolateTargetPainting;
    public bool disableLegacyPaintingControllers => _disableLegacyPaintingControllers;
    public WaterLiliesDurationProfile formalDurations
    {
        get
        {
            EnsureDurationProfilesInitialized();
            return _formalDurations;
        }
    }

    public WaterLiliesDurationProfile pilotDurations
    {
        get
        {
            EnsureDurationProfilesInitialized();
            return _pilotDurations;
        }
    }

    public WaterLiliesDurationProfile activeDurations
    {
        get
        {
            EnsureDurationProfilesInitialized();
            return _mode == WaterLiliesExperimentMode.Formal ? _formalDurations : _pilotDurations;
        }
    }

    public float baselineSeconds => activeDurations.baselineSeconds;
    public float adaptationSeconds => activeDurations.adaptationSeconds;
    public float conditionSeconds => activeDurations.conditionSeconds;
    public float preConditionBaselineSeconds => activeDurations.preConditionBaselineSeconds;
    public float preConditionBaselineAnalysisSeconds => activeDurations.preConditionBaselineAnalysisSeconds;
    public float questionnaireMinimumSeconds => activeDurations.questionnaireMinimumSeconds;
    public float recenterSeconds => activeDurations.recenterSeconds;
    public float restSeconds => activeDurations.restSeconds;
    public int restEveryConditionCount => activeDurations.restEveryConditionCount;
    public WaterLiliesLevelValues intensityValues => _intensityValues;
    public WaterLiliesLevelValues frequencyValues => _frequencyValues;
    public float baselineIntensity => Mathf.Max(0f, _baselineIntensity);
    public float baselineFrequency => Mathf.Max(0f, _baselineFrequency);
    public bool autoStart => _autoStart;
    public bool requireManualQuestionnaireContinue => _requireManualQuestionnaireContinue;
    public bool requireHeadsetWornBeforeQuestionnaireContinue => _requireHeadsetWornBeforeQuestionnaireContinue;
    public bool requireHeadsetCycleBeforeQuestionnaireContinue => _requireHeadsetCycleBeforeQuestionnaireContinue;
    public bool autoDetectHeadsetPresence => _autoDetectHeadsetPresence;
    public bool allowPilotSkipShortcut => _allowPilotSkipShortcut;
    public bool writeCsv => _writeCsv;
    public bool writeJsonLines => _writeJsonLines;
    public string logFolderName => string.IsNullOrWhiteSpace(_logFolderName) ? "water_lilies_vfx_experiment" : _logFolderName.Trim();
    public bool useExternalLogRoot => _useExternalLogRoot && !string.IsNullOrWhiteSpace(_externalLogRootPath);
    public string externalLogRootPath => _externalLogRootPath;
    public float sampleIntervalSeconds => Mathf.Max(0.02f, _sampleIntervalSeconds);

    public static WaterLiliesConditionDefinition[] CreateDefaultConditions()
    {
        return new[]
        {
            new WaterLiliesConditionDefinition("C1", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Low),
            new WaterLiliesConditionDefinition("C2", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Medium),
            new WaterLiliesConditionDefinition("C3", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.High),
            new WaterLiliesConditionDefinition("C4", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Low),
            new WaterLiliesConditionDefinition("C5", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Medium),
            new WaterLiliesConditionDefinition("C6", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.High),
            new WaterLiliesConditionDefinition("C7", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Low),
            new WaterLiliesConditionDefinition("C8", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Medium),
            new WaterLiliesConditionDefinition("C9", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.High)
        };
    }

    public static string[] CreateDefaultConditionOrder()
    {
        return new[] { "C1", "C2", "C3", "C4", "C5", "C6", "C7", "C8", "C9" };
    }

    void OnValidate()
    {
        EnsureDurationProfilesInitialized();
        _sampleIntervalSeconds = Mathf.Max(0.02f, _sampleIntervalSeconds);
        _intensityValues.Normalize();
        _frequencyValues.Normalize();
        _baselineIntensity = Mathf.Max(0f, _baselineIntensity);
        _baselineFrequency = Mathf.Max(0f, _baselineFrequency);

        if (_conditions == null || _conditions.Length == 0)
        {
            _conditions = CreateDefaultConditions();
        }

        for (var i = 0; i < _conditions.Length; i++)
        {
            if (_conditions[i] == null)
            {
                _conditions[i] = new WaterLiliesConditionDefinition();
            }

            _conditions[i].Normalize(conditionSeconds);
        }

        if (_conditionOrder == null || _conditionOrder.Length == 0)
        {
            _conditionOrder = CreateDefaultConditionOrder();
        }
    }

    public void BuildResolvedConditionList(List<WaterLiliesResolvedCondition> output)
    {
        output.Clear();
        var order = _conditionOrder == null || _conditionOrder.Length == 0
            ? CreateDefaultConditionOrder()
            : _conditionOrder;

        for (var i = 0; i < order.Length; i++)
        {
            if (!TryResolveCondition(order[i], out var condition))
            {
                Debug.LogWarning("[WaterLiliesExperimentConfig] Unknown condition ID in order list: " + order[i], this);
                continue;
            }

            output.Add(condition);
        }
    }

    public bool TryResolveCondition(string conditionId, out WaterLiliesResolvedCondition resolved)
    {
        resolved = default;
        if (string.IsNullOrWhiteSpace(conditionId))
        {
            return false;
        }

        var conditions = _conditions == null || _conditions.Length == 0
            ? CreateDefaultConditions()
            : _conditions;

        for (var i = 0; i < conditions.Length; i++)
        {
            var condition = conditions[i];
            if (condition == null ||
                !string.Equals(condition.conditionId, conditionId.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            resolved = new WaterLiliesResolvedCondition
            {
                conditionId = condition.conditionId,
                intensityLevel = condition.intensityLevel,
                frequencyLevel = condition.frequencyLevel,
                intensityValue = _intensityValues.Get(condition.intensityLevel),
                frequencyValue = _frequencyValues.Get(condition.frequencyLevel),
                durationSeconds = condition.overrideDurationSeconds
                    ? Mathf.Max(1f, condition.durationSeconds)
                    : conditionSeconds
            };
            return true;
        }

        return false;
    }

    void EnsureDurationProfilesInitialized()
    {
        if (_formalDurations == null)
        {
            _formalDurations = WaterLiliesDurationProfile.CreateDefault();
        }

        if (_pilotDurations == null)
        {
            _pilotDurations = WaterLiliesDurationProfile.CreateDefault();
        }

        if (!_durationProfilesMigrated)
        {
            var legacyDurations = new WaterLiliesDurationProfile(
                _baselineSeconds,
                _adaptationSeconds,
                _conditionSeconds,
                _questionnaireMinimumSeconds,
                _recenterSeconds,
                _restSeconds,
                _restEveryConditionCount,
                _preConditionBaselineSeconds,
                _preConditionBaselineAnalysisSeconds);
            _formalDurations.CopyFrom(legacyDurations);
            _pilotDurations.CopyFrom(legacyDurations);
            _durationProfilesMigrated = true;
        }

        _formalDurations.Normalize();
        _pilotDurations.Normalize();
    }
}
