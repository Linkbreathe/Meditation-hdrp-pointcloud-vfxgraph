using NUnit.Framework;
using UnityEngine;

public sealed class WaterLiliesNaturalVortexModulatorTests
{
    [Test]
    public void FractalEnvelopeStaysInSignedUnitRange()
    {
        for (var i = 0; i < 120; i++)
        {
            var envelope = WaterLiliesNaturalVortexModulator.EvaluateFractalEnvelope(
                i * 0.25,
                1439,
                17f,
                7f,
                2.5f,
                0f);

            Assert.GreaterOrEqual(envelope, -1f);
            Assert.LessOrEqual(envelope, 1f);
        }
    }

    [Test]
    public void ApplyDepthKeepsModulationAroundBaseValue()
    {
        Assert.AreEqual(0.088f, WaterLiliesNaturalVortexModulator.ApplyDepth(0.1f, 0.12f, -1f), 0.0001f);
        Assert.AreEqual(0.1f, WaterLiliesNaturalVortexModulator.ApplyDepth(0.1f, 0.12f, 0f), 0.0001f);
        Assert.AreEqual(0.112f, WaterLiliesNaturalVortexModulator.ApplyDepth(0.1f, 0.12f, 1f), 0.0001f);
    }

    [Test]
    public void ApplyDepthNeverReturnsNegativeValues()
    {
        Assert.AreEqual(0f, WaterLiliesNaturalVortexModulator.ApplyDepth(0f, 0.35f, -1f));
        Assert.GreaterOrEqual(WaterLiliesNaturalVortexModulator.ApplyDepth(0.1f, 10f, -1f), 0f);
    }

    [Test]
    public void TemplateElapsedRestartsFromZeroAfterPhaseReset()
    {
        var gameObject = new GameObject("natural vortex modulator test");
        try
        {
            var modulator = gameObject.AddComponent<WaterLiliesNaturalVortexModulator>();
            modulator.ResetTemplatePhaseAt(120.0);
            Assert.AreEqual(0.0, modulator.GetTemplateElapsedSeconds(120.0), 0.0001);
            Assert.AreEqual(3.5, modulator.GetTemplateElapsedSeconds(123.5), 0.0001);

            modulator.ResetTemplatePhaseAt(240.0);
            Assert.AreEqual(0.0, modulator.GetTemplateElapsedSeconds(240.0), 0.0001);
            Assert.AreEqual(3.5, modulator.GetTemplateElapsedSeconds(243.5), 0.0001);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
