using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

public static class WaterLiliesExperimentValidator
{
    public const string ConfigAssetPath = "Assets/Resources/WaterLiliesExperimentConfig.asset";
    public const string ExperimentRootName = "Water Lilies Experiment";

    [MenuItem("Tools/Water Lilies Experiment/Validate Open Scene")]
    public static void ValidateOpenSceneMenu()
    {
        var report = ValidateOpenScene();
        if (report.success)
        {
            Debug.Log(report.Format());
            EditorUtility.DisplayDialog("Water Lilies Experiment", "Validation passed.", "OK");
            return;
        }

        Debug.LogError(report.Format());
        EditorUtility.DisplayDialog("Water Lilies Experiment", report.Format(), "OK");
    }

    public static ValidationReport ValidateOpenScene()
    {
        var report = new ValidationReport();
        var activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid() || !activeScene.isLoaded)
        {
            report.AddError("No valid open scene.");
            return report;
        }

        var config = AssetDatabase.LoadAssetAtPath<WaterLiliesExperimentConfig>(ConfigAssetPath);
        if (config == null)
        {
            report.AddError("Missing config asset: " + ConfigAssetPath);
            return report;
        }

        ValidateConditionConfig(config, report);

        var root = FindSceneObject(ExperimentRootName);
        if (root == null)
        {
            report.AddError("Missing scene root object: " + ExperimentRootName);
        }
        else
        {
            if (root.GetComponent<WaterLiliesExperimentManager>() == null)
            {
                report.AddError(ExperimentRootName + " is missing WaterLiliesExperimentManager.");
            }

            if (root.GetComponent<WaterLiliesExperimentLogger>() == null)
            {
                report.AddError(ExperimentRootName + " is missing WaterLiliesExperimentLogger.");
            }

            if (root.GetComponent<WaterLiliesTrackingSampler>() == null)
            {
                report.AddError(ExperimentRootName + " is missing WaterLiliesTrackingSampler.");
            }
        }

        var painting = FindSceneObject(config.targetPaintingObjectName);
        if (painting == null)
        {
            report.AddError("Missing target Water Lilies object: " + config.targetPaintingObjectName);
        }
        else
        {
            var visualEffect = painting.GetComponent<VisualEffect>();
            if (visualEffect == null)
            {
                report.AddError(config.targetPaintingObjectName + " is missing VisualEffect.");
            }
            else
            {
                ValidateVfxProperty(visualEffect, WaterLiliesVfxController.DefaultIntensityProperty, report);
                ValidateVfxProperty(visualEffect, WaterLiliesVfxController.DefaultFrequencyProperty, report);
            }
        }

        var markers = WaterLiliesExperimentManager.RequiredEventMarkers;
        if (markers.Length < 19)
        {
            report.AddError("Required event marker contract is incomplete.");
        }

        report.AddInfo("Scene: " + activeScene.path);
        report.AddInfo("Config: " + ConfigAssetPath);
        report.AddInfo("Target painting: " + config.targetPaintingObjectName);
        report.AddInfo("Required markers: " + string.Join(", ", markers));
        return report;
    }

    static void ValidateConditionConfig(WaterLiliesExperimentConfig config, ValidationReport report)
    {
        var conditions = new List<WaterLiliesResolvedCondition>();
        config.BuildResolvedConditionList(conditions);
        if (conditions.Count != 9)
        {
            report.AddError("Expected 9 resolved conditions in the configured order; found " + conditions.Count + ".");
            return;
        }

        if (config.mode == WaterLiliesExperimentMode.Formal)
        {
            if (!config.requireManualQuestionnaireContinue)
            {
                report.AddError("Formal mode requires manual questionnaire continue.");
            }

            if (!config.requireHeadsetCycleBeforeQuestionnaireContinue)
            {
                report.AddError("Formal mode requires each questionnaire break to record headset_removed and headset_worn.");
            }

            if (!config.writeCsv && !config.writeJsonLines)
            {
                report.AddError("Formal mode requires CSV and/or JSON Lines logging.");
            }
        }

        var seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < conditions.Count; i++)
        {
            var condition = conditions[i];
            if (!seen.Add(condition.conditionId))
            {
                report.AddError("Duplicate condition ID in order: " + condition.conditionId);
            }

            if (condition.intensityValue < 0f || condition.frequencyValue < 0f)
            {
                report.AddError(condition.conditionId + " has negative parameter values.");
            }

            if (condition.durationSeconds <= 0f)
            {
                report.AddError(condition.conditionId + " has non-positive duration.");
            }
        }

        for (var i = 1; i <= 9; i++)
        {
            var expected = "C" + i;
            if (!seen.Contains(expected))
            {
                report.AddError("Configured condition order is missing " + expected + ".");
            }
        }
    }

    static void ValidateVfxProperty(VisualEffect visualEffect, string propertyName, ValidationReport report)
    {
        if (visualEffect.visualEffectAsset == null)
        {
            report.AddError(visualEffect.name + " has no VisualEffectAsset assigned.");
            return;
        }

        if (!visualEffect.HasFloat(propertyName))
        {
            report.AddError(visualEffect.name + " VFX is missing exposed float: " + propertyName);
        }
    }

    static GameObject FindSceneObject(string objectName)
    {
        var objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (var i = 0; i < objects.Length; i++)
        {
            var candidate = objects[i];
            if (candidate == null ||
                !candidate.scene.IsValid() ||
                !candidate.scene.isLoaded ||
                candidate.hideFlags != HideFlags.None ||
                candidate.name != objectName)
            {
                continue;
            }

            return candidate;
        }

        return null;
    }

    public sealed class ValidationReport
    {
        readonly List<string> _errors = new List<string>();
        readonly List<string> _info = new List<string>();

        public bool success => _errors.Count == 0;
        public IReadOnlyList<string> errors => _errors;
        public IReadOnlyList<string> info => _info;

        public void AddError(string message)
        {
            _errors.Add(message);
        }

        public void AddInfo(string message)
        {
            _info.Add(message);
        }

        public string Format()
        {
            var builder = new StringBuilder();
            builder.AppendLine(success ? "Water Lilies Experiment validation passed." : "Water Lilies Experiment validation failed.");

            if (_errors.Count > 0)
            {
                builder.AppendLine("Errors:");
                for (var i = 0; i < _errors.Count; i++)
                {
                    builder.AppendLine("- " + _errors[i]);
                }
            }

            if (_info.Count > 0)
            {
                builder.AppendLine("Info:");
                for (var i = 0; i < _info.Count; i++)
                {
                    builder.AppendLine("- " + _info[i]);
                }
            }

            return builder.ToString();
        }
    }
}
