using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff Bridge", fileName = "Staff Bridge")]
    public class BridgeAbility : Ability
    {
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

        public override void OnRunReset(PlayerLogic wizard)
        {
            if (wizard.HasPole && wizard.Pole.IsPlanted && wizard.Pole.Mode == StaffMode.Bridge)
                wizard.RecoverStaff();
        }
    }
}
