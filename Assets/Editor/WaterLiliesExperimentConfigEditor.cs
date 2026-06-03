using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaterLiliesExperimentConfig))]
public sealed class WaterLiliesExperimentConfigEditor : Editor
{
    SerializedProperty _script;
    SerializedProperty _participantId;
    SerializedProperty _mode;
    SerializedProperty _targetPaintingObjectName;
    SerializedProperty _isolateTargetPainting;
    SerializedProperty _disableLegacyPaintingControllers;
    SerializedProperty _formalDurations;
    SerializedProperty _pilotDurations;
    SerializedProperty _intensityValues;
    SerializedProperty _frequencyValues;
    SerializedProperty _baselineIntensity;
    SerializedProperty _baselineFrequency;
    SerializedProperty _conditions;
    SerializedProperty _conditionOrder;
    SerializedProperty _autoStart;
    SerializedProperty _requireManualQuestionnaireContinue;
    SerializedProperty _requireHeadsetWornBeforeQuestionnaireContinue;
    SerializedProperty _requireHeadsetCycleBeforeQuestionnaireContinue;
    SerializedProperty _autoDetectHeadsetPresence;
    SerializedProperty _allowPilotSkipShortcut;
    SerializedProperty _writeCsv;
    SerializedProperty _writeJsonLines;
    SerializedProperty _logFolderName;
    SerializedProperty _useExternalLogRoot;
    SerializedProperty _externalLogRootPath;
    SerializedProperty _sampleIntervalSeconds;

    void OnEnable()
    {
        _script = serializedObject.FindProperty("m_Script");
        _participantId = serializedObject.FindProperty("_participantId");
        _mode = serializedObject.FindProperty("_mode");
        _targetPaintingObjectName = serializedObject.FindProperty("_targetPaintingObjectName");
        _isolateTargetPainting = serializedObject.FindProperty("_isolateTargetPainting");
        _disableLegacyPaintingControllers = serializedObject.FindProperty("_disableLegacyPaintingControllers");
        _formalDurations = serializedObject.FindProperty("_formalDurations");
        _pilotDurations = serializedObject.FindProperty("_pilotDurations");
        _intensityValues = serializedObject.FindProperty("_intensityValues");
        _frequencyValues = serializedObject.FindProperty("_frequencyValues");
        _baselineIntensity = serializedObject.FindProperty("_baselineIntensity");
        _baselineFrequency = serializedObject.FindProperty("_baselineFrequency");
        _conditions = serializedObject.FindProperty("_conditions");
        _conditionOrder = serializedObject.FindProperty("_conditionOrder");
        _autoStart = serializedObject.FindProperty("_autoStart");
        _requireManualQuestionnaireContinue = serializedObject.FindProperty("_requireManualQuestionnaireContinue");
        _requireHeadsetWornBeforeQuestionnaireContinue = serializedObject.FindProperty("_requireHeadsetWornBeforeQuestionnaireContinue");
        _requireHeadsetCycleBeforeQuestionnaireContinue = serializedObject.FindProperty("_requireHeadsetCycleBeforeQuestionnaireContinue");
        _autoDetectHeadsetPresence = serializedObject.FindProperty("_autoDetectHeadsetPresence");
        _allowPilotSkipShortcut = serializedObject.FindProperty("_allowPilotSkipShortcut");
        _writeCsv = serializedObject.FindProperty("_writeCsv");
        _writeJsonLines = serializedObject.FindProperty("_writeJsonLines");
        _logFolderName = serializedObject.FindProperty("_logFolderName");
        _useExternalLogRoot = serializedObject.FindProperty("_useExternalLogRoot");
        _externalLogRootPath = serializedObject.FindProperty("_externalLogRootPath");
        _sampleIntervalSeconds = serializedObject.FindProperty("_sampleIntervalSeconds");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(_script);
        }

        DrawSection("Session");
        EditorGUILayout.PropertyField(_participantId);
        EditorGUILayout.PropertyField(_mode);

        DrawSection("Fixed Painting");
        EditorGUILayout.PropertyField(_targetPaintingObjectName);
        EditorGUILayout.PropertyField(_isolateTargetPainting);
        EditorGUILayout.PropertyField(_disableLegacyPaintingControllers);

        DrawSection("Durations");
        var activeIsFormal = _mode.enumValueIndex == (int)WaterLiliesExperimentMode.Formal;
        var activeProfile = activeIsFormal ? _formalDurations : _pilotDurations;
        var inactiveProfile = activeIsFormal ? _pilotDurations : _formalDurations;
        var activeLabel = activeIsFormal ? "Active Durations (Formal)" : "Active Durations (Pilot)";
        var inactiveLabel = activeIsFormal ? "Other Mode Durations (Pilot)" : "Other Mode Durations (Formal)";
        DrawDurationProfile(activeProfile, activeLabel);
        EditorGUILayout.Space(4f);
        DrawDurationProfile(inactiveProfile, inactiveLabel);

        DrawSection("Parameter Tables");
        EditorGUILayout.PropertyField(_intensityValues, true);
        EditorGUILayout.PropertyField(_frequencyValues, true);
        EditorGUILayout.PropertyField(_baselineIntensity);
        EditorGUILayout.PropertyField(_baselineFrequency);

        DrawSection("Conditions");
        EditorGUILayout.PropertyField(_conditions, true);
        EditorGUILayout.PropertyField(_conditionOrder, true);
        if (GUILayout.Button("Reset Condition Order to C1-C9"))
        {
            ResetConditionOrderToDefault(_conditionOrder);
        }

        DrawSection("Control");
        EditorGUILayout.PropertyField(_autoStart);
        EditorGUILayout.PropertyField(_requireManualQuestionnaireContinue);
        EditorGUILayout.PropertyField(_requireHeadsetWornBeforeQuestionnaireContinue);
        EditorGUILayout.PropertyField(_requireHeadsetCycleBeforeQuestionnaireContinue);
        EditorGUILayout.PropertyField(_autoDetectHeadsetPresence);
        EditorGUILayout.PropertyField(_allowPilotSkipShortcut);

        DrawSection("Logging");
        EditorGUILayout.PropertyField(_writeCsv);
        EditorGUILayout.PropertyField(_writeJsonLines);
        EditorGUILayout.PropertyField(_logFolderName);
        EditorGUILayout.PropertyField(_useExternalLogRoot);
        EditorGUILayout.PropertyField(_externalLogRootPath);
        EditorGUILayout.PropertyField(_sampleIntervalSeconds);

        serializedObject.ApplyModifiedProperties();
    }

    static void DrawSection(string label)
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
    }

    static void DrawDurationProfile(SerializedProperty profile, string label)
    {
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        using (new EditorGUI.IndentLevelScope())
        {
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_baselineSeconds"), new GUIContent("Baseline Seconds"));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_adaptationSeconds"), new GUIContent("Adaptation Seconds"));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_conditionSeconds"), new GUIContent("Condition Seconds"));
            EditorGUILayout.PropertyField(
                profile.FindPropertyRelative("_preConditionBaselineSeconds"),
                new GUIContent("Pre-condition Baseline Seconds", "Neutral baseline shown after questionnaire/recenter and before each formal condition. Set to 0 to disable."));
            EditorGUILayout.PropertyField(
                profile.FindPropertyRelative("_preConditionBaselineAnalysisSeconds"),
                new GUIContent("Baseline Analysis Window Seconds", "Recommended analysis window at the end of each pre-condition baseline phase."));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_questionnaireMinimumSeconds"), new GUIContent("Questionnaire Minimum Seconds"));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_recenterSeconds"), new GUIContent("Recenter Seconds"));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_restSeconds"), new GUIContent("Rest Seconds"));
            EditorGUILayout.PropertyField(profile.FindPropertyRelative("_restEveryConditionCount"), new GUIContent("Rest Every Condition Count"));
        }
    }

    static void ResetConditionOrderToDefault(SerializedProperty conditionOrder)
    {
        var defaultOrder = WaterLiliesExperimentConfig.CreateDefaultConditionOrder();
        conditionOrder.arraySize = defaultOrder.Length;
        for (var i = 0; i < defaultOrder.Length; i++)
        {
            conditionOrder.GetArrayElementAtIndex(i).stringValue = defaultOrder[i];
        }
    }
}
