using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Rock : Hazard
    {
        void Reset()
        {
            minimumSpeed = 4f;
            rearmDelay = 1f;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
