using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        InputAction moveAction;
        InputAction jumpAction;

        public Vector2 Move => Active ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        public bool JumpPressedThisFrame => Active && jumpAction.WasPressedThisFrame();

        public bool JumpHeld => Active && jumpAction.IsPressed();

        bool Active => moveAction != null && jumpAction != null && !GamePause.IsPaused;

        void Awake()
        {
            InputActionAsset actions = InputSystem.actions;
            if (actions == null)
            {
                Debug.LogError("No project-wide input actions assigned. " +
                               "Set them in Project Settings > Input System Package.", this);
                return;
            }

            moveAction = actions.FindAction("Player/Move");
            jumpAction = actions.FindAction("Player/Jump");
        }

        void OnEnable()
        {
            moveAction?.Enable();
            jumpAction?.Enable();
        }
    }
}
