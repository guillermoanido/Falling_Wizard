using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
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
