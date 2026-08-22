using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Touch it and this becomes where you come back from. It banks the spells you have learned so
    // far as well as the spot, so dying costs you whatever you picked up since - and the shrine
    // that granted it is standing there again.
    //
    // Not a Hazard: it has no speed gate, no re-arm and nothing to be immune to. It just shares
    // the same contact plumbing, which is what stops the staff's own trigger firing it twice.
    [RequireComponent(typeof(Collider2D))]
    public class Checkpoint : PlayerTrigger
    {
        [Header("Checkpoint")]
        [Tooltip("Where the wizard reappears, relative to this object. Lift it clear of the " +
                 "floor so they do not respawn inside it.")]
        public Vector2 respawnOffset = new Vector2(0f, 0.5f);

        [Tooltip("Optional prefab spawned when this checkpoint is first reached.")]
        public GameObject reachedEffect;

        [Header("Look")]
        [Tooltip("Tinted to show which checkpoint is the live one. Empty uses the first sprite " +
                 "found underneath.")]
        public SpriteRenderer visual;

        public Color dormant = new Color(0.45f, 0.45f, 0.52f);
        public Color active = new Color(0.98f, 0.86f, 0.42f);

        public Vector2 RespawnPoint => (Vector2)transform.position + respawnOffset;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void Awake()
        {
            if (visual == null)
                visual = GetComponentInChildren<SpriteRenderer>();

            // Read the lit state back from Progress rather than remembering which one was last
            // touched: the level reloads on every death, so anything held in a static would be
            // pointing at a destroyed object by the time it mattered.
            Tint(IsLive);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = active;
            Gizmos.DrawWireSphere(RespawnPoint, 0.25f);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (IsLive)
                return;

            Progress.MarkCheckpoint(RespawnPoint);

            foreach (Checkpoint other in FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
                other.Tint(other == this);

            if (reachedEffect != null)
                Instantiate(reachedEffect, transform.position, Quaternion.identity);
        }

        bool IsLive =>
            Progress.CheckpointIsHere &&
            (Progress.CheckpointPoint - RespawnPoint).sqrMagnitude < 0.0001f;

        void Tint(bool lit)
        {
            if (visual != null)
                visual.color = lit ? active : dormant;
        }
    }
}
