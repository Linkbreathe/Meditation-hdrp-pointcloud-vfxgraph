using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.VFX;

[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Starry Night Rhone VFX Auto Animator")]
public sealed class StarryNightRhoneVfxAutoAnimator : MonoBehaviour
{
    public const string DefaultVfxEntityName = "Starry_Night_Over_the_Rhone";
    public const string DefaultParticleIntensityProperty = "System update particle - intensity";
    public const string DefaultParticleFrequencyProperty = "System update particle - frequency";

    static readonly string[] DrivenPropertyNamesBacking =
    {
        DefaultParticleIntensityProperty,
        DefaultParticleFrequencyProperty
    };

    [Header("VFX Binding")]
    [Tooltip("If Visual Effect is not assigned, the script searches this GameObject name at runtime.")]
    [SerializeField] string _vfxEntityName = DefaultVfxEntityName;
    [SerializeField] VisualEffect _visualEffect;
    [Tooltip("When present, the animator updates this controller so its continuous Apply loop keeps the animated values.")]
    [SerializeField] MonaLisaVfxController _vfxController;

    [Header("VFX Property Names")]
    [SerializeField] string _particleIntensityProperty = DefaultParticleIntensityProperty;
    [SerializeField] string _particleFrequencyProperty = DefaultParticleFrequencyProperty;

    [Header("Initial State")]
    [SerializeField, Range(0f, 1f)] float _initialParticleIntensity = 0.01f;
    [SerializeField, Range(0f, 1f)] float _initialParticleFrequency = 0f;

    [Header("Timing")]
    [SerializeField, Min(0f)] float _initialStateDuration = 10f;
    [SerializeField, Min(0f)] float _stageDuration = 20f;
    [SerializeField, Min(0.01f)] float _updateInterval = 5f;
    [SerializeField, Min(0f)] float _returnToInitialDuration = 10f;

    [Header("Random Ranges")]
    [SerializeField] RandomRange[] _randomRanges =
    {
        new RandomRange(0.01f, 0.33f),
        new RandomRange(0.34f, 0.66f),
        new RandomRange(0.67f, 1f)
    };

    [Header("Runtime")]
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _logValueChanges = true;
    [SerializeField] bool _logMissingProperties = true;

    Coroutine _animationRoutine;
    bool _missingPropertyWarningIssued;
    float _lastPlayStartedAt;
    float _lastCompletedDuration;
    bool _hasPlayStart;
    float _lastParticleIntensity;
    float _lastParticleFrequency;
    string _lastStageOrderCsv = string.Empty;

    public event Action<StarryNightRhoneVfxAutoAnimator> AnimationCompleted;
    public event Action<StageEvent> StageStarted;
    public event Action<StageEvent> StageValueApplied;
    public event Action<StageEvent> StageCompleted;
    public event StageChoicePromptHandler StageChoicePromptRequested;

    public delegate IEnumerator StageChoicePromptHandler(StageEvent stageEvent);

    public static string[] DrivenPropertyNames => (string[])DrivenPropertyNamesBacking.Clone();

    public bool playOnStart
    {
        get => _playOnStart;
        set => _playOnStart = value;
    }

    public string lastStageOrderCsv => _lastStageOrderCsv;

    public bool isPlaying => _animationRoutine != null;
    public float currentOrLastPlayDuration
    {
        get
        {
            if (isPlaying && _hasPlayStart)
            {
                return Mathf.Max(0f, Time.time - _lastPlayStartedAt);
            }

            return _lastCompletedDuration;
        }
    }

    public static AnimationSettings CreateDefaultSettings()
    {
        return new AnimationSettings
        {
            vfxEntityName = DefaultVfxEntityName,
            initialParticleIntensity = 0.01f,
            initialParticleFrequency = 0f,
            initialStateDuration = 10f,
            stageDuration = 20f,
            updateInterval = 5f,
            returnToInitialDuration = 10f,
            randomRanges = new[]
            {
                new RandomRange(0.01f, 0.33f),
                new RandomRange(0.34f, 0.66f),
                new RandomRange(0.67f, 1f)
            }
        };
    }

    public static int GetUpdateCount(float stageDuration, float updateInterval)
    {
        if (stageDuration <= 0f || updateInterval <= 0f)
        {
            return 0;
        }

        return Mathf.FloorToInt(stageDuration / updateInterval);
    }

    public static float CalculateTotalDuration(AnimationSettings settings)
    {
        var randomStageCount = settings.randomRanges == null ? 0 : settings.randomRanges.Length;
        return Mathf.Max(0f, settings.initialStateDuration) +
               Mathf.Max(0f, settings.stageDuration) * randomStageCount +
               Mathf.Max(0f, settings.returnToInitialDuration);
    }

    public static string FormatParticleControlLogMessage(
        string vfxEntityName,
        float particleIntensity,
        float particleFrequency)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "StarryNightRhoneVfxAutoAnimator [{0}] set particle intensity={1:0.###}, particle frequency={2:0.###}",
            vfxEntityName,
            particleIntensity,
            particleFrequency);
    }

    void Reset()
    {
        _visualEffect = GetComponent<VisualEffect>();
        _vfxController = GetComponent<MonaLisaVfxController>();
    }

    void Start()
    {
        if (_playOnStart)
        {
            Play();
        }
    }

    void OnDisable()
    {
        StopAnimationRoutine();
    }

    void OnValidate()
    {
        _initialParticleIntensity = Mathf.Clamp01(_initialParticleIntensity);
        _initialParticleFrequency = Mathf.Clamp01(_initialParticleFrequency);
        _initialStateDuration = Mathf.Max(0f, _initialStateDuration);
        _stageDuration = Mathf.Max(0f, _stageDuration);
        _updateInterval = Mathf.Max(0.01f, _updateInterval);
        _returnToInitialDuration = Mathf.Max(0f, _returnToInitialDuration);

        if (_randomRanges == null || _randomRanges.Length == 0)
        {
            _randomRanges = CreateDefaultSettings().randomRanges;
        }

        for (var i = 0; i < _randomRanges.Length; i++)
        {
            _randomRanges[i].Normalize();
        }
    }

    [ContextMenu("Play VFX Animation")]
    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopAnimationRoutine();
        _lastPlayStartedAt = Time.time;
        _lastCompletedDuration = 0f;
        _hasPlayStart = true;
        _animationRoutine = StartCoroutine(PlayAnimationRoutine());
    }

    [ContextMenu("Stop And Reset To Initial State")]
    public void StopAndResetToInitialState()
    {
        StopAnimationRoutine();
        ApplyInitialState();
    }

    public void ApplyInitialState()
    {
        SetParticleControls(_initialParticleIntensity, _initialParticleFrequency);
    }

    IEnumerator PlayAnimationRoutine()
    {
        EnsureTargetComponents();
        if (_visualEffect == null && _vfxController == null)
        {
            Debug.LogWarning(
                "StarryNightRhoneVfxAutoAnimator could not find a VisualEffect or MonaLisaVfxController. " +
                "Assign one in the inspector or set the VFX Entity Name to the target GameObject.",
                this);
            yield break;
        }

        // 1. 初始状态：先设置低粒子效果，并保持指定秒数。
        ApplyInitialState();
        yield return WaitForDuration(_initialStateDuration);

        // 2-4. 三个增强阶段：每次播放时打乱预设阶段顺序，每个阶段仍使用自己的随机范围。
        var stageOrder = CreateShuffledStageOrder(_randomRanges.Length);
        _lastStageOrderCsv = FormatStageOrder(stageOrder);
        for (var i = 0; i < stageOrder.Length; i++)
        {
            var presetIndex = stageOrder[i];
            var stageEvent = default(StageEvent);
            yield return PlayRandomStage(i, presetIndex, _randomRanges[presetIndex], stage => stageEvent = stage);
            yield return RequestStageChoicePrompt(stageEvent);
        }

        // 5. 回到初始状态：恢复低强度和 0 频率，并在动画结束后保持这个状态。
        ApplyInitialState();
        yield return WaitForDuration(_returnToInitialDuration);
        ApplyInitialState();
        _lastCompletedDuration = _hasPlayStart
            ? Mathf.Max(0f, Time.time - _lastPlayStartedAt)
            : CalculateTotalDuration(CreateAnimationSettingsSnapshot());
        _animationRoutine = null;
        AnimationCompleted?.Invoke(this);
    }

    IEnumerator PlayRandomStage(int stageIndex, int stagePresetIndex, RandomRange randomRange, Action<StageEvent> completed)
    {
        randomRange.Normalize();
        var stageCount = _randomRanges != null ? _randomRanges.Length : 0;
        var updateCount = GetUpdateCount(_stageDuration, _updateInterval);
        var stageStartedRealtime = Time.realtimeSinceStartup;
        StageStarted?.Invoke(CreateStageEvent(
            stageIndex,
            stagePresetIndex,
            stageCount,
            -1,
            updateCount,
            stageStartedRealtime,
            randomRange));

        for (var i = 0; i < updateCount; i++)
        {
            var randomIntensity = randomRange.RandomValue();
            var randomFrequency = randomRange.RandomValue();

            // intensity 和 frequency 分别独立生成随机数；particle drag 不在这里设置。
            SetParticleControls(randomIntensity, randomFrequency);
            StageValueApplied?.Invoke(CreateStageEvent(
                stageIndex,
                stagePresetIndex,
                stageCount,
                i,
                updateCount,
                stageStartedRealtime,
                randomRange));
            yield return WaitForDuration(_updateInterval);
        }

        var remainder = _stageDuration - updateCount * _updateInterval;
        yield return WaitForDuration(remainder);

        var stageEvent = CreateStageEvent(
            stageIndex,
            stagePresetIndex,
            stageCount,
            -1,
            updateCount,
            stageStartedRealtime,
            randomRange);
        StageCompleted?.Invoke(stageEvent);
        completed?.Invoke(stageEvent);
    }

    static int[] CreateShuffledStageOrder(int stageCount)
    {
        var order = new int[Mathf.Max(0, stageCount)];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        for (var i = order.Length - 1; i > 0; i--)
        {
            var swapIndex = UnityEngine.Random.Range(0, i + 1);
            var swap = order[i];
            order[i] = order[swapIndex];
            order[swapIndex] = swap;
        }

        if (IsIdentityOrder(order) && order.Length > 1)
        {
            var swap = order[0];
            order[0] = order[1];
            order[1] = swap;
        }

        return order;
    }

    static bool IsIdentityOrder(int[] order)
    {
        if (order == null)
        {
            return true;
        }

        for (var i = 0; i < order.Length; i++)
        {
            if (order[i] != i)
            {
                return false;
            }
        }

        return true;
    }

    static string FormatStageOrder(int[] order)
    {
        if (order == null || order.Length == 0)
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < order.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder.Append(order[i] + 1);
        }

        return builder.ToString();
    }

    IEnumerator RequestStageChoicePrompt(StageEvent stageEvent)
    {
        var handler = StageChoicePromptRequested;
        if (handler == null)
        {
            yield break;
        }

        foreach (StageChoicePromptHandler promptHandler in handler.GetInvocationList())
        {
            yield return promptHandler(stageEvent);
        }
    }

    IEnumerator WaitForDuration(float seconds)
    {
        if (seconds <= 0f)
        {
            yield break;
        }

        yield return new WaitForSeconds(seconds);
    }

    void SetParticleControls(float particleIntensity, float particleFrequency)
    {
        EnsureTargetComponents();

        var clampedParticleIntensity = Mathf.Clamp01(particleIntensity);
        var clampedParticleFrequency = Mathf.Clamp01(particleFrequency);
        _lastParticleIntensity = clampedParticleIntensity;
        _lastParticleFrequency = clampedParticleFrequency;

        if (_vfxController != null)
        {
            _vfxController.SetAnimatedParticleControls(clampedParticleIntensity, clampedParticleFrequency);
            LogParticleControlChange(clampedParticleIntensity, clampedParticleFrequency);
            return;
        }

        if (_visualEffect == null)
        {
            return;
        }

        TrySetFloat(_particleIntensityProperty, clampedParticleIntensity);
        TrySetFloat(_particleFrequencyProperty, clampedParticleFrequency);
        LogParticleControlChange(clampedParticleIntensity, clampedParticleFrequency);
    }

    StageEvent CreateStageEvent(
        int stageIndex,
        int stagePresetIndex,
        int stageCount,
        int stageValueIndex,
        int stageValueCount,
        float stageStartedRealtime,
        RandomRange randomRange)
    {
        return new StageEvent(
            this,
            Mathf.Max(0, stageIndex),
            Mathf.Max(0, stagePresetIndex),
            Mathf.Max(0, stageCount),
            stageValueIndex,
            Mathf.Max(0, stageValueCount),
            randomRange.minimum,
            randomRange.maximum,
            _lastParticleIntensity,
            _lastParticleFrequency,
            _stageDuration,
            _updateInterval,
            stageStartedRealtime);
    }

    void LogParticleControlChange(float particleIntensity, float particleFrequency)
    {
        // 每次自动脚本设置 intensity / frequency 时打印当前值；particle drag 不在这里修改。
        if (_logValueChanges)
        {
            Debug.Log(
                FormatParticleControlLogMessage(
                    _vfxEntityName,
                    particleIntensity,
                    particleFrequency),
                this);
        }
    }

    void TrySetFloat(string propertyName, float value)
    {
        if (string.IsNullOrEmpty(propertyName) || !_visualEffect.HasFloat(propertyName))
        {
            WarnAboutMissingProperty(propertyName);
            return;
        }

        _visualEffect.SetFloat(propertyName, value);
    }

    void WarnAboutMissingProperty(string propertyName)
    {
        if (!_logMissingProperties || _missingPropertyWarningIssued)
        {
            return;
        }

        _missingPropertyWarningIssued = true;
        var missingName = string.IsNullOrEmpty(propertyName) ? "(empty property name)" : propertyName;
        Debug.LogWarning(
            "StarryNightRhoneVfxAutoAnimator could not find exposed VFX float property: " +
            missingName +
            ". Replace the property name with the exact Visual Effect Graph exposed property reference name.",
            this);
    }

    void EnsureTargetComponents()
    {
        if (_visualEffect == null)
        {
            _visualEffect = GetComponent<VisualEffect>();
        }

        if (_vfxController == null)
        {
            _vfxController = GetComponent<MonaLisaVfxController>();
        }

        if (_vfxController == null && _visualEffect != null)
        {
            _vfxController = _visualEffect.GetComponent<MonaLisaVfxController>();
        }

        if (_visualEffect == null && _vfxController != null)
        {
            _visualEffect = _vfxController.GetComponent<VisualEffect>();
        }

        if ((_visualEffect != null && _vfxController != null) || string.IsNullOrEmpty(_vfxEntityName))
        {
            return;
        }

        var vfxObject = GameObject.Find(_vfxEntityName);
        if (vfxObject == null)
        {
            return;
        }

        if (_visualEffect == null)
        {
            _visualEffect = vfxObject.GetComponent<VisualEffect>();
        }

        if (_vfxController == null)
        {
            _vfxController = vfxObject.GetComponent<MonaLisaVfxController>();
        }
    }

    void StopAnimationRoutine()
    {
        if (_animationRoutine == null)
        {
            return;
        }

        StopCoroutine(_animationRoutine);
        _animationRoutine = null;
    }

    AnimationSettings CreateAnimationSettingsSnapshot()
    {
        return new AnimationSettings
        {
            vfxEntityName = _vfxEntityName,
            initialParticleIntensity = _initialParticleIntensity,
            initialParticleFrequency = _initialParticleFrequency,
            initialStateDuration = _initialStateDuration,
            stageDuration = _stageDuration,
            updateInterval = _updateInterval,
            returnToInitialDuration = _returnToInitialDuration,
            randomRanges = _randomRanges
        };
    }

    [Serializable]
    public struct RandomRange
    {
        [Range(0f, 1f)] public float minimum;
        [Range(0f, 1f)] public float maximum;

        public RandomRange(float minimum, float maximum)
        {
            this.minimum = minimum;
            this.maximum = maximum;
        }

        public float RandomValue()
        {
            return UnityEngine.Random.Range(minimum, maximum);
        }

        public void Normalize()
        {
            minimum = Mathf.Clamp01(minimum);
            maximum = Mathf.Clamp01(maximum);

            if (minimum <= maximum)
            {
                return;
            }

            var previousMinimum = minimum;
            minimum = maximum;
            maximum = previousMinimum;
        }
    }

    [Serializable]
    public struct AnimationSettings
    {
        public string vfxEntityName;
        public float initialParticleIntensity;
        public float initialParticleFrequency;
        public float initialStateDuration;
        public float stageDuration;
        public float updateInterval;
        public float returnToInitialDuration;
        public RandomRange[] randomRanges;
    }

    public sealed class StageEvent
    {
        public readonly StarryNightRhoneVfxAutoAnimator animator;
        public readonly int stageIndex;
        public readonly int stagePresetIndex;
        public readonly int stageCount;
        public readonly int stageValueIndex;
        public readonly int stageValueCount;
        public readonly float rangeMinimum;
        public readonly float rangeMaximum;
        public readonly float particleIntensity;
        public readonly float particleFrequency;
        public readonly float stageDuration;
        public readonly float updateInterval;
        public readonly float stageStartedRealtime;
        public readonly float realtimeSinceStartup;
        public readonly int frame;

        public StageEvent(
            StarryNightRhoneVfxAutoAnimator animator,
            int stageIndex,
            int stagePresetIndex,
            int stageCount,
            int stageValueIndex,
            int stageValueCount,
            float rangeMinimum,
            float rangeMaximum,
            float particleIntensity,
            float particleFrequency,
            float stageDuration,
            float updateInterval,
            float stageStartedRealtime)
        {
            this.animator = animator;
            this.stageIndex = stageIndex;
            this.stagePresetIndex = stagePresetIndex;
            this.stageCount = stageCount;
            this.stageValueIndex = stageValueIndex;
            this.stageValueCount = stageValueCount;
            this.rangeMinimum = rangeMinimum;
            this.rangeMaximum = rangeMaximum;
            this.particleIntensity = particleIntensity;
            this.particleFrequency = particleFrequency;
            this.stageDuration = stageDuration;
            this.updateInterval = updateInterval;
            this.stageStartedRealtime = stageStartedRealtime;
            realtimeSinceStartup = Time.realtimeSinceStartup;
            frame = Time.frameCount;
        }
    }
}
