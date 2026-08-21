using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public struct PlayerInput
    {
        const float LookThreshold = 0.5f;

        public Vector2 Move;
        public bool JumpPressed;
        public bool JumpHeld;
        public bool Walk;
        public bool StaffPressed;
        public bool StaffHeld;

        public float Steer => Move.x;

        public float Lean => Move.y;

        public bool LookingDown => Move.y < -LookThreshold;

        public bool LookingUp => Move.y > LookThreshold;

        public MoveCommand Movement => new MoveCommand
        {
            Steer = Move.x,
            JumpHeld = JumpHeld,
            Walk = Walk,
        };
    }

    public class PlayerInputReader
    {
        const string MoveActionPath = "Player/Move";
        const string JumpActionPath = "Player/Jump";
        const string WalkActionPath = "Player/Walk";
        const string StaffActionPath = "Player/Staff";

        readonly InputAction move;
        readonly InputAction jump;
        readonly InputAction walk;
        readonly InputAction staff;

        public PlayerInputReader()
        {
            move = FindAction(MoveActionPath);
            jump = FindAction(JumpActionPath);
            walk = FindAction(WalkActionPath);
            staff = FindAction(StaffActionPath);
        }

        public PlayerInput Read()
        {
            if (Game.IsPaused)
                return default;

            return new PlayerInput
            {
                Move = move != null ? move.ReadValue<Vector2>() : Vector2.zero,
                JumpPressed = jump != null && jump.WasPressedThisFrame(),
                JumpHeld = jump != null && jump.IsPressed(),
                Walk = walk != null && walk.IsPressed(),
                StaffPressed = staff != null && staff.WasPressedThisFrame(),
                StaffHeld = staff != null && staff.IsPressed(),
            };
        }

        static InputAction FindAction(string path)
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
