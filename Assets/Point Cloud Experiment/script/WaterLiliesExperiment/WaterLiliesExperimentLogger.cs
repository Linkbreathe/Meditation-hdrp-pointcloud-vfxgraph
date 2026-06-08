using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class WaterLiliesExperimentLogRow
{
    public string row_type = "";
    public string event_type = "";
    public string session_id = "";
    public string participant_id = "";
    public string experiment_mode = "";
    public string phase = "";
    public string condition_id = "";
    public int condition_order_index = -1;
    public int condition_order_total = -1;
    public string intensity_level = "";
    public string frequency_level = "";
    public double intensity_value = double.NaN;
    public double frequency_value = double.NaN;
    public double applied_intensity_value = double.NaN;
    public double applied_frequency_value = double.NaN;
    public bool natural_modulation_enabled;
    public int natural_modulation_seed = -1;
    public double natural_modulation_template_elapsed_seconds = double.NaN;
    public float natural_modulation_intensity_depth = float.NaN;
    public float natural_modulation_frequency_depth = float.NaN;
    public float natural_modulation_large_scale_seconds = float.NaN;
    public float natural_modulation_medium_scale_seconds = float.NaN;
    public float natural_modulation_fine_scale_seconds = float.NaN;
    public double planned_duration_seconds = double.NaN;
    public double phase_elapsed_seconds = double.NaN;
    public double phase_remaining_seconds = double.NaN;
    public bool formal_viewing;
    public string utc_timestamp_iso = "";
    public long unix_time_ms;
    public double realtime_since_startup_seconds = double.NaN;
    public double session_elapsed_seconds = double.NaN;
    public bool headset_presence_available;
    public bool headset_user_present;
    public double headset_off_interval_seconds = double.NaN;
    public bool head_pose_available;
    public float head_position_x;
    public float head_position_y;
    public float head_position_z;
    public float head_rotation_x;
    public float head_rotation_y;
    public float head_rotation_z;
    public float head_rotation_w;
    public float head_velocity_x;
    public float head_velocity_y;
    public float head_velocity_z;
    public float head_angular_velocity_deg_s;
    public bool gaze_available;
    public float gaze_origin_x;
    public float gaze_origin_y;
    public float gaze_origin_z;
    public float gaze_direction_x;
    public float gaze_direction_y;
    public float gaze_direction_z;
    public bool gaze_hit;
    public float gaze_hit_x;
    public float gaze_hit_y;
    public float gaze_hit_z;
    public bool gaze_on_painting;
    public bool face_sample_available;
    public bool face_permission_granted;
    public bool face_tracking_supported;
    public bool face_tracking_enabled;
    public bool face_expressions_found;
    public bool face_expressions_enabled;
    public bool face_state_available;
    public bool face_valid;
    public bool eye_following_blendshapes_valid;
    public string face_data_source = "";
    public double face_state_time_seconds = double.NaN;
    public bool face_region_confidences_available;
    public float face_lower_region_confidence = float.NaN;
    public float face_upper_region_confidence = float.NaN;
    public bool face_visemes_valid;
    public int face_expression_count = -1;
    public bool eyes_closed_weights_available;
    public float eyes_closed_l_raw = float.NaN;
    public float eyes_closed_r_raw = float.NaN;
    public float eyes_look_down_l = float.NaN;
    public float eyes_look_down_r = float.NaN;
    public float eyes_look_left_l = float.NaN;
    public float eyes_look_left_r = float.NaN;
    public float eyes_look_right_l = float.NaN;
    public float eyes_look_right_r = float.NaN;
    public float eyes_look_up_l = float.NaN;
    public float eyes_look_up_r = float.NaN;
    public float upper_lid_raiser_l = float.NaN;
    public float upper_lid_raiser_r = float.NaN;
    public float lid_tightener_l = float.NaN;
    public float lid_tightener_r = float.NaN;
    public float brow_lowerer_l = float.NaN;
    public float brow_lowerer_r = float.NaN;
    public float inner_brow_raiser_l = float.NaN;
    public float inner_brow_raiser_r = float.NaN;
    public float outer_brow_raiser_l = float.NaN;
    public float outer_brow_raiser_r = float.NaN;
    public float cheek_raiser_l = float.NaN;
    public float cheek_raiser_r = float.NaN;
    public float eyes_closed_l = float.NaN;
    public float eyes_closed_r = float.NaN;
    public float eye_closure_mean = float.NaN;
    public float eye_closure_difference = float.NaN;
    public float eye_closed_signal_min = float.NaN;
    public float eye_closed_signal_max = float.NaN;
    public float eye_closed_signal_range = float.NaN;
    public bool eye_closed_signal_responsive;
    public bool left_eye_closed_candidate;
    public bool right_eye_closed_candidate;
    public bool both_eyes_closed_candidate;
    public bool left_eye_open_candidate;
    public bool right_eye_open_candidate;
    public bool both_eyes_open_candidate;
    public bool eye_state_label_available;
    public string eye_state_label = "";
    public float eye_state_confidence = float.NaN;
    public bool left_eye_closed;
    public bool right_eye_closed;
    public bool both_eyes_closed;
    public bool left_eye_open;
    public bool right_eye_open;
    public bool both_eyes_open;
    public bool eye_tracking_supported;
    public bool eye_tracking_enabled;
    public bool eye_gazes_state_available;
    public double eye_gazes_state_time_seconds = double.NaN;
    public bool left_eye_gaze_valid;
    public bool right_eye_gaze_valid;
    public float left_eye_gaze_confidence = float.NaN;
    public float right_eye_gaze_confidence = float.NaN;
    public string face_expression_weights = "";
    public string face_diagnostic = "";
    public string notes = "";
}

[Serializable]
public sealed class WaterLiliesVideoFrameLogRow
{
    public string session_id = "";
    public string participant_id = "";
    public string experiment_mode = "";
    public string utc_timestamp_iso = "";
    public long unix_time_ms;
    public double realtime_since_startup_seconds = double.NaN;
    public double session_elapsed_seconds = double.NaN;
    public int frame_index = -1;
    public int unity_frame = -1;
    public int width = -1;
    public int height = -1;
    public float capture_fps = float.NaN;
    public string image_format = "";
    public int jpeg_quality = -1;
    public string relative_path = "";
    public string absolute_path = "";
    public string source_camera_name = "";
    public string capture_camera_name = "";
    public Vector3 camera_position;
    public Quaternion camera_rotation = Quaternion.identity;
    public Vector3 camera_forward;
    public Vector3 camera_up;
    public long encoded_bytes = -1;
    public float readback_latency_ms = float.NaN;
    public float encode_write_latency_ms = float.NaN;
    public int dropped_frames;
    public string notes = "";
}

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Experiment Logger")]
public sealed class WaterLiliesExperimentLogger : MonoBehaviour
{
    const string CsvSeparatorDirective = "sep=,";

    static readonly string[] EventColumns =
    {
        "row_type",
        "event_type",
        "session_id",
        "participant_id",
        "experiment_mode",
        "phase",
        "condition_id",
        "condition_order_index",
        "condition_order_total",
        "intensity_level",
        "frequency_level",
        "intensity_value",
        "frequency_value",
        "applied_intensity_value",
        "applied_frequency_value",
        "natural_modulation_enabled",
        "natural_modulation_seed",
        "natural_modulation_template_elapsed_seconds",
        "natural_modulation_intensity_depth",
        "natural_modulation_frequency_depth",
        "natural_modulation_large_scale_seconds",
        "natural_modulation_medium_scale_seconds",
        "natural_modulation_fine_scale_seconds",
        "planned_duration_seconds",
        "phase_elapsed_seconds",
        "phase_remaining_seconds",
        "formal_viewing",
        "utc_timestamp_iso",
        "unix_time_ms",
        "realtime_since_startup_seconds",
        "session_elapsed_seconds",
        "headset_presence_available",
        "headset_user_present",
        "headset_off_interval_seconds",
        "head_pose_available",
        "head_position_x",
        "head_position_y",
        "head_position_z",
        "head_rotation_x",
        "head_rotation_y",
        "head_rotation_z",
        "head_rotation_w",
        "head_velocity_x",
        "head_velocity_y",
        "head_velocity_z",
        "head_angular_velocity_deg_s",
        "gaze_available",
        "gaze_origin_x",
        "gaze_origin_y",
        "gaze_origin_z",
        "gaze_direction_x",
        "gaze_direction_y",
        "gaze_direction_z",
        "gaze_hit",
        "gaze_hit_x",
        "gaze_hit_y",
        "gaze_hit_z",
        "gaze_on_painting",
        "notes"
    };

    static readonly string[] SampleColumns =
    {
        "row_type",
        "event_type",
        "session_id",
        "participant_id",
        "experiment_mode",
        "phase",
        "condition_id",
        "condition_order_index",
        "condition_order_total",
        "intensity_level",
        "frequency_level",
        "intensity_value",
        "frequency_value",
        "applied_intensity_value",
        "applied_frequency_value",
        "natural_modulation_enabled",
        "natural_modulation_seed",
        "natural_modulation_template_elapsed_seconds",
        "natural_modulation_intensity_depth",
        "natural_modulation_frequency_depth",
        "natural_modulation_large_scale_seconds",
        "natural_modulation_medium_scale_seconds",
        "natural_modulation_fine_scale_seconds",
        "planned_duration_seconds",
        "phase_elapsed_seconds",
        "phase_remaining_seconds",
        "formal_viewing",
        "utc_timestamp_iso",
        "unix_time_ms",
        "realtime_since_startup_seconds",
        "session_elapsed_seconds",
        "headset_presence_available",
        "headset_user_present",
        "headset_off_interval_seconds",
        "head_pose_available",
        "head_position_x",
        "head_position_y",
        "head_position_z",
        "head_rotation_x",
        "head_rotation_y",
        "head_rotation_z",
        "head_rotation_w",
        "head_velocity_x",
        "head_velocity_y",
        "head_velocity_z",
        "head_angular_velocity_deg_s",
        "notes"
    };

    static readonly string[] EyeTrackingColumns =
    {
        "row_type",
        "event_type",
        "session_id",
        "participant_id",
        "experiment_mode",
        "phase",
        "condition_id",
        "condition_order_index",
        "condition_order_total",
        "intensity_level",
        "frequency_level",
        "intensity_value",
        "frequency_value",
        "applied_intensity_value",
        "applied_frequency_value",
        "natural_modulation_enabled",
        "natural_modulation_seed",
        "natural_modulation_template_elapsed_seconds",
        "natural_modulation_intensity_depth",
        "natural_modulation_frequency_depth",
        "natural_modulation_large_scale_seconds",
        "natural_modulation_medium_scale_seconds",
        "natural_modulation_fine_scale_seconds",
        "planned_duration_seconds",
        "phase_elapsed_seconds",
        "phase_remaining_seconds",
        "formal_viewing",
        "utc_timestamp_iso",
        "unix_time_ms",
        "realtime_since_startup_seconds",
        "session_elapsed_seconds",
        "gaze_available",
        "gaze_origin_x",
        "gaze_origin_y",
        "gaze_origin_z",
        "gaze_direction_x",
        "gaze_direction_y",
        "gaze_direction_z",
        "gaze_hit",
        "gaze_hit_x",
        "gaze_hit_y",
        "gaze_hit_z",
        "gaze_on_painting",
        "notes"
    };

    static readonly string[] FaceTrackingColumns =
    {
        "row_type",
        "event_type",
        "session_id",
        "participant_id",
        "experiment_mode",
        "phase",
        "condition_id",
        "condition_order_index",
        "condition_order_total",
        "intensity_level",
        "frequency_level",
        "intensity_value",
        "frequency_value",
        "applied_intensity_value",
        "applied_frequency_value",
        "natural_modulation_enabled",
        "natural_modulation_seed",
        "natural_modulation_template_elapsed_seconds",
        "natural_modulation_intensity_depth",
        "natural_modulation_frequency_depth",
        "natural_modulation_large_scale_seconds",
        "natural_modulation_medium_scale_seconds",
        "natural_modulation_fine_scale_seconds",
        "planned_duration_seconds",
        "phase_elapsed_seconds",
        "phase_remaining_seconds",
        "formal_viewing",
        "utc_timestamp_iso",
        "unix_time_ms",
        "realtime_since_startup_seconds",
        "session_elapsed_seconds",
        "face_sample_available",
        "face_permission_granted",
        "face_tracking_supported",
        "face_tracking_enabled",
        "face_expressions_found",
        "face_expressions_enabled",
        "face_state_available",
        "face_valid",
        "eye_following_blendshapes_valid",
        "face_data_source",
        "face_state_time_seconds",
        "face_region_confidences_available",
        "face_lower_region_confidence",
        "face_upper_region_confidence",
        "face_visemes_valid",
        "face_expression_count",
        "eyes_closed_weights_available",
        "eyes_closed_l_raw",
        "eyes_closed_r_raw",
        "eyes_look_down_l",
        "eyes_look_down_r",
        "eyes_look_left_l",
        "eyes_look_left_r",
        "eyes_look_right_l",
        "eyes_look_right_r",
        "eyes_look_up_l",
        "eyes_look_up_r",
        "upper_lid_raiser_l",
        "upper_lid_raiser_r",
        "lid_tightener_l",
        "lid_tightener_r",
        "brow_lowerer_l",
        "brow_lowerer_r",
        "inner_brow_raiser_l",
        "inner_brow_raiser_r",
        "outer_brow_raiser_l",
        "outer_brow_raiser_r",
        "cheek_raiser_l",
        "cheek_raiser_r",
        "eyes_closed_l",
        "eyes_closed_r",
        "eye_closure_mean",
        "eye_closure_difference",
        "eye_closed_signal_min",
        "eye_closed_signal_max",
        "eye_closed_signal_range",
        "eye_closed_signal_responsive",
        "left_eye_closed_candidate",
        "right_eye_closed_candidate",
        "both_eyes_closed_candidate",
        "left_eye_open_candidate",
        "right_eye_open_candidate",
        "both_eyes_open_candidate",
        "eye_state_label_available",
        "eye_state_label",
        "eye_state_confidence",
        "left_eye_closed",
        "right_eye_closed",
        "both_eyes_closed",
        "left_eye_open",
        "right_eye_open",
        "both_eyes_open",
        "eye_tracking_supported",
        "eye_tracking_enabled",
        "eye_gazes_state_available",
        "eye_gazes_state_time_seconds",
        "left_eye_gaze_valid",
        "right_eye_gaze_valid",
        "left_eye_gaze_confidence",
        "right_eye_gaze_confidence",
        "face_expression_weights",
        "face_diagnostic",
        "notes"
    };

    static readonly string[] VideoFrameColumns =
    {
        "session_id",
        "participant_id",
        "experiment_mode",
        "utc_timestamp_iso",
        "unix_time_ms",
        "realtime_since_startup_seconds",
        "session_elapsed_seconds",
        "frame_index",
        "unity_frame",
        "width",
        "height",
        "capture_fps",
        "image_format",
        "jpeg_quality",
        "relative_path",
        "absolute_path",
        "source_camera_name",
        "capture_camera_name",
        "camera_position_x",
        "camera_position_y",
        "camera_position_z",
        "camera_rotation_x",
        "camera_rotation_y",
        "camera_rotation_z",
        "camera_rotation_w",
        "camera_forward_x",
        "camera_forward_y",
        "camera_forward_z",
        "camera_up_x",
        "camera_up_y",
        "camera_up_z",
        "encoded_bytes",
        "readback_latency_ms",
        "encode_write_latency_ms",
        "dropped_frames",
        "notes"
    };

    [Header("CSV")]
    [SerializeField] bool _writeExcelSeparatorDirective = true;
    [SerializeField] bool _flushEveryWrite = true;

    StreamWriter _eventsCsvWriter;
    StreamWriter _eventsJsonlWriter;
    StreamWriter _samplesCsvWriter;
    StreamWriter _samplesJsonlWriter;
    StreamWriter _eyeTrackingCsvWriter;
    StreamWriter _eyeTrackingJsonlWriter;
    StreamWriter _faceTrackingCsvWriter;
    StreamWriter _faceTrackingJsonlWriter;
    StreamWriter _videoFramesCsvWriter;
    StreamWriter _videoFramesJsonlWriter;
    string _sessionId;
    string _sessionFolderPath;
    string _participantId;
    string _experimentMode;
    DateTimeOffset _sessionStartedUtc;
    double _sessionStartedRealtime;
    bool _sessionActive;
    bool _writeCsv;
    bool _writeJsonLines;

    public string sessionId => _sessionId;
    public string sessionFolderPath => _sessionFolderPath;
    public bool sessionActive => _sessionActive;

    void OnDisable()
    {
        CloseSession();
    }

    public bool StartSession(WaterLiliesExperimentConfig config, string overrideLogRootPath = "")
    {
        CloseSession();

        var now = DateTimeOffset.UtcNow;
        _sessionStartedUtc = now;
        _sessionStartedRealtime = Time.realtimeSinceStartupAsDouble;
        _participantId = config != null ? config.participantId : "P001";
        _experimentMode = config != null ? config.mode.ToString() : string.Empty;
        _sessionId = SafeFileName(_participantId) + "_" + now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        _writeCsv = config == null || config.writeCsv;
        _writeJsonLines = config == null || config.writeJsonLines;

        var root = ResolveLogRoot(config, overrideLogRootPath);
        _sessionFolderPath = Path.Combine(root, _sessionId);
        Directory.CreateDirectory(_sessionFolderPath);

        if (_writeCsv)
        {
            _eventsCsvWriter = OpenCsvWriter("events.csv", EventColumns);
            _samplesCsvWriter = OpenCsvWriter("samples.csv", SampleColumns);
            _eyeTrackingCsvWriter = OpenCsvWriter("eye_tracking.csv", EyeTrackingColumns);
            _faceTrackingCsvWriter = OpenCsvWriter("face_tracking.csv", FaceTrackingColumns);
            _videoFramesCsvWriter = OpenCsvWriter("video_frames.csv", VideoFrameColumns);
        }

        if (_writeJsonLines)
        {
            _eventsJsonlWriter = new StreamWriter(Path.Combine(_sessionFolderPath, "events.jsonl"), false, Encoding.UTF8);
            _samplesJsonlWriter = new StreamWriter(Path.Combine(_sessionFolderPath, "samples.jsonl"), false, Encoding.UTF8);
            _eyeTrackingJsonlWriter = new StreamWriter(Path.Combine(_sessionFolderPath, "eye_tracking.jsonl"), false, Encoding.UTF8);
            _faceTrackingJsonlWriter = new StreamWriter(Path.Combine(_sessionFolderPath, "face_tracking.jsonl"), false, Encoding.UTF8);
            _videoFramesJsonlWriter = new StreamWriter(Path.Combine(_sessionFolderPath, "video_frames.jsonl"), false, Encoding.UTF8);
        }

        _sessionActive = true;
        Debug.Log("[WaterLiliesExperimentLogger] Session folder: " + _sessionFolderPath, this);
        return true;
    }

    public void CloseSession()
    {
        CloseWriter(ref _eventsCsvWriter);
        CloseWriter(ref _eventsJsonlWriter);
        CloseWriter(ref _samplesCsvWriter);
        CloseWriter(ref _samplesJsonlWriter);
        CloseWriter(ref _eyeTrackingCsvWriter);
        CloseWriter(ref _eyeTrackingJsonlWriter);
        CloseWriter(ref _faceTrackingCsvWriter);
        CloseWriter(ref _faceTrackingJsonlWriter);
        CloseWriter(ref _videoFramesCsvWriter);
        CloseWriter(ref _videoFramesJsonlWriter);
        _sessionActive = false;
    }

    public void LogEvent(WaterLiliesExperimentLogRow row)
    {
        WriteRow(row, "event", _eventsCsvWriter, _eventsJsonlWriter);
    }

    public void LogSample(WaterLiliesExperimentLogRow row)
    {
        if (!_sessionActive)
        {
            return;
        }

        row ??= new WaterLiliesExperimentLogRow();
        PrepareRow(row, "sample");
        WritePreparedSampleRow(row);

        row.row_type = "eye_tracking_sample";
        WritePreparedEyeTrackingRow(row);

        if (row.face_sample_available)
        {
            row.row_type = "face_tracking_sample";
            WritePreparedFaceTrackingRow(row);
        }
    }

    public bool TryLogVideoFrame(WaterLiliesVideoFrameLogRow row)
    {
        if (!_sessionActive)
        {
            return false;
        }

        row ??= new WaterLiliesVideoFrameLogRow();
        if (string.IsNullOrEmpty(row.session_id))
        {
            row.session_id = _sessionId;
        }

        if (string.IsNullOrEmpty(row.participant_id))
        {
            row.participant_id = _participantId;
        }

        if (string.IsNullOrEmpty(row.experiment_mode))
        {
            row.experiment_mode = _experimentMode;
        }

        var sampleRealtime = !double.IsNaN(row.realtime_since_startup_seconds)
            ? row.realtime_since_startup_seconds
            : Time.realtimeSinceStartupAsDouble;
        row.realtime_since_startup_seconds = sampleRealtime;
        row.session_elapsed_seconds = sampleRealtime - _sessionStartedRealtime;

        var timestamp = _sessionStartedUtc.AddSeconds(row.session_elapsed_seconds);
        row.utc_timestamp_iso = timestamp.ToString("O", CultureInfo.InvariantCulture);
        row.unix_time_ms = timestamp.ToUnixTimeMilliseconds();

        if (_writeCsv && _videoFramesCsvWriter != null)
        {
            _videoFramesCsvWriter.WriteLine(ToVideoFrameCsv(row));
            FlushIfNeeded(_videoFramesCsvWriter);
        }

        if (_writeJsonLines && _videoFramesJsonlWriter != null)
        {
            _videoFramesJsonlWriter.WriteLine(ToVideoFrameJsonLine(row));
            FlushIfNeeded(_videoFramesJsonlWriter);
        }

        return true;
    }

    void WriteRow(
        WaterLiliesExperimentLogRow row,
        string rowType,
        StreamWriter csvWriter,
        StreamWriter jsonlWriter)
    {
        if (!_sessionActive)
        {
            return;
        }

        row ??= new WaterLiliesExperimentLogRow();
        PrepareRow(row, rowType);
        WritePreparedRow(row, csvWriter, jsonlWriter);
    }

    void PrepareRow(WaterLiliesExperimentLogRow row, string rowType)
    {
        row.row_type = rowType;
        if (string.IsNullOrEmpty(row.session_id))
        {
            row.session_id = _sessionId;
        }

        var now = DateTimeOffset.UtcNow;
        row.utc_timestamp_iso = now.ToString("O", CultureInfo.InvariantCulture);
        row.unix_time_ms = now.ToUnixTimeMilliseconds();
        row.realtime_since_startup_seconds = Time.realtimeSinceStartupAsDouble;
        row.session_elapsed_seconds = row.realtime_since_startup_seconds - _sessionStartedRealtime;
    }

    void WritePreparedRow(
        WaterLiliesExperimentLogRow row,
        StreamWriter csvWriter,
        StreamWriter jsonlWriter)
    {
        if (!_sessionActive)
        {
            return;
        }

        if (_writeCsv && csvWriter != null)
        {
            csvWriter.WriteLine(ToCsv(row));
            FlushIfNeeded(csvWriter);
        }

        if (_writeJsonLines && jsonlWriter != null)
        {
            jsonlWriter.WriteLine(ToJsonLine(row));
            FlushIfNeeded(jsonlWriter);
        }
    }

    void WritePreparedSampleRow(WaterLiliesExperimentLogRow row)
    {
        if (!_sessionActive)
        {
            return;
        }

        if (_writeCsv && _samplesCsvWriter != null)
        {
            _samplesCsvWriter.WriteLine(ToSampleCsv(row));
            FlushIfNeeded(_samplesCsvWriter);
        }

        if (_writeJsonLines && _samplesJsonlWriter != null)
        {
            _samplesJsonlWriter.WriteLine(ToSampleJsonLine(row));
            FlushIfNeeded(_samplesJsonlWriter);
        }
    }

    void WritePreparedEyeTrackingRow(WaterLiliesExperimentLogRow row)
    {
        if (!_sessionActive)
        {
            return;
        }

        if (_writeCsv && _eyeTrackingCsvWriter != null)
        {
            _eyeTrackingCsvWriter.WriteLine(ToEyeTrackingCsv(row));
            FlushIfNeeded(_eyeTrackingCsvWriter);
        }

        if (_writeJsonLines && _eyeTrackingJsonlWriter != null)
        {
            _eyeTrackingJsonlWriter.WriteLine(ToEyeTrackingJsonLine(row));
            FlushIfNeeded(_eyeTrackingJsonlWriter);
        }
    }

    void WritePreparedFaceTrackingRow(WaterLiliesExperimentLogRow row)
    {
        if (!_sessionActive)
        {
            return;
        }

        if (_writeCsv && _faceTrackingCsvWriter != null)
        {
            _faceTrackingCsvWriter.WriteLine(ToFaceTrackingCsv(row));
            FlushIfNeeded(_faceTrackingCsvWriter);
        }

        if (_writeJsonLines && _faceTrackingJsonlWriter != null)
        {
            _faceTrackingJsonlWriter.WriteLine(ToFaceTrackingJsonLine(row));
            FlushIfNeeded(_faceTrackingJsonlWriter);
        }
    }

    StreamWriter OpenCsvWriter(string fileName, string[] columns)
    {
        var writer = new StreamWriter(Path.Combine(_sessionFolderPath, fileName), false, Encoding.UTF8);
        if (_writeExcelSeparatorDirective)
        {
            writer.WriteLine(CsvSeparatorDirective);
        }

        writer.WriteLine(string.Join(",", columns));
        return writer;
    }

    static string ResolveLogRoot(WaterLiliesExperimentConfig config, string overrideLogRootPath)
    {
        if (!string.IsNullOrWhiteSpace(overrideLogRootPath))
        {
            return overrideLogRootPath.Trim();
        }

        if (config != null && config.useExternalLogRoot)
        {
            return config.externalLogRootPath;
        }

        var folderName = config != null ? config.logFolderName : "water_lilies_vfx_experiment";
        return Path.Combine(Application.persistentDataPath, folderName);
    }

    static string ToCsv(WaterLiliesExperimentLogRow row)
    {
        var values = new[]
        {
            row.row_type,
            row.event_type,
            row.session_id,
            row.participant_id,
            row.experiment_mode,
            row.phase,
            row.condition_id,
            FormatInt(row.condition_order_index),
            FormatInt(row.condition_order_total),
            row.intensity_level,
            row.frequency_level,
            FormatDouble(row.intensity_value),
            FormatDouble(row.frequency_value),
            FormatDouble(row.applied_intensity_value),
            FormatDouble(row.applied_frequency_value),
            FormatBool(row.natural_modulation_enabled),
            FormatInt(row.natural_modulation_seed),
            FormatDouble(row.natural_modulation_template_elapsed_seconds),
            FormatFloat(row.natural_modulation_intensity_depth),
            FormatFloat(row.natural_modulation_frequency_depth),
            FormatFloat(row.natural_modulation_large_scale_seconds),
            FormatFloat(row.natural_modulation_medium_scale_seconds),
            FormatFloat(row.natural_modulation_fine_scale_seconds),
            FormatDouble(row.planned_duration_seconds),
            FormatDouble(row.phase_elapsed_seconds),
            FormatDouble(row.phase_remaining_seconds),
            FormatBool(row.formal_viewing),
            row.utc_timestamp_iso,
            row.unix_time_ms.ToString(CultureInfo.InvariantCulture),
            FormatDouble(row.realtime_since_startup_seconds),
            FormatDouble(row.session_elapsed_seconds),
            FormatBool(row.headset_presence_available),
            FormatBool(row.headset_user_present),
            FormatDouble(row.headset_off_interval_seconds),
            FormatBool(row.head_pose_available),
            FormatFloat(row.head_position_x),
            FormatFloat(row.head_position_y),
            FormatFloat(row.head_position_z),
            FormatFloat(row.head_rotation_x),
            FormatFloat(row.head_rotation_y),
            FormatFloat(row.head_rotation_z),
            FormatFloat(row.head_rotation_w),
            FormatFloat(row.head_velocity_x),
            FormatFloat(row.head_velocity_y),
            FormatFloat(row.head_velocity_z),
            FormatFloat(row.head_angular_velocity_deg_s),
            FormatBool(row.gaze_available),
            FormatFloat(row.gaze_origin_x),
            FormatFloat(row.gaze_origin_y),
            FormatFloat(row.gaze_origin_z),
            FormatFloat(row.gaze_direction_x),
            FormatFloat(row.gaze_direction_y),
            FormatFloat(row.gaze_direction_z),
            FormatBool(row.gaze_hit),
            FormatFloat(row.gaze_hit_x),
            FormatFloat(row.gaze_hit_y),
            FormatFloat(row.gaze_hit_z),
            FormatBool(row.gaze_on_painting),
            row.notes
        };

        return ToCsvLine(values);
    }

    static string ToSampleCsv(WaterLiliesExperimentLogRow row)
    {
        var values = new[]
        {
            row.row_type,
            row.event_type,
            row.session_id,
            row.participant_id,
            row.experiment_mode,
            row.phase,
            row.condition_id,
            FormatInt(row.condition_order_index),
            FormatInt(row.condition_order_total),
            row.intensity_level,
            row.frequency_level,
            FormatDouble(row.intensity_value),
            FormatDouble(row.frequency_value),
            FormatDouble(row.applied_intensity_value),
            FormatDouble(row.applied_frequency_value),
            FormatBool(row.natural_modulation_enabled),
            FormatInt(row.natural_modulation_seed),
            FormatDouble(row.natural_modulation_template_elapsed_seconds),
            FormatFloat(row.natural_modulation_intensity_depth),
            FormatFloat(row.natural_modulation_frequency_depth),
            FormatFloat(row.natural_modulation_large_scale_seconds),
            FormatFloat(row.natural_modulation_medium_scale_seconds),
            FormatFloat(row.natural_modulation_fine_scale_seconds),
            FormatDouble(row.planned_duration_seconds),
            FormatDouble(row.phase_elapsed_seconds),
            FormatDouble(row.phase_remaining_seconds),
            FormatBool(row.formal_viewing),
            row.utc_timestamp_iso,
            row.unix_time_ms.ToString(CultureInfo.InvariantCulture),
            FormatDouble(row.realtime_since_startup_seconds),
            FormatDouble(row.session_elapsed_seconds),
            FormatBool(row.headset_presence_available),
            FormatBool(row.headset_user_present),
            FormatDouble(row.headset_off_interval_seconds),
            FormatBool(row.head_pose_available),
            FormatFloat(row.head_position_x),
            FormatFloat(row.head_position_y),
            FormatFloat(row.head_position_z),
            FormatFloat(row.head_rotation_x),
            FormatFloat(row.head_rotation_y),
            FormatFloat(row.head_rotation_z),
            FormatFloat(row.head_rotation_w),
            FormatFloat(row.head_velocity_x),
            FormatFloat(row.head_velocity_y),
            FormatFloat(row.head_velocity_z),
            FormatFloat(row.head_angular_velocity_deg_s),
            row.notes
        };

        return ToCsvLine(values);
    }

    static string ToEyeTrackingCsv(WaterLiliesExperimentLogRow row)
    {
        var values = new[]
        {
            row.row_type,
            row.event_type,
            row.session_id,
            row.participant_id,
            row.experiment_mode,
            row.phase,
            row.condition_id,
            FormatInt(row.condition_order_index),
            FormatInt(row.condition_order_total),
            row.intensity_level,
            row.frequency_level,
            FormatDouble(row.intensity_value),
            FormatDouble(row.frequency_value),
            FormatDouble(row.applied_intensity_value),
            FormatDouble(row.applied_frequency_value),
            FormatBool(row.natural_modulation_enabled),
            FormatInt(row.natural_modulation_seed),
            FormatDouble(row.natural_modulation_template_elapsed_seconds),
            FormatFloat(row.natural_modulation_intensity_depth),
            FormatFloat(row.natural_modulation_frequency_depth),
            FormatFloat(row.natural_modulation_large_scale_seconds),
            FormatFloat(row.natural_modulation_medium_scale_seconds),
            FormatFloat(row.natural_modulation_fine_scale_seconds),
            FormatDouble(row.planned_duration_seconds),
            FormatDouble(row.phase_elapsed_seconds),
            FormatDouble(row.phase_remaining_seconds),
            FormatBool(row.formal_viewing),
            row.utc_timestamp_iso,
            row.unix_time_ms.ToString(CultureInfo.InvariantCulture),
            FormatDouble(row.realtime_since_startup_seconds),
            FormatDouble(row.session_elapsed_seconds),
            FormatBool(row.gaze_available),
            FormatFloat(row.gaze_origin_x),
            FormatFloat(row.gaze_origin_y),
            FormatFloat(row.gaze_origin_z),
            FormatFloat(row.gaze_direction_x),
            FormatFloat(row.gaze_direction_y),
            FormatFloat(row.gaze_direction_z),
            FormatBool(row.gaze_hit),
            FormatFloat(row.gaze_hit_x),
            FormatFloat(row.gaze_hit_y),
            FormatFloat(row.gaze_hit_z),
            FormatBool(row.gaze_on_painting),
            row.notes
        };

        return ToCsvLine(values);
    }

    static string ToFaceTrackingCsv(WaterLiliesExperimentLogRow row)
    {
        var values = new[]
        {
            row.row_type,
            row.event_type,
            row.session_id,
            row.participant_id,
            row.experiment_mode,
            row.phase,
            row.condition_id,
            FormatInt(row.condition_order_index),
            FormatInt(row.condition_order_total),
            row.intensity_level,
            row.frequency_level,
            FormatDouble(row.intensity_value),
            FormatDouble(row.frequency_value),
            FormatDouble(row.applied_intensity_value),
            FormatDouble(row.applied_frequency_value),
            FormatBool(row.natural_modulation_enabled),
            FormatInt(row.natural_modulation_seed),
            FormatDouble(row.natural_modulation_template_elapsed_seconds),
            FormatFloat(row.natural_modulation_intensity_depth),
            FormatFloat(row.natural_modulation_frequency_depth),
            FormatFloat(row.natural_modulation_large_scale_seconds),
            FormatFloat(row.natural_modulation_medium_scale_seconds),
            FormatFloat(row.natural_modulation_fine_scale_seconds),
            FormatDouble(row.planned_duration_seconds),
            FormatDouble(row.phase_elapsed_seconds),
            FormatDouble(row.phase_remaining_seconds),
            FormatBool(row.formal_viewing),
            row.utc_timestamp_iso,
            row.unix_time_ms.ToString(CultureInfo.InvariantCulture),
            FormatDouble(row.realtime_since_startup_seconds),
            FormatDouble(row.session_elapsed_seconds),
            FormatBool(row.face_sample_available),
            FormatBool(row.face_permission_granted),
            FormatBool(row.face_tracking_supported),
            FormatBool(row.face_tracking_enabled),
            FormatBool(row.face_expressions_found),
            FormatBool(row.face_expressions_enabled),
            FormatBool(row.face_state_available),
            FormatBool(row.face_valid),
            FormatBool(row.eye_following_blendshapes_valid),
            row.face_data_source,
            FormatDouble(row.face_state_time_seconds),
            FormatBool(row.face_region_confidences_available),
            FormatFloat(row.face_lower_region_confidence),
            FormatFloat(row.face_upper_region_confidence),
            FormatBool(row.face_visemes_valid),
            FormatInt(row.face_expression_count),
            FormatBool(row.eyes_closed_weights_available),
            FormatFloat(row.eyes_closed_l_raw),
            FormatFloat(row.eyes_closed_r_raw),
            FormatFloat(row.eyes_look_down_l),
            FormatFloat(row.eyes_look_down_r),
            FormatFloat(row.eyes_look_left_l),
            FormatFloat(row.eyes_look_left_r),
            FormatFloat(row.eyes_look_right_l),
            FormatFloat(row.eyes_look_right_r),
            FormatFloat(row.eyes_look_up_l),
            FormatFloat(row.eyes_look_up_r),
            FormatFloat(row.upper_lid_raiser_l),
            FormatFloat(row.upper_lid_raiser_r),
            FormatFloat(row.lid_tightener_l),
            FormatFloat(row.lid_tightener_r),
            FormatFloat(row.brow_lowerer_l),
            FormatFloat(row.brow_lowerer_r),
            FormatFloat(row.inner_brow_raiser_l),
            FormatFloat(row.inner_brow_raiser_r),
            FormatFloat(row.outer_brow_raiser_l),
            FormatFloat(row.outer_brow_raiser_r),
            FormatFloat(row.cheek_raiser_l),
            FormatFloat(row.cheek_raiser_r),
            FormatFloat(row.eyes_closed_l),
            FormatFloat(row.eyes_closed_r),
            FormatFloat(row.eye_closure_mean),
            FormatFloat(row.eye_closure_difference),
            FormatFloat(row.eye_closed_signal_min),
            FormatFloat(row.eye_closed_signal_max),
            FormatFloat(row.eye_closed_signal_range),
            FormatBool(row.eye_closed_signal_responsive),
            FormatBool(row.left_eye_closed_candidate),
            FormatBool(row.right_eye_closed_candidate),
            FormatBool(row.both_eyes_closed_candidate),
            FormatBool(row.left_eye_open_candidate),
            FormatBool(row.right_eye_open_candidate),
            FormatBool(row.both_eyes_open_candidate),
            FormatBool(row.eye_state_label_available),
            row.eye_state_label,
            FormatFloat(row.eye_state_confidence),
            FormatBool(row.left_eye_closed),
            FormatBool(row.right_eye_closed),
            FormatBool(row.both_eyes_closed),
            FormatBool(row.left_eye_open),
            FormatBool(row.right_eye_open),
            FormatBool(row.both_eyes_open),
            FormatBool(row.eye_tracking_supported),
            FormatBool(row.eye_tracking_enabled),
            FormatBool(row.eye_gazes_state_available),
            FormatDouble(row.eye_gazes_state_time_seconds),
            FormatBool(row.left_eye_gaze_valid),
            FormatBool(row.right_eye_gaze_valid),
            FormatFloat(row.left_eye_gaze_confidence),
            FormatFloat(row.right_eye_gaze_confidence),
            row.face_expression_weights,
            row.face_diagnostic,
            row.notes
        };

        return ToCsvLine(values);
    }

    static string ToVideoFrameCsv(WaterLiliesVideoFrameLogRow row)
    {
        var values = new[]
        {
            row.session_id,
            row.participant_id,
            row.experiment_mode,
            row.utc_timestamp_iso,
            row.unix_time_ms.ToString(CultureInfo.InvariantCulture),
            FormatDouble(row.realtime_since_startup_seconds),
            FormatDouble(row.session_elapsed_seconds),
            FormatInt(row.frame_index),
            FormatInt(row.unity_frame),
            FormatInt(row.width),
            FormatInt(row.height),
            FormatFloat(row.capture_fps),
            row.image_format,
            FormatInt(row.jpeg_quality),
            row.relative_path,
            row.absolute_path,
            row.source_camera_name,
            row.capture_camera_name,
            FormatFloat(row.camera_position.x),
            FormatFloat(row.camera_position.y),
            FormatFloat(row.camera_position.z),
            FormatFloat(row.camera_rotation.x),
            FormatFloat(row.camera_rotation.y),
            FormatFloat(row.camera_rotation.z),
            FormatFloat(row.camera_rotation.w),
            FormatFloat(row.camera_forward.x),
            FormatFloat(row.camera_forward.y),
            FormatFloat(row.camera_forward.z),
            FormatFloat(row.camera_up.x),
            FormatFloat(row.camera_up.y),
            FormatFloat(row.camera_up.z),
            FormatLong(row.encoded_bytes),
            FormatFloat(row.readback_latency_ms),
            FormatFloat(row.encode_write_latency_ms),
            FormatInt(row.dropped_frames),
            row.notes
        };

        return ToCsvLine(values);
    }

    static string ToCsvLine(string[] values)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            AppendCsvValue(builder, values[i]);
        }

        return builder.ToString();
    }

    public static string ToJsonLine(WaterLiliesExperimentLogRow row)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        AppendJson(builder, ref first, "row_type", row.row_type);
        AppendJson(builder, ref first, "event_type", row.event_type);
        AppendJson(builder, ref first, "session_id", row.session_id);
        AppendJson(builder, ref first, "participant_id", row.participant_id);
        AppendJson(builder, ref first, "experiment_mode", row.experiment_mode);
        AppendJson(builder, ref first, "phase", row.phase);
        AppendJson(builder, ref first, "condition_id", row.condition_id);
        AppendJson(builder, ref first, "condition_order_index", row.condition_order_index);
        AppendJson(builder, ref first, "condition_order_total", row.condition_order_total);
        AppendJson(builder, ref first, "intensity_level", row.intensity_level);
        AppendJson(builder, ref first, "frequency_level", row.frequency_level);
        AppendJson(builder, ref first, "intensity_value", row.intensity_value);
        AppendJson(builder, ref first, "frequency_value", row.frequency_value);
        AppendJson(builder, ref first, "applied_intensity_value", row.applied_intensity_value);
        AppendJson(builder, ref first, "applied_frequency_value", row.applied_frequency_value);
        AppendJson(builder, ref first, "natural_modulation_enabled", row.natural_modulation_enabled);
        AppendJson(builder, ref first, "natural_modulation_seed", row.natural_modulation_seed);
        AppendJson(builder, ref first, "natural_modulation_template_elapsed_seconds", row.natural_modulation_template_elapsed_seconds);
        AppendJson(builder, ref first, "natural_modulation_intensity_depth", row.natural_modulation_intensity_depth);
        AppendJson(builder, ref first, "natural_modulation_frequency_depth", row.natural_modulation_frequency_depth);
        AppendJson(builder, ref first, "natural_modulation_large_scale_seconds", row.natural_modulation_large_scale_seconds);
        AppendJson(builder, ref first, "natural_modulation_medium_scale_seconds", row.natural_modulation_medium_scale_seconds);
        AppendJson(builder, ref first, "natural_modulation_fine_scale_seconds", row.natural_modulation_fine_scale_seconds);
        AppendJson(builder, ref first, "planned_duration_seconds", row.planned_duration_seconds);
        AppendJson(builder, ref first, "phase_elapsed_seconds", row.phase_elapsed_seconds);
        AppendJson(builder, ref first, "phase_remaining_seconds", row.phase_remaining_seconds);
        AppendJson(builder, ref first, "formal_viewing", row.formal_viewing);
        AppendJson(builder, ref first, "utc_timestamp_iso", row.utc_timestamp_iso);
        AppendJson(builder, ref first, "unix_time_ms", row.unix_time_ms);
        AppendJson(builder, ref first, "realtime_since_startup_seconds", row.realtime_since_startup_seconds);
        AppendJson(builder, ref first, "session_elapsed_seconds", row.session_elapsed_seconds);
        AppendJson(builder, ref first, "headset_presence_available", row.headset_presence_available);
        AppendJson(builder, ref first, "headset_user_present", row.headset_user_present);
        AppendJson(builder, ref first, "headset_off_interval_seconds", row.headset_off_interval_seconds);
        AppendJson(builder, ref first, "head_pose_available", row.head_pose_available);
        AppendJson(builder, ref first, "head_position_x", row.head_position_x);
        AppendJson(builder, ref first, "head_position_y", row.head_position_y);
        AppendJson(builder, ref first, "head_position_z", row.head_position_z);
        AppendJson(builder, ref first, "head_rotation_x", row.head_rotation_x);
        AppendJson(builder, ref first, "head_rotation_y", row.head_rotation_y);
        AppendJson(builder, ref first, "head_rotation_z", row.head_rotation_z);
        AppendJson(builder, ref first, "head_rotation_w", row.head_rotation_w);
        AppendJson(builder, ref first, "head_velocity_x", row.head_velocity_x);
        AppendJson(builder, ref first, "head_velocity_y", row.head_velocity_y);
        AppendJson(builder, ref first, "head_velocity_z", row.head_velocity_z);
        AppendJson(builder, ref first, "head_angular_velocity_deg_s", row.head_angular_velocity_deg_s);
        AppendJson(builder, ref first, "gaze_available", row.gaze_available);
        AppendJson(builder, ref first, "gaze_origin_x", row.gaze_origin_x);
        AppendJson(builder, ref first, "gaze_origin_y", row.gaze_origin_y);
        AppendJson(builder, ref first, "gaze_origin_z", row.gaze_origin_z);
        AppendJson(builder, ref first, "gaze_direction_x", row.gaze_direction_x);
        AppendJson(builder, ref first, "gaze_direction_y", row.gaze_direction_y);
        AppendJson(builder, ref first, "gaze_direction_z", row.gaze_direction_z);
        AppendJson(builder, ref first, "gaze_hit", row.gaze_hit);
        AppendJson(builder, ref first, "gaze_hit_x", row.gaze_hit_x);
        AppendJson(builder, ref first, "gaze_hit_y", row.gaze_hit_y);
        AppendJson(builder, ref first, "gaze_hit_z", row.gaze_hit_z);
        AppendJson(builder, ref first, "gaze_on_painting", row.gaze_on_painting);
        AppendJson(builder, ref first, "notes", row.notes);
        builder.Append('}');
        return builder.ToString();
    }

    static string ToSampleJsonLine(WaterLiliesExperimentLogRow row)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        AppendCommonSampleJson(builder, ref first, row);
        AppendJson(builder, ref first, "headset_presence_available", row.headset_presence_available);
        AppendJson(builder, ref first, "headset_user_present", row.headset_user_present);
        AppendJson(builder, ref first, "headset_off_interval_seconds", row.headset_off_interval_seconds);
        AppendJson(builder, ref first, "head_pose_available", row.head_pose_available);
        AppendJson(builder, ref first, "head_position_x", row.head_position_x);
        AppendJson(builder, ref first, "head_position_y", row.head_position_y);
        AppendJson(builder, ref first, "head_position_z", row.head_position_z);
        AppendJson(builder, ref first, "head_rotation_x", row.head_rotation_x);
        AppendJson(builder, ref first, "head_rotation_y", row.head_rotation_y);
        AppendJson(builder, ref first, "head_rotation_z", row.head_rotation_z);
        AppendJson(builder, ref first, "head_rotation_w", row.head_rotation_w);
        AppendJson(builder, ref first, "head_velocity_x", row.head_velocity_x);
        AppendJson(builder, ref first, "head_velocity_y", row.head_velocity_y);
        AppendJson(builder, ref first, "head_velocity_z", row.head_velocity_z);
        AppendJson(builder, ref first, "head_angular_velocity_deg_s", row.head_angular_velocity_deg_s);
        AppendJson(builder, ref first, "notes", row.notes);
        builder.Append('}');
        return builder.ToString();
    }

    static string ToEyeTrackingJsonLine(WaterLiliesExperimentLogRow row)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        AppendCommonSampleJson(builder, ref first, row);
        AppendJson(builder, ref first, "gaze_available", row.gaze_available);
        AppendJson(builder, ref first, "gaze_origin_x", row.gaze_origin_x);
        AppendJson(builder, ref first, "gaze_origin_y", row.gaze_origin_y);
        AppendJson(builder, ref first, "gaze_origin_z", row.gaze_origin_z);
        AppendJson(builder, ref first, "gaze_direction_x", row.gaze_direction_x);
        AppendJson(builder, ref first, "gaze_direction_y", row.gaze_direction_y);
        AppendJson(builder, ref first, "gaze_direction_z", row.gaze_direction_z);
        AppendJson(builder, ref first, "gaze_hit", row.gaze_hit);
        AppendJson(builder, ref first, "gaze_hit_x", row.gaze_hit_x);
        AppendJson(builder, ref first, "gaze_hit_y", row.gaze_hit_y);
        AppendJson(builder, ref first, "gaze_hit_z", row.gaze_hit_z);
        AppendJson(builder, ref first, "gaze_on_painting", row.gaze_on_painting);
        AppendJson(builder, ref first, "notes", row.notes);
        builder.Append('}');
        return builder.ToString();
    }

    static string ToFaceTrackingJsonLine(WaterLiliesExperimentLogRow row)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        AppendCommonSampleJson(builder, ref first, row);
        AppendJson(builder, ref first, "face_sample_available", row.face_sample_available);
        AppendJson(builder, ref first, "face_permission_granted", row.face_permission_granted);
        AppendJson(builder, ref first, "face_tracking_supported", row.face_tracking_supported);
        AppendJson(builder, ref first, "face_tracking_enabled", row.face_tracking_enabled);
        AppendJson(builder, ref first, "face_expressions_found", row.face_expressions_found);
        AppendJson(builder, ref first, "face_expressions_enabled", row.face_expressions_enabled);
        AppendJson(builder, ref first, "face_state_available", row.face_state_available);
        AppendJson(builder, ref first, "face_valid", row.face_valid);
        AppendJson(builder, ref first, "eye_following_blendshapes_valid", row.eye_following_blendshapes_valid);
        AppendJson(builder, ref first, "face_data_source", row.face_data_source);
        AppendJson(builder, ref first, "face_state_time_seconds", row.face_state_time_seconds);
        AppendJson(builder, ref first, "face_region_confidences_available", row.face_region_confidences_available);
        AppendJson(builder, ref first, "face_lower_region_confidence", row.face_lower_region_confidence);
        AppendJson(builder, ref first, "face_upper_region_confidence", row.face_upper_region_confidence);
        AppendJson(builder, ref first, "face_visemes_valid", row.face_visemes_valid);
        AppendJson(builder, ref first, "face_expression_count", row.face_expression_count);
        AppendJson(builder, ref first, "eyes_closed_weights_available", row.eyes_closed_weights_available);
        AppendJson(builder, ref first, "eyes_closed_l_raw", row.eyes_closed_l_raw);
        AppendJson(builder, ref first, "eyes_closed_r_raw", row.eyes_closed_r_raw);
        AppendJson(builder, ref first, "eyes_look_down_l", row.eyes_look_down_l);
        AppendJson(builder, ref first, "eyes_look_down_r", row.eyes_look_down_r);
        AppendJson(builder, ref first, "eyes_look_left_l", row.eyes_look_left_l);
        AppendJson(builder, ref first, "eyes_look_left_r", row.eyes_look_left_r);
        AppendJson(builder, ref first, "eyes_look_right_l", row.eyes_look_right_l);
        AppendJson(builder, ref first, "eyes_look_right_r", row.eyes_look_right_r);
        AppendJson(builder, ref first, "eyes_look_up_l", row.eyes_look_up_l);
        AppendJson(builder, ref first, "eyes_look_up_r", row.eyes_look_up_r);
        AppendJson(builder, ref first, "upper_lid_raiser_l", row.upper_lid_raiser_l);
        AppendJson(builder, ref first, "upper_lid_raiser_r", row.upper_lid_raiser_r);
        AppendJson(builder, ref first, "lid_tightener_l", row.lid_tightener_l);
        AppendJson(builder, ref first, "lid_tightener_r", row.lid_tightener_r);
        AppendJson(builder, ref first, "brow_lowerer_l", row.brow_lowerer_l);
        AppendJson(builder, ref first, "brow_lowerer_r", row.brow_lowerer_r);
        AppendJson(builder, ref first, "inner_brow_raiser_l", row.inner_brow_raiser_l);
        AppendJson(builder, ref first, "inner_brow_raiser_r", row.inner_brow_raiser_r);
        AppendJson(builder, ref first, "outer_brow_raiser_l", row.outer_brow_raiser_l);
        AppendJson(builder, ref first, "outer_brow_raiser_r", row.outer_brow_raiser_r);
        AppendJson(builder, ref first, "cheek_raiser_l", row.cheek_raiser_l);
        AppendJson(builder, ref first, "cheek_raiser_r", row.cheek_raiser_r);
        AppendJson(builder, ref first, "eyes_closed_l", row.eyes_closed_l);
        AppendJson(builder, ref first, "eyes_closed_r", row.eyes_closed_r);
        AppendJson(builder, ref first, "eye_closure_mean", row.eye_closure_mean);
        AppendJson(builder, ref first, "eye_closure_difference", row.eye_closure_difference);
        AppendJson(builder, ref first, "eye_closed_signal_min", row.eye_closed_signal_min);
        AppendJson(builder, ref first, "eye_closed_signal_max", row.eye_closed_signal_max);
        AppendJson(builder, ref first, "eye_closed_signal_range", row.eye_closed_signal_range);
        AppendJson(builder, ref first, "eye_closed_signal_responsive", row.eye_closed_signal_responsive);
        AppendJson(builder, ref first, "left_eye_closed_candidate", row.left_eye_closed_candidate);
        AppendJson(builder, ref first, "right_eye_closed_candidate", row.right_eye_closed_candidate);
        AppendJson(builder, ref first, "both_eyes_closed_candidate", row.both_eyes_closed_candidate);
        AppendJson(builder, ref first, "left_eye_open_candidate", row.left_eye_open_candidate);
        AppendJson(builder, ref first, "right_eye_open_candidate", row.right_eye_open_candidate);
        AppendJson(builder, ref first, "both_eyes_open_candidate", row.both_eyes_open_candidate);
        AppendJson(builder, ref first, "eye_state_label_available", row.eye_state_label_available);
        AppendJson(builder, ref first, "eye_state_label", row.eye_state_label);
        AppendJson(builder, ref first, "eye_state_confidence", row.eye_state_confidence);
        AppendJson(builder, ref first, "left_eye_closed", row.left_eye_closed);
        AppendJson(builder, ref first, "right_eye_closed", row.right_eye_closed);
        AppendJson(builder, ref first, "both_eyes_closed", row.both_eyes_closed);
        AppendJson(builder, ref first, "left_eye_open", row.left_eye_open);
        AppendJson(builder, ref first, "right_eye_open", row.right_eye_open);
        AppendJson(builder, ref first, "both_eyes_open", row.both_eyes_open);
        AppendJson(builder, ref first, "eye_tracking_supported", row.eye_tracking_supported);
        AppendJson(builder, ref first, "eye_tracking_enabled", row.eye_tracking_enabled);
        AppendJson(builder, ref first, "eye_gazes_state_available", row.eye_gazes_state_available);
        AppendJson(builder, ref first, "eye_gazes_state_time_seconds", row.eye_gazes_state_time_seconds);
        AppendJson(builder, ref first, "left_eye_gaze_valid", row.left_eye_gaze_valid);
        AppendJson(builder, ref first, "right_eye_gaze_valid", row.right_eye_gaze_valid);
        AppendJson(builder, ref first, "left_eye_gaze_confidence", row.left_eye_gaze_confidence);
        AppendJson(builder, ref first, "right_eye_gaze_confidence", row.right_eye_gaze_confidence);
        AppendJson(builder, ref first, "face_expression_weights", row.face_expression_weights);
        AppendJson(builder, ref first, "face_diagnostic", row.face_diagnostic);
        AppendJson(builder, ref first, "notes", row.notes);
        builder.Append('}');
        return builder.ToString();
    }

    static void AppendCommonSampleJson(StringBuilder builder, ref bool first, WaterLiliesExperimentLogRow row)
    {
        AppendJson(builder, ref first, "row_type", row.row_type);
        AppendJson(builder, ref first, "event_type", row.event_type);
        AppendJson(builder, ref first, "session_id", row.session_id);
        AppendJson(builder, ref first, "participant_id", row.participant_id);
        AppendJson(builder, ref first, "experiment_mode", row.experiment_mode);
        AppendJson(builder, ref first, "phase", row.phase);
        AppendJson(builder, ref first, "condition_id", row.condition_id);
        AppendJson(builder, ref first, "condition_order_index", row.condition_order_index);
        AppendJson(builder, ref first, "condition_order_total", row.condition_order_total);
        AppendJson(builder, ref first, "intensity_level", row.intensity_level);
        AppendJson(builder, ref first, "frequency_level", row.frequency_level);
        AppendJson(builder, ref first, "intensity_value", row.intensity_value);
        AppendJson(builder, ref first, "frequency_value", row.frequency_value);
        AppendJson(builder, ref first, "applied_intensity_value", row.applied_intensity_value);
        AppendJson(builder, ref first, "applied_frequency_value", row.applied_frequency_value);
        AppendJson(builder, ref first, "natural_modulation_enabled", row.natural_modulation_enabled);
        AppendJson(builder, ref first, "natural_modulation_seed", row.natural_modulation_seed);
        AppendJson(builder, ref first, "natural_modulation_template_elapsed_seconds", row.natural_modulation_template_elapsed_seconds);
        AppendJson(builder, ref first, "natural_modulation_intensity_depth", row.natural_modulation_intensity_depth);
        AppendJson(builder, ref first, "natural_modulation_frequency_depth", row.natural_modulation_frequency_depth);
        AppendJson(builder, ref first, "natural_modulation_large_scale_seconds", row.natural_modulation_large_scale_seconds);
        AppendJson(builder, ref first, "natural_modulation_medium_scale_seconds", row.natural_modulation_medium_scale_seconds);
        AppendJson(builder, ref first, "natural_modulation_fine_scale_seconds", row.natural_modulation_fine_scale_seconds);
        AppendJson(builder, ref first, "planned_duration_seconds", row.planned_duration_seconds);
        AppendJson(builder, ref first, "phase_elapsed_seconds", row.phase_elapsed_seconds);
        AppendJson(builder, ref first, "phase_remaining_seconds", row.phase_remaining_seconds);
        AppendJson(builder, ref first, "formal_viewing", row.formal_viewing);
        AppendJson(builder, ref first, "utc_timestamp_iso", row.utc_timestamp_iso);
        AppendJson(builder, ref first, "unix_time_ms", row.unix_time_ms);
        AppendJson(builder, ref first, "realtime_since_startup_seconds", row.realtime_since_startup_seconds);
        AppendJson(builder, ref first, "session_elapsed_seconds", row.session_elapsed_seconds);
    }

    static string ToVideoFrameJsonLine(WaterLiliesVideoFrameLogRow row)
    {
        var builder = new StringBuilder();
        builder.Append('{');
        var first = true;
        AppendJson(builder, ref first, "session_id", row.session_id);
        AppendJson(builder, ref first, "participant_id", row.participant_id);
        AppendJson(builder, ref first, "experiment_mode", row.experiment_mode);
        AppendJson(builder, ref first, "utc_timestamp_iso", row.utc_timestamp_iso);
        AppendJson(builder, ref first, "unix_time_ms", row.unix_time_ms);
        AppendJson(builder, ref first, "realtime_since_startup_seconds", row.realtime_since_startup_seconds);
        AppendJson(builder, ref first, "session_elapsed_seconds", row.session_elapsed_seconds);
        AppendJson(builder, ref first, "frame_index", row.frame_index);
        AppendJson(builder, ref first, "unity_frame", row.unity_frame);
        AppendJson(builder, ref first, "width", row.width);
        AppendJson(builder, ref first, "height", row.height);
        AppendJson(builder, ref first, "capture_fps", row.capture_fps);
        AppendJson(builder, ref first, "image_format", row.image_format);
        AppendJson(builder, ref first, "jpeg_quality", row.jpeg_quality);
        AppendJson(builder, ref first, "relative_path", row.relative_path);
        AppendJson(builder, ref first, "absolute_path", row.absolute_path);
        AppendJson(builder, ref first, "source_camera_name", row.source_camera_name);
        AppendJson(builder, ref first, "capture_camera_name", row.capture_camera_name);
        AppendJson(builder, ref first, "camera_position_x", row.camera_position.x);
        AppendJson(builder, ref first, "camera_position_y", row.camera_position.y);
        AppendJson(builder, ref first, "camera_position_z", row.camera_position.z);
        AppendJson(builder, ref first, "camera_rotation_x", row.camera_rotation.x);
        AppendJson(builder, ref first, "camera_rotation_y", row.camera_rotation.y);
        AppendJson(builder, ref first, "camera_rotation_z", row.camera_rotation.z);
        AppendJson(builder, ref first, "camera_rotation_w", row.camera_rotation.w);
        AppendJson(builder, ref first, "camera_forward_x", row.camera_forward.x);
        AppendJson(builder, ref first, "camera_forward_y", row.camera_forward.y);
        AppendJson(builder, ref first, "camera_forward_z", row.camera_forward.z);
        AppendJson(builder, ref first, "camera_up_x", row.camera_up.x);
        AppendJson(builder, ref first, "camera_up_y", row.camera_up.y);
        AppendJson(builder, ref first, "camera_up_z", row.camera_up.z);
        AppendJson(builder, ref first, "encoded_bytes", row.encoded_bytes);
        AppendJson(builder, ref first, "readback_latency_ms", row.readback_latency_ms);
        AppendJson(builder, ref first, "encode_write_latency_ms", row.encode_write_latency_ms);
        AppendJson(builder, ref first, "dropped_frames", row.dropped_frames);
        AppendJson(builder, ref first, "notes", row.notes);
        builder.Append('}');
        return builder.ToString();
    }

    static void AppendCsvValue(StringBuilder builder, string value)
    {
        value ??= string.Empty;
        var mustQuote = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");
        if (!mustQuote)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\""));
        builder.Append('"');
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, string value)
    {
        AppendJsonName(builder, ref first, name);
        builder.Append('"');
        builder.Append(JsonEscape(value));
        builder.Append('"');
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, bool value)
    {
        AppendJsonName(builder, ref first, name);
        builder.Append(value ? "true" : "false");
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, int value)
    {
        AppendJsonName(builder, ref first, name);
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, long value)
    {
        AppendJsonName(builder, ref first, name);
        builder.Append(value.ToString(CultureInfo.InvariantCulture));
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, float value)
    {
        AppendJson(builder, ref first, name, (double)value);
    }

    static void AppendJson(StringBuilder builder, ref bool first, string name, double value)
    {
        AppendJsonName(builder, ref first, name);
        builder.Append(double.IsNaN(value) || double.IsInfinity(value)
            ? "null"
            : value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    static void AppendJsonName(StringBuilder builder, ref bool first, string name)
    {
        if (!first)
        {
            builder.Append(',');
        }

        first = false;
        builder.Append('"');
        builder.Append(name);
        builder.Append("\":");
    }

    static string JsonEscape(string value)
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

    static string FormatBool(bool value)
    {
        return value ? "true" : "false";
    }

    static string FormatInt(int value)
    {
        return value < 0 ? string.Empty : value.ToString(CultureInfo.InvariantCulture);
    }

    static string FormatLong(long value)
    {
        return value < 0 ? string.Empty : value.ToString(CultureInfo.InvariantCulture);
    }

    static string FormatFloat(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value)
            ? string.Empty
            : value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    static string FormatDouble(double value)
    {
        return double.IsNaN(value) || double.IsInfinity(value)
            ? string.Empty
            : value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    static string SafeFileName(string value)
    {
        value = string.IsNullOrWhiteSpace(value) ? "session" : value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    void FlushIfNeeded(StreamWriter writer)
    {
        if (_flushEveryWrite)
        {
            writer.Flush();
        }
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
}
