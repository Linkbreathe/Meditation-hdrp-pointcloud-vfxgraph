using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

    static readonly string[] RequiredEventMarkersBacking =
    {
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
        EventExperimentEnd
    };

    public static string[] RequiredEventMarkers => (string[])RequiredEventMarkersBacking.Clone();

    [Header("Config")]
    [SerializeField] WaterLiliesExperimentConfig _config;

    [Header("References")]
    [SerializeField] WaterLiliesVfxController _vfxController;
    [SerializeField] WaterLiliesExperimentLogger _logger;
    [SerializeField] WaterLiliesTrackingSampler _trackingSampler;
    [SerializeField] GameObject _targetPainting;

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
    Text _statusText;
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
    bool _headsetPresenceKnown;
    bool _lastHeadsetUserPresent;
    bool _headsetCurrentlyOff;
    bool _questionnaireHeadsetRemovedRecorded;
    bool _questionnaireHeadsetWornRecorded;
    double _headsetRemovedStartedRealtime = double.NaN;
    double _nextSampleRealtime;

    public WaterLiliesExperimentPhase phase => _phase;
    public bool isRunning => _runRoutine != null;

    void Reset()
    {
        _logger = GetComponent<WaterLiliesExperimentLogger>();
        _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
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
        UpdateRuntimeUi();
    }

    void OnDisable()
    {
        if (_logger != null && _logger.sessionActive)
        {
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

        _logger.StartSession(_config);
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
        _logger.CloseSession();
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
        if (_config == null || !_config.autoDetectHeadsetPresence || _trackingSampler == null || !_logger.sessionActive)
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

    void RequestPilotSkip()
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
        if (_config == null || _logger == null || !_logger.sessionActive || _trackingSampler == null)
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
        if (!_autoCreateRuntimeUi || _statusText != null)
        {
            return;
        }

        EnsureEventSystem();

        var canvasObject = new GameObject("Water Lilies Experiment Runtime UI");
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        canvasObject.AddComponent<GraphicRaycaster>();

        var panelObject = new GameObject("Panel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        var panelImage = panelObject.AddComponent<Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.78f);
        var panelRect = panelImage.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = _runtimeUiSize;

        _statusText = CreateText(panelObject.transform, "Status", 18, TextAnchor.UpperLeft);
        var textRect = _statusText.rectTransform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(20f, 86f);
        textRect.offsetMax = new Vector2(-20f, -20f);

        CreateButton(panelObject.transform, "Start", new Vector2(20f, 20f), 84f, StartExperiment);
        CreateButton(panelObject.transform, "Continue", new Vector2(112f, 20f), 112f, ContinueCurrentBreak);
        CreateButton(panelObject.transform, "Removed", new Vector2(232f, 20f), 104f, MarkHeadsetRemoved);
        CreateButton(panelObject.transform, "Worn", new Vector2(344f, 20f), 84f, MarkHeadsetWorn);
        CreateButton(panelObject.transform, "Skip", new Vector2(436f, 20f), 76f, RequestPilotSkip);
    }

    Text CreateText(Transform parent, string name, int fontSize, TextAnchor alignment)
    {
        var textObject = new GameObject(name);
        textObject.transform.SetParent(parent, false);
        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    void CreateButton(Transform parent, string label, Vector2 position, float width, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(label + " Button");
        buttonObject.transform.SetParent(parent, false);
        var image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.15f, 0.18f, 0.22f, 0.95f);
        var button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(action);
        var rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(width, 44f);

        var text = CreateText(buttonObject.transform, "Text", 14, TextAnchor.MiddleCenter);
        text.text = label;
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
    }

    void UpdateRuntimeUi()
    {
        if (_statusText == null)
        {
            return;
        }

        _statusText.text = BuildStatusText();
    }

    string BuildStatusText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Water Lilies VR-VFX Experiment");
        builder.AppendLine("Participant: " + (_config != null ? _config.participantId : "P001"));
        builder.AppendLine("Mode: " + (_config != null ? _config.mode.ToString() : "Unknown"));
        builder.AppendLine("Phase: " + _phase);

        if (_hasCurrentCondition)
        {
            builder.AppendLine("Condition: " + _currentCondition.conditionId + " (" + (_currentConditionIndex + 1) + "/" + _orderedConditions.Count + ")");
            builder.AppendLine("Intensity: " + _currentCondition.intensityLevel + " = " + _currentCondition.intensityValue.ToString("0.###"));
            builder.AppendLine("Frequency: " + _currentCondition.frequencyLevel + " = " + _currentCondition.frequencyValue.ToString("0.###"));
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
        builder.AppendLine("Elapsed: " + elapsed.ToString("0.0") + "s");
        if (!double.IsNaN(_phasePlannedDurationSeconds) && _phasePlannedDurationSeconds > 0.0)
        {
            builder.AppendLine("Remaining: " + Math.Max(0.0, _phasePlannedDurationSeconds - elapsed).ToString("0.0") + "s");
        }

        builder.AppendLine("Formal viewing: " + (_formalViewingActive ? "YES" : "NO"));
        builder.AppendLine("Headset off: " + (_headsetCurrentlyOff ? "YES" : "NO"));
        builder.AppendLine("Keys: S start | Space continue | H removed | J worn | K skip pilot | Esc abort");
        return builder.ToString();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        var eventSystem = new GameObject("Water Lilies Experiment EventSystem");
        eventSystem.transform.SetParent(transform, false);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
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
