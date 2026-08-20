using UnityEngine.InputSystem;

namespace FallingWizard.Core
{
    public static class MenuInput
    {
        // Esc on a keyboard, Start on a gamepad. Opens and closes the pause menu.
        public static bool PausePressedThisFrame =>
            (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

        // Any key, face button or click. Used to skip the cutscene.
        public static bool AnyButtonPressedThisFrame =>
            (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) ||
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                                         Gamepad.current.startButton.wasPressedThisFrame));
    }
}
