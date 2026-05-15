using NUnit.Framework;
using UnityEngine;

public sealed class VfxAutoAnimatorGroupControllerTests
{
    [Test]
    public void AppliesSharedSettingsToInactiveChildAnimators()
    {
        var root = new GameObject("Root");

        try
        {
            var activeChild = new GameObject("Active Child");
            activeChild.transform.SetParent(root.transform);
            var activeAnimator = activeChild.AddComponent<StarryNightRhoneVfxAutoAnimator>();

            var inactiveChild = new GameObject("Inactive Child");
            inactiveChild.transform.SetParent(root.transform);
            var inactiveAnimator = inactiveChild.AddComponent<StarryNightRhoneVfxAutoAnimator>();
            inactiveChild.SetActive(false);

            var controller = root.AddComponent<VfxAutoAnimatorGroupController>();
            controller.sharedSettings = new StarryNightRhoneVfxAutoAnimator.SharedSettings
            {
                initialParticleIntensity = 0.2f,
                initialParticleFrequency = 0.3f,
                initialStateDuration = 4f,
                stageDuration = 12f,
                updateInterval = 3f,
                returnToInitialDuration = 2f,
                randomRanges = new[]
                {
                    new StarryNightRhoneVfxAutoAnimator.RandomRange(0.2f, 0.4f),
                    new StarryNightRhoneVfxAutoAnimator.RandomRange(0.6f, 0.8f)
                },
                playOnStart = false,
                logValueChanges = false,
                logMissingProperties = false
            };

            controller.RefreshChildAnimators();
            controller.ApplyToChildAnimators();

            Assert.AreEqual(2, controller.childAnimatorCount);
            AssertSharedSettings(activeAnimator.CreateSharedSettingsSnapshot());
            AssertSharedSettings(inactiveAnimator.CreateSharedSettingsSnapshot());
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RestoreCapturedChildSettingsReturnsChildrenToPreviousValues()
    {
        var root = new GameObject("Root");

        try
        {
            var child = new GameObject("Child");
            child.transform.SetParent(root.transform);
            var animator = child.AddComponent<StarryNightRhoneVfxAutoAnimator>();
            animator.ApplySharedSettings(new StarryNightRhoneVfxAutoAnimator.SharedSettings
            {
                initialParticleIntensity = 0.11f,
                initialParticleFrequency = 0.12f,
                initialStateDuration = 1f,
                stageDuration = 2f,
                updateInterval = 0.5f,
                returnToInitialDuration = 3f,
                randomRanges = new[]
                {
                    new StarryNightRhoneVfxAutoAnimator.RandomRange(0.1f, 0.2f)
                },
                playOnStart = true,
                logValueChanges = true,
                logMissingProperties = true
            });

            var controller = root.AddComponent<VfxAutoAnimatorGroupController>();
            controller.sharedSettings = new StarryNightRhoneVfxAutoAnimator.SharedSettings
            {
                initialParticleIntensity = 0.8f,
                initialParticleFrequency = 0.9f,
                initialStateDuration = 8f,
                stageDuration = 9f,
                updateInterval = 1f,
                returnToInitialDuration = 7f,
                randomRanges = new[]
                {
                    new StarryNightRhoneVfxAutoAnimator.RandomRange(0.7f, 0.8f)
                },
                playOnStart = false,
                logValueChanges = false,
                logMissingProperties = false
            };

            controller.RefreshChildAnimators();
            controller.ApplyToChildAnimators();
            Assert.AreEqual(0.8f, animator.CreateSharedSettingsSnapshot().initialParticleIntensity);

            controller.RestoreCapturedChildSettings();

            var restored = animator.CreateSharedSettingsSnapshot();
            Assert.AreEqual(0.11f, restored.initialParticleIntensity);
            Assert.AreEqual(0.12f, restored.initialParticleFrequency);
            Assert.AreEqual(1f, restored.initialStateDuration);
            Assert.AreEqual(2f, restored.stageDuration);
            Assert.AreEqual(0.5f, restored.updateInterval);
            Assert.AreEqual(3f, restored.returnToInitialDuration);
            Assert.AreEqual(0.1f, restored.randomRanges[0].minimum);
            Assert.AreEqual(0.2f, restored.randomRanges[0].maximum);
            Assert.IsTrue(restored.playOnStart);
            Assert.IsTrue(restored.logValueChanges);
            Assert.IsTrue(restored.logMissingProperties);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static void AssertSharedSettings(StarryNightRhoneVfxAutoAnimator.SharedSettings settings)
    {
        Assert.AreEqual(0.2f, settings.initialParticleIntensity);
        Assert.AreEqual(0.3f, settings.initialParticleFrequency);
        Assert.AreEqual(4f, settings.initialStateDuration);
        Assert.AreEqual(12f, settings.stageDuration);
        Assert.AreEqual(3f, settings.updateInterval);
        Assert.AreEqual(2f, settings.returnToInitialDuration);
        Assert.AreEqual(2, settings.randomRanges.Length);
        Assert.AreEqual(0.2f, settings.randomRanges[0].minimum);
        Assert.AreEqual(0.4f, settings.randomRanges[0].maximum);
        Assert.AreEqual(0.6f, settings.randomRanges[1].minimum);
        Assert.AreEqual(0.8f, settings.randomRanges[1].maximum);
        Assert.IsFalse(settings.playOnStart);
        Assert.IsFalse(settings.logValueChanges);
        Assert.IsFalse(settings.logMissingProperties);
    }
}
