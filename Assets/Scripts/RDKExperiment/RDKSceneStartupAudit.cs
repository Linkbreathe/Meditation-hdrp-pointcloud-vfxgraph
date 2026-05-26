using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.XR.Interaction.Toolkit;
#endif

[DefaultExecutionOrder(-9900)]
[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK Scene Startup Audit")]
public sealed class RDKSceneStartupAudit : MonoBehaviour
{
    [SerializeField] bool _runAuditOnStart = true;
    [SerializeField] bool _warnOnFailures = true;

    void Start()
    {
        if (_runAuditOnStart)
        {
            RunAudit();
        }
    }

    [ContextMenu("Run RDK Scene Startup Audit")]
    public void RunAudit()
    {
        int failures = 0;
        int warnings = 0;

        Check(ObjectExists("RDK Experiment"), "RDK Experiment GameObject is active in the scene.", ref failures);
        Check(ObjectExists("XR Origin (XR Rig)"), "XR Origin (XR Rig) is active in the scene.", ref failures);
        Check(ObjectExists("XR Interaction Manager"), "XR Interaction Manager is active in the scene.", ref failures);

        RDKExperimentManager experimentManager = FindObjectOfType<RDKExperimentManager>(true);
        RDKStimulusManager stimulusManager = FindObjectOfType<RDKStimulusManager>(true);
        RDKVRInputManager inputManager = FindObjectOfType<RDKVRInputManager>(true);
        RDKDataLogger dataLogger = FindObjectOfType<RDKDataLogger>(true);
        Check(experimentManager != null && experimentManager.gameObject.activeInHierarchy && experimentManager.enabled, "RDKExperimentManager is present, active, and enabled.", ref failures);
        Check(stimulusManager != null && stimulusManager.gameObject.activeInHierarchy && stimulusManager.enabled, "RDKStimulusManager is present, active, and enabled.", ref failures);
        Check(inputManager != null && inputManager.gameObject.activeInHierarchy && inputManager.enabled, "RDKVRInputManager is present, active, and enabled.", ref failures);
        Check(dataLogger != null && dataLogger.gameObject.activeInHierarchy && dataLogger.enabled, "RDKDataLogger is present, active, and enabled.", ref failures);

        Camera[] cameras = FindObjectsOfType<Camera>(true);
        int activeCameraCount = 0;
        int activeMainCameraCount = 0;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || !camera.enabled || !camera.gameObject.activeInHierarchy)
            {
                continue;
            }

            activeCameraCount++;
            if (camera.CompareTag("MainCamera"))
            {
                activeMainCameraCount++;
            }
        }

        Check(activeCameraCount > 0, $"At least one active Camera exists. Active cameras={activeCameraCount}.", ref failures);
        Check(activeMainCameraCount == 1, $"Exactly one active MainCamera exists. Active MainCamera count={activeMainCameraCount}.", ref failures);

        AudioListener[] listeners = FindObjectsOfType<AudioListener>(true);
        int activeListenerCount = 0;
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i] != null && listeners[i].enabled && listeners[i].gameObject.activeInHierarchy)
            {
                activeListenerCount++;
            }
        }

        Check(activeListenerCount == 1, $"Exactly one active AudioListener exists. Active AudioListener count={activeListenerCount}.", ref warnings, true);
        Check(EventSystem.current != null, $"EventSystem exists. Current={(EventSystem.current != null ? EventSystem.current.name : "none")}.", ref failures);

        if (stimulusManager != null)
        {
            Check(stimulusManager.stimulusRoot != null, "Stimulus root is assigned.", ref failures);
            Check(stimulusManager.viewerTransform != null, "Stimulus viewer transform is assigned.", ref failures);
            Check(stimulusManager.cachedPointCount > 0, $"Stimulus manager cached at least one point. Cached={stimulusManager.cachedPointCount}.", ref failures);
        }

        FurnitureSphereDistanceArranger arranger = FindObjectOfType<FurnitureSphereDistanceArranger>(true);
        if (arranger != null)
        {
            bool maintainDuringPlay = ReadPrivateBool(arranger, "_maintainDistanceDuringPlay");
            Check(!maintainDuringPlay, "FurnitureSphereDistanceArranger is not maintaining distance every frame during Play.", ref failures);
        }
        else
        {
            Check(false, "FurnitureSphereDistanceArranger exists for CPP/furniture layout.", ref warnings, true);
        }

#if ENABLE_INPUT_SYSTEM
        ActionBasedController[] controllers = FindObjectsOfType<ActionBasedController>(true);
        XRBaseControllerInteractor[] interactors = FindObjectsOfType<XRBaseControllerInteractor>(true);
        Check(controllers.Length > 0, $"ActionBasedController objects exist. Count={controllers.Length}.", ref failures);
        Check(interactors.Length > 0, $"XRBaseControllerInteractor objects exist. Count={interactors.Length}.", ref failures);

        bool rightControllerHasKeyboard = false;
        for (int i = 0; i < controllers.Length; i++)
        {
            ActionBasedController controller = controllers[i];
            if (controller == null || !controller.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (NameContainsInParents(controller.transform, "right") &&
                HasBinding(controller.activateAction.action, "<Keyboard>/space") &&
                HasBinding(controller.selectAction.action, "<Keyboard>/e"))
            {
                rightControllerHasKeyboard = true;
                break;
            }
        }

        Check(rightControllerHasKeyboard, "Right ActionBasedController has keyboard bindings for Trigger(Space) and Grip(E).", ref failures);
#else
        Check(false, "Input System scripting define is enabled.", ref failures);
#endif

        string summary = $"[RDKSceneStartupAudit] Completed with failures={failures}, warnings={warnings}.";
        if (failures > 0 && _warnOnFailures)
        {
            Debug.LogWarning(summary, this);
        }
        else
        {
            Debug.Log(summary, this);
        }
    }

    static bool ObjectExists(string name)
    {
        GameObject value = GameObject.Find(name);
        return value != null && value.activeInHierarchy;
    }

    void Check(bool condition, string message, ref int counter, bool warning = false)
    {
        string prefix = condition ? "PASS" : warning ? "WARN" : "FAIL";
        string line = $"[RDKSceneStartupAudit] {prefix}: {message}";

        if (condition)
        {
            Debug.Log(line, this);
            return;
        }

        counter++;
        if (_warnOnFailures)
        {
            Debug.LogWarning(line, this);
        }
        else
        {
            Debug.Log(line, this);
        }
    }

    static bool ReadPrivateBool(Object target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return field != null && field.FieldType == typeof(bool) && (bool)field.GetValue(target);
    }

#if ENABLE_INPUT_SYSTEM
    static bool NameContainsInParents(Transform transform, string token)
    {
        while (transform != null)
        {
            if (transform.name.ToLowerInvariant().Contains(token))
            {
                return true;
            }

            transform = transform.parent;
        }

        return false;
    }

    static bool HasBinding(UnityEngine.InputSystem.InputAction action, string path)
    {
        if (action == null)
        {
            return false;
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].effectivePath == path || action.bindings[i].path == path)
            {
                return true;
            }
        }

        return false;
    }
#endif
}
