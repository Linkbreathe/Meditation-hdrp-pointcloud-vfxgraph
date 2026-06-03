using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.VFX;

[DefaultExecutionOrder(300)]
[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Experiment Manager")]
public sealed class WaterLiliesExperimentManager : MonoBehaviour
{
    const string ResourceConfigName = "WaterLiliesExperimentConfig";

    public const string EventExperimentStart = "experiment_start";
    public const string EventExperimentSetupError = "experiment_setup_error";
    public const string EventBaselineStart = "baseline_start";
    public const string EventBaselineEnd = "baseline_end";
    public const string EventAdaptationStart = "adaptation_start";
    public const string EventAdaptationEnd = "adaptation_end";
    public const string EventConditionPrepare = "condition_prepare";
    public const string EventConditionStart = "condition_start";
    public const string EventConditionEnd = "condition_end";
    public const string EventQuestionnaireBreakStart = "questionnaire_break_start";
    public const string EventQuestionnaireBreakEnd = "questionnaire_break_end";
    public const string EventHeadsetRemoved = "headset_removed";
    public const string EventHeadsetWorn = "headset_worn";
    public const string EventRecenterStart = "recenter_start";
    public const string EventRecenterEnd = "recenter_end";
    public const string EventRestStart = "rest_start";
    public const string EventRestEnd = "rest_end";
    public const string EventExperimentAborted = "experiment_aborted";
    public const string EventExperimentEnd = "experiment_end";
    public const string EventVideoRecordingStart = "video_recording_started";
    public const string EventVideoRecordingStop = "video_recording_stopped";
    public const string EventVideoRecordingFailed = "video_recording_failed";

    static readonly string[] RequiredEventMarkersBacking =
    {
        EventVideoRecordingStart,
        EventExperimentStart,
        EventBaselineStart,
        EventBaselineEnd,
        EventAdaptationStart,
        EventAdaptationEnd,
        EventConditionPrepare,
        EventConditionStart,
        EventConditionEnd,
        EventQuestionnaireBreakStart,
        EventHeadsetRemoved,
        EventHeadsetWorn,
        EventQuestionnaireBreakEnd,
        EventRecenterStart,
        EventRecenterEnd,
        EventRestStart,
        EventRestEnd,
        EventExperimentEnd,
        EventVideoRecordingStop
    };

    public static string[] RequiredEventMarkers => (string[])RequiredEventMarkersBacking.Clone();

    [Header("Config")]
    [SerializeField] WaterLiliesExperimentConfig _config;

    [Header("References")]
    [SerializeField] WaterLiliesVfxController _vfxController;
    [SerializeField] WaterLiliesExperimentLogger _logger;
    [SerializeField] WaterLiliesTrackingSampler _trackingSampler;
    [SerializeField] WaterLiliesLslMarkerOutlet _lslMarkerOutlet;
    [SerializeField] GameObject _targetPainting;

    [Header("Recording")]
    [SerializeField] bool _recordVideo = true;
    [SerializeField] ExperimentVideoRecorder _videoRecorder;
    [SerializeField] bool _autoCreateVideoRecorder = true;
    [SerializeField, Min(0f)] float _videoRecorderFlushTimeoutSeconds = 5f;

    [Header("Data Output")]
    [SerializeField] bool _overrideConfigLogRoot;
    [SerializeField] string _dataRootPath = "";

    [Header("LSL")]
    [SerializeField] bool _autoCreateLslMarkerOutlet = true;

    [Header("Keyboard Controls")]
    [SerializeField] KeyCode _startKey = KeyCode.S;
    [SerializeField] KeyCode _continueKey = KeyCode.Space;
    [SerializeField] KeyCode _headsetRemovedKey = KeyCode.H;
    [SerializeField] KeyCode _headsetWornKey = KeyCode.J;
    [SerializeField] KeyCode _pilotSkipKey = KeyCode.K;
    [SerializeField] KeyCode _abortKey = KeyCode.Escape;

    [Header("Runtime UI")]
    [SerializeField] bool _prepareFixedPaintingOnPlay = true;
    [SerializeField] bool _autoCreateRuntimeUi = true;
    [SerializeField] Vector2 _runtimeUiSize = new Vector2(560f, 360f);

    [Header("Runtime Painting Placement")]
    [Tooltip("When enabled, Play Mode moves the painting in front of the current MainCamera/CenterEyeAnchor. Disable this when positioning the painting manually in the scene.")]
    [SerializeField] bool _placePaintingInFrontOfViewer;
    [SerializeField, Min(0.5f)] float _paintingDistanceMeters = 4f;
    [SerializeField] float _paintingVerticalOffsetMeters;
    [SerializeField] bool _alignPaintingYawWithViewer = true;

    readonly List<WaterLiliesResolvedCondition> _orderedConditions = new List<WaterLiliesResolvedCondition>();
    Coroutine _runRoutine;
    WaterLiliesExperimentPhase _phase = WaterLiliesExperimentPhase.Idle;
    WaterLiliesResolvedCondition _currentCondition;
    bool _hasCurrentCondition;
    int _currentConditionIndex = -1;
    double _phaseStartedRealtime;
    double _phasePlannedDurationSeconds = double.NaN;
    bool _formalViewingActive;
    bool _continueRequested;
    bool _abortRequested;
    bool _pilotSkipRequested;
    bool _sessionClosing;
    bool _headsetPresenceKnown;
    bool _lastHeadsetUserPresent;
    bool _headsetCurrentlyOff;
    bool _questionnaireHeadsetRemovedRecorded;
    bool _questionnaireHeadsetWornRecorded;
    double _headsetRemovedStartedRealtime = double.NaN;
    double _nextSampleRealtime;

    public WaterLiliesExperimentPhase phase => _phase;
    public bool isRunning => _runRoutine != null;
    public string dataRootPath => ResolveDataRootOverride();
    public bool operatorUiEnabled => _autoCreateRuntimeUi;
    public string operatorPhaseText => BuildPhaseBadgeText();
    public string operatorNextActionText => BuildNextActionText();
    public string operatorCurrentIntensityText => FormatCurrentVfxParameter(currentVfxIntensity);
    public string operatorCurrentFrequencyText => FormatCurrentVfxParameter(currentVfxFrequency);
    public string operatorStatusText => BuildStatusText();
    public float currentVfxIntensity => _vfxController != null ? _vfxController.currentIntensity : float.NaN;
    public float currentVfxFrequency => _vfxController != null ? _vfxController.currentFrequency : float.NaN;

    void Reset()
    {
        _logger = GetComponent<WaterLiliesExperimentLogger>();
        _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        _lslMarkerOutlet = GetComponent<WaterLiliesLslMarkerOutlet>();
    }

    void Awake()
    {
        ResolveConfig();
        ResolveLocalComponents();
        PrepareFixedPaintingPreview();
        EnsureRuntimeUi();
    }

    void Start()
    {
        if (_config != null && _config.autoStart)
        {
            StartExperiment();
        }
    }

    void Update()
    {
        HandleKeyboard();
        UpdateHeadsetPresenceMarkers();
        LogTrackingSampleIfDue();
    }

    void OnDisable()
    {
        if (_logger != null && _logger.sessionActive)
        {
            StopVideoRecording("component_disabled");
            _logger.CloseSession();
        }
    }

    [ContextMenu("Start Water Lilies Experiment")]
    public void StartExperiment()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }

        _abortRequested = false;
        _continueRequested = false;
        _pilotSkipRequested = false;
        _sessionClosing = false;
        _runRoutine = StartCoroutine(RunExperimentRoutine());
    }

    [ContextMenu("Continue Current Break")]
    public void ContinueCurrentBreak()
    {
        _continueRequested = true;
    }

    [ContextMenu("Mark Headset Removed")]
    public void MarkHeadsetRemoved()
    {
        MarkHeadsetRemoved("manual");
    }

    [ContextMenu("Mark Headset Worn Again")]
    public void MarkHeadsetWorn()
    {
        MarkHeadsetWorn("manual");
    }

    [ContextMenu("Abort Water Lilies Experiment")]
    public void AbortExperiment()
    {
        _abortRequested = true;
    }

    IEnumerator RunExperimentRoutine()
    {
        ResolveConfig();
        ResolveLocalComponents();

        _orderedConditions.Clear();
        _config.BuildResolvedConditionList(_orderedConditions);
        _currentConditionIndex = -1;
        _hasCurrentCondition = false;
        _nextSampleRealtime = 0.0;

        if (!ValidateConditionPlan(out var conditionPlanError))
        {
            Debug.LogError("[WaterLiliesExperimentManager] " + conditionPlanError, this);
            _runRoutine = null;
            yield break;
        }

        _logger.StartSession(_config, ResolveDataRootOverride());
        StartVideoRecording("experiment_start");
        LogEvent(EventExperimentStart, "Water Lilies Intensity x Frequency experiment started.");

        if (!PrepareFixedPainting())
        {
            LogEvent(EventExperimentSetupError, "Target Water Lilies painting or VFX binding was not available.");
            yield return FinishRoutine(true);
            yield break;
        }

        _vfxController.ApplyBaseline(_config);
        yield return RunTimedPhase(
            WaterLiliesExperimentPhase.Baseline,
            _config.baselineSeconds,
            EventBaselineStart,
            EventBaselineEnd,
            false);

        if (_abortRequested)
        {
            yield return FinishRoutine(true);
            yield break;
        }

        _vfxController.ApplyAdaptation(_config);
        yield return RunTimedPhase(
            WaterLiliesExperimentPhase.Adaptation,
            _config.adaptationSeconds,
            EventAdaptationStart,
            EventAdaptationEnd,
            false);

        for (var i = 0; i < _orderedConditions.Count; i++)
        {
            if (_abortRequested)
            {
                break;
            }

            _currentCondition = _orderedConditions[i];
            _currentConditionIndex = i;
            _hasCurrentCondition = true;
            SetPhase(WaterLiliesExperimentPhase.ConditionPrepare, 0.0);
            _vfxController.FreezeFormalStimulus();
            LogEvent(EventConditionPrepare, "Preparing " + _currentCondition.conditionId + ".");

            yield return RunTimedPhase(
                WaterLiliesExperimentPhase.RecenterStabilization,
                _config.recenterSeconds,
                EventRecenterStart,
                EventRecenterEnd,
                false);

            if (_abortRequested)
            {
                break;
            }

            _vfxController.ApplyCondition(_currentCondition);
            yield return RunTimedPhase(
                WaterLiliesExperimentPhase.ConditionViewing,
                _currentCondition.durationSeconds,
                EventConditionStart,
                EventConditionEnd,
                true);

            _formalViewingActive = false;
            _vfxController.FreezeFormalStimulus();

            if (_abortRequested)
            {
                break;
            }

            yield return RunQuestionnaireBreak();

            if (_abortRequested)
            {
                break;
            }

            var completedCount = i + 1;
            var shouldRest = _config.restSeconds > 0f &&
                             completedCount < _orderedConditions.Count &&
                             completedCount % _config.restEveryConditionCount == 0;
            if (shouldRest)
            {
                yield return RunTimedPhase(
                    WaterLiliesExperimentPhase.Rest,
                    _config.restSeconds,
                    EventRestStart,
                    EventRestEnd,
                    false);
            }
        }

        yield return FinishRoutine(_abortRequested);
    }

    IEnumerator RunTimedPhase(
        WaterLiliesExperimentPhase nextPhase,
        double durationSeconds,
        string startEvent,
        string endEvent,
        bool formalViewing)
    {
        SetPhase(nextPhase, durationSeconds);
        _formalViewingActive = formalViewing;
        if (formalViewing && nextPhase == WaterLiliesExperimentPhase.ConditionViewing && _vfxController != null)
        {
            _vfxController.ResetNaturalTextureTemplate();
        }

        LogEvent(startEvent);

        var endRealtime = Time.realtimeSinceStartupAsDouble + Math.Max(0.0, durationSeconds);
        while (!_abortRequested && Time.realtimeSinceStartupAsDouble < endRealtime)
        {
            if (CanPilotSkip() && _pilotSkipRequested)
            {
                _pilotSkipRequested = false;
                break;
            }

            yield return null;
        }

        LogEvent(endEvent);
        if (formalViewing)
        {
            _formalViewingActive = false;
        }
    }

    IEnumerator RunQuestionnaireBreak()
    {
        SetPhase(WaterLiliesExperimentPhase.QuestionnaireBreak, 0.0);
        _continueRequested = false;
        _questionnaireHeadsetRemovedRecorded = _headsetCurrentlyOff;
        _questionnaireHeadsetWornRecorded = false;
        var breakStartedRealtime = Time.realtimeSinceStartupAsDouble;
        LogEvent(
            EventQuestionnaireBreakStart,
            _currentCondition.conditionId + " completed. Please complete Google Form for " + _currentCondition.conditionId + ".");

        while (!_abortRequested)
        {
            var elapsed = Time.realtimeSinceStartupAsDouble - breakStartedRealtime;
            var minimumElapsed = elapsed >= _config.questionnaireMinimumSeconds;
            var headsetReady = IsQuestionnaireHeadsetReady();
            if (!_config.requireManualQuestionnaireContinue && minimumElapsed && headsetReady)
            {
                break;
            }

            if (_continueRequested && minimumElapsed && headsetReady)
            {
                break;
            }

            yield return null;
        }

        LogEvent(EventQuestionnaireBreakEnd, "Questionnaire break ended.");
        _questionnaireHeadsetRemovedRecorded = false;
        _questionnaireHeadsetWornRecorded = false;
    }

    bool IsQuestionnaireHeadsetReady()
    {
        if (_config == null)
        {
            return true;
        }

        if (_config.requireHeadsetCycleBeforeQuestionnaireContinue)
        {
            return _questionnaireHeadsetRemovedRecorded &&
                   _questionnaireHeadsetWornRecorded &&
                   !_headsetCurrentlyOff;
        }

        return !_config.requireHeadsetWornBeforeQuestionnaireContinue || !_headsetCurrentlyOff;
    }

    IEnumerator FinishRoutine(bool aborted)
    {
        _formalViewingActive = false;
        if (_vfxController != null)
        {
            _vfxController.FreezeFormalStimulus();
        }

        SetPhase(aborted ? WaterLiliesExperimentPhase.Aborted : WaterLiliesExperimentPhase.Finished, 0.0);

        if (aborted)
        {
            LogEvent(EventExperimentAborted, "Experiment aborted by operator.");
        }

        LogEvent(EventExperimentEnd, aborted ? "Experiment ended after abort." : "Experiment completed.");
        _sessionClosing = true;
        yield return StopVideoRecordingRoutine(aborted ? "experiment_aborted" : "experiment_completed");
        _logger.CloseSession();
        _sessionClosing = false;
        _runRoutine = null;
        yield break;
    }

    bool PrepareFixedPainting()
    {
        if (_targetPainting == null)
        {
            _targetPainting = FindSceneObjectByName(_config.targetPaintingObjectName);
        }

        if (_targetPainting == null)
        {
            Debug.LogWarning("[WaterLiliesExperimentManager] Could not find target painting object: " + _config.targetPaintingObjectName, this);
            return false;
        }

        if (_config.disableLegacyPaintingControllers)
        {
            DisableLegacyControllers(_targetPainting);
        }

        SetActiveSelfAndParents(_targetPainting.transform);
        if (_config.isolateTargetPainting)
        {
            IsolateAmongSiblings(_targetPainting.transform);
        }

        PlacePaintingInFrontOfViewer();

        _vfxController = _targetPainting.GetComponent<WaterLiliesVfxController>();
        if (_vfxController == null)
        {
            _vfxController = _targetPainting.AddComponent<WaterLiliesVfxController>();
        }

        var visualEffect = _targetPainting.GetComponent<VisualEffect>();
        var legacyController = _targetPainting.GetComponent<MonaLisaVfxController>();
        if (visualEffect == null)
        {
            Debug.LogWarning("[WaterLiliesExperimentManager] Target painting does not have a VisualEffect component: " + _targetPainting.name, this);
            return false;
        }

        _vfxController.Bind(visualEffect, legacyController);

        if (_trackingSampler != null)
        {
            _trackingSampler.BindPainting(_targetPainting);
        }

        return true;
    }

    void DisableLegacyControllers(GameObject target)
    {
        var parentRotationControllers = target.GetComponentsInParent<PaintingRotationController>(true);
        for (var i = 0; i < parentRotationControllers.Length; i++)
        {
            parentRotationControllers[i].enabled = false;
        }

        var parentGroupControllers = target.GetComponentsInParent<VfxAutoAnimatorGroupController>(true);
        for (var i = 0; i < parentGroupControllers.Length; i++)
        {
            parentGroupControllers[i].enabled = false;
        }

        var targetAutoAnimators = target.GetComponents<StarryNightRhoneVfxAutoAnimator>();
        for (var i = 0; i < targetAutoAnimators.Length; i++)
        {
            targetAutoAnimators[i].enabled = false;
        }
    }

    bool ValidateConditionPlan(out string error)
    {
        error = string.Empty;
        if (_orderedConditions.Count == 0)
        {
            error = "No valid conditions are configured.";
            return false;
        }

        if (_config == null || _config.mode != WaterLiliesExperimentMode.Formal)
        {
            return true;
        }

        if (_orderedConditions.Count != 9)
        {
            error = "Formal mode requires exactly 9 configured condition entries; found " + _orderedConditions.Count + ".";
            return false;
        }

        if (!_config.requireManualQuestionnaireContinue)
        {
            error = "Formal mode requires manual questionnaire continue so Google Form time cannot be merged into stimulus exposure.";
            return false;
        }

        if (!_config.requireHeadsetCycleBeforeQuestionnaireContinue)
        {
            error = "Formal mode requires headset_removed and headset_worn markers during each questionnaire break.";
            return false;
        }

        if (!_config.writeCsv && !_config.writeJsonLines)
        {
            error = "Formal mode requires at least one log output format enabled.";
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < _orderedConditions.Count; i++)
        {
            if (!seen.Add(_orderedConditions[i].conditionId))
            {
                error = "Formal mode condition order contains duplicate condition ID: " + _orderedConditions[i].conditionId + ".";
                return false;
            }
        }

        for (var i = 1; i <= 9; i++)
        {
            var expected = "C" + i;
            if (!seen.Contains(expected))
            {
                error = "Formal mode condition order is missing " + expected + ".";
                return false;
            }
        }

        return true;
    }

    void SetActiveSelfAndParents(Transform target)
    {
        var current = target;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            current = current.parent;
        }
    }

    void IsolateAmongSiblings(Transform target)
    {
        var parent = target.parent;
        if (parent == null)
        {
            return;
        }

        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            child.gameObject.SetActive(child == target);
        }
    }

    void PlacePaintingInFrontOfViewer()
    {
        if (!_placePaintingInFrontOfViewer || _targetPainting == null)
        {
            return;
        }

        var viewer = ResolveViewerTransform();
        if (viewer == null)
        {
            Debug.LogWarning("[WaterLiliesExperimentManager] Could not find MainCamera or CenterEyeAnchor for runtime painting placement.", this);
            return;
        }

        var forward = Vector3.ProjectOnPlane(viewer.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        var targetTransform = _targetPainting.transform;
        targetTransform.position = viewer.position + forward * Mathf.Max(0.5f, _paintingDistanceMeters) + Vector3.up * _paintingVerticalOffsetMeters;
        if (_alignPaintingYawWithViewer)
        {
            targetTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }

    static Transform ResolveViewerTransform()
    {
        var mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        var centerEye = FindSceneObjectByName("CenterEyeAnchor");
        return centerEye != null ? centerEye.transform : null;
    }

    void ResolveConfig()
    {
        if (_config != null)
        {
            return;
        }

        _config = Resources.Load<WaterLiliesExperimentConfig>(ResourceConfigName);
        if (_config == null)
        {
            _config = ScriptableObject.CreateInstance<WaterLiliesExperimentConfig>();
            Debug.LogWarning("[WaterLiliesExperimentManager] Using runtime default config because no Resources/WaterLiliesExperimentConfig asset was found.", this);
        }
    }

    void ResolveLocalComponents()
    {
        if (_logger == null)
        {
            _logger = GetComponent<WaterLiliesExperimentLogger>();
        }

        if (_logger == null)
        {
            _logger = gameObject.AddComponent<WaterLiliesExperimentLogger>();
        }

        if (_trackingSampler == null)
        {
            _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        }

        if (_trackingSampler == null)
        {
            _trackingSampler = gameObject.AddComponent<WaterLiliesTrackingSampler>();
        }

        if (_lslMarkerOutlet == null)
        {
            _lslMarkerOutlet = GetComponent<WaterLiliesLslMarkerOutlet>();
        }

        if (_lslMarkerOutlet == null && _autoCreateLslMarkerOutlet)
        {
            _lslMarkerOutlet = gameObject.AddComponent<WaterLiliesLslMarkerOutlet>();
        }

        ResolveVideoRecorder();
    }

    void ResolveVideoRecorder()
    {
        if (!_recordVideo || _videoRecorder != null)
        {
            return;
        }

        _videoRecorder = FindObjectOfType<ExperimentVideoRecorder>();
        if (_videoRecorder == null && _autoCreateVideoRecorder)
        {
            _videoRecorder = ExperimentVideoRecorder.Instance;
        }
    }

    string ResolveDataRootOverride()
    {
        return _overrideConfigLogRoot && !string.IsNullOrWhiteSpace(_dataRootPath)
            ? _dataRootPath.Trim()
            : string.Empty;
    }

    void StartVideoRecording(string reason)
    {
        if (!_recordVideo || _logger == null || !_logger.sessionActive)
        {
            return;
        }

        ResolveVideoRecorder();
        if (_videoRecorder == null)
        {
            LogEvent(EventVideoRecordingFailed, "reason=no_recorder; source=" + reason);
            return;
        }

        if (_videoRecorder.StartRecording(_logger, reason))
        {
            LogEvent(EventVideoRecordingStart, BuildVideoRecordingNotes(reason));
            return;
        }

        LogEvent(EventVideoRecordingFailed, "reason=start_failed; source=" + reason);
    }

    void StopVideoRecording(string reason)
    {
        if (!_recordVideo || _videoRecorder == null || !_videoRecorder.isRecording)
        {
            return;
        }

        _videoRecorder.StopRecording(reason);
        LogEvent(EventVideoRecordingStop, BuildVideoRecordingNotes(reason));
    }

    IEnumerator StopVideoRecordingRoutine(string reason)
    {
        StopVideoRecording(reason);
        if (_videoRecorder == null || _videoRecorderFlushTimeoutSeconds <= 0f)
        {
            yield break;
        }

        var deadline = Time.realtimeSinceStartupAsDouble + _videoRecorderFlushTimeoutSeconds;
        while (_videoRecorder.isFinalizingRecording && Time.realtimeSinceStartupAsDouble < deadline)
        {
            yield return null;
        }
    }

    string BuildVideoRecordingNotes(string reason)
    {
        var builder = new StringBuilder();
        builder.Append("reason=").Append(reason);
        if (_videoRecorder != null)
        {
            builder
                .Append("; frames=").Append(_videoRecorder.recordedFrameCount)
                .Append("; dropped=").Append(_videoRecorder.droppedFrameCount)
                .Append("; folder=").Append(_videoRecorder.framesFolderPath);
        }

        return builder.ToString();
    }

    void PrepareFixedPaintingPreview()
    {
        if (!_prepareFixedPaintingOnPlay)
        {
            return;
        }

        if (PrepareFixedPainting() && _vfxController != null)
        {
            _vfxController.ApplyBaseline(_config);
        }
    }

    void HandleKeyboard()
    {
        if (Input.GetKeyDown(_startKey) && !isRunning)
        {
            StartExperiment();
        }

        if (Input.GetKeyDown(_continueKey))
        {
            ContinueCurrentBreak();
        }

        if (Input.GetKeyDown(_headsetRemovedKey))
        {
            MarkHeadsetRemoved("manual_key");
        }

        if (Input.GetKeyDown(_headsetWornKey))
        {
            MarkHeadsetWorn("manual_key");
        }

        if (Input.GetKeyDown(_pilotSkipKey))
        {
            RequestPilotSkip();
        }

        if (Input.GetKeyDown(_abortKey))
        {
            AbortExperiment();
        }
    }

    void UpdateHeadsetPresenceMarkers()
    {
        if (_sessionClosing || _config == null || !_config.autoDetectHeadsetPresence || _trackingSampler == null || !_logger.sessionActive)
        {
            return;
        }

        if (!_trackingSampler.TryGetHeadsetPresence(out var userPresent))
        {
            return;
        }

        if (!_headsetPresenceKnown)
        {
            _headsetPresenceKnown = true;
            _lastHeadsetUserPresent = userPresent;
            return;
        }

        if (_lastHeadsetUserPresent == userPresent)
        {
            return;
        }

        _lastHeadsetUserPresent = userPresent;
        if (userPresent)
        {
            MarkHeadsetWorn("auto_user_presence");
        }
        else
        {
            MarkHeadsetRemoved("auto_user_presence");
        }
    }

    void MarkHeadsetRemoved(string source)
    {
        if (_headsetCurrentlyOff)
        {
            return;
        }

        _headsetCurrentlyOff = true;
        _headsetRemovedStartedRealtime = Time.realtimeSinceStartupAsDouble;
        if (_phase == WaterLiliesExperimentPhase.QuestionnaireBreak)
        {
            _questionnaireHeadsetRemovedRecorded = true;
        }

        LogEvent(EventHeadsetRemoved, "source=" + source);
    }

    void MarkHeadsetWorn(string source)
    {
        var intervalSeconds = double.NaN;
        if (_headsetCurrentlyOff && !double.IsNaN(_headsetRemovedStartedRealtime))
        {
            intervalSeconds = Time.realtimeSinceStartupAsDouble - _headsetRemovedStartedRealtime;
        }

        _headsetCurrentlyOff = false;
        _headsetRemovedStartedRealtime = double.NaN;
        if (_phase == WaterLiliesExperimentPhase.QuestionnaireBreak)
        {
            _questionnaireHeadsetWornRecorded = true;
        }

        LogEvent(EventHeadsetWorn, "source=" + source, intervalSeconds);
    }

    public void RequestPilotSkip()
    {
        if (!CanPilotSkip())
        {
            Debug.Log("[WaterLiliesExperimentManager] Skip ignored because pilot skip is disabled outside Pilot mode.", this);
            return;
        }

        _pilotSkipRequested = true;
    }

    void LogTrackingSampleIfDue()
    {
        if (_sessionClosing || _config == null || _logger == null || !_logger.sessionActive || _trackingSampler == null)
        {
            return;
        }

        var now = Time.realtimeSinceStartupAsDouble;
        if (now < _nextSampleRealtime)
        {
            return;
        }

        _nextSampleRealtime = now + _config.sampleIntervalSeconds;
        var row = CreateLogRow("tracking_sample", string.Empty);
        PopulateTracking(row, _trackingSampler.Capture());
        _logger.LogSample(row);
    }

    void LogEvent(string eventType, string notes = "", double intervalSeconds = double.NaN)
    {
        if (_logger == null || !_logger.sessionActive)
        {
            return;
        }

        var row = CreateLogRow(eventType, notes);
        row.headset_off_interval_seconds = intervalSeconds;
        if (_trackingSampler != null)
        {
            PopulateTracking(row, _trackingSampler.Capture());
        }

        _logger.LogEvent(row);
        if (_lslMarkerOutlet != null)
        {
            _lslMarkerOutlet.PushEvent(row);
        }
    }

    WaterLiliesExperimentLogRow CreateLogRow(string eventType, string notes)
    {
        var row = new WaterLiliesExperimentLogRow
        {
            event_type = eventType,
            participant_id = _config != null ? _config.participantId : "P001",
            experiment_mode = _config != null ? _config.mode.ToString() : string.Empty,
            phase = _phase.ToString(),
            condition_order_index = _currentConditionIndex >= 0 ? _currentConditionIndex + 1 : -1,
            condition_order_total = _orderedConditions.Count > 0 ? _orderedConditions.Count : -1,
            formal_viewing = _formalViewingActive,
            planned_duration_seconds = _phasePlannedDurationSeconds,
            notes = notes
        };

        var phaseElapsed = Time.realtimeSinceStartupAsDouble - _phaseStartedRealtime;
        row.phase_elapsed_seconds = phaseElapsed;
        if (!double.IsNaN(_phasePlannedDurationSeconds) && _phasePlannedDurationSeconds > 0.0)
        {
            row.phase_remaining_seconds = Math.Max(0.0, _phasePlannedDurationSeconds - phaseElapsed);
        }

        if (_vfxController != null)
        {
            row.applied_intensity_value = _vfxController.currentIntensity;
            row.applied_frequency_value = _vfxController.currentFrequency;
            row.natural_modulation_enabled = _vfxController.naturalTextureModulationEnabled;
            row.natural_modulation_seed = _vfxController.naturalTextureSeed;
            row.natural_modulation_template_elapsed_seconds = _vfxController.naturalTextureTemplateElapsedSeconds;
            row.natural_modulation_intensity_depth = _vfxController.naturalTextureIntensityDepth;
            row.natural_modulation_frequency_depth = _vfxController.naturalTextureFrequencyDepth;
            row.natural_modulation_large_scale_seconds = _vfxController.naturalTextureLargeScaleSeconds;
            row.natural_modulation_medium_scale_seconds = _vfxController.naturalTextureMediumScaleSeconds;
            row.natural_modulation_fine_scale_seconds = _vfxController.naturalTextureFineScaleSeconds;
        }

        if (_hasCurrentCondition)
        {
            row.condition_id = _currentCondition.conditionId;
            row.intensity_level = _currentCondition.intensityLevel.ToString();
            row.frequency_level = _currentCondition.frequencyLevel.ToString();
            row.intensity_value = _currentCondition.intensityValue;
            row.frequency_value = _currentCondition.frequencyValue;
            if (_phase == WaterLiliesExperimentPhase.ConditionViewing)
            {
                row.planned_duration_seconds = _currentCondition.durationSeconds;
            }
        }

        return row;
    }

    static void PopulateTracking(WaterLiliesExperimentLogRow row, WaterLiliesTrackingSample sample)
    {
        row.headset_presence_available = sample.headsetPresenceAvailable;
        row.headset_user_present = sample.headsetUserPresent;
        row.head_pose_available = sample.headPoseAvailable;
        row.head_position_x = sample.headPosition.x;
        row.head_position_y = sample.headPosition.y;
        row.head_position_z = sample.headPosition.z;
        row.head_rotation_x = sample.headRotation.x;
        row.head_rotation_y = sample.headRotation.y;
        row.head_rotation_z = sample.headRotation.z;
        row.head_rotation_w = sample.headRotation.w;
        row.head_velocity_x = sample.headVelocity.x;
        row.head_velocity_y = sample.headVelocity.y;
        row.head_velocity_z = sample.headVelocity.z;
        row.head_angular_velocity_deg_s = sample.headAngularVelocityDegPerSecond;
        row.gaze_available = sample.gazeAvailable;
        row.gaze_origin_x = sample.gazeOrigin.x;
        row.gaze_origin_y = sample.gazeOrigin.y;
        row.gaze_origin_z = sample.gazeOrigin.z;
        row.gaze_direction_x = sample.gazeDirection.x;
        row.gaze_direction_y = sample.gazeDirection.y;
        row.gaze_direction_z = sample.gazeDirection.z;
        row.gaze_hit = sample.gazeHit;
        row.gaze_hit_x = sample.gazeHitPoint.x;
        row.gaze_hit_y = sample.gazeHitPoint.y;
        row.gaze_hit_z = sample.gazeHitPoint.z;
        row.gaze_on_painting = sample.gazeOnPainting;
    }

    void SetPhase(WaterLiliesExperimentPhase nextPhase, double plannedDurationSeconds)
    {
        _phase = nextPhase;
        _phaseStartedRealtime = Time.realtimeSinceStartupAsDouble;
        _phasePlannedDurationSeconds = plannedDurationSeconds > 0.0 ? plannedDurationSeconds : double.NaN;
    }

    bool CanPilotSkip()
    {
        return _config != null &&
               _config.mode == WaterLiliesExperimentMode.Pilot &&
               _config.allowPilotSkipShortcut;
    }

    void EnsureRuntimeUi()
    {
    }

    string BuildStatusText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Participant: " + (_config != null ? _config.participantId : "P001"));
        builder.AppendLine("Mode: " + (_config != null ? _config.mode.ToString() : "Unknown"));
        builder.AppendLine("Current VFX intensity: " + operatorCurrentIntensityText);
        builder.AppendLine("Current VFX frequency: " + operatorCurrentFrequencyText);

        if (_hasCurrentCondition)
        {
            builder.AppendLine("Condition: " + _currentCondition.conditionId + " (" + (_currentConditionIndex + 1) + "/" + _orderedConditions.Count + ")");
            builder.AppendLine("Condition target intensity: " + _currentCondition.intensityLevel + " = " + _currentCondition.intensityValue.ToString("0.###"));
            builder.AppendLine("Condition target frequency: " + _currentCondition.frequencyLevel + " = " + _currentCondition.frequencyValue.ToString("0.###"));
            if (_phase == WaterLiliesExperimentPhase.QuestionnaireBreak)
            {
                builder.AppendLine(_currentCondition.conditionId + " completed. Please complete Google Form for " + _currentCondition.conditionId + ".");
                if (_config != null && _config.requireHeadsetCycleBeforeQuestionnaireContinue)
                {
                    if (!_questionnaireHeadsetRemovedRecorded)
                    {
                        builder.AppendLine("Waiting for headset_removed marker.");
                    }
                    else if (!_questionnaireHeadsetWornRecorded || _headsetCurrentlyOff)
                    {
                        builder.AppendLine("Waiting for headset_worn marker.");
                    }
                }
                else if (_headsetCurrentlyOff && _config != null && _config.requireHeadsetWornBeforeQuestionnaireContinue)
                {
                    builder.AppendLine("Waiting for headset_worn before continue.");
                }
            }
            else if (_phase == WaterLiliesExperimentPhase.RecenterStabilization)
            {
                builder.AppendLine("Re-wear stabilization before " + _currentCondition.conditionId + ".");
            }
        }

        var elapsed = Time.realtimeSinceStartupAsDouble - _phaseStartedRealtime;
        builder.AppendLine("Elapsed: " + FormatWholeSeconds(elapsed));
        if (!double.IsNaN(_phasePlannedDurationSeconds) && _phasePlannedDurationSeconds > 0.0)
        {
            builder.AppendLine("Remaining: " + FormatWholeSeconds(Math.Max(0.0, _phasePlannedDurationSeconds - elapsed)));
        }

        builder.AppendLine("Formal viewing: " + (_formalViewingActive ? "YES" : "NO"));
        builder.AppendLine("Headset off: " + (_headsetCurrentlyOff ? "YES" : "NO"));
        builder.AppendLine("Keys: S start | Space continue | H removed | J worn | K skip pilot | Esc abort");
        return builder.ToString();
    }

    string BuildPhaseBadgeText()
    {
        var text = "CURRENT PHASE: " + GetPhaseDisplayName(_phase);
        if (_hasCurrentCondition && IsConditionRelatedPhase(_phase))
        {
            text += "  |  " + _currentCondition.conditionId + " (" + (_currentConditionIndex + 1) + "/" + _orderedConditions.Count + ")";
        }

        return text;
    }

    string BuildNextActionText()
    {
        return "NEXT ACTION: " + GetNextActionForPhase();
    }

    string GetNextActionForPhase()
    {
        switch (_phase)
        {
            case WaterLiliesExperimentPhase.Idle:
                return "Confirm Quest Link and recording, then press Start or S.";
            case WaterLiliesExperimentPhase.Baseline:
                return "Let the participant watch the baseline; next is adaptation.";
            case WaterLiliesExperimentPhase.Adaptation:
                return "Keep the participant watching; next is the first condition setup.";
            case WaterLiliesExperimentPhase.ConditionPrepare:
                return "Prepare " + CurrentConditionLabel() + "; next is re-wear stabilization.";
            case WaterLiliesExperimentPhase.RecenterStabilization:
                return "Keep the headset worn and stable; next is " + CurrentConditionLabel() + " viewing.";
            case WaterLiliesExperimentPhase.ConditionViewing:
                return "Let the participant watch; next they remove the headset and complete the form.";
            case WaterLiliesExperimentPhase.QuestionnaireBreak:
                return BuildQuestionnaireNextAction();
            case WaterLiliesExperimentPhase.Rest:
                return "Rest in progress; next is the following condition.";
            case WaterLiliesExperimentPhase.Finished:
                return "Experiment finished; verify logs and recording files.";
            case WaterLiliesExperimentPhase.Aborted:
                return "Experiment aborted; check logs before restarting.";
            default:
                return "Monitor the operator panel.";
        }
    }

    string BuildQuestionnaireNextAction()
    {
        if (_config != null && _config.requireHeadsetCycleBeforeQuestionnaireContinue)
        {
            if (!_questionnaireHeadsetRemovedRecorded)
            {
                return "Remove headset and complete the form for " + CurrentConditionLabel() + ".";
            }

            if (!_questionnaireHeadsetWornRecorded || _headsetCurrentlyOff)
            {
                return "After the form, wear the headset again and wait for headset_worn.";
            }
        }
        else if (_headsetCurrentlyOff && _config != null && _config.requireHeadsetWornBeforeQuestionnaireContinue)
        {
            return "Wear the headset again, then press Continue or Space.";
        }

        return "When the form is complete, press Continue or Space.";
    }

    string CurrentConditionLabel()
    {
        return _hasCurrentCondition ? _currentCondition.conditionId : "the current condition";
    }

    static bool IsConditionRelatedPhase(WaterLiliesExperimentPhase phase)
    {
        return phase == WaterLiliesExperimentPhase.ConditionPrepare ||
               phase == WaterLiliesExperimentPhase.RecenterStabilization ||
               phase == WaterLiliesExperimentPhase.ConditionViewing ||
               phase == WaterLiliesExperimentPhase.QuestionnaireBreak;
    }

    static string FormatCurrentVfxParameter(float value)
    {
        return float.IsNaN(value) ? "not bound" : value.ToString("0.###");
    }

    static string GetPhaseDisplayName(WaterLiliesExperimentPhase phase)
    {
        switch (phase)
        {
            case WaterLiliesExperimentPhase.Idle:
                return "Idle";
            case WaterLiliesExperimentPhase.Baseline:
                return "Baseline";
            case WaterLiliesExperimentPhase.Adaptation:
                return "Adaptation";
            case WaterLiliesExperimentPhase.ConditionPrepare:
                return "Condition Prepare";
            case WaterLiliesExperimentPhase.RecenterStabilization:
                return "Re-wear Stabilization";
            case WaterLiliesExperimentPhase.ConditionViewing:
                return "Condition Viewing";
            case WaterLiliesExperimentPhase.QuestionnaireBreak:
                return "Questionnaire Break";
            case WaterLiliesExperimentPhase.Rest:
                return "Rest";
            case WaterLiliesExperimentPhase.Finished:
                return "Finished";
            case WaterLiliesExperimentPhase.Aborted:
                return "Aborted";
            default:
                return phase.ToString();
        }
    }

    static string FormatWholeSeconds(double seconds)
    {
        return Math.Max(0.0, seconds).ToString("0") + "s";
    }

    static GameObject FindSceneObjectByName(string objectName)
    {
        var transforms = FindObjectsOfType<Transform>(true);
        for (var i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != null && string.Equals(transforms[i].name, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return transforms[i].gameObject;
            }
        }

        return null;
    }
}
