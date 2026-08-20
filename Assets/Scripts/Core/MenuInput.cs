using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    public static class MenuInput
    {
        const string PauseActionPath = "UI/Pause";
        const string SkipActionPath = "UI/Skip";

        static InputAction pauseAction;
        static InputAction skipAction;

        public static bool PausePressedThisFrame => pauseAction != null && pauseAction.WasPressedThisFrame();

        public static bool SkipPressedThisFrame => skipAction != null && skipAction.WasPressedThisFrame();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bind()
        {
            pauseAction = Find(PauseActionPath);
            skipAction = Find(SkipActionPath);
        }

        static InputAction Find(string path)
        {
            InputActionAsset actions = InputSystem.actions;
            InputAction action = actions != null ? actions.FindAction(path) : null;

            if (action == null)
                Debug.LogError($"Input action '{path}' is missing from the project-wide actions asset.");
            else
                action.Enable();

            return action;
        }
    }
}
