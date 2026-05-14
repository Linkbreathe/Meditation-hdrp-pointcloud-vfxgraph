using NUnit.Framework;
using System.Reflection;
using UnityEngine;
using UnityEngine.VFX;

public sealed class PaintingRotationControllerTests
{
    [Test]
    public void GetNextIndexWrapsAtEndOfList()
    {
        Assert.AreEqual(1, PaintingRotationController.GetNextIndex(0, 3));
        Assert.AreEqual(0, PaintingRotationController.GetNextIndex(2, 3));
        Assert.AreEqual(-1, PaintingRotationController.GetNextIndex(0, 0));
    }

    [Test]
    public void SmoothStep01ClampsAndEasesProgress()
    {
        Assert.AreEqual(0f, PaintingRotationController.SmoothStep01(-1f));
        Assert.AreEqual(0.5f, PaintingRotationController.SmoothStep01(0.5f));
        Assert.AreEqual(1f, PaintingRotationController.SmoothStep01(2f));
    }

    [Test]
    public void StartButtonPressOnlyFiresOnRisingEdge()
    {
        Assert.IsTrue(PaintingRotationController.IsPressedThisFrame(true, false));
        Assert.IsFalse(PaintingRotationController.IsPressedThisFrame(true, true));
        Assert.IsFalse(PaintingRotationController.IsPressedThisFrame(false, true));
        Assert.IsFalse(PaintingRotationController.IsPressedThisFrame(false, false));
    }

    [Test]
    public void DefaultFadeDurationsAreSlowerThanPreviousSplitTransition()
    {
        Assert.Greater(PaintingRotationController.DefaultFadeOutDuration, 4f);
        Assert.Greater(PaintingRotationController.DefaultFadeInDuration, 4f);
    }

    [Test]
    public void DefaultDissolveHoldDurationLeavesOutgoingVisibleAfterSpawnStops()
    {
        Assert.Greater(PaintingRotationController.DefaultDissolveHoldDuration, 0f);
    }

    [Test]
    public void FeedbackTimeoutStaysInsideFadeOutWindowAfterDelay()
    {
        Assert.AreEqual(
            4.8f,
            PaintingRotationController.CalculateFeedbackTimeout(6f, 0.3f, 0.85f),
            0.0001f);
        Assert.AreEqual(0f, PaintingRotationController.CalculateFeedbackTimeout(0.2f, 0.3f, 0.85f));
    }

    [Test]
    public void CreatePaintingIdNormalizesSceneObjectNames()
    {
        Assert.AreEqual(
            "01_bright_to_dark_claude_monet_044",
            PaintingRotationController.CreatePaintingId("01_bright_to_dark_Claude_Monet_044", 0));
        Assert.AreEqual("painting_03", PaintingRotationController.CreatePaintingId("   ", 2));
    }

    [Test]
    public void RefreshPaintingsCollectsChildPaintingsWithRequiredComponents()
    {
        var root = new GameObject("paintings");

        try
        {
            CreatePainting("first", root.transform);
            CreatePainting("second", root.transform);
            new GameObject("ignored").transform.SetParent(root.transform);

            var rotationController = root.AddComponent<PaintingRotationController>();
            rotationController.RefreshPaintings();

            Assert.AreEqual(2, rotationController.paintingCount);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RefreshPaintingsDisablesChildAutoPlayWhileWaitingForStartButton()
    {
        var root = new GameObject("paintings");

        try
        {
            var animator = CreatePainting("first", root.transform);
            var rotationController = root.AddComponent<PaintingRotationController>();
            SetPrivateField(rotationController, "_waitForRightControllerAButtonBeforeStart", true);

            rotationController.RefreshPaintings();

            Assert.IsFalse(animator.playOnStart);
            Assert.IsTrue(rotationController.isWaitingForStartInput);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void StartGateHidesAllPaintingsUntilStartButton()
    {
        var root = new GameObject("paintings");

        try
        {
            var first = CreatePainting("first", root.transform).gameObject;
            var second = CreatePainting("second", root.transform).gameObject;
            var rotationController = root.AddComponent<PaintingRotationController>();
            SetPrivateField(rotationController, "_waitForRightControllerAButtonBeforeStart", true);

            InvokePrivateMethod(rotationController, "PrepareStartGate");

            Assert.IsFalse(first.activeSelf);
            Assert.IsFalse(second.activeSelf);
            Assert.IsTrue(rotationController.isWaitingForStartInput);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void SetPaintingVisibilityTogglesGameObjectAndRenderers()
    {
        var gameObject = GameObject.CreatePrimitive(PrimitiveType.Cube);

        try
        {
            var renderer = gameObject.GetComponent<Renderer>();
            renderer.enabled = false;

            PaintingRotationController.SetPaintingVisibility(gameObject, true);

            Assert.IsTrue(gameObject.activeSelf);
            Assert.IsTrue(renderer.enabled);

            PaintingRotationController.SetPaintingVisibility(gameObject, false);

            Assert.IsFalse(gameObject.activeSelf);
            Assert.IsFalse(renderer.enabled);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void ApplyOutgoingSpawnFadeOnlyChangesSpawnRate()
    {
        var outgoingObject = new GameObject("outgoing");

        try
        {
            var outgoing = AddVfxController(outgoingObject);
            outgoing.SetControls(100f, 0.4f, 0.8f, 0.6f, 0.3f, 0.2f);

            var outgoingSnapshot = PaintingRotationController.CreateSnapshot(outgoing);
            PaintingRotationController.ApplyOutgoingSpawnFade(outgoing, outgoingSnapshot, 0.5f);

            Assert.AreEqual(50f, outgoing.spawnRate);
            Assert.AreEqual(0.4f, outgoing.particleIntensity);
            Assert.AreEqual(0.6f, outgoing.particleFrequency);
            Assert.AreEqual(0.8f, outgoing.particleDrag);
        }
        finally
        {
            Object.DestroyImmediate(outgoingObject);
        }
    }

    [Test]
    public void ApplyIncomingSpawnFadeStartsAtZeroSpawnAndKeepsTargetParticleControls()
    {
        var incomingObject = new GameObject("incoming");

        try
        {
            var incoming = AddVfxController(incomingObject);
            incoming.SetControls(200f, 0.8f, 0.7f, 0.2f, 0.5f, 0.6f);

            var incomingSnapshot = PaintingRotationController.CreateSnapshot(incoming);
            PaintingRotationController.PrepareIncomingSpawnFade(incoming, incomingSnapshot);

            Assert.AreEqual(0f, incoming.spawnRate);
            Assert.AreEqual(0.8f, incoming.particleIntensity);
            Assert.AreEqual(0.2f, incoming.particleFrequency);

            PaintingRotationController.ApplyIncomingSpawnFade(incoming, incomingSnapshot, 0.5f);

            Assert.AreEqual(100f, incoming.spawnRate);
            Assert.AreEqual(0.8f, incoming.particleIntensity);
            Assert.AreEqual(0.2f, incoming.particleFrequency);
            Assert.AreEqual(0.7f, incoming.particleDrag);
        }
        finally
        {
            Object.DestroyImmediate(incomingObject);
        }
    }

    [Test]
    public void TransitionLogMessageIncludesPhasePaintingsProgressAndMilliseconds()
    {
        var message = PaintingRotationController.FormatTransitionLogMessage(
            "FadeOut",
            "A",
            "B",
            1.234f,
            12f,
            0.5f,
            12345.678f);

        StringAssert.Contains("[PaintingRotationController]", message);
        StringAssert.Contains("phase=FadeOut", message);
        StringAssert.Contains("from=\"A\"", message);
        StringAssert.Contains("to=\"B\"", message);
        StringAssert.Contains("elapsed=1234ms/12000ms", message);
        StringAssert.Contains("progress=50.0%", message);
        StringAssert.Contains("spawnRate=12345.678", message);
    }

    static StarryNightRhoneVfxAutoAnimator CreatePainting(string name, Transform parent)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent);
        AddVfxController(gameObject);
        return gameObject.AddComponent<StarryNightRhoneVfxAutoAnimator>();
    }

    static MonaLisaVfxController AddVfxController(GameObject gameObject)
    {
        gameObject.AddComponent<VisualEffect>();
        return gameObject.AddComponent<MonaLisaVfxController>();
    }

    static void SetPrivateField<T>(object target, string fieldName, T value)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, fieldName);
        field.SetValue(target, value);
    }

    static void InvokePrivateMethod(object target, string methodName)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, methodName);
        method.Invoke(target, null);
    }
}
