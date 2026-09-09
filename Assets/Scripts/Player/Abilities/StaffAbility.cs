using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        const float DefaultLean = 0.5f;

        [Header("Ranks")]
        [Tooltip("How long the staff is at each rank, against the length you built it. Element 0 " +
                 "is rank 1. Everything is measured off the pole's own scale, so this is the only " +
                 "number that has to change - how tall a wall it will climb, how deep a drop it " +
                 "will reach down and where the hand-hang ends all follow.")]
        public float[] lengthByRank = { 1f, 1.5f };

        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats) =>
            wizard.SetStaffLength(TierFor(lengthByRank, wizard.spellbook.RankOf(this)));

        public override void OnUnequipped(PlayerLogic wizard) => wizard.SetStaffLength(1f);

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.IsOnStaff ||
            (wizard.StaffIsFree && (wizard.movement.IsAtEdge || wizard.CanClimbHere));

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!wizard.HasPole)
                return "there is no Staff object under the wizard to plant";

            if (wizard.Pole.IsPlanted && wizard.Pole.Mode != StaffMode.Ladder)
                return "the staff is already out, laid flat";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            if (wizard.HasPole && !wizard.Pole.IsReady)
                return "the staff is still being brought back to hand, " +
                       $"{wizard.Pole.CooldownLeft:0.00}s to go";

            if (wizard.movement.TryFindLedgeEdge(out _))
                return "the drop here is too shallow for the staff to reach down into";

            PlayerLogic.Movement walk = wizard.movement;

            switch (walk.WhyNoClimb)
            {
                case PlayerLogic.Movement.ClimbRefusal.NotStanding:
                    return "you are not stood on anything to prop the staff against";

                case PlayerLogic.Movement.ClimbRefusal.NoWall:
                    return "the staff's tip is resting on something barely off the floor - " +
                           "aim it higher, or stand closer to what you are climbing";

                case PlayerLogic.Movement.ClimbRefusal.NothingOnTop:
                    return $"the staff's tip, {walk.ClimbCanReach:0.00} boxes up, has no ledge " +
                           "under it - aim it up or down until it rests on one";

                case PlayerLogic.Movement.ClimbRefusal.TooTall:
                    return "the staff's tip is buried in the wall rather than resting on top " +
                           "of it - aim it higher";

                case PlayerLogic.Movement.ClimbRefusal.NoRoomOnTop:
                    return $"the top is {walk.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but the wizard does not fit standing on it - something is in " +
                           "the way just past the lip";

                case PlayerLogic.Movement.ClimbRefusal.NoHeadroom:
                    return $"the top is {walk.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but something is in the way directly above the wizard's head";
            }

            return null;
        }

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            if (wizard.IsOnStaff || !wizard.StaffIsFree)
                return;

            wizard.PropStaff();
            wizard.AimStaff(wizard.Steering.Lean, fixedDeltaTime);

            if (!ReachingUp(wizard) && wizard.movement.IsAtEdge &&
                wizard.TryPlantStaff(StaffMode.Ladder))
                return;

            wizard.TryClimbStaff();
        }

        static bool ReachingUp(PlayerLogic wizard)
        {
            float threshold = wizard.HasPole ? wizard.Pole.leanThreshold : DefaultLean;

            return wizard.Steering.Lean > threshold;
        }

        public override void OnReleased(PlayerLogic wizard, float heldSeconds) =>
            wizard.LowerStaff();

        public override void OnChargeLost(PlayerLogic wizard) => wizard.LowerStaff();
    }
}
