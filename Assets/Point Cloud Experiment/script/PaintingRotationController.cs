using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.XR;

[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Painting Rotation Controller")]
public sealed class PaintingRotationController : MonoBehaviour
{
    public const float DefaultFadeOutDuration = 12f;
    public const float DefaultFadeInDuration = 12f;
    public const float DefaultDissolveHoldDuration = 8f;
    public const float DefaultFeedbackDelay = 0.3f;
    public const float DefaultFeedbackTimeoutRatio = 0.85f;
    public const float DefaultChoicePromptReminderDelaySeconds = 45f;
    public const float DefaultChoicePromptTimeoutSeconds = DefaultChoicePromptReminderDelaySeconds;
    public const float DefaultChoicePromptReminderAudioIntervalSeconds = 3f;
    public const float DefaultEyeRestDuration = 30f;

    const float DefaultAutoEyeRestOverlayDistanceMeters = 0.75f;
    const float AutoEyeRestOverlayFovPadding = 1.6f;
    const string DefaultChoicePromptReminderClipAssetPath = "Assets/Audio/notification.mp3";

    [Header("Paintings")]
    [SerializeField] Transform _paintingsRoot;
    [SerializeField] bool _preferActiveChildOnStart = true;
    [SerializeField, Min(0)] int _startIndex;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] float _fadeOutDuration = DefaultFadeOutDuration;
    [SerializeField, Min(0f)] float _dissolveHoldDuration = DefaultDissolveHoldDuration;
    [SerializeField, Min(0.01f)] float _fadeInDuration = DefaultFadeInDuration;

    [Header("Calmness Feedback")]
    [SerializeField] bool _collectCalmnessFeedback;
    [SerializeField] CalmnessFeedbackCollector _feedbackCollector;
    [SerializeField, Min(0f)] float _feedbackDelay = DefaultFeedbackDelay;
    [SerializeField, Range(0.05f, 1f)] float _feedbackTimeoutRatio = DefaultFeedbackTimeoutRatio;

    [Header("Meditation Choice Stage Gate")]
    [SerializeField] bool _promptMeditationChoiceAfterStages = true;
    [SerializeField] MeditationChoiceEyeGazeFeedback _meditationChoiceFeedback;
    [SerializeField] MeditationExperimentCsvLogger _experimentCsvLogger;
    [SerializeField] float _choiceNormalColorIntensity = 0f;
    [SerializeField] float _choicePromptColorIntensity = 3.5f;
    [SerializeField, Min(0.1f)] float _choicePromptBreathInSeconds = 1.1f;
    [SerializeField, Min(0.1f)] float _choicePromptBreathOutSeconds = 1.55f;
    [SerializeField, Range(0f, 1f)] float _choicePromptBreathMinimumAmount;
    [SerializeField, FormerlySerializedAs("_choicePromptTimeoutSeconds"), Min(0f)] float _choicePromptReminderDelaySeconds = DefaultChoicePromptReminderDelaySeconds;
    [SerializeField] Volume _choicePromptReminderSkyVolume;
    [SerializeField] Color _choicePromptReminderSkyMiddleColor = new Color(49f / 255f, 143f / 255f, 166f / 255f, 1f);
    [SerializeField, Min(0.01f)] float _choicePromptReminderSkyTransitionSeconds = 1.5f;
    [SerializeField] AnimationCurve _choicePromptReminderSkyTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] AudioClip _choicePromptReminderClip;
    [SerializeField] AudioSource _choicePromptReminderAudioSource;
    [SerializeField, Min(0.1f)] float _choicePromptReminderAudioIntervalSeconds = DefaultChoicePromptReminderAudioIntervalSeconds;
    [SerializeField, Range(0f, 1f)] float _choicePromptReminderAudioVolume = 1f;
    [SerializeField] bool _requireEyeTrackingReadyBeforeStart = true;
    [SerializeField, Min(0f)] float _eyeTrackingStartGateTimeoutSeconds = 8f;
    [SerializeField, Min(0.05f)] float _eyeTrackingStartGatePollSeconds = 0.25f;

    [Header("Eye Rest Gate")]
    [SerializeField] bool _enableEyeRestGate = true;
    [SerializeField, Min(1)] int _paintingsBetweenEyeRests = 2;
    [SerializeField, Min(0f)] float _eyeRestDuration = DefaultEyeRestDuration;
    [SerializeField] KeyCode _eyeRestContinueKey = KeyCode.B;
    [SerializeField] Color _eyeRestOverlayColor = Color.black;
    [SerializeField, Range(0f, 1f)] float _eyeRestOverlayAlpha = 1f;
    [SerializeField] CanvasGroup _eyeRestOverlay;
    [SerializeField] bool _autoCreateEyeRestOverlay = true;
    [SerializeField, Min(0.1f)] float _autoEyeRestOverlayDistanceMeters = DefaultAutoEyeRestOverlayDistanceMeters;
    [SerializeField] bool _allowRightControllerBButtonForEyeRest = true;
    [SerializeField] bool _logEyeRestGate = true;

    [Header("Runtime")]
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _waitForRightControllerAButtonBeforeStart;
    [SerializeField] bool _disableChildPlayOnStartWhileWaiting = true;
    [SerializeField] bool _allowKeyboardStartInEditor = true;
    [SerializeField] bool _logStartButtonInput = true;
    [SerializeField] bool _logTransitions = true;
    [SerializeField, Min(0.05f)] float _progressLogInterval = 1f;

    static readonly List<InputDevice> InputDevicesBuffer = new List<InputDevice>();
    static PaintingRotationController _activeExperimentController;

    readonly List<PaintingEntry> _paintings = new List<PaintingEntry>();
    Coroutine _rotationRoutine;
    Coroutine _startWhenEyeTrackingReadyRoutine;
    StarryNightRhoneVfxAutoAnimator _subscribedAnimator;
    bool _currentAnimationCompleted;
    bool _hasReceivedStartInput;
    bool _hasStartedExperimentSession;
    bool _choiceCompleted;
    bool _wasUnityXrRightControllerAButtonPressed;
    bool _wasOvrRightControllerAButtonPressed;
    bool _wasUnityXrRightControllerBButtonPressed;
    bool _wasOvrRightControllerBButtonPressed;
    bool _createdRuntimeEyeRestOverlay;
    bool _missingEyeRestOverlayWarningIssued;
    int _currentIndex = -1;
    int _paintingRunIndex;
    float _choicePromptStartedAt;
    MeditationChoiceEyeGazeFeedback.SelectionResult _lastChoiceResult;
    MeditationChoiceEyeGazeFeedback _subscribedChoiceFeedback;
    Coroutine _choicePromptReminderRoutine;
    Coroutine _choicePromptSkyTransitionRoutine;
    AudioSource _runtimeChoicePromptReminderAudioSource;
    GradientSky _choicePromptGradientSky;
    Color _choicePromptOriginalSkyMiddleColor;
    bool _choicePromptOriginalSkyMiddleOverrideState;
    bool _choicePromptSkyOriginalCaptured;
    bool _choicePromptReminderActive;
    bool _missingChoicePromptReminderSkyWarningIssued;
    bool _missingChoicePromptReminderClipWarningIssued;
    Canvas _runtimeEyeRestCanvas;
    Image _runtimeEyeRestOverlayImage;

    public int paintingCount => _paintings.Count;
    public int currentIndex => _currentIndex;
    public bool isWaitingForStartInput => _waitForRightControllerAButtonBeforeStart && !_hasReceivedStartInput;
    public bool isRotating => _rotationRoutine != null;

    public static int GetNextIndex(int currentIndex, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        return (currentIndex + 1) % count;
    }

    public static float SmoothStep01(float progress)
    {
        var t = Mathf.Clamp01(progress);
        return t * t * (3f - 2f * t);
    }

    public static bool IsPressedThisFrame(bool isPressed, bool wasPressed)
    {
        return isPressed && !wasPressed;
    }

    public static bool ShouldStartEyeRestGate(
        bool enabled,
        int paintingsBetweenEyeRests,
        int completedPaintingCount)
    {
        if (!enabled || completedPaintingCount <= 0)
        {
            return false;
        }

        var interval = Mathf.Max(1, paintingsBetweenEyeRests);
        return completedPaintingCount % interval == 0;
    }

    public static string FormatTransitionLogMessage(
        string phase,
        string fromName,
        string toName,
        float elapsedSeconds,
        float durationSeconds,
        float progress,
        float spawnRate)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[PaintingRotationController] phase={0} from=\"{1}\" to=\"{2}\" elapsed={3}ms/{4}ms progress={5:0.0}% spawnRate={6:0.###}",
            phase,
            fromName,
            toName,
            SecondsToMilliseconds(elapsedSeconds),
            SecondsToMilliseconds(durationSeconds),
            Mathf.Clamp01(progress) * 100f,
            spawnRate);
    }

    public static string FormatEyeRestLogMessage(
        string phase,
        string fromName,
        string toName,
        int completedPaintingCount,
        float requiredRestSeconds,
        float elapsedSeconds,
        string inputSource)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[PaintingRotationController] eyeRest={0} from=\"{1}\" to=\"{2}\" completedPaintings={3} requiredRest={4}ms elapsed={5}ms input=\"{6}\"",
            phase,
            fromName,
            toName,
            Mathf.Max(0, completedPaintingCount),
            SecondsToMilliseconds(requiredRestSeconds),
            SecondsToMilliseconds(elapsedSeconds),
            inputSource ?? string.Empty);
    }

    public static float CalculateFeedbackTimeout(float fadeOutDuration, float feedbackDelay, float timeoutRatio)
    {
        var fadeDuration = Mathf.Max(0f, fadeOutDuration);
        var delay = Mathf.Max(0f, feedbackDelay);
        var ratio = Mathf.Clamp01(timeoutRatio);
        return Mathf.Max(0f, fadeDuration * ratio - delay);
    }

    public static string CreatePaintingId(string paintingName, int paintingIndex)
    {
        var builder = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(paintingName))
        {
            for (var i = 0; i < paintingName.Length; i++)
            {
                var character = char.ToLowerInvariant(paintingName[i]);
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                }
                else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                {
                    builder.Append('_');
                }
            }
        }

        while (builder.Length > 0 && builder[builder.Length - 1] == '_')
        {
            builder.Length--;
        }

        if (builder.Length == 0)
        {
            builder.AppendFormat(CultureInfo.InvariantCulture, "painting_{0:00}", Mathf.Max(0, paintingIndex + 1));
        }

        return builder.ToString();
    }

    public static VfxControlSnapshot CreateSnapshot(MonaLisaVfxController controller)
    {
        if (controller == null)
        {
            return VfxControlSnapshot.zero;
        }

        return new VfxControlSnapshot(
            controller.spawnRate,
            controller.particleIntensity,
            controller.particleFrequency);
    }

    public static void ApplyOutgoingSpawnFade(
        MonaLisaVfxController outgoing,
        VfxControlSnapshot outgoingStart,
        float progress)
    {
        var t = SmoothStep01(progress);
        ApplySnapshot(
            outgoing,
            new VfxControlSnapshot(
                Mathf.Lerp(outgoingStart.spawnRate, 0f, t),
                outgoingStart.particleIntensity,
                outgoingStart.particleFrequency));
    }

    public static void PrepareIncomingSpawnFade(
        MonaLisaVfxController incoming,
        VfxControlSnapshot incomingTarget)
    {
        ApplySnapshot(
            incoming,
            new VfxControlSnapshot(
                0f,
                incomingTarget.particleIntensity,
                incomingTarget.particleFrequency));
    }

    public static void ApplyIncomingSpawnFade(
        MonaLisaVfxController incoming,
        VfxControlSnapshot incomingTarget,
        float progress)
    {
        var t = SmoothStep01(progress);
        ApplySnapshot(
            incoming,
            new VfxControlSnapshot(
                Mathf.Lerp(0f, incomingTarget.spawnRate, t),
                incomingTarget.particleIntensity,
                incomingTarget.particleFrequency));
    }

    public static void SetPaintingVisibility(GameObject painting, bool isVisible)
    {
        if (painting == null)
        {
            return;
        }

        var renderers = painting.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = isVisible;
        }

        painting.SetActive(isVisible);
    }

    [ContextMenu("Refresh Paintings")]
    public void RefreshPaintings()
    {
        if (_paintingsRoot == null)
        {
            _paintingsRoot = transform;
        }

        _paintings.Clear();

        for (var i = 0; i < _paintingsRoot.childCount; i++)
        {
            var child = _paintingsRoot.GetChild(i);
            var controller = child.GetComponent<MonaLisaVfxController>();
            var animator = child.GetComponent<StarryNightRhoneVfxAutoAnimator>();
            if (controller == null || animator == null)
            {
                continue;
            }

            ConfigureManagedChildAnimator(animator);
            _paintings.Add(new PaintingEntry(
                child.gameObject,
                CreatePaintingId(child.gameObject.name, _paintings.Count),
                controller,
                animator,
                CreateSnapshot(controller)));
        }

        _currentIndex = ResolveInitialIndex();
    }

    [ContextMenu("Play Painting Rotation")]
    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (isWaitingForStartInput)
        {
            PrepareStartGate();
            return;
        }

        StartRotation("AutoStart");
    }

    bool StartRotation(string inputSource)
    {
        if (!TryClaimExperimentControl(inputSource))
        {
            return false;
        }

        if (!TryValidateRequiredEyeTrackingReadyForStart(inputSource, out var eyeTrackingReason))
        {
            Debug.LogWarning(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[PaintingRotationController] Experiment start blocked because eye tracking is not ready. source={0} reason={1}",
                    inputSource,
                    eyeTrackingReason),
                this);
            ReleaseExperimentControl();
            return false;
        }

        StopRotation(false);
        RefreshPaintings();
        if (_paintings.Count == 0)
        {
            Debug.LogWarning(
                "PaintingRotationController could not find child paintings with MonaLisaVfxController and StarryNightRhoneVfxAutoAnimator.",
                this);
            ReleaseExperimentControl();
            return false;
        }

        BeginExperimentSessionIfNeeded(inputSource);
        if (!_hasStartedExperimentSession)
        {
            ReleaseExperimentControl();
            return false;
        }

        ConfigureMeditationChoiceForIdle();
        ResetEyeRestContinuePressedState();
        SetEyeRestOverlayVisible(false);
        _rotationRoutine = StartCoroutine(RunRotationRoutine());
        return true;
    }

    [ContextMenu("Stop Painting Rotation")]
    public void StopRotation()
    {
        StopRotation(true);
    }

    void StopRotation(bool releaseExperimentControl)
    {
        if (releaseExperimentControl)
        {
            StopWaitingForEyeTrackingStartGate();
        }

        if (_rotationRoutine == null)
        {
            UnsubscribeFromMeditationChoiceFeedback();
            StopChoicePromptReminder(true);
            SetEyeRestOverlayVisible(false);
            if (releaseExperimentControl)
            {
                ReleaseExperimentControl();
            }

            return;
        }

        StopCoroutine(_rotationRoutine);
        _rotationRoutine = null;
        UnsubscribeFromCurrentAnimator();
        UnsubscribeFromMeditationChoiceFeedback();
        StopChoicePromptReminder(true);
        ConfigureMeditationChoiceForIdle();
        SetEyeRestOverlayVisible(false);
        if (releaseExperimentControl)
        {
            ReleaseExperimentControl();
        }
    }

    void Reset()
    {
        _paintingsRoot = transform;
    }

    void Awake()
    {
        if (_paintingsRoot == null)
        {
            _paintingsRoot = transform;
        }

        RefreshPaintings();
    }

    void Start()
    {
        if (_playOnStart)
        {
            Play();
            return;
        }

        if (isWaitingForStartInput)
        {
            PrepareStartGate();
        }
    }

    void Update()
    {
        if (!WasStartButtonPressedThisFrame(out var inputSource))
        {
            return;
        }

        if (!isWaitingForStartInput)
        {
            LogStartButtonInput(string.Format(
                CultureInfo.InvariantCulture,
                "Start button observed but ignored source={0} frame={1} waiting={2} receivedStart={3} isRotating={4} pendingEyeTrackingStart={5}",
                inputSource,
                Time.frameCount,
                isWaitingForStartInput,
                _hasReceivedStartInput,
                isRotating,
                _startWhenEyeTrackingReadyRoutine != null));
            return;
        }

        BeginFromStartInput(inputSource);
    }

    void OnDisable()
    {
        StopRotation();
    }

    void BeginFromStartInput(string inputSource)
    {
        if (_hasReceivedStartInput)
        {
            return;
        }

        if (!TryClaimExperimentControl(inputSource))
        {
            return;
        }

        LogStartButtonInput(string.Format(
            CultureInfo.InvariantCulture,
            "Start button pressed source={0} frame={1} currentIndex={2} paintingCount={3}",
            inputSource,
            Time.frameCount,
            _currentIndex,
            _paintings.Count));

        if (RequiresEyeTrackingReadyBeforeStart(out var choiceFeedback) &&
            !choiceFeedback.TryEnsureEyeTrackingReady(out var eyeTrackingReason))
        {
            _hasReceivedStartInput = true;
            if (_eyeTrackingStartGateTimeoutSeconds > 0f)
            {
                LogStartButtonInput(string.Format(
                    CultureInfo.InvariantCulture,
                    "Start button accepted; waiting up to {0:0.##}s for eye tracking before starting. reason={1}",
                    _eyeTrackingStartGateTimeoutSeconds,
                    eyeTrackingReason));
                _startWhenEyeTrackingReadyRoutine = StartCoroutine(WaitForEyeTrackingThenStart(inputSource, choiceFeedback));
                return;
            }

            Debug.LogWarning(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[PaintingRotationController] Start button accepted but experiment was not started because eye tracking is not ready. source={0} reason={1}",
                    inputSource,
                    eyeTrackingReason),
                this);
            _hasReceivedStartInput = false;
            ReleaseExperimentControl();
            return;
        }

        _hasReceivedStartInput = true;
        if (!StartRotation(inputSource))
        {
            _hasReceivedStartInput = false;
        }
    }

    IEnumerator WaitForEyeTrackingThenStart(
        string inputSource,
        MeditationChoiceEyeGazeFeedback choiceFeedback)
    {
        var startedWaitingAt = Time.unscaledTime;
        var timeoutSeconds = Mathf.Max(0f, _eyeTrackingStartGateTimeoutSeconds);
        var pollSeconds = Mathf.Max(0.05f, _eyeTrackingStartGatePollSeconds);
        var lastReason = "eye tracking readiness was not checked";

        while (choiceFeedback != null && Time.unscaledTime - startedWaitingAt < timeoutSeconds)
        {
            if (choiceFeedback.TryEnsureEyeTrackingReady(out lastReason))
            {
                _startWhenEyeTrackingReadyRoutine = null;
                LogStartButtonInput(string.Format(
                    CultureInfo.InvariantCulture,
                    "Eye tracking became ready after {0:0.##}s; starting experiment. source={1}",
                    Time.unscaledTime - startedWaitingAt,
                    inputSource));
                if (!StartRotation(inputSource))
                {
                    _hasReceivedStartInput = false;
                }

                yield break;
            }

            yield return new WaitForSecondsRealtime(pollSeconds);
        }

        if (choiceFeedback != null)
        {
            choiceFeedback.TryEnsureEyeTrackingReady(out lastReason);
        }
        else
        {
            lastReason = "MeditationChoiceEyeGazeFeedback was destroyed or disabled";
        }

        _startWhenEyeTrackingReadyRoutine = null;
        _hasReceivedStartInput = false;
        ReleaseExperimentControl();
        Debug.LogWarning(
            string.Format(
                CultureInfo.InvariantCulture,
                "[PaintingRotationController] Experiment start timed out while waiting for eye tracking. waited={0:0.##}s source={1} reason={2}",
                Time.unscaledTime - startedWaitingAt,
                inputSource,
                lastReason),
            this);
    }

    void PrepareStartGate()
    {
        StopRotation();
        RefreshPaintings();
        if (_paintings.Count == 0)
        {
            return;
        }

        StopChildAnimatorsForStartGate();
        DeactivateAllPaintings();
        ConfigureMeditationChoiceForIdle();
        LogStartButtonInput(string.Format(
            CultureInfo.InvariantCulture,
            "Waiting for Meta Quest Pro right-controller A button. currentIndex={0} paintingCount={1} paintingsHidden=True",
            _currentIndex,
            _paintings.Count));
    }

    IEnumerator RunRotationRoutine()
    {
        ActivateOnly(_currentIndex);

        while (_paintings.Count > 0)
        {
            var current = _paintings[_currentIndex];
            yield return PlayAndWaitForCompletion(current);

            var completedPaintingCount = _paintingRunIndex;
            var nextIndex = GetNextIndex(_currentIndex, _paintings.Count);
            if (nextIndex < 0 || nextIndex == _currentIndex)
            {
                yield break;
            }

            var runEyeRestGate = ShouldStartEyeRestGate(
                _enableEyeRestGate,
                _paintingsBetweenEyeRests,
                completedPaintingCount);
            yield return TransitionToNextPainting(nextIndex, runEyeRestGate, completedPaintingCount);
        }
    }

    IEnumerator PlayAndWaitForCompletion(PaintingEntry painting)
    {
        BeginPaintingRun(painting);
        SetPaintingVisibility(painting.gameObject, true);
        SubscribeToAnimator(painting.animator);
        painting.animator.Play();

        while (!_currentAnimationCompleted && painting.gameObject.activeInHierarchy)
        {
            yield return null;
        }

        UnsubscribeFromCurrentAnimator();
        CompletePaintingRun(painting);
    }

    IEnumerator TransitionToNextPainting(int nextIndex, bool runEyeRestGate, int completedPaintingCount)
    {
        var outgoing = _paintings[_currentIndex];
        var incoming = _paintings[nextIndex];
        var outgoingStart = CreateSnapshot(outgoing.controller);
        var incomingTarget = incoming.visibleControls;
        var outgoingViewDuration = GetPaintingViewDuration(outgoing);
        var elapsed = 0f;
        var fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
        var dissolveHoldDuration = Mathf.Max(0f, _dissolveHoldDuration);
        var fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);
        var feedbackTriggered = false;
        var feedbackTimeout = CalculateFeedbackTimeout(
            fadeOutDuration,
            _feedbackDelay,
            _feedbackTimeoutRatio);

        LogTransition("FadeOutStart", outgoing, incoming, 0f, fadeOutDuration, 0f, outgoingStart.spawnRate);

        var nextProgressLogTime = _progressLogInterval;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            var progress = elapsed / fadeOutDuration;
            ApplyOutgoingSpawnFade(outgoing.controller, outgoingStart, progress);
            if (!feedbackTriggered && elapsed >= _feedbackDelay)
            {
                feedbackTriggered = true;
                TryStartCalmnessFeedback(outgoing, feedbackTimeout, outgoingViewDuration);
            }

            if (elapsed >= nextProgressLogTime && elapsed < fadeOutDuration)
            {
                LogTransition(
                    "FadeOutProgress",
                    outgoing,
                    incoming,
                    elapsed,
                    fadeOutDuration,
                    progress,
                    outgoing.controller.spawnRate);
                nextProgressLogTime += _progressLogInterval;
            }

            yield return null;
        }

        ApplyOutgoingSpawnFade(outgoing.controller, outgoingStart, 1f);
        LogTransition("FadeOutEnd", outgoing, incoming, fadeOutDuration, fadeOutDuration, 1f, outgoing.controller.spawnRate);

        if (dissolveHoldDuration > 0f)
        {
            LogTransition("DissolveHoldStart", outgoing, incoming, 0f, dissolveHoldDuration, 0f, outgoing.controller.spawnRate);

            elapsed = 0f;
            nextProgressLogTime = _progressLogInterval;
            while (elapsed < dissolveHoldDuration)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / dissolveHoldDuration;
                if (elapsed >= nextProgressLogTime && elapsed < dissolveHoldDuration)
                {
                    LogTransition(
                        "DissolveHoldProgress",
                        outgoing,
                        incoming,
                        elapsed,
                        dissolveHoldDuration,
                        progress,
                        outgoing.controller.spawnRate);
                    nextProgressLogTime += _progressLogInterval;
                }

                yield return null;
            }

            LogTransition(
                "DissolveHoldEnd",
                outgoing,
                incoming,
                dissolveHoldDuration,
                dissolveHoldDuration,
                1f,
                outgoing.controller.spawnRate);
        }

        SetPaintingVisibility(outgoing.gameObject, false);

        if (runEyeRestGate)
        {
            yield return RunEyeRestGate(outgoing, incoming, completedPaintingCount);
        }

        SetPaintingVisibility(incoming.gameObject, true);
        incoming.activatedAtSeconds = Time.time;
        PrepareIncomingSpawnFade(incoming.controller, incomingTarget);
        LogTransition("FadeInStart", outgoing, incoming, 0f, fadeInDuration, 0f, incoming.controller.spawnRate);

        elapsed = 0f;
        nextProgressLogTime = _progressLogInterval;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            var progress = elapsed / fadeInDuration;
            ApplyIncomingSpawnFade(incoming.controller, incomingTarget, progress);
            if (elapsed >= nextProgressLogTime && elapsed < fadeInDuration)
            {
                LogTransition(
                    "FadeInProgress",
                    outgoing,
                    incoming,
                    elapsed,
                    fadeInDuration,
                    progress,
                    incoming.controller.spawnRate);
                nextProgressLogTime += _progressLogInterval;
            }

            yield return null;
        }

        ApplyIncomingSpawnFade(incoming.controller, incomingTarget, 1f);
        LogTransition("FadeInEnd", outgoing, incoming, fadeInDuration, fadeInDuration, 1f, incoming.controller.spawnRate);
        _currentIndex = nextIndex;
    }

    void ActivateOnly(int activeIndex)
    {
        for (var i = 0; i < _paintings.Count; i++)
        {
            var isActive = i == activeIndex;
            SetPaintingVisibility(_paintings[i].gameObject, isActive);
            if (isActive)
            {
                _paintings[i].activatedAtSeconds = Time.time;
            }
        }
    }

    void DeactivateAllPaintings()
    {
        for (var i = 0; i < _paintings.Count; i++)
        {
            SetPaintingVisibility(_paintings[i].gameObject, false);
        }
    }

    int ResolveInitialIndex()
    {
        if (_paintings.Count == 0)
        {
            return -1;
        }

        if (_preferActiveChildOnStart)
        {
            for (var i = 0; i < _paintings.Count; i++)
            {
                if (_paintings[i].gameObject.activeSelf)
                {
                    return i;
                }
            }
        }

        return Mathf.Clamp(_startIndex, 0, _paintings.Count - 1);
    }

    void ConfigureManagedChildAnimator(StarryNightRhoneVfxAutoAnimator animator)
    {
        if (animator == null)
        {
            return;
        }

        animator.playOnStart = false;
    }

    void StopChildAnimatorsForStartGate()
    {
        if (!ShouldHoldChildAnimatorsForStartGate())
        {
            return;
        }

        for (var i = 0; i < _paintings.Count; i++)
        {
            var animator = _paintings[i].animator;
            if (animator == null)
            {
                continue;
            }

            animator.playOnStart = false;
            animator.StopAndResetToInitialState();
        }
    }

    bool ShouldHoldChildAnimatorsForStartGate()
    {
        return isWaitingForStartInput && _disableChildPlayOnStartWhileWaiting;
    }

    bool RequiresEyeTrackingReadyBeforeStart(out MeditationChoiceEyeGazeFeedback choiceFeedback)
    {
        choiceFeedback = null;
        if (!_promptMeditationChoiceAfterStages || !_requireEyeTrackingReadyBeforeStart)
        {
            return false;
        }

        choiceFeedback = ResolveMeditationChoiceFeedback();
        return choiceFeedback != null;
    }

    bool TryValidateRequiredEyeTrackingReadyForStart(string inputSource, out string reason)
    {
        reason = null;
        if (!RequiresEyeTrackingReadyBeforeStart(out var choiceFeedback))
        {
            return true;
        }

        if (choiceFeedback.TryEnsureEyeTrackingReady(out reason))
        {
            return true;
        }

        if (string.IsNullOrEmpty(reason))
        {
            reason = "eye tracking readiness check returned false";
        }

        LogStartButtonInput(string.Format(
            CultureInfo.InvariantCulture,
            "Eye tracking is not ready for start source={0} reason={1}",
            inputSource,
            reason));
        return false;
    }

    void StopWaitingForEyeTrackingStartGate()
    {
        if (_startWhenEyeTrackingReadyRoutine == null)
        {
            return;
        }

        StopCoroutine(_startWhenEyeTrackingReadyRoutine);
        _startWhenEyeTrackingReadyRoutine = null;
        _hasReceivedStartInput = false;
    }

    bool TryClaimExperimentControl(string inputSource)
    {
        if (_activeExperimentController == null || !_activeExperimentController.isActiveAndEnabled)
        {
            _activeExperimentController = this;
            return true;
        }

        if (_activeExperimentController == this)
        {
            return true;
        }

        Debug.LogWarning(
            string.Format(
                CultureInfo.InvariantCulture,
                "[PaintingRotationController] Ignoring start request from {0}; active experiment controller is {1}. source={2}",
                DescribeController(this),
                DescribeController(_activeExperimentController),
                inputSource),
            this);
        return false;
    }

    void ReleaseExperimentControl()
    {
        if (_activeExperimentController == this)
        {
            _activeExperimentController = null;
        }
    }

    static string DescribeController(PaintingRotationController controller)
    {
        return controller == null
            ? "(none)"
            : string.Format(
                CultureInfo.InvariantCulture,
                "{0}#{1}",
                controller.name,
                controller.GetInstanceID());
    }

    static void ApplySnapshot(MonaLisaVfxController controller, VfxControlSnapshot snapshot)
    {
        if (controller == null)
        {
            return;
        }

        controller.SetControls(
            snapshot.spawnRate,
            snapshot.particleIntensity,
            controller.particleDrag,
            snapshot.particleFrequency,
            controller.cubeSmoothness,
            controller.cubeMetallic);
    }

    void SubscribeToAnimator(StarryNightRhoneVfxAutoAnimator animator)
    {
        UnsubscribeFromCurrentAnimator();
        _currentAnimationCompleted = false;
        _subscribedAnimator = animator;

        if (_subscribedAnimator != null)
        {
            _subscribedAnimator.AnimationCompleted += HandleAnimationCompleted;
            _subscribedAnimator.StageStarted += HandleStageStarted;
            _subscribedAnimator.StageValueApplied += HandleStageValueApplied;
            _subscribedAnimator.StageCompleted += HandleStageCompleted;
            _subscribedAnimator.StageChoicePromptRequested += HandleStageChoicePromptRequested;
        }
    }

    void UnsubscribeFromCurrentAnimator()
    {
        if (_subscribedAnimator != null)
        {
            _subscribedAnimator.AnimationCompleted -= HandleAnimationCompleted;
            _subscribedAnimator.StageStarted -= HandleStageStarted;
            _subscribedAnimator.StageValueApplied -= HandleStageValueApplied;
            _subscribedAnimator.StageCompleted -= HandleStageCompleted;
            _subscribedAnimator.StageChoicePromptRequested -= HandleStageChoicePromptRequested;
            _subscribedAnimator = null;
        }

        _currentAnimationCompleted = false;
    }

    void HandleAnimationCompleted(StarryNightRhoneVfxAutoAnimator finishedAnimator)
    {
        if (finishedAnimator == _subscribedAnimator)
        {
            _currentAnimationCompleted = true;
        }
    }

    void HandleStageStarted(StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent)
    {
        LogStageCsv("stage_started", stageEvent, string.Empty);
    }

    void HandleStageValueApplied(StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent)
    {
        LogStageCsv("stage_value_applied", stageEvent, string.Empty);
    }

    void HandleStageCompleted(StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent)
    {
        LogStageCsv("stage_completed", stageEvent, "Painting holds this stage's final intensity/frequency while waiting for choice.");
    }

    IEnumerator HandleStageChoicePromptRequested(StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent)
    {
        if (!_promptMeditationChoiceAfterStages)
        {
            yield break;
        }

        var choiceFeedback = ResolveMeditationChoiceFeedback();
        if (choiceFeedback == null)
        {
            LogChoiceCsv(
                "choice_prompt_skipped",
                stageEvent,
                null,
                0f,
                "MeditationChoiceEyeGazeFeedback was not found.");
            yield break;
        }

        _choiceCompleted = false;
        _lastChoiceResult = null;
        _choicePromptStartedAt = Time.realtimeSinceStartup;
        SubscribeToMeditationChoiceFeedback(choiceFeedback);

        LogStageCsv("choice_prompt_started", stageEvent, "Orb prompt breathing light starts; choices remain enabled during the breathing cue.");
        choiceFeedback.BeginChoicePrompt(
            _choicePromptColorIntensity,
            _choicePromptBreathInSeconds,
            _choicePromptBreathOutSeconds,
            _choicePromptBreathMinimumAmount);
        BeginChoicePromptReminder();

        while (!_choiceCompleted)
        {
            yield return null;
        }

        UnsubscribeFromMeditationChoiceFeedback();
        StopChoicePromptReminder(true);

        if (_lastChoiceResult != null)
        {
            var waitSeconds = Mathf.Max(0f, _lastChoiceResult.triggeredRealtime - _choicePromptStartedAt);
            choiceFeedback.CancelChoicePrompt(_choiceNormalColorIntensity);
            LogChoiceCsv(
                "choice_completed",
                stageEvent,
                _lastChoiceResult,
                waitSeconds,
                "Selection logged after orb burst completed.");
        }
        else
        {
            choiceFeedback.CancelChoicePrompt(_choiceNormalColorIntensity);
            Debug.LogWarning("[PaintingRotationController] Choice prompt ended without a selection result.", this);
        }
    }

    void HandleMeditationChoiceCompleted(MeditationChoiceEyeGazeFeedback.SelectionResult result)
    {
        _lastChoiceResult = result;
        _choiceCompleted = true;
    }

    void SubscribeToMeditationChoiceFeedback(MeditationChoiceEyeGazeFeedback choiceFeedback)
    {
        UnsubscribeFromMeditationChoiceFeedback();
        if (choiceFeedback == null)
        {
            return;
        }

        _subscribedChoiceFeedback = choiceFeedback;
        _subscribedChoiceFeedback.SelectionCompleted += HandleMeditationChoiceCompleted;
    }

    void UnsubscribeFromMeditationChoiceFeedback()
    {
        if (_subscribedChoiceFeedback == null)
        {
            return;
        }

        _subscribedChoiceFeedback.SelectionCompleted -= HandleMeditationChoiceCompleted;
        _subscribedChoiceFeedback = null;
    }

    void BeginChoicePromptReminder()
    {
        StopChoicePromptReminder(false);
        _choicePromptReminderActive = false;
        _choicePromptSkyOriginalCaptured = CaptureChoicePromptSkyOriginal();
        _choicePromptReminderRoutine = StartCoroutine(ChoicePromptReminderRoutine());
    }

    void StopChoicePromptReminder(bool restoreSky)
    {
        var shouldRestoreSky = restoreSky &&
                               _choicePromptSkyOriginalCaptured &&
                               (_choicePromptReminderActive || _choicePromptSkyTransitionRoutine != null);

        if (_choicePromptReminderRoutine != null)
        {
            StopCoroutine(_choicePromptReminderRoutine);
            _choicePromptReminderRoutine = null;
        }

        StopChoicePromptReminderAudio();

        if (_choicePromptSkyTransitionRoutine != null)
        {
            StopCoroutine(_choicePromptSkyTransitionRoutine);
            _choicePromptSkyTransitionRoutine = null;
        }

        _choicePromptReminderActive = false;

        if (shouldRestoreSky)
        {
            StartChoicePromptSkyTransition(_choicePromptOriginalSkyMiddleColor, true);
        }
        else
        {
            _choicePromptSkyOriginalCaptured = false;
        }
    }

    IEnumerator ChoicePromptReminderRoutine()
    {
        var delaySeconds = Mathf.Max(0f, _choicePromptReminderDelaySeconds);
        var waitedSeconds = 0f;
        while (!_choiceCompleted && waitedSeconds < delaySeconds)
        {
            waitedSeconds += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_choiceCompleted)
        {
            yield break;
        }

        _choicePromptReminderActive = true;
        StartChoicePromptSkyTransition(_choicePromptReminderSkyMiddleColor, false);

        while (!_choiceCompleted)
        {
            PlayChoicePromptReminderAudio();

            var intervalSeconds = Mathf.Max(0.1f, _choicePromptReminderAudioIntervalSeconds);
            var intervalElapsed = 0f;
            while (!_choiceCompleted && intervalElapsed < intervalSeconds)
            {
                intervalElapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }

    bool CaptureChoicePromptSkyOriginal()
    {
        if (!TryResolveChoicePromptGradientSky(out var gradientSky))
        {
            WarnMissingChoicePromptReminderSky();
            return false;
        }

        _choicePromptOriginalSkyMiddleColor = gradientSky.middle.value;
        _choicePromptOriginalSkyMiddleOverrideState = gradientSky.middle.overrideState;
        return true;
    }

    void StartChoicePromptSkyTransition(Color targetColor, bool restoreOriginalOverrideState)
    {
        if (!TryResolveChoicePromptGradientSky(out var gradientSky))
        {
            WarnMissingChoicePromptReminderSky();
            return;
        }

        if (_choicePromptSkyTransitionRoutine != null)
        {
            StopCoroutine(_choicePromptSkyTransitionRoutine);
            _choicePromptSkyTransitionRoutine = null;
        }

        if (isActiveAndEnabled && Application.isPlaying)
        {
            _choicePromptSkyTransitionRoutine = StartCoroutine(ChoicePromptSkyTransitionRoutine(
                gradientSky,
                targetColor,
                restoreOriginalOverrideState));
            return;
        }

        ApplyChoicePromptSkyMiddle(gradientSky, targetColor, true);
        if (restoreOriginalOverrideState)
        {
            gradientSky.middle.overrideState = _choicePromptOriginalSkyMiddleOverrideState;
            _choicePromptSkyOriginalCaptured = false;
        }
    }

    IEnumerator ChoicePromptSkyTransitionRoutine(
        GradientSky gradientSky,
        Color targetColor,
        bool restoreOriginalOverrideState)
    {
        var startColor = gradientSky.middle.value;
        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, _choicePromptReminderSkyTransitionSeconds);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var eased = EvaluateChoicePromptReminderSkyCurve(progress);
            ApplyChoicePromptSkyMiddle(gradientSky, Color.Lerp(startColor, targetColor, eased), true);
            yield return null;
        }

        ApplyChoicePromptSkyMiddle(gradientSky, targetColor, true);
        if (restoreOriginalOverrideState)
        {
            gradientSky.middle.overrideState = _choicePromptOriginalSkyMiddleOverrideState;
            _choicePromptSkyOriginalCaptured = false;
            RequestHdrpSkyEnvironmentUpdate();
        }

        _choicePromptSkyTransitionRoutine = null;
    }

    float EvaluateChoicePromptReminderSkyCurve(float progress)
    {
        var curve = _choicePromptReminderSkyTransitionCurve;
        if (curve == null || curve.length == 0)
        {
            return SmoothStep01(progress);
        }

        return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(progress)));
    }

    static void ApplyChoicePromptSkyMiddle(GradientSky gradientSky, Color color, bool overrideState)
    {
        if (gradientSky == null)
        {
            return;
        }

        gradientSky.middle.overrideState = overrideState;
        gradientSky.middle.value = color;
        RequestHdrpSkyEnvironmentUpdate();
    }

    bool TryResolveChoicePromptGradientSky(out GradientSky gradientSky)
    {
        gradientSky = null;
        if (_choicePromptGradientSky != null)
        {
            gradientSky = _choicePromptGradientSky;
            return true;
        }

        var volume = _choicePromptReminderSkyVolume != null
            ? _choicePromptReminderSkyVolume
            : FindChoicePromptReminderSkyVolume();
        if (volume == null)
        {
            return false;
        }

        var profile = Application.isPlaying ? volume.profile : volume.sharedProfile;
        if (profile == null || !profile.TryGet(out gradientSky))
        {
            return false;
        }

        _choicePromptReminderSkyVolume = volume;
        _choicePromptGradientSky = gradientSky;
        return true;
    }

    static Volume FindChoicePromptReminderSkyVolume()
    {
        Volume fallback = null;
        var volumes = FindObjectsOfType<Volume>(true);
        for (var i = 0; i < volumes.Length; i++)
        {
            var volume = volumes[i];
            if (volume == null || !VolumeHasGradientSky(volume))
            {
                continue;
            }

            if (volume.isGlobal)
            {
                return volume;
            }

            if (fallback == null)
            {
                fallback = volume;
            }
        }

        return fallback;
    }

    static bool VolumeHasGradientSky(Volume volume)
    {
        var profile = volume != null ? volume.sharedProfile : null;
        return profile != null && profile.TryGet<GradientSky>(out _);
    }

    void WarnMissingChoicePromptReminderSky()
    {
        if (_missingChoicePromptReminderSkyWarningIssued)
        {
            return;
        }

        _missingChoicePromptReminderSkyWarningIssued = true;
        Debug.LogWarning(
            "[PaintingRotationController] Choice reminder could not find a Global Volume profile with Gradient Sky; audio reminder will still run.",
            this);
    }

    void PlayChoicePromptReminderAudio()
    {
        var clip = ResolveChoicePromptReminderClip();
        if (clip == null)
        {
            WarnMissingChoicePromptReminderClip();
            return;
        }

        var audioSource = ResolveChoicePromptReminderAudioSource();
        if (audioSource != null)
        {
            audioSource.PlayOneShot(clip, _choicePromptReminderAudioVolume);
            return;
        }

        var position = Camera.main != null ? Camera.main.transform.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, position, _choicePromptReminderAudioVolume);
    }

    AudioClip ResolveChoicePromptReminderClip()
    {
        if (_choicePromptReminderClip != null)
        {
            return _choicePromptReminderClip;
        }

#if UNITY_EDITOR
        _choicePromptReminderClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultChoicePromptReminderClipAssetPath);
#endif
        return _choicePromptReminderClip;
    }

    AudioSource ResolveChoicePromptReminderAudioSource()
    {
        if (_choicePromptReminderAudioSource != null)
        {
            return _choicePromptReminderAudioSource;
        }

        if (_runtimeChoicePromptReminderAudioSource == null)
        {
            _runtimeChoicePromptReminderAudioSource = gameObject.AddComponent<AudioSource>();
            _runtimeChoicePromptReminderAudioSource.playOnAwake = false;
            _runtimeChoicePromptReminderAudioSource.loop = false;
            _runtimeChoicePromptReminderAudioSource.spatialBlend = 0f;
        }

        return _runtimeChoicePromptReminderAudioSource;
    }

    void StopChoicePromptReminderAudio()
    {
        if (_choicePromptReminderAudioSource != null)
        {
            _choicePromptReminderAudioSource.Stop();
        }

        if (_runtimeChoicePromptReminderAudioSource != null)
        {
            _runtimeChoicePromptReminderAudioSource.Stop();
        }
    }

    void WarnMissingChoicePromptReminderClip()
    {
        if (_missingChoicePromptReminderClipWarningIssued)
        {
            return;
        }

        _missingChoicePromptReminderClipWarningIssued = true;
        Debug.LogWarning(
            "[PaintingRotationController] Choice reminder audio clip is not assigned and Assets/Audio/notification.mp3 could not be loaded.",
            this);
    }

    static void RequestHdrpSkyEnvironmentUpdate()
    {
        if (RenderPipelineManager.currentPipeline is HDRenderPipeline hdRenderPipeline)
        {
            hdRenderPipeline.RequestSkyEnvironmentUpdate();
        }
    }

    void AutoAssignChoicePromptReminderDefaultsInEditor()
    {
#if UNITY_EDITOR
        if (_choicePromptReminderClip == null)
        {
            _choicePromptReminderClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(DefaultChoicePromptReminderClipAssetPath);
        }

        if (_choicePromptReminderSkyVolume == null)
        {
            _choicePromptReminderSkyVolume = FindChoicePromptReminderSkyVolume();
        }
#endif
    }

    IEnumerator RunEyeRestGate(PaintingEntry outgoing, PaintingEntry incoming, int completedPaintingCount)
    {
        var startedRealtime = Time.realtimeSinceStartupAsDouble;
        var requiredRestSeconds = Mathf.Max(0f, _eyeRestDuration);

        ResetEyeRestContinuePressedState();
        SetEyeRestOverlayVisible(true);
        LogEyeRestGate(
            "eye_rest_started",
            outgoing,
            incoming,
            completedPaintingCount,
            requiredRestSeconds,
            0f,
            string.Empty);

        var elapsed = 0f;
        while (elapsed < requiredRestSeconds)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        elapsed = Mathf.Max(0f, (float)(Time.realtimeSinceStartupAsDouble - startedRealtime));
        LogEyeRestGate(
            "eye_rest_timer_completed",
            outgoing,
            incoming,
            completedPaintingCount,
            requiredRestSeconds,
            elapsed,
            string.Empty);
        SetEyeRestOverlayVisible(false);

        ResetEyeRestContinuePressedState();
        var inputSource = string.Empty;
        while (!WasEyeRestContinuePressedThisFrame(out inputSource))
        {
            yield return null;
        }

        elapsed = Mathf.Max(0f, (float)(Time.realtimeSinceStartupAsDouble - startedRealtime));
        LogEyeRestGate(
            "eye_rest_continue_pressed",
            outgoing,
            incoming,
            completedPaintingCount,
            requiredRestSeconds,
            elapsed,
            inputSource);
    }

    void SetEyeRestOverlayVisible(bool visible)
    {
        var overlay = visible ? ResolveEyeRestOverlay() : _eyeRestOverlay;
        if (overlay == null)
        {
            if (visible && !_missingEyeRestOverlayWarningIssued)
            {
                Debug.LogWarning(
                    "[PaintingRotationController] Eye rest gate is enabled, but no CanvasGroup overlay is assigned and the runtime overlay could not be created.",
                    this);
                _missingEyeRestOverlayWarningIssued = true;
            }

            return;
        }

        if (visible)
        {
            overlay.gameObject.SetActive(true);
            UpdateRuntimeEyeRestOverlayGeometry();
            if (_runtimeEyeRestOverlayImage != null)
            {
                _runtimeEyeRestOverlayImage.color = _eyeRestOverlayColor;
            }

            overlay.alpha = _eyeRestOverlayAlpha;
            overlay.blocksRaycasts = true;
            overlay.interactable = false;
            return;
        }

        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
        overlay.interactable = false;
        overlay.gameObject.SetActive(false);
    }

    CanvasGroup ResolveEyeRestOverlay()
    {
        if (_eyeRestOverlay != null)
        {
            return _eyeRestOverlay;
        }

        if (!_autoCreateEyeRestOverlay || !Application.isPlaying)
        {
            return null;
        }

        var overlayObject = new GameObject("Eye Rest Black Overlay", typeof(RectTransform));
        var canvas = overlayObject.AddComponent<Canvas>();
        canvas.sortingOrder = short.MaxValue;
        var group = overlayObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var imageObject = new GameObject("Black Panel", typeof(RectTransform));
        imageObject.transform.SetParent(overlayObject.transform, false);
        var image = imageObject.AddComponent<Image>();
        image.color = _eyeRestOverlayColor;
        image.raycastTarget = false;
        StretchToFill(image.rectTransform);

        _eyeRestOverlay = group;
        _runtimeEyeRestCanvas = canvas;
        _runtimeEyeRestOverlayImage = image;
        _createdRuntimeEyeRestOverlay = true;
        UpdateRuntimeEyeRestOverlayGeometry();
        overlayObject.SetActive(false);
        return _eyeRestOverlay;
    }

    void UpdateRuntimeEyeRestOverlayGeometry()
    {
        if (!_createdRuntimeEyeRestOverlay || _runtimeEyeRestCanvas == null)
        {
            return;
        }

        var camera = ResolveEyeRestCamera();
        var rootRect = _runtimeEyeRestCanvas.GetComponent<RectTransform>();
        if (camera != null)
        {
            _runtimeEyeRestCanvas.renderMode = RenderMode.WorldSpace;
            _runtimeEyeRestCanvas.worldCamera = camera;
            var overlayTransform = _runtimeEyeRestCanvas.transform;
            if (overlayTransform.parent != camera.transform)
            {
                overlayTransform.SetParent(camera.transform, false);
            }

            var distance = Mathf.Max(
                _autoEyeRestOverlayDistanceMeters,
                camera.nearClipPlane + 0.05f);
            overlayTransform.localPosition = new Vector3(0f, 0f, distance);
            overlayTransform.localRotation = Quaternion.identity;
            overlayTransform.localScale = Vector3.one;

            if (rootRect != null)
            {
                var height = 2f *
                             distance *
                             Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) *
                             AutoEyeRestOverlayFovPadding;
                var width = height * Mathf.Max(0.01f, camera.aspect);
                rootRect.sizeDelta = new Vector2(width, height);
            }
        }
        else
        {
            _runtimeEyeRestCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (rootRect != null)
            {
                StretchToFill(rootRect);
            }
        }

        if (_runtimeEyeRestOverlayImage != null)
        {
            StretchToFill(_runtimeEyeRestOverlayImage.rectTransform);
        }
    }

    static void StretchToFill(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    static Camera ResolveEyeRestCamera()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        return Object.FindObjectOfType<Camera>();
    }

    void LogEyeRestGate(
        string phase,
        PaintingEntry outgoing,
        PaintingEntry incoming,
        int completedPaintingCount,
        float requiredRestSeconds,
        float elapsedSeconds,
        string inputSource)
    {
        var message = FormatEyeRestLogMessage(
            phase,
            outgoing != null ? outgoing.gameObject.name : string.Empty,
            incoming != null ? incoming.gameObject.name : string.Empty,
            completedPaintingCount,
            requiredRestSeconds,
            elapsedSeconds,
            inputSource);

        if (_logEyeRestGate)
        {
            Debug.Log(message, this);
        }

        ResolveExperimentCsvLogger().LogEvent(new MeditationExperimentCsvLogger.Row
        {
            eventType = phase,
            paintingRunIndex = outgoing != null ? outgoing.currentRunIndex : -1,
            paintingIndex = outgoing != null ? _paintings.IndexOf(outgoing) + 1 : -1,
            paintingId = outgoing != null ? outgoing.paintingId : string.Empty,
            paintingName = outgoing != null ? outgoing.gameObject.name : string.Empty,
            eventRealtime = Time.realtimeSinceStartupAsDouble,
            notes = message
        });
    }

    void ResetEyeRestContinuePressedState()
    {
        _wasUnityXrRightControllerBButtonPressed = IsUnityXrRightControllerBButtonPressed(out _);
        _wasOvrRightControllerBButtonPressed = IsOvrRightControllerBButtonPressed(out _);
    }

    void OnValidate()
    {
        _fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
        _dissolveHoldDuration = Mathf.Max(0f, _dissolveHoldDuration);
        _fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);
        _progressLogInterval = Mathf.Max(0.05f, _progressLogInterval);
        _feedbackDelay = Mathf.Max(0f, _feedbackDelay);
        _feedbackTimeoutRatio = Mathf.Clamp(_feedbackTimeoutRatio, 0.05f, 1f);
        _choicePromptBreathInSeconds = Mathf.Max(0.1f, _choicePromptBreathInSeconds);
        _choicePromptBreathOutSeconds = Mathf.Max(0.1f, _choicePromptBreathOutSeconds);
        _choicePromptBreathMinimumAmount = Mathf.Clamp01(_choicePromptBreathMinimumAmount);
        _choicePromptReminderDelaySeconds = Mathf.Max(0f, _choicePromptReminderDelaySeconds);
        _choicePromptReminderSkyTransitionSeconds = Mathf.Max(0.01f, _choicePromptReminderSkyTransitionSeconds);
        if (_choicePromptReminderSkyTransitionCurve == null || _choicePromptReminderSkyTransitionCurve.length == 0)
        {
            _choicePromptReminderSkyTransitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        _choicePromptReminderAudioIntervalSeconds = Mathf.Max(0.1f, _choicePromptReminderAudioIntervalSeconds);
        _choicePromptReminderAudioVolume = Mathf.Clamp01(_choicePromptReminderAudioVolume);
        AutoAssignChoicePromptReminderDefaultsInEditor();
        _eyeTrackingStartGateTimeoutSeconds = Mathf.Max(0f, _eyeTrackingStartGateTimeoutSeconds);
        _eyeTrackingStartGatePollSeconds = Mathf.Max(0.05f, _eyeTrackingStartGatePollSeconds);
        _paintingsBetweenEyeRests = Mathf.Max(1, _paintingsBetweenEyeRests);
        _eyeRestDuration = Mathf.Max(0f, _eyeRestDuration);
        _autoEyeRestOverlayDistanceMeters = Mathf.Max(0.1f, _autoEyeRestOverlayDistanceMeters);
        _eyeRestOverlayAlpha = Mathf.Clamp01(_eyeRestOverlayAlpha);
    }

    bool WasStartButtonPressedThisFrame(out string inputSource)
    {
        inputSource = string.Empty;

#if UNITY_EDITOR
        if (_allowKeyboardStartInEditor &&
            (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            inputSource = "Keyboard(A/Space/Return)";
            return true;
        }
#endif

        var unityXrPressed = IsUnityXrRightControllerAButtonPressed(out var unityXrSource);
        var unityXrPressedThisFrame = IsPressedThisFrame(
            unityXrPressed,
            _wasUnityXrRightControllerAButtonPressed);
        _wasUnityXrRightControllerAButtonPressed = unityXrPressed;
        if (unityXrPressedThisFrame)
        {
            inputSource = unityXrSource;
            return true;
        }

        var ovrPressed = IsOvrRightControllerAButtonPressed(out var ovrSource);
        var ovrPressedThisFrame = IsPressedThisFrame(
            ovrPressed,
            _wasOvrRightControllerAButtonPressed);
        _wasOvrRightControllerAButtonPressed = ovrPressed;
        if (ovrPressedThisFrame)
        {
            inputSource = ovrSource;
            return true;
        }

        return false;
    }

    static bool IsUnityXrRightControllerAButtonPressed(out string inputSource)
    {
        inputSource = "UnityEngine.XR CommonUsages.primaryButton";
        InputDevicesBuffer.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            InputDevicesBuffer);

        for (var i = 0; i < InputDevicesBuffer.Count; i++)
        {
            if (InputDevicesBuffer[i].TryGetFeatureValue(CommonUsages.primaryButton, out var primaryButton) &&
                primaryButton)
            {
                inputSource = string.Format(
                    CultureInfo.InvariantCulture,
                    "UnityEngine.XR primaryButton device=\"{0}\"",
                    InputDevicesBuffer[i].name);
                return true;
            }
        }

        return false;
    }

    static bool IsOvrRightControllerAButtonPressed(out string inputSource)
    {
        var buttonOnePressed = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch);
        var rawAOnRightTouchPressed = OVRInput.Get(OVRInput.RawButton.A, OVRInput.Controller.RTouch);
        var rawAOnActivePressed = OVRInput.Get(OVRInput.RawButton.A, OVRInput.Controller.Active);
        var pressed = buttonOnePressed || rawAOnRightTouchPressed || rawAOnActivePressed;

        inputSource = string.Format(
            CultureInfo.InvariantCulture,
            "OVRInput Button.One/RawButton.A buttonOne={0} rawA(RTouch)={1} rawA(Active)={2} active={3} connected={4}",
            buttonOnePressed,
            rawAOnRightTouchPressed,
            rawAOnActivePressed,
            OVRInput.GetActiveController(),
            OVRInput.GetConnectedControllers());

        return pressed;
    }

    bool WasEyeRestContinuePressedThisFrame(out string inputSource)
    {
        inputSource = string.Empty;

        if (_eyeRestContinueKey != KeyCode.None && Input.GetKeyDown(_eyeRestContinueKey))
        {
            inputSource = "Keyboard(" + _eyeRestContinueKey + ")";
            return true;
        }

        if (!_allowRightControllerBButtonForEyeRest)
        {
            return false;
        }

        var unityXrPressed = IsUnityXrRightControllerBButtonPressed(out var unityXrSource);
        var unityXrPressedThisFrame = IsPressedThisFrame(
            unityXrPressed,
            _wasUnityXrRightControllerBButtonPressed);
        _wasUnityXrRightControllerBButtonPressed = unityXrPressed;
        if (unityXrPressedThisFrame)
        {
            inputSource = unityXrSource;
            return true;
        }

        var ovrPressed = IsOvrRightControllerBButtonPressed(out var ovrSource);
        var ovrPressedThisFrame = IsPressedThisFrame(
            ovrPressed,
            _wasOvrRightControllerBButtonPressed);
        _wasOvrRightControllerBButtonPressed = ovrPressed;
        if (ovrPressedThisFrame)
        {
            inputSource = ovrSource;
            return true;
        }

        return false;
    }

    static bool IsUnityXrRightControllerBButtonPressed(out string inputSource)
    {
        inputSource = "UnityEngine.XR CommonUsages.secondaryButton";
        InputDevicesBuffer.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            InputDevicesBuffer);

        for (var i = 0; i < InputDevicesBuffer.Count; i++)
        {
            if (InputDevicesBuffer[i].TryGetFeatureValue(CommonUsages.secondaryButton, out var secondaryButton) &&
                secondaryButton)
            {
                inputSource = string.Format(
                    CultureInfo.InvariantCulture,
                    "UnityEngine.XR secondaryButton device=\"{0}\"",
                    InputDevicesBuffer[i].name);
                return true;
            }
        }

        return false;
    }

    static bool IsOvrRightControllerBButtonPressed(out string inputSource)
    {
        var buttonTwoPressed = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch);
        var rawBOnRightTouchPressed = OVRInput.Get(OVRInput.RawButton.B, OVRInput.Controller.RTouch);
        var rawBOnActivePressed = OVRInput.Get(OVRInput.RawButton.B, OVRInput.Controller.Active);
        var pressed = buttonTwoPressed || rawBOnRightTouchPressed || rawBOnActivePressed;

        inputSource = string.Format(
            CultureInfo.InvariantCulture,
            "OVRInput Button.Two/RawButton.B buttonTwo={0} rawB(RTouch)={1} rawB(Active)={2} active={3} connected={4}",
            buttonTwoPressed,
            rawBOnRightTouchPressed,
            rawBOnActivePressed,
            OVRInput.GetActiveController(),
            OVRInput.GetConnectedControllers());

        return pressed;
    }

    void LogStartButtonInput(string message)
    {
        if (!_logStartButtonInput)
        {
            return;
        }

        Debug.Log("[PaintingRotationController] " + message, this);
    }

    void BeginExperimentSessionIfNeeded(string inputSource)
    {
        if (_hasStartedExperimentSession)
        {
            return;
        }

        _paintingRunIndex = 0;
        _hasStartedExperimentSession = ResolveExperimentCsvLogger().StartSession(
            FormatSessionInputSource(inputSource));
        if (!_hasStartedExperimentSession)
        {
            Debug.LogWarning(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[PaintingRotationController] CSV session was not started for {0}. source={1}",
                    DescribeController(this),
                    inputSource),
                this);
        }
    }

    string FormatSessionInputSource(string inputSource)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} controller={1}",
            inputSource,
            DescribeController(this));
    }

    MeditationExperimentCsvLogger ResolveExperimentCsvLogger()
    {
        if (_experimentCsvLogger == null)
        {
            _experimentCsvLogger = MeditationExperimentCsvLogger.Instance;
        }

        return _experimentCsvLogger;
    }

    MeditationChoiceEyeGazeFeedback ResolveMeditationChoiceFeedback()
    {
        if (_meditationChoiceFeedback == null)
        {
            _meditationChoiceFeedback = FindObjectOfType<MeditationChoiceEyeGazeFeedback>();
        }

        return _meditationChoiceFeedback;
    }

    void ConfigureMeditationChoiceForIdle()
    {
        UnsubscribeFromMeditationChoiceFeedback();
        StopChoicePromptReminder(true);

        if (!_promptMeditationChoiceAfterStages)
        {
            return;
        }

        var choiceFeedback = ResolveMeditationChoiceFeedback();
        if (choiceFeedback == null)
        {
            return;
        }

        choiceFeedback.CancelChoicePrompt(_choiceNormalColorIntensity);
    }

    void BeginPaintingRun(PaintingEntry painting)
    {
        if (painting == null)
        {
            return;
        }

        painting.currentRunIndex = ++_paintingRunIndex;
        painting.runStartedRealtime = Time.realtimeSinceStartupAsDouble;
        ResolveExperimentCsvLogger().LogPaintingStarted(CreatePaintingRow(
            "painting_started",
            painting,
            "Painting run started."));
    }

    void CompletePaintingRun(PaintingEntry painting)
    {
        if (painting == null)
        {
            return;
        }

        painting.runEndedRealtime = Time.realtimeSinceStartupAsDouble;
        ResolveExperimentCsvLogger().LogPaintingCompleted(CreatePaintingRow(
            "painting_completed",
            painting,
            "Painting run completed."));
    }

    void LogStageCsv(
        string eventType,
        StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent,
        string notes)
    {
        var painting = FindPainting(stageEvent != null ? stageEvent.animator : null);
        var logger = ResolveExperimentCsvLogger();
        var row = CreateStageRow(eventType, painting, stageEvent, notes);
        if (string.Equals(eventType, "stage_value_applied"))
        {
            logger.LogStageValue(row);
        }
        else
        {
            logger.LogEvent(row);
        }
    }

    void LogChoiceCsv(
        string eventType,
        StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent,
        MeditationChoiceEyeGazeFeedback.SelectionResult selection,
        float choiceWaitSeconds,
        string notes)
    {
        var painting = FindPainting(stageEvent != null ? stageEvent.animator : null);
        var row = CreateStageRow(eventType, painting, stageEvent, notes);
        if (selection != null)
        {
            row.orbIndex = selection.orbIndex + 1;
            row.orbLabel = selection.label;
            row.selectionDwellSeconds = selection.dwellSeconds;
            row.selectionEffectSeconds = selection.effectSeconds;
            row.selectionTriggeredRealtime = selection.triggeredRealtime;
            row.selectionTriggeredFrame = selection.triggeredFrame;
            row.eventRealtime = selection.completedRealtime;
        }

        row.promptStartedRealtime =
            (string.Equals(eventType, "choice_completed") || string.Equals(eventType, "choice_timeout")) &&
            _choicePromptStartedAt > 0f
                ? _choicePromptStartedAt
                : double.NaN;
        row.choiceWaitSeconds = choiceWaitSeconds;
        row.orbColorIntensity = _choiceNormalColorIntensity;
        ResolveExperimentCsvLogger().LogChoice(row);
    }

    MeditationExperimentCsvLogger.Row CreatePaintingRow(
        string eventType,
        PaintingEntry painting,
        string notes)
    {
        return new MeditationExperimentCsvLogger.Row
        {
            eventType = eventType,
            paintingRunIndex = painting != null ? painting.currentRunIndex : -1,
            paintingIndex = painting != null ? _paintings.IndexOf(painting) + 1 : -1,
            paintingId = painting != null ? painting.paintingId : string.Empty,
            paintingName = painting != null ? painting.gameObject.name : string.Empty,
            paintingStartedRealtime = painting != null ? painting.runStartedRealtime : double.NaN,
            paintingEndedRealtime = painting != null ? painting.runEndedRealtime : double.NaN,
            eventRealtime = string.Equals(eventType, "painting_completed") && painting != null
                ? painting.runEndedRealtime
                : painting != null ? painting.runStartedRealtime : double.NaN,
            stageOrder = painting != null && painting.animator != null ? painting.animator.lastStageOrderCsv : string.Empty,
            notes = notes
        };
    }

    MeditationExperimentCsvLogger.Row CreateStageRow(
        string eventType,
        PaintingEntry painting,
        StarryNightRhoneVfxAutoAnimator.StageEvent stageEvent,
        string notes)
    {
        var row = new MeditationExperimentCsvLogger.Row
        {
            eventType = eventType,
            paintingRunIndex = painting != null ? painting.currentRunIndex : -1,
            paintingIndex = painting != null ? _paintings.IndexOf(painting) + 1 : -1,
            paintingId = painting != null ? painting.paintingId : string.Empty,
            paintingName = painting != null ? painting.gameObject.name : string.Empty,
            notes = notes
        };

        if (stageEvent != null)
        {
            row.stageIndex = stageEvent.stageIndex + 1;
            row.stagePresetIndex = stageEvent.stagePresetIndex + 1;
            row.stageCount = stageEvent.stageCount;
            row.stageValueIndex = stageEvent.stageValueIndex >= 0 ? stageEvent.stageValueIndex + 1 : -1;
            row.stageValueCount = stageEvent.stageValueCount;
            row.stageRangeMinimum = stageEvent.rangeMinimum;
            row.stageRangeMaximum = stageEvent.rangeMaximum;
            row.particleIntensity = stageEvent.particleIntensity;
            row.particleFrequency = stageEvent.particleFrequency;
            row.eventRealtime = stageEvent.realtimeSinceStartup;
            row.stageStartedRealtime = stageEvent.stageStartedRealtime;
        }

        if (string.Equals(eventType, "choice_prompt_started"))
        {
            row.orbColorIntensity = _choicePromptColorIntensity;
        }
        else if (string.Equals(eventType, "choice_timeout"))
        {
            row.orbColorIntensity = _choiceNormalColorIntensity;
        }

        return row;
    }

    PaintingEntry FindPainting(StarryNightRhoneVfxAutoAnimator animator)
    {
        if (animator == null)
        {
            return null;
        }

        if (_currentIndex >= 0 &&
            _currentIndex < _paintings.Count &&
            _paintings[_currentIndex].animator == animator)
        {
            return _paintings[_currentIndex];
        }

        for (var i = 0; i < _paintings.Count; i++)
        {
            if (_paintings[i].animator == animator)
            {
                return _paintings[i];
            }
        }

        return null;
    }

    void TryStartCalmnessFeedback(PaintingEntry painting, float timeoutSeconds, float totalViewDurationSec)
    {
        if (!_collectCalmnessFeedback || timeoutSeconds <= 0f)
        {
            return;
        }

        var collector = _feedbackCollector != null ? _feedbackCollector : CalmnessFeedbackCollector.Instance;
        if (collector == null)
        {
            return;
        }

        collector.ShowAndCollect(
            painting.paintingId,
            _currentIndex,
            totalViewDurationSec,
            timeoutSeconds,
            CalmnessFeedbackLogger.Log);
    }

    float GetPaintingViewDuration(PaintingEntry painting)
    {
        if (painting.animator != null && painting.animator.currentOrLastPlayDuration > 0f)
        {
            return painting.animator.currentOrLastPlayDuration;
        }

        return Mathf.Max(0f, Time.time - painting.activatedAtSeconds);
    }

    void LogTransition(
        string phase,
        PaintingEntry outgoing,
        PaintingEntry incoming,
        float elapsedSeconds,
        float durationSeconds,
        float progress,
        float spawnRate)
    {
        if (!_logTransitions)
        {
            return;
        }

        Debug.Log(
            FormatTransitionLogMessage(
                phase,
                outgoing.gameObject.name,
                incoming.gameObject.name,
                elapsedSeconds,
                durationSeconds,
                progress,
                spawnRate) +
            string.Format(
                CultureInfo.InvariantCulture,
                " runtime={0}ms",
                SecondsToMilliseconds(Time.time)),
            this);
    }

    static int SecondsToMilliseconds(float seconds)
    {
        return Mathf.RoundToInt(Mathf.Max(0f, seconds) * 1000f);
    }

    sealed class PaintingEntry
    {
        public readonly GameObject gameObject;
        public readonly string paintingId;
        public readonly MonaLisaVfxController controller;
        public readonly StarryNightRhoneVfxAutoAnimator animator;
        public readonly VfxControlSnapshot visibleControls;
        public float activatedAtSeconds;
        public int currentRunIndex = -1;
        public double runStartedRealtime = double.NaN;
        public double runEndedRealtime = double.NaN;

        public PaintingEntry(
            GameObject gameObject,
            string paintingId,
            MonaLisaVfxController controller,
            StarryNightRhoneVfxAutoAnimator animator,
            VfxControlSnapshot visibleControls)
        {
            this.gameObject = gameObject;
            this.paintingId = paintingId;
            this.controller = controller;
            this.animator = animator;
            this.visibleControls = visibleControls;
        }
    }

    public readonly struct VfxControlSnapshot
    {
        public static readonly VfxControlSnapshot zero = new VfxControlSnapshot(0f, 0f, 0f);

        public readonly float spawnRate;
        public readonly float particleIntensity;
        public readonly float particleFrequency;

        public VfxControlSnapshot(float spawnRate, float particleIntensity, float particleFrequency)
        {
            this.spawnRate = Mathf.Max(0f, spawnRate);
            this.particleIntensity = Mathf.Max(0f, particleIntensity);
            this.particleFrequency = Mathf.Max(0f, particleFrequency);
        }

        public static VfxControlSnapshot Lerp(VfxControlSnapshot from, VfxControlSnapshot to, float progress)
        {
            var t = Mathf.Clamp01(progress);
            return new VfxControlSnapshot(
                Mathf.Lerp(from.spawnRate, to.spawnRate, t),
                Mathf.Lerp(from.particleIntensity, to.particleIntensity, t),
                Mathf.Lerp(from.particleFrequency, to.particleFrequency, t));
        }
    }
}
