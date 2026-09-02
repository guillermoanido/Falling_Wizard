using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    public static class Controls
    {
        public const string KeyboardScheme = "Keyboard&Mouse";
        public const string GamepadScheme = "Gamepad";

        const string PausePath = "UI/Pause";
        const string SkipPath = "UI/Skip";
        const string CancelPath = "UI/Cancel";

        // The loadout door - Tab. On a key of its own rather than sharing Pause: two
        // MonoBehaviours reading the same WasPressedThisFrame in an undefined order both act on
        // it, which is how one press used to open the pause menu AND drop the skill screen on
        // top of it.
        const string LoadoutPath = "UI/Loadout";

        static readonly HashSet<InputAction> Watched = new HashSet<InputAction>();

        static InputAction pause;
        static InputAction skip;
        static InputAction cancel;
        static InputAction loadout;

        public static string Scheme { get; private set; } = KeyboardScheme;

        public static event Action SchemeChanged;

        public static bool PausePressed => pause != null && pause.WasPressedThisFrame();

        public static bool SkipPressed => skip != null && skip.WasPressedThisFrame();

        public static bool CancelPressed => cancel != null && cancel.WasPressedThisFrame();

        public static bool LoadoutPressed => loadout != null && loadout.WasPressedThisFrame();

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
            cancel = Find(CancelPath);
            loadout = Find(LoadoutPath);

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

        static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            bool lost = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected;

            if (device is Gamepad && lost && Scheme == GamepadScheme)
                Scheme = KeyboardScheme;

            SchemeChanged?.Invoke();
        }
    }
}
