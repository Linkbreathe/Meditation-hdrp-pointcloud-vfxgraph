using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WaterLiliesExperimentConfigTests
{
    [Test]
    public void DefaultConfigBuildsNineFixedIntensityFrequencyConditions()
    {
        var config = ScriptableObject.CreateInstance<WaterLiliesExperimentConfig>();
        var conditions = new List<WaterLiliesResolvedCondition>();

        try
        {
            config.BuildResolvedConditionList(conditions);

            Assert.AreEqual(9, conditions.Count);
            AssertCondition(conditions[0], "C1", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Low, 0.15f, 0.15f);
            AssertCondition(conditions[1], "C2", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Medium, 0.15f, 0.4f);
            AssertCondition(conditions[2], "C3", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.High, 0.15f, 0.65f);
            AssertCondition(conditions[3], "C4", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Low, 0.4f, 0.15f);
            AssertCondition(conditions[4], "C5", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Medium, 0.4f, 0.4f);
            AssertCondition(conditions[5], "C6", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.High, 0.4f, 0.65f);
            AssertCondition(conditions[6], "C7", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Low, 0.65f, 0.15f);
            AssertCondition(conditions[7], "C8", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Medium, 0.65f, 0.4f);
            AssertCondition(conditions[8], "C9", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.High, 0.65f, 0.65f);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void DefaultResourceConfigAssetExistsAndUsesConfiguredDurations()
    {
        var config = AssetDatabase.LoadAssetAtPath<WaterLiliesExperimentConfig>(
            WaterLiliesExperimentValidator.ConfigAssetPath);

        Assert.IsNotNull(config);
        Assert.AreEqual("P001", config.participantId);
        Assert.AreEqual("5_Water_Lilies", config.targetPaintingObjectName);
        Assert.AreEqual(5f, config.baselineSeconds);
        Assert.AreEqual(6f, config.adaptationSeconds);
        Assert.AreEqual(9f, config.conditionSeconds);
        Assert.AreEqual(5f, config.formalDurations.baselineSeconds);
        Assert.AreEqual(5f, config.pilotDurations.baselineSeconds);
        Assert.AreEqual(0.15f, config.intensityValues.Get(WaterLiliesParameterLevel.Low));
        Assert.AreEqual(0.4f, config.intensityValues.Get(WaterLiliesParameterLevel.Medium));
        Assert.AreEqual(0.65f, config.intensityValues.Get(WaterLiliesParameterLevel.High));
        Assert.AreEqual(0.15f, config.frequencyValues.Get(WaterLiliesParameterLevel.Low));
        Assert.AreEqual(0.4f, config.frequencyValues.Get(WaterLiliesParameterLevel.Medium));
        Assert.AreEqual(0.65f, config.frequencyValues.Get(WaterLiliesParameterLevel.High));
        Assert.AreEqual(0.01f, config.baselineIntensity);
        Assert.AreEqual(0f, config.baselineFrequency);
        Assert.AreEqual(7f, config.recenterSeconds);
        Assert.AreEqual(3f, config.restSeconds);
        Assert.IsTrue(config.requireManualQuestionnaireContinue);
        Assert.IsTrue(config.requireHeadsetWornBeforeQuestionnaireContinue);
        Assert.IsTrue(config.requireHeadsetCycleBeforeQuestionnaireContinue);

        var conditions = new List<WaterLiliesResolvedCondition>();
        config.BuildResolvedConditionList(conditions);
        Assert.AreEqual(9, conditions.Count);
        for (var i = 0; i < conditions.Count; i++)
        {
            Assert.AreEqual(9f, conditions[i].durationSeconds);
            Assert.AreEqual("C" + (i + 1), conditions[i].conditionId);
        }
    }

    [Test]
    public void ModeSelectsIndependentDurationProfile()
    {
        var config = ScriptableObject.CreateInstance<WaterLiliesExperimentConfig>();

        try
        {
            var serialized = new SerializedObject(config);
            serialized.FindProperty("_durationProfilesMigrated").boolValue = true;
            SetDurationProfile(serialized.FindProperty("_formalDurations"), 50f, 60f, 90f, 0f, 7f, 30f, 3);
            SetDurationProfile(serialized.FindProperty("_pilotDurations"), 5f, 6f, 7f, 1f, 2f, 3f, 2);
            serialized.FindProperty("_mode").enumValueIndex = (int)WaterLiliesExperimentMode.Formal;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(50f, config.baselineSeconds);
            Assert.AreEqual(60f, config.adaptationSeconds);
            Assert.AreEqual(90f, config.conditionSeconds);
            Assert.AreEqual(0f, config.questionnaireMinimumSeconds);
            Assert.AreEqual(7f, config.recenterSeconds);
            Assert.AreEqual(30f, config.restSeconds);
            Assert.AreEqual(3, config.restEveryConditionCount);

            serialized.Update();
            serialized.FindProperty("_mode").enumValueIndex = (int)WaterLiliesExperimentMode.Pilot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(5f, config.baselineSeconds);
            Assert.AreEqual(6f, config.adaptationSeconds);
            Assert.AreEqual(7f, config.conditionSeconds);
            Assert.AreEqual(1f, config.questionnaireMinimumSeconds);
            Assert.AreEqual(2f, config.recenterSeconds);
            Assert.AreEqual(3f, config.restSeconds);
            Assert.AreEqual(2, config.restEveryConditionCount);

            var conditions = new List<WaterLiliesResolvedCondition>();
            config.BuildResolvedConditionList(conditions);
            Assert.AreEqual(9, conditions.Count);
            Assert.AreEqual(7f, conditions[0].durationSeconds);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void RequiredMarkerContractIncludesArtifactSeparationEvents()
    {
        var markers = WaterLiliesExperimentManager.RequiredEventMarkers;

        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventVideoRecordingStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventExperimentStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventBaselineStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventBaselineEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventAdaptationStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventAdaptationEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventConditionPrepare);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventConditionStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventConditionEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventQuestionnaireBreakStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventQuestionnaireBreakEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventHeadsetRemoved);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventHeadsetWorn);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventRecenterStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventRecenterEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventRestStart);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventRestEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventExperimentEnd);
        CollectionAssert.Contains(markers, WaterLiliesExperimentManager.EventVideoRecordingStop);
    }

    static void AssertCondition(
        WaterLiliesResolvedCondition condition,
        string conditionId,
        WaterLiliesParameterLevel intensityLevel,
        WaterLiliesParameterLevel frequencyLevel,
        float intensityValue,
        float frequencyValue)
    {
        Assert.AreEqual(conditionId, condition.conditionId);
        Assert.AreEqual(intensityLevel, condition.intensityLevel);
        Assert.AreEqual(frequencyLevel, condition.frequencyLevel);
        Assert.AreEqual(intensityValue, condition.intensityValue);
        Assert.AreEqual(frequencyValue, condition.frequencyValue);
        Assert.AreEqual(90f, condition.durationSeconds);
    }

    static void SetDurationProfile(
        SerializedProperty profile,
        float baselineSeconds,
        float adaptationSeconds,
        float conditionSeconds,
        float questionnaireMinimumSeconds,
        float recenterSeconds,
        float restSeconds,
        int restEveryConditionCount)
    {
        profile.FindPropertyRelative("_baselineSeconds").floatValue = baselineSeconds;
        profile.FindPropertyRelative("_adaptationSeconds").floatValue = adaptationSeconds;
        profile.FindPropertyRelative("_conditionSeconds").floatValue = conditionSeconds;
        profile.FindPropertyRelative("_questionnaireMinimumSeconds").floatValue = questionnaireMinimumSeconds;
        profile.FindPropertyRelative("_recenterSeconds").floatValue = recenterSeconds;
        profile.FindPropertyRelative("_restSeconds").floatValue = restSeconds;
        profile.FindPropertyRelative("_restEveryConditionCount").intValue = restEveryConditionCount;
    }
}
