using FallingWizard.Core;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Collider2D))]
    public class AbilityShrine : PlayerTrigger
    {
        [Header("Shrine")]
        [Tooltip("The spell taught here, free and for good. Most spells should be bought with " +
                 "Wisps at the skill screen - a shrine is for the one you want every player to " +
                 "meet, whether or not they went looking.")]
        public Ability ability;

        [Tooltip("Put it straight into the first empty slot, so it can be used the moment it is " +
                 "found. With all four slots full it is learned but left on the bench.")]
        public bool equipOnPickup = true;

        [Tooltip("Wear the spell's own icon in the editor, so a shrine is recognisable at a glance.")]
        public bool showAbilityIcon = true;

        void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
            PullArt();
        }

        void OnValidate() => PullArt();

        void Awake()
        {
            if (ability == null)
            {
                Debug.LogWarning("This shrine has no spell in it, so walking into it does " +
                                 "nothing. Give it one or delete it.", this);
                return;
            }

            if (Progress.Owns(ability.Key))
                Destroy(gameObject);
        }

        protected override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (ability == null || Progress.Owns(ability.Key))
                return;

            Progress.Grant(ability.Key);

            if (equipOnPickup)
            {
                int slot = Progress.FirstEmptySlot();

                if (slot >= 0)
                    Progress.Equip(slot, ability.Key);
            }

            wizard.Logic.spellbook.Reload();

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
