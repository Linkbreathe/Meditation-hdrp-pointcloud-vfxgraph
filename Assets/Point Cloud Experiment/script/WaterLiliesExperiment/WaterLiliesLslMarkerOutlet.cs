using System;
using LSL;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies LSL Marker Outlet")]
public sealed class WaterLiliesLslMarkerOutlet : MonoBehaviour
{
    [Header("Stream")]
    [SerializeField] bool _publishMarkers = true;
    [SerializeField] string _streamName = "WaterLiliesExperimentMarkers";
    [SerializeField] string _streamType = "Markers";
    [SerializeField] string _sourceId = "";

    [Header("Diagnostics")]
    [SerializeField] bool _logOutletCreated = true;
    [SerializeField] bool _logPublishFailures = true;

    StreamOutlet _outlet;
    readonly string[] _sample = { "" };
    bool _unavailableWarningLogged;

    public bool publishMarkers
    {
        get => _publishMarkers;
        set => _publishMarkers = value;
    }

    public bool isReady => _outlet != null;

    void OnEnable()
    {
        if (_publishMarkers)
        {
            TryEnsureOutlet();
        }
    }

    public void PushEvent(WaterLiliesExperimentLogRow row)
    {
        if (!_publishMarkers || row == null)
        {
            return;
        }

        if (!TryEnsureOutlet())
        {
            return;
        }

        _sample[0] = WaterLiliesExperimentLogger.ToJsonLine(row);
        try
        {
            _outlet.push_sample(_sample, LSL.LSL.local_clock());
        }
        catch (Exception exception)
        {
            DisposeOutlet();
            LogUnavailable("Failed to publish LSL marker.", exception);
        }
    }

    void OnDisable()
    {
        DisposeOutlet();
    }

    bool TryEnsureOutlet()
    {
        if (_outlet != null)
        {
            return true;
        }

        try
        {
            var streamInfo = new StreamInfo(
                ResolveStreamName(),
                ResolveStreamType(),
                1,
                LSL.LSL.IRREGULAR_RATE,
                channel_format_t.cf_string,
                ResolveSourceId());
            _outlet = new StreamOutlet(streamInfo);
            _unavailableWarningLogged = false;

            if (_logOutletCreated)
            {
                Debug.Log(
                    "[WaterLiliesLslMarkerOutlet] Created LSL marker stream \"" + ResolveStreamName() + "\".",
                    this);
            }

            return true;
        }
        catch (Exception exception)
        {
            DisposeOutlet();
            LogUnavailable("Failed to create LSL marker outlet.", exception);
            return false;
        }
    }

    string ResolveStreamName()
    {
        return string.IsNullOrWhiteSpace(_streamName)
            ? "WaterLiliesExperimentMarkers"
            : _streamName.Trim();
    }

    string ResolveStreamType()
    {
        return string.IsNullOrWhiteSpace(_streamType)
            ? "Markers"
            : _streamType.Trim();
    }

    string ResolveSourceId()
    {
        if (!string.IsNullOrWhiteSpace(_sourceId))
        {
            return _sourceId.Trim();
        }

        var hash = new Hash128();
        hash.Append(ResolveStreamName());
        hash.Append(ResolveStreamType());
        hash.Append(Application.productName);
        hash.Append(gameObject.scene.path);
        return hash.ToString();
    }

    void LogUnavailable(string message, Exception exception)
    {
        if (!_logPublishFailures || _unavailableWarningLogged)
        {
            return;
        }

        _unavailableWarningLogged = true;
        Debug.LogWarning(
            "[WaterLiliesLslMarkerOutlet] " + message + " LSL markers will be skipped. " +
            exception.GetType().Name + ": " + exception.Message,
            this);
    }

    void DisposeOutlet()
    {
        if (_outlet == null)
        {
            return;
        }

        _outlet.Dispose();
        _outlet = null;
    }
}
