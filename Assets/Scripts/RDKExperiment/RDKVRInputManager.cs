using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[AddComponentMenu("RDK Experiment/RDK VR Input Manager")]
public sealed class RDKVRInputManager : MonoBehaviour
{
    [Header("Keyboard Fallback")]
    [SerializeField] bool _allowKeyboardFallback = true;
    [SerializeField] bool _allowAnyKeyboardStart;
    [SerializeField] bool _allowMouseStart = true;
    [SerializeField] KeyCode _startKey = KeyCode.P;
    [SerializeField] KeyCode _alternateStartKey = KeyCode.Space;
    [SerializeField] KeyCode _xrPrimaryStartKey = KeyCode.B;
    [SerializeField] KeyCode _xrSecondaryStartKey = KeyCode.N;
    [SerializeField] KeyCode _xrGripStartKey = KeyCode.E;
    [SerializeField] KeyCode _xrGripAlternateStartKey = KeyCode.G;
    [SerializeField] KeyCode _enterStartKey = KeyCode.Return;
    [SerializeField] KeyCode _resetKey = KeyCode.R;
    [SerializeField] KeyCode _alternateResetKey = KeyCode.V;
    [SerializeField] KeyCode _leftKey = KeyCode.LeftArrow;
    [SerializeField] KeyCode _leftAlternateKey = KeyCode.A;
    [SerializeField] KeyCode _rightKey = KeyCode.RightArrow;
    [SerializeField] KeyCode _rightAlternateKey = KeyCode.D;
    [SerializeField] KeyCode _rightPrimaryKey = KeyCode.Return;
    [SerializeField] KeyCode _rightPrimaryAlternateKey = KeyCode.B;
    [SerializeField] KeyCode _randomKey = KeyCode.DownArrow;
    [SerializeField] KeyCode _randomAlternateKey = KeyCode.W;
    [SerializeField] KeyCode _randomSecondaryKey = KeyCode.N;
    [SerializeField] bool _logInputEvents = true;

    readonly List<UnityEngine.XR.InputDevice> _devices = new List<UnityEngine.XR.InputDevice>();
    bool _leftTriggerWasDown;
    bool _rightTriggerWasDown;
    bool _leftGripWasDown;
    bool _rightGripWasDown;
    bool _leftMenuWasDown;
    bool _rightMenuWasDown;
    bool _leftPrimaryWasDown;
    bool _leftSecondaryWasDown;
    bool _rightPrimaryWasDown;
    bool _rightSecondaryWasDown;

    public void ResetButtonState()
    {
        _leftTriggerWasDown = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.triggerButton, out _);
        _rightTriggerWasDown = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.triggerButton, out _);
        _leftGripWasDown = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.gripButton, out _);
        _rightGripWasDown = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.gripButton, out _);
        _leftMenuWasDown = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.menuButton, out _);
        _rightMenuWasDown = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.menuButton, out _);
        _leftPrimaryWasDown = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out _);
        _leftSecondaryWasDown = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out _);
        _rightPrimaryWasDown = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out _);
        _rightSecondaryWasDown = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out _);
    }

    public bool TryConsumeStart(out RDKInputSample sample)
    {
        sample = default;

        if (_allowKeyboardFallback)
        {
            if (TryConsumeKeyboardKey(_startKey, RDKResponseDirection.None, "KeyboardStart", out sample) ||
                TryConsumeKeyboardKey(_alternateStartKey, RDKResponseDirection.None, "KeyboardStart", out sample) ||
                TryConsumeKeyboardKey(_xrPrimaryStartKey, RDKResponseDirection.None, "XR Simulator Primary", out sample) ||
                TryConsumeKeyboardKey(_xrSecondaryStartKey, RDKResponseDirection.None, "XR Simulator Secondary", out sample) ||
                TryConsumeKeyboardKey(_xrGripStartKey, RDKResponseDirection.None, "XR Simulator Grip", out sample) ||
                TryConsumeKeyboardKey(_xrGripAlternateStartKey, RDKResponseDirection.None, "XR Simulator Grip", out sample) ||
                TryConsumeKeyboardKey(_enterStartKey, RDKResponseDirection.None, "KeyboardStart", out sample))
            {
                return EmitInput("start", sample);
            }

            if (_allowMouseStart && WasMousePressed())
            {
                sample = RDKInputSample.Create(RDKResponseDirection.None, "MouseLeft", "MouseStart/XR Simulator Trigger");
                return EmitInput("start", sample);
            }

            if (_allowAnyKeyboardStart && WasAnyKeyboardPressed())
            {
                sample = RDKInputSample.Create(RDKResponseDirection.None, "AnyKey", "KeyboardStart");
                return EmitInput("start", sample);
            }
        }

        bool leftTrigger = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.triggerButton, out string leftTriggerDevice);
        bool rightTrigger = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.triggerButton, out string rightTriggerDevice);
        bool leftTriggerDown = leftTrigger && !_leftTriggerWasDown;
        bool rightTriggerDown = rightTrigger && !_rightTriggerWasDown;
        _leftTriggerWasDown = leftTrigger;
        _rightTriggerWasDown = rightTrigger;
        if (leftTriggerDown || rightTriggerDown)
        {
            sample = RDKInputSample.Create(
                RDKResponseDirection.None,
                "XR trigger",
                leftTriggerDown ? leftTriggerDevice : rightTriggerDevice);
            return EmitInput("start", sample);
        }

        bool leftGrip = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.gripButton, out string leftGripDevice);
        bool rightGrip = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.gripButton, out string rightGripDevice);
        bool leftGripDown = leftGrip && !_leftGripWasDown;
        bool rightGripDown = rightGrip && !_rightGripWasDown;
        _leftGripWasDown = leftGrip;
        _rightGripWasDown = rightGrip;
        if (leftGripDown || rightGripDown)
        {
            sample = RDKInputSample.Create(
                RDKResponseDirection.None,
                "XR grip",
                leftGripDown ? leftGripDevice : rightGripDevice);
            return EmitInput("start", sample);
        }

        bool leftPrimary = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out string leftPrimaryDevice);
        bool leftSecondary = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out string leftSecondaryDevice);
        bool rightPrimary = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out string rightPrimaryDevice);
        bool rightSecondary = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out string rightSecondaryDevice);
        bool leftPrimaryDown = leftPrimary && !_leftPrimaryWasDown;
        bool leftSecondaryDown = leftSecondary && !_leftSecondaryWasDown;
        bool rightPrimaryDown = rightPrimary && !_rightPrimaryWasDown;
        bool rightSecondaryDown = rightSecondary && !_rightSecondaryWasDown;
        _leftPrimaryWasDown = leftPrimary;
        _leftSecondaryWasDown = leftSecondary;
        _rightPrimaryWasDown = rightPrimary;
        _rightSecondaryWasDown = rightSecondary;
        if (leftPrimaryDown || leftSecondaryDown || rightPrimaryDown || rightSecondaryDown)
        {
            string deviceName = FirstNonEmpty(
                leftPrimaryDown ? leftPrimaryDevice : string.Empty,
                leftSecondaryDown ? leftSecondaryDevice : string.Empty,
                rightPrimaryDown ? rightPrimaryDevice : string.Empty,
                rightSecondaryDown ? rightSecondaryDevice : string.Empty);
            sample = RDKInputSample.Create(RDKResponseDirection.None, "XR primary/secondary", deviceName);
            return EmitInput("start", sample);
        }

        return false;
    }

    public bool TryConsumeReset(out RDKInputSample sample)
    {
        sample = default;

        if (_allowKeyboardFallback &&
            (TryConsumeKeyboardKey(_resetKey, RDKResponseDirection.None, "XR Simulator Reset", out sample) ||
             TryConsumeKeyboardKey(_alternateResetKey, RDKResponseDirection.None, "KeyboardReset", out sample)))
        {
            return EmitInput("reset", sample);
        }

        bool leftMenu = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.menuButton, out string leftMenuDevice);
        bool rightMenu = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.menuButton, out string rightMenuDevice);
        bool leftMenuDown = leftMenu && !_leftMenuWasDown;
        bool rightMenuDown = rightMenu && !_rightMenuWasDown;
        _leftMenuWasDown = leftMenu;
        _rightMenuWasDown = rightMenu;
        if (leftMenuDown || rightMenuDown)
        {
            sample = RDKInputSample.Create(
                RDKResponseDirection.None,
                "XR menu",
                leftMenuDown ? leftMenuDevice : rightMenuDevice);
            return EmitInput("reset", sample);
        }

        return false;
    }

    public bool TryConsumeResponse(out RDKInputSample sample)
    {
        sample = default;

        if (_allowKeyboardFallback)
        {
            if (TryConsumeKeyboardKey(_leftKey, RDKResponseDirection.Left, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_leftAlternateKey, RDKResponseDirection.Left, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_rightKey, RDKResponseDirection.Right, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_rightAlternateKey, RDKResponseDirection.Right, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_rightPrimaryKey, RDKResponseDirection.Right, "PC Simulated Right Primary", out sample) ||
                TryConsumeKeyboardKey(_rightPrimaryAlternateKey, RDKResponseDirection.Right, "XR Simulator Primary", out sample) ||
                TryConsumeKeyboardKey(_randomKey, RDKResponseDirection.Random, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_randomAlternateKey, RDKResponseDirection.Random, "Keyboard", out sample) ||
                TryConsumeKeyboardKey(_randomSecondaryKey, RDKResponseDirection.Random, "XR Simulator Secondary", out sample))
            {
                return EmitInput("response", sample);
            }
        }

        bool leftPrimary = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out string leftPrimaryDevice);
        bool leftSecondary = ReadButton(InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out string leftSecondaryDevice);
        bool rightPrimary = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.primaryButton, out string rightPrimaryDevice);
        bool rightSecondary = ReadButton(InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, UnityEngine.XR.CommonUsages.secondaryButton, out string rightSecondaryDevice);

        bool leftPrimaryDown = leftPrimary && !_leftPrimaryWasDown;
        bool leftSecondaryDown = leftSecondary && !_leftSecondaryWasDown;
        bool rightPrimaryDown = rightPrimary && !_rightPrimaryWasDown;
        bool rightSecondaryDown = rightSecondary && !_rightSecondaryWasDown;

        _leftPrimaryWasDown = leftPrimary;
        _leftSecondaryWasDown = leftSecondary;
        _rightPrimaryWasDown = rightPrimary;
        _rightSecondaryWasDown = rightSecondary;

        if (leftPrimaryDown)
        {
            sample = RDKInputSample.Create(RDKResponseDirection.Left, "Left primary/X", leftPrimaryDevice);
            return EmitInput("response", sample);
        }

        if (rightPrimaryDown)
        {
            sample = RDKInputSample.Create(RDKResponseDirection.Right, "Right primary/A", rightPrimaryDevice);
            return EmitInput("response", sample);
        }

        if (rightSecondaryDown)
        {
            sample = RDKInputSample.Create(RDKResponseDirection.Random, "Right secondary/B", rightSecondaryDevice);
            return EmitInput("response", sample);
        }

        if (leftSecondaryDown)
        {
            sample = RDKInputSample.Create(RDKResponseDirection.Random, "Left secondary/Y", leftSecondaryDevice);
            return EmitInput("response", sample);
        }

        return false;
    }

    bool TryConsumeKeyboardKey(KeyCode key, RDKResponseDirection response, string deviceName, out RDKInputSample sample)
    {
        sample = default;
        if (key == KeyCode.None || !WasKeyboardPressed(key))
        {
            return false;
        }

        sample = RDKInputSample.Create(response, key.ToString(), deviceName);
        return true;
    }

    bool EmitInput(string phase, RDKInputSample sample)
    {
        if (_logInputEvents)
        {
            Debug.Log($"[RDKVRInputManager] {phase}: {sample.buttonName} from {sample.deviceName}", this);
        }

        return true;
    }

    static string FirstNonEmpty(string a, string b, string c, string d)
    {
        if (!string.IsNullOrEmpty(a))
        {
            return a;
        }

        if (!string.IsNullOrEmpty(b))
        {
            return b;
        }

        if (!string.IsNullOrEmpty(c))
        {
            return c;
        }

        return d;
    }

    static bool WasAnyKeyboardPressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.anyKeyDown;
#endif

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        pressed |= keyboard != null && keyboard.anyKey.wasPressedThisFrame;
#endif

        return pressed;
    }

    static bool WasMousePressed()
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetMouseButtonDown(0);
#endif

#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        pressed |= mouse != null && mouse.leftButton.wasPressedThisFrame;
#endif

        return pressed;
    }

    static bool WasKeyboardPressed(KeyCode key)
    {
        bool pressed = false;

#if ENABLE_LEGACY_INPUT_MANAGER
        pressed |= Input.GetKeyDown(key);
#endif

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return pressed;
        }

        switch (key)
        {
            case KeyCode.LeftArrow:
                pressed |= keyboard.leftArrowKey.wasPressedThisFrame;
                break;
            case KeyCode.RightArrow:
                pressed |= keyboard.rightArrowKey.wasPressedThisFrame;
                break;
            case KeyCode.DownArrow:
                pressed |= keyboard.downArrowKey.wasPressedThisFrame;
                break;
            case KeyCode.UpArrow:
                pressed |= keyboard.upArrowKey.wasPressedThisFrame;
                break;
            case KeyCode.Space:
                pressed |= keyboard.spaceKey.wasPressedThisFrame;
                break;
            case KeyCode.A:
                pressed |= keyboard.aKey.wasPressedThisFrame;
                break;
            case KeyCode.B:
                pressed |= keyboard.bKey.wasPressedThisFrame;
                break;
            case KeyCode.D:
                pressed |= keyboard.dKey.wasPressedThisFrame;
                break;
            case KeyCode.E:
                pressed |= keyboard.eKey.wasPressedThisFrame;
                break;
            case KeyCode.G:
                pressed |= keyboard.gKey.wasPressedThisFrame;
                break;
            case KeyCode.N:
                pressed |= keyboard.nKey.wasPressedThisFrame;
                break;
            case KeyCode.S:
                pressed |= keyboard.sKey.wasPressedThisFrame;
                break;
            case KeyCode.R:
                pressed |= keyboard.rKey.wasPressedThisFrame;
                break;
            case KeyCode.W:
                pressed |= keyboard.wKey.wasPressedThisFrame;
                break;
            case KeyCode.P:
                pressed |= keyboard.pKey.wasPressedThisFrame;
                break;
            case KeyCode.V:
                pressed |= keyboard.vKey.wasPressedThisFrame;
                break;
            case KeyCode.Return:
                pressed |= keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
                break;
            case KeyCode.KeypadEnter:
                pressed |= keyboard.numpadEnterKey.wasPressedThisFrame;
                break;
        }
#endif

        return pressed;
    }

    bool ReadButton(InputDeviceCharacteristics characteristics, InputFeatureUsage<bool> usage, out string deviceName)
    {
        deviceName = string.Empty;
        _devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(characteristics, _devices);

        for (int i = 0; i < _devices.Count; i++)
        {
            UnityEngine.XR.InputDevice device = _devices[i];
            if (device.TryGetFeatureValue(usage, out bool pressed) && pressed)
            {
                deviceName = device.name;
                return true;
            }
        }

        return false;
    }
}

public struct RDKInputSample
{
    public RDKResponseDirection response;
    public string buttonName;
    public string deviceName;
    public double realtime;

    public static RDKInputSample Create(RDKResponseDirection response, string buttonName, string deviceName)
    {
        return new RDKInputSample
        {
            response = response,
            buttonName = buttonName,
            deviceName = deviceName,
            realtime = Time.realtimeSinceStartupAsDouble
        };
    }
}
