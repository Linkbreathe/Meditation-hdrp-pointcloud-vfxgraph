using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK Data Logger")]
public sealed class RDKDataLogger : MonoBehaviour
{
    const string SeparatorDirective = "sep=,";

    [SerializeField] string _sessionFolderPrefix = "rdk_experiment";
    [SerializeField] string _fileName = "rdk_trials.csv";
    [SerializeField] bool _writeExcelSeparatorDirective = true;
    [SerializeField] bool _flushEveryTrial = true;

    StreamWriter _writer;
    string _sessionFolderPath;
    string _csvPath;
    bool _sessionActive;

    public string sessionFolderPath => _sessionFolderPath;
    public string csvPath => _csvPath;
    public bool sessionActive => _sessionActive;

    void OnDisable()
    {
        CloseSession();
    }

    public void StartSession()
    {
        CloseSession();

        string sessionId = _sessionFolderPrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        _sessionFolderPath = Path.Combine(Application.persistentDataPath, sessionId);
        Directory.CreateDirectory(_sessionFolderPath);
        _csvPath = Path.Combine(_sessionFolderPath, _fileName);
        _writer = new StreamWriter(_csvPath, false, new UTF8Encoding(false));

        if (_writeExcelSeparatorDirective)
        {
            _writer.WriteLine(SeparatorDirective);
        }

        _writer.WriteLine(GetHeader());
        _sessionActive = true;
        Debug.Log("[RDKDataLogger] Writing trial CSV to: " + _csvPath, this);
    }

    public void CloseSession()
    {
        if (_writer != null)
        {
            _writer.Flush();
            _writer.Dispose();
            _writer = null;
        }

        _sessionActive = false;
    }

    public void LogTrial(RDKTrialRecord record)
    {
        if (!_sessionActive)
        {
            StartSession();
        }

        _writer.WriteLine(ToCsv(record));
        if (_flushEveryTrial)
        {
            _writer.Flush();
        }
    }

    static string GetHeader()
    {
        return string.Join(",",
            "trial_index",
            "block_index",
            "trial_index_in_block",
            "phase",
            "condition_type",
            "condition_name",
            "true_motion_direction",
            "motion_coherence",
            "coherent_dot_count",
            "random_dot_count",
            "stimulus_start_realtime",
            "stimulus_end_realtime",
            "response_button",
            "response_device",
            "response_direction",
            "response_realtime",
            "reaction_time",
            "correct",
            "timeout",
            "early_response",
            "early_response_button",
            "early_response_device",
            "early_response_realtime",
            "head_position_x",
            "head_position_y",
            "head_position_z",
            "head_forward_x",
            "head_forward_y",
            "head_forward_z",
            "head_rotation_x",
            "head_rotation_y",
            "head_rotation_z",
            "head_rotation_w");
    }

    static string ToCsv(RDKTrialRecord record)
    {
        return string.Join(",",
            record.trialIndex.ToString(CultureInfo.InvariantCulture),
            record.blockIndex.ToString(CultureInfo.InvariantCulture),
            record.trialIndexInBlock.ToString(CultureInfo.InvariantCulture),
            CsvEscape(record.phase),
            CsvEscape(record.conditionType),
            CsvEscape(record.conditionName),
            CsvEscape(record.trueMotionDirection),
            record.motionCoherence.ToString("0.####", CultureInfo.InvariantCulture),
            record.coherentDotCount.ToString(CultureInfo.InvariantCulture),
            record.randomDotCount.ToString(CultureInfo.InvariantCulture),
            record.stimulusStartRealtime.ToString("0.####", CultureInfo.InvariantCulture),
            record.stimulusEndRealtime.ToString("0.####", CultureInfo.InvariantCulture),
            CsvEscape(record.responseButton),
            CsvEscape(record.responseDevice),
            CsvEscape(record.responseDirection),
            record.responseRealtime.ToString("0.####", CultureInfo.InvariantCulture),
            record.reactionTime.ToString("0.####", CultureInfo.InvariantCulture),
            Bool(record.correct),
            Bool(record.timeout),
            Bool(record.earlyResponse),
            CsvEscape(record.earlyResponseButton),
            CsvEscape(record.earlyResponseDevice),
            record.earlyResponseRealtime.ToString("0.####", CultureInfo.InvariantCulture),
            record.headPosition.x.ToString("0.####", CultureInfo.InvariantCulture),
            record.headPosition.y.ToString("0.####", CultureInfo.InvariantCulture),
            record.headPosition.z.ToString("0.####", CultureInfo.InvariantCulture),
            record.headForward.x.ToString("0.####", CultureInfo.InvariantCulture),
            record.headForward.y.ToString("0.####", CultureInfo.InvariantCulture),
            record.headForward.z.ToString("0.####", CultureInfo.InvariantCulture),
            record.headRotation.x.ToString("0.####", CultureInfo.InvariantCulture),
            record.headRotation.y.ToString("0.####", CultureInfo.InvariantCulture),
            record.headRotation.z.ToString("0.####", CultureInfo.InvariantCulture),
            record.headRotation.w.ToString("0.####", CultureInfo.InvariantCulture));
    }

    static string Bool(bool value)
    {
        return value ? "1" : "0";
    }

    static string CsvEscape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        bool needsQuotes = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
        string escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? "\"" + escaped + "\"" : escaped;
    }
}

public struct RDKTrialRecord
{
    public int trialIndex;
    public int blockIndex;
    public int trialIndexInBlock;
    public string phase;
    public string conditionType;
    public string conditionName;
    public string trueMotionDirection;
    public float motionCoherence;
    public int coherentDotCount;
    public int randomDotCount;
    public double stimulusStartRealtime;
    public double stimulusEndRealtime;
    public string responseButton;
    public string responseDevice;
    public string responseDirection;
    public double responseRealtime;
    public double reactionTime;
    public bool correct;
    public bool timeout;
    public bool earlyResponse;
    public string earlyResponseButton;
    public string earlyResponseDevice;
    public double earlyResponseRealtime;
    public Vector3 headPosition;
    public Vector3 headForward;
    public Quaternion headRotation;
}
