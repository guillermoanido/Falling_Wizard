using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Something you clip rather than collide with. You pass straight through it and go over,
    // but only if you were actually running - walking past is fine.
    public class Rock : Hazard
    {
        void Reset()
        {
            minimumSpeed = 4f;      // between a walk (2) and a run (6)
            rearmDelay = 1f;
        }

        // Just trip them. The launch - onward, and up enough to actually leave the floor -
        // belongs to the tumble itself, so a rock, a slime and a bad staircase all throw the
        // wizard the same way. Tune it once on PlayerLogic > Ragdoll > Launch.
        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
