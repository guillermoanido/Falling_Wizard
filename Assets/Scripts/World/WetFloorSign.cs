using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WetFloorSign : Hazard
    {
        const float RunningOnly = 3f;

        const float SlipCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = SlipCooldown;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
