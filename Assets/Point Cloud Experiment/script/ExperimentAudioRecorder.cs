using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

[DefaultExecutionOrder(1250)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Experiment Audio Recorder")]
public sealed class ExperimentAudioRecorder : MonoBehaviour
{
    const string WavFileName = "audio.wav";
    const string ManifestFileName = "audio_manifest.json";
    const string CenterEyeAnchorName = "CenterEyeAnchor";
    const int WavHeaderBytes = 44;
    const int BitsPerSample = 16;

    [Header("Session")]
    [SerializeField] bool _autoStartWithExperimentSession = true;
    [SerializeField] MeditationExperimentCsvLogger _experimentCsvLogger;

    [Header("Buffering")]
    [SerializeField, Min(1)] int _maxQueuedAudioBuffers = 180;

    [Header("Logging")]
    [SerializeField] bool _logLifecycle = true;

    static ExperimentAudioRecorder _instance;

    readonly object _audioLock = new object();
    readonly Queue<byte[]> _pendingAudioBuffers = new Queue<byte[]>();

    FileStream _wavStream;
    BinaryWriter _wavWriter;
    string _sessionFolderPath;
    string _wavPath;
    int _sampleRate;
    int _channels;
    int _recordingGeneration;
    int _droppedAudioBuffers;
    long _audioDataBytes;
    long _sampleFrames;
    double _startRealtime;
    double _startDspTime;
    double _firstSampleDspTime = double.NaN;
    bool _isRecording;
    bool _writeWarningLogged;
    bool _listenerWarningLogged;

    public static ExperimentAudioRecorder Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<ExperimentAudioRecorder>();
            if (_instance != null)
            {
                return _instance;
            }

            var listener = FindAudioListenerForCapture();
            var gameObject = listener != null
                ? listener.gameObject
                : new GameObject("ExperimentAudioRecorder");

            if (listener == null)
            {
                DontDestroyOnLoad(gameObject);
            }

            _instance = gameObject.GetComponent<ExperimentAudioRecorder>();
            if (_instance == null)
            {
                _instance = gameObject.AddComponent<ExperimentAudioRecorder>();
            }

            return _instance;
        }
    }

    public bool isRecording => _isRecording;
    public string wavPath => _wavPath;
    public int sampleRate => _sampleRate;
    public int channels => _channels;
    public long sampleFrames => _sampleFrames;
    public int droppedAudioBuffers => _droppedAudioBuffers;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            return;
        }

        _instance = this;
    }

    void Update()
    {
        DrainQueuedAudioBuffers();

        if (!_autoStartWithExperimentSession)
        {
            return;
        }

        var logger = ResolveExperimentCsvLogger(false);
        if (logger != null && logger.sessionActive)
        {
            if (!_isRecording)
            {
                StartRecording(logger, "auto_session_active");
            }

            return;
        }

        if (_isRecording)
        {
            StopRecording("session_inactive");
        }
    }

    void OnDisable()
    {
        StopRecording("component_disabled");
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    void OnValidate()
    {
        _maxQueuedAudioBuffers = Mathf.Max(1, _maxQueuedAudioBuffers);
    }

    [ContextMenu("Start Recording Active Session")]
    public void StartRecordingForActiveSession()
    {
        StartRecording(ResolveExperimentCsvLogger(false), "context_menu");
    }

    [ContextMenu("Stop Recording")]
    public void StopRecordingFromContextMenu()
    {
        StopRecording("context_menu");
    }

    public bool StartRecording(MeditationExperimentCsvLogger logger, string reason = "")
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[ExperimentAudioRecorder] Cannot start recording because the recorder component is disabled.", this);
            return false;
        }

        if (_isRecording)
        {
            return true;
        }

        _experimentCsvLogger = logger != null ? logger : ResolveExperimentCsvLogger(false);
        if (_experimentCsvLogger == null || !_experimentCsvLogger.sessionActive)
        {
            Debug.LogWarning("[ExperimentAudioRecorder] Cannot start recording because there is no active experiment session.", this);
            return false;
        }

        if (GetComponent<AudioListener>() == null && GetComponent<AudioSource>() == null && !_listenerWarningLogged)
        {
            _listenerWarningLogged = true;
            Debug.LogWarning(
                "[ExperimentAudioRecorder] This component is not attached to an AudioListener or AudioSource. Unity may not call OnAudioFilterRead, so audio.wav may be silent.",
                this);
        }

        _sessionFolderPath = _experimentCsvLogger.sessionFolderPath;
        _wavPath = Path.Combine(_sessionFolderPath, WavFileName);
        Directory.CreateDirectory(_sessionFolderPath);

        try
        {
            _wavStream = new FileStream(_wavPath, FileMode.Create, FileAccess.Write, FileShare.Read);
            _wavWriter = new BinaryWriter(_wavStream);
            WriteEmptyWavHeader();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[ExperimentAudioRecorder] Cannot open audio.wav for writing: " + ex.Message, this);
            CloseWavWriter(false);
            return false;
        }

        lock (_audioLock)
        {
            _pendingAudioBuffers.Clear();
            _channels = 0;
            _audioDataBytes = 0;
            _sampleFrames = 0;
            _droppedAudioBuffers = 0;
            _firstSampleDspTime = double.NaN;
        }

        _sampleRate = AudioSettings.outputSampleRate;
        _startRealtime = Time.realtimeSinceStartupAsDouble;
        _startDspTime = AudioSettings.dspTime;
        _writeWarningLogged = false;
        _recordingGeneration++;
        _isRecording = true;

        WriteManifest("started", reason);
        LogExperimentEvent("audio_recording_started", reason);

        if (_logLifecycle)
        {
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[ExperimentAudioRecorder] Recording started. sampleRate={0} wav={1}",
                    _sampleRate,
                    _wavPath),
                this);
        }

        return true;
    }

    public void StopRecording(string reason = "")
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        _recordingGeneration++;
        DrainQueuedAudioBuffers();
        LogExperimentEvent("audio_recording_stopped", reason);
        WriteManifest("stopped", reason);
        CloseWavWriter(true);

        if (_logLifecycle)
        {
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[ExperimentAudioRecorder] Recording stopped. sampleFrames={0} bytes={1} droppedBuffers={2} reason={3}",
                    _sampleFrames,
                    _audioDataBytes,
                    _droppedAudioBuffers,
                    reason),
                this);
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (!_isRecording || data == null || data.Length == 0 || channels <= 0)
        {
            return;
        }

        var generation = _recordingGeneration;
        var pcm = ConvertToPcm16(data);
        var sampleFramesInBuffer = data.Length / channels;
        lock (_audioLock)
        {
            if (!_isRecording || generation != _recordingGeneration)
            {
                return;
            }

            if (_channels == 0)
            {
                _channels = channels;
            }

            if (channels != _channels || _pendingAudioBuffers.Count >= _maxQueuedAudioBuffers)
            {
                _droppedAudioBuffers++;
                return;
            }

            if (double.IsNaN(_firstSampleDspTime))
            {
                _firstSampleDspTime = AudioSettings.dspTime;
            }

            _pendingAudioBuffers.Enqueue(pcm);
            _audioDataBytes += pcm.LongLength;
            _sampleFrames += sampleFramesInBuffer;
        }
    }

    void DrainQueuedAudioBuffers()
    {
        if (_wavWriter == null)
        {
            return;
        }

        while (true)
        {
            byte[] buffer;
            lock (_audioLock)
            {
                if (_pendingAudioBuffers.Count == 0)
                {
                    break;
                }

                buffer = _pendingAudioBuffers.Dequeue();
            }

            try
            {
                _wavWriter.Write(buffer);
            }
            catch (Exception ex)
            {
                if (!_writeWarningLogged)
                {
                    _writeWarningLogged = true;
                    Debug.LogWarning("[ExperimentAudioRecorder] Failed to write audio data: " + ex.Message, this);
                }

                break;
            }
        }

        _wavWriter.Flush();
    }

    void WriteEmptyWavHeader()
    {
        for (var i = 0; i < WavHeaderBytes; i++)
        {
            _wavWriter.Write((byte)0);
        }
    }

    void CloseWavWriter(bool finalizeHeader)
    {
        if (_wavWriter == null)
        {
            return;
        }

        if (finalizeHeader)
        {
            WriteWavHeader();
        }

        _wavWriter.Flush();
        _wavWriter.Dispose();
        _wavWriter = null;
        _wavStream = null;
    }

    void WriteWavHeader()
    {
        var channels = Mathf.Max(1, _channels);
        var sampleRate = Mathf.Max(1, _sampleRate);
        var byteRate = sampleRate * channels * BitsPerSample / 8;
        var blockAlign = channels * BitsPerSample / 8;
        var dataBytes = Math.Min((long)int.MaxValue, _audioDataBytes);
        var riffSize = 36 + dataBytes;

        _wavStream.Seek(0, SeekOrigin.Begin);
        WriteAscii("RIFF");
        _wavWriter.Write((int)riffSize);
        WriteAscii("WAVE");
        WriteAscii("fmt ");
        _wavWriter.Write(16);
        _wavWriter.Write((short)1);
        _wavWriter.Write((short)channels);
        _wavWriter.Write(sampleRate);
        _wavWriter.Write(byteRate);
        _wavWriter.Write((short)blockAlign);
        _wavWriter.Write((short)BitsPerSample);
        WriteAscii("data");
        _wavWriter.Write((int)dataBytes);
    }

    void WriteAscii(string value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            _wavWriter.Write((byte)value[i]);
        }
    }

    MeditationExperimentCsvLogger ResolveExperimentCsvLogger(bool createIfMissing)
    {
        if (_experimentCsvLogger != null)
        {
            return _experimentCsvLogger;
        }

        _experimentCsvLogger = FindObjectOfType<MeditationExperimentCsvLogger>();
        if (_experimentCsvLogger == null && createIfMissing)
        {
            _experimentCsvLogger = MeditationExperimentCsvLogger.Instance;
        }

        return _experimentCsvLogger;
    }

    static AudioListener FindAudioListenerForCapture()
    {
        var centerEye = GameObject.Find(CenterEyeAnchorName);
        if (centerEye != null &&
            centerEye.TryGetComponent<AudioListener>(out var centerEyeListener) &&
            centerEyeListener.enabled &&
            centerEyeListener.gameObject.activeInHierarchy)
        {
            return centerEyeListener;
        }

        var mainCamera = Camera.main;
        if (mainCamera != null &&
            mainCamera.TryGetComponent<AudioListener>(out var mainCameraListener) &&
            mainCameraListener.enabled &&
            mainCameraListener.gameObject.activeInHierarchy)
        {
            return mainCameraListener;
        }

        var listeners = FindObjectsOfType<AudioListener>();
        for (var i = 0; i < listeners.Length; i++)
        {
            var listener = listeners[i];
            if (listener != null && listener.enabled && listener.gameObject.activeInHierarchy)
            {
                return listener;
            }
        }

        return listeners.Length > 0 ? listeners[0] : null;
    }

    void LogExperimentEvent(string eventType, string reason)
    {
        if (_experimentCsvLogger == null || !_experimentCsvLogger.sessionActive)
        {
            return;
        }

        _experimentCsvLogger.LogEvent(new MeditationExperimentCsvLogger.Row
        {
            eventType = eventType,
            eventRealtime = Time.realtimeSinceStartupAsDouble,
            notes = string.Format(
                CultureInfo.InvariantCulture,
                "reason={0}; wav={1}; sampleRate={2}; channels={3}; sampleFrames={4}; dspStart={5:0.######}; firstSampleDsp={6:0.######}; droppedAudioBuffers={7}",
                reason,
                _wavPath,
                _sampleRate,
                _channels,
                _sampleFrames,
                _startDspTime,
                _firstSampleDspTime,
                _droppedAudioBuffers)
        });
    }

    void WriteManifest(string state, string reason)
    {
        if (string.IsNullOrEmpty(_sessionFolderPath))
        {
            return;
        }

        var path = Path.Combine(_sessionFolderPath, ManifestFileName);
        var durationSeconds = _sampleRate > 0
            ? _sampleFrames / (double)_sampleRate
            : 0.0;
        var content = string.Join(Environment.NewLine,
            "{",
            "  \"sessionId\": " + JsonString(_experimentCsvLogger != null ? _experimentCsvLogger.sessionId : string.Empty) + ",",
            "  \"state\": " + JsonString(state) + ",",
            "  \"reason\": " + JsonString(reason) + ",",
            "  \"path\": " + JsonString(_wavPath) + ",",
            "  \"format\": \"pcm_s16le\",",
            "  \"sampleRate\": " + _sampleRate.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"channels\": " + _channels.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"bitsPerSample\": " + BitsPerSample.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"sampleFrames\": " + _sampleFrames.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"durationSeconds\": " + durationSeconds.ToString("0.######", CultureInfo.InvariantCulture) + ",",
            "  \"startRealtime\": " + _startRealtime.ToString("0.######", CultureInfo.InvariantCulture) + ",",
            "  \"startDspTime\": " + _startDspTime.ToString("0.######", CultureInfo.InvariantCulture) + ",",
            "  \"firstSampleDspTime\": " + JsonDouble(_firstSampleDspTime) + ",",
            "  \"audioDataBytes\": " + _audioDataBytes.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"droppedAudioBuffers\": " + _droppedAudioBuffers.ToString(CultureInfo.InvariantCulture),
            "}");

        File.WriteAllText(path, content);
    }

    static byte[] ConvertToPcm16(float[] data)
    {
        var bytes = new byte[data.Length * 2];
        var byteIndex = 0;
        for (var i = 0; i < data.Length; i++)
        {
            var sample = data[i];
            if (sample > 1f)
            {
                sample = 1f;
            }
            else if (sample < -1f)
            {
                sample = -1f;
            }

            var pcm = (short)Math.Round(sample * short.MaxValue);
            bytes[byteIndex++] = (byte)(pcm & 0xff);
            bytes[byteIndex++] = (byte)((pcm >> 8) & 0xff);
        }

        return bytes;
    }

    static string JsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    static string JsonDouble(double value)
    {
        return double.IsNaN(value)
            ? "null"
            : value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
