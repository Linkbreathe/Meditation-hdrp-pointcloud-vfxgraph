using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>One-click scene wiring for the isolated Adaptive Control components.</summary>
public static class AdaptiveControlSetup
{
    [MenuItem("Adaptive Control/Configure")]
    public static void Configure()
    {
        var vfx = FindSceneComponent<WaterLiliesVfxController>();
        var tracking = FindSceneComponent<WaterLiliesTrackingSampler>();
        var manager = FindSceneComponent<WaterLiliesExperimentManager>();
        if (vfx == null || tracking == null)
        {
            EditorUtility.DisplayDialog(
                "Adaptive Control",
                "Open Meditation.unity with WaterLiliesVfxController and WaterLiliesTrackingSampler available before configuring Adaptive Control.",
                "OK");
            return;
        }

        var root = vfx.gameObject;
        var controller = root.GetComponent<AdaptiveControlController>();
        if (controller == null)
        {
            controller = Undo.AddComponent<AdaptiveControlController>(root);
        }

        var publisher = root.GetComponent<AdaptiveControlSensorPublisher>();
        if (publisher == null)
        {
            publisher = Undo.AddComponent<AdaptiveControlSensorPublisher>(root);
        }

        var dashboard = root.GetComponent<AdaptiveControlRuntimeDashboard>();
        if (dashboard == null)
        {
            dashboard = Undo.AddComponent<AdaptiveControlRuntimeDashboard>(root);
        }

        Assign(controller, "_vfxController", vfx);
        Assign(controller, "_formalExperimentManager", manager);
        Assign(controller, "_trackingSampler", tracking);
        AssignString(controller, "_profileRelativePath", AdaptiveControlProfile.DefaultRelativePath);
        Assign(publisher, "_controller", controller);
        Assign(publisher, "_trackingSampler", tracking);
        Assign(dashboard, "_controller", controller);
        AssignBool(dashboard, "_visible", false);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(publisher);
        EditorUtility.SetDirty(dashboard);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[AdaptiveControlSetup] Adaptive Control components are configured on " + root.name + ".", root);
    }

    [MenuItem("Adaptive Control/Configure", true)]
    static bool ValidateConfigure()
    {
        return !EditorApplication.isPlaying;
    }

    static void Assign(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning("[AdaptiveControlSetup] Missing serialized property " + propertyName + " on " + target.GetType().Name + ".", target);
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignString(Object target, string propertyName, string value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning("[AdaptiveControlSetup] Missing serialized property " + propertyName + " on " + target.GetType().Name + ".", target);
            return;
        }

        property.stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AssignBool(Object target, string propertyName, bool value)
    {
        var serialized = new SerializedObject(target);
        var property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogWarning("[AdaptiveControlSetup] Missing serialized property " + propertyName + " on " + target.GetType().Name + ".", target);
            return;
        }

        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static T FindSceneComponent<T>() where T : Component
    {
        var candidates = Resources.FindObjectsOfTypeAll<T>();
        for (var i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && candidates[i].gameObject.scene.IsValid())
            {
                return candidates[i];
            }
        }

        return null;
    }
}
