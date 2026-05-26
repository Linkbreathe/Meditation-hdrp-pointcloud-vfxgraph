using System;
using System.Collections.Generic;
using UnityEngine;

public enum RDKMotionDirection
{
    None = 0,
    Left = -1,
    Right = 1
}

public enum RDKResponseDirection
{
    None = 0,
    Left = -1,
    Right = 1,
    Random = 2
}

[Serializable]
public sealed class RDKTrialCondition
{
    public string conditionName = "right_50";
    public RDKMotionDirection motionDirection = RDKMotionDirection.Right;
    [Range(0f, 1f)] public float motionCoherence = 0.5f;
    [Min(1)] public int repetitions = 1;
}

public struct RDKTrialPlan
{
    public int blockIndex;
    public int conditionIndex;
    public int trialIndexInBlock;
    public bool isPractice;
    public string conditionName;
    public RDKMotionDirection motionDirection;
    public float motionCoherence;

    public string conditionType => motionDirection == RDKMotionDirection.None ? "none" : motionDirection.ToString().ToLowerInvariant();
}

[CreateAssetMenu(menuName = "RDK Experiment/Config", fileName = "RDKExperimentConfig")]
public sealed class RDKExperimentConfig : ScriptableObject
{
    [Header("Trial Sequence")]
    [SerializeField, Min(1)] int _blockCount = 1;
    [SerializeField] bool _shuffleTrialsWithinBlock = true;
    [SerializeField] RDKTrialCondition[] _conditions =
    {
        new RDKTrialCondition { conditionName = "left_50", motionDirection = RDKMotionDirection.Left, motionCoherence = 0.5f, repetitions = 8 },
        new RDKTrialCondition { conditionName = "right_50", motionDirection = RDKMotionDirection.Right, motionCoherence = 0.5f, repetitions = 8 },
        new RDKTrialCondition { conditionName = "random_0", motionDirection = RDKMotionDirection.None, motionCoherence = 0f, repetitions = 8 }
    };
    [SerializeField] RDKTrialCondition[] _practiceConditions =
    {
        new RDKTrialCondition { conditionName = "practice_left_75", motionDirection = RDKMotionDirection.Left, motionCoherence = 0.75f, repetitions = 1 },
        new RDKTrialCondition { conditionName = "practice_right_75", motionDirection = RDKMotionDirection.Right, motionCoherence = 0.75f, repetitions = 1 },
        new RDKTrialCondition { conditionName = "practice_random_0", motionDirection = RDKMotionDirection.None, motionCoherence = 0f, repetitions = 1 }
    };

    [Header("Stimulus")]
    [SerializeField, Min(1)] int _pointCount = 54;
    [SerializeField, Min(0.01f)] float _pointSizeMeters = 0.12f;
    [SerializeField, Min(0.01f)] float _dotSpeedMetersPerSecond = 0.45f;
    [SerializeField, Min(0.2f)] float _panelDistanceMeters = 3f;
    [SerializeField] Vector2 _observationWindowMeters = new Vector2(2.8f, 1.75f);
    [SerializeField] bool _useCurvedSurface = true;
    [SerializeField, Min(0f)] float _randomDirectionRefreshSeconds;
    [SerializeField] bool _randomizeDirectionOnWrap = true;

    [Header("Timing")]
    [SerializeField, Min(0f)] float _fixationSeconds = 0.75f;
    [SerializeField, Min(0.05f)] float _stimulusSeconds = 1.5f;
    [SerializeField, Min(0.05f)] float _responseWindowSeconds = 2f;
    [SerializeField, Min(0f)] float _interTrialIntervalSeconds = 1f;

    [Header("Environment")]
    [SerializeField] Color _backgroundColor = Color.black;
    [SerializeField] bool _recenterPanelBeforeEachTrial = true;
    [SerializeField] bool _useHeadYawOnly = true;

    public int blockCount => Mathf.Max(1, _blockCount);
    public bool shuffleTrialsWithinBlock => _shuffleTrialsWithinBlock;
    public int pointCount => Mathf.Max(1, _pointCount);
    public float pointSizeMeters => Mathf.Max(0.01f, _pointSizeMeters);
    public float dotSpeedMetersPerSecond => Mathf.Max(0.01f, _dotSpeedMetersPerSecond);
    public float panelDistanceMeters => Mathf.Max(0.2f, _panelDistanceMeters);
    public Vector2 observationWindowMeters => new Vector2(Mathf.Max(0.1f, _observationWindowMeters.x), Mathf.Max(0.1f, _observationWindowMeters.y));
    public bool useCurvedSurface => _useCurvedSurface;
    public float randomDirectionRefreshSeconds => Mathf.Max(0f, _randomDirectionRefreshSeconds);
    public bool randomizeDirectionOnWrap => _randomizeDirectionOnWrap;
    public float fixationSeconds => Mathf.Max(0f, _fixationSeconds);
    public float stimulusSeconds => Mathf.Max(0.05f, _stimulusSeconds);
    public float responseWindowSeconds => Mathf.Max(0.05f, _responseWindowSeconds);
    public float interTrialIntervalSeconds => Mathf.Max(0f, _interTrialIntervalSeconds);
    public Color backgroundColor => _backgroundColor;
    public bool recenterPanelBeforeEachTrial => _recenterPanelBeforeEachTrial;
    public bool useHeadYawOnly => _useHeadYawOnly;

    void OnValidate()
    {
        _blockCount = Mathf.Max(1, _blockCount);
        _pointCount = Mathf.Max(1, _pointCount);
        _pointSizeMeters = Mathf.Max(0.01f, _pointSizeMeters);
        _dotSpeedMetersPerSecond = Mathf.Max(0.01f, _dotSpeedMetersPerSecond);
        _panelDistanceMeters = Mathf.Max(0.2f, _panelDistanceMeters);
        _observationWindowMeters.x = Mathf.Max(0.1f, _observationWindowMeters.x);
        _observationWindowMeters.y = Mathf.Max(0.1f, _observationWindowMeters.y);
        _fixationSeconds = Mathf.Max(0f, _fixationSeconds);
        _stimulusSeconds = Mathf.Max(0.05f, _stimulusSeconds);
        _responseWindowSeconds = Mathf.Max(0.05f, _responseWindowSeconds);
        _interTrialIntervalSeconds = Mathf.Max(0f, _interTrialIntervalSeconds);
        ClampConditions(_conditions);
        ClampConditions(_practiceConditions);
    }

    public void BuildTrialSequence(List<RDKTrialPlan> output, bool practice)
    {
        output.Clear();
        RDKTrialCondition[] source = practice && _practiceConditions != null && _practiceConditions.Length > 0
            ? _practiceConditions
            : _conditions;

        if (source == null || source.Length == 0)
        {
            source = _conditions;
        }

        int blocks = practice ? 1 : blockCount;
        for (int block = 0; block < blocks; block++)
        {
            int blockStart = output.Count;
            for (int conditionIndex = 0; conditionIndex < source.Length; conditionIndex++)
            {
                RDKTrialCondition condition = source[conditionIndex];
                int repetitions = Mathf.Max(1, condition.repetitions);

                for (int repeat = 0; repeat < repetitions; repeat++)
                {
                    output.Add(new RDKTrialPlan
                    {
                        blockIndex = block + 1,
                        conditionIndex = conditionIndex,
                        trialIndexInBlock = output.Count - blockStart + 1,
                        isPractice = practice,
                        conditionName = string.IsNullOrEmpty(condition.conditionName) ? condition.motionDirection.ToString() : condition.conditionName,
                        motionDirection = condition.motionDirection,
                        motionCoherence = Mathf.Clamp01(condition.motionCoherence)
                    });
                }
            }

            if (!practice && _shuffleTrialsWithinBlock)
            {
                ShuffleRange(output, blockStart, output.Count);
                for (int i = blockStart; i < output.Count; i++)
                {
                    RDKTrialPlan trial = output[i];
                    trial.trialIndexInBlock = i - blockStart + 1;
                    output[i] = trial;
                }
            }
        }
    }

    static void ClampConditions(RDKTrialCondition[] conditions)
    {
        if (conditions == null)
        {
            return;
        }

        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i] == null)
            {
                conditions[i] = new RDKTrialCondition();
            }

            conditions[i].motionCoherence = Mathf.Clamp01(conditions[i].motionCoherence);
            conditions[i].repetitions = Mathf.Max(1, conditions[i].repetitions);
        }
    }

    static void ShuffleRange(List<RDKTrialPlan> trials, int startInclusive, int endExclusive)
    {
        for (int i = endExclusive - 1; i > startInclusive; i--)
        {
            int swapIndex = UnityEngine.Random.Range(startInclusive, i + 1);
            RDKTrialPlan temp = trials[i];
            trials[i] = trials[swapIndex];
            trials[swapIndex] = temp;
        }
    }
}
