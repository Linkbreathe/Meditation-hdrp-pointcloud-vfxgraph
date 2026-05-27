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
            AssertCondition(conditions[0], "C1", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Low, 0.2f, 0.2f);
            AssertCondition(conditions[1], "C2", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.Medium, 0.2f, 0.5f);
            AssertCondition(conditions[2], "C3", WaterLiliesParameterLevel.Low, WaterLiliesParameterLevel.High, 0.2f, 0.8f);
            AssertCondition(conditions[3], "C4", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Low, 0.5f, 0.2f);
            AssertCondition(conditions[4], "C5", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.Medium, 0.5f, 0.5f);
            AssertCondition(conditions[5], "C6", WaterLiliesParameterLevel.Medium, WaterLiliesParameterLevel.High, 0.5f, 0.8f);
            AssertCondition(conditions[6], "C7", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Low, 0.8f, 0.2f);
            AssertCondition(conditions[7], "C8", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.Medium, 0.8f, 0.5f);
            AssertCondition(conditions[8], "C9", WaterLiliesParameterLevel.High, WaterLiliesParameterLevel.High, 0.8f, 0.8f);
        }
        finally
        {
            Object.DestroyImmediate(config);
        }
    }

    [Test]
    public void DefaultResourceConfigAssetExistsAndUsesNinetySecondConditions()
    {
        var config = AssetDatabase.LoadAssetAtPath<WaterLiliesExperimentConfig>(
            WaterLiliesExperimentValidator.ConfigAssetPath);

        Assert.IsNotNull(config);
        Assert.AreEqual("P001", config.participantId);
        Assert.AreEqual("5_Water_Lilies", config.targetPaintingObjectName);
        Assert.AreEqual(90f, config.baselineSeconds);
        Assert.AreEqual(60f, config.adaptationSeconds);
        Assert.AreEqual(90f, config.conditionSeconds);
        Assert.AreEqual(0.01f, config.baselineIntensity);
        Assert.AreEqual(0f, config.baselineFrequency);
        Assert.AreEqual(7f, config.recenterSeconds);
        Assert.AreEqual(30f, config.restSeconds);
        Assert.IsTrue(config.requireManualQuestionnaireContinue);
        Assert.IsTrue(config.requireHeadsetWornBeforeQuestionnaireContinue);
        Assert.IsTrue(config.requireHeadsetCycleBeforeQuestionnaireContinue);

        var conditions = new List<WaterLiliesResolvedCondition>();
        config.BuildResolvedConditionList(conditions);
        Assert.AreEqual(9, conditions.Count);
        for (var i = 0; i < conditions.Count; i++)
        {
            Assert.AreEqual(90f, conditions[i].durationSeconds);
            Assert.AreEqual("C" + (i + 1), conditions[i].conditionId);
        }
    }

    [Test]
    public void RequiredMarkerContractIncludesArtifactSeparationEvents()
    {
        var markers = WaterLiliesExperimentManager.RequiredEventMarkers;

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
}
