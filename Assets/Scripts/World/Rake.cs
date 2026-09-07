using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Rake : Hazard
    {
        const float StepOnIt = 1.5f;

        const float HandleCooldown = 1.25f;

        [Header("Rake")]
        [Tooltip("Extra shove BACKWARDS on top of the ordinary trip, in boxes per second. Leave " +
                 "this at 0 and a rake throws exactly as hard as every other trip in the game, " +
                 "which is almost always what you want - one number, the wizard's own " +
                 "Ragdoll launch, then tunes them all together. Raise it only to turn one " +
                 "particular rake into something the player has to solve rather than survive: 3 " +
                 "costs them roughly an extra box of ground.")]
        [Min(0f)] public float extraKick = 0f;

        void Reset()
        {
            minimumSpeed = StepOnIt;
            rearmDelay = HandleCooldown;
            damage = 0;
        }

        protected override void Affect(PlayerLogic wizard)
        {
            int back = -wizard.movement.TravelDirection;

            if (!wizard.Trip(back))
                return;

            if (extraKick > 0f)
                wizard.Shove(new Vector2(back * extraKick, 0f), 0f);
        }
    }
}
