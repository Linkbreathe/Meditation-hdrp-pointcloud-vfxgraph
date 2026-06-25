using UnityEngine;

/// <summary>
/// Reuses the existing Water Lilies head/eye sampler and sends its values to the
/// adaptive control controller at a bounded 10 Hz cadence.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Adaptive Control/Sensor Publisher")]
public sealed class AdaptiveControlSensorPublisher : MonoBehaviour
{
    [SerializeField] AdaptiveControlController _controller;
    [SerializeField] WaterLiliesTrackingSampler _trackingSampler;
    [SerializeField, Min(1f)] float _sampleHz = 10f;

    double _nextSampleRealtime;

    void Reset()
    {
        _controller = GetComponent<AdaptiveControlController>();
        _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
    }

    void Awake()
    {
        if (_controller == null)
        {
            _controller = GetComponent<AdaptiveControlController>();
        }

        if (_trackingSampler == null)
        {
            _trackingSampler = GetComponent<WaterLiliesTrackingSampler>();
        }
    }

    void Update()
    {
        if (_controller == null || _trackingSampler == null || !_controller.isRunning)
        {
            return;
        }

        var now = Time.realtimeSinceStartupAsDouble;
        var interval = 1.0 / Mathf.Max(1f, _sampleHz);
        if (now < _nextSampleRealtime)
        {
            return;
        }

        _nextSampleRealtime = now + interval;
        _controller.PublishSensorSample(_trackingSampler.Capture());
    }
}
