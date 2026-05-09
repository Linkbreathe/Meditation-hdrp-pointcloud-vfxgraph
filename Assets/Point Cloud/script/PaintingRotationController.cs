using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Painting Rotation Controller")]
public sealed class PaintingRotationController : MonoBehaviour
{
    public const float DefaultFadeOutDuration = 12f;
    public const float DefaultFadeInDuration = 12f;
    public const float DefaultDissolveHoldDuration = 8f;

    [Header("Paintings")]
    [SerializeField] Transform _paintingsRoot;
    [SerializeField] bool _preferActiveChildOnStart = true;
    [SerializeField, Min(0)] int _startIndex;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] float _fadeOutDuration = DefaultFadeOutDuration;
    [SerializeField, Min(0f)] float _dissolveHoldDuration = DefaultDissolveHoldDuration;
    [SerializeField, Min(0.01f)] float _fadeInDuration = DefaultFadeInDuration;

    [Header("Runtime")]
    [SerializeField] bool _playOnStart = true;
    [SerializeField] bool _logTransitions = true;
    [SerializeField, Min(0.05f)] float _progressLogInterval = 1f;

    readonly List<PaintingEntry> _paintings = new List<PaintingEntry>();
    Coroutine _rotationRoutine;
    StarryNightRhoneVfxAutoAnimator _subscribedAnimator;
    bool _currentAnimationCompleted;
    int _currentIndex = -1;

    public int paintingCount => _paintings.Count;
    public int currentIndex => _currentIndex;

    public static int GetNextIndex(int currentIndex, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        return (currentIndex + 1) % count;
    }

    public static float SmoothStep01(float progress)
    {
        var t = Mathf.Clamp01(progress);
        return t * t * (3f - 2f * t);
    }

    public static string FormatTransitionLogMessage(
        string phase,
        string fromName,
        string toName,
        float elapsedSeconds,
        float durationSeconds,
        float progress,
        float spawnRate)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "[PaintingRotationController] phase={0} from=\"{1}\" to=\"{2}\" elapsed={3}ms/{4}ms progress={5:0.0}% spawnRate={6:0.###}",
            phase,
            fromName,
            toName,
            SecondsToMilliseconds(elapsedSeconds),
            SecondsToMilliseconds(durationSeconds),
            Mathf.Clamp01(progress) * 100f,
            spawnRate);
    }

    public static VfxControlSnapshot CreateSnapshot(MonaLisaVfxController controller)
    {
        if (controller == null)
        {
            return VfxControlSnapshot.zero;
        }

        return new VfxControlSnapshot(
            controller.spawnRate,
            controller.particleIntensity,
            controller.particleFrequency);
    }

    public static void ApplyOutgoingSpawnFade(
        MonaLisaVfxController outgoing,
        VfxControlSnapshot outgoingStart,
        float progress)
    {
        var t = SmoothStep01(progress);
        ApplySnapshot(
            outgoing,
            new VfxControlSnapshot(
                Mathf.Lerp(outgoingStart.spawnRate, 0f, t),
                outgoingStart.particleIntensity,
                outgoingStart.particleFrequency));
    }

    public static void PrepareIncomingSpawnFade(
        MonaLisaVfxController incoming,
        VfxControlSnapshot incomingTarget)
    {
        ApplySnapshot(
            incoming,
            new VfxControlSnapshot(
                0f,
                incomingTarget.particleIntensity,
                incomingTarget.particleFrequency));
    }

    public static void ApplyIncomingSpawnFade(
        MonaLisaVfxController incoming,
        VfxControlSnapshot incomingTarget,
        float progress)
    {
        var t = SmoothStep01(progress);
        ApplySnapshot(
            incoming,
            new VfxControlSnapshot(
                Mathf.Lerp(0f, incomingTarget.spawnRate, t),
                incomingTarget.particleIntensity,
                incomingTarget.particleFrequency));
    }

    public static void SetPaintingVisibility(GameObject painting, bool isVisible)
    {
        if (painting == null)
        {
            return;
        }

        var renderers = painting.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = isVisible;
        }

        painting.SetActive(isVisible);
    }

    [ContextMenu("Refresh Paintings")]
    public void RefreshPaintings()
    {
        if (_paintingsRoot == null)
        {
            _paintingsRoot = transform;
        }

        _paintings.Clear();

        for (var i = 0; i < _paintingsRoot.childCount; i++)
        {
            var child = _paintingsRoot.GetChild(i);
            var controller = child.GetComponent<MonaLisaVfxController>();
            var animator = child.GetComponent<StarryNightRhoneVfxAutoAnimator>();
            if (controller == null || animator == null)
            {
                continue;
            }

            _paintings.Add(new PaintingEntry(
                child.gameObject,
                controller,
                animator,
                CreateSnapshot(controller)));
        }

        _currentIndex = ResolveInitialIndex();
    }

    [ContextMenu("Play Painting Rotation")]
    public void Play()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopRotation();
        RefreshPaintings();
        if (_paintings.Count == 0)
        {
            Debug.LogWarning(
                "PaintingRotationController could not find child paintings with MonaLisaVfxController and StarryNightRhoneVfxAutoAnimator.",
                this);
            return;
        }

        _rotationRoutine = StartCoroutine(RunRotationRoutine());
    }

    [ContextMenu("Stop Painting Rotation")]
    public void StopRotation()
    {
        if (_rotationRoutine == null)
        {
            return;
        }

        StopCoroutine(_rotationRoutine);
        _rotationRoutine = null;
        UnsubscribeFromCurrentAnimator();
    }

    void Reset()
    {
        _paintingsRoot = transform;
    }

    void Awake()
    {
        if (_paintingsRoot == null)
        {
            _paintingsRoot = transform;
        }

        RefreshPaintings();
    }

    void Start()
    {
        if (_playOnStart)
        {
            Play();
        }
    }

    void OnDisable()
    {
        StopRotation();
    }

    IEnumerator RunRotationRoutine()
    {
        ActivateOnly(_currentIndex);

        while (_paintings.Count > 0)
        {
            var current = _paintings[_currentIndex];
            yield return PlayAndWaitForCompletion(current);

            var nextIndex = GetNextIndex(_currentIndex, _paintings.Count);
            if (nextIndex < 0 || nextIndex == _currentIndex)
            {
                yield break;
            }

            yield return TransitionToNextPainting(nextIndex);
        }
    }

    IEnumerator PlayAndWaitForCompletion(PaintingEntry painting)
    {
        SetPaintingVisibility(painting.gameObject, true);
        SubscribeToAnimator(painting.animator);
        painting.animator.Play();

        while (!_currentAnimationCompleted && painting.gameObject.activeInHierarchy)
        {
            yield return null;
        }

        UnsubscribeFromCurrentAnimator();
    }

    IEnumerator TransitionToNextPainting(int nextIndex)
    {
        var outgoing = _paintings[_currentIndex];
        var incoming = _paintings[nextIndex];
        var outgoingStart = CreateSnapshot(outgoing.controller);
        var incomingTarget = incoming.visibleControls;
        var elapsed = 0f;
        var fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
        var dissolveHoldDuration = Mathf.Max(0f, _dissolveHoldDuration);
        var fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);

        LogTransition("FadeOutStart", outgoing, incoming, 0f, fadeOutDuration, 0f, outgoingStart.spawnRate);

        var nextProgressLogTime = _progressLogInterval;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            var progress = elapsed / fadeOutDuration;
            ApplyOutgoingSpawnFade(outgoing.controller, outgoingStart, progress);
            if (elapsed >= nextProgressLogTime && elapsed < fadeOutDuration)
            {
                LogTransition(
                    "FadeOutProgress",
                    outgoing,
                    incoming,
                    elapsed,
                    fadeOutDuration,
                    progress,
                    outgoing.controller.spawnRate);
                nextProgressLogTime += _progressLogInterval;
            }

            yield return null;
        }

        ApplyOutgoingSpawnFade(outgoing.controller, outgoingStart, 1f);
        LogTransition("FadeOutEnd", outgoing, incoming, fadeOutDuration, fadeOutDuration, 1f, outgoing.controller.spawnRate);

        if (dissolveHoldDuration > 0f)
        {
            LogTransition("DissolveHoldStart", outgoing, incoming, 0f, dissolveHoldDuration, 0f, outgoing.controller.spawnRate);

            elapsed = 0f;
            nextProgressLogTime = _progressLogInterval;
            while (elapsed < dissolveHoldDuration)
            {
                elapsed += Time.deltaTime;
                var progress = elapsed / dissolveHoldDuration;
                if (elapsed >= nextProgressLogTime && elapsed < dissolveHoldDuration)
                {
                    LogTransition(
                        "DissolveHoldProgress",
                        outgoing,
                        incoming,
                        elapsed,
                        dissolveHoldDuration,
                        progress,
                        outgoing.controller.spawnRate);
                    nextProgressLogTime += _progressLogInterval;
                }

                yield return null;
            }

            LogTransition(
                "DissolveHoldEnd",
                outgoing,
                incoming,
                dissolveHoldDuration,
                dissolveHoldDuration,
                1f,
                outgoing.controller.spawnRate);
        }

        SetPaintingVisibility(outgoing.gameObject, false);

        SetPaintingVisibility(incoming.gameObject, true);
        PrepareIncomingSpawnFade(incoming.controller, incomingTarget);
        LogTransition("FadeInStart", outgoing, incoming, 0f, fadeInDuration, 0f, incoming.controller.spawnRate);

        elapsed = 0f;
        nextProgressLogTime = _progressLogInterval;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            var progress = elapsed / fadeInDuration;
            ApplyIncomingSpawnFade(incoming.controller, incomingTarget, progress);
            if (elapsed >= nextProgressLogTime && elapsed < fadeInDuration)
            {
                LogTransition(
                    "FadeInProgress",
                    outgoing,
                    incoming,
                    elapsed,
                    fadeInDuration,
                    progress,
                    incoming.controller.spawnRate);
                nextProgressLogTime += _progressLogInterval;
            }

            yield return null;
        }

        ApplyIncomingSpawnFade(incoming.controller, incomingTarget, 1f);
        LogTransition("FadeInEnd", outgoing, incoming, fadeInDuration, fadeInDuration, 1f, incoming.controller.spawnRate);
        _currentIndex = nextIndex;
    }

    void ActivateOnly(int activeIndex)
    {
        for (var i = 0; i < _paintings.Count; i++)
        {
            SetPaintingVisibility(_paintings[i].gameObject, i == activeIndex);
        }
    }

    int ResolveInitialIndex()
    {
        if (_paintings.Count == 0)
        {
            return -1;
        }

        if (_preferActiveChildOnStart)
        {
            for (var i = 0; i < _paintings.Count; i++)
            {
                if (_paintings[i].gameObject.activeSelf)
                {
                    return i;
                }
            }
        }

        return Mathf.Clamp(_startIndex, 0, _paintings.Count - 1);
    }

    static void ApplySnapshot(MonaLisaVfxController controller, VfxControlSnapshot snapshot)
    {
        if (controller == null)
        {
            return;
        }

        controller.SetControls(
            snapshot.spawnRate,
            snapshot.particleIntensity,
            controller.particleDrag,
            snapshot.particleFrequency,
            controller.cubeSmoothness,
            controller.cubeMetallic);
    }

    void SubscribeToAnimator(StarryNightRhoneVfxAutoAnimator animator)
    {
        UnsubscribeFromCurrentAnimator();
        _currentAnimationCompleted = false;
        _subscribedAnimator = animator;

        if (_subscribedAnimator != null)
        {
            _subscribedAnimator.AnimationCompleted += HandleAnimationCompleted;
        }
    }

    void UnsubscribeFromCurrentAnimator()
    {
        if (_subscribedAnimator != null)
        {
            _subscribedAnimator.AnimationCompleted -= HandleAnimationCompleted;
            _subscribedAnimator = null;
        }

        _currentAnimationCompleted = false;
    }

    void HandleAnimationCompleted(StarryNightRhoneVfxAutoAnimator finishedAnimator)
    {
        if (finishedAnimator == _subscribedAnimator)
        {
            _currentAnimationCompleted = true;
        }
    }

    void OnValidate()
    {
        _fadeOutDuration = Mathf.Max(0.01f, _fadeOutDuration);
        _dissolveHoldDuration = Mathf.Max(0f, _dissolveHoldDuration);
        _fadeInDuration = Mathf.Max(0.01f, _fadeInDuration);
        _progressLogInterval = Mathf.Max(0.05f, _progressLogInterval);
    }

    void LogTransition(
        string phase,
        PaintingEntry outgoing,
        PaintingEntry incoming,
        float elapsedSeconds,
        float durationSeconds,
        float progress,
        float spawnRate)
    {
        if (!_logTransitions)
        {
            return;
        }

        Debug.Log(
            FormatTransitionLogMessage(
                phase,
                outgoing.gameObject.name,
                incoming.gameObject.name,
                elapsedSeconds,
                durationSeconds,
                progress,
                spawnRate) +
            string.Format(
                CultureInfo.InvariantCulture,
                " runtime={0}ms",
                SecondsToMilliseconds(Time.time)),
            this);
    }

    static int SecondsToMilliseconds(float seconds)
    {
        return Mathf.RoundToInt(Mathf.Max(0f, seconds) * 1000f);
    }

    sealed class PaintingEntry
    {
        public readonly GameObject gameObject;
        public readonly MonaLisaVfxController controller;
        public readonly StarryNightRhoneVfxAutoAnimator animator;
        public readonly VfxControlSnapshot visibleControls;

        public PaintingEntry(
            GameObject gameObject,
            MonaLisaVfxController controller,
            StarryNightRhoneVfxAutoAnimator animator,
            VfxControlSnapshot visibleControls)
        {
            this.gameObject = gameObject;
            this.controller = controller;
            this.animator = animator;
            this.visibleControls = visibleControls;
        }
    }

    public readonly struct VfxControlSnapshot
    {
        public static readonly VfxControlSnapshot zero = new VfxControlSnapshot(0f, 0f, 0f);

        public readonly float spawnRate;
        public readonly float particleIntensity;
        public readonly float particleFrequency;

        public VfxControlSnapshot(float spawnRate, float particleIntensity, float particleFrequency)
        {
            this.spawnRate = Mathf.Max(0f, spawnRate);
            this.particleIntensity = Mathf.Max(0f, particleIntensity);
            this.particleFrequency = Mathf.Max(0f, particleFrequency);
        }

        public static VfxControlSnapshot Lerp(VfxControlSnapshot from, VfxControlSnapshot to, float progress)
        {
            var t = Mathf.Clamp01(progress);
            return new VfxControlSnapshot(
                Mathf.Lerp(from.spawnRate, to.spawnRate, t),
                Mathf.Lerp(from.particleIntensity, to.particleIntensity, t),
                Mathf.Lerp(from.particleFrequency, to.particleFrequency, t));
        }
    }
}
