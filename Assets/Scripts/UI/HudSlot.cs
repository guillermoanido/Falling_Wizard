using FallingWizard.Player;
using TMPro;
using UnityEngine;
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

        public void Show(PlayerLogic.Spellbook.Slot spell, PlayerHud hud)
        {
            bool known = spell.Owned && spell.Ability != null;

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
            button.enabled = wanted;

            // Asked for by device, so it reads E on a keyboard and X on a pad. Never both.
            button.text = wanted ? Core.Controls.Glyph(spell.Action) : string.Empty;
        }
    }
}
