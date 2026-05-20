using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Meditation Experiment CSV Logger")]
public sealed class MeditationExperimentCsvLogger : MonoBehaviour
{
    const string CsvVersion = "5";
    const string CsvSeparatorDirective = "sep=,";

    [SerializeField] string _sessionFolderPrefix = "meditation_experiment";
    [SerializeField] string _dataCollectionFolderName = "data_collection";
    [SerializeField] bool _useExternalDataCollectionRoot = true;
    [SerializeField] string _externalDataCollectionRootPath = "C:/Users/linki/amaster/data collection";
    [SerializeField] bool _usePersistentDataPathOutsideEditor = true;
    [SerializeField] bool _logSessionPath = true;
    [SerializeField] bool _writeExcelSeparatorDirective = true;

    static MeditationExperimentCsvLogger _instance;

    StreamWriter _sessionWriter;
    StreamWriter _paintingsWriter;
    StreamWriter _stageValuesWriter;
    StreamWriter _choicesWriter;
    StreamWriter _eventsWriter;
    StreamWriter _summaryWriter;
    StreamWriter _eyeTrackingWriter;
    StreamWriter _videoFramesWriter;
    string _sessionId;
    string _sessionFolderPath;
    DateTimeOffset _sessionStartedAt;
    long _sessionStartedUnixMs;
    double _sessionStartedRealtime;
    bool _sessionActive;

    public static MeditationExperimentCsvLogger Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<MeditationExperimentCsvLogger>();
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject("MeditationExperimentCsvLogger");
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<MeditationExperimentCsvLogger>();
            return _instance;
        }
    }

    public string csvPath => _sessionFolderPath;
    public string sessionFolderPath => _sessionFolderPath;
    public string sessionId => _sessionId;
    public bool sessionActive => _sessionActive;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            return;
        }

        _instance = this;
    }

    void OnDisable()
    {
        CloseCsv();
    }

    public bool StartSession(string inputSource, bool forceRestart = false)
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[MeditationExperimentCsvLogger] Cannot start session because the logger component is disabled.", this);
            return false;
        }

        if (_sessionActive && !forceRestart)
        {
            Debug.LogWarning(
                "[MeditationExperimentCsvLogger] Ignoring StartSession because a CSV session is already active: " +
                _sessionId,
                this);
            return false;
        }

        CloseCsv();

        _sessionStartedAt = DateTimeOffset.Now;
        _sessionStartedUnixMs = _sessionStartedAt.ToUnixTimeMilliseconds();
        _sessionStartedRealtime = Time.realtimeSinceStartupAsDouble;
        _sessionId = CreateSessionId(_sessionStartedAt);
        _sessionActive = true;
        OpenCsvSet();

        WriteLine(_sessionWriter, string.Join(",",
            CsvEscape(_sessionId),
            CsvEscape(FormatTimestamp(_sessionStartedAt)),
            _sessionStartedUnixMs.ToString(CultureInfo.InvariantCulture),
            CsvEscape(inputSource),
            CsvEscape(CsvVersion),
            CsvEscape(_sessionFolderPath)));

        LogEvent(new Row
        {
            eventType = "session_started",
            inputSource = inputSource,
            eventRealtime = _sessionStartedRealtime,
            notes = "CSV session created"
        });

        return true;
    }

    public void Log(Row row)
    {
        LogEvent(row);
    }

    public void LogEvent(Row row)
    {
        EnsureSession();
        row = row ?? new Row();
        var eventRealtime = ResolveEventRealtime(row);
        WriteLine(_eventsWriter, ToEventCsv(row, eventRealtime));
    }

    public void LogPaintingStarted(Row row)
    {
        row = row ?? new Row();
        LogEvent(row);
    }

    public void LogPaintingCompleted(Row row)
    {
        EnsureSession();
        row = row ?? new Row();
        var eventRealtime = ResolveEventRealtime(row);
        WriteLine(_paintingsWriter, ToPaintingCsv(row, eventRealtime));
        LogEvent(row);
    }

    public void LogStageValue(Row row)
    {
        EnsureSession();
        row = row ?? new Row();
        var eventRealtime = ResolveEventRealtime(row);
        WriteLine(_stageValuesWriter, ToStageValueCsv(row, eventRealtime));
        LogEvent(row);
    }

    public void LogChoice(Row row)
    {
        EnsureSession();
        row = row ?? new Row();
        var eventRealtime = ResolveEventRealtime(row);
        WriteLine(_choicesWriter, ToChoiceCsv(row, eventRealtime));
        WriteLine(_summaryWriter, ToSummaryCsv(row, eventRealtime));
        LogEvent(row);
    }

    public bool TryLogEyeTrackingSample(EyeTrackingRow row)
    {
        if (!_sessionActive)
        {
            return false;
        }

        row = row ?? new EyeTrackingRow();
        var sampleRealtime = !double.IsNaN(row.sampleRealtime)
            ? row.sampleRealtime
            : Time.realtimeSinceStartupAsDouble;
        WriteLine(_eyeTrackingWriter, ToEyeTrackingCsv(row, sampleRealtime));
        return true;
    }

    public bool TryLogVideoFrame(VideoFrameRow row)
    {
        if (!_sessionActive)
        {
            return false;
        }

        row = row ?? new VideoFrameRow();
        var sampleRealtime = !double.IsNaN(row.sampleRealtime)
            ? row.sampleRealtime
            : Time.realtimeSinceStartupAsDouble;
        WriteLine(_videoFramesWriter, ToVideoFrameCsv(row, sampleRealtime));
        return true;
    }

    public void CloseCsv()
    {
        CloseWriter(ref _sessionWriter);
        CloseWriter(ref _paintingsWriter);
        CloseWriter(ref _stageValuesWriter);
        CloseWriter(ref _choicesWriter);
        CloseWriter(ref _eventsWriter);
        CloseWriter(ref _summaryWriter);
        CloseWriter(ref _eyeTrackingWriter);
        CloseWriter(ref _videoFramesWriter);
        _sessionActive = false;
    }

    void EnsureSession()
    {
        if (!_sessionActive)
        {
            StartSession("ImplicitStart");
        }
    }

    void OpenCsvSet()
    {
        var root = GetDataCollectionRoot();
        Directory.CreateDirectory(root);

        _sessionFolderPath = Path.Combine(root, _sessionId);
        Directory.CreateDirectory(_sessionFolderPath);

        _sessionWriter = OpenWriter("session.csv", GetSessionHeader());
        _paintingsWriter = OpenWriter("paintings.csv", GetPaintingsHeader());
        _stageValuesWriter = OpenWriter("stage_values.csv", GetStageValuesHeader());
        _choicesWriter = OpenWriter("choices.csv", GetChoicesHeader());
        _eventsWriter = OpenWriter("events.csv", GetEventsHeader());
        _summaryWriter = OpenWriter("summary.csv", GetSummaryHeader());
        _eyeTrackingWriter = OpenWriter("eye_tracking.csv", GetEyeTrackingHeader());
        _videoFramesWriter = OpenWriter("video_frames.csv", GetVideoFramesHeader());

        if (_logSessionPath)
        {
            Debug.Log("[MeditationExperimentCsvLogger] Writing CSV folder to: " + _sessionFolderPath, this);
        }
    }

    string GetWritableDataRoot()
    {
#if UNITY_EDITOR
        _ = _usePersistentDataPathOutsideEditor;
        return Application.dataPath;
#else
        return _usePersistentDataPathOutsideEditor ? Application.persistentDataPath : Application.dataPath;
#endif
    }

    string GetDataCollectionRoot()
    {
        if (_useExternalDataCollectionRoot && !string.IsNullOrWhiteSpace(_externalDataCollectionRootPath))
        {
            return NormalizePath(_externalDataCollectionRootPath);
        }

        return Path.Combine(GetWritableDataRoot(), SafeFolderName(_dataCollectionFolderName, "data_collection"));
    }

    StreamWriter OpenWriter(string fileName, string header)
    {
        var path = Path.Combine(_sessionFolderPath, fileName);
        var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, Encoding.UTF8);
        if (_writeExcelSeparatorDirective)
        {
            // Lets Excel split comma CSV correctly on systems whose list separator is semicolon.
            writer.WriteLine(CsvSeparatorDirective);
        }

        writer.WriteLine(header);
        writer.Flush();
        return writer;
    }

    static void WriteLine(StreamWriter writer, string line)
    {
        if (writer == null)
        {
            return;
        }

        writer.WriteLine(line);
        writer.Flush();
    }

    static void CloseWriter(ref StreamWriter writer)
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Dispose();
        writer = null;
    }

    string ToEventCsv(Row row, double eventRealtime)
    {
        return string.Join(",",
            CsvEscape(_sessionId),
            CsvEscape(FormatTimestamp(_sessionStartedAt)),
            _sessionStartedUnixMs.ToString(CultureInfo.InvariantCulture),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(eventRealtime))),
            UnixMs(eventRealtime),
            ElapsedMs(eventRealtime),
            Time.frameCount.ToString(CultureInfo.InvariantCulture),
            CsvEscape(row.eventType),
            CsvEscape(row.inputSource),
            I(row.paintingRunIndex),
            I(row.paintingIndex),
            CsvEscape(row.paintingId),
            CsvEscape(row.paintingName),
            I(row.stageIndex),
            I(row.stagePresetIndex),
            I(row.stageCount),
            I(row.stageValueIndex),
            I(row.stageValueCount),
            ElapsedBetweenMs(row.stageStartedRealtime, eventRealtime),
            ElapsedBetweenMs(row.promptStartedRealtime, eventRealtime),
            Timestamp(row.selectionTriggeredRealtime),
            UnixMs(row.selectionTriggeredRealtime),
            ElapsedMs(row.selectionTriggeredRealtime),
            ElapsedBetweenMs(row.promptStartedRealtime, row.selectionTriggeredRealtime),
            I(row.selectionTriggeredFrame),
            F(row.stageRangeMinimum),
            F(row.stageRangeMaximum),
            F(row.particleIntensity),
            F(row.particleFrequency),
            I(row.orbIndex),
            CsvEscape(row.orbLabel),
            F(row.orbColorIntensity),
            SecondsMs(row.selectionDwellSeconds),
            SecondsMs(row.selectionEffectSeconds),
            SecondsMs(row.choiceWaitSeconds),
            CsvEscape(row.notes));
    }

    string ToPaintingCsv(Row row, double eventRealtime)
    {
        var startRealtime = double.IsNaN(row.paintingStartedRealtime) ? eventRealtime : row.paintingStartedRealtime;
        var endRealtime = double.IsNaN(row.paintingEndedRealtime) ? eventRealtime : row.paintingEndedRealtime;

        return string.Join(",",
            CsvEscape(_sessionId),
            I(row.paintingRunIndex),
            I(row.paintingIndex),
            CsvEscape(row.paintingId),
            CsvEscape(row.paintingName),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(startRealtime))),
            UnixMs(startRealtime),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(endRealtime))),
            UnixMs(endRealtime),
            ElapsedBetweenMs(startRealtime, endRealtime),
            CsvEscape(row.stageOrder),
            CsvEscape(row.notes));
    }

    string ToStageValueCsv(Row row, double eventRealtime)
    {
        return string.Join(",",
            CsvEscape(_sessionId),
            I(row.paintingRunIndex),
            I(row.paintingIndex),
            CsvEscape(row.paintingId),
            CsvEscape(row.paintingName),
            I(row.stageIndex),
            I(row.stagePresetIndex),
            I(row.stageValueIndex),
            I(row.stageValueCount),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(eventRealtime))),
            UnixMs(eventRealtime),
            ElapsedMs(eventRealtime),
            ElapsedBetweenMs(row.stageStartedRealtime, eventRealtime),
            F(row.stageRangeMinimum),
            F(row.stageRangeMaximum),
            F(row.particleIntensity),
            F(row.particleFrequency));
    }

    string ToChoiceCsv(Row row, double eventRealtime)
    {
        var promptRealtime = double.IsNaN(row.promptStartedRealtime) ? eventRealtime : row.promptStartedRealtime;

        return string.Join(",",
            CsvEscape(_sessionId),
            I(row.paintingRunIndex),
            I(row.paintingIndex),
            CsvEscape(row.paintingId),
            CsvEscape(row.paintingName),
            I(row.stageIndex),
            I(row.stagePresetIndex),
            CsvEscape(row.eventType),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(promptRealtime))),
            UnixMs(promptRealtime),
            Timestamp(row.selectionTriggeredRealtime),
            UnixMs(row.selectionTriggeredRealtime),
            ElapsedMs(row.selectionTriggeredRealtime),
            ElapsedBetweenMs(promptRealtime, row.selectionTriggeredRealtime),
            I(row.selectionTriggeredFrame),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(eventRealtime))),
            UnixMs(eventRealtime),
            ElapsedMs(eventRealtime),
            ElapsedBetweenMs(promptRealtime, eventRealtime),
            SecondsMs(row.choiceWaitSeconds),
            I(row.orbIndex),
            CsvEscape(row.orbLabel),
            SecondsMs(row.selectionDwellSeconds),
            SecondsMs(row.selectionEffectSeconds),
            F(row.particleIntensity),
            F(row.particleFrequency),
            F(row.orbColorIntensity),
            CsvEscape(row.notes));
    }

    string ToSummaryCsv(Row row, double eventRealtime)
    {
        return string.Join(",",
            CsvEscape(_sessionId),
            I(row.paintingRunIndex),
            I(row.paintingIndex),
            CsvEscape(row.paintingId),
            CsvEscape(row.paintingName),
            I(row.stageIndex),
            I(row.stagePresetIndex),
            I(row.stageValueCount),
            F(row.stageRangeMinimum),
            F(row.stageRangeMaximum),
            F(row.particleIntensity),
            F(row.particleFrequency),
            CsvEscape(row.eventType),
            I(row.orbIndex),
            CsvEscape(row.orbLabel),
            Timestamp(row.selectionTriggeredRealtime),
            UnixMs(row.selectionTriggeredRealtime),
            ElapsedMs(row.selectionTriggeredRealtime),
            ElapsedBetweenMs(row.promptStartedRealtime, row.selectionTriggeredRealtime),
            I(row.selectionTriggeredFrame),
            SecondsMs(row.choiceWaitSeconds),
            SecondsMs(row.selectionDwellSeconds),
            SecondsMs(row.selectionEffectSeconds),
            F(row.orbColorIntensity),
            CsvEscape(FormatTimestamp(RealtimeToTimestamp(eventRealtime))),
            UnixMs(eventRealtime),
            ElapsedMs(eventRealtime),
            CsvEscape(row.notes));
    }

    string ToEyeTrackingCsv(EyeTrackingRow row, double sampleRealtime)
    {
        return string.Join(",",
            CsvEscape(_sessionId),
            UnixMs(sampleRealtime),
            ElapsedMs(sampleRealtime),
            row.frame.ToString(CultureInfo.InvariantCulture),
            B(row.leftValid),
            F(row.leftConfidence),
            VectorCsv(row.leftOrigin),
            VectorCsv(row.leftForward),
            B(row.rightValid),
            F(row.rightConfidence),
            VectorCsv(row.rightOrigin),
            VectorCsv(row.rightForward),
            VectorCsv(row.centerEyePosition),
            VectorCsv(row.centerEyeForward));
    }

    string ToVideoFrameCsv(VideoFrameRow row, double sampleRealtime)
    {
        return string.Join(",",
            CsvEscape(_sessionId),
            UnixMs(sampleRealtime),
            ElapsedMs(sampleRealtime),
            I(row.frameIndex),
            row.unityFrame.ToString(CultureInfo.InvariantCulture),
            I(row.width),
            I(row.height),
            F(row.captureFps),
            CsvEscape(row.imageFormat),
            I(row.jpegQuality),
            CsvEscape(row.relativePath),
            CsvEscape(row.absolutePath),
            CsvEscape(row.sourceCameraName),
            CsvEscape(row.captureCameraName),
            VectorCsv(row.cameraPosition),
            QuaternionCsv(row.cameraRotation),
            VectorCsv(row.cameraForward),
            VectorCsv(row.cameraUp),
            L(row.encodedBytes),
            F(row.readbackLatencyMs),
            F(row.encodeWriteLatencyMs),
            I(row.droppedFrames),
            CsvEscape(row.notes));
    }

    static string GetSessionHeader()
    {
        return string.Join(",",
            "sessionId",
            "sessionStartLocal",
            "sessionStartUnixMs",
            "inputSource",
            "csvVersion",
            "sessionFolderPath");
    }

    static string GetPaintingsHeader()
    {
        return string.Join(",",
            "sessionId",
            "paintingRunIndex",
            "paintingIndex",
            "paintingId",
            "paintingName",
            "paintingStartLocal",
            "paintingStartUnixMs",
            "paintingEndLocal",
            "paintingEndUnixMs",
            "paintingDurationMs",
            "stageOrder",
            "notes");
    }

    static string GetStageValuesHeader()
    {
        return string.Join(",",
            "sessionId",
            "paintingRunIndex",
            "paintingIndex",
            "paintingId",
            "paintingName",
            "stageIndex",
            "stagePresetIndex",
            "stageValueIndex",
            "stageValueCount",
            "eventLocal",
            "eventUnixMs",
            "elapsedMs",
            "stageElapsedMs",
            "stageRangeMinimum",
            "stageRangeMaximum",
            "particleIntensity",
            "particleFrequency");
    }

    static string GetChoicesHeader()
    {
        return string.Join(",",
            "sessionId",
            "paintingRunIndex",
            "paintingIndex",
            "paintingId",
            "paintingName",
            "stageIndex",
            "stagePresetIndex",
            "eventType",
            "promptStartLocal",
            "promptStartUnixMs",
            "selectionTriggeredLocal",
            "selectionTriggeredUnixMs",
            "selectionTriggeredElapsedMs",
            "promptToSelectionMs",
            "selectionTriggeredFrame",
            "choiceCompleteLocal",
            "choiceCompleteUnixMs",
            "elapsedMs",
            "promptElapsedMs",
            "choiceWaitMs",
            "orbIndex",
            "orbLabel",
            "selectionDwellMs",
            "selectionEffectMs",
            "finalIntensity",
            "finalFrequency",
            "orbColorIntensity",
            "notes");
    }

    static string GetEventsHeader()
    {
        return string.Join(",",
            "sessionId",
            "sessionStartLocal",
            "sessionStartUnixMs",
            "eventLocal",
            "eventUnixMs",
            "elapsedMs",
            "frame",
            "eventType",
            "inputSource",
            "paintingRunIndex",
            "paintingIndex",
            "paintingId",
            "paintingName",
            "stageIndex",
            "stagePresetIndex",
            "stageCount",
            "stageValueIndex",
            "stageValueCount",
            "stageElapsedMs",
            "promptElapsedMs",
            "selectionTriggeredLocal",
            "selectionTriggeredUnixMs",
            "selectionTriggeredElapsedMs",
            "promptToSelectionMs",
            "selectionTriggeredFrame",
            "stageRangeMinimum",
            "stageRangeMaximum",
            "particleIntensity",
            "particleFrequency",
            "orbIndex",
            "orbLabel",
            "orbColorIntensity",
            "selectionDwellMs",
            "selectionEffectMs",
            "choiceWaitMs",
            "notes");
    }

    static string GetSummaryHeader()
    {
        return string.Join(",",
            "sessionId",
            "paintingRunIndex",
            "paintingIndex",
            "paintingId",
            "paintingName",
            "stageIndex",
            "stagePresetIndex",
            "stageValueCount",
            "stageRangeMinimum",
            "stageRangeMaximum",
            "finalIntensity",
            "finalFrequency",
            "choiceEventType",
            "orbIndex",
            "orbLabel",
            "selectionTriggeredLocal",
            "selectionTriggeredUnixMs",
            "selectionTriggeredElapsedMs",
            "promptToSelectionMs",
            "selectionTriggeredFrame",
            "choiceWaitMs",
            "selectionDwellMs",
            "selectionEffectMs",
            "orbColorIntensity",
            "eventLocal",
            "eventUnixMs",
            "elapsedMs",
            "notes");
    }

    static string GetEyeTrackingHeader()
    {
        return string.Join(",",
            "sessionId",
            "sampleUnixMs",
            "elapsedMs",
            "frame",
            "leftValid",
            "leftConfidence",
            VectorHeader("leftOrigin"),
            VectorHeader("leftForward"),
            "rightValid",
            "rightConfidence",
            VectorHeader("rightOrigin"),
            VectorHeader("rightForward"),
            VectorHeader("centerEye"),
            VectorHeader("centerForward"));
    }

    static string GetVideoFramesHeader()
    {
        return string.Join(",",
            "sessionId",
            "sampleUnixMs",
            "elapsedMs",
            "frameIndex",
            "unityFrame",
            "width",
            "height",
            "captureFps",
            "imageFormat",
            "jpegQuality",
            "relativePath",
            "absolutePath",
            "sourceCameraName",
            "captureCameraName",
            VectorHeader("cameraPosition"),
            QuaternionHeader("cameraRotation"),
            VectorHeader("cameraForward"),
            VectorHeader("cameraUp"),
            "encodedBytes",
            "readbackLatencyMs",
            "encodeWriteLatencyMs",
            "droppedFrames",
            "notes");
    }

    string CreateSessionId(DateTimeOffset startedAt)
    {
        var safePrefix = SafeFolderName(_sessionFolderPrefix, "meditation_experiment");
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0}_{1:yyyyMMdd_HHmmss_fff}",
            safePrefix,
            startedAt.LocalDateTime);
    }

    DateTimeOffset RealtimeToTimestamp(double realtime)
    {
        if (double.IsNaN(realtime))
        {
            return DateTimeOffset.Now;
        }

        var elapsedMs = (realtime - _sessionStartedRealtime) * 1000.0;
        return _sessionStartedAt.AddMilliseconds(elapsedMs);
    }

    string Timestamp(double realtime)
    {
        return double.IsNaN(realtime)
            ? string.Empty
            : CsvEscape(FormatTimestamp(RealtimeToTimestamp(realtime)));
    }

    string UnixMs(double realtime)
    {
        if (double.IsNaN(realtime))
        {
            return string.Empty;
        }

        return RealtimeToTimestamp(realtime).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    }

    string ElapsedMs(double realtime)
    {
        if (double.IsNaN(realtime))
        {
            return string.Empty;
        }

        return Mathf.RoundToInt((float)((realtime - _sessionStartedRealtime) * 1000.0)).ToString(CultureInfo.InvariantCulture);
    }

    static string ElapsedBetweenMs(double startRealtime, double endRealtime)
    {
        if (double.IsNaN(startRealtime) || double.IsNaN(endRealtime))
        {
            return string.Empty;
        }

        return Mathf.RoundToInt((float)((endRealtime - startRealtime) * 1000.0)).ToString(CultureInfo.InvariantCulture);
    }

    static double ResolveEventRealtime(Row row)
    {
        return row != null && !double.IsNaN(row.eventRealtime)
            ? row.eventRealtime
            : Time.realtimeSinceStartupAsDouble;
    }

    static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
    }

    static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    static string I(int value)
    {
        return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    static string L(long value)
    {
        return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    static string F(float value)
    {
        if (float.IsNaN(value))
        {
            return string.Empty;
        }

        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    static string B(bool value)
    {
        return value ? "1" : "0";
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

    static string SecondsMs(float seconds)
    {
        if (float.IsNaN(seconds))
        {
            return string.Empty;
        }

        return Mathf.RoundToInt(seconds * 1000f).ToString(CultureInfo.InvariantCulture);
    }

    static string SafeFolderName(string value, string fallback)
    {
        var source = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            var isInvalid = false;
            for (var j = 0; j < invalid.Length; j++)
            {
                if (c == invalid[j])
                {
                    isInvalid = true;
                    break;
                }
            }

            builder.Append(isInvalid ? '_' : c);
        }

        return builder.Length > 0 ? builder.ToString() : fallback;
    }

    static string NormalizePath(string path)
    {
        return path.Trim().Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
    }

    public sealed class Row
    {
        public string eventType;
        public string inputSource;
        public int paintingRunIndex = -1;
        public int paintingIndex = -1;
        public string paintingId;
        public string paintingName;
        public string stageOrder;
        public int stageIndex = -1;
        public int stagePresetIndex = -1;
        public int stageCount = -1;
        public int stageValueIndex = -1;
        public int stageValueCount = -1;
        public float stageRangeMinimum = float.NaN;
        public float stageRangeMaximum = float.NaN;
        public float particleIntensity = float.NaN;
        public float particleFrequency = float.NaN;
        public int orbIndex = -1;
        public string orbLabel;
        public float orbColorIntensity = float.NaN;
        public float selectionDwellSeconds = float.NaN;
        public float selectionEffectSeconds = float.NaN;
        public float choiceWaitSeconds = float.NaN;
        public double eventRealtime = double.NaN;
        public double selectionTriggeredRealtime = double.NaN;
        public int selectionTriggeredFrame = -1;
        public double paintingStartedRealtime = double.NaN;
        public double paintingEndedRealtime = double.NaN;
        public double stageStartedRealtime = double.NaN;
        public double promptStartedRealtime = double.NaN;
        public string notes;
    }

    public sealed class EyeTrackingRow
    {
        public double sampleRealtime = double.NaN;
        public int frame;
        public bool leftValid;
        public float leftConfidence = float.NaN;
        public Vector3 leftOrigin;
        public Vector3 leftForward;
        public bool rightValid;
        public float rightConfidence = float.NaN;
        public Vector3 rightOrigin;
        public Vector3 rightForward;
        public Vector3 centerEyePosition;
        public Vector3 centerEyeForward;
    }

    public sealed class VideoFrameRow
    {
        public double sampleRealtime = double.NaN;
        public int frameIndex;
        public int unityFrame;
        public int width;
        public int height;
        public float captureFps = float.NaN;
        public string imageFormat;
        public int jpegQuality = -1;
        public string relativePath;
        public string absolutePath;
        public string sourceCameraName;
        public string captureCameraName;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation = Quaternion.identity;
        public Vector3 cameraForward;
        public Vector3 cameraUp;
        public long encodedBytes = -1;
        public float readbackLatencyMs = float.NaN;
        public float encodeWriteLatencyMs = float.NaN;
        public int droppedFrames;
        public string notes;
    }
}
