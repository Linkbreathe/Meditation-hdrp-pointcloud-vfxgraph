using NUnit.Framework;

public sealed class CalmnessFeedbackLoggerTests
{
    [Test]
    public void CalmnessScaleUsesWaterLabelsAndFourLevels()
    {
        Assert.AreEqual("\u6d9f\u6f2a", CalmnessFeedbackScale.GetLabel(1));
        Assert.AreEqual("\u5fae\u6f9c", CalmnessFeedbackScale.GetLabel(2));
        Assert.AreEqual("\u5e73\u6e56", CalmnessFeedbackScale.GetLabel(3));
        Assert.AreEqual("\u5982\u955c", CalmnessFeedbackScale.GetLabel(4));
        Assert.IsNull(CalmnessFeedbackScale.GetLabel(0));
    }

    [Test]
    public void TimeoutJsonWritesNullCalmnessFields()
    {
        var result = CalmnessFeedbackResult.Create(
            "water_lilies_01",
            0,
            0,
            0f,
            true,
            0f,
            false,
            142.3f);
        result.sessionId = "session";
        result.timestampUtc = "2026-05-13T10:23:41.0000000Z";

        var json = CalmnessFeedbackLogger.FormatJson(result);

        StringAssert.Contains("\"calmnessLevel\":null", json);
        StringAssert.Contains("\"calmnessLabel\":null", json);
        StringAssert.Contains("\"timedOut\":true", json);
        StringAssert.Contains("\"gazeEntryTimestamp\":null", json);
    }

    [Test]
    public void SelectionJsonIncludesLabelAndDwellDuration()
    {
        var result = CalmnessFeedbackResult.Create(
            "water_lilies_01",
            0,
            3,
            1.243f,
            false,
            1.8f,
            true,
            142.3f);
        result.sessionId = "session";
        result.timestampUtc = "2026-05-13T10:23:41.0000000Z";

        var json = CalmnessFeedbackLogger.FormatJson(result);

        StringAssert.Contains("\"calmnessLevel\":3", json);
        StringAssert.Contains("\"calmnessLabel\":\"\u5e73\u6e56\"", json);
        StringAssert.Contains("\"dwellDurationMs\":1243", json);
        StringAssert.Contains("\"timedOut\":false", json);
        StringAssert.Contains("\"gazeEntryTimestamp\":1.8", json);
    }
}
