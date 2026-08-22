using UnityEngine;

namespace FallingWizard.Player
{
    // Spell one, the one the wizard starts with. Press at the lip of a ledge to drive the staff
    // in and climb down it; press again to let go from wherever you have got to.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        // Hanging on it, or free to plant it. Never while it is doing something else.
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

            // Returning false when there is no ledge yet keeps the press buffered, so pressing
            // a moment before arriving still plants.
            return wizard.TryPlantStaff(StaffMode.Ladder);
        }
    }
}
