using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // A volume that pushes the wizard while they are inside it. One component covers a gale from
    // either side, an updraught and a downdraught - it is just a vector.
    //
    // Named WindZone2D because UnityEngine.WindZone already exists and would shadow it.
    public class WindZone2D : Hazard
    {
        [Header("Wind")]
        [Tooltip("Push in boxes per second. (-6,0) is a hard leftward gale, (0,4) an updraught. " +
                 "Compare against a run of 6 to judge how hard it fights the player.")]
        public Vector2 push = new Vector2(-4f, 0f);

        [Tooltip("How quickly the wind takes hold once you step in, in boxes per second squared.")]
        [Min(0f)] public float rampup = 20f;

        [Tooltip("How much of it is felt with both feet on the ground. 0 means it only pushes " +
                 "you while you are in the air.")]
        [Range(0f, 1f)] public float groundScale = 0.35f;

        // Wind is not a hit, it is a place you are standing.
        protected override bool Continuous => true;

        void Reset()
        {
            rearmDelay = 0f;
            damage = 0;
            GetComponent<Collider2D>().isTrigger = true;
        }

        // Walking in does nothing on its own; being in it is the whole effect.
        protected override void Affect(PlayerLogic wizard) { }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!Allowed(wizard))
                return;

            wizard.Logic.Push(push, rampup, groundScale);
        }

        void OnDrawGizmosSelected()
        {
            var origin = (Vector2)transform.position;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin, origin + push * 0.25f);
        }
    }
}
