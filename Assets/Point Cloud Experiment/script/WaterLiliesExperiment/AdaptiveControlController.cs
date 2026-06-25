using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public enum AdaptiveControlRuntimeState
{
    Idle,
    Warmup,
    Running,
    Degraded,
    Failsafe,
    Stopped
}

[Serializable]
public sealed class AdaptiveHeadPosePayload
{
    public float head_position_x;
    public float head_position_y;
    public float head_position_z;
    public float head_angular_velocity_deg_s;
}

[Serializable]
public sealed class AdaptiveEyePayload
{
    public float gaze_direction_x;
    public float gaze_direction_y;
    public float gaze_direction_z;
    public float gaze_on_painting;
}

[Serializable]
public sealed class AdaptiveSensorFrame
{
    public string message_type = "UnitySensorFrame";
    public string protocol_version = "adaptive-control-v1";
    public string session_id;
    public long sequence;
    public long unix_time_ms;
    public string participant_id;
    public string condition_id;
    public bool headset_presence_available;
    public bool headset_worn;
    public AdaptiveHeadPosePayload head_pose;
    public AdaptiveEyePayload eye;
}

[Serializable]
public sealed class AdaptiveReadinessRequest
{
    public string message_type = "AdaptiveReadinessRequest";
    public string protocol_version = "adaptive-control-v1";
    public string request_id;
    public long unix_time_ms;
    public bool headset_presence_available;
    public bool headset_worn;
    public bool head_pose_available;
    public bool eye_tracking_available;
    public string gaze_source;
}

[Serializable]
public sealed class AdaptiveControlCommand
{
    public string message_type;
    public string protocol_version;
    public string session_id;
    public string command_id;
    public int cycle_index;
    public long issued_unix_ms;
    public long expires_unix_ms;
    public string action;
    public string current_condition;
    public string target_condition;
    public float intensity;
    public float frequency;
    public float transition_seconds;
    public float utility_delta;
    public float predicted_relaxation;
    public float predicted_discomfort;
    public string model_variant;
    public string[] active_modalities;
    public string profile_id;
    public string profile_sha256;
    public string[] reasons;
}

[Serializable]
public sealed class AdaptiveStatusSnapshot
{
    public string message_type;
    public string protocol_version;
    public string session_id;
    public long unix_time_ms;
    public string runtime_state;
    public float next_decision_in_seconds;
    public string current_condition;
    public string target_condition;
    public string model_bundle_id;
    public string model_version;
    public string model_variant;
    public float relaxation;
    public float discomfort;
    public string prediction_source;
    public string model_supervision;
    public string motion_source;
    public int model_input_feature_count;
    public int model_available_feature_count;
    public int model_missing_feature_count;
    public string[] model_modalities_used;
    public bool headset_presence_available;
    public bool headset_worn;
    public bool lsl_eeg_stream_found;
    public bool lsl_eeg_sample_received;
    public bool warmup_complete;
    public string[] modality_names;
    public float[] modality_coverage_values;
    public float[] modality_age_values;
    public string[] candidate_conditions;
    public float[] candidate_utility_values;
    public string last_command_id;
    public string[] reasons;
}

[Serializable]
public sealed class AdaptiveReadinessSnapshot
{
    public string message_type;
    public string protocol_version;
    public string request_id;
    public long unix_time_ms;
    public bool python_ready;
    public bool model_ready;
    public string model_bundle_id;
    public string model_version;
    public bool lsl_eeg_stream_found;
    public bool lsl_eeg_sample_received;
    public bool eeg_ready;
    public bool ecg_ready;
    public int physio_sample_channel_count;
    public int expected_min_channel_count;
    public string[] reasons;
}

[Serializable]
public sealed class AdaptivePhysioStats
{
    public int sample_count;
    public float mean;
    public float std;
    public float min;
    public float max;
    public float rms;
    public float peak_to_peak;
}

[Serializable]
public sealed class AdaptivePhysioChannelSnapshot
{
    public string name;
    public string source;
    public string unit;
    public float[] raw_values;
    public float[] filtered_values;
    public AdaptivePhysioStats raw;
    public AdaptivePhysioStats filtered;
}

[Serializable]
public sealed class AdaptivePhysioSnapshot
{
    public string message_type;
    public string protocol_version;
    public long unix_time_ms;
    public bool lsl_eeg_stream_found;
    public bool lsl_eeg_sample_received;
    public string stream_name;
    public string stream_type;
    public int channel_count;
    public int expected_channel_count;
    public float nominal_srate;
    public float sample_rate_hz;
    public float window_seconds;
    public float sample_age_ms;
    public int sample_count;
    public int max_points;
    public AdaptivePhysioChannelSnapshot[] channels;
    public string[] reasons;
}

[Serializable]
public sealed class AdaptiveControlAck
{
    public string message_type = "AdaptiveControlAck";
    public string protocol_version = "adaptive-control-v1";
    public string session_id;
    public string command_id;
    public long unix_time_ms;
    public string status;
    public string reason;
    public string condition_id;
    public float intensity;
    public float frequency;
}

/// <summary>
/// Autonomous receiver and executor for the explicitly non-research adaptive-control loop.
/// Attach this component to the Water Lilies painting object, together with
/// WaterLiliesVfxController and AdaptiveControlSensorPublisher.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Adaptive Control/Controller")]
public sealed class AdaptiveControlController : MonoBehaviour
{
    const string LegacyProfileDirectory = "WaterLiliesDemo";
    const string LegacyProfileFile = "water-lilies-demo-v1.json";

    [Header("References")]
    [SerializeField] WaterLiliesVfxController _vfxController;
    [SerializeField] WaterLiliesExperimentManager _formalExperimentManager;
    [SerializeField] WaterLiliesTrackingSampler _trackingSampler;

    [Header("Session Identity")]
    [SerializeField] string _participantId = "CONTROL";
    [SerializeField] bool _autoStart;
    [SerializeField] string _initialCondition = "C5";

    [Header("Profile")]
    [SerializeField] string _profileRelativePath = AdaptiveControlProfile.DefaultRelativePath;

    [Header("Local UDP")]
    [SerializeField] string _pythonHost = "127.0.0.1";
    [SerializeField, Min(1)] int _unityToPythonPort = 5055;
    [SerializeField, Min(1)] int _pythonToUnityPort = 5056;
    [SerializeField, Min(1f)] float _commandTimeoutSeconds = 25f;
    [SerializeField, Min(0.01f)] float _defaultTransitionSeconds = 2f;
    [SerializeField, Min(0.5f)] float _readinessTimeoutSeconds = 2.5f;
    [SerializeField, Min(1f)] float _readinessFreshSeconds = 60f;

    AdaptiveControlProfile _profile;
    UdpClient _sender;
    UdpClient _receiver;
    IPEndPoint _pythonEndpoint;
    Coroutine _transitionRoutine;
    long _sensorSequence;
    int _lastAcceptedCycle = -1;
    string _sessionId;
    string _currentCondition;
    string _targetCondition;
    double _lastValidCommandRealtime;
    float _transitionProgress;
    string _lastReason = "Not started.";
    AdaptiveStatusSnapshot _latestStatus;
    AdaptiveReadinessSnapshot _latestReadiness;
    AdaptivePhysioSnapshot _latestPhysioSnapshot;
    WaterLiliesTrackingSample _latestReadinessTrackingSample;
    bool _hasReadinessTrackingSample;
    bool _readinessCheckInProgress;
    string _readinessRequestId;
    double _readinessRequestRealtime;
    double _lastReadinessResultRealtime = -1d;
    double _localWindowStartRealtime = -1d;

    public AdaptiveControlRuntimeState runtimeState { get; private set; } = AdaptiveControlRuntimeState.Idle;
    public string sessionId => _sessionId ?? string.Empty;
    public string currentConditionId => _currentCondition ?? string.Empty;
    public string targetConditionId => _targetCondition ?? string.Empty;
    public string profileId => _profile != null ? _profile.profileId : string.Empty;
    public string profileSha256 => _profile != null ? _profile.sha256 : string.Empty;
    public float transitionProgress => _transitionProgress;
    public string lastReason => _lastReason;
    public AdaptiveStatusSnapshot latestStatus => _latestStatus;
    public AdaptiveReadinessSnapshot latestReadiness => _latestReadiness;
    public AdaptivePhysioSnapshot latestPhysioSnapshot => _latestPhysioSnapshot;
    public bool readinessCheckInProgress => _readinessCheckInProgress;
    public bool readinessResultIsFresh => _latestReadiness != null &&
                                          !_readinessCheckInProgress &&
                                          Time.realtimeSinceStartupAsDouble - _lastReadinessResultRealtime <= ReadinessFreshSeconds();
    public float readinessResultAgeSeconds => _lastReadinessResultRealtime < 0d
        ? float.NaN
        : (float)(Time.realtimeSinceStartupAsDouble - _lastReadinessResultRealtime);
    public bool hasReadinessTrackingSample => _hasReadinessTrackingSample;
    public bool latestHeadTrackingReady => _hasReadinessTrackingSample && _latestReadinessTrackingSample.headPoseAvailable;
    public bool latestEyeTrackingReady => _hasReadinessTrackingSample && _latestReadinessTrackingSample.eyeTrackingAvailable;
    public string latestGazeSource => _hasReadinessTrackingSample ? _latestReadinessTrackingSample.gazeSource : string.Empty;
    public bool latestHeadsetPresenceAvailable => _hasReadinessTrackingSample && _latestReadinessTrackingSample.headsetPresenceAvailable;
    public bool latestHeadsetWorn => _hasReadinessTrackingSample && _latestReadinessTrackingSample.headsetUserPresent;
    public float tenSecondWindowCountdownSeconds => GetTenSecondWindowCountdownSeconds();
    public float currentIntensity => _vfxController != null ? _vfxController.currentIntensity : float.NaN;
    public float currentFrequency => _vfxController != null ? _vfxController.currentFrequency : float.NaN;
    public bool isRunning => runtimeState == AdaptiveControlRuntimeState.Warmup ||
                             runtimeState == AdaptiveControlRuntimeState.Running ||
                             runtimeState == AdaptiveControlRuntimeState.Degraded;

    void Reset()
    {
        _vfxController = GetComponent<WaterLiliesVfxController>();
        _formalExperimentManager = GetComponent<WaterLiliesExperimentManager>();
        _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        _profileRelativePath = AdaptiveControlProfile.DefaultRelativePath;
    }

    void Awake()
    {
        if (_vfxController == null)
        {
            _vfxController = GetComponent<WaterLiliesVfxController>();
        }

        if (_trackingSampler == null)
        {
            _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        }
    }

    void Start()
    {
        if (_autoStart)
        {
            StartControl();
        }
    }

    void Update()
    {
        DrainIncomingMessages();
        UpdateReadinessTimeout();
        if (!isRunning || _lastValidCommandRealtime <= 0d)
        {
            return;
        }

        if (Time.realtimeSinceStartupAsDouble - _lastValidCommandRealtime > _commandTimeoutSeconds)
        {
            EnterFailsafe("python_command_timeout");
        }
    }

    void OnDestroy()
    {
        CloseSockets();
    }

    [ContextMenu("Start Adaptive Control")]
    public void StartControl()
    {
        if (isRunning)
        {
            return;
        }

        if (!CanStartControl(out var readinessReason))
        {
            _lastReason = "Readiness check failed: " + readinessReason;
            return;
        }

        if (!LoadProfile())
        {
            runtimeState = AdaptiveControlRuntimeState.Failsafe;
            return;
        }

        if (_vfxController == null)
        {
            _lastReason = "WaterLiliesVfxController reference is missing.";
            runtimeState = AdaptiveControlRuntimeState.Failsafe;
            return;
        }

        if (!AdaptiveControlProfile.TryNormalizeCondition(_initialCondition, out var initialCondition) ||
            !_profile.TryGetValues(initialCondition, out _))
        {
            _lastReason = "Initial condition must be C1-C9.";
            runtimeState = AdaptiveControlRuntimeState.Failsafe;
            return;
        }

        try
        {
            OpenSockets();
        }
        catch (Exception exception)
        {
            _lastReason = "Unable to open local UDP sockets: " + exception.Message;
            runtimeState = AdaptiveControlRuntimeState.Failsafe;
            return;
        }

        if (_formalExperimentManager != null && _formalExperimentManager.enabled)
        {
            _formalExperimentManager.enabled = false;
        }

        _sessionId = Guid.NewGuid().ToString("N");
        _sensorSequence = 0;
        _lastAcceptedCycle = -1;
        _currentCondition = initialCondition;
        _targetCondition = initialCondition;
        _lastValidCommandRealtime = Time.realtimeSinceStartupAsDouble;
        _localWindowStartRealtime = _lastValidCommandRealtime;
        runtimeState = AdaptiveControlRuntimeState.Warmup;
        _lastReason = "Warmup: C5 is collecting the first 10-second feature window.";
        BeginTransition(initialCondition, _defaultTransitionSeconds, false);
    }

    [ContextMenu("Check Adaptive Control Readiness")]
    public void RequestReadinessCheck()
    {
        if (!Application.isPlaying)
        {
            _lastReason = "Enter Play Mode before checking readiness.";
            return;
        }

        if (isRunning)
        {
            _lastReason = "Readiness check is only available before Start Control.";
            return;
        }

        if (_trackingSampler == null)
        {
            _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        }

        if (_trackingSampler == null)
        {
            _hasReadinessTrackingSample = false;
            _readinessCheckInProgress = false;
            _lastReason = "WaterLiliesTrackingSampler reference is missing.";
            return;
        }

        _latestReadinessTrackingSample = _trackingSampler.Capture();
        _hasReadinessTrackingSample = true;
        _latestReadiness = null;
        _readinessRequestId = Guid.NewGuid().ToString("N");
        _readinessRequestRealtime = Time.realtimeSinceStartupAsDouble;
        _readinessCheckInProgress = true;
        if (runtimeState == AdaptiveControlRuntimeState.Failsafe || runtimeState == AdaptiveControlRuntimeState.Stopped)
        {
            runtimeState = AdaptiveControlRuntimeState.Idle;
        }

        try
        {
            OpenSockets();
        }
        catch (Exception exception)
        {
            _readinessCheckInProgress = false;
            _lastReason = "Unable to open local UDP sockets for readiness check: " + exception.Message;
            return;
        }

        SendPayload(new AdaptiveReadinessRequest
        {
            request_id = _readinessRequestId,
            unix_time_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            headset_presence_available = _latestReadinessTrackingSample.headsetPresenceAvailable,
            headset_worn = _latestReadinessTrackingSample.headsetUserPresent,
            head_pose_available = _latestReadinessTrackingSample.headPoseAvailable,
            eye_tracking_available = _latestReadinessTrackingSample.eyeTrackingAvailable,
            gaze_source = string.IsNullOrEmpty(_latestReadinessTrackingSample.gazeSource)
                ? "unavailable"
                : _latestReadinessTrackingSample.gazeSource
        });
        _lastReason = "Readiness check requested.";
    }

    public bool EnsureTransportOpen(out string reason)
    {
        reason = string.Empty;
        if (!Application.isPlaying)
        {
            reason = "Enter Play Mode before listening for Python diagnostics.";
            return false;
        }

        if (_sender != null && _receiver != null && _pythonEndpoint != null)
        {
            reason = "Listening for Python diagnostics.";
            return true;
        }

        try
        {
            OpenSockets();
            reason = "Listening for Python diagnostics on UDP " + _pythonToUnityPort + ".";
            _lastReason = reason;
            return true;
        }
        catch (Exception exception)
        {
            reason = "Unable to open UDP sockets for diagnostics: " + exception.Message;
            _lastReason = reason;
            return false;
        }
    }

    public bool CanStartControl(out string reason)
    {
        reason = string.Empty;
        if (!Application.isPlaying)
        {
            reason = "Enter Play Mode first.";
            return false;
        }

        if (isRunning)
        {
            reason = "Adaptive Control is already running.";
            return false;
        }

        if (_readinessCheckInProgress)
        {
            reason = "Waiting for readiness check result.";
            return false;
        }

        if (!_hasReadinessTrackingSample)
        {
            reason = "Run Check Readiness first.";
            return false;
        }

        if (_latestReadiness == null)
        {
            reason = "Python readiness response has not arrived.";
            return false;
        }

        if (!readinessResultIsFresh)
        {
            reason = "Readiness check is stale. Run Check Readiness again.";
            return false;
        }

        if (_latestReadinessTrackingSample.headsetPresenceAvailable && !_latestReadinessTrackingSample.headsetUserPresent)
        {
            reason = "Headset is not worn.";
            return false;
        }

        if (!_latestReadinessTrackingSample.headPoseAvailable)
        {
            reason = "Head tracking is not available.";
            return false;
        }

        if (!_latestReadinessTrackingSample.eyeTrackingAvailable)
        {
            reason = "Eye tracking is not available.";
            return false;
        }

        if (!_latestReadiness.python_ready || !_latestReadiness.model_ready)
        {
            reason = "Python model is not ready: " + JoinReasonLabels(_latestReadiness.reasons, "unknown_python_readiness_error");
            return false;
        }

        if (!_latestReadiness.eeg_ready || !_latestReadiness.ecg_ready)
        {
            reason = "EEG/ECG LSL is not ready: " + JoinReasonLabels(_latestReadiness.reasons, "no_lsl_sample");
            return false;
        }

        reason = "Ready.";
        return true;
    }

    [ContextMenu("Stop Adaptive Control")]
    public void StopControl()
    {
        if (_profile != null && _vfxController != null)
        {
            ApplyBaseline(_defaultTransitionSeconds);
        }

        runtimeState = AdaptiveControlRuntimeState.Stopped;
        _lastReason = "Stopped by operator.";
        SendAck("stopped", _lastReason, string.Empty);
        CloseSockets();
    }

    [ContextMenu("Emergency Stop Adaptive Control")]
    public void EmergencyStop()
    {
        EnterFailsafe("operator_emergency_stop");
    }

    public void PublishSensorSample(WaterLiliesTrackingSample sample)
    {
        if (!isRunning || _sender == null || _pythonEndpoint == null || string.IsNullOrEmpty(_sessionId))
        {
            return;
        }

        var frame = new AdaptiveSensorFrame
        {
            session_id = _sessionId,
            sequence = ++_sensorSequence,
            unix_time_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            participant_id = string.IsNullOrWhiteSpace(_participantId) ? "CONTROL" : _participantId.Trim(),
            condition_id = _currentCondition,
            headset_presence_available = sample.headsetPresenceAvailable,
            headset_worn = sample.headsetUserPresent,
            head_pose = sample.headPoseAvailable
                ? new AdaptiveHeadPosePayload
                {
                    head_position_x = sample.headPosition.x,
                    head_position_y = sample.headPosition.y,
                    head_position_z = sample.headPosition.z,
                    head_angular_velocity_deg_s = sample.headAngularVelocityDegPerSecond
                }
                : null,
            eye = sample.gazeAvailable
                ? new AdaptiveEyePayload
                {
                    gaze_direction_x = sample.gazeDirection.x,
                    gaze_direction_y = sample.gazeDirection.y,
                    gaze_direction_z = sample.gazeDirection.z,
                    gaze_on_painting = sample.gazeOnPainting ? 1f : 0f
                }
                : null
        };
        SendPayload(frame);
    }

    bool LoadProfile()
    {
        var relativePath = NormalizeProfileRelativePath(_profileRelativePath);
        var path = System.IO.Path.Combine(Application.streamingAssetsPath, relativePath);
        if (!AdaptiveControlProfile.TryLoad(path, out _profile, out var error))
        {
            _lastReason = error;
            return false;
        }

        _profileRelativePath = relativePath;
        return true;
    }

    void OpenSockets()
    {
        CloseSockets();
        var address = IPAddress.Parse(_pythonHost);
        _pythonEndpoint = new IPEndPoint(address, _unityToPythonPort);
        _sender = new UdpClient();
        _receiver = new UdpClient(_pythonToUnityPort);
        _receiver.Client.Blocking = false;
    }

    void CloseSockets()
    {
        if (_sender != null)
        {
            _sender.Close();
            _sender = null;
        }

        if (_receiver != null)
        {
            _receiver.Close();
            _receiver = null;
        }

        _pythonEndpoint = null;
    }

    void DrainIncomingMessages()
    {
        if (_receiver == null)
        {
            return;
        }

        try
        {
            while (_receiver.Available > 0)
            {
                var endpoint = new IPEndPoint(IPAddress.Any, 0);
                var raw = _receiver.Receive(ref endpoint);
                var payload = Encoding.UTF8.GetString(raw);
                if (payload.IndexOf("\"message_type\":\"AdaptiveControlCommand\"", StringComparison.Ordinal) >= 0)
                {
                    HandleControlCommand(JsonUtility.FromJson<AdaptiveControlCommand>(payload));
                }
                else if (payload.IndexOf("\"message_type\":\"AdaptiveStatusSnapshot\"", StringComparison.Ordinal) >= 0)
                {
                    HandleStatus(JsonUtility.FromJson<AdaptiveStatusSnapshot>(payload));
                }
                else if (payload.IndexOf("\"message_type\":\"AdaptiveReadinessSnapshot\"", StringComparison.Ordinal) >= 0)
                {
                    HandleReadiness(JsonUtility.FromJson<AdaptiveReadinessSnapshot>(payload));
                }
                else if (payload.IndexOf("\"message_type\":\"AdaptivePhysioSnapshot\"", StringComparison.Ordinal) >= 0)
                {
                    HandlePhysioSnapshot(JsonUtility.FromJson<AdaptivePhysioSnapshot>(payload));
                }
            }
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.WouldBlock)
        {
            // All currently available datagrams were drained.
        }
        catch (Exception exception)
        {
            _lastReason = "UDP receive error: " + exception.Message;
        }
    }

    void UpdateReadinessTimeout()
    {
        if (!_readinessCheckInProgress)
        {
            return;
        }

        if (Time.realtimeSinceStartupAsDouble - _readinessRequestRealtime <= _readinessTimeoutSeconds)
        {
            return;
        }

        _readinessCheckInProgress = false;
        _lastReadinessResultRealtime = -1d;
        _lastReason = "Readiness check timed out. Start Python adaptive-control service and check UDP 5055/5056.";
    }

    void HandlePhysioSnapshot(AdaptivePhysioSnapshot snapshot)
    {
        if (snapshot == null || snapshot.protocol_version != "adaptive-control-v1")
        {
            return;
        }

        _latestPhysioSnapshot = snapshot;
    }

    void HandleStatus(AdaptiveStatusSnapshot status)
    {
        if (status == null || status.protocol_version != "adaptive-control-v1" || status.session_id != _sessionId)
        {
            return;
        }

        _latestStatus = status;
        if (status.runtime_state == "Running")
        {
            runtimeState = AdaptiveControlRuntimeState.Running;
        }
        else if (status.runtime_state == "Degraded" && isRunning)
        {
            runtimeState = AdaptiveControlRuntimeState.Degraded;
        }
    }

    void HandleReadiness(AdaptiveReadinessSnapshot readiness)
    {
        if (readiness == null ||
            readiness.protocol_version != "adaptive-control-v1" ||
            readiness.request_id != _readinessRequestId)
        {
            return;
        }

        _latestReadiness = readiness;
        _readinessCheckInProgress = false;
        _lastReadinessResultRealtime = Time.realtimeSinceStartupAsDouble;
        _lastReason = CanStartControl(out var reason)
            ? "Readiness check passed."
            : "Readiness check failed: " + reason;
    }

    void HandleControlCommand(AdaptiveControlCommand command)
    {
        if (!TryValidateCommand(command, out var reason))
        {
            SendAck("rejected", reason, command != null ? command.command_id : string.Empty);
            return;
        }

        _lastValidCommandRealtime = Time.realtimeSinceStartupAsDouble;
        _lastAcceptedCycle = command.cycle_index;
        _targetCondition = command.target_condition;
        _lastReason = command.reasons != null && command.reasons.Length > 0
            ? JoinReasonLabels(command.reasons, command.action)
            : command.action;

        if (command.action == "failsafe")
        {
            EnterFailsafe("python_failsafe:" + _lastReason, command.command_id);
            return;
        }

        if (command.action == "hold")
        {
            SendAck("held", _lastReason, command.command_id);
            return;
        }

        SendAck("accepted", _lastReason, command.command_id);
        BeginTransition(command.target_condition, command.transition_seconds, true, command.command_id);
    }

    bool TryValidateCommand(AdaptiveControlCommand command, out string reason)
    {
        reason = string.Empty;
        if (!isRunning || _profile == null || command == null)
        {
            reason = "adaptive_control_not_running";
            return false;
        }

        if (command.message_type != "AdaptiveControlCommand" || command.protocol_version != "adaptive-control-v1")
        {
            reason = "unsupported_command_contract";
            return false;
        }

        if (command.session_id != _sessionId || string.IsNullOrWhiteSpace(command.command_id))
        {
            reason = "session_or_command_id_mismatch";
            return false;
        }

        if (command.cycle_index <= _lastAcceptedCycle)
        {
            reason = "out_of_order_cycle";
            return false;
        }

        if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() > command.expires_unix_ms)
        {
            reason = "expired_command";
            return false;
        }

        if (command.profile_id != _profile.profileId || command.profile_sha256 != _profile.sha256)
        {
            reason = "control_profile_mismatch";
            return false;
        }

        if (command.current_condition != _currentCondition ||
            !_profile.IsAdjacentOrSame(_currentCondition, command.target_condition))
        {
            reason = "invalid_condition_transition";
            return false;
        }

        if (command.action != "apply" && command.action != "hold" && command.action != "failsafe")
        {
            reason = "unsupported_action";
            return false;
        }

        var expected = command.action == "failsafe" ? _profile.baseline : default(AdaptiveControlParameterValues);
        if (command.action != "failsafe" && !_profile.TryGetValues(command.target_condition, out expected))
        {
            reason = "unknown_target_condition";
            return false;
        }

        if (!Approximately(command.intensity, expected.intensity) || !Approximately(command.frequency, expected.frequency) ||
            !IsFinite(command.intensity) || !IsFinite(command.frequency) ||
            !IsFinite(command.transition_seconds) || command.transition_seconds <= 0f)
        {
            reason = "command_parameter_mismatch";
            return false;
        }

        return true;
    }

    void BeginTransition(string targetCondition, float seconds, bool updateConditionOnCompletion, string commandId = "")
    {
        if (_profile == null || _vfxController == null || !_profile.TryGetValues(targetCondition, out var target))
        {
            EnterFailsafe("transition_target_unavailable", commandId);
            return;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
        }

        _transitionRoutine = StartCoroutine(TransitionRoutine(
            targetCondition,
            target,
            Mathf.Max(0.01f, seconds),
            updateConditionOnCompletion,
            commandId));
    }

    void ApplyBaseline(float seconds)
    {
        if (_profile == null || _vfxController == null)
        {
            return;
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
        }

        _transitionRoutine = StartCoroutine(BaselineRoutine(Mathf.Max(0.01f, seconds)));
    }

    IEnumerator TransitionRoutine(
        string targetCondition,
        AdaptiveControlParameterValues target,
        float seconds,
        bool updateConditionOnCompletion,
        string commandId)
    {
        var fromIntensity = _vfxController.currentIntensity;
        var fromFrequency = _vfxController.currentFrequency;
        var elapsed = 0f;
        _transitionProgress = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _transitionProgress = Mathf.Clamp01(elapsed / seconds);
            _vfxController.ApplyParameters(
                Mathf.Lerp(fromIntensity, target.intensity, _transitionProgress),
                Mathf.Lerp(fromFrequency, target.frequency, _transitionProgress),
                false);
            yield return null;
        }

        _vfxController.ApplyParameters(target.intensity, target.frequency, false);
        _transitionProgress = 1f;
        if (updateConditionOnCompletion)
        {
            _currentCondition = targetCondition;
            SendAck("applied", "transition_complete", commandId);
        }

        _transitionRoutine = null;
    }

    IEnumerator BaselineRoutine(float seconds)
    {
        var fromIntensity = _vfxController.currentIntensity;
        var fromFrequency = _vfxController.currentFrequency;
        var elapsed = 0f;
        _transitionProgress = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _transitionProgress = Mathf.Clamp01(elapsed / seconds);
            _vfxController.ApplyParameters(
                Mathf.Lerp(fromIntensity, _profile.baseline.intensity, _transitionProgress),
                Mathf.Lerp(fromFrequency, _profile.baseline.frequency, _transitionProgress),
                false);
            yield return null;
        }

        _vfxController.ApplyParameters(_profile.baseline.intensity, _profile.baseline.frequency, false);
        _transitionProgress = 1f;
        _transitionRoutine = null;
    }

    void EnterFailsafe(string reason, string commandId = "")
    {
        if (runtimeState == AdaptiveControlRuntimeState.Failsafe)
        {
            return;
        }

        runtimeState = AdaptiveControlRuntimeState.Failsafe;
        _lastReason = reason;
        ApplyBaseline(_defaultTransitionSeconds);
        SendAck("failsafe", reason, commandId);
    }

    void SendAck(string status, string reason, string commandId)
    {
        if (_sender == null || _pythonEndpoint == null || string.IsNullOrEmpty(_sessionId))
        {
            return;
        }

        SendPayload(new AdaptiveControlAck
        {
            session_id = _sessionId,
            command_id = commandId,
            unix_time_ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            status = status,
            reason = reason,
            condition_id = _currentCondition,
            intensity = currentIntensity,
            frequency = currentFrequency
        });
    }

    void SendPayload(object payload)
    {
        try
        {
            var raw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            _sender.Send(raw, raw.Length, _pythonEndpoint);
        }
        catch (Exception exception)
        {
            _lastReason = "UDP send error: " + exception.Message;
        }
    }

    float GetTenSecondWindowCountdownSeconds()
    {
        if (_latestStatus != null)
        {
            return Mathf.Max(0f, _latestStatus.next_decision_in_seconds);
        }

        if (!isRunning || _localWindowStartRealtime < 0d)
        {
            return float.NaN;
        }

        return Mathf.Max(0f, 10f - (float)(Time.realtimeSinceStartupAsDouble - _localWindowStartRealtime));
    }

    float ReadinessFreshSeconds()
    {
        return Mathf.Max(60f, _readinessFreshSeconds);
    }

    public static string JoinReasonLabels(string[] reasons, string fallback)
    {
        if (reasons == null || reasons.Length == 0)
        {
            return FormatReasonCode(fallback);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < reasons.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(", ");
            }

            builder.Append(FormatReasonCode(reasons[i]));
        }

        return builder.ToString();
    }

    public static string FormatReasonCode(string reason)
    {
        if (string.IsNullOrEmpty(reason))
        {
            return "-";
        }

        switch (reason)
        {
            case "adaptive_utility_step":
            case "highest_adaptive_control_utility":
                return "Adaptive utility selected a neighbor";
            case "stable_probe":
                return "Stable signal: probing a neighboring condition";
            case "calm_exploration":
                return "Calm signal: exploring a richer condition";
            case "stress_recovery_step":
                return "Stress signal: stepping toward recovery";
            case "stable_no_better_neighbor":
            case "no_neighbor_exceeds_adaptive_control_hysteresis":
                return "Staying put: no neighbor is clearly better";
            case "insufficient_active_modalities":
                return "Data hold: not enough active modalities";
            case "headset_not_worn":
                return "Data hold: headset is not worn";
            case "low_modality_coverage_timeout":
                return "Failsafe: modality coverage stayed too low";
            case "extreme_predicted_discomfort":
                return "Failsafe: predicted discomfort is extreme";
            case "failsafe_locked":
                return "Failsafe is locked";
            case "ready":
                return "Ready";
            case "no_type_eeg_lsl_stream":
                return "EEG/ECG LSL stream was not found";
            case "lsl_stream_found_but_no_sample":
                return "EEG/ECG LSL stream found, waiting for samples";
            case "physio_sample_too_short":
                return "EEG/ECG sample has too few channels";
            case "insufficient_physio_window":
                return "Waiting for a fuller 10-second physio window";
            default:
                return reason;
        }
    }

    static string Join(string[] values, string fallback)
    {
        return values != null && values.Length > 0 ? string.Join(", ", values) : fallback;
    }

    static bool Approximately(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.00001f;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static string NormalizeProfileRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return AdaptiveControlProfile.DefaultRelativePath;
        }

        var normalized = relativePath.Trim().Replace('\\', '/');
        if (normalized.IndexOf(LegacyProfileDirectory, StringComparison.OrdinalIgnoreCase) >= 0 ||
            normalized.IndexOf(LegacyProfileFile, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return AdaptiveControlProfile.DefaultRelativePath;
        }

        return normalized;
    }
}
