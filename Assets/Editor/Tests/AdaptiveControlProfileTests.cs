using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class AdaptiveControlProfileTests
{
    [Test]
    public void SharedAdaptiveControlProfileContainsExactTrainingGridAndBaseline()
    {
        var path = Path.Combine(Application.streamingAssetsPath, AdaptiveControlProfile.DefaultRelativePath);

        Assert.IsTrue(AdaptiveControlProfile.TryLoad(path, out var profile, out var error), error);
        Assert.AreEqual("adaptive-control-v1", profile.profileId);
        Assert.AreEqual(0.01f, profile.baseline.intensity);
        Assert.AreEqual(0f, profile.baseline.frequency);
        Assert.IsTrue(profile.TryGetValues("C5", out var c5));
        Assert.AreEqual(0.16f, c5.intensity);
        Assert.AreEqual(0.26f, c5.frequency);
    }

    [Test]
    public void AdaptiveControlProfileOnlyAllowsOneOrthogonalConditionStep()
    {
        var path = Path.Combine(Application.streamingAssetsPath, AdaptiveControlProfile.DefaultRelativePath);
        Assert.IsTrue(AdaptiveControlProfile.TryLoad(path, out var profile, out var error), error);

        Assert.IsTrue(profile.IsAdjacentOrSame("C5", "C2"));
        Assert.IsTrue(profile.IsAdjacentOrSame("C5", "C4"));
        Assert.IsTrue(profile.IsAdjacentOrSame("C5", "C6"));
        Assert.IsTrue(profile.IsAdjacentOrSame("C5", "C8"));
        Assert.IsFalse(profile.IsAdjacentOrSame("C5", "C9"));
        Assert.IsFalse(profile.IsAdjacentOrSame("C1", "C8"));
    }

    [Test]
    public void AdaptivePhysioSnapshotParsesNestedChannels()
    {
        const string json = "{\"message_type\":\"AdaptivePhysioSnapshot\",\"protocol_version\":\"adaptive-control-v1\",\"unix_time_ms\":123,\"lsl_eeg_stream_found\":true,\"lsl_eeg_sample_received\":true,\"stream_name\":\"02_007_5_eeg\",\"stream_type\":\"eeg\",\"channel_count\":9,\"expected_channel_count\":9,\"nominal_srate\":500.0,\"sample_rate_hz\":500.0,\"window_seconds\":10.0,\"sample_age_ms\":8.0,\"sample_count\":5000,\"max_points\":240,\"channels\":[{\"name\":\"T7\",\"source\":\"EEG\",\"unit\":\"uV\",\"raw_values\":[1.0,2.0],\"filtered_values\":[0.1,0.2],\"raw\":{\"sample_count\":2,\"mean\":1.5,\"std\":0.5,\"min\":1.0,\"max\":2.0,\"rms\":1.58,\"peak_to_peak\":1.0},\"filtered\":{\"sample_count\":2,\"mean\":0.15,\"std\":0.05,\"min\":0.1,\"max\":0.2,\"rms\":0.16,\"peak_to_peak\":0.1}}],\"reasons\":[\"ready\"]}";

        var snapshot = JsonUtility.FromJson<AdaptivePhysioSnapshot>(json);

        Assert.AreEqual("AdaptivePhysioSnapshot", snapshot.message_type);
        Assert.AreEqual("02_007_5_eeg", snapshot.stream_name);
        Assert.AreEqual(1, snapshot.channels.Length);
        Assert.AreEqual("T7", snapshot.channels[0].name);
        Assert.AreEqual(2, snapshot.channels[0].raw_values.Length);
        Assert.AreEqual(1.5f, snapshot.channels[0].raw.mean);
        Assert.AreEqual(0.1f, snapshot.channels[0].filtered.peak_to_peak);
    }

    [Test]
    public void AdaptiveStatusSnapshotParsesRealtimeModelDiagnostics()
    {
        const string json = "{\"message_type\":\"AdaptiveStatusSnapshot\",\"protocol_version\":\"adaptive-control-v1\",\"session_id\":\"s1\",\"unix_time_ms\":123,\"runtime_state\":\"Running\",\"next_decision_in_seconds\":1.2,\"current_condition\":\"C5\",\"target_condition\":\"C5\",\"model_bundle_id\":\"realtime_multimodal_window_v1\",\"model_version\":\"1.0.0\",\"model_variant\":\"full\",\"relaxation\":0.62,\"discomfort\":0.18,\"prediction_source\":\"window_multimodal_model\",\"model_supervision\":\"weak_window_supervision_v1\",\"motion_source\":\"HMD Motion\",\"model_input_feature_count\":111,\"model_available_feature_count\":108,\"model_missing_feature_count\":3,\"model_modalities_used\":[\"eeg\",\"ecg\",\"eye\",\"head\"],\"headset_presence_available\":true,\"headset_worn\":true,\"lsl_eeg_stream_found\":true,\"lsl_eeg_sample_received\":true,\"warmup_complete\":true,\"modality_names\":[\"ecg\",\"eeg\",\"eye\",\"head\"],\"modality_coverage_values\":[1,1,0.9,0.9],\"modality_age_values\":[10,10,20,20],\"candidate_conditions\":[\"C4\",\"C5\"],\"candidate_utility_values\":[0.4,0.5],\"last_command_id\":\"s1:1\",\"reasons\":[\"ready\"]}";

        var snapshot = JsonUtility.FromJson<AdaptiveStatusSnapshot>(json);

        Assert.AreEqual("realtime_multimodal_window_v1", snapshot.model_bundle_id);
        Assert.AreEqual("window_multimodal_model", snapshot.prediction_source);
        Assert.AreEqual("weak_window_supervision_v1", snapshot.model_supervision);
        Assert.AreEqual("HMD Motion", snapshot.motion_source);
        Assert.AreEqual(111, snapshot.model_input_feature_count);
        Assert.AreEqual(108, snapshot.model_available_feature_count);
        Assert.AreEqual(3, snapshot.model_missing_feature_count);
        Assert.AreEqual(4, snapshot.model_modalities_used.Length);
        Assert.AreEqual("head", snapshot.model_modalities_used[3]);
    }
}
