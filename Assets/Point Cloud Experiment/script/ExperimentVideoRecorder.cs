using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

[DefaultExecutionOrder(1300)]
[DisallowMultipleComponent]
[AddComponentMenu("Point Cloud/Experiment Video Recorder")]
public sealed class ExperimentVideoRecorder : MonoBehaviour
{
    const string CenterEyeAnchorName = "CenterEyeAnchor";
    const string FramesFolderName = "video_frames";
    const string ManifestFileName = "video_manifest.json";

    public enum FrameImageFormat
    {
        Jpg,
        Png
    }

    [Header("Session")]
    [SerializeField] bool _autoStartWithExperimentSession = true;
    [SerializeField] MeditationExperimentCsvLogger _experimentCsvLogger;

    [Header("View")]
    [SerializeField] bool _autoFindReferences = true;
    [SerializeField] Transform _viewTransform;
    [SerializeField] Camera _sourceCamera;

    [Header("Capture")]
    [SerializeField] bool _captureXrRenderPass = true;
    [SerializeField, Min(0)] int _xrViewIndex;
    [SerializeField] bool _fallbackToCameraCaptureBridge;
    [SerializeField] bool _matchXrEyeTextureAspect;
    [SerializeField, Min(16)] int _captureWidth = 1280;
    [SerializeField, Min(16)] int _captureHeight = 720;
    [SerializeField] bool _preserveSourceAspect = true;
    [SerializeField] bool _cropToFillOutput = true;
    [SerializeField, Min(0.5f)] float _captureFps = 15f;
    [SerializeField] FrameImageFormat _imageFormat = FrameImageFormat.Jpg;
    [SerializeField, Range(1, 100)] int _jpegQuality = 95;
    [SerializeField, Min(1)] int _maxPendingReadbacks = 2;
    [SerializeField] bool _flipVertically = true;

    [Header("Logging")]
    [SerializeField] bool _logLifecycle = true;
    [SerializeField] bool _logDroppedFrames;

    static ExperimentVideoRecorder _instance;
    static readonly List<XRDisplaySubsystem> XrDisplays = new List<XRDisplaySubsystem>(1);

    Action<RenderTargetIdentifier, CommandBuffer> _captureAction;
    RenderTexture _renderTexture;
    RenderTexture _eyeCopyTexture;
    Camera _registeredCamera;
    bool _registeredRenderPipelineCapture;
    string _sessionFolderPath;
    string _framesFolderPath;
    int _frameIndex;
    int _droppedFrames;
    int _pendingReadbacks;
    int _recordingGeneration;
    int _activeCaptureWidth;
    int _activeCaptureHeight;
    int _activeSourceWidth;
    int _activeSourceHeight;
    int _activeSourceSlice;
    double _nextCaptureRealtime;
    double _captureIntervalSeconds;
    bool _isRecording;
    bool _releaseRenderTextureWhenPendingComplete;
    bool _readbackWarningLogged;
    bool _writeWarningLogged;
    bool _blackFrameWarningLogged;
    bool _xrRenderPassWarningLogged;
    string _captureMethodName = "XRDisplayRenderPass";

    public static ExperimentVideoRecorder Instance
    {
        get
        {
            if (_instance != null)
            {
                return _instance;
            }

            _instance = FindObjectOfType<ExperimentVideoRecorder>();
            if (_instance != null)
            {
                return _instance;
            }

            var gameObject = new GameObject("ExperimentVideoRecorder");
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<ExperimentVideoRecorder>();
            return _instance;
        }
    }

    public bool isRecording => _isRecording;
    public string framesFolderPath => _framesFolderPath;
    public int recordedFrameCount => _frameIndex;
    public int droppedFrameCount => _droppedFrames;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            return;
        }

        _instance = this;
        _captureAction = CaptureCameraFrame;
        if (!_captureXrRenderPass && !_fallbackToCameraCaptureBridge)
        {
            _captureXrRenderPass = true;
        }

        if (_autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    void Update()
    {
        if (_autoFindReferences && HasMissingReferences())
        {
            AutoFindReferences();
        }

        if (!_autoStartWithExperimentSession)
        {
            return;
        }

        var logger = ResolveExperimentCsvLogger(false);
        if (logger != null && logger.sessionActive)
        {
            if (!_isRecording)
            {
                StartRecording(logger, "auto_session_active");
            }

            return;
        }

        if (_isRecording)
        {
            StopRecording("session_inactive");
        }
    }

    void OnDisable()
    {
        StopRecording("component_disabled");
    }

    void OnDestroy()
    {
        _recordingGeneration++;
        UnregisterCaptureAction();
        ReleaseRenderTexture();
        ReleaseEyeCopyTexture();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    void OnValidate()
    {
        _captureWidth = Mathf.Max(16, _captureWidth);
        _captureHeight = Mathf.Max(16, _captureHeight);
        _captureFps = Mathf.Max(0.5f, _captureFps);
        _jpegQuality = Mathf.Clamp(_jpegQuality, 1, 100);
        _maxPendingReadbacks = Mathf.Max(1, _maxPendingReadbacks);
        _xrViewIndex = Mathf.Max(0, _xrViewIndex);
    }

    [ContextMenu("Start Recording Active Session")]
    public void StartRecordingForActiveSession()
    {
        StartRecording(ResolveExperimentCsvLogger(false), "context_menu");
    }

    [ContextMenu("Stop Recording")]
    public void StopRecordingFromContextMenu()
    {
        StopRecording("context_menu");
    }

    public bool StartRecording(MeditationExperimentCsvLogger logger, string reason = "")
    {
        if (!isActiveAndEnabled)
        {
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because the recorder component is disabled.", this);
            return false;
        }

        if (_isRecording)
        {
            return true;
        }

        _experimentCsvLogger = logger != null ? logger : ResolveExperimentCsvLogger(false);
        if (_experimentCsvLogger == null || !_experimentCsvLogger.sessionActive)
        {
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because there is no active experiment session.", this);
            return false;
        }

        if (_autoFindReferences)
        {
            AutoFindReferences();
        }

        if (_sourceCamera == null)
        {
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because no XR source camera was found.", this);
            return false;
        }

        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because AsyncGPUReadback is not supported on this device.", this);
            return false;
        }

        if (!EnsureCaptureTarget())
        {
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because the capture target could not be prepared.", this);
            return false;
        }

        _sessionFolderPath = _experimentCsvLogger.sessionFolderPath;
        _framesFolderPath = Path.Combine(_sessionFolderPath, FramesFolderName);
        Directory.CreateDirectory(_framesFolderPath);

        _frameIndex = 0;
        _droppedFrames = 0;
        _pendingReadbacks = 0;
        _readbackWarningLogged = false;
        _writeWarningLogged = false;
        _blackFrameWarningLogged = false;
        _xrRenderPassWarningLogged = false;
        _releaseRenderTextureWhenPendingComplete = false;
        _captureIntervalSeconds = 1.0 / Mathf.Max(0.5f, _captureFps);
        _nextCaptureRealtime = Time.realtimeSinceStartupAsDouble;
        _activeSourceWidth = 0;
        _activeSourceHeight = 0;
        _activeSourceSlice = 0;
        _captureMethodName = _captureXrRenderPass ? "XRDisplayRenderPass" : "CameraCaptureBridge";
        _isRecording = true;
        _recordingGeneration++;

        if (!RegisterCaptureAction())
        {
            _isRecording = false;
            Debug.LogWarning("[ExperimentVideoRecorder] Cannot start recording because the camera capture action could not be registered.", this);
            return false;
        }

        WriteManifest("started", reason);
        LogExperimentEvent("video_recording_started", reason);

        if (_logLifecycle)
        {
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[ExperimentVideoRecorder] Recording started. method={0} camera={1} size={2}x{3} fps={4:0.##} folder={5}",
                    _captureMethodName,
                    _sourceCamera != null ? _sourceCamera.name : string.Empty,
                    _activeCaptureWidth,
                    _activeCaptureHeight,
                    _captureFps,
                    _framesFolderPath),
                this);
        }

        return true;
    }

    public void StopRecording(string reason = "")
    {
        if (!_isRecording)
        {
            return;
        }

        _isRecording = false;
        UnregisterCaptureAction();

        LogExperimentEvent("video_recording_stopped", reason);
        WriteManifest("stopped", reason);

        if (_pendingReadbacks > 0)
        {
            _releaseRenderTextureWhenPendingComplete = true;
        }
        else
        {
            ReleaseRenderTexture();
        }

        if (_logLifecycle)
        {
            Debug.Log(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "[ExperimentVideoRecorder] Recording stopped. frames={0} dropped={1} pending={2} reason={3}",
                    _frameIndex,
                    _droppedFrames,
                    _pendingReadbacks,
                    reason),
                this);
        }
    }

    void CaptureXrRenderPassFrame(ScriptableRenderContext context, Camera camera)
    {
        if (!_isRecording || camera == null || camera != _sourceCamera)
        {
            return;
        }

        var now = Time.realtimeSinceStartupAsDouble;
        if (now + 0.0001 < _nextCaptureRealtime)
        {
            return;
        }

        AdvanceNextCaptureTime(now);
        if (_pendingReadbacks >= _maxPendingReadbacks)
        {
            _droppedFrames++;
            if (_logDroppedFrames)
            {
                Debug.LogWarning("[ExperimentVideoRecorder] Dropped video frame because GPU readback is still pending.", this);
            }

            return;
        }

        if (!TryGetXrRenderPassSource(camera, out var renderPass, out var renderParam, out var sourceX, out var sourceY, out var sourceWidth, out var sourceHeight, out var sourceSlice))
        {
            _droppedFrames++;
            return;
        }

        if (!EnsureEyeCopyTarget(renderPass.renderTargetDesc, sourceWidth, sourceHeight) || !EnsureCaptureTarget())
        {
            _droppedFrames++;
            return;
        }

        _activeSourceWidth = sourceWidth;
        _activeSourceHeight = sourceHeight;
        _activeSourceSlice = sourceSlice;

        var frame = CreateFrameMetadata(now);
        var generation = _recordingGeneration;
        _pendingReadbacks++;

        var commandBuffer = CommandBufferPool.Get("Experiment XR Render Pass Capture");
        commandBuffer.CopyTexture(
            renderPass.renderTarget,
            sourceSlice,
            0,
            sourceX,
            sourceY,
            sourceWidth,
            sourceHeight,
            _eyeCopyTexture,
            0,
            0,
            0,
            0);
        commandBuffer.SetRenderTarget(_renderTexture);
        commandBuffer.SetViewport(new Rect(0f, 0f, _activeCaptureWidth, _activeCaptureHeight));
        commandBuffer.ClearRenderTarget(false, true, Color.clear);
        BlitEyeCopyToCaptureTarget(commandBuffer);
        commandBuffer.RequestAsyncReadback(
            _renderTexture,
            0,
            TextureFormat.RGB24,
            request => HandleAsyncReadback(request, frame, generation));
        context.ExecuteCommandBuffer(commandBuffer);
        context.Submit();
        CommandBufferPool.Release(commandBuffer);
    }

    void BlitEyeCopyToCaptureTarget(CommandBuffer commandBuffer)
    {
        if (!_preserveSourceAspect || _activeSourceWidth <= 0 || _activeSourceHeight <= 0 || _activeCaptureWidth <= 0 || _activeCaptureHeight <= 0)
        {
            commandBuffer.Blit(_eyeCopyTexture, BuiltinRenderTextureType.CurrentActive);
            return;
        }

        var sourceAspect = _activeSourceWidth / (float)_activeSourceHeight;
        var targetAspect = _activeCaptureWidth / (float)_activeCaptureHeight;
        var scale = Vector2.one;
        var offset = Vector2.zero;

        if (_cropToFillOutput)
        {
            if (sourceAspect < targetAspect)
            {
                scale.y = Mathf.Clamp01(sourceAspect / targetAspect);
                offset.y = (1f - scale.y) * 0.5f;
            }
            else if (sourceAspect > targetAspect)
            {
                scale.x = Mathf.Clamp01(targetAspect / sourceAspect);
                offset.x = (1f - scale.x) * 0.5f;
            }
        }
        else
        {
            if (sourceAspect < targetAspect)
            {
                var viewportWidth = Mathf.RoundToInt(_activeCaptureHeight * sourceAspect);
                var viewportX = Mathf.RoundToInt((_activeCaptureWidth - viewportWidth) * 0.5f);
                commandBuffer.SetViewport(new Rect(viewportX, 0f, viewportWidth, _activeCaptureHeight));
            }
            else if (sourceAspect > targetAspect)
            {
                var viewportHeight = Mathf.RoundToInt(_activeCaptureWidth / sourceAspect);
                var viewportY = Mathf.RoundToInt((_activeCaptureHeight - viewportHeight) * 0.5f);
                commandBuffer.SetViewport(new Rect(0f, viewportY, _activeCaptureWidth, viewportHeight));
            }
        }

        commandBuffer.Blit(_eyeCopyTexture, BuiltinRenderTextureType.CurrentActive, scale, offset);
    }

    void CaptureCameraFrame(RenderTargetIdentifier source, CommandBuffer commandBuffer)
    {
        if (!_isRecording || commandBuffer == null)
        {
            return;
        }

        var now = Time.realtimeSinceStartupAsDouble;
        if (now + 0.0001 < _nextCaptureRealtime)
        {
            return;
        }

        AdvanceNextCaptureTime(now);
        if (_pendingReadbacks >= _maxPendingReadbacks)
        {
            _droppedFrames++;
            if (_logDroppedFrames)
            {
                Debug.LogWarning("[ExperimentVideoRecorder] Dropped video frame because GPU readback is still pending.", this);
            }

            return;
        }

        if (!EnsureCaptureTarget())
        {
            _droppedFrames++;
            return;
        }

        var frame = CreateFrameMetadata(now);
        var generation = _recordingGeneration;
        _pendingReadbacks++;

        commandBuffer.SetRenderTarget(_renderTexture);
        commandBuffer.SetViewport(new Rect(0f, 0f, _activeCaptureWidth, _activeCaptureHeight));
        commandBuffer.ClearRenderTarget(false, true, Color.clear);
        commandBuffer.Blit(source, BuiltinRenderTextureType.CurrentActive);
        commandBuffer.RequestAsyncReadback(
            _renderTexture,
            0,
            TextureFormat.RGB24,
            request => HandleAsyncReadback(request, frame, generation));
    }

    void AdvanceNextCaptureTime(double now)
    {
        if (_nextCaptureRealtime <= 0.0)
        {
            _nextCaptureRealtime = now + _captureIntervalSeconds;
            return;
        }

        _nextCaptureRealtime += _captureIntervalSeconds;
        if (now - _nextCaptureRealtime > _captureIntervalSeconds)
        {
            _nextCaptureRealtime = now + _captureIntervalSeconds;
        }
    }

    void HandleAsyncReadback(AsyncGPUReadbackRequest request, FrameMetadata frame, int generation)
    {
        _pendingReadbacks = Mathf.Max(0, _pendingReadbacks - 1);

        if (generation != _recordingGeneration)
        {
            ReleaseRenderTextureIfReady();
            return;
        }

        if (request.hasError)
        {
            _droppedFrames++;
            if (!_readbackWarningLogged)
            {
                _readbackWarningLogged = true;
                Debug.LogWarning("[ExperimentVideoRecorder] AsyncGPUReadback failed; video frame was dropped.", this);
            }

            ReleaseRenderTextureIfReady();
            return;
        }

        var readbackFinishedAt = Time.realtimeSinceStartupAsDouble;
        var raw = request.GetData<byte>().ToArray();
        var readbackLatencyMs = (float)((readbackFinishedAt - frame.sampleRealtime) * 1000.0);
        EncodeAndWriteFrame(raw, frame, readbackLatencyMs);
        ReleaseRenderTextureIfReady();
    }

    void EncodeAndWriteFrame(byte[] rawData, FrameMetadata frame, float readbackLatencyMs)
    {
        if (rawData == null || rawData.Length == 0)
        {
            _droppedFrames++;
            return;
        }

        var encodeStartedAt = Time.realtimeSinceStartupAsDouble;
        if (_flipVertically)
        {
            FlipRowsInPlace(rawData, frame.width, frame.height, 3);
        }

        var blackFrameDetected = IsProbablyBlackFrame(rawData, frame.width, frame.height, 3);
        if (blackFrameDetected && !_blackFrameWarningLogged)
        {
            _blackFrameWarningLogged = true;
            Debug.LogWarning(
                "[ExperimentVideoRecorder] Captured video frame is entirely black. If this persists, the selected capture source is not exposing a readable XR eye color buffer on this runtime.",
                this);
        }

        var texture = new Texture2D(frame.width, frame.height, TextureFormat.RGB24, false, false);
        texture.LoadRawTextureData(rawData);
        texture.Apply(false, false);
        var encoded = _imageFormat == FrameImageFormat.Png
            ? ImageConversion.EncodeToPNG(texture)
            : ImageConversion.EncodeToJPG(texture, _jpegQuality);
        UnityEngine.Object.Destroy(texture);
        if (encoded == null || encoded.Length == 0)
        {
            _droppedFrames++;
            return;
        }

        try
        {
            File.WriteAllBytes(frame.absolutePath, encoded);
        }
        catch (Exception ex)
        {
            _droppedFrames++;
            if (!_writeWarningLogged)
            {
                _writeWarningLogged = true;
                Debug.LogWarning("[ExperimentVideoRecorder] Failed to write a video frame: " + ex.Message, this);
            }

            return;
        }

        var encodeWriteLatencyMs = (float)((Time.realtimeSinceStartupAsDouble - encodeStartedAt) * 1000.0);

        var logger = _experimentCsvLogger;
        if (logger != null && logger.sessionActive)
        {
            logger.TryLogVideoFrame(new MeditationExperimentCsvLogger.VideoFrameRow
            {
                sampleRealtime = frame.sampleRealtime,
                frameIndex = frame.frameIndex,
                unityFrame = frame.unityFrame,
                width = frame.width,
                height = frame.height,
                captureFps = _captureFps,
                imageFormat = GetImageFormatName(),
                jpegQuality = _jpegQuality,
                relativePath = frame.relativePath,
                absolutePath = frame.absolutePath,
                sourceCameraName = frame.sourceCameraName,
                captureCameraName = frame.captureCameraName,
                cameraPosition = frame.cameraPosition,
                cameraRotation = frame.cameraRotation,
                cameraForward = frame.cameraForward,
                cameraUp = frame.cameraUp,
                encodedBytes = encoded.LongLength,
                readbackLatencyMs = readbackLatencyMs,
                encodeWriteLatencyMs = encodeWriteLatencyMs,
                droppedFrames = _droppedFrames,
                notes = blackFrameDetected ? frame.notes + "; blackFrameDetected=True" : frame.notes
            });
        }
    }

    FrameMetadata CreateFrameMetadata(double sampleRealtime)
    {
        var frameIndex = ++_frameIndex;
        var fileName = string.Format(CultureInfo.InvariantCulture, "frame_{0:000000}.{1}", frameIndex, GetFrameFileExtension());
        var view = ResolveViewTransform();

        return new FrameMetadata
        {
            frameIndex = frameIndex,
            unityFrame = Time.frameCount,
            sampleRealtime = sampleRealtime,
            width = _activeCaptureWidth,
            height = _activeCaptureHeight,
            relativePath = FramesFolderName + "/" + fileName,
            absolutePath = Path.Combine(_framesFolderPath, fileName),
            sourceCameraName = _sourceCamera != null ? _sourceCamera.name : string.Empty,
            captureCameraName = _captureMethodName,
            cameraPosition = view != null ? view.position : Vector3.zero,
            cameraRotation = view != null ? view.rotation : Quaternion.identity,
            cameraForward = view != null ? view.forward : Vector3.forward,
            cameraUp = view != null ? view.up : Vector3.up,
            notes = string.Format(
                CultureInfo.InvariantCulture,
                "captureMethod={0}; frameTap={1}; xrViewIndex={2}; xrSource={3}x{4}; xrSourceSlice={5}; outputAspect={6:0.######}; preserveSourceAspect={7}; cropToFill={8}; flipVertically={9}; xrEnabled={10}; xrEyeTexture={11}x{12}; matchXrEyeAspect={13}",
                _captureMethodName,
                _captureMethodName == "XRDisplayRenderPass" ? "EndCameraRendering" : "AfterPostProcess",
                _xrViewIndex,
                _activeSourceWidth,
                _activeSourceHeight,
                _activeSourceSlice,
                _activeCaptureHeight > 0 ? _activeCaptureWidth / (float)_activeCaptureHeight : 0f,
                _preserveSourceAspect,
                _cropToFillOutput,
                _flipVertically,
                XRSettings.enabled,
                XRSettings.eyeTextureWidth,
                XRSettings.eyeTextureHeight,
                _matchXrEyeTextureAspect)
        };
    }

    bool EnsureCaptureTarget()
    {
        ResolveCaptureDimensions(out var width, out var height);
        if (_renderTexture == null ||
            _renderTexture.width != width ||
            _renderTexture.height != height)
        {
            ReleaseRenderTexture();
            _renderTexture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB)
            {
                name = "Experiment XR Frame Capture Target",
                antiAliasing = 1,
                autoGenerateMips = false,
                useMipMap = false
            };
            _renderTexture.Create();
            _activeCaptureWidth = width;
            _activeCaptureHeight = height;
        }

        return _renderTexture != null && _renderTexture.IsCreated();
    }

    bool EnsureEyeCopyTarget(RenderTextureDescriptor sourceDesc, int width, int height)
    {
        width = Mathf.Max(16, width);
        height = Mathf.Max(16, height);

        var desc = sourceDesc;
        desc.width = width;
        desc.height = height;
        desc.depthBufferBits = 0;
        desc.dimension = TextureDimension.Tex2D;
        desc.volumeDepth = 1;
        desc.vrUsage = VRTextureUsage.None;
        desc.useMipMap = false;
        desc.autoGenerateMips = false;

        if (_eyeCopyTexture == null ||
            _eyeCopyTexture.width != width ||
            _eyeCopyTexture.height != height ||
            _eyeCopyTexture.descriptor.graphicsFormat != desc.graphicsFormat)
        {
            ReleaseEyeCopyTexture();
            _eyeCopyTexture = new RenderTexture(desc)
            {
                name = "Experiment XR Eye Copy Target",
                autoGenerateMips = false,
                useMipMap = false
            };
            _eyeCopyTexture.Create();
        }

        return _eyeCopyTexture != null && _eyeCopyTexture.IsCreated();
    }

    bool TryGetXrRenderPassSource(
        Camera camera,
        out XRDisplaySubsystem.XRRenderPass renderPass,
        out XRDisplaySubsystem.XRRenderParameter renderParam,
        out int sourceX,
        out int sourceY,
        out int sourceWidth,
        out int sourceHeight,
        out int sourceSlice)
    {
        renderPass = default;
        renderParam = default;
        sourceX = 0;
        sourceY = 0;
        sourceWidth = 0;
        sourceHeight = 0;
        sourceSlice = 0;

        XrDisplays.Clear();
        SubsystemManager.GetSubsystems(XrDisplays);
        XRDisplaySubsystem display = null;
        for (var i = 0; i < XrDisplays.Count; i++)
        {
            var candidate = XrDisplays[i];
            if (candidate != null && candidate.running && candidate.GetRenderPassCount() > 0)
            {
                display = candidate;
                break;
            }
        }

        if (display == null)
        {
            LogXrRenderPassWarning("no running XRDisplaySubsystem with render passes");
            return false;
        }

        display.GetRenderPass(0, out renderPass);
        var parameterCount = renderPass.GetRenderParameterCount();
        if (parameterCount <= 0)
        {
            LogXrRenderPassWarning("XR render pass has no render parameters");
            return false;
        }

        var parameterIndex = Mathf.Clamp(_xrViewIndex, 0, parameterCount - 1);
        renderPass.GetRenderParameter(camera, parameterIndex, out renderParam);

        var desc = renderPass.renderTargetDesc;
        var viewport = renderParam.viewport;
        sourceX = Mathf.Clamp(Mathf.RoundToInt(viewport.x * desc.width), 0, Mathf.Max(0, desc.width - 1));
        sourceY = Mathf.Clamp(Mathf.RoundToInt(viewport.y * desc.height), 0, Mathf.Max(0, desc.height - 1));
        sourceWidth = Mathf.Clamp(Mathf.RoundToInt(viewport.width * desc.width), 1, desc.width - sourceX);
        sourceHeight = Mathf.Clamp(Mathf.RoundToInt(viewport.height * desc.height), 1, desc.height - sourceY);
        sourceSlice = Mathf.Max(0, renderParam.textureArraySlice);

        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            LogXrRenderPassWarning("XR render pass resolved to an empty source viewport");
            return false;
        }

        return true;
    }

    void ResolveCaptureDimensions(out int width, out int height)
    {
        width = Mathf.Max(16, _captureWidth);
        height = Mathf.Max(16, _captureHeight);

        if (_isRecording &&
            _activeCaptureWidth > 0 &&
            _activeCaptureHeight > 0 &&
            XRSettings.eyeTextureWidth <= 0 &&
            XRSettings.eyeTextureHeight <= 0)
        {
            width = _activeCaptureWidth;
            height = _activeCaptureHeight;
            return;
        }

        if (!_matchXrEyeTextureAspect)
        {
            width = MakeEven(width);
            height = MakeEven(height);
            return;
        }

        var sourceWidth = XRSettings.eyeTextureWidth;
        var sourceHeight = XRSettings.eyeTextureHeight;
        if ((sourceWidth <= 0 || sourceHeight <= 0) && _sourceCamera != null)
        {
            sourceWidth = _sourceCamera.pixelWidth;
            sourceHeight = _sourceCamera.pixelHeight;
        }

        if (sourceWidth > 0 && sourceHeight > 0)
        {
            var aspect = sourceWidth / (float)sourceHeight;
            width = Mathf.Max(16, Mathf.RoundToInt(height * aspect));
        }

        width = MakeEven(width);
        height = MakeEven(height);
    }

    bool RegisterCaptureAction()
    {
        if (_captureXrRenderPass)
        {
            if (_registeredRenderPipelineCapture)
            {
                return true;
            }

            RenderPipelineManager.endCameraRendering += CaptureXrRenderPassFrame;
            _registeredRenderPipelineCapture = true;
            _captureMethodName = "XRDisplayRenderPass";
            return true;
        }

        if (!_fallbackToCameraCaptureBridge || _sourceCamera == null)
        {
            return false;
        }

        if (_captureAction == null)
        {
            _captureAction = CaptureCameraFrame;
        }

        if (_registeredCamera == _sourceCamera)
        {
            return true;
        }

        UnregisterCaptureAction();
        CameraCaptureBridge.AddCaptureAction(_sourceCamera, _captureAction);
        _registeredCamera = _sourceCamera;
        _captureMethodName = "CameraCaptureBridge";
        return true;
    }

    void UnregisterCaptureAction()
    {
        if (_registeredRenderPipelineCapture)
        {
            RenderPipelineManager.endCameraRendering -= CaptureXrRenderPassFrame;
            _registeredRenderPipelineCapture = false;
        }

        if (_registeredCamera == null)
        {
            return;
        }

        CameraCaptureBridge.RemoveCaptureAction(_registeredCamera, _captureAction);
        _registeredCamera = null;
    }

    Transform ResolveViewTransform()
    {
        if (_viewTransform != null)
        {
            return _viewTransform;
        }

        if (_sourceCamera != null)
        {
            _viewTransform = _sourceCamera.transform;
            return _viewTransform;
        }

        if (!_autoFindReferences)
        {
            return null;
        }

        AutoFindReferences();
        return _viewTransform;
    }

    MeditationExperimentCsvLogger ResolveExperimentCsvLogger(bool createIfMissing)
    {
        if (_experimentCsvLogger != null)
        {
            return _experimentCsvLogger;
        }

        _experimentCsvLogger = FindObjectOfType<MeditationExperimentCsvLogger>();
        if (_experimentCsvLogger == null && createIfMissing)
        {
            _experimentCsvLogger = MeditationExperimentCsvLogger.Instance;
        }

        return _experimentCsvLogger;
    }

    void AutoFindReferences()
    {
        if (_viewTransform == null)
        {
            var centerEye = GameObject.Find(CenterEyeAnchorName);
            if (centerEye != null)
            {
                _viewTransform = centerEye.transform;
            }
        }

        if (_sourceCamera == null && _viewTransform != null)
        {
            _sourceCamera = _viewTransform.GetComponent<Camera>();
        }

        if (_sourceCamera == null)
        {
            _sourceCamera = Camera.main;
        }

        if (_viewTransform == null && _sourceCamera != null)
        {
            _viewTransform = _sourceCamera.transform;
        }
    }

    bool HasMissingReferences()
    {
        return _viewTransform == null || _sourceCamera == null;
    }

    void LogExperimentEvent(string eventType, string reason)
    {
        if (_experimentCsvLogger == null || !_experimentCsvLogger.sessionActive)
        {
            return;
        }

        _experimentCsvLogger.LogEvent(new MeditationExperimentCsvLogger.Row
        {
            eventType = eventType,
            eventRealtime = Time.realtimeSinceStartupAsDouble,
            notes = string.Format(
                CultureInfo.InvariantCulture,
                "reason={0}; captureMethod={1}; sourceCamera={2}; size={3}x{4}; sourceSize={5}x{6}; sourceSlice={7}; preserveSourceAspect={8}; cropToFill={9}; flipVertically={10}; fps={11:0.##}; imageFormat={12}; colorSpace={13}; xrEnabled={14}; xrEyeTexture={15}x{16}; frames={17}; dropped={18}; folder={19}",
                reason,
                _captureMethodName,
                _sourceCamera != null ? _sourceCamera.name : string.Empty,
                _activeCaptureWidth,
                _activeCaptureHeight,
                _activeSourceWidth,
                _activeSourceHeight,
                _activeSourceSlice,
                _preserveSourceAspect,
                _cropToFillOutput,
                _flipVertically,
                _captureFps,
                GetImageFormatName(),
                QualitySettings.activeColorSpace,
                XRSettings.enabled,
                XRSettings.eyeTextureWidth,
                XRSettings.eyeTextureHeight,
                _frameIndex,
                _droppedFrames,
                _framesFolderPath)
        });
    }

    void LogXrRenderPassWarning(string reason)
    {
        if (_xrRenderPassWarningLogged)
        {
            return;
        }

        _xrRenderPassWarningLogged = true;
        Debug.LogWarning("[ExperimentVideoRecorder] XR render pass capture skipped: " + reason + ".", this);
    }

    void WriteManifest(string state, string reason)
    {
        if (string.IsNullOrEmpty(_sessionFolderPath))
        {
            return;
        }

        var path = Path.Combine(_sessionFolderPath, ManifestFileName);
        var content = string.Join(Environment.NewLine,
            "{",
            "  \"sessionId\": " + JsonString(_experimentCsvLogger != null ? _experimentCsvLogger.sessionId : string.Empty) + ",",
            "  \"state\": " + JsonString(state) + ",",
            "  \"reason\": " + JsonString(reason) + ",",
            "  \"captureMethod\": " + JsonString(_captureMethodName) + ",",
            "  \"frameTap\": " + JsonString(_captureMethodName == "XRDisplayRenderPass" ? "EndCameraRendering" : "AfterPostProcess") + ",",
            "  \"sourceCamera\": " + JsonString(_sourceCamera != null ? _sourceCamera.name : string.Empty) + ",",
            "  \"xrViewIndex\": " + _xrViewIndex.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"xrSourceWidth\": " + _activeSourceWidth.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"xrSourceHeight\": " + _activeSourceHeight.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"xrSourceSlice\": " + _activeSourceSlice.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"requestedWidth\": " + _captureWidth.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"requestedHeight\": " + _captureHeight.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"width\": " + _activeCaptureWidth.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"height\": " + _activeCaptureHeight.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"matchXrEyeTextureAspect\": " + JsonBool(_matchXrEyeTextureAspect) + ",",
            "  \"preserveSourceAspect\": " + JsonBool(_preserveSourceAspect) + ",",
            "  \"cropToFillOutput\": " + JsonBool(_cropToFillOutput) + ",",
            "  \"flipVertically\": " + JsonBool(_flipVertically) + ",",
            "  \"xrEnabled\": " + JsonBool(XRSettings.enabled) + ",",
            "  \"xrEyeTextureWidth\": " + XRSettings.eyeTextureWidth.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"xrEyeTextureHeight\": " + XRSettings.eyeTextureHeight.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"fps\": " + _captureFps.ToString("0.######", CultureInfo.InvariantCulture) + ",",
            "  \"imageFormat\": " + JsonString(GetImageFormatName()) + ",",
            "  \"jpegQuality\": " + _jpegQuality.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"unityColorSpace\": " + JsonString(QualitySettings.activeColorSpace.ToString()) + ",",
            "  \"renderTextureReadWrite\": \"sRGB\",",
            "  \"frameCount\": " + _frameIndex.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"droppedFrames\": " + _droppedFrames.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"pendingReadbacks\": " + _pendingReadbacks.ToString(CultureInfo.InvariantCulture) + ",",
            "  \"framesFolder\": " + JsonString(_framesFolderPath) + ",",
            "  \"videoFramesCsv\": " + JsonString(Path.Combine(_sessionFolderPath, "video_frames.csv")) + ",",
            "  \"audioWav\": " + JsonString(Path.Combine(_sessionFolderPath, "audio.wav")) + ",",
            "  \"ffmpegExample\": " + JsonString(GetFfmpegExample()),
            "}");

        File.WriteAllText(path, content);
    }

    void ReleaseRenderTextureIfReady()
    {
        if (_releaseRenderTextureWhenPendingComplete && _pendingReadbacks <= 0)
        {
            _releaseRenderTextureWhenPendingComplete = false;
            ReleaseRenderTexture();
        }
    }

    void ReleaseRenderTexture()
    {
        if (_pendingReadbacks > 0)
        {
            _releaseRenderTextureWhenPendingComplete = true;
            return;
        }

        if (_renderTexture == null)
        {
            return;
        }

        _renderTexture.Release();
        UnityEngine.Object.Destroy(_renderTexture);
        _renderTexture = null;
    }

    void ReleaseEyeCopyTexture()
    {
        if (_eyeCopyTexture == null)
        {
            return;
        }

        _eyeCopyTexture.Release();
        UnityEngine.Object.Destroy(_eyeCopyTexture);
        _eyeCopyTexture = null;
    }

    string GetImageFormatName()
    {
        return _imageFormat == FrameImageFormat.Png ? "png" : "jpg";
    }

    string GetFrameFileExtension()
    {
        return GetImageFormatName();
    }

    string GetFfmpegExample()
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "ffmpeg -framerate {0:0.##} -start_number 1 -i video_frames/frame_%06d.{1} -i audio.wav -c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -c:a aac -shortest -movflags +faststart video_with_audio.mp4",
            _captureFps,
            GetFrameFileExtension());
    }

    static int MakeEven(int value)
    {
        value = Mathf.Max(16, value);
        return value % 2 == 0 ? value : value + 1;
    }

    static void FlipRowsInPlace(byte[] data, int width, int height, int bytesPerPixel)
    {
        var stride = width * bytesPerPixel;
        var temp = new byte[stride];
        for (var y = 0; y < height / 2; y++)
        {
            var top = y * stride;
            var bottom = (height - y - 1) * stride;
            Buffer.BlockCopy(data, top, temp, 0, stride);
            Buffer.BlockCopy(data, bottom, data, top, stride);
            Buffer.BlockCopy(temp, 0, data, bottom, stride);
        }
    }

    static bool IsProbablyBlackFrame(byte[] data, int width, int height, int bytesPerPixel)
    {
        if (data == null || data.Length == 0 || width <= 0 || height <= 0 || bytesPerPixel <= 0)
        {
            return false;
        }

        var stride = width * bytesPerPixel;
        var stepX = Mathf.Max(1, width / 32);
        var stepY = Mathf.Max(1, height / 32);
        var brightSamples = 0;
        var totalSamples = 0;

        for (var y = 0; y < height; y += stepY)
        {
            var row = y * stride;
            for (var x = 0; x < width; x += stepX)
            {
                var index = row + x * bytesPerPixel;
                if (index + 2 >= data.Length)
                {
                    continue;
                }

                totalSamples++;
                if (data[index] > 3 || data[index + 1] > 3 || data[index + 2] > 3)
                {
                    brightSamples++;
                    if (brightSamples > 2)
                    {
                        return false;
                    }
                }
            }
        }

        return totalSamples > 0;
    }

    static string JsonString(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    static string JsonBool(bool value)
    {
        return value ? "true" : "false";
    }

    sealed class FrameMetadata
    {
        public int frameIndex;
        public int unityFrame;
        public double sampleRealtime;
        public int width;
        public int height;
        public string relativePath;
        public string absolutePath;
        public string sourceCameraName;
        public string captureCameraName;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Vector3 cameraForward;
        public Vector3 cameraUp;
        public string notes;
    }
}
