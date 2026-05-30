using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

[DefaultExecutionOrder(1200)]
[DisallowMultipleComponent]
[AddComponentMenu("XR/Eye Tracking Data Logger")]
public sealed class EyeTrackingDataLogger : MonoBehaviour
{
    const string RightEyeGazeName = "[BuildingBlock] Eye Gaze Right";
    const string LeftEyeGazeName = "[BuildingBlock] Eye Gaze Left";
    const string CenterEyeAnchorName = "CenterEyeAnchor";
    const string CsvSeparatorDirective = "sep=,";

    [Header("References")]
    [SerializeField] bool _autoFindReferences = true;
    [SerializeField] OVREyeGaze _leftEyeGaze;
    [SerializeField] OVREyeGaze _rightEyeGaze;
    [SerializeField] Transform _leftGazeTransform;
    [SerializeField] Transform _rightGazeTransform;
    [SerializeField] Transform _centerEyeTransform;

    [Header("Startup")]
    [SerializeField] bool _requestEyeTrackingPermission = true;
    [SerializeField] bool _startEyeTrackingIfNeeded = true;
    [SerializeField, Min(0.25f)] float _startRetryInterval = 2f;
    [SerializeField] bool _logRuntimeDiagnosticsOnStartup = true;
    [SerializeField] bool _logRuntimeDiagnosticsWhenUnsupported = true;

    [Header("Logging")]
    [SerializeField, Min(0.02f)] float _sampleInterval = 0.1f;
    [SerializeField, Min(0.1f)] float _consoleInterval = 1f;
    [SerializeField] bool _logToConsole = true;
    [SerializeField] bool _writeCsv = true;
    [SerializeField] bool _writeToExperimentSession = true;
    [SerializeField] bool _writeStandaloneCsv;
    [SerializeField] bool _writeExcelSeparatorDirective = true;
    [SerializeField] bool _logInvalidSamplesToConsole = true;
    [SerializeField] string _csvFilePrefix = "eye_tracking_log";

    [Header("Debug Ray")]
    [SerializeField, Tooltip("Draws left/right gaze debug rays in the Game view when Gizmos are enabled. Keep disabled for participant-facing runs.")]
    bool _drawDebugRays;
    [SerializeField, Min(0.01f)] float _debugRayLength = 3f;
    [SerializeField] Color _leftRayColor = new Color(0.25f, 0.65f, 1f, 1f);
    [SerializeField] Color _rightRayColor = new Color(0.1f, 1f, 0.7f, 1f);

    OVRPlugin.EyeGazesState _eyeGazesState;
    StreamWriter _csvWriter;
    string _csvPath;
    float _nextSampleTime;
    float _nextConsoleTime;
    float _nextStartAttemptTime;
    bool _startupDiagnosticsLogged;
    bool _unsupportedDiagnosticsLogged;
    Action<string> _permissionGrantedCallback;
    MeditationExperimentCsvLogger _experimentCsvLogger;

    public string csvPath => _csvPath;

    void Awake()
    {
        _permissionGrantedCallback = HandlePermissionGranted;
        if (_autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    void OnEnable()
    {
        OVRPermissionsRequester.PermissionGranted -= _permissionGrantedCallback;
        OVRPermissionsRequester.PermissionGranted += _permissionGrantedCallback;

        if (_autoFindReferences)
        {
            AutoFindReferences();
        }

        RequestPermissionIfNeeded();
        _nextStartAttemptTime = 0f;
        StartEyeTrackingIfNeeded();
        LogRuntimeDiagnostics("startup");
        OpenStandaloneCsvIfNeeded();

        _nextSampleTime = 0f;
        _nextConsoleTime = 0f;
    }

    void OnDisable()
    {
        OVRPermissionsRequester.PermissionGranted -= _permissionGrantedCallback;
        CloseCsv();
    }

    void OnValidate()
    {
        _sampleInterval = Mathf.Max(0.02f, _sampleInterval);
        _consoleInterval = Mathf.Max(0.1f, _consoleInterval);
        _startRetryInterval = Mathf.Max(0.25f, _startRetryInterval);
        _debugRayLength = Mathf.Max(0.01f, _debugRayLength);
    }

    void Update()
    {
        if (_autoFindReferences && HasMissingReferences())
        {
            AutoFindReferences();
        }

        StartEyeTrackingIfNeeded();
        DrawDebugRays();

        var now = Time.unscaledTime;
        if (now < _nextSampleTime)
        {
            return;
        }

        _nextSampleTime = now + _sampleInterval;
        SampleAndLog(now);
    }

    [ContextMenu("Log One Sample Now")]
    public void LogOneSampleNow()
    {
        if (_autoFindReferences)
        {
            AutoFindReferences();
        }

        SampleAndLog(Time.unscaledTime, true);
    }

    [ContextMenu("Print CSV Path")]
    public void PrintCsvPath()
    {
        Debug.Log(string.IsNullOrEmpty(_csvPath)
            ? "[EyeTrackingDataLogger] CSV logging is not active."
            : "[EyeTrackingDataLogger] CSV path: " + _csvPath,
            this);
    }

    void RequestPermissionIfNeeded()
    {
        if (!_requestEyeTrackingPermission)
        {
            return;
        }

        if (OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking))
        {
            return;
        }

        OVRPermissionsRequester.Request(new List<OVRPermissionsRequester.Permission>
        {
            OVRPermissionsRequester.Permission.EyeTracking
        });
    }

    void StartEyeTrackingIfNeeded()
    {
        if (!_startEyeTrackingIfNeeded)
        {
            return;
        }

        if (!OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking))
        {
            return;
        }

        if (OVRPlugin.eyeTrackingEnabled)
        {
            return;
        }

        if (!OVRPlugin.initialized)
        {
            return;
        }

        var now = Time.unscaledTime;
        if (now < _nextStartAttemptTime)
        {
            return;
        }

        _nextStartAttemptTime = now + _startRetryInterval;
        if (!OVRPlugin.eyeTrackingSupported)
        {
            LogUnsupportedEyeTrackingWarning();
            return;
        }

        var started = OVRPlugin.StartEyeTracking();
        Debug.Log("[EyeTrackingDataLogger] OVRPlugin.StartEyeTracking result: " + started, this);
    }

    void LogRuntimeDiagnostics(string reason)
    {
        if (!_logRuntimeDiagnosticsOnStartup || _startupDiagnosticsLogged)
        {
            return;
        }

        _startupDiagnosticsLogged = true;
        Debug.Log(
            "[EyeTrackingDataLogger] Runtime diagnostics (" + reason + "): " +
            MetaQuestRuntimeDiagnostics.BuildEyeTrackingStatus(),
            this);
    }

    void LogUnsupportedEyeTrackingWarning()
    {
        if (_logRuntimeDiagnosticsWhenUnsupported)
        {
            if (_unsupportedDiagnosticsLogged)
            {
                return;
            }

            _unsupportedDiagnosticsLogged = true;
            Debug.LogWarning(
                "[EyeTrackingDataLogger] OVRPlugin reports eye tracking is not supported on this runtime/device. " +
                MetaQuestRuntimeDiagnostics.BuildEyeTrackingStatus(),
                this);
            return;
        }

        Debug.LogWarning("[EyeTrackingDataLogger] OVRPlugin reports eye tracking is not supported on this runtime/device.", this);
    }

    void HandlePermissionGranted(string permissionId)
    {
        if (permissionId != OVRPermissionsRequester.GetPermissionId(OVRPermissionsRequester.Permission.EyeTracking))
        {
            return;
        }

        Debug.Log("[EyeTrackingDataLogger] Eye tracking permission granted.", this);
        _nextStartAttemptTime = 0f;
        StartEyeTrackingIfNeeded();
    }

    void SampleAndLog(float now, bool forceConsole = false)
    {
        var permissionGranted = OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking);
        var supported = OVRPlugin.eyeTrackingSupported;
        var enabled = OVRPlugin.eyeTrackingEnabled;
        var gotState = OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _eyeGazesState);

        var leftRaw = GetRawEyeState(OVRPlugin.Eye.Left, gotState);
        var rightRaw = GetRawEyeState(OVRPlugin.Eye.Right, gotState);

        var sample = new Sample
        {
            realtime = Time.realtimeSinceStartupAsDouble,
            time = Time.time,
            frame = Time.frameCount,
            permissionGranted = permissionGranted,
            supported = supported,
            enabled = enabled,
            gotState = gotState,
            pluginTime = _eyeGazesState.Time,
            leftRaw = leftRaw,
            rightRaw = rightRaw,
            leftComponent = CreateComponentSample(_leftEyeGaze, _leftGazeTransform),
            rightComponent = CreateComponentSample(_rightEyeGaze, _rightGazeTransform),
            centerTransform = CreateTransformSample(_centerEyeTransform)
        };

        if (_writeCsv)
        {
            WriteCsv(sample);
        }

        if (!_logToConsole)
        {
            return;
        }

        var shouldConsoleLog = forceConsole || now >= _nextConsoleTime;
        if (!shouldConsoleLog)
        {
            return;
        }

        _nextConsoleTime = now + _consoleInterval;
        if (sample.HasUsableEyeData() || _logInvalidSamplesToConsole || forceConsole)
        {
            Debug.Log(FormatConsole(sample), this);
        }
    }

    EyeRawSample GetRawEyeState(OVRPlugin.Eye eye, bool gotState)
    {
        var sample = new EyeRawSample
        {
            eye = eye.ToString(),
            present = false
        };

        var index = (int)eye;
        if (!gotState ||
            _eyeGazesState.EyeGazes == null ||
            index < 0 ||
            index >= _eyeGazesState.EyeGazes.Length)
        {
            return sample;
        }

        var state = _eyeGazesState.EyeGazes[index];
        var orientation = state.Pose.Orientation;
        var rotation = new Quaternion(orientation.x, orientation.y, orientation.z, orientation.w);

        sample.present = true;
        sample.valid = state.IsValid;
        sample.confidence = state.Confidence;
        sample.position = ToVector3(state.Pose.Position);
        sample.rotation = rotation;
        sample.forward = rotation * Vector3.forward;
        return sample;
    }

    ComponentSample CreateComponentSample(OVREyeGaze eyeGaze, Transform fallbackTransform)
    {
        var transformToLog = eyeGaze != null ? eyeGaze.transform : fallbackTransform;
        var sample = new ComponentSample
        {
            found = transformToLog != null,
            componentEnabled = eyeGaze != null && eyeGaze.enabled,
            confidence = eyeGaze != null ? eyeGaze.Confidence : -1f,
            transform = CreateTransformSample(transformToLog)
        };

        return sample;
    }

    TransformSample CreateTransformSample(Transform target)
    {
        if (target == null)
        {
            return new TransformSample { found = false };
        }

        return new TransformSample
        {
            found = true,
            name = target.name,
            position = target.position,
            rotation = target.rotation,
            forward = target.forward
        };
    }

    void AutoFindReferences()
    {
        var leftObject = GameObject.Find(LeftEyeGazeName);
        var rightObject = GameObject.Find(RightEyeGazeName);
        var centerObject = GameObject.Find(CenterEyeAnchorName);

        if (leftObject != null)
        {
            _leftGazeTransform = leftObject.transform;
            _leftEyeGaze = leftObject.GetComponent<OVREyeGaze>();
        }

        if (rightObject != null)
        {
            _rightGazeTransform = rightObject.transform;
            _rightEyeGaze = rightObject.GetComponent<OVREyeGaze>();
        }

        if (centerObject != null)
        {
            _centerEyeTransform = centerObject.transform;
        }

        if (_leftEyeGaze == null || _rightEyeGaze == null)
        {
            var eyeGazes = FindObjectsOfType<OVREyeGaze>(true);
            for (var i = 0; i < eyeGazes.Length; i++)
            {
                var eyeGaze = eyeGazes[i];
                if (eyeGaze.Eye == OVREyeGaze.EyeId.Left && _leftEyeGaze == null)
                {
                    _leftEyeGaze = eyeGaze;
                    _leftGazeTransform = eyeGaze.transform;
                }
                else if (eyeGaze.Eye == OVREyeGaze.EyeId.Right && _rightEyeGaze == null)
                {
                    _rightEyeGaze = eyeGaze;
                    _rightGazeTransform = eyeGaze.transform;
                }
            }
        }
    }

    bool HasMissingReferences()
    {
        return _leftEyeGaze == null ||
               _rightEyeGaze == null ||
               _leftGazeTransform == null ||
               _rightGazeTransform == null ||
               _centerEyeTransform == null;
    }

    void DrawDebugRays()
    {
        if (!_drawDebugRays)
        {
            return;
        }

        if (_leftGazeTransform != null)
        {
            Debug.DrawRay(_leftGazeTransform.position, _leftGazeTransform.forward * _debugRayLength, _leftRayColor);
        }

        if (_rightGazeTransform != null)
        {
            Debug.DrawRay(_rightGazeTransform.position, _rightGazeTransform.forward * _debugRayLength, _rightRayColor);
        }
    }

    void OpenStandaloneCsvIfNeeded()
    {
        if (!_writeCsv || !_writeStandaloneCsv || _csvWriter != null)
        {
            return;
        }

        var safePrefix = string.IsNullOrWhiteSpace(_csvFilePrefix) ? "eye_tracking_log" : _csvFilePrefix.Trim();
        var fileName = string.Format(
            CultureInfo.InvariantCulture,
            "{0}_{1:yyyyMMdd_HHmmss}.csv",
            safePrefix,
            DateTime.Now);

        _csvPath = Path.Combine(Application.persistentDataPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(_csvPath));

        var stream = new FileStream(_csvPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _csvWriter = new StreamWriter(stream, Encoding.UTF8);
        if (_writeExcelSeparatorDirective)
        {
            // Lets Excel split comma CSV correctly on systems whose list separator is semicolon.
            _csvWriter.WriteLine(CsvSeparatorDirective);
        }

        _csvWriter.WriteLine(GetCsvHeader());
        _csvWriter.Flush();

        Debug.Log("[EyeTrackingDataLogger] Writing eye tracking CSV to: " + _csvPath, this);
    }

    void CloseCsv()
    {
        if (_csvWriter == null)
        {
            return;
        }

        _csvWriter.Flush();
        _csvWriter.Dispose();
        _csvWriter = null;
    }

    void WriteCsv(Sample sample)
    {
        var wroteExperimentSample = false;
        if (_writeToExperimentSession)
        {
            wroteExperimentSample = WriteExperimentCsv(sample);
        }

        if (_writeStandaloneCsv)
        {
            WriteStandaloneCsv(sample);
        }
        else if (!wroteExperimentSample && !_writeToExperimentSession)
        {
            WriteStandaloneCsv(sample);
        }
    }

    bool WriteExperimentCsv(Sample sample)
    {
        var logger = ResolveExperimentCsvLogger();
        if (logger == null || !logger.sessionActive)
        {
            return false;
        }

        return logger.TryLogEyeTrackingSample(CreateExperimentEyeTrackingRow(sample));
    }

    MeditationExperimentCsvLogger ResolveExperimentCsvLogger()
    {
        if (_experimentCsvLogger != null)
        {
            return _experimentCsvLogger;
        }

        _experimentCsvLogger = FindObjectOfType<MeditationExperimentCsvLogger>();
        return _experimentCsvLogger;
    }

    MeditationExperimentCsvLogger.EyeTrackingRow CreateExperimentEyeTrackingRow(Sample sample)
    {
        return new MeditationExperimentCsvLogger.EyeTrackingRow
        {
            sampleRealtime = sample.realtime,
            frame = sample.frame,
            leftValid = IsEyeSampleValid(sample.leftRaw, sample.leftComponent),
            leftConfidence = ResolveEyeConfidence(sample.leftRaw, sample.leftComponent),
            leftOrigin = ResolveEyeOrigin(sample.leftRaw, sample.leftComponent.transform),
            leftForward = ResolveEyeForward(sample.leftRaw, sample.leftComponent.transform),
            rightValid = IsEyeSampleValid(sample.rightRaw, sample.rightComponent),
            rightConfidence = ResolveEyeConfidence(sample.rightRaw, sample.rightComponent),
            rightOrigin = ResolveEyeOrigin(sample.rightRaw, sample.rightComponent.transform),
            rightForward = ResolveEyeForward(sample.rightRaw, sample.rightComponent.transform),
            centerEyePosition = sample.centerTransform.position,
            centerEyeForward = sample.centerTransform.forward
        };
    }

    static bool IsEyeSampleValid(EyeRawSample rawSample, ComponentSample componentSample)
    {
        return rawSample.valid || componentSample.confidence > 0f;
    }

    static float ResolveEyeConfidence(EyeRawSample rawSample, ComponentSample componentSample)
    {
        if (componentSample.confidence >= 0f)
        {
            return componentSample.confidence;
        }

        return rawSample.present ? rawSample.confidence : float.NaN;
    }

    static Vector3 ResolveEyeOrigin(EyeRawSample rawSample, TransformSample componentTransform)
    {
        if (componentTransform.found)
        {
            return componentTransform.position;
        }

        return rawSample.present ? rawSample.position : Vector3.zero;
    }

    static Vector3 ResolveEyeForward(EyeRawSample rawSample, TransformSample componentTransform)
    {
        if (componentTransform.found)
        {
            return componentTransform.forward;
        }

        return rawSample.present ? rawSample.forward : Vector3.zero;
    }

    void WriteStandaloneCsv(Sample sample)
    {
        if (_csvWriter == null)
        {
            OpenStandaloneCsvIfNeeded();
        }

        if (_csvWriter == null)
        {
            return;
        }

        _csvWriter.WriteLine(ToCsv(sample));
        _csvWriter.Flush();
    }

    static string FormatConsole(Sample sample)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[EyeTrackingDataLogger] frame={0} permission={1} supported={2} enabled={3} gotState={4} " +
            "L(raw valid={5} conf={6:0.000} dir={7}) R(raw valid={8} conf={9:0.000} dir={10}) " +
            "L(go enabled={11} conf={12:0.000} fwd={13}) R(go enabled={14} conf={15:0.000} fwd={16})",
            sample.frame,
            sample.permissionGranted,
            sample.supported,
            sample.enabled,
            sample.gotState,
            sample.leftRaw.valid,
            sample.leftRaw.confidence,
            FormatVector(sample.leftRaw.forward),
            sample.rightRaw.valid,
            sample.rightRaw.confidence,
            FormatVector(sample.rightRaw.forward),
            sample.leftComponent.componentEnabled,
            sample.leftComponent.confidence,
            FormatVector(sample.leftComponent.transform.forward),
            sample.rightComponent.componentEnabled,
            sample.rightComponent.confidence,
            FormatVector(sample.rightComponent.transform.forward));
    }

    static string GetCsvHeader()
    {
        return string.Join(",",
            "realtime",
            "time",
            "frame",
            "permissionGranted",
            "supported",
            "enabled",
            "gotState",
            "pluginTime",
            RawHeader("leftRaw"),
            RawHeader("rightRaw"),
            ComponentHeader("leftComponent"),
            ComponentHeader("rightComponent"),
            TransformHeader("centerEye"));
    }

    static string ToCsv(Sample sample)
    {
        return string.Join(",",
            D(sample.realtime),
            F(sample.time),
            sample.frame.ToString(CultureInfo.InvariantCulture),
            B(sample.permissionGranted),
            B(sample.supported),
            B(sample.enabled),
            B(sample.gotState),
            D(sample.pluginTime),
            RawCsv(sample.leftRaw),
            RawCsv(sample.rightRaw),
            ComponentCsv(sample.leftComponent),
            ComponentCsv(sample.rightComponent),
            TransformCsv(sample.centerTransform));
    }

    static string RawHeader(string prefix)
    {
        return string.Join(",",
            prefix + "Present",
            prefix + "Valid",
            prefix + "Confidence",
            VectorHeader(prefix + "Position"),
            QuaternionHeader(prefix + "Rotation"),
            VectorHeader(prefix + "Forward"));
    }

    static string RawCsv(EyeRawSample sample)
    {
        return string.Join(",",
            B(sample.present),
            B(sample.valid),
            F(sample.confidence),
            VectorCsv(sample.position),
            QuaternionCsv(sample.rotation),
            VectorCsv(sample.forward));
    }

    static string ComponentHeader(string prefix)
    {
        return string.Join(",",
            prefix + "Found",
            prefix + "Enabled",
            prefix + "Confidence",
            TransformHeader(prefix + "Transform"));
    }

    static string ComponentCsv(ComponentSample sample)
    {
        return string.Join(",",
            B(sample.found),
            B(sample.componentEnabled),
            F(sample.confidence),
            TransformCsv(sample.transform));
    }

    static string TransformHeader(string prefix)
    {
        return string.Join(",",
            prefix + "Found",
            prefix + "Name",
            VectorHeader(prefix + "Position"),
            QuaternionHeader(prefix + "Rotation"),
            VectorHeader(prefix + "Forward"));
    }

    static string TransformCsv(TransformSample sample)
    {
        return string.Join(",",
            B(sample.found),
            CsvEscape(sample.name),
            VectorCsv(sample.position),
            QuaternionCsv(sample.rotation),
            VectorCsv(sample.forward));
    }

    static string VectorHeader(string prefix)
    {
        return prefix + "X," + prefix + "Y," + prefix + "Z";
    }

    static string QuaternionHeader(string prefix)
    {
        return prefix + "X," + prefix + "Y," + prefix + "Z," + prefix + "W";
    }

    static string VectorCsv(Vector3 value)
    {
        return string.Join(",", F(value.x), F(value.y), F(value.z));
    }

    static string QuaternionCsv(Quaternion value)
    {
        return string.Join(",", F(value.x), F(value.y), F(value.z), F(value.w));
    }

    static Vector3 ToVector3(OVRPlugin.Vector3f value)
    {
        return new Vector3(value.x, value.y, value.z);
    }

    static string FormatVector(Vector3 value)
    {
        return string.Format(CultureInfo.InvariantCulture, "({0:0.000},{1:0.000},{2:0.000})", value.x, value.y, value.z);
    }

    static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string B(bool value)
    {
        return value ? "1" : "0";
    }

    static string F(float value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    static string D(double value)
    {
        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    struct Sample
    {
        public double realtime;
        public float time;
        public int frame;
        public bool permissionGranted;
        public bool supported;
        public bool enabled;
        public bool gotState;
        public double pluginTime;
        public EyeRawSample leftRaw;
        public EyeRawSample rightRaw;
        public ComponentSample leftComponent;
        public ComponentSample rightComponent;
        public TransformSample centerTransform;

        public bool HasUsableEyeData()
        {
            return gotState &&
                   ((leftRaw.valid && leftRaw.confidence > 0f) ||
                    (rightRaw.valid && rightRaw.confidence > 0f));
        }
    }

    struct EyeRawSample
    {
        public string eye;
        public bool present;
        public bool valid;
        public float confidence;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 forward;
    }

    struct ComponentSample
    {
        public bool found;
        public bool componentEnabled;
        public float confidence;
        public TransformSample transform;
    }

    struct TransformSample
    {
        public bool found;
        public string name;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 forward;
    }
}

static class MetaQuestRuntimeDiagnostics
{
    public static string BuildEyeTrackingStatus()
    {
        var sb = new StringBuilder(512);
        Append(sb, "unity", Application.unityVersion);
        Append(sb, "platform", Application.platform);
        Append(sb, "isEditor", Application.isEditor);
        Append(sb, "deviceModel", SystemInfo.deviceModel);
        Append(sb, "xrEnabled", Safe(() => XRSettings.enabled));
        Append(sb, "xrDeviceActive", Safe(() => XRSettings.isDeviceActive));
        Append(sb, "loadedDevice", Safe(() => XRSettings.loadedDeviceName));
        Append(sb, "activeLoader", GetActiveLoaderName());
        Append(sb, "ovrInitialized", Safe(() => OVRPlugin.initialized));
        Append(sb, "ovrManagerInitialized", Safe(() => OVRManager.OVRManagerinitialized));
        Append(sb, "headset", Safe(() => OVRManager.systemHeadsetType));
        Append(sb, "xrApi", Safe(() => OVRPlugin.nativeXrApi));
        Append(sb, "ovrPluginVersion", Safe(() => OVRManager.pluginVersion));
        Append(sb, "ovrSdkVersion", Safe(() => OVRManager.sdkVersion));
        Append(sb, "eyePermissionGranted", Safe(() => OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking)));
        Append(sb, "eyeSupported", Safe(() => OVRPlugin.eyeTrackingSupported));
        Append(sb, "eyeEnabled", Safe(() => OVRPlugin.eyeTrackingEnabled));
        Append(sb, "openXRInstance", Safe(() => OVRPlugin.GetNativeOpenXRInstance()));
        Append(sb, "openXRSession", Safe(() => OVRPlugin.GetNativeOpenXRSession()));
        return sb.ToString();
    }

    static string GetActiveLoaderName()
    {
        return Safe(() =>
        {
            var settings = XRGeneralSettings.Instance;
            var manager = settings != null ? settings.Manager : null;
            var loader = manager != null ? manager.activeLoader : null;
            return loader != null ? loader.name : "null";
        });
    }

    static string Safe(Func<object> read)
    {
        try
        {
            var value = read();
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
        }
        catch (Exception ex)
        {
            return "error:" + ex.GetType().Name;
        }
    }

    static void Append(StringBuilder builder, string name, object value)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(name);
        builder.Append('=');
        builder.Append(value ?? "null");
    }
}
