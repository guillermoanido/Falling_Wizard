using FallingWizard.Core;
using FallingWizard.Player;
using FallingWizard.UI;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public class RestSite : PlayerTrigger
    {
        // Two respawn points closer together than a hundredth of a box are the
        // same one. These are matched by position because the level is rebuilt
        // on every death, so an id kept in a static would be pointing at a
        // destroyed object by the time it mattered.
        const float SamePlace = 0.01f;

        const float MarkerRadius = 0.25f;

        [Header("Rest")]
        [Tooltip("Where the wizard reappears after a fall, relative to this object. Lift it clear " +
                 "of the floor so they do not come back inside it.")]
        public Vector2 respawnOffset = new Vector2(0f, 0.5f);

        [Tooltip("What the screen is headed with.")]
        public string title = "A place to rest";

        [Tooltip("The line under the heading. Say where this leads.")]
        public string blurb = "Further down, or back the way you came.";

        [Tooltip("Optional prefab spawned the first time this is reached.")]
        public GameObject reachedEffect;

        [Header("Look")]
        [Tooltip("Tinted to show this one has been reached. Empty uses the first sprite found " +
                 "underneath.")]
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
            Gizmos.DrawWireSphere(RespawnPoint, MarkerRadius);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Screens.ModalOpen)
                return;

            bool first = !IsLive;

            Progress.MarkCheckpoint(RespawnPoint);

            foreach (RestSite other in FindObjectsByType<RestSite>(FindObjectsSortMode.None))
                other.Tint(other == this);

            if (first && reachedEffect != null)
                Instantiate(reachedEffect, transform.position, Quaternion.identity);

            Offer(wizard);
        }

        void Offer(PlayerCharacter wizard)
        {
            PlayerLogic.Health health = wizard.Logic.health;

            ChoiceScreen screen = ChoiceScreen.Open(title, blurb);

            screen.Status($"Carrying {Progress.CarriedWisps} wisps    " +
                          $"{Progress.Wisps} already banked    " +
                          $"{health.Current}/{health.Max} hearts");

            screen.Choice("Rest, then press on", () =>
            {
                wizard.Logic.RestoreHealth();
                wizard.Logic.spellbook.ResetForRun();
                screen.Close();
            });

            screen.Choice($"Turn back and bank {Progress.CarriedWisps} wisps", () =>
                screen.CloseThen(() =>
                {
                    Progress.EndRun();
                    SkillScreen.Open(Game.LoadFirstLevel);
                }));
        }

        bool IsLive =>
            Progress.CheckpointIsHere &&
            (Progress.CheckpointPoint - RespawnPoint).sqrMagnitude < SamePlace * SamePlace;

        void Tint(bool lit)
        {
            if (visual != null)
                visual.color = lit ? active : dormant;
        }
    }
}
