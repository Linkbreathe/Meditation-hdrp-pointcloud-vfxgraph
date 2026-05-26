using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
#endif

[DefaultExecutionOrder(-10000)]
[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK XR Simulator Input Bootstrap")]
public sealed class RDKXRSimulatorInputBootstrap : MonoBehaviour
{
    [SerializeField] bool _configureActionBasedControllers = true;
    [SerializeField] bool _bindKeyboardToRightController = true;
    [SerializeField] bool _logConfiguration = true;

    void Awake()
    {
        Configure();
    }

    [ContextMenu("Configure XR Simulator Input")]
    public void Configure()
    {
#if ENABLE_INPUT_SYSTEM
        if (!_configureActionBasedControllers)
        {
            return;
        }

        ActionBasedController[] controllers = FindObjectsOfType<ActionBasedController>(true);
        for (int i = 0; i < controllers.Length; i++)
        {
            ActionBasedController controller = controllers[i];
            if (controller == null)
            {
                continue;
            }

            bool rightHand = IsRightHand(controller);
            ConfigureController(controller, rightHand);
        }

        if (_logConfiguration)
        {
            XRBaseControllerInteractor[] interactors = FindObjectsOfType<XRBaseControllerInteractor>(true);
            XRInteractionManager interactionManager = FindObjectOfType<XRInteractionManager>(true);
            Debug.Log($"[RDKXRSimulatorInputBootstrap] Configured {controllers.Length} ActionBasedController objects and found {interactors.Length} controller interactors. Keyboard drives the right controller={_bindKeyboardToRightController}. XRInteractionManager active={(interactionManager != null && interactionManager.gameObject.activeInHierarchy)}.", this);

            if (controllers.Length == 0)
            {
                Debug.LogWarning("[RDKXRSimulatorInputBootstrap] No ActionBasedController was found in the scene.", this);
            }

            if (interactors.Length == 0)
            {
                Debug.LogWarning("[RDKXRSimulatorInputBootstrap] No XRBaseControllerInteractor was found in the scene.", this);
            }

            if (interactionManager == null || !interactionManager.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[RDKXRSimulatorInputBootstrap] XR Interaction Manager is missing or inactive.", this);
            }
        }
#else
        Debug.LogWarning("[RDKXRSimulatorInputBootstrap] ENABLE_INPUT_SYSTEM is not defined. Enable the Input System or Both in Player Settings.", this);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    void ConfigureController(ActionBasedController controller, bool rightHand)
    {
        string handedness = rightHand ? "RightHand" : "LeftHand";
        bool bindKeyboard = _bindKeyboardToRightController && rightHand;

        controller.positionAction = ActionProperty(
            "Position",
            InputActionType.Value,
            "Vector3",
            $"<XRController>{{{handedness}}}/pointerPosition",
            $"<XRController>{{{handedness}}}/devicePosition",
            $"<XRHandDevice>{{{handedness}}}/devicePosition");

        controller.rotationAction = ActionProperty(
            "Rotation",
            InputActionType.Value,
            "Quaternion",
            $"<XRController>{{{handedness}}}/pointerRotation",
            $"<XRController>{{{handedness}}}/deviceRotation",
            $"<XRHandDevice>{{{handedness}}}/deviceRotation");

        InputAction isTracked = CreateAction(
            "Is Tracked",
            InputActionType.Button,
            "Button",
            $"<XRController>{{{handedness}}}/isTracked",
            $"<XRHandDevice>{{{handedness}}}/isTracked");
        isTracked.wantsInitialStateCheck = true;
        controller.isTrackedAction = new InputActionProperty(isTracked);

        controller.trackingStateAction = ActionProperty(
            "Tracking State",
            InputActionType.Value,
            "Integer",
            $"<XRController>{{{handedness}}}/trackingState",
            $"<XRHandDevice>{{{handedness}}}/trackingState");

        controller.hapticDeviceAction = ActionProperty(
            "Haptic Device",
            InputActionType.PassThrough,
            string.Empty,
            $"<XRController>{{{handedness}}}/*");

        controller.selectAction = ActionProperty(
            "Select",
            InputActionType.Button,
            "Button",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{GripButton}}", $"<MetaAimHand>{{{handedness}}}/indexPressed", "<Keyboard>/e", "<Keyboard>/g"));

        controller.selectActionValue = ActionProperty(
            "Select Value",
            InputActionType.Value,
            "Axis",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{Grip}}", $"<MetaAimHand>{{{handedness}}}/pinchStrengthIndex", "<Keyboard>/e", "<Keyboard>/g"));

        controller.activateAction = ActionProperty(
            "Activate",
            InputActionType.Button,
            "Button",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{TriggerButton}}", "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton"));

        controller.activateActionValue = ActionProperty(
            "Activate Value",
            InputActionType.Value,
            "Axis",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{Trigger}}", "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton"));

        controller.uiPressAction = ActionProperty(
            "UI Press",
            InputActionType.Button,
            "Button",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{TriggerButton}}", "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton"));

        controller.uiPressActionValue = ActionProperty(
            "UI Press Value",
            InputActionType.Value,
            "Axis",
            Paths(bindKeyboard, $"<XRController>{{{handedness}}}/{{Trigger}}", "<Keyboard>/space", "<Keyboard>/enter", "<Mouse>/leftButton"));

        controller.uiScrollAction = ActionProperty(
            "UI Scroll",
            InputActionType.Value,
            "Vector2",
            $"<XRController>{{{handedness}}}/{{Primary2DAxis}}",
            "<Mouse>/scroll");
    }

    static bool IsRightHand(ActionBasedController controller)
    {
        Transform current = controller.transform;
        while (current != null)
        {
            string objectName = current.name.ToLowerInvariant();
            if (objectName.Contains("right"))
            {
                return true;
            }

            if (objectName.Contains("left"))
            {
                return false;
            }

            current = current.parent;
        }

        return true;
    }

    static InputActionProperty ActionProperty(string name, InputActionType type, string expectedControlType, params string[] paths)
    {
        return new InputActionProperty(CreateAction(name, type, expectedControlType, paths));
    }

    static InputAction CreateAction(string name, InputActionType type, string expectedControlType, params string[] paths)
    {
        InputAction action = new InputAction(name, type, expectedControlType: expectedControlType);
        for (int i = 0; i < paths.Length; i++)
        {
            if (!string.IsNullOrEmpty(paths[i]))
            {
                action.AddBinding(paths[i]);
            }
        }

        return action;
    }

    static string[] Paths(bool includeKeyboard, params string[] paths)
    {
        if (includeKeyboard)
        {
            return paths;
        }

        int xrPathCount = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i].StartsWith("<XRController>") || paths[i].StartsWith("<XRHandDevice>") || paths[i].StartsWith("<MetaAimHand>"))
            {
                xrPathCount++;
            }
        }

        string[] xrPaths = new string[xrPathCount];
        int output = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            if (paths[i].StartsWith("<XRController>") || paths[i].StartsWith("<XRHandDevice>") || paths[i].StartsWith("<MetaAimHand>"))
            {
                xrPaths[output++] = paths[i];
            }
        }

        return xrPaths;
    }
#endif
}
