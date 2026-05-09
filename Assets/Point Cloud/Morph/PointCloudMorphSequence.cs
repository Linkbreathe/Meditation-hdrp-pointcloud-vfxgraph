using System;
using UnityEngine;
using UnityEngine.VFX;
using Pcx;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
#endif

namespace PointCloudMorphing
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(VisualEffect))]
    [AddComponentMenu("Point Cloud/VFX Point Cloud Morph Sequence")]
    public sealed class PointCloudMorphSequence : MonoBehaviour
    {
        [Serializable]
        public sealed class PointCloudTargetAlignment
        {
            public Vector3 localPosition = Vector3.zero;
            public Vector3 localRotation = Vector3.zero;
            public Vector3 localScale = Vector3.one;

            public Matrix4x4 ToMatrix()
            {
                var scale = localScale == Vector3.zero ? Vector3.one : localScale;
                return Matrix4x4.TRS(localPosition, Quaternion.Euler(localRotation), scale);
            }
        }

        const string PositionMapProperty = "MorphPositionMap";
        const string ColorMapProperty = "MorphColorMap";
        const string PointCloudGuidPattern = "(?:guid:\\s*\"?|\"guid\":\")([0-9a-fA-F]{32})";
        const int ThreadGroupSize = 8;

        [Header("VFX Binding")]
        [SerializeField] VisualEffect _visualEffect;
        [SerializeField] VisualEffectAsset _morphVisualEffectAsset;
        [SerializeField] VisualEffectAsset[] _vfxTargets;
        [SerializeField] ComputeShader _morphCompute;
        [SerializeField, HideInInspector] BakedPointCloud[] _resolvedPointClouds;

        [Header("Target Alignments")]
        [SerializeField] PointCloudTargetAlignment[] _targetAlignments;

        [Header("Playback")]
        [SerializeField] bool _autoPlay = true;
        [SerializeField] bool _loop;
        [SerializeField, Min(0.01f)] float _segmentDuration = 8f;
        [SerializeField] float _timeScale = 1f;
        [SerializeField, Min(0f)] float _previewTime;

        [Header("Transition Motion")]
        [SerializeField, Range(0f, 3f)] float _danceRadius = 0.18f;
        [SerializeField, Range(0f, 3f)] float _verticalWave = 0.22f;
        [SerializeField, Range(0f, 3f)] float _swirlTurns = 0.85f;
        [SerializeField, Range(0.01f, 8f)] float _danceSpeed = 1.35f;
        [SerializeField, Range(0f, 1f)] float _particleStagger = 0.38f;
        [SerializeField, Range(0f, 1f)] float _colorShimmer = 0.12f;

        [Header("After Arrival")]
        [SerializeField, Range(0f, 0.5f)] float _breathAmount = 0.045f;
        [SerializeField, Range(0f, 0.5f)] float _floatAmount = 0.035f;
        [SerializeField, Range(0.01f, 5f)] float _breathSpeed = 0.65f;

        RenderTexture _generatedPositionMap;
        RenderTexture _generatedColorMap;
        int _kernel = -1;
        int _allocatedMapSize;
        int _allocatedPointCount;
        float _elapsedTime;

        public VisualEffectAsset[] vfxTargets
        {
            get => _vfxTargets;
            set
            {
                _vfxTargets = value;
                ResolvePointClouds();
            }
        }

        public PointCloudTargetAlignment[] targetAlignments => _targetAlignments;

        public float elapsedTime
        {
            get => _elapsedTime;
            set => SetElapsedTime(value);
        }

        public void Restart()
        {
            SetElapsedTime(0f);
        }

        public void SetElapsedTime(float elapsedTime)
        {
            _elapsedTime = Mathf.Max(0f, elapsedTime);
            _previewTime = _elapsedTime;
            UpdateMorph();
        }

        public void ResolvePointClouds()
        {
            EnsureTargetAlignments();
#if UNITY_EDITOR
            var targetCount = _vfxTargets == null ? 0 : _vfxTargets.Length;
            _resolvedPointClouds = new BakedPointCloud[targetCount];

            for (var i = 0; i < targetCount; i++)
            {
                _resolvedPointClouds[i] = ResolvePointCloud(_vfxTargets[i]);
            }
#endif
        }

        void Reset()
        {
            _visualEffect = GetComponent<VisualEffect>();
            AssignDefaultAssets();
            ResolvePointClouds();
        }

        void OnEnable()
        {
            AssignDefaultAssets();
            EnsureVisualEffect();
            if (!HasResolvedPointClouds())
            {
                ResolvePointClouds();
            }

            _elapsedTime = Application.isPlaying && _autoPlay ? 0f : _previewTime;
            UpdateMorph();
        }

        void OnDisable()
        {
            ReleaseGeneratedMaps();
        }

        void OnDestroy()
        {
            ReleaseGeneratedMaps();
        }

        void OnValidate()
        {
            _segmentDuration = Mathf.Max(0.01f, _segmentDuration);
            _particleStagger = Mathf.Clamp01(_particleStagger);
            AssignDefaultAssets();
            EnsureTargetAlignments();
            ResolvePointClouds();
        }

        void Update()
        {
            if (Application.isPlaying)
            {
                if (_autoPlay)
                {
                    _elapsedTime += Time.deltaTime * _timeScale;
                    _previewTime = _elapsedTime;
                }
                else
                {
                    _elapsedTime = _previewTime;
                }
            }
            else
            {
                _elapsedTime = _previewTime;
            }

            UpdateMorph();
        }

        void UpdateMorph()
        {
            EnsureVisualEffect();

            if (_visualEffect == null || _morphCompute == null || !EnsureKernel())
            {
                return;
            }

            var targetCount = CountValidPointClouds();
            if (targetCount == 0)
            {
                return;
            }

            var segment = targetCount == 1
                ? new PointCloudMorphSegment(0, 0, 0, 0f, 1f, 1f, true)
                : PointCloudMorphMath.Evaluate(_elapsedTime, targetCount, _segmentDuration, _loop);

            var source = GetValidPointCloud(segment.SourceIndex, out var sourceTargetIndex);
            var target = GetValidPointCloud(segment.TargetIndex, out var targetTargetIndex);
            if (!IsUsable(source) || !IsUsable(target))
            {
                return;
            }

            var outputPointCount = PointCloudMorphMath.MaxPointCount(source.pointCount, target.pointCount);
            var outputMapSize = Mathf.Max(
                CeilSquareSize(outputPointCount),
                Mathf.Max(source.positionMap.width, target.positionMap.width));

            EnsureGeneratedMaps(outputMapSize, outputPointCount);
            DispatchMorph(
                source,
                target,
                segment,
                outputMapSize,
                outputPointCount,
                GetTargetAlignmentMatrix(sourceTargetIndex),
                GetTargetAlignmentMatrix(targetTargetIndex));
            ApplyGeneratedMapsToVfx();
        }

        void EnsureVisualEffect()
        {
            if (_visualEffect == null)
            {
                _visualEffect = GetComponent<VisualEffect>();
            }

            if (_visualEffect == null)
            {
                return;
            }

            if (_morphVisualEffectAsset != null && _visualEffect.visualEffectAsset != _morphVisualEffectAsset)
            {
                _visualEffect.visualEffectAsset = _morphVisualEffectAsset;
                _visualEffect.Reinit();
                _visualEffect.Play();
            }
        }

        bool EnsureKernel()
        {
            if (_kernel >= 0)
            {
                return true;
            }

            try
            {
                _kernel = _morphCompute.FindKernel("CSMain");
                return _kernel >= 0;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Point cloud morph compute shader is missing CSMain: {exception.Message}", this);
                return false;
            }
        }

        void EnsureGeneratedMaps(int mapSize, int pointCount)
        {
            if (_generatedPositionMap != null &&
                _generatedColorMap != null &&
                _allocatedMapSize == mapSize &&
                _allocatedPointCount == pointCount)
            {
                return;
            }

            ReleaseGeneratedMaps();
            _allocatedMapSize = mapSize;
            _allocatedPointCount = pointCount;
            _generatedPositionMap = CreateGeneratedMap(mapSize, RenderTextureFormat.ARGBHalf, "PointCloud Morph Position Map");
            _generatedColorMap = CreateGeneratedMap(mapSize, RenderTextureFormat.ARGBHalf, "PointCloud Morph Color Map");
        }

        void DispatchMorph(
            BakedPointCloud source,
            BakedPointCloud target,
            PointCloudMorphSegment segment,
            int outputMapSize,
            int outputPointCount,
            Matrix4x4 sourceTransform,
            Matrix4x4 targetTransform)
        {
            _morphCompute.SetTexture(_kernel, "_SourcePositionMap", source.positionMap);
            _morphCompute.SetTexture(_kernel, "_SourceColorMap", source.colorMap);
            _morphCompute.SetTexture(_kernel, "_TargetPositionMap", target.positionMap);
            _morphCompute.SetTexture(_kernel, "_TargetColorMap", target.colorMap);
            _morphCompute.SetTexture(_kernel, "_OutputPositionMap", _generatedPositionMap);
            _morphCompute.SetTexture(_kernel, "_OutputColorMap", _generatedColorMap);

            _morphCompute.SetInt("_SourceSize", source.positionMap.width);
            _morphCompute.SetInt("_TargetSize", target.positionMap.width);
            _morphCompute.SetInt("_OutputSize", outputMapSize);
            _morphCompute.SetInt("_SourcePointCount", source.pointCount);
            _morphCompute.SetInt("_TargetPointCount", target.pointCount);
            _morphCompute.SetInt("_OutputPointCount", outputPointCount);

            _morphCompute.SetMatrix("_SourceTransform", sourceTransform);
            _morphCompute.SetMatrix("_TargetTransform", targetTransform);
            _morphCompute.SetFloat("_Progress", Mathf.Clamp01(segment.Progress));
            _morphCompute.SetFloat("_EasedProgress", Mathf.Clamp01(segment.EasedProgress));
            _morphCompute.SetFloat("_Time", Application.isPlaying ? Time.time : _elapsedTime);
            _morphCompute.SetFloat("_DanceRadius", _danceRadius);
            _morphCompute.SetFloat("_VerticalWave", _verticalWave);
            _morphCompute.SetFloat("_SwirlTurns", _swirlTurns);
            _morphCompute.SetFloat("_DanceSpeed", _danceSpeed);
            _morphCompute.SetFloat("_ParticleStagger", _particleStagger);
            _morphCompute.SetFloat("_ColorShimmer", _colorShimmer);
            _morphCompute.SetFloat("_BreathAmount", _breathAmount);
            _morphCompute.SetFloat("_FloatAmount", _floatAmount);
            _morphCompute.SetFloat("_BreathSpeed", _breathSpeed);
            _morphCompute.SetFloat("_IdleStrength", segment.IsComplete ? 1f : 0f);

            var groups = Mathf.CeilToInt(outputMapSize / (float)ThreadGroupSize);
            _morphCompute.Dispatch(_kernel, groups, groups, 1);
        }

        void ApplyGeneratedMapsToVfx()
        {
            if (_visualEffect == null || _visualEffect.visualEffectAsset == null)
            {
                return;
            }

            if (_visualEffect.HasTexture(PositionMapProperty))
            {
                _visualEffect.SetTexture(PositionMapProperty, _generatedPositionMap);
            }

            if (_visualEffect.HasTexture(ColorMapProperty))
            {
                _visualEffect.SetTexture(ColorMapProperty, _generatedColorMap);
            }
        }

        int CountValidPointClouds()
        {
            if (_resolvedPointClouds == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < _resolvedPointClouds.Length; i++)
            {
                if (IsUsable(_resolvedPointClouds[i]))
                {
                    count++;
                }
            }

            return count;
        }

        BakedPointCloud GetValidPointCloud(int validIndex, out int targetIndex)
        {
            targetIndex = -1;
            if (_resolvedPointClouds == null)
            {
                return null;
            }

            var count = 0;
            for (var i = 0; i < _resolvedPointClouds.Length; i++)
            {
                var pointCloud = _resolvedPointClouds[i];
                if (!IsUsable(pointCloud))
                {
                    continue;
                }

                if (count == validIndex)
                {
                    targetIndex = i;
                    return pointCloud;
                }

                count++;
            }

            return null;
        }

        Matrix4x4 GetTargetAlignmentMatrix(int targetIndex)
        {
            EnsureTargetAlignments();
            if (targetIndex < 0 || _targetAlignments == null || targetIndex >= _targetAlignments.Length)
            {
                return Matrix4x4.identity;
            }

            return _targetAlignments[targetIndex]?.ToMatrix() ?? Matrix4x4.identity;
        }

        void EnsureTargetAlignments()
        {
            var targetCount = _vfxTargets == null ? 0 : _vfxTargets.Length;
            if (_targetAlignments == null || _targetAlignments.Length != targetCount)
            {
                var resized = new PointCloudTargetAlignment[targetCount];
                var copyCount = _targetAlignments == null ? 0 : Mathf.Min(_targetAlignments.Length, targetCount);
                for (var i = 0; i < copyCount; i++)
                {
                    resized[i] = _targetAlignments[i];
                }

                _targetAlignments = resized;
            }

            for (var i = 0; i < _targetAlignments.Length; i++)
            {
                if (_targetAlignments[i] == null)
                {
                    _targetAlignments[i] = new PointCloudTargetAlignment();
                }
            }
        }

        bool HasResolvedPointClouds()
        {
            if (_vfxTargets == null || _vfxTargets.Length == 0)
            {
                return false;
            }

            if (_resolvedPointClouds == null || _resolvedPointClouds.Length != _vfxTargets.Length)
            {
                return false;
            }

            for (var i = 0; i < _vfxTargets.Length; i++)
            {
                if (_vfxTargets[i] != null && _resolvedPointClouds[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        void AssignDefaultAssets()
        {
#if UNITY_EDITOR
            if (_visualEffect == null)
            {
                _visualEffect = GetComponent<VisualEffect>();
            }

            if (_morphVisualEffectAsset == null)
            {
                _morphVisualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(
                    "Assets/Point Cloud/Flower2_To_Flower3_Morph.vfx");
            }

            if (_morphCompute == null)
            {
                _morphCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(
                    "Assets/Point Cloud/Morph/Shaders/PointCloudMorphMap.compute");
                _kernel = -1;
            }

            if (_vfxTargets == null || _vfxTargets.Length == 0)
            {
                var flower2 = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Point Cloud/Flower2.vfx");
                var flower3 = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Point Cloud/Flower 3.vfx");
                if (flower3 == null)
                {
                    flower3 = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Point Cloud/Flower3.vfx");
                }

                if (flower2 != null && flower3 != null)
                {
                    _vfxTargets = new[] { flower2, flower3 };
                }
            }
#endif
            EnsureTargetAlignments();
        }

        static RenderTexture CreateGeneratedMap(int mapSize, RenderTextureFormat format, string name)
        {
            var map = new RenderTexture(mapSize, mapSize, 0, format, RenderTextureReadWrite.Linear)
            {
                name = name,
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };

            map.Create();
            return map;
        }

        static int CeilSquareSize(int pointCount)
        {
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, pointCount))));
        }

        static bool IsUsable(BakedPointCloud pointCloud)
        {
            return pointCloud != null &&
                   pointCloud.pointCount > 0 &&
                   pointCloud.positionMap != null &&
                   pointCloud.colorMap != null;
        }

        void ReleaseGeneratedMaps()
        {
            ReleaseGeneratedMap(ref _generatedPositionMap);
            ReleaseGeneratedMap(ref _generatedColorMap);
            _allocatedMapSize = 0;
            _allocatedPointCount = 0;
        }

        static void ReleaseGeneratedMap(ref RenderTexture map)
        {
            if (map == null)
            {
                return;
            }

            map.Release();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(map);
            }
            else
#endif
            {
                Destroy(map);
            }

            map = null;
        }

#if UNITY_EDITOR
        static BakedPointCloud ResolvePointCloud(VisualEffectAsset vfxAsset)
        {
            if (vfxAsset == null)
            {
                return null;
            }

            var vfxPath = AssetDatabase.GetAssetPath(vfxAsset);
            if (string.IsNullOrEmpty(vfxPath) || !File.Exists(vfxPath))
            {
                return null;
            }

            var text = File.ReadAllText(vfxPath);
            var seenGuids = new HashSet<string>();
            foreach (Match match in Regex.Matches(text, PointCloudGuidPattern))
            {
                var guid = match.Groups[1].Value;
                if (!seenGuids.Add(guid))
                {
                    continue;
                }

                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath))
                {
                    continue;
                }

                var pointCloud = AssetDatabase.LoadAssetAtPath<BakedPointCloud>(assetPath);
                if (pointCloud != null)
                {
                    return pointCloud;
                }

                var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                for (var i = 0; i < assets.Length; i++)
                {
                    pointCloud = assets[i] as BakedPointCloud;
                    if (pointCloud != null)
                    {
                        return pointCloud;
                    }
                }
            }

            Debug.LogWarning($"Could not resolve a Pcx.BakedPointCloud from {vfxPath}.", vfxAsset);
            return null;
        }
#endif
    }
}
