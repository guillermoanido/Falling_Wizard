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

        [NonSerialized] InputAction shownAction;
        [NonSerialized] string shownScheme;
        [NonSerialized] bool shownWanted;
        [NonSerialized] bool everShown;

        public void Show(PlayerLogic.Spellbook.Slot spell, PlayerHud hud)
        {
            bool known = spell.Owned && spell.Ability != null;

            ShowButton(spell, hud.showButtons);

            gameObject.SetActive(known || hud.showLockedSlots);

            if (!gameObject.activeSelf)
                return;

            if (icon != null)
            {
                if (spell.Ability != null && spell.Ability.icon != null)
                    icon.sprite = spell.Ability.icon;

                icon.color = !known ? hud.lockedTint
                           : spell.IsReady ? hud.readyTint
                           : hud.notReadyTint;
            }

            if (charge == null)
                return;

            float fill = spell.IsLit ? spell.LitProgress
                       : spell.CooldownLeft > 0f ? spell.CooldownProgress
                       : 0f;

            charge.color = hud.chargeTint;
            charge.enabled = known && fill > 0f;
            charge.fillAmount = fill;
        }

        public void ShowButton(PlayerLogic.Spellbook.Slot spell, bool show)
        {
            if (button == null)
                return;

            bool wanted = show && spell.Owned && spell.Action != null;
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
