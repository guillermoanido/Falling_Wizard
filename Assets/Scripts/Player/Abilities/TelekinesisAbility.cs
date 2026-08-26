using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Telekinesis", fileName = "Telekinesis")]
    public class TelekinesisAbility : Ability
    {
        [Header("Reach")]
        [Tooltip("How far the wizard can take hold of something, in boxes. A mage is one box, so " +
                 "6 is about a screen's worth of stone.")]
        [Min(1f)] public float reach = 6f;

        [Header("Carrying")]
        [Tooltip("How far in front of the wizard the stone rides, in boxes.")]
        [Min(0f)] public float holdDistance = 1.9f;

        [Tooltip("How far above their feet it rides, in boxes.")]
        public float holdHeight = 0.9f;

        [Tooltip("How fast it is dragged to where it should be, in boxes per second. Low is " +
                 "heavy and lags behind you; high snaps it to your side.")]
        [Min(0.5f)] public float pullSpeed = 12f;

        [Header("Letting Go")]
        [Tooltip("Speed it is thrown at when you let go while holding a direction, in boxes per " +
                 "second. Let go with no direction and it simply drops where it hangs.")]
        [Min(0f)] public float throwSpeed = 14f;

        [Tooltip("Stick tilt needed to count as aiming a throw rather than dropping it.")]
        [Range(0.05f, 1f)] public float aimThreshold = 0.35f;

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.spellbook.StateOf<Hold>(this).stone != null)
                return true;

            return wizard.State == PlayerState.Normal &&
                   Liftable.Nearest(wizard.movement.Position, reach) != null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Hold hold = wizard.spellbook.StateOf<Hold>(this);

            if (hold.stone != null)
            {
                LetGo(wizard, hold);
                return true;
            }

            Liftable stone = Liftable.Nearest(wizard.movement.Position, reach);

            if (stone == null)
                return false;

            stone.Grab();
            hold.stone = stone;
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Hold hold = wizard.spellbook.StateOf<Hold>(this);

            if (hold.stone == null)
            {
                wizard.spellbook.Extinguish(this);
                return;
            }

            hold.stone.CarryTo(HoldPoint(wizard), pullSpeed, fixedDeltaTime);
        }

        public override void OnEnded(PlayerLogic wizard) =>
            LetGo(wizard, wizard.spellbook.StateOf<Hold>(this));

        public override void OnRunReset(PlayerLogic wizard) =>
            LetGo(wizard, wizard.spellbook.StateOf<Hold>(this));

        public override void OnUnequipped(PlayerLogic wizard) =>
            LetGo(wizard, wizard.spellbook.StateOf<Hold>(this));

        Vector2 HoldPoint(PlayerLogic wizard)
        {
            PlayerLogic.Movement walk = wizard.movement;

            return new Vector2(walk.Position.x + walk.Facing * holdDistance,
                               walk.FeetY + holdHeight);
        }

        void LetGo(PlayerLogic wizard, Hold hold)
        {
            if (hold.stone == null)
                return;

            Vector2 aim = wizard.Steering.Move;

            Vector2 launch = aim.sqrMagnitude >= aimThreshold * aimThreshold
                ? aim.normalized * throwSpeed
                : Vector2.zero;

            hold.stone.Release(launch);
            hold.stone = null;
        }

        public class Hold
        {
            public Liftable stone;
        }
    }
}
