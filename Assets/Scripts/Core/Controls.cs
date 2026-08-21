using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    // Every input lookup in the game goes through here, so there is one place that knows the
    // action paths, one place that knows which device the player is actually holding, and one
    // place that turns an action into the letter or button to print on the HUD.
    public static class Controls
    {
        public const string KeyboardScheme = "Keyboard&Mouse";
        public const string GamepadScheme = "Gamepad";

        const string PausePath = "UI/Pause";
        const string SkipPath = "UI/Skip";

        // Only actions this game asked for. InputSystem.actions.Enable() switches on every map,
        // including UI/Point and Player/Look, which fire on any mouse twitch; without this filter
        // the HUD would flip to keyboard letters mid gamepad play every time the mouse moved.
        static readonly HashSet<InputAction> Watched = new HashSet<InputAction>();

        static InputAction pause;
        static InputAction skip;

        // Which kind of device the player last actually used, for the HUD to print against.
        public static string Scheme { get; private set; } = KeyboardScheme;

        public static event Action SchemeChanged;

        public static bool PausePressed => pause != null && pause.WasPressedThisFrame();

        public static bool SkipPressed => skip != null && skip.WasPressedThisFrame();

        public static InputAction Player(string action) => Find($"Player/{action}");

        public static InputAction Find(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            InputActionAsset actions = InputSystem.actions;
            InputAction action = actions != null ? actions.FindAction(path) : null;

            if (action == null)
            {
                Debug.LogError($"Input action '{path}' is missing from the project-wide actions asset.");
                return null;
            }

            action.Enable();
            Watched.Add(action);
            return action;
        }

        // "E" on a keyboard, "X" on an Xbox pad, "Square" on a DualSense. Passing the group is the
        // whole trick: without it the Input System helpfully returns "E | X" for every action.
        public static string Glyph(InputAction action)
        {
            if (action == null)
                return string.Empty;

            return action.GetBindingDisplayString(
                InputBinding.MaskByGroup(Scheme),
                InputBinding.DisplayStringOptions.DontIncludeInteractions);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Watched.Clear();
            Scheme = Gamepad.current != null && Keyboard.current == null ? GamepadScheme : KeyboardScheme;

            pause = Find(PausePath);
            skip = Find(SkipPath);

            InputSystem.onActionChange -= OnActionChange;
            InputSystem.onActionChange += OnActionChange;
            InputSystem.onDeviceChange -= OnDeviceChange;
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        static void OnActionChange(object subject, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed)
                return;

            if (!(subject is InputAction action) || !Watched.Contains(action))
                return;

            InputDevice device = action.activeControl != null ? action.activeControl.device : null;

            string next = device is Gamepad ? GamepadScheme
                        : device is Keyboard || device is Mouse ? KeyboardScheme
                        : Scheme;

            if (next == Scheme)
                return;

            Scheme = next;
            SchemeChanged?.Invoke();
        }

        // Unplugging the pad mid game has to fall back, or the HUD keeps promising buttons that
        // are no longer attached to anything.
        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool lost = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;

            if (device is Gamepad && lost && Scheme == GamepadScheme)
                Scheme = KeyboardScheme;

            SchemeChanged?.Invoke();
        }
    }
}
