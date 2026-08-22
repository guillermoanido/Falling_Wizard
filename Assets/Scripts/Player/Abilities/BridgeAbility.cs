using UnityEngine;

namespace FallingWizard.Player
{
    // Lays the staff flat across the lip of a ledge so it becomes a plank you can walk out on.
    // Press again to pick it back up.
    //
    // The thing you stand on is a separate SOLID collider on a child of the staff, on the Ground
    // layer - the staff itself is on the Player layer, which the ground check deliberately
    // ignores, so a solid collider on the staff would be one you fall straight through.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff Bridge", fileName = "Staff Bridge")]
    public class BridgeAbility : Ability
    {
        // Deliberately the same shape as the Staff spell: press at a ledge to put it out,
        // press again to take it back. The only difference is which mode it asks for - so
        // neither spell can ever touch a staff the other one is using.
        public override bool CanCast(PlayerLogic wizard) =>
            wizard.StaffIsPlantedAs(StaffMode.Bridge) ||
            (wizard.StaffIsFree && wizard.movement.IsAtEdge);

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.StaffIsPlantedAs(StaffMode.Bridge))
            {
                wizard.RecoverStaff();
                return true;
            }

            return wizard.TryPlantStaff(StaffMode.Bridge);
        }

        // Walking off the far end of your own bridge and dying with it still out there would be
        // a bad surprise, so a death puts it back on the wizard's shoulder.
        public override void OnRunReset(PlayerLogic wizard)
        {
            if (wizard.HasPole && wizard.Pole.IsPlanted && wizard.Pole.Mode == StaffMode.Bridge)
                wizard.RecoverStaff();
        }
    }
}
