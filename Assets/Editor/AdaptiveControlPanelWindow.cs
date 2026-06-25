using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Dedicated desktop/editor control surface for Adaptive Control.
/// Unlike the optional runtime HUD, this window never appears in the participant's Game view.
/// </summary>
public sealed class AdaptiveControlPanelWindow : EditorWindow
{
    Vector2 _scroll;
    AdaptiveControlController _controller;

    [MenuItem("Adaptive Control/Control Panel")]
    public static void Open()
    {
        var window = GetWindow<AdaptiveControlPanelWindow>("Adaptive Control");
        window.minSize = new Vector2(420f, 540f);
        window.Show();
    }

    void OnEnable()
    {
        RefreshController();
    }

    void OnInspectorUpdate()
    {
        RefreshController();
        Repaint();
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("ADAPTIVE CONTROL", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Operator-only control panel. This window is separate from the formal Water Lilies operator panel and is never shown in the participant Game view.",
            MessageType.Info);

        if (_controller == null)
        {
            EditorGUILayout.HelpBox(
                "No AdaptiveControlController was found. Open the Meditation scene and run Adaptive Control > Configure.",
                MessageType.Warning);
            if (GUILayout.Button("Configure Adaptive Control"))
            {
                AdaptiveControlSetup.Configure();
                RefreshController();
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        DrawSessionControls();
        DrawReadinessSection();
        DrawVfxSection();
        DrawModalitySection();
        DrawModelSection();
        DrawDecisionSection();
        DrawTransportSection();
        EditorGUILayout.EndScrollView();
    }

    void DrawSessionControls()
    {
        Section("Session Control");
        EditorGUILayout.LabelField("State", _controller.runtimeState.ToString());
        EditorGUILayout.LabelField("Session", Short(_controller.sessionId, 22));
        var canStart = _controller.CanStartControl(out var startGateReason);
        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(_controller.isRunning || !canStart))
        {
            if (GUILayout.Button("Start Control", GUILayout.Height(28f)))
            {
                _controller.StartControl();
            }
        }

        using (new EditorGUI.DisabledScope(!_controller.isRunning))
        {
            if (GUILayout.Button("Stop Control", GUILayout.Height(28f)))
            {
                _controller.StopControl();
            }
        }

        var oldColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.42f, 0.42f);
        if (GUILayout.Button("Emergency Stop", GUILayout.Height(28f)))
        {
            _controller.EmergencyStop();
        }

        GUI.backgroundColor = oldColor;
        EditorGUILayout.EndHorizontal();
        if (!_controller.isRunning && !canStart)
        {
            EditorGUILayout.LabelField("Start Gate", startGateReason, EditorStyles.wordWrappedMiniLabel);
        }

        var dashboard = _controller.GetComponent<AdaptiveControlRuntimeDashboard>();
        if (dashboard != null)
        {
            var showHud = EditorGUILayout.ToggleLeft("Show optional Game View HUD", dashboard.isVisible);
            if (showHud != dashboard.isVisible)
            {
                Undo.RecordObject(dashboard, "Toggle Adaptive Control HUD");
                dashboard.SetVisible(showHud);
                EditorUtility.SetDirty(dashboard);
            }
        }
    }

    void DrawReadinessSection()
    {
        Section("Readiness Check");
        using (new EditorGUI.DisabledScope(!Application.isPlaying || _controller.isRunning || _controller.readinessCheckInProgress))
        {
            if (GUILayout.Button(_controller.readinessCheckInProgress ? "Checking Readiness..." : "Check Readiness", GUILayout.Height(26f)))
            {
                _controller.RequestReadinessCheck();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Play Mode", "Enter Play Mode before checking readiness.");
            return;
        }

        _controller.CanStartControl(out var gateReason);
        EditorGUILayout.LabelField("Start Gate", gateReason, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.LabelField(
            "Headset",
            _controller.latestHeadsetPresenceAvailable
                ? (_controller.latestHeadsetWorn ? "worn" : "not worn")
                : "not reported by device");
        EditorGUILayout.LabelField(
            "Head Tracking",
            _controller.hasReadinessTrackingSample
                ? ReadyText(_controller.latestHeadTrackingReady, "head pose available", "head pose missing")
                : "not checked");
        EditorGUILayout.LabelField(
            "Eye Tracking",
            _controller.hasReadinessTrackingSample
                ? ReadyText(_controller.latestEyeTrackingReady, "eye tracking available", "eye tracking missing") +
                  " | source " + Dash(_controller.latestGazeSource)
                : "not checked");

        var readiness = _controller.latestReadiness;
        if (readiness == null)
        {
            EditorGUILayout.LabelField(
                "Python / EEG / ECG",
                _controller.readinessCheckInProgress ? "waiting for Python readiness response..." : "not checked");
            return;
        }

        if (!_controller.readinessResultIsFresh)
        {
            EditorGUILayout.LabelField("Last Check Age", FormatCountdown(_controller.readinessResultAgeSeconds));
            EditorGUILayout.LabelField("Python / EEG / ECG", "last result is stale; click Check Readiness again");
            return;
        }

        EditorGUILayout.LabelField("Last Check Age", FormatCountdown(_controller.readinessResultAgeSeconds));
        EditorGUILayout.LabelField("Python Model", ReadyText(readiness.python_ready && readiness.model_ready, "ready", "not ready"));
        EditorGUILayout.LabelField(
            "EEG/ECG LSL",
            ReadyText(readiness.eeg_ready && readiness.ecg_ready, "stream and samples ready", "not ready"));
        EditorGUILayout.LabelField(
            "Physio Channels",
            readiness.physio_sample_channel_count + " / " + readiness.expected_min_channel_count);
        EditorGUILayout.LabelField("Reason", AdaptiveControlController.JoinReasonLabels(readiness.reasons, "-"), EditorStyles.wordWrappedMiniLabel);
    }

    void DrawVfxSection()
    {
        Section("VFX State");
        EditorGUILayout.LabelField("Current Condition", Dash(_controller.currentConditionId));
        EditorGUILayout.LabelField("Target Condition", Dash(_controller.targetConditionId));
        EditorGUILayout.LabelField("Intensity", Format(_controller.currentIntensity));
        EditorGUILayout.LabelField("Frequency", Format(_controller.currentFrequency));
        EditorGUILayout.LabelField("Transition Progress", (_controller.transitionProgress * 100f).ToString("0") + "%");
    }

    void DrawModalitySection()
    {
        Section("Modality Health");
        var status = _controller.latestStatus;
        if (status == null || status.modality_names == null || status.modality_names.Length == 0)
        {
            EditorGUILayout.LabelField("Waiting for Python status snapshot...", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Window Countdown", FormatCountdown(_controller.tenSecondWindowCountdownSeconds));
            return;
        }

        EditorGUILayout.LabelField(
            "10-second Window",
            status.warmup_complete ? "Ready for decisions" : "Warming up: collecting the first complete window");
        EditorGUILayout.LabelField("Window Countdown", FormatCountdown(_controller.tenSecondWindowCountdownSeconds));
        EditorGUILayout.LabelField("Headset Worn", HeadsetText(status));
        EditorGUILayout.LabelField("EEG LSL", EegStreamText(status));

        for (var i = 0; i < status.modality_names.Length; i++)
        {
            var coverage = ValueAt(status.modality_coverage_values, i);
            var age = ValueAt(status.modality_age_values, i);
            EditorGUILayout.LabelField(
                status.modality_names[i],
                "coverage " + FormatCoverage(coverage) + " | last sample " + FormatAge(age));
        }
    }

    void DrawModelSection()
    {
        Section("Model And Prediction");
        var status = _controller.latestStatus;
        if (status == null)
        {
            EditorGUILayout.LabelField("Model details appear after Python preflight and status delivery.", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.LabelField("Bundle", Dash(status.model_bundle_id));
        EditorGUILayout.LabelField("Version", Dash(status.model_version));
        EditorGUILayout.LabelField("Variant", Dash(status.model_variant));
        EditorGUILayout.LabelField("Prediction Source", Dash(status.prediction_source));
        EditorGUILayout.LabelField("Supervision", Dash(status.model_supervision));
        EditorGUILayout.LabelField("Motion Source", Dash(status.motion_source));
        EditorGUILayout.LabelField(
            "Model Inputs",
            status.model_available_feature_count + " / " + status.model_input_feature_count +
            " available | missing " + status.model_missing_feature_count);
        EditorGUILayout.LabelField("Model Modalities", Join(status.model_modalities_used, "-"));
        EditorGUILayout.LabelField("Relaxation", Format(status.relaxation));
        EditorGUILayout.LabelField("Discomfort", Format(status.discomfort));
        EditorGUILayout.LabelField("Profile", Short(_controller.profileId, 22));
        EditorGUILayout.HelpBox(
            "Select and verify the model with Python adaptive-model before starting a session. Running sessions do not support hot-swapping.",
            MessageType.None);
    }

    void DrawDecisionSection()
    {
        Section("Decision Explanation");
        var status = _controller.latestStatus;
        if (status == null)
        {
            EditorGUILayout.LabelField(_controller.lastReason, EditorStyles.wordWrappedMiniLabel);
            return;
        }

        EditorGUILayout.LabelField("Next Decision", status.next_decision_in_seconds.ToString("0.0") + " s");
        EditorGUILayout.LabelField("Last Command", Short(status.last_command_id, 22));
        DrawCandidateGrid(status);
        EditorGUILayout.LabelField("Reason", AdaptiveControlController.JoinReasonLabels(status.reasons, _controller.lastReason), EditorStyles.wordWrappedMiniLabel);
    }

    void DrawTransportSection()
    {
        Section("Transport And Audit");
        var status = _controller.latestStatus;
        EditorGUILayout.LabelField("Python State", status != null ? Dash(status.runtime_state) : "Waiting for connection");
        EditorGUILayout.LabelField("Protocol", "adaptive-control-v1 / UDP 5055 <-> 5056");
        EditorGUILayout.LabelField("Profile SHA", Short(_controller.profileSha256, 22));
        EditorGUILayout.LabelField("Latest Reason", _controller.lastReason, EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button("Copy Diagnostic Summary"))
        {
            EditorGUIUtility.systemCopyBuffer = BuildDiagnosticSummary(status);
        }
    }

    void DrawCandidateGrid(AdaptiveStatusSnapshot status)
    {
        var utilities = new float[10];
        var hasUtility = new bool[10];
        if (status.candidate_conditions != null && status.candidate_utility_values != null)
        {
            for (var i = 0; i < status.candidate_conditions.Length && i < status.candidate_utility_values.Length; i++)
            {
                if (AdaptiveControlProfile.TryNormalizeCondition(status.candidate_conditions[i], out var condition))
                {
                    var index = int.Parse(condition.Substring(1));
                    utilities[index] = status.candidate_utility_values[i];
                    hasUtility[index] = true;
                }
            }
        }

        for (var row = 0; row < 3; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (var column = 0; column < 3; column++)
            {
                var index = row * 3 + column + 1;
                var condition = "C" + index;
                var oldColor = GUI.backgroundColor;
                if (condition == _controller.currentConditionId)
                {
                    GUI.backgroundColor = new Color(0.45f, 0.78f, 1f);
                }
                else if (condition == _controller.targetConditionId)
                {
                    GUI.backgroundColor = new Color(0.58f, 0.96f, 0.62f);
                }

                GUILayout.Button(condition + "\n" + (hasUtility[index] ? utilities[index].ToString("0.000") : "-"), GUILayout.Height(40f));
                GUI.backgroundColor = oldColor;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    void RefreshController()
    {
        if (_controller != null)
        {
            return;
        }

        var all = Resources.FindObjectsOfTypeAll<AdaptiveControlController>();
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.scene.IsValid())
            {
                _controller = all[i];
                return;
            }
        }
    }

    static float ValueAt(float[] values, int index)
    {
        return values != null && index < values.Length ? values[index] : float.NaN;
    }

    static string BuildDiagnosticSummary(AdaptiveStatusSnapshot status)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Adaptive Control");
        builder.AppendLine("Status: " + (status != null ? status.runtime_state : "waiting"));
        builder.AppendLine("Model: " + (status != null ? status.model_bundle_id + " / " + status.model_variant : "-"));
        builder.AppendLine("Prediction Source: " + (status != null ? Dash(status.prediction_source) : "-"));
        builder.AppendLine("Model Inputs: " + (status != null ? status.model_available_feature_count + "/" + status.model_input_feature_count : "-"));
        builder.AppendLine("Current: " + (status != null ? status.current_condition : "-"));
        builder.AppendLine("Reason: " + (status != null ? AdaptiveControlController.JoinReasonLabels(status.reasons, "-") : "-"));
        return builder.ToString();
    }

    static string Join(string[] values, string fallback)
    {
        return values != null && values.Length > 0 ? string.Join(", ", values) : fallback;
    }

    static string ReadyText(bool ready, string readyText, string notReadyText)
    {
        return ready ? readyText : notReadyText;
    }

    static string Dash(string value)
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

    static string Format(float value)
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
}
