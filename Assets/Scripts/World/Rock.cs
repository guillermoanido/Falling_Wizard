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
        [Header("Rock")]
        [Tooltip("Backward and upward shove as they go over, in boxes per second.")]
        public Vector2 knockback = new Vector2(2f, 1.5f);

        [Tooltip("Seconds the wizard cannot steer after being clipped.")]
        [Min(0f)] public float controlLockout = 0.15f;

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

        protected override void Affect(PlayerLogic wizard)
        {
            // Backwards is away from where they were heading, not away from the rock: a rock you
            // clipped a corner of should still throw you the way you were going.
            var shove = new Vector2(-wizard.movement.Facing * knockback.x, knockback.y);

            wizard.Shove(shove, controlLockout);
            wizard.Trip();
        }
    }
}
