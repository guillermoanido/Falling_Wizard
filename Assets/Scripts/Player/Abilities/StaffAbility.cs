using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        [Header("Ranks")]
        [Tooltip("How long the staff is at each rank, against the length you built it. Element 0 " +
                 "is rank 1. The reach is measured off the pole's own scale, so this is the only " +
                 "number that has to change - the climb, the hang and the drop all follow.")]
        public float[] lengthByRank = { 1f, 1.5f };

        // Applied from here rather than OnEquipped because buying a rank does not change WHICH
        // spell is in the slot, so OnEquipped would not fire and the staff would stay short until
        // the wizard next died. SetLengthScale early-outs when the number has not moved.
        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats) =>
            wizard.SetStaffLength(TierFor(lengthByRank, wizard.spellbook.RankOf(this)));

        public override void OnUnequipped(PlayerLogic wizard) => wizard.SetStaffLength(1f);

        // Two ways in, and which one you get is decided by what is in front of you rather than
        // by a second button. A ledge means the staff goes over it and you climb DOWN; anything
        // else means the staff goes UP against whatever is there, and you climb it.
        public override bool CanCast(PlayerLogic wizard) =>
            wizard.StaffIsPlantedAs(StaffMode.Ladder) ||
            (wizard.StaffIsFree && (wizard.movement.IsAtEdge || wizard.CanClimbHere));

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!wizard.HasPole)
                return "there is no Staff object under the wizard to plant";

            if (wizard.Pole.IsPlanted && wizard.Pole.Mode != StaffMode.Ladder)
                return "the staff is already out, laid flat";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            // Asked POSITIVELY, and never off IsAtEdge. That flag is a physics step old - it is
            // not refreshed while the wizard is on the staff - and it says only that ground is
            // MISSING ahead, not that a pole can be driven in there. Testing it left the one
            // case that actually needs explaining, a ledge the staff cannot use, returning null
            // and printing nothing at all.
            bool ledge = wizard.movement.TryFindLedgeEdge(out _);

            if (!ledge && !wizard.CanClimbHere)
                return "there is no ledge to hang the staff over and nothing ahead of you it " +
                       "will reach the top of";

            if (ledge)
                return "the drop here is too shallow for the staff to reach down into";

            return "the wall ahead is too tall for the staff to reach the top of";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.IsOnStaff)
            {
                wizard.DropFromStaff();
                return true;
            }

            // The ledge first, so the descent behaves exactly as it always has. Falling through
            // to the climb when that refuses is deliberate: at the very lip of a step both are
            // arguably true, and being carried up is the more useful of the two answers when the
            // drop was too shallow to plant over.
            if (wizard.movement.IsAtEdge && wizard.TryPlantStaff(StaffMode.Ladder))
                return true;

            return wizard.TryClimbStaff();
        }
    }
}
