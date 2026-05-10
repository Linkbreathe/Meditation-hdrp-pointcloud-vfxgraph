using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;

public static class GazeVfxDisturbanceSetup
{
    const string PearlLadyName = "pearl_lady";
    const string RightEyeGazeName = "[BuildingBlock] Eye Gaze Right";
    const string LeftEyeGazeName = "[BuildingBlock] Eye Gaze Left";
    const string CenterEyeName = "CenterEyeAnchor";
    const string ExperimentVfxPath = "Assets/Point Cloud/pearl_lady_gaze_experiment.vfx";

    [MenuItem("Tools/Point Cloud/Gaze Disturbance/Setup Pearl Lady Driver")]
    public static void SetupPearlLadyDriver()
    {
        SetupPearlLadyDriver(false);
    }

    [MenuItem("Tools/Point Cloud/Gaze Disturbance/Duplicate Pearl Lady VFX Copy And Setup Driver")]
    public static void DuplicateVfxCopyAndSetupPearlLadyDriver()
    {
        SetupPearlLadyDriver(true);
    }

    static void SetupPearlLadyDriver(bool duplicateAndAssignVfxCopy)
    {
        var pearlLady = GameObject.Find(PearlLadyName);
        if (pearlLady == null)
        {
            EditorUtility.DisplayDialog(
                "Gaze Disturbance Setup",
                "Could not find a GameObject named pearl_lady in the open scene.",
                "OK");
            return;
        }

        var visualEffect = pearlLady.GetComponent<VisualEffect>();
        if (visualEffect == null)
        {
            EditorUtility.DisplayDialog(
                "Gaze Disturbance Setup",
                "pearl_lady does not have a VisualEffect component.",
                "OK");
            return;
        }

        if (duplicateAndAssignVfxCopy)
        {
            var copy = CreateOrLoadVfxExperimentCopy(visualEffect);
            if (copy == null)
            {
                return;
            }

            Undo.RecordObject(visualEffect, "Assign Pearl Lady Gaze VFX Copy");
            visualEffect.visualEffectAsset = copy;
            EditorUtility.SetDirty(visualEffect);
        }

        var driver = pearlLady.GetComponent<GazeVfxDisturbanceDriver>();
        if (driver == null)
        {
            driver = Undo.AddComponent<GazeVfxDisturbanceDriver>(pearlLady);
        }
        else
        {
            Undo.RecordObject(driver, "Setup Pearl Lady Gaze Driver");
        }

        ConfigureDriver(driver, visualEffect, pearlLady.transform);
        Selection.activeObject = pearlLady;
        EditorSceneManager.MarkSceneDirty(pearlLady.scene);

        Debug.Log(
            "[GazeVfxDisturbanceSetup] Configured pearl_lady with GazeVfxDisturbanceDriver. " +
            "Average Eyes mode uses CenterEyeAnchor as the ray origin and the left/right eye gaze directions.",
            pearlLady);
    }

    static VisualEffectAsset CreateOrLoadVfxExperimentCopy(VisualEffect visualEffect)
    {
        var sourcePath = AssetDatabase.GetAssetPath(visualEffect.visualEffectAsset);
        if (string.IsNullOrEmpty(sourcePath))
        {
            EditorUtility.DisplayDialog(
                "Gaze Disturbance Setup",
                "Could not resolve the current pearl_lady VisualEffectAsset path.",
                "OK");
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(ExperimentVfxPath) == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, ExperimentVfxPath))
            {
                EditorUtility.DisplayDialog(
                    "Gaze Disturbance Setup",
                    "Could not copy the current pearl_lady VFX asset.",
                    "OK");
                return null;
            }

            AssetDatabase.ImportAsset(ExperimentVfxPath);
        }

        return AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(ExperimentVfxPath);
    }

    static void ConfigureDriver(
        GazeVfxDisturbanceDriver driver,
        VisualEffect visualEffect,
        Transform targetTransform)
    {
        var serializedObject = new SerializedObject(driver);

        SetObject(serializedObject, "_visualEffect", visualEffect);
        SetObject(serializedObject, "_targetTransform", targetTransform);
        SetEnum(serializedObject, "_gazeSourceMode", (int)GazeVfxDisturbanceDriver.GazeSourceMode.AverageEyes);
        SetObject(serializedObject, "_gazeTransform", ResolveTransform(RightEyeGazeName));
        SetObject(serializedObject, "_leftGazeTransform", ResolveTransform(LeftEyeGazeName));
        SetObject(serializedObject, "_rightGazeTransform", ResolveTransform(RightEyeGazeName));
        SetObject(serializedObject, "_rayOriginTransform", ResolveTransform(CenterEyeName));
        SetBool(serializedObject, "_autoFindGazeTransform", true);
        SetString(serializedObject, "_preferredGazeObjectName", RightEyeGazeName);
        SetString(serializedObject, "_fallbackGazeObjectName", LeftEyeGazeName);
        SetString(serializedObject, "_leftGazeObjectName", LeftEyeGazeName);
        SetString(serializedObject, "_rightGazeObjectName", RightEyeGazeName);
        SetString(serializedObject, "_rayOriginObjectName", CenterEyeName);
        SetString(serializedObject, "_cameraFallbackName", CenterEyeName);
        SetVector3(serializedObject, "_regionCenterLocal", Vector3.zero);
        SetFloat(serializedObject, "_regionRadiusLocal", 5f);
        SetFloat(serializedObject, "_softEdgeLocal", 0.5f);
        SetFloat(serializedObject, "_brushRadiusLocal", 1f);
        SetEnum(serializedObject, "_regionStrengthMode", (int)GazeVfxDisturbanceDriver.RegionStrengthMode.ConstantInsideRegion);
        SetFloat(serializedObject, "_attackSpeed", 22f);
        SetFloat(serializedObject, "_releaseSpeed", 10f);
        SetFloat(serializedObject, "_hitPositionSmoothingSpeed", 30f);
        SetBool(serializedObject, "_driveExistingGlobalControls", true);

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(driver);
    }

    static Transform ResolveTransform(string objectName)
    {
        var gameObject = GameObject.Find(objectName);
        return gameObject != null ? gameObject.transform : null;
    }

    static void SetEnum(SerializedObject serializedObject, string propertyName, int value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.enumValueIndex = value;
        }
    }

    static void SetObject(SerializedObject serializedObject, string propertyName, Object value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.objectReferenceValue = value;
        }
    }

    static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.stringValue = value;
        }
    }

    static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.floatValue = value;
        }
    }

    static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.boolValue = value;
        }
    }

    static void SetVector3(SerializedObject serializedObject, string propertyName, Vector3 value)
    {
        var property = serializedObject.FindProperty(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }
}
