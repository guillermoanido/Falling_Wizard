using FallingWizard.Core;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    // Walk into it and the spell is yours for good. This replaces the old PowerUpPickup: there
    // is one field to set, and the icon, the name and the sparkle all come from the spell asset,
    // so a shrine is never out of step with what it grants.
    [RequireComponent(typeof(Collider2D))]
    public class AbilityShrine : PlayerTrigger
    {
        [Header("Shrine")]
        [Tooltip("The spell learned here. It must also be listed in the spellbook, or it has no " +
                 "place on the bar and cannot be learned.")]
        public Ability ability;

        [Tooltip("Wear the spell's own icon in the editor, so a shrine is recognisable at a glance.")]
        public bool showAbilityIcon = true;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            PullArt();
        }

        void OnValidate() => PullArt();

        // Permanent means permanent: a shrine already drained does not come back when the level
        // reloads after a death.
        void Awake()
        {
            if (ability != null && Progress.Knows(ability.Key))
                Destroy(gameObject);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (ability == null || !wizard.Logic.spellbook.Learn(ability))
                return;

            if (ability.collectedEffect != null)
                Instantiate(ability.collectedEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }

        void PullArt()
        {
            if (!showAbilityIcon || ability == null)
                return;

            var art = GetComponentInChildren<SpriteRenderer>();

            if (art != null && ability.icon != null)
                art.sprite = ability.icon;
        }
    }
}
