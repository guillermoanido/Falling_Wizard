using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        [Header("Controls")]
        [Tooltip("A tap this short reaches the staff down over a ledge instead. Hold it any " +
                 "longer and the staff spends the whole hold trying to grab whatever is in " +
                 "front of you.")]
        [Min(0.01f)] public float tapSeconds = 0.2f;

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
                    return wizard.movement.catchLedgesInTheAir
                        ? "there is nothing here to raise the staff against"
                        : "you are not stood on anything to raise the staff from";

                case PlayerLogic.Movement.ClimbRefusal.NoWall:
                    return $"there is nothing within {walk.climbReach:0.00} boxes in front of " +
                           "you to raise the staff against - walk right up to it, or raise " +
                           "Movement.climbReach";

                case PlayerLogic.Movement.ClimbRefusal.NothingOnTop:
                    return $"there is a wall ahead but nothing to stand on within the staff's " +
                           $"{walk.ClimbCanReach:0.00} boxes of reach";

                case PlayerLogic.Movement.ClimbRefusal.TooTall:
                    return $"what is ahead is taller than the staff, which reaches " +
                           $"{walk.ClimbCanReach:0.00} boxes up";

                case PlayerLogic.Movement.ClimbRefusal.NoRoomOnTop:
                    return $"the top is {walk.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but the wizard does not fit standing on it - something is in " +
                           "the way just past the lip";

                case PlayerLogic.Movement.ClimbRefusal.NoHeadroom:
                    return $"the top is {walk.ClimbRise:0.00} boxes up, which the staff can " +
                           "reach, but something is in the way directly above the wizard's head";

                case PlayerLogic.Movement.ClimbRefusal.Blocked:
                    return "the staff has nowhere to reach out from - something is already " +
                           "in the way right beside the wizard, usually a ceiling or an " +
                           "overhang directly overhead";
            }

            return null;
        }

        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            if (wizard.IsOnStaff || !wizard.StaffIsFree)
                return;

            wizard.RaiseStaff();
            wizard.TryClimbStaff();
        }

        public override void OnReleased(PlayerLogic wizard, float heldSeconds)
        {
            if (heldSeconds <= tapSeconds && wizard.StaffIsFree && wizard.movement.IsAtEdge &&
                wizard.TryPlantStaff(StaffMode.Ladder))
                return;

            wizard.LowerStaff();
        }

        public override void OnChargeLost(PlayerLogic wizard) => wizard.LowerStaff();
    }
}
