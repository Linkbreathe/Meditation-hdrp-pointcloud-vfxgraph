using System.Text;
using UnityEngine;

/// <summary>
/// Optional operator HUD. IMGUI keeps the scene asset-free: attach it to the
/// controller object and toggle visibility from the editor control panel.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Adaptive Control/Runtime Dashboard")]
public sealed class AdaptiveControlRuntimeDashboard : MonoBehaviour
{
    [SerializeField] AdaptiveControlController _controller;
    [SerializeField] bool _visible = false;
    [SerializeField, Min(360f)] float _panelWidth = 520f;
    [SerializeField, Min(10f)] float _margin = 16f;

    GUIStyle _titleStyle;
    GUIStyle _sectionStyle;
    GUIStyle _bodyStyle;
    GUIStyle _warningStyle;
    Vector2 _scroll;

    public bool isVisible => _visible;

    void Reset()
    {
        _controller = GetComponent<AdaptiveControlController>();
    }

    void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponent<AdaptiveControlController>();
        }
    }

    public void SetVisible(bool visible)
    {
        _visible = visible;
    }

    void OnGUI()
    {
        if (!_visible || _controller == null)
        {
            return;
        }

        EnsureStyles();
        var height = Mathf.Max(420f, Screen.height - _margin * 2f);
        GUILayout.BeginArea(new Rect(_margin, _margin, _panelWidth, height), GUI.skin.box);
        _scroll = GUILayout.BeginScrollView(_scroll);
        GUILayout.Label("ADAPTIVE CONTROL", _titleStyle);
        GUILayout.Label("OPERATOR HUD - NOT PARTICIPANT-FACING", _warningStyle);
        DrawSessionSection();
        DrawReadinessSection();
        DrawVfxSection();
        DrawModalitySection();
        DrawModelSection();
        DrawDecisionSection();
        DrawTransportSection();
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawSessionSection()
    {
        Section("1. Session State");
        Line("State", _controller.runtimeState.ToString());
        Line("Session", Short(_controller.sessionId, 14));
        var canStart = _controller.CanStartControl(out var startGateReason);
        if (!_controller.isRunning)
        {
            var previousEnabled = GUI.enabled;
            GUI.enabled = canStart;
            if (GUILayout.Button("Start Control"))
            {
                _controller.StartControl();
            }

            GUI.enabled = previousEnabled;
            Line("Start Gate", startGateReason);
        }
        else if (GUILayout.Button("Stop Control"))
        {
            _controller.StopControl();
        }

        var previous = GUI.color;
        GUI.color = new Color(1f, 0.42f, 0.42f);
        if (GUILayout.Button("Emergency Stop"))
        {
            _controller.EmergencyStop();
        }

        GUI.color = previous;
    }

    void DrawReadinessSection()
    {
        Section("2. Readiness Check");
        var canCheck = Application.isPlaying && !_controller.isRunning && !_controller.readinessCheckInProgress;
        var previousEnabled = GUI.enabled;
        GUI.enabled = canCheck;
        if (GUILayout.Button(_controller.readinessCheckInProgress ? "Checking Readiness..." : "Check Readiness"))
        {
            _controller.RequestReadinessCheck();
        }

        GUI.enabled = previousEnabled;
        if (!Application.isPlaying)
        {
            Line("Play Mode", "enter Play Mode before checking readiness");
            return;
        }

        Line("Head Tracking", _controller.hasReadinessTrackingSample
            ? (_controller.latestHeadTrackingReady ? "ready" : "missing")
            : "not checked");
        Line("Eye Tracking", _controller.hasReadinessTrackingSample
            ? ((_controller.latestEyeTrackingReady ? "ready" : "missing") + " | source " + EmptyAsDash(_controller.latestGazeSource))
            : "not checked");
        var readiness = _controller.latestReadiness;
        if (readiness == null)
        {
            Line("Python", "not checked/not ready");
            Line("EEG/ECG LSL", "not checked/not ready");
            return;
        }

        if (!_controller.readinessResultIsFresh)
        {
            Line("Last Check Age", FormatCountdown(_controller.readinessResultAgeSeconds));
            Line("Python / EEG / ECG", "stale: click Check Readiness again");
            return;
        }

        Line("Last Check Age", FormatCountdown(_controller.readinessResultAgeSeconds));
        Line("Python", readiness.python_ready && readiness.model_ready ? "ready" : "not ready");
        Line("EEG/ECG LSL", readiness.eeg_ready && readiness.ecg_ready ? "ready" : "not ready");
        if (readiness != null)
        {
            Line("Reason", JoinReasons(readiness.reasons, "-"));
        }
    }

    void DrawVfxSection()
    {
        Section("3. VFX State");
        Line("Current Condition", EmptyAsDash(_controller.currentConditionId));
        Line("Target Condition", EmptyAsDash(_controller.targetConditionId));
        Line("Intensity", FormatFloat(_controller.currentIntensity));
        Line("Frequency", FormatFloat(_controller.currentFrequency));
        Line("Transition Progress", (_controller.transitionProgress * 100f).ToString("0") + "%");
    }

    void DrawModalitySection()
    {
        Section("4. Modality Health");
        var status = _controller.latestStatus;
        if (status == null || status.modality_names == null)
        {
            GUILayout.Label("Waiting for Python status snapshot...", _bodyStyle);
            Line("Window Countdown", FormatCountdown(_controller.tenSecondWindowCountdownSeconds));
            return;
        }

        Line("10-second Window", status.warmup_complete ? "ready for decisions" : "warming up");
        Line("Window Countdown", FormatCountdown(_controller.tenSecondWindowCountdownSeconds));
        Line("Headset Worn", HeadsetText(status));
        Line("EEG LSL", EegStreamText(status));

        for (var i = 0; i < status.modality_names.Length; i++)
        {
            var coverage = status.modality_coverage_values != null && i < status.modality_coverage_values.Length
                ? status.modality_coverage_values[i]
                : float.NaN;
            var age = status.modality_age_values != null && i < status.modality_age_values.Length
                ? status.modality_age_values[i]
                : float.NaN;
            Line(status.modality_names[i], "coverage " + FormatCoverage(coverage) + " | last sample " + FormatAge(age));
        }
    }

    void DrawModelSection()
    {
        Section("5. Model And Prediction");
        var status = _controller.latestStatus;
        if (status == null)
        {
            GUILayout.Label("Model details appear after Python preflight and status delivery.", _bodyStyle);
            return;
        }

        Line("Bundle", EmptyAsDash(status.model_bundle_id));
        Line("Version", EmptyAsDash(status.model_version));
        Line("Variant", EmptyAsDash(status.model_variant));
        Line("Relaxation", FormatFloat(status.relaxation));
        Line("Discomfort", FormatFloat(status.discomfort));
        Line("Profile", Short(_controller.profileId, 20));
        GUILayout.Label("Verify the model with Python adaptive-model before starting a session.", _bodyStyle);
    }

    void DrawDecisionSection()
    {
        Section("6. Decision Explanation");
        var status = _controller.latestStatus;
        if (status == null)
        {
            GUILayout.Label(_controller.lastReason, _bodyStyle);
            return;
        }

        Line("Next Decision", status.next_decision_in_seconds.ToString("0.0") + " s");
        Line("Last Command", Short(status.last_command_id, 18));
        if (status.candidate_conditions != null && status.candidate_utility_values != null)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < status.candidate_conditions.Length && i < status.candidate_utility_values.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append("   ");
                }

                builder.Append(status.candidate_conditions[i]);
                builder.Append(": ");
                builder.Append(status.candidate_utility_values[i].ToString("0.000"));
            }

            GUILayout.Label(builder.ToString(), _bodyStyle);
        }

        GUILayout.Label(JoinReasons(status.reasons, _controller.lastReason), _bodyStyle);
    }

    void DrawTransportSection()
    {
        Section("7. Transport And Audit");
        var status = _controller.latestStatus;
        Line("Protocol", "adaptive-control-v1 / UDP 5055 <-> 5056");
        Line("Profile SHA", Short(_controller.profileSha256, 16));
        Line("Python State", status != null ? EmptyAsDash(status.runtime_state) : "waiting for connection");
        GUILayout.Label("Reason: " + _controller.lastReason, _bodyStyle);
    }

    void EnsureStyles()
    {
        if (_titleStyle != null)
        {
            return;
        }

        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            wordWrap = true
        };
        _sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.55f, 0.85f, 1f) },
            margin = new RectOffset(0, 0, 10, 2)
        };
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.92f, 0.92f, 0.92f) },
            wordWrap = true
        };
        _warningStyle = new GUIStyle(_bodyStyle)
        {
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.75f, 0.25f) }
        };
    }

    void Section(string label)
    {
        GUILayout.Label(label, _sectionStyle);
    }

    void Line(string label, string value)
    {
        GUILayout.Label(label + ": " + value, _bodyStyle);
    }

    static string JoinReasons(string[] reasons, string fallback)
    {
        return AdaptiveControlController.JoinReasonLabels(reasons, fallback);
    }

    static string FormatFloat(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? "-" : value.ToString("0.000");
    }

    static string FormatCountdown(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? "-" : value.ToString("0.0") + " s";
    }

    static string FormatCoverage(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? "-" : (value * 100f).ToString("0") + "%";
    }

    static string FormatAge(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) || value < 0f
            ? "not received"
            : value.ToString("0") + " ms";
    }

    static string HeadsetText(AdaptiveStatusSnapshot status)
    {
        if (!status.headset_presence_available)
        {
            return "not reported by device";
        }

        return status.headset_worn ? "worn" : "not worn: holding current stimulus";
    }

    static string EegStreamText(AdaptiveStatusSnapshot status)
    {
        if (!status.lsl_eeg_stream_found)
        {
            return "no type=eeg LSL stream found";
        }

        return status.lsl_eeg_sample_received ? "stream found, samples received" : "stream found, waiting for first sample";
    }

    static string EmptyAsDash(string value)
    {
        return string.IsNullOrEmpty(value) ? "-" : value;
    }

    static string Short(string value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "-";
        }

        return value.Length <= length ? value : value.Substring(0, length) + "...";
    }
}
