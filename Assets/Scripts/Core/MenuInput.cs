using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    public static class MenuInput
    {
        public static bool PausePressedThisFrame =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        public static bool AnyButtonPressedThisFrame =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                         Gamepad.current.startButton.wasPressedThisFrame));
    }
}
