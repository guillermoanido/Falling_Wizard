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

        [Tooltip("Sideways scatter away from the slime, in boxes per second.")]
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
            float away = Mathf.Sign(wizard.movement.Position.x - transform.position.x);

            if (Mathf.Approximately(away, 0f))
                away = 1f;

            wizard.Bounce(bounceHeight, away * shove, resetsFall);

            if (ragdolls)
                wizard.Trip();
        }
    }
}
