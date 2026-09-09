using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class Rake : Hazard
    {
        const float HandleCooldown = 1.25f;

        const float ReversalKick = 4f;

        const float OffCentre = 0.05f;

        public enum Aim
        {
            Auto,
            Left,
            Right,
        }

        [Header("Rake")]
        [Tooltip("Which way this rake throws the wizard. Auto reads the prefab's own flip - the " +
                 "side its trigger sits on, then the art's mirroring - so flipping a copy is all " +
                 "it takes to make the mirrored version. Set it explicitly to override that.")]
        public Aim aim = Aim.Auto;

        [Tooltip("Shove along the aim, in boxes per second, on top of the ordinary trip. This is " +
                 "what makes a rake a launcher rather than a stumble.")]
        [Min(0f)] public float extraKick = 5f;

        [Tooltip("Extra lift on top of the trip's own, in boxes per second. Raise it for a rake " +
                 "that should pop the wizard up and over something rather than skim them along.")]
        [Min(0f)] public float extraLift = 0f;

        void Reset()
        {
            minimumSpeed = 0f;
            rearmDelay = HandleCooldown;
            damage = 0;
            affectsRagdolled = true;
            ignoresWalking = true;
        }

        protected override void Affect(PlayerLogic wizard)
        {
            int way = Aimed();

            if (wizard.Trip(way))
            {
                if (extraKick > 0f || extraLift > 0f)
                    wizard.Shove(new Vector2(way * extraKick, extraLift), 0f);

                return;
            }

            if (wizard.State != PlayerState.Ragdoll)
                return;

            float carried = wizard.movement.Velocity.x;
            float target = way * Mathf.Max(extraKick, ReversalKick);

            wizard.Shove(new Vector2(target - carried, extraLift), 0f);
        }

        int Aimed()
        {
            if (aim == Aim.Left)
                return -1;

            if (aim == Aim.Right)
                return 1;

            var hitbox = GetComponent<Collider2D>();

            if (hitbox != null && Mathf.Abs(hitbox.offset.x) > OffCentre)
                return hitbox.offset.x < 0f ? -1 : 1;

            return Mirrored() ? -1 : 1;
        }

        bool Mirrored()
        {
            if (transform.lossyScale.x < 0f)
                return true;

            foreach (Transform child in transform)
                if (child.localScale.x < 0f)
                    return true;

            return false;
        }
    }
}
