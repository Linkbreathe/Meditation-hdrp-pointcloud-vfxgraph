using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/VFX Auto Animator Group Controller")]
public sealed class VfxAutoAnimatorGroupController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] bool _includeInactiveChildren = true;
    [SerializeField] bool _autoRefreshTargets = true;
    [SerializeField] bool _applyOnEnable = true;
    [SerializeField] bool _restartRunningAnimators;
    [SerializeField] bool _restoreChildSettingsOnDisable = true;
    [SerializeField] StarryNightRhoneVfxAutoAnimator[] _childAnimators = Array.Empty<StarryNightRhoneVfxAutoAnimator>();

    [Header("Initial State")]
    [SerializeField, Range(0f, 1f)] float _initialParticleIntensity = 0.01f;
    [SerializeField, Range(0f, 1f)] float _initialParticleFrequency;

    [Header("Timing")]
    [SerializeField, Min(0f)] float _initialStateDuration = 10f;
    [SerializeField, Min(0f)] float _stageDuration = 20f;
    [SerializeField, Min(0.01f)] float _updateInterval = 5f;
    [SerializeField, Min(0f)] float _returnToInitialDuration = 10f;

    [Header("Random Ranges")]
    [SerializeField] StarryNightRhoneVfxAutoAnimator.RandomRange[] _randomRanges =
    {
        new StarryNightRhoneVfxAutoAnimator.RandomRange(0.01f, 0.33f),
        new StarryNightRhoneVfxAutoAnimator.RandomRange(0.34f, 0.66f),
        new StarryNightRhoneVfxAutoAnimator.RandomRange(0.67f, 1f)
    };

    [Header("Runtime")]
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _logValueChanges = true;
    [SerializeField] bool _logMissingProperties = true;

    ChildSettingsSnapshot[] _originalSettings = Array.Empty<ChildSettingsSnapshot>();
    bool _hasCapturedOriginalSettings;

    public int childAnimatorCount
    {
        get
        {
            EnsureChildAnimators();
            return _childAnimators.Length;
        }
    }

    public StarryNightRhoneVfxAutoAnimator.SharedSettings sharedSettings
    {
        get => CreateSharedSettingsSnapshot();
        set => ApplySharedSettings(value);
    }

    void Reset()
    {
        ApplySharedSettings(StarryNightRhoneVfxAutoAnimator.CreateDefaultSharedSettings());
        RefreshChildAnimators();
    }

    void Awake()
    {
        if (_autoRefreshTargets)
        {
            RefreshChildAnimators();
        }
    }

    void OnEnable()
    {
        _hasCapturedOriginalSettings = false;

        if (_applyOnEnable && Application.isPlaying)
        {
            ApplyToChildAnimators();
        }
    }

    void Start()
    {
        if (_applyOnEnable && Application.isPlaying)
        {
            ApplyToChildAnimators();
        }
    }

    void OnDisable()
    {
        if (_restoreChildSettingsOnDisable)
        {
            RestoreCapturedChildSettings();
        }
    }

    void OnValidate()
    {
        NormalizeSerializedValues();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            ApplyToChildAnimators();
        }
    }

    [ContextMenu("Refresh Child Animators")]
    public void RefreshChildAnimators()
    {
        _childAnimators = GetComponentsInChildren<StarryNightRhoneVfxAutoAnimator>(_includeInactiveChildren);
    }

    [ContextMenu("Apply To Child Animators")]
    public void ApplyToChildAnimators()
    {
        EnsureChildAnimators();
        CaptureOriginalSettingsIfNeeded();

        var settings = CreateSharedSettingsSnapshot();
        for (var i = 0; i < _childAnimators.Length; i++)
        {
            var animator = _childAnimators[i];
            if (animator == null)
            {
                continue;
            }

            animator.ApplySharedSettings(settings, _restartRunningAnimators);
            MarkAnimatorDirty(animator);
        }
    }

    [ContextMenu("Restore Captured Child Settings")]
    public void RestoreCapturedChildSettings()
    {
        if (!_hasCapturedOriginalSettings)
        {
            return;
        }

        for (var i = 0; i < _originalSettings.Length; i++)
        {
            var snapshot = _originalSettings[i];
            if (snapshot.animator == null)
            {
                continue;
            }

            snapshot.animator.ApplySharedSettings(snapshot.settings, _restartRunningAnimators);
            MarkAnimatorDirty(snapshot.animator);
        }

        _hasCapturedOriginalSettings = false;
        _originalSettings = Array.Empty<ChildSettingsSnapshot>();
    }

    [ContextMenu("Copy Settings From First Child Animator")]
    public void CopySettingsFromFirstChildAnimator()
    {
        EnsureChildAnimators();
        for (var i = 0; i < _childAnimators.Length; i++)
        {
            var animator = _childAnimators[i];
            if (animator == null)
            {
                continue;
            }

            ApplySharedSettings(animator.CreateSharedSettingsSnapshot());
            return;
        }
    }

    [ContextMenu("Play Child Animators")]
    public void PlayChildAnimators()
    {
        ApplyToChildAnimators();

        for (var i = 0; i < _childAnimators.Length; i++)
        {
            var animator = _childAnimators[i];
            if (animator != null)
            {
                animator.Play();
            }
        }
    }

    [ContextMenu("Stop And Reset Child Animators")]
    public void StopAndResetChildAnimators()
    {
        ApplyToChildAnimators();

        for (var i = 0; i < _childAnimators.Length; i++)
        {
            var animator = _childAnimators[i];
            if (animator != null)
            {
                animator.StopAndResetToInitialState();
            }
        }
    }

    StarryNightRhoneVfxAutoAnimator.SharedSettings CreateSharedSettingsSnapshot()
    {
        return new StarryNightRhoneVfxAutoAnimator.SharedSettings
        {
            initialParticleIntensity = _initialParticleIntensity,
            initialParticleFrequency = _initialParticleFrequency,
            initialStateDuration = _initialStateDuration,
            stageDuration = _stageDuration,
            updateInterval = _updateInterval,
            returnToInitialDuration = _returnToInitialDuration,
            randomRanges = CloneRandomRanges(_randomRanges),
            playOnStart = _playOnStart,
            logValueChanges = _logValueChanges,
            logMissingProperties = _logMissingProperties
        };
    }

    void ApplySharedSettings(StarryNightRhoneVfxAutoAnimator.SharedSettings settings)
    {
        _initialParticleIntensity = settings.initialParticleIntensity;
        _initialParticleFrequency = settings.initialParticleFrequency;
        _initialStateDuration = settings.initialStateDuration;
        _stageDuration = settings.stageDuration;
        _updateInterval = settings.updateInterval;
        _returnToInitialDuration = settings.returnToInitialDuration;
        _randomRanges = CloneRandomRanges(settings.randomRanges);
        _playOnStart = settings.playOnStart;
        _logValueChanges = settings.logValueChanges;
        _logMissingProperties = settings.logMissingProperties;

        NormalizeSerializedValues();
    }

    void EnsureChildAnimators()
    {
        if (_autoRefreshTargets || _childAnimators == null || _childAnimators.Length == 0)
        {
            RefreshChildAnimators();
        }
    }

    void CaptureOriginalSettingsIfNeeded()
    {
        if (_hasCapturedOriginalSettings)
        {
            return;
        }

        _originalSettings = new ChildSettingsSnapshot[_childAnimators.Length];
        for (var i = 0; i < _childAnimators.Length; i++)
        {
            var animator = _childAnimators[i];
            _originalSettings[i] = new ChildSettingsSnapshot(
                animator,
                animator != null
                    ? animator.CreateSharedSettingsSnapshot()
                    : default);
        }

        _hasCapturedOriginalSettings = true;
    }

    void NormalizeSerializedValues()
    {
        _initialParticleIntensity = Mathf.Clamp01(_initialParticleIntensity);
        _initialParticleFrequency = Mathf.Clamp01(_initialParticleFrequency);
        _initialStateDuration = Mathf.Max(0f, _initialStateDuration);
        _stageDuration = Mathf.Max(0f, _stageDuration);
        _updateInterval = Mathf.Max(0.01f, _updateInterval);
        _returnToInitialDuration = Mathf.Max(0f, _returnToInitialDuration);

        if (_randomRanges == null || _randomRanges.Length == 0)
        {
            _randomRanges = StarryNightRhoneVfxAutoAnimator.CreateDefaultSharedSettings().randomRanges;
        }

        for (var i = 0; i < _randomRanges.Length; i++)
        {
            _randomRanges[i].Normalize();
        }
    }

    static StarryNightRhoneVfxAutoAnimator.RandomRange[] CloneRandomRanges(
        StarryNightRhoneVfxAutoAnimator.RandomRange[] randomRanges)
    {
        if (randomRanges == null || randomRanges.Length == 0)
        {
            return Array.Empty<StarryNightRhoneVfxAutoAnimator.RandomRange>();
        }

        var clone = new StarryNightRhoneVfxAutoAnimator.RandomRange[randomRanges.Length];
        Array.Copy(randomRanges, clone, randomRanges.Length);
        return clone;
    }

    static void MarkAnimatorDirty(StarryNightRhoneVfxAutoAnimator animator)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || animator == null)
        {
            return;
        }

        EditorUtility.SetDirty(animator);
        if (animator.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        }
#endif
    }

    readonly struct ChildSettingsSnapshot
    {
        public readonly StarryNightRhoneVfxAutoAnimator animator;
        public readonly StarryNightRhoneVfxAutoAnimator.SharedSettings settings;

        public ChildSettingsSnapshot(
            StarryNightRhoneVfxAutoAnimator animator,
            StarryNightRhoneVfxAutoAnimator.SharedSettings settings)
        {
            this.animator = animator;
            this.settings = settings;
        }
    }
}
