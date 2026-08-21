using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Land on it and you are thrown back into the air, tumbling. Put it on the Hazard layer, not
    // Ground - if the ground check could see it, the wizard would be billed fall damage for
    // arriving before it ever got the chance to bounce them.
    public class Slime : Hazard
    {
        [Header("Bounce")]
        [Tooltip("How high the bounce throws the wizard, in boxes. Worked out from gravity, so " +
                 "3 really is three boxes.")]
        [Min(0f)] public float bounceHeight = 3f;

        [Tooltip("Sideways push as they launch, in boxes per second, carrying them ONWARD the " +
                 "way they were already going. Bouncing them away from the slime instead would " +
                 "mean the same jump throws you a different way depending on where you landed.")]
        public float shove = 2f;

        [Tooltip("Send the wizard tumbling as well as up.")]
        public bool ragdolls = true;

        [Tooltip("Count the bounce as a fresh start for fall damage. Leave this on, or the next " +
                 "real landing charges for the whole flight.")]
        public bool resetsFall = true;

        void Reset()
        {
            rearmDelay = 0.25f;
            damage = 0;

            // Must be a trigger. Solid plus the Hazard layer is the one combination that soft
            // locks the wizard: they come to rest on it while the ground check, which only sees
            // the Ground layer, still reports them airborne - so they can never jump or get up.
            GetComponent<Collider2D>().isTrigger = true;
        }

        protected override void Affect(PlayerLogic wizard)
        {
            // Which way they arrived, measured before the impact stopped them.
            int onward = wizard.movement.TravelDirection;

            // Trip first, then fling. A tumble kicks the wizard downward as it starts, so
            // bouncing first would just be undone - the slime would throw them into the floor.
            if (ragdolls)
                wizard.Trip();

            wizard.Bounce(bounceHeight, onward * shove, resetsFall);
        }
    }
}
