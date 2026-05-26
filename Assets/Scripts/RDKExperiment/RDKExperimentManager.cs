using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RDKExperimentState
{
    Idle,
    Starting,
    Instruction,
    Practice,
    TrialFixation,
    TrialStimulus,
    TrialResponse,
    PracticeFeedback,
    Finished,
    Error
}

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK Experiment Manager")]
public sealed class RDKExperimentManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] RDKExperimentConfig _config;
    [SerializeField] RDKStimulusManager _stimulusManager;
    [SerializeField] RDKVRInputManager _inputManager;
    [SerializeField] RDKDataLogger _dataLogger;
    [SerializeField] Transform _headTransform;

    [Header("UI Hooks")]
    [SerializeField] GameObject _instructionRoot;
    [SerializeField] GameObject _fixationRoot;
    [SerializeField] GameObject _practiceFeedbackRoot;
    [SerializeField] GameObject _finishedRoot;
    [SerializeField] bool _autoCreateRuntimeUi = true;
    [SerializeField, Min(0.5f)] float _uiDistanceMeters = 2f;
    [SerializeField] Vector2 _uiPanelSizeMeters = new Vector2(1.35f, 0.72f);
    [SerializeField] bool _hideNonExperimentCanvases = true;

    [Header("Run")]
    [SerializeField] bool _autoStart;
    [SerializeField] bool _runPractice = true;
    [SerializeField] bool _waitForInputOnInstruction = true;
    [SerializeField, Min(0f)] float _practiceFeedbackSeconds = 0.75f;
    [SerializeField] bool _debugLogging = true;
    [SerializeField] bool _preferXriCameraForPcSimulation = true;

    readonly List<RDKTrialPlan> _trialBuffer = new List<RDKTrialPlan>();
    readonly List<Canvas> _hiddenCanvases = new List<Canvas>();
    Coroutine _runRoutine;
    Text _practiceFeedbackText;
    int _globalTrialIndex;
    RDKExperimentState _state = RDKExperimentState.Idle;

    public bool isRunning => _runRoutine != null;
    public RDKExperimentState state => _state;

    void Reset()
    {
        _stimulusManager = FindObjectOfType<RDKStimulusManager>();
        _inputManager = FindObjectOfType<RDKVRInputManager>();
        _dataLogger = FindObjectOfType<RDKDataLogger>();
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            _headTransform = mainCamera.transform;
        }
    }

    void Awake()
    {
        ResolveReferences();
        EnsureEventSystem();
        EnsureRuntimeUi();
        RunStartupDiagnostics();
        SetOptionalRoot(_fixationRoot, false);
        SetOptionalRoot(_practiceFeedbackRoot, false);
        SetOptionalRoot(_finishedRoot, false);
    }

    void Start()
    {
        if (_autoStart)
        {
            StartExperiment();
        }
    }

    void Update()
    {
        if (_inputManager == null)
        {
            return;
        }

        if (_inputManager.TryConsumeReset(out RDKInputSample resetSample))
        {
            DebugLog($"Reset/restart requested by {resetSample.buttonName} ({resetSample.deviceName}).");
            StartExperiment();
            return;
        }

        if (_runRoutine == null && _inputManager.TryConsumeStart(out RDKInputSample startSample))
        {
            DebugLog($"Manual start requested by {startSample.buttonName} ({startSample.deviceName}).");
            StartExperiment();
        }
    }

    [ContextMenu("Start RDK Experiment")]
    public void StartExperiment()
    {
        DebugLog("StartExperiment called.");

        if (_runRoutine != null)
        {
            DebugLog("A previous run is active; cleaning it before restart.");
            CleanupActiveRun();
        }

        _runRoutine = StartCoroutine(RunExperimentRoutine());
    }

    [ContextMenu("Stop RDK Experiment")]
    public void StopExperiment()
    {
        DebugLog("StopExperiment called.");
        CleanupActiveRun();
        SetState(RDKExperimentState.Idle);
    }

    void CleanupActiveRun()
    {
        if (_runRoutine != null)
        {
            StopCoroutine(_runRoutine);
            _runRoutine = null;
        }

        if (_stimulusManager != null)
        {
            _stimulusManager.EndStimulus();
        }

        SetOptionalRoot(_fixationRoot, false);
        SetOptionalRoot(_instructionRoot, false);
        SetOptionalRoot(_practiceFeedbackRoot, false);
        _dataLogger?.CloseSession();
        RestoreHiddenCanvases();
    }

    IEnumerator RunExperimentRoutine()
    {
        SetState(RDKExperimentState.Starting);
        ResolveReferences();

        if (!ValidateReferences())
        {
            SetState(RDKExperimentState.Error);
            _runRoutine = null;
            yield break;
        }

        RenderSettings.skybox = RenderSettings.skybox;
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.backgroundColor = _config.backgroundColor;
        }

        _globalTrialIndex = 0;
        _dataLogger.StartSession();
        _stimulusManager.Configure(_config);
        _stimulusManager.SetViewerTransform(_headTransform);
        EnsureRuntimeUi();
        HideNonExperimentCanvases();
        DebugLog($"Experiment session started. WaitingOnInstruction={_waitForInputOnInstruction}, practice={_runPractice}.");

        yield return ShowInstructionRoutine();

        if (_runPractice)
        {
            SetState(RDKExperimentState.Practice);
            _config.BuildTrialSequence(_trialBuffer, true);
            yield return RunTrialListRoutine(_trialBuffer, true);
        }

        _config.BuildTrialSequence(_trialBuffer, false);
        yield return RunTrialListRoutine(_trialBuffer, false);

        SetOptionalRoot(_finishedRoot, true);
        _dataLogger.CloseSession();
        RestoreHiddenCanvases();
        SetState(RDKExperimentState.Finished);
        DebugLog("Experiment session finished.");
        _runRoutine = null;
    }

    IEnumerator ShowInstructionRoutine()
    {
        SetState(RDKExperimentState.Instruction);
        PositionUiInFrontOfHead(_instructionRoot);
        SetOptionalRoot(_instructionRoot, true);

        if (_instructionRoot != null && _waitForInputOnInstruction)
        {
            _inputManager.ResetButtonState();
            DebugLog("Instruction screen is waiting for start input: Space/Trigger, Enter/Primary, E/G Grip, B, N, mouse left, or P.");
            while (true)
            {
                if (_inputManager.TryConsumeStart(out RDKInputSample startSample))
                {
                    DebugLog($"Instruction start received from {startSample.buttonName} ({startSample.deviceName}).");
                    break;
                }

                yield return null;
            }
        }

        SetOptionalRoot(_instructionRoot, false);
    }

    IEnumerator RunTrialListRoutine(List<RDKTrialPlan> trials, bool practice)
    {
        for (int i = 0; i < trials.Count; i++)
        {
            _globalTrialIndex++;
            yield return RunTrialRoutine(trials[i], practice);
        }
    }

    IEnumerator RunTrialRoutine(RDKTrialPlan trial, bool practice)
    {
        DebugLog($"Trial {_globalTrialIndex}: {trial.conditionName}, direction={trial.motionDirection}, coherence={trial.motionCoherence:0.###}.");

        bool earlyResponse = false;
        RDKInputSample earlySample = default;
        _stimulusManager.PrepareTrial(trial);
        _inputManager.ResetButtonState();

        SetState(RDKExperimentState.TrialFixation);
        PositionUiInFrontOfHead(_fixationRoot);
        SetOptionalRoot(_fixationRoot, true);
        float fixationStart = Time.time;
        while (Time.time - fixationStart < _config.fixationSeconds)
        {
            if (_inputManager.TryConsumeResponse(out RDKInputSample sample))
            {
                earlyResponse = true;
                earlySample = sample;
            }

            yield return null;
        }

        SetOptionalRoot(_fixationRoot, false);
        _inputManager.ResetButtonState();

        double stimulusStart = Time.realtimeSinceStartupAsDouble;
        _stimulusManager.BeginStimulus();
        SetState(RDKExperimentState.TrialStimulus);

        bool stimulusEnded = false;
        bool responded = false;
        RDKInputSample responseSample = default;
        double stimulusEnd = double.NaN;
        float responseDeadline = Time.time + _config.responseWindowSeconds;
        float stimulusEndTime = Time.time + _config.stimulusSeconds;

        while (Time.time < responseDeadline)
        {
            if (!stimulusEnded && Time.time >= stimulusEndTime)
            {
                _stimulusManager.EndStimulus();
                stimulusEnded = true;
                stimulusEnd = Time.realtimeSinceStartupAsDouble;
                SetState(RDKExperimentState.TrialResponse);
            }

            if (_inputManager.TryConsumeResponse(out responseSample))
            {
                responded = true;
                break;
            }

            yield return null;
        }

        if (!stimulusEnded)
        {
            _stimulusManager.EndStimulus();
            stimulusEnd = Time.realtimeSinceStartupAsDouble;
        }

        bool timeout = !responded;
        RDKResponseDirection response = responded ? responseSample.response : RDKResponseDirection.None;
        bool correct = !timeout && !earlyResponse && IsCorrect(trial.motionDirection, response);

        if (earlyResponse && !responded)
        {
            responseSample = earlySample;
        }

        LogTrial(trial, practice, stimulusStart, stimulusEnd, responseSample, earlySample, responded, correct, timeout, earlyResponse);
        DebugLog($"Trial {_globalTrialIndex} ended. responded={responded}, correct={correct}, timeout={timeout}, early={earlyResponse}.");

        if (practice)
        {
            SetPracticeFeedback(correct, timeout, earlyResponse);
            PositionUiInFrontOfHead(_practiceFeedbackRoot);
            SetOptionalRoot(_practiceFeedbackRoot, true);
            SetState(RDKExperimentState.PracticeFeedback);
            yield return new WaitForSeconds(_practiceFeedbackSeconds);
            SetOptionalRoot(_practiceFeedbackRoot, false);
        }

        if (_config.interTrialIntervalSeconds > 0f)
        {
            yield return new WaitForSeconds(_config.interTrialIntervalSeconds);
        }
    }

    void LogTrial(
        RDKTrialPlan trial,
        bool practice,
        double stimulusStart,
        double stimulusEnd,
        RDKInputSample responseSample,
        RDKInputSample earlySample,
        bool responded,
        bool correct,
        bool timeout,
        bool earlyResponse)
    {
        Transform head = _headTransform;
        Vector3 headPosition = head != null ? head.position : Vector3.zero;
        Vector3 headForward = head != null ? head.forward : Vector3.forward;
        Quaternion headRotation = head != null ? head.rotation : Quaternion.identity;

        _dataLogger.LogTrial(new RDKTrialRecord
        {
            trialIndex = _globalTrialIndex,
            blockIndex = trial.blockIndex,
            trialIndexInBlock = trial.trialIndexInBlock,
            phase = practice ? "practice" : "formal",
            conditionType = trial.conditionType,
            conditionName = trial.conditionName,
            trueMotionDirection = trial.motionDirection.ToString(),
            motionCoherence = trial.motionCoherence,
            coherentDotCount = _stimulusManager.coherentDotCount,
            randomDotCount = _stimulusManager.randomDotCount,
            stimulusStartRealtime = stimulusStart,
            stimulusEndRealtime = stimulusEnd,
            responseButton = responded || earlyResponse ? responseSample.buttonName : string.Empty,
            responseDevice = responded || earlyResponse ? responseSample.deviceName : string.Empty,
            responseDirection = responded ? responseSample.response.ToString() : RDKResponseDirection.None.ToString(),
            responseRealtime = responded || earlyResponse ? responseSample.realtime : double.NaN,
            reactionTime = responded ? responseSample.realtime - stimulusStart : double.NaN,
            correct = correct,
            timeout = timeout,
            earlyResponse = earlyResponse,
            earlyResponseButton = earlyResponse ? earlySample.buttonName : string.Empty,
            earlyResponseDevice = earlyResponse ? earlySample.deviceName : string.Empty,
            earlyResponseRealtime = earlyResponse ? earlySample.realtime : double.NaN,
            headPosition = headPosition,
            headForward = headForward,
            headRotation = headRotation
        });
    }

    void ResolveReferences()
    {
        if (_stimulusManager == null)
        {
            _stimulusManager = FindObjectOfType<RDKStimulusManager>();
        }

        if (_inputManager == null)
        {
            _inputManager = FindObjectOfType<RDKVRInputManager>();
        }

        if (_dataLogger == null)
        {
            _dataLogger = FindObjectOfType<RDKDataLogger>();
        }

        if (_headTransform == null && Camera.main != null)
        {
            _headTransform = Camera.main.transform;
        }

        if (_preferXriCameraForPcSimulation)
        {
            Transform preferredHead = FindPreferredHeadTransform();
            if (preferredHead != null && _headTransform != preferredHead)
            {
                DebugLog($"Head transform switched to PC/XRI camera: {preferredHead.name}.");
                _headTransform = preferredHead;
            }
        }

        if (_stimulusManager != null && _headTransform != null)
        {
            _stimulusManager.SetViewerTransform(_headTransform);
        }
    }

    void EnsureRuntimeUi()
    {
        if (!_autoCreateRuntimeUi)
        {
            return;
        }

        if (_instructionRoot == null)
        {
            _instructionRoot = CreateTextPanel(
                "RDK Instructions",
                "Random dot motion task\n\nStart: Space/Trigger, Enter/Primary, E/G Grip, mouse click, B, N, or P\nReset: R or V\nLeft: A or Left Arrow / X\nRight: D, Right Arrow, Enter, or B / A\nRandom: W, Down Arrow, or N / B or Y",
                28);
        }

        if (_fixationRoot == null)
        {
            _fixationRoot = CreateFixationRoot();
        }

        if (_practiceFeedbackRoot == null)
        {
            _practiceFeedbackRoot = CreateTextPanel("RDK Practice Feedback", string.Empty, 34);
            _practiceFeedbackText = _practiceFeedbackRoot.GetComponentInChildren<Text>(true);
        }
        else if (_practiceFeedbackText == null)
        {
            _practiceFeedbackText = _practiceFeedbackRoot.GetComponentInChildren<Text>(true);
        }

        if (_finishedRoot == null)
        {
            _finishedRoot = CreateTextPanel(
                "RDK Finished",
                "Experiment complete.\n\nCSV has been saved automatically.",
                30);
        }
    }

    GameObject CreateTextPanel(string name, string message, int fontSize)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 120f;
        root.AddComponent<GraphicRaycaster>();

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = _uiPanelSizeMeters;

        GameObject background = new GameObject("Background");
        background.transform.SetParent(root.transform, false);
        Image image = background.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.72f);
        RectTransform backgroundRect = image.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        GameObject label = new GameObject("Text");
        label.transform.SetParent(root.transform, false);
        Text text = label.AddComponent<Text>();
        text.text = message;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(0.08f, 0.08f);
        textRect.offsetMax = new Vector2(-0.08f, -0.08f);

        root.SetActive(false);
        return root;
    }

    GameObject CreateFixationRoot()
    {
        GameObject root = new GameObject("RDK Fixation");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0.18f, 0.18f);

        GameObject label = new GameObject("Fixation Cross");
        label.transform.SetParent(root.transform, false);
        Text text = label.AddComponent<Text>();
        text.text = "+";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 72;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        root.SetActive(false);
        return root;
    }

    void PositionUiInFrontOfHead(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        Transform head = _headTransform != null ? _headTransform : Camera.main != null ? Camera.main.transform : null;
        if (head == null)
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = head.forward;
        }

        forward.Normalize();
        root.transform.position = head.position + forward * _uiDistanceMeters;
        root.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void SetPracticeFeedback(bool correct, bool timeout, bool earlyResponse)
    {
        if (_practiceFeedbackText == null)
        {
            return;
        }

        if (earlyResponse)
        {
            _practiceFeedbackText.text = "Too early";
        }
        else if (timeout)
        {
            _practiceFeedbackText.text = "No response";
        }
        else
        {
            _practiceFeedbackText.text = correct ? "Correct" : "Incorrect";
        }
    }

    void HideNonExperimentCanvases()
    {
        if (!_hideNonExperimentCanvases)
        {
            return;
        }

        RestoreHiddenCanvases();
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null ||
                !canvas.gameObject.activeSelf ||
                canvas.transform.IsChildOf(transform))
            {
                continue;
            }

            canvas.gameObject.SetActive(false);
            _hiddenCanvases.Add(canvas);
        }
    }

    void RestoreHiddenCanvases()
    {
        for (int i = 0; i < _hiddenCanvases.Count; i++)
        {
            if (_hiddenCanvases[i] != null)
            {
                _hiddenCanvases[i].gameObject.SetActive(true);
            }
        }

        _hiddenCanvases.Clear();
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject("RDK EventSystem");
        eventSystem.transform.SetParent(transform, false);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
        DebugLog("Created a runtime EventSystem for experiment UI/input compatibility.");
    }

    void RunStartupDiagnostics()
    {
        if (!_debugLogging)
        {
            return;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        int activeCameraCount = 0;
        int activeMainCameraCount = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeCameraCount++;
            if (camera.CompareTag("MainCamera"))
            {
                activeMainCameraCount++;
            }
        }

        if (activeMainCameraCount != 1)
        {
            Debug.LogWarning($"[RDKExperimentManager] Expected exactly one active MainCamera, found {activeMainCameraCount}. The experiment will use explicit head transform {(_headTransform != null ? _headTransform.name : "none")}.", this);
        }

        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        int activeListenerCount = 0;
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
            {
                activeListenerCount++;
            }
        }

        if (activeListenerCount > 1)
        {
            Debug.LogWarning($"[RDKExperimentManager] Found {activeListenerCount} active AudioListeners. Disable old camera rig listeners for clean PC simulation.", this);
        }

        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        GameObject xrInteractionManager = GameObject.Find("XR Interaction Manager");
        DebugLog($"Startup diagnostics: cameras={activeCameraCount}, activeMainCameras={activeMainCameraCount}, eventSystem={(EventSystem.current != null ? EventSystem.current.name : "none")}, xrOrigin={(xrOrigin != null && xrOrigin.activeInHierarchy)}, xrInteractionManager={(xrInteractionManager != null && xrInteractionManager.activeInHierarchy)}.");

        if (_stimulusManager != null && _stimulusManager.stimulusRoot != null)
        {
            int sphereCount = CountNamedChildren(_stimulusManager.stimulusRoot, "Sphere");
            DebugLog($"Stimulus root={_stimulusManager.stimulusRoot.name}, cachedPoints={_stimulusManager.cachedPointCount}, sphereLikeChildren={sphereCount}.");
        }
    }

    Transform FindPreferredHeadTransform()
    {
        GameObject xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin != null)
        {
            Camera[] xrCameras = xrOrigin.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < xrCameras.Length; i++)
            {
                Camera camera = xrCameras[i];
                if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
                {
                    return camera.transform;
                }
            }
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera.transform;
        }

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera != null && camera.enabled && camera.gameObject.activeInHierarchy)
            {
                return camera.transform;
            }
        }

        return null;
    }

    static int CountNamedChildren(Transform root, string prefix)
    {
        if (root == null)
        {
            return 0;
        }

        int count = 0;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i] != root && transforms[i].name.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
            {
                count++;
            }
        }

        return count;
    }

    bool ValidateReferences()
    {
        if (_config == null || _stimulusManager == null || _inputManager == null || _dataLogger == null)
        {
            Debug.LogWarning(
                $"[RDKExperimentManager] Missing reference. config={_config != null}, stimulus={_stimulusManager != null}, input={_inputManager != null}, logger={_dataLogger != null}.",
                this);
            return false;
        }

        DebugLog($"References OK. head={(_headTransform != null ? _headTransform.name : "none")}, camera={(Camera.main != null ? Camera.main.name : "none")}.");
        return true;
    }

    static bool IsCorrect(RDKMotionDirection trueDirection, RDKResponseDirection response)
    {
        if (trueDirection == RDKMotionDirection.Left)
        {
            return response == RDKResponseDirection.Left;
        }

        if (trueDirection == RDKMotionDirection.Right)
        {
            return response == RDKResponseDirection.Right;
        }

        return response == RDKResponseDirection.Random;
    }

    static void SetOptionalRoot(GameObject root, bool active)
    {
        if (root != null)
        {
            root.SetActive(active);
        }
    }

    void DebugLog(string message)
    {
        if (_debugLogging)
        {
            Debug.Log($"[RDKExperimentManager] {message}", this);
        }
    }

    void SetState(RDKExperimentState nextState)
    {
        if (_state == nextState)
        {
            return;
        }

        DebugLog($"State: {_state} -> {nextState}");
        _state = nextState;
    }
}
