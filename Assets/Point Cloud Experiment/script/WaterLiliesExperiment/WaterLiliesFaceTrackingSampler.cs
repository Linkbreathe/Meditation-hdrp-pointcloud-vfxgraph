using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

public struct WaterLiliesFaceTrackingSample
{
    public bool sampleAvailable;
    public bool permissionGranted;
    public bool faceTrackingSupported;
    public bool faceTrackingEnabled;
    public bool faceExpressionsFound;
    public bool faceExpressionsEnabled;
    public bool faceStateAvailable;
    public bool validExpressions;
    public bool eyeFollowingBlendshapesValid;
    public string faceDataSource;
    public double faceStateTimeSeconds;
    public bool faceRegionConfidencesAvailable;
    public float faceLowerRegionConfidence;
    public float faceUpperRegionConfidence;
    public bool visemesValid;
    public int faceExpressionCount;
    public bool eyesClosedWeightsAvailable;
    public float eyesClosedLeftRaw;
    public float eyesClosedRightRaw;
    public float eyesLookDownLeft;
    public float eyesLookDownRight;
    public float eyesLookLeftLeft;
    public float eyesLookLeftRight;
    public float eyesLookRightLeft;
    public float eyesLookRightRight;
    public float eyesLookUpLeft;
    public float eyesLookUpRight;
    public float upperLidRaiserLeft;
    public float upperLidRaiserRight;
    public float lidTightenerLeft;
    public float lidTightenerRight;
    public float browLowererLeft;
    public float browLowererRight;
    public float innerBrowRaiserLeft;
    public float innerBrowRaiserRight;
    public float outerBrowRaiserLeft;
    public float outerBrowRaiserRight;
    public float cheekRaiserLeft;
    public float cheekRaiserRight;
    public float eyesClosedLeft;
    public float eyesClosedRight;
    public float eyeClosureMean;
    public float eyeClosureDifference;
    public float eyeClosedSignalMin;
    public float eyeClosedSignalMax;
    public float eyeClosedSignalRange;
    public bool eyeClosedSignalResponsive;
    public bool leftEyeClosedCandidate;
    public bool rightEyeClosedCandidate;
    public bool bothEyesClosedCandidate;
    public bool leftEyeOpenCandidate;
    public bool rightEyeOpenCandidate;
    public bool bothEyesOpenCandidate;
    public bool eyeStateLabelAvailable;
    public string eyeStateLabel;
    public float eyeStateConfidence;
    public bool leftEyeClosed;
    public bool rightEyeClosed;
    public bool bothEyesClosed;
    public bool leftEyeOpen;
    public bool rightEyeOpen;
    public bool bothEyesOpen;
    public bool eyeTrackingSupported;
    public bool eyeTrackingEnabled;
    public bool eyeGazesStateAvailable;
    public double eyeGazesStateTimeSeconds;
    public bool leftEyeGazeValid;
    public bool rightEyeGazeValid;
    public float leftEyeGazeConfidence;
    public float rightEyeGazeConfidence;
    public string faceExpressionWeights;
    public string diagnostic;
}

public struct MetaQuestTrackingCheckResult
{
    public bool passed;
    public string summary;
    public string details;

    public MetaQuestTrackingCheckResult(bool passed, string summary, string details)
    {
        this.passed = passed;
        this.summary = summary ?? string.Empty;
        this.details = details ?? string.Empty;
    }

    public string ToDisplayText()
    {
        return (passed ? "PASS: " : "FAIL: ") + summary +
               (string.IsNullOrEmpty(details) ? string.Empty : "\n" + details);
    }
}

[DisallowMultipleComponent]
[AddComponentMenu("Water Lilies Experiment/Water Lilies Face Tracking Sampler")]
public sealed class WaterLiliesFaceTrackingSampler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] OVRFaceExpressions _faceExpressions;

    [Header("Startup")]
    [SerializeField] bool _autoCreateFaceExpressions = true;
    [SerializeField] bool _requestFaceTrackingPermission = true;
    [SerializeField] bool _enableFaceExpressionsWhenReady = true;

    [Header("Eye State Thresholds")]
    [SerializeField, Range(0f, 1f)] float _closedThreshold = 0.65f;
    [SerializeField, Range(0f, 1f)] float _openThreshold = 0.25f;
    [SerializeField, Range(0f, 1f)] float _minimumUpperFaceConfidence = 0.5f;
    [SerializeField, Range(0f, 1f)] float _responsiveSignalRangeThreshold = 0.2f;
    [SerializeField] bool _correctEyesClosedWithLookDown = true;
    [SerializeField] bool _requireResponsiveEyeClosedSignalForLabels = true;

    static readonly OVRFaceExpressions.FaceExpression[] AllFaceExpressions =
        (OVRFaceExpressions.FaceExpression[])Enum.GetValues(typeof(OVRFaceExpressions.FaceExpression));

    OVRPlugin.FaceState _faceState = new OVRPlugin.FaceState();
    OVRPlugin.EyeGazesState _eyeGazesState;
    float _eyeClosedSignalMin = float.PositiveInfinity;
    float _eyeClosedSignalMax = float.NegativeInfinity;
    int _usableEyeClosedSamples;
    string _lastDiagnostic = "Face tracking has not been sampled.";

    public string lastDiagnostic => _lastDiagnostic;
    public float closedThreshold => _closedThreshold;
    public float openThreshold => _openThreshold;

    void Awake()
    {
        ResolveFaceExpressions();
        RequestFaceTrackingPermissionIfNeeded();
        EnableFaceExpressionsWhenReady();
    }

    void OnEnable()
    {
        OVRPermissionsRequester.PermissionGranted -= OnPermissionGranted;
        OVRPermissionsRequester.PermissionGranted += OnPermissionGranted;
    }

    void OnDisable()
    {
        OVRPermissionsRequester.PermissionGranted -= OnPermissionGranted;
    }

    void OnValidate()
    {
        _closedThreshold = Mathf.Clamp01(_closedThreshold);
        _openThreshold = Mathf.Clamp01(_openThreshold);
        _minimumUpperFaceConfidence = Mathf.Clamp01(_minimumUpperFaceConfidence);
        _responsiveSignalRangeThreshold = Mathf.Clamp01(_responsiveSignalRangeThreshold);
    }

    public WaterLiliesFaceTrackingSample Capture()
    {
        ResolveFaceExpressions();
        RequestFaceTrackingPermissionIfNeeded();
        EnableFaceExpressionsWhenReady();

        var sample = CreateBaseSample();
        if (_faceExpressions == null)
        {
            sample.diagnostic = "No OVRFaceExpressions component was found or created.";
            _lastDiagnostic = sample.diagnostic;
            return sample;
        }

        CaptureFaceState(ref sample);
        CaptureFaceExpressions(ref sample);
        CaptureEyeGazes(ref sample);
        ClassifyEyeState(ref sample);

        sample.diagnostic = BuildDiagnostic(sample);
        _lastDiagnostic = sample.diagnostic;
        return sample;
    }

    public MetaQuestTrackingCheckResult CheckFaceTracking()
    {
        var sample = Capture();
        var details = sample.diagnostic;

        if (!sample.permissionGranted)
        {
            return new MetaQuestTrackingCheckResult(
                false,
                "Face tracking permission is not granted.",
                details + " Grant the permission in the headset, then run the check again.");
        }

        if (!sample.faceTrackingSupported)
        {
            return new MetaQuestTrackingCheckResult(false, "Face tracking is not supported by this runtime/device.", details);
        }

        if (!sample.faceTrackingEnabled)
        {
            return new MetaQuestTrackingCheckResult(false, "Face tracking is supported but not currently enabled.", details);
        }

        if (!sample.faceExpressionsFound || !sample.faceExpressionsEnabled)
        {
            return new MetaQuestTrackingCheckResult(false, "OVRFaceExpressions is not active.", details);
        }

        if (!sample.validExpressions || !sample.eyesClosedWeightsAvailable)
        {
            return new MetaQuestTrackingCheckResult(false, "Face expressions are not valid yet.", details);
        }

        if (!sample.eyeClosedSignalResponsive)
        {
            return new MetaQuestTrackingCheckResult(
                false,
                "Face tracking is running, but the eye-closed signal has not shown a usable open/closed range yet.",
                details + " Ask the participant to blink or close/open eyes once, then run this check again.");
        }

        return new MetaQuestTrackingCheckResult(true, "Face tracking is returning a responsive eye-closed signal.", details);
    }

    WaterLiliesFaceTrackingSample CreateBaseSample()
    {
        return new WaterLiliesFaceTrackingSample
        {
            sampleAvailable = true,
            permissionGranted = IsFaceTrackingPermissionGranted(),
            faceTrackingSupported = SafeFaceTrackingSupported(),
            faceTrackingEnabled = SafeFaceTrackingEnabled(),
            faceExpressionsFound = _faceExpressions != null,
            faceExpressionsEnabled = _faceExpressions != null && _faceExpressions.enabled,
            faceDataSource = string.Empty,
            faceStateTimeSeconds = double.NaN,
            faceLowerRegionConfidence = float.NaN,
            faceUpperRegionConfidence = float.NaN,
            eyesClosedLeftRaw = float.NaN,
            eyesClosedRightRaw = float.NaN,
            eyesLookDownLeft = float.NaN,
            eyesLookDownRight = float.NaN,
            eyesLookLeftLeft = float.NaN,
            eyesLookLeftRight = float.NaN,
            eyesLookRightLeft = float.NaN,
            eyesLookRightRight = float.NaN,
            eyesLookUpLeft = float.NaN,
            eyesLookUpRight = float.NaN,
            upperLidRaiserLeft = float.NaN,
            upperLidRaiserRight = float.NaN,
            lidTightenerLeft = float.NaN,
            lidTightenerRight = float.NaN,
            browLowererLeft = float.NaN,
            browLowererRight = float.NaN,
            innerBrowRaiserLeft = float.NaN,
            innerBrowRaiserRight = float.NaN,
            outerBrowRaiserLeft = float.NaN,
            outerBrowRaiserRight = float.NaN,
            cheekRaiserLeft = float.NaN,
            cheekRaiserRight = float.NaN,
            eyesClosedLeft = float.NaN,
            eyesClosedRight = float.NaN,
            eyeClosureMean = float.NaN,
            eyeClosureDifference = float.NaN,
            eyeClosedSignalMin = float.NaN,
            eyeClosedSignalMax = float.NaN,
            eyeClosedSignalRange = float.NaN,
            eyeStateLabel = "unavailable",
            eyeStateConfidence = float.NaN,
            eyeGazesStateTimeSeconds = double.NaN,
            leftEyeGazeConfidence = float.NaN,
            rightEyeGazeConfidence = float.NaN,
            faceExpressionWeights = string.Empty,
            diagnostic = string.Empty
        };
    }

    void CaptureFaceState(ref WaterLiliesFaceTrackingSample sample)
    {
        sample.faceStateAvailable = OVRPlugin.GetFaceState2(OVRPlugin.Step.Render, -1, ref _faceState);
        if (!sample.faceStateAvailable)
        {
            sample.validExpressions = _faceExpressions.ValidExpressions;
            sample.eyeFollowingBlendshapesValid = _faceExpressions.EyeFollowingBlendshapesValid;
            sample.visemesValid = _faceExpressions.AreVisemesValid;
            return;
        }

        sample.validExpressions = _faceState.Status.IsValid;
        sample.eyeFollowingBlendshapesValid = _faceState.Status.IsEyeFollowingBlendshapesValid;
        sample.faceStateTimeSeconds = _faceState.Time;
        sample.faceDataSource = _faceState.DataSource.ToString();
        sample.visemesValid = _faceExpressions.AreVisemesValid;
        sample.faceExpressionCount = _faceState.ExpressionWeights != null ? _faceState.ExpressionWeights.Length : 0;

        if (_faceState.ExpressionWeightConfidences != null &&
            _faceState.ExpressionWeightConfidences.Length > (int)OVRPlugin.FaceRegionConfidence.Upper)
        {
            sample.faceRegionConfidencesAvailable = true;
            sample.faceLowerRegionConfidence = _faceState.ExpressionWeightConfidences[(int)OVRPlugin.FaceRegionConfidence.Lower];
            sample.faceUpperRegionConfidence = _faceState.ExpressionWeightConfidences[(int)OVRPlugin.FaceRegionConfidence.Upper];
        }
        else
        {
            sample.faceRegionConfidencesAvailable =
                _faceExpressions.TryGetWeightConfidence(OVRFaceExpressions.FaceRegionConfidence.Lower, out sample.faceLowerRegionConfidence) &&
                _faceExpressions.TryGetWeightConfidence(OVRFaceExpressions.FaceRegionConfidence.Upper, out sample.faceUpperRegionConfidence);
        }
    }

    void CaptureFaceExpressions(ref WaterLiliesFaceTrackingSample sample)
    {
        var hasClosedLeft = TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesClosedL, out sample.eyesClosedLeftRaw);
        var hasClosedRight = TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesClosedR, out sample.eyesClosedRightRaw);
        sample.eyesClosedWeightsAvailable = hasClosedLeft && hasClosedRight;

        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookDownL, out sample.eyesLookDownLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookDownR, out sample.eyesLookDownRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookLeftL, out sample.eyesLookLeftLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookLeftR, out sample.eyesLookLeftRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookRightL, out sample.eyesLookRightLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookRightR, out sample.eyesLookRightRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookUpL, out sample.eyesLookUpLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.EyesLookUpR, out sample.eyesLookUpRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.UpperLidRaiserL, out sample.upperLidRaiserLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.UpperLidRaiserR, out sample.upperLidRaiserRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.LidTightenerL, out sample.lidTightenerLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.LidTightenerR, out sample.lidTightenerRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.BrowLowererL, out sample.browLowererLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.BrowLowererR, out sample.browLowererRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.InnerBrowRaiserL, out sample.innerBrowRaiserLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.InnerBrowRaiserR, out sample.innerBrowRaiserRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.OuterBrowRaiserL, out sample.outerBrowRaiserLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.OuterBrowRaiserR, out sample.outerBrowRaiserRight);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.CheekRaiserL, out sample.cheekRaiserLeft);
        TryGetExpressionWeight(sample, OVRFaceExpressions.FaceExpression.CheekRaiserR, out sample.cheekRaiserRight);

        sample.eyesClosedLeft = sample.eyesClosedLeftRaw;
        sample.eyesClosedRight = sample.eyesClosedRightRaw;

        if (_correctEyesClosedWithLookDown && sample.eyeFollowingBlendshapesValid)
        {
            var lookDownCorrection = Mathf.Min(SafeWeight(sample.eyesLookDownLeft), SafeWeight(sample.eyesLookDownRight));
            sample.eyesClosedLeft = Mathf.Clamp01(SafeWeight(sample.eyesClosedLeft) + lookDownCorrection);
            sample.eyesClosedRight = Mathf.Clamp01(SafeWeight(sample.eyesClosedRight) + lookDownCorrection);
        }

        if (sample.validExpressions && sample.eyesClosedWeightsAvailable)
        {
            sample.eyeClosureMean = (sample.eyesClosedLeft + sample.eyesClosedRight) * 0.5f;
            sample.eyeClosureDifference = Mathf.Abs(sample.eyesClosedLeft - sample.eyesClosedRight);
            UpdateObservedEyeClosedRange(sample.eyeClosureMean);
            sample.eyeClosedSignalMin = _eyeClosedSignalMin;
            sample.eyeClosedSignalMax = _eyeClosedSignalMax;
            sample.eyeClosedSignalRange = _eyeClosedSignalMax - _eyeClosedSignalMin;
            sample.eyeClosedSignalResponsive = _usableEyeClosedSamples >= 2 &&
                                               sample.eyeClosedSignalRange >= _responsiveSignalRangeThreshold;
        }

        sample.faceExpressionWeights = BuildFaceExpressionWeightsString(sample);
    }

    void CaptureEyeGazes(ref WaterLiliesFaceTrackingSample sample)
    {
        sample.eyeTrackingSupported = OVRPlugin.eyeTrackingSupported;
        sample.eyeTrackingEnabled = OVRPlugin.eyeTrackingEnabled;
        sample.eyeGazesStateAvailable = OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref _eyeGazesState);
        if (!sample.eyeGazesStateAvailable ||
            _eyeGazesState.EyeGazes == null ||
            _eyeGazesState.EyeGazes.Length < 2)
        {
            return;
        }

        sample.eyeGazesStateTimeSeconds = _eyeGazesState.Time;
        var left = _eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Left];
        var right = _eyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Right];
        sample.leftEyeGazeValid = left.IsValid;
        sample.rightEyeGazeValid = right.IsValid;
        sample.leftEyeGazeConfidence = left.Confidence;
        sample.rightEyeGazeConfidence = right.Confidence;
    }

    void ClassifyEyeState(ref WaterLiliesFaceTrackingSample sample)
    {
        var hasUsableEyeState = sample.validExpressions && sample.eyesClosedWeightsAvailable;
        var upperFaceConfidenceOk = !sample.faceRegionConfidencesAvailable ||
                                    sample.faceUpperRegionConfidence >= _minimumUpperFaceConfidence;

        sample.leftEyeClosedCandidate = hasUsableEyeState && sample.eyesClosedLeft >= _closedThreshold;
        sample.rightEyeClosedCandidate = hasUsableEyeState && sample.eyesClosedRight >= _closedThreshold;
        sample.bothEyesClosedCandidate = sample.leftEyeClosedCandidate && sample.rightEyeClosedCandidate;
        sample.leftEyeOpenCandidate = hasUsableEyeState && sample.eyesClosedLeft <= _openThreshold;
        sample.rightEyeOpenCandidate = hasUsableEyeState && sample.eyesClosedRight <= _openThreshold;
        sample.bothEyesOpenCandidate = sample.leftEyeOpenCandidate && sample.rightEyeOpenCandidate;

        var labelsAllowed = hasUsableEyeState &&
                            upperFaceConfidenceOk &&
                            (!_requireResponsiveEyeClosedSignalForLabels || sample.eyeClosedSignalResponsive);

        sample.leftEyeClosed = labelsAllowed && sample.leftEyeClosedCandidate;
        sample.rightEyeClosed = labelsAllowed && sample.rightEyeClosedCandidate;
        sample.bothEyesClosed = labelsAllowed && sample.bothEyesClosedCandidate;
        sample.leftEyeOpen = labelsAllowed && sample.leftEyeOpenCandidate;
        sample.rightEyeOpen = labelsAllowed && sample.rightEyeOpenCandidate;
        sample.bothEyesOpen = labelsAllowed && sample.bothEyesOpenCandidate;

        if (!hasUsableEyeState)
        {
            sample.eyeStateLabel = "unavailable";
            sample.eyeStateLabelAvailable = false;
            return;
        }

        if (!upperFaceConfidenceOk)
        {
            sample.eyeStateLabel = "low_upper_face_confidence";
            sample.eyeStateLabelAvailable = false;
            return;
        }

        if (_requireResponsiveEyeClosedSignalForLabels && !sample.eyeClosedSignalResponsive)
        {
            sample.eyeStateLabel = sample.bothEyesClosedCandidate
                ? "closed_candidate_unvalidated"
                : sample.bothEyesOpenCandidate
                    ? "open_candidate_unvalidated"
                    : "mixed_candidate_unvalidated";
            sample.eyeStateLabelAvailable = false;
            return;
        }

        if (sample.bothEyesClosed)
        {
            sample.eyeStateLabel = "closed";
            sample.eyeStateLabelAvailable = true;
            sample.eyeStateConfidence = Mathf.Min(RegionConfidenceOrOne(sample), sample.eyeClosureMean);
        }
        else if (sample.bothEyesOpen)
        {
            sample.eyeStateLabel = "open";
            sample.eyeStateLabelAvailable = true;
            sample.eyeStateConfidence = Mathf.Min(RegionConfidenceOrOne(sample), 1f - sample.eyeClosureMean);
        }
        else
        {
            sample.eyeStateLabel = "mixed_or_uncertain";
            sample.eyeStateLabelAvailable = false;
            sample.eyeStateConfidence = 0f;
        }
    }

    bool TryGetExpressionWeight(
        WaterLiliesFaceTrackingSample sample,
        OVRFaceExpressions.FaceExpression expression,
        out float weight)
    {
        var index = (int)expression;
        if (sample.faceStateAvailable &&
            sample.validExpressions &&
            _faceState.ExpressionWeights != null &&
            index >= 0 &&
            index < _faceState.ExpressionWeights.Length)
        {
            weight = _faceState.ExpressionWeights[index];
            return true;
        }

        if (_faceExpressions != null)
        {
            return _faceExpressions.TryGetFaceExpressionWeight(expression, out weight);
        }

        weight = float.NaN;
        return false;
    }

    void UpdateObservedEyeClosedRange(float eyeClosureMean)
    {
        if (!IsFinite(eyeClosureMean))
        {
            return;
        }

        _usableEyeClosedSamples++;
        _eyeClosedSignalMin = Mathf.Min(_eyeClosedSignalMin, eyeClosureMean);
        _eyeClosedSignalMax = Mathf.Max(_eyeClosedSignalMax, eyeClosureMean);
    }

    string BuildFaceExpressionWeightsString(WaterLiliesFaceTrackingSample sample)
    {
        if (!sample.faceStateAvailable || !sample.validExpressions || _faceState.ExpressionWeights == null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(1024);
        for (var i = 0; i < AllFaceExpressions.Length; i++)
        {
            var expression = AllFaceExpressions[i];
            var index = (int)expression;
            if (index < 0 ||
                expression == OVRFaceExpressions.FaceExpression.Invalid ||
                expression == OVRFaceExpressions.FaceExpression.Max ||
                index >= _faceState.ExpressionWeights.Length)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('|');
            }

            builder
                .Append(expression)
                .Append('=')
                .Append(_faceState.ExpressionWeights[index].ToString("0.######", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    void ResolveFaceExpressions()
    {
        if (_faceExpressions != null)
        {
            return;
        }

        _faceExpressions = GetComponent<OVRFaceExpressions>();
        if (_faceExpressions != null)
        {
            return;
        }

        _faceExpressions = FindObjectOfType<OVRFaceExpressions>(true);
        if (_faceExpressions == null && _autoCreateFaceExpressions)
        {
            _faceExpressions = gameObject.AddComponent<OVRFaceExpressions>();
        }
    }

    void RequestFaceTrackingPermissionIfNeeded()
    {
        if (!_requestFaceTrackingPermission || !Application.isPlaying || IsFaceTrackingPermissionGranted())
        {
            return;
        }

        OVRPermissionsRequester.Request(new List<OVRPermissionsRequester.Permission>
        {
            OVRPermissionsRequester.Permission.FaceTracking
        });
    }

    void EnableFaceExpressionsWhenReady()
    {
        if (!_enableFaceExpressionsWhenReady ||
            _faceExpressions == null ||
            _faceExpressions.enabled ||
            !Application.isPlaying ||
            !IsFaceTrackingPermissionGranted() ||
            !SafeFaceTrackingSupported())
        {
            return;
        }

        _faceExpressions.enabled = true;
    }

    void OnPermissionGranted(string permissionId)
    {
        if (permissionId == OVRPermissionsRequester.GetPermissionId(OVRPermissionsRequester.Permission.FaceTracking))
        {
            EnableFaceExpressionsWhenReady();
        }
    }

    static bool IsFaceTrackingPermissionGranted()
    {
        return OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.FaceTracking);
    }

    static bool SafeFaceTrackingSupported()
    {
        return OVRPlugin.faceTrackingSupported || OVRPlugin.faceTracking2Supported;
    }

    static bool SafeFaceTrackingEnabled()
    {
        return OVRPlugin.faceTrackingEnabled || OVRPlugin.faceTracking2Enabled;
    }

    static float SafeWeight(float value)
    {
        return IsFinite(value) ? value : 0f;
    }

    static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static float RegionConfidenceOrOne(WaterLiliesFaceTrackingSample sample)
    {
        return sample.faceRegionConfidencesAvailable ? sample.faceUpperRegionConfidence : 1f;
    }

    string BuildDiagnostic(WaterLiliesFaceTrackingSample sample)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "permission={0} supported={1} enabled={2} component={3}/{4} faceState={5} valid={6} source={7} " +
            "upperConf={8:0.###} lowerConf={9:0.###} eyeFollowValid={10} closedRaw=({11:0.###},{12:0.###}) " +
            "lookDown=({13:0.###},{14:0.###}) closed=({15:0.###},{16:0.###}) closureMean={17:0.###} " +
            "range={18:0.###} responsive={19} label={20} labelAvailable={21} eyeGazeValid=({22},{23}) eyeGazeConf=({24:0.###},{25:0.###})",
            sample.permissionGranted,
            sample.faceTrackingSupported,
            sample.faceTrackingEnabled,
            sample.faceExpressionsFound,
            sample.faceExpressionsEnabled,
            sample.faceStateAvailable,
            sample.validExpressions,
            sample.faceDataSource,
            sample.faceUpperRegionConfidence,
            sample.faceLowerRegionConfidence,
            sample.eyeFollowingBlendshapesValid,
            sample.eyesClosedLeftRaw,
            sample.eyesClosedRightRaw,
            sample.eyesLookDownLeft,
            sample.eyesLookDownRight,
            sample.eyesClosedLeft,
            sample.eyesClosedRight,
            sample.eyeClosureMean,
            sample.eyeClosedSignalRange,
            sample.eyeClosedSignalResponsive,
            sample.eyeStateLabel,
            sample.eyeStateLabelAvailable,
            sample.leftEyeGazeValid,
            sample.rightEyeGazeValid,
            sample.leftEyeGazeConfidence,
            sample.rightEyeGazeConfidence);
    }
}

public static class MetaQuestTrackingDiagnostics
{
    static OVRPlugin.EyeGazesState EyeGazesState;

    public static MetaQuestTrackingCheckResult CheckEyeTracking()
    {
        var permissionGranted = OVRPermissionsRequester.IsPermissionGranted(OVRPermissionsRequester.Permission.EyeTracking);
        var permissionNote = string.Empty;
        if (!permissionGranted && Application.isPlaying)
        {
            OVRPermissionsRequester.Request(new List<OVRPermissionsRequester.Permission>
            {
                OVRPermissionsRequester.Permission.EyeTracking
            });
            permissionNote = " Eye tracking permission was requested; grant it in the headset and run the check again.";
        }

        var initialized = OVRPlugin.initialized;
        var supported = OVRPlugin.eyeTrackingSupported;
        var enabled = OVRPlugin.eyeTrackingEnabled;
        var startAttempted = false;
        var startResult = false;

        if (Application.isPlaying && permissionGranted && initialized && supported && !enabled)
        {
            startAttempted = true;
            startResult = OVRPlugin.StartEyeTracking();
            enabled = OVRPlugin.eyeTrackingEnabled;
        }

        var gotState = OVRPlugin.GetEyeGazesState(OVRPlugin.Step.Render, -1, ref EyeGazesState);
        var leftValid = false;
        var rightValid = false;
        var leftConfidence = float.NaN;
        var rightConfidence = float.NaN;

        if (gotState && EyeGazesState.EyeGazes != null && EyeGazesState.EyeGazes.Length >= 2)
        {
            var left = EyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Left];
            var right = EyeGazesState.EyeGazes[(int)OVRPlugin.Eye.Right];
            leftValid = left.IsValid;
            rightValid = right.IsValid;
            leftConfidence = left.Confidence;
            rightConfidence = right.Confidence;
        }

        var passed = permissionGranted && supported && enabled && gotState && (leftValid || rightValid);
        var details = string.Format(
            CultureInfo.InvariantCulture,
            "permission={0} initialized={1} supported={2} enabled={3} startAttempted={4} startResult={5} " +
            "gotState={6} leftValid={7} leftConf={8:0.###} rightValid={9} rightConf={10:0.###}.{11}",
            permissionGranted,
            initialized,
            supported,
            enabled,
            startAttempted,
            startResult,
            gotState,
            leftValid,
            leftConfidence,
            rightValid,
            rightConfidence,
            permissionNote);

        return new MetaQuestTrackingCheckResult(
            passed,
            passed ? "Eye tracking is returning at least one valid gaze sample." : "Eye tracking did not return a valid gaze sample.",
            details);
    }
}
