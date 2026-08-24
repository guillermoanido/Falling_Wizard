using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
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
        }

        protected override void Affect(PlayerLogic wizard)
        {
            int onward = wizard.movement.TravelDirection;

            if (ragdolls)
                wizard.Trip();

            wizard.Bounce(bounceHeight, onward * shove, resetsFall);
        }
    }
}
