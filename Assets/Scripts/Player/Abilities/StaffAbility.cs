using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
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
