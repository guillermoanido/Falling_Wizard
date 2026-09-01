using System;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class HudSlot : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The spell's own icon goes here.")]
        public Image icon;

        [Tooltip("Drawn over the icon as the spell runs and cools down. Must be an Image set to " +
                 "Filled, or it will sit there at full and never move.")]
        public Image charge;

        [Tooltip("The button to press. Hidden for passive spells, which have no button.")]
        public TextMeshProUGUI button;

        [Tooltip("Casts left before the next rest, for a spell that only has so many. Blank for " +
                 "everything else.")]
        public TextMeshProUGUI uses;

        [NonSerialized] InputAction shownAction;
        [NonSerialized] string shownScheme;
        [NonSerialized] bool shownWanted;
        [NonSerialized] bool everShown;

        public void Show(PlayerLogic.Spellbook.Slot spell, PlayerHud hud, PlayerLogic wizard)
        {
            bool filled = spell.Ability != null;

            ShowButton(spell, hud.showButtons);

            gameObject.SetActive(filled || hud.showEmptySlots);

            if (!gameObject.activeSelf)
                return;

            if (icon != null)
            {
                // The spell decides what it looks like right now, not the slot. Telekinesis
                // answers with whatever it is carrying, which is the only way to tell a stored
                // slime from a stored rock without opening a menu.
                icon.sprite = filled ? spell.Ability.IconFor(wizard) : hud.emptySlotIcon;

                Color state = !filled ? hud.emptyTint
                            : spell.IsReady ? hud.readyTint
                            : hud.notReadyTint;

                // MULTIPLIED, not assigned: assigning would throw away the ready/cooling state
                // and every slot would read as available.
                icon.color = filled ? state * spell.Ability.IconTintFor(wizard) : state;
            }

            if (uses != null)
            {
                bool counted = filled && spell.Ability.usesPerLevel > 0;

                uses.enabled = counted;
                uses.text = counted ? spell.UsesLeft.ToString() : string.Empty;
            }

            if (charge == null)
                return;

            // A spell winding up owns the meter outright - below zero means it has nothing of
            // its own to show and the slot falls back to its lit window and its cooldown.
            float own = filled ? spell.Ability.ChargeFor(wizard) : -1f;
            bool winding = own >= 0f;

            float fill = winding ? own
                       : spell.IsLit ? spell.LitProgress
                       : spell.CooldownLeft > 0f ? spell.CooldownProgress
                       : 0f;

            charge.color = winding ? hud.windupTint : hud.chargeTint;
            charge.enabled = filled && fill > 0f;
            charge.fillAmount = fill;
        }

        public void ShowButton(PlayerLogic.Spellbook.Slot spell, bool show)
        {
            if (button == null)
                return;

            bool passive = spell.Ability != null && spell.Ability.IsPassive;
            bool wanted = show && spell.Action != null && !passive;
            string scheme = Core.Controls.Scheme;

            if (everShown && wanted == shownWanted && spell.Action == shownAction && scheme == shownScheme)
                return;

            everShown = true;
            shownWanted = wanted;
            shownAction = spell.Action;
            shownScheme = scheme;

            button.enabled = wanted;

            button.text = wanted ? Core.Controls.Glyph(spell.Action) : string.Empty;
        }
    }
}
