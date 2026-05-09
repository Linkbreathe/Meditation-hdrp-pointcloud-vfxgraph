using NUnit.Framework;
using UnityEngine;

public sealed class MonaLisaVfxControllerTests
{
    [Test]
    public void DefaultPropertyNamesMatchMonaLisaGraphBindingContract()
    {
        Assert.AreEqual("System Spawn - rate", MonaLisaVfxController.DefaultSpawnRateProperty);
        Assert.AreEqual("System update particle - intensity", MonaLisaVfxController.DefaultParticleIntensityProperty);
        Assert.AreEqual("System update particle - drag", MonaLisaVfxController.DefaultParticleDragProperty);
        Assert.AreEqual("System update particle - frequency", MonaLisaVfxController.DefaultParticleFrequencyProperty);
        Assert.AreEqual("System hdrp lit cube - smoothness", MonaLisaVfxController.DefaultCubeSmoothnessProperty);
        Assert.AreEqual("System hdrp lit cube - metallic", MonaLisaVfxController.DefaultCubeMetallicProperty);
    }

    [Test]
    public void VisibleVfxInputNameAliasesMatchGraphUiLabels()
    {
        Assert.AreEqual("System Spawn - Rate", MonaLisaVfxController.VisibleSpawnRateProperty);
        Assert.AreEqual("System update particle - Intensity", MonaLisaVfxController.VisibleParticleIntensityProperty);
        Assert.AreEqual("System update particle - Drag", MonaLisaVfxController.VisibleParticleDragProperty);
        Assert.AreEqual("System update particle - Frequency", MonaLisaVfxController.VisibleParticleFrequencyProperty);
        Assert.AreEqual("System hdrp lit cube - Smoothness", MonaLisaVfxController.VisibleCubeSmoothnessProperty);
        Assert.AreEqual("System hdrp lit cube - Metallic", MonaLisaVfxController.VisibleCubeMetallicProperty);
    }

    [Test]
    public void AnimatedParticleControlsUpdateIntensityAndFrequencyWithoutChangingDrag()
    {
        var gameObject = new GameObject("MonaLisaVfxController test");

        try
        {
            var controller = gameObject.AddComponent<MonaLisaVfxController>();
            controller.SetControls(100f, 0.2f, 0.8f, 0.3f, 0.4f, 0.5f);

            controller.SetAnimatedParticleControls(0.75f, 0.25f);

            Assert.AreEqual(0.75f, controller.particleIntensity);
            Assert.AreEqual(0.25f, controller.particleFrequency);
            Assert.AreEqual(0.8f, controller.particleDrag);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
