using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class StarryNightRhoneVfxAutoAnimatorTests
{
    [Test]
    public void DefaultBindingUsesStarryNightGraphProperties()
    {
        Assert.AreEqual("Starry_Night_Over_the_Rhone", StarryNightRhoneVfxAutoAnimator.DefaultVfxEntityName);
        Assert.AreEqual("System update particle - intensity", StarryNightRhoneVfxAutoAnimator.DefaultParticleIntensityProperty);
        Assert.AreEqual("System update particle - frequency", StarryNightRhoneVfxAutoAnimator.DefaultParticleFrequencyProperty);
    }

    [Test]
    public void DefaultTimelineMatchesRequestedSequence()
    {
        var settings = StarryNightRhoneVfxAutoAnimator.CreateDefaultSettings();

        Assert.AreEqual(0.01f, settings.initialParticleIntensity);
        Assert.AreEqual(0f, settings.initialParticleFrequency);
        Assert.AreEqual(10f, settings.initialStateDuration);
        Assert.AreEqual(20f, settings.stageDuration);
        Assert.AreEqual(5f, settings.updateInterval);
        Assert.AreEqual(10f, settings.returnToInitialDuration);

        Assert.AreEqual(3, settings.randomRanges.Length);
        AssertRange(settings.randomRanges[0], 0.01f, 0.33f);
        AssertRange(settings.randomRanges[1], 0.34f, 0.66f);
        AssertRange(settings.randomRanges[2], 0.67f, 1f);

        Assert.AreEqual(4, StarryNightRhoneVfxAutoAnimator.GetUpdateCount(
            settings.stageDuration,
            settings.updateInterval));
        Assert.AreEqual(80f, StarryNightRhoneVfxAutoAnimator.CalculateTotalDuration(settings));
    }

    [Test]
    public void AnimationCompletedEventFiresWhenRoutineFinishes()
    {
        var gameObject = new GameObject("Starry_Night_Over_the_Rhone");

        try
        {
            var controller = gameObject.AddComponent<MonaLisaVfxController>();
            controller.SetControls(100f, 0.2f, 0.8f, 0.3f, 0.4f, 0.5f);

            var animator = gameObject.AddComponent<StarryNightRhoneVfxAutoAnimator>();
            SetPrivateField(animator, "_initialStateDuration", 0f);
            SetPrivateField(animator, "_stageDuration", 0f);
            SetPrivateField(animator, "_returnToInitialDuration", 0f);
            SetPrivateField(animator, "_randomRanges", new StarryNightRhoneVfxAutoAnimator.RandomRange[0]);

            var completed = false;
            StarryNightRhoneVfxAutoAnimator completedAnimator = null;
            animator.AnimationCompleted += finishedAnimator =>
            {
                completed = true;
                completedAnimator = finishedAnimator;
            };

            var routine = InvokePlayAnimationRoutine(animator);
            while (routine.MoveNext())
            {
            }

            Assert.IsTrue(completed);
            Assert.AreSame(animator, completedAnimator);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void DefaultAnimationOnlyDrivesIntensityAndFrequency()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "System update particle - intensity",
                "System update particle - frequency"
            },
            StarryNightRhoneVfxAutoAnimator.DrivenPropertyNames);

        CollectionAssert.DoesNotContain(
            StarryNightRhoneVfxAutoAnimator.DrivenPropertyNames,
            MonaLisaVfxController.DefaultParticleDragProperty);
    }

    [Test]
    public void ParticleControlLogMessageIncludesEntityAndDrivenValues()
    {
        var message = StarryNightRhoneVfxAutoAnimator.FormatParticleControlLogMessage(
            "Starry_Night_Over_the_Rhone",
            0.12345f,
            0.67891f);

        Assert.AreEqual(
            "StarryNightRhoneVfxAutoAnimator [Starry_Night_Over_the_Rhone] set particle intensity=0.123, particle frequency=0.679",
            message);
    }

    [Test]
    public void ParticleControlsPreferMonaLisaControllerWhenPresent()
    {
        var gameObject = new GameObject("Starry_Night_Over_the_Rhone");

        try
        {
            var controller = gameObject.AddComponent<MonaLisaVfxController>();
            controller.SetControls(100f, 0.2f, 0.8f, 0.3f, 0.4f, 0.5f);

            var animator = gameObject.AddComponent<StarryNightRhoneVfxAutoAnimator>();

            InvokeSetParticleControls(animator, 0.55f, 0.66f);

            Assert.AreEqual(0.55f, controller.particleIntensity);
            Assert.AreEqual(0.66f, controller.particleFrequency);
            Assert.AreEqual(0.8f, controller.particleDrag);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    static void AssertRange(StarryNightRhoneVfxAutoAnimator.RandomRange range, float minimum, float maximum)
    {
        Assert.AreEqual(minimum, range.minimum);
        Assert.AreEqual(maximum, range.maximum);
    }

    static void InvokeSetParticleControls(
        StarryNightRhoneVfxAutoAnimator animator,
        float particleIntensity,
        float particleFrequency)
    {
        var method = typeof(StarryNightRhoneVfxAutoAnimator).GetMethod(
            "SetParticleControls",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(animator, new object[] { particleIntensity, particleFrequency });
    }

    static System.Collections.IEnumerator InvokePlayAnimationRoutine(StarryNightRhoneVfxAutoAnimator animator)
    {
        var method = typeof(StarryNightRhoneVfxAutoAnimator).GetMethod(
            "PlayAnimationRoutine",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (System.Collections.IEnumerator)method.Invoke(animator, null);
    }

    static void SetPrivateField<T>(StarryNightRhoneVfxAutoAnimator animator, string fieldName, T value)
    {
        var field = typeof(StarryNightRhoneVfxAutoAnimator).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        field.SetValue(animator, value);
    }
}
