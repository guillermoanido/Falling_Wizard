using System;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    // One spell on the bar. Its own component rather than something the HUD builds, so the whole
    // slot - frame, icon, wipe, button - is an ordinary bit of UI you can restyle in the editor,
    // and PlayerHud only ever fills it in.
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

            // Every frame, because learning a spell flips Owned on a slot that already exists -
            // nothing rebuilds the bar, so the button has to notice for itself. The check inside
            // makes that nearly free.
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

            // Whichever clock is running: the spell's own lit window while it lasts, then the
            // cooldown until it can be cast again.
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

            // Only three things can change what the button says: whether it should be there at
            // all, which action it is, and which device is in hand. Working out the glyph builds
            // a string, so do it when one of those moves and not sixty times a second.
            if (everShown && wanted == shownWanted && spell.Action == shownAction && scheme == shownScheme)
                return;

            everShown = true;
            shownWanted = wanted;
            shownAction = spell.Action;
            shownScheme = scheme;

            button.enabled = wanted;

            // Asked for by device, so it reads E on a keyboard and X on a pad. Never both.
            button.text = wanted ? Core.Controls.Glyph(spell.Action) : string.Empty;
        }
    }
}
