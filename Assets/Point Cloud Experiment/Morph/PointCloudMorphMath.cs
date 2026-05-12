using System;

namespace PointCloudMorphing
{
    public readonly struct PointCloudMorphSegment
    {
        public PointCloudMorphSegment(
            int segmentIndex,
            int sourceIndex,
            int targetIndex,
            float localTime,
            float progress,
            float easedProgress,
            bool isComplete)
        {
            SegmentIndex = segmentIndex;
            SourceIndex = sourceIndex;
            TargetIndex = targetIndex;
            LocalTime = localTime;
            Progress = progress;
            EasedProgress = easedProgress;
            IsComplete = isComplete;
        }

        public int SegmentIndex { get; }
        public int SourceIndex { get; }
        public int TargetIndex { get; }
        public float LocalTime { get; }
        public float Progress { get; }
        public float EasedProgress { get; }
        public bool IsComplete { get; }
    }

    public static class PointCloudMorphMath
    {
        const float MinimumSegmentDuration = 0.0001f;

        public static PointCloudMorphSegment Evaluate(
            float elapsedTime,
            int targetCount,
            float segmentDuration,
            bool loop)
        {
            if (targetCount <= 0)
            {
                return new PointCloudMorphSegment(0, 0, 0, 0f, 1f, 1f, true);
            }

            if (targetCount == 1)
            {
                return new PointCloudMorphSegment(0, 0, 0, 0f, 1f, 1f, true);
            }

            var safeDuration = Math.Max(MinimumSegmentDuration, segmentDuration);
            var segmentCount = loop ? targetCount : targetCount - 1;
            var totalDuration = safeDuration * segmentCount;
            var time = loop ? Repeat(elapsedTime, totalDuration) : Clamp(elapsedTime, 0f, totalDuration);

            if (!loop && time >= totalDuration)
            {
                var lastIndex = targetCount - 1;
                return new PointCloudMorphSegment(
                    segmentCount - 1,
                    lastIndex,
                    lastIndex,
                    safeDuration,
                    1f,
                    1f,
                    true);
            }

            var segmentIndex = Math.Min(segmentCount - 1, (int)(time / safeDuration));
            var localTime = Clamp(time - segmentIndex * safeDuration, 0f, safeDuration);
            var progress = Clamp01(localTime / safeDuration);
            var sourceIndex = segmentIndex % targetCount;
            var targetIndex = loop ? (sourceIndex + 1) % targetCount : Math.Min(sourceIndex + 1, targetCount - 1);

            return new PointCloudMorphSegment(
                segmentIndex,
                sourceIndex,
                targetIndex,
                localTime,
                progress,
                SmoothStep01(progress),
                false);
        }

        public static int MaxPointCount(int sourcePointCount, int targetPointCount)
        {
            return Math.Max(0, Math.Max(sourcePointCount, targetPointCount));
        }

        public static float SmoothStep01(float value)
        {
            var t = Clamp01(value);
            return t * t * (3f - 2f * t);
        }

        public static float Clamp01(float value)
        {
            return Clamp(value, 0f, 1f);
        }

        static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        static float Repeat(float value, float length)
        {
            if (length <= 0f)
            {
                return 0f;
            }

            return value - (float)Math.Floor(value / length) * length;
        }
    }
}
