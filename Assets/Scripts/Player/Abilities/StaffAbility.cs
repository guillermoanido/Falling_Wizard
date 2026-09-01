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

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.StaffIsPlantedAs(StaffMode.Ladder) ||
            (wizard.StaffIsFree && wizard.movement.IsAtEdge);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!wizard.HasPole)
                return "there is no Staff object under the wizard to plant";

            if (wizard.Pole.IsPlanted && wizard.Pole.Mode != StaffMode.Ladder)
                return "the staff is already out, laid flat";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            if (!wizard.movement.IsAtEdge)
                return "you are not stood at a ledge";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.IsOnStaff)
            {
                wizard.DropFromStaff();
                return true;
            }

            return wizard.TryPlantStaff(StaffMode.Ladder);
        }
    }
}
