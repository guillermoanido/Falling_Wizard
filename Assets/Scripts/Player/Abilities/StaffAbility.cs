using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        public override bool CanCast(PlayerLogic wizard) =>
            wizard.StaffIsPlantedAs(StaffMode.Ladder) ||
            (wizard.StaffIsFree && wizard.movement.IsAtEdge);

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
