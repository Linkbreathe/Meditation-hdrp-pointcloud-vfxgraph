using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Operator-only live EEG/ECG diagnostics for the adaptive-control Python stream.
/// </summary>
public sealed class AdaptivePhysioMonitorWindow : EditorWindow
{
    enum SignalViewMode
    {
        Both,
        Raw,
        Filtered
    }

    static readonly Color BackgroundColor = new Color(0.11f, 0.11f, 0.11f);
    static readonly Color GridColor = new Color(1f, 1f, 1f, 0.08f);
    static readonly Color RawColor = new Color(0.30f, 0.68f, 1f);
    static readonly Color FilteredColor = new Color(1f, 0.68f, 0.24f);
    static readonly Color ZeroColor = new Color(1f, 1f, 1f, 0.20f);

    Vector2 _scroll;
    AdaptiveControlController _controller;
    AdaptivePhysioSnapshot _pausedSnapshot;
    bool _paused;
    bool _autoScale = true;
    SignalViewMode _viewMode = SignalViewMode.Both;

    [MenuItem("Adaptive Control/Physio Monitor")]
    public static void Open()
    {
        var window = GetWindow<AdaptivePhysioMonitorWindow>("Physio Monitor");
        window.minSize = new Vector2(760f, 620f);
        window.Show();
    }

    void OnEnable()
    {
        RefreshController();
    }

    void OnInspectorUpdate()
    {
        RefreshController();
        if (_controller != null && Application.isPlaying)
        {
            _controller.EnsureTransportOpen(out _);
        }

        Repaint();
    }

    void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("ADAPTIVE PHYSIO MONITOR", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Operator-only EEG/ECG monitor. This window is never shown in the participant Game view.",
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

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to listen for Python physio diagnostics.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            return;
        }

        var listening = _controller.EnsureTransportOpen(out var listenReason);
        DrawToolbar();
        EditorGUILayout.LabelField("Transport", listening ? listenReason : listenReason, EditorStyles.wordWrappedMiniLabel);

        var snapshot = _paused ? _pausedSnapshot : _controller.latestPhysioSnapshot;
        if (snapshot == null)
        {
            EditorGUILayout.LabelField("Waiting for AdaptivePhysioSnapshot from Python...", EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawOverview(snapshot);
        DrawWaveforms(snapshot);
        DrawStatsTable(snapshot);
        EditorGUILayout.EndScrollView();
    }

    void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            var nextPaused = GUILayout.Toggle(_paused, "Pause", EditorStyles.toolbarButton, GUILayout.Width(64f));
            if (nextPaused != _paused)
            {
                _paused = nextPaused;
                _pausedSnapshot = _paused ? _controller.latestPhysioSnapshot : null;
            }

            _autoScale = GUILayout.Toggle(_autoScale, "Auto Scale", EditorStyles.toolbarButton, GUILayout.Width(86f));
            GUILayout.Label("View", GUILayout.Width(34f));
            _viewMode = (SignalViewMode)EditorGUILayout.EnumPopup(_viewMode, GUILayout.Width(88f));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Control Panel", EditorStyles.toolbarButton, GUILayout.Width(98f)))
            {
                AdaptiveControlPanelWindow.Open();
            }
        }
    }

    void DrawOverview(AdaptivePhysioSnapshot snapshot)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Stream", StreamText(snapshot));
        EditorGUILayout.LabelField("Sample Age", FormatAge(snapshot.sample_age_ms));
        EditorGUILayout.LabelField("Window", snapshot.window_seconds.ToString("0.0") + " s / " + snapshot.sample_count + " samples");
        EditorGUILayout.LabelField("Reasons", AdaptiveControlController.JoinReasonLabels(snapshot.reasons, "-"), EditorStyles.wordWrappedMiniLabel);
    }

    void DrawWaveforms(AdaptivePhysioSnapshot snapshot)
    {
        Section("Waveforms");
        if (snapshot.channels == null || snapshot.channels.Length == 0)
        {
            EditorGUILayout.LabelField("No physio samples available in the current window.", EditorStyles.miniLabel);
            return;
        }

        for (var i = 0; i < snapshot.channels.Length; i++)
        {
            var channel = snapshot.channels[i];
            EditorGUILayout.LabelField(ChannelLabel(channel), EditorStyles.boldLabel);
            var rect = GUILayoutUtility.GetRect(10f, 92f, GUILayout.ExpandWidth(true));
            DrawWaveformRect(rect, channel);
        }
    }

    void DrawStatsTable(AdaptivePhysioSnapshot snapshot)
    {
        Section("Statistics");
        DrawStatsHeader();
        if (snapshot.channels == null)
        {
            return;
        }

        for (var i = 0; i < snapshot.channels.Length; i++)
        {
            var channel = snapshot.channels[i];
            DrawStatsRow(channel.name + " raw", channel.raw);
            DrawStatsRow(channel.name + " filtered", channel.filtered);
        }
    }

    void DrawWaveformRect(Rect rect, AdaptivePhysioChannelSnapshot channel)
    {
        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        EditorGUI.DrawRect(rect, BackgroundColor);
        var plotRect = new Rect(rect.x + 6f, rect.y + 8f, rect.width - 12f, rect.height - 16f);
        var min = 0f;
        var max = 0f;
        GetRange(channel, out min, out max);

        Handles.BeginGUI();
        Handles.color = GridColor;
        for (var i = 1; i < 4; i++)
        {
            var y = Mathf.Lerp(plotRect.yMin, plotRect.yMax, i / 4f);
            Handles.DrawLine(new Vector3(plotRect.xMin, y), new Vector3(plotRect.xMax, y));
        }

        if (min < 0f && max > 0f)
        {
            var zeroY = ValueToY(plotRect, 0f, min, max);
            Handles.color = ZeroColor;
            Handles.DrawLine(new Vector3(plotRect.xMin, zeroY), new Vector3(plotRect.xMax, zeroY));
        }

        if (_viewMode == SignalViewMode.Both || _viewMode == SignalViewMode.Raw)
        {
            DrawSeries(plotRect, channel.raw_values, min, max, RawColor);
        }

        if (_viewMode == SignalViewMode.Both || _viewMode == SignalViewMode.Filtered)
        {
            DrawSeries(plotRect, channel.filtered_values, min, max, FilteredColor);
        }

        Handles.EndGUI();
        GUI.Label(new Rect(rect.x + 8f, rect.y + 4f, 180f, 18f), RangeText(min, max), EditorStyles.miniLabel);
    }

    void GetRange(AdaptivePhysioChannelSnapshot channel, out float min, out float max)
    {
        if (!_autoScale)
        {
            var halfRange = string.Equals(channel.source, "ECG", StringComparison.OrdinalIgnoreCase) ? 2500f : 500f;
            min = -halfRange;
            max = halfRange;
            return;
        }

        var values = new List<float>();
        if (_viewMode == SignalViewMode.Both || _viewMode == SignalViewMode.Raw)
        {
            AddFinite(values, channel.raw_values);
        }

        if (_viewMode == SignalViewMode.Both || _viewMode == SignalViewMode.Filtered)
        {
            AddFinite(values, channel.filtered_values);
        }

        if (values.Count == 0)
        {
            min = -1f;
            max = 1f;
            return;
        }

        min = values[0];
        max = values[0];
        for (var i = 1; i < values.Count; i++)
        {
            min = Mathf.Min(min, values[i]);
            max = Mathf.Max(max, values[i]);
        }

        if (Mathf.Abs(max - min) < 1e-5f)
        {
            min -= 1f;
            max += 1f;
        }
        else
        {
            var padding = (max - min) * 0.08f;
            min -= padding;
            max += padding;
        }
    }

    static void DrawSeries(Rect rect, float[] values, float min, float max, Color color)
    {
        if (values == null || values.Length < 2)
        {
            return;
        }

        var points = new Vector3[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var x = Mathf.Lerp(rect.xMin, rect.xMax, i / (float)(values.Length - 1));
            var y = ValueToY(rect, values[i], min, max);
            points[i] = new Vector3(x, y);
        }

        Handles.color = color;
        Handles.DrawAAPolyLine(2f, points);
    }

    static float ValueToY(Rect rect, float value, float min, float max)
    {
        var t = Mathf.InverseLerp(min, max, value);
        return Mathf.Lerp(rect.yMax, rect.yMin, Mathf.Clamp01(t));
    }

    static void AddFinite(List<float> output, float[] values)
    {
        if (values == null)
        {
            return;
        }

        for (var i = 0; i < values.Length; i++)
        {
            if (!float.IsNaN(values[i]) && !float.IsInfinity(values[i]))
            {
                output.Add(values[i]);
            }
        }
    }

    void RefreshController()
    {
        if (_controller != null && _controller.gameObject != null && _controller.gameObject.scene.IsValid())
        {
            return;
        }

        _controller = null;
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

    static void Section(string title)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    static void DrawStatsHeader()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Channel", EditorStyles.miniBoldLabel, GUILayout.Width(110f));
            GUILayout.Label("n", EditorStyles.miniBoldLabel, GUILayout.Width(50f));
            GUILayout.Label("Mean", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label("Std", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label("Min", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label("Max", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label("RMS", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
            GUILayout.Label("P-P", EditorStyles.miniBoldLabel, GUILayout.Width(78f));
        }
    }

    static void DrawStatsRow(string label, AdaptivePhysioStats stats)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label(label, GUILayout.Width(110f));
            GUILayout.Label(stats != null ? stats.sample_count.ToString() : "-", GUILayout.Width(50f));
            GUILayout.Label(Stat(stats, value => value.mean), GUILayout.Width(78f));
            GUILayout.Label(Stat(stats, value => value.std), GUILayout.Width(78f));
            GUILayout.Label(Stat(stats, value => value.min), GUILayout.Width(78f));
            GUILayout.Label(Stat(stats, value => value.max), GUILayout.Width(78f));
            GUILayout.Label(Stat(stats, value => value.rms), GUILayout.Width(78f));
            GUILayout.Label(Stat(stats, value => value.peak_to_peak), GUILayout.Width(78f));
        }
    }

    static string Stat(AdaptivePhysioStats stats, Func<AdaptivePhysioStats, float> selector)
    {
        if (stats == null || stats.sample_count <= 0)
        {
            return "-";
        }

        return FormatNumber(selector(stats));
    }

    static string StreamText(AdaptivePhysioSnapshot snapshot)
    {
        if (!snapshot.lsl_eeg_stream_found)
        {
            return "not found";
        }

        var name = string.IsNullOrEmpty(snapshot.stream_name) ? "unnamed" : snapshot.stream_name;
        return name + " / " + snapshot.stream_type + " / " + snapshot.channel_count + "/" +
               snapshot.expected_channel_count + " channels / " + snapshot.nominal_srate.ToString("0.#") + " Hz";
    }

    static string ChannelLabel(AdaptivePhysioChannelSnapshot channel)
    {
        var unit = string.IsNullOrEmpty(channel.unit) ? "" : " (" + channel.unit + ")";
        return channel.name + " / " + channel.source + unit;
    }

    static string RangeText(float min, float max)
    {
        return FormatNumber(min) + " .. " + FormatNumber(max);
    }

    static string FormatAge(float milliseconds)
    {
        if (float.IsNaN(milliseconds) || float.IsInfinity(milliseconds) || milliseconds < 0f)
        {
            return "-";
        }

        return milliseconds < 1000f
            ? milliseconds.ToString("0") + " ms"
            : (milliseconds / 1000f).ToString("0.0") + " s";
    }

    static string FormatNumber(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return "-";
        }

        var absolute = Mathf.Abs(value);
        if (absolute >= 10000f)
        {
            return value.ToString("0.00e+0");
        }

        if (absolute >= 100f)
        {
            return value.ToString("0");
        }

        if (absolute >= 1f)
        {
            return value.ToString("0.00");
        }

        return value.ToString("0.000");
    }

    static string Join(string[] values, string fallback)
    {
        return values != null && values.Length > 0 ? string.Join(", ", values) : fallback;
    }
}
