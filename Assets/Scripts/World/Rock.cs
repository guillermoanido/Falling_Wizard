using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Rock : Hazard
    {
        // Between a walk (2 boxes a second) and a run (6), so running into one trips and
        // picking your way past at a walk does not.
        const float RunningOnly = 4f;

        const float TripCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = TripCooldown;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
