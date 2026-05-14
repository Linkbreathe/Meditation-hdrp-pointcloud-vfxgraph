using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class CalmnessFeedbackResult
{
    public string sessionId;
    public string paintingId;
    public int paintingIndex;
    public string timestampUtc;
    public int calmnessLevel;
    public string calmnessLabel;
    public int dwellDurationMs;
    public bool timedOut;
    public float gazeEntryTimestamp;
    public bool hasGazeEntryTimestamp;
    public float totalViewDurationSec;

    public static CalmnessFeedbackResult Create(
        string paintingId,
        int paintingIndex,
        int calmnessLevel,
        float dwellDurationSeconds,
        bool timedOut,
        float gazeEntryTimestamp,
        bool hasGazeEntryTimestamp,
        float totalViewDurationSec)
    {
        return new CalmnessFeedbackResult
        {
            paintingId = paintingId,
            paintingIndex = paintingIndex,
            timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            calmnessLevel = calmnessLevel,
            calmnessLabel = CalmnessFeedbackScale.GetLabel(calmnessLevel),
            dwellDurationMs = Mathf.RoundToInt(Mathf.Max(0f, dwellDurationSeconds) * 1000f),
            timedOut = timedOut,
            gazeEntryTimestamp = Mathf.Max(0f, gazeEntryTimestamp),
            hasGazeEntryTimestamp = hasGazeEntryTimestamp,
            totalViewDurationSec = Mathf.Max(0f, totalViewDurationSec)
        };
    }
}

public static class CalmnessFeedbackScale
{
    public const int MinimumLevel = 1;
    public const int MaximumLevel = 4;

    static readonly string[] Labels =
    {
        "\u6d9f\u6f2a",
        "\u5fae\u6f9c",
        "\u5e73\u6e56",
        "\u5982\u955c"
    };

    static readonly Color[] Colors =
    {
        FromRgb(0xE8, 0x88, 0x4A),
        FromRgb(0xA8, 0xB8, 0x6A),
        FromRgb(0x4A, 0xA8, 0xA0),
        FromRgb(0x2A, 0x60, 0x80)
    };

    public static string GetLabel(int level)
    {
        return IsValidLevel(level) ? Labels[level - 1] : null;
    }

    public static Color GetColor(int level)
    {
        return IsValidLevel(level) ? Colors[level - 1] : Color.white;
    }

    public static bool IsValidLevel(int level)
    {
        return level >= MinimumLevel && level <= MaximumLevel;
    }

    static Color FromRgb(byte r, byte g, byte b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}

[DefaultExecutionOrder(1250)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Calmness Feedback Logger")]
public sealed class CalmnessFeedbackLogger : MonoBehaviour
{
    [Header("Logging")]
    [SerializeField] bool _writeJsonLines = true;
    [SerializeField] bool _logToConsole = true;
    [SerializeField] string _filePrefix = "calmness_feedback";

    static CalmnessFeedbackLogger _instance;

    readonly string _sessionId = Guid.NewGuid().ToString("N");
    StreamWriter _writer;
    string _jsonlPath;

    public static CalmnessFeedbackLogger Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<CalmnessFeedbackLogger>();
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject("CalmnessFeedbackLogger");
            _instance = gameObject.AddComponent<CalmnessFeedbackLogger>();
            return _instance;
        }
    }

    public string jsonlPath => _jsonlPath;

    public static void Log(CalmnessFeedbackResult result)
    {
        Instance.LogResult(result);
    }

    public void LogResult(CalmnessFeedbackResult result)
    {
        if (result == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(result.sessionId))
        {
            result.sessionId = _sessionId;
        }

        if (string.IsNullOrEmpty(result.timestampUtc))
        {
            result.timestampUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        }

        var json = FormatJson(result);
        if (_writeJsonLines)
        {
            WriteJsonLine(json);
        }

        if (_logToConsole)
        {
            Debug.Log("[CalmnessFeedbackLogger] " + json, this);
        }
    }

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
        CloseWriter();
    }

    void WriteJsonLine(string json)
    {
        if (_writer == null)
        {
            OpenWriter();
        }

        if (_writer == null)
        {
            return;
        }

        _writer.WriteLine(json);
        _writer.Flush();
    }

    void OpenWriter()
    {
        var safePrefix = string.IsNullOrWhiteSpace(_filePrefix) ? "calmness_feedback" : _filePrefix.Trim();
        var fileName = string.Format(
            CultureInfo.InvariantCulture,
            "{0}_{1:yyyyMMdd_HHmmss}.jsonl",
            safePrefix,
            DateTime.Now);

        _jsonlPath = Path.Combine(Application.persistentDataPath, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(_jsonlPath));

        var stream = new FileStream(_jsonlPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(stream, Encoding.UTF8);
        Debug.Log("[CalmnessFeedbackLogger] Writing calmness feedback JSONL to: " + _jsonlPath, this);
    }

    void CloseWriter()
    {
        if (_writer == null)
        {
            return;
        }

        _writer.Flush();
        _writer.Dispose();
        _writer = null;
    }

    public static string FormatJson(CalmnessFeedbackResult result)
    {
        var builder = new StringBuilder(256);
        builder.Append('{');
        AppendString(builder, "sessionId", result.sessionId);
        builder.Append(',');
        AppendString(builder, "paintingId", result.paintingId);
        builder.Append(',');
        AppendNumber(builder, "paintingIndex", result.paintingIndex);
        builder.Append(',');
        AppendString(builder, "timestamp", result.timestampUtc);
        builder.Append(',');
        AppendNullableNumber(builder, "calmnessLevel", result.calmnessLevel, result.calmnessLevel > 0);
        builder.Append(',');
        AppendNullableString(builder, "calmnessLabel", result.calmnessLabel, !string.IsNullOrEmpty(result.calmnessLabel));
        builder.Append(',');
        AppendNumber(builder, "dwellDurationMs", result.dwellDurationMs);
        builder.Append(',');
        AppendBool(builder, "timedOut", result.timedOut);
        builder.Append(',');
        AppendNullableFloat(builder, "gazeEntryTimestamp", result.gazeEntryTimestamp, result.hasGazeEntryTimestamp);
        builder.Append(',');
        AppendFloat(builder, "totalViewDurationSec", result.totalViewDurationSec);
        builder.Append('}');
        return builder.ToString();
    }

    static void AppendString(StringBuilder builder, string name, string value)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append('"').Append(Escape(value)).Append('"');
    }

    static void AppendNullableString(StringBuilder builder, string name, string value, bool hasValue)
    {
        builder.Append('"').Append(name).Append("\":");
        if (!hasValue)
        {
            builder.Append("null");
            return;
        }

        builder.Append('"').Append(Escape(value)).Append('"');
    }

    static void AppendNumber(StringBuilder builder, string name, int value)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendNullableNumber(StringBuilder builder, string name, int value, bool hasValue)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append(hasValue ? value.ToString(CultureInfo.InvariantCulture) : "null");
    }

    static void AppendFloat(StringBuilder builder, string name, float value)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    static void AppendNullableFloat(StringBuilder builder, string name, float value, bool hasValue)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append(hasValue ? value.ToString("0.###", CultureInfo.InvariantCulture) : "null");
    }

    static void AppendBool(StringBuilder builder, string name, bool value)
    {
        builder.Append('"').Append(name).Append("\":");
        builder.Append(value ? "true" : "false");
    }

    static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
