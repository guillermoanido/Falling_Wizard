using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Something you run into rather than across. Clips the wizard's legs and sends them tumbling,
    // but only if they were actually running - walking past is fine.
    //
    // For a surface you STUMBLE ACROSS rather than trip over, use RoughGround instead.
    public class Rock : Hazard
    {
        // Works either way round, and the choice decides more than it looks like:
        //   SOLID on the Ground layer  - a block you bump into AND can stand on top of.
        //   TRIGGER on the Hazard layer - a stone you clip and stumble straight through.
        // A solid collider on the Hazard layer is the one combination to avoid: you would land
        // on top of it and the ground check, which only looks at Ground, would not see it.
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
