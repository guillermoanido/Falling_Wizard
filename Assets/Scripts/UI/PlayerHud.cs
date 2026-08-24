using System.Collections.Generic;
using FallingWizard.Player;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class PlayerHud : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("Hearts are laid out under here, left to right.")]
        public RectTransform heartRow;

        [Tooltip("One heart, copied once per point of health. Restyle this and they all follow.")]
        public Image heartTemplate;

        [Tooltip("Spell slots are laid out under here, in spellbook order.")]
        public RectTransform spellBar;

        [Tooltip("One spell slot, copied once per spell. Restyle this and they all follow.")]
        public HudSlot slotTemplate;

        [Header("Hearts")]
        public Color fullHeart = new Color(0.85f, 0.22f, 0.30f);
        public Color emptyHeart = new Color(0.20f, 0.16f, 0.22f);

        [Header("Spells")]
        [Tooltip("Show a dimmed slot for spells not learned yet, so the bar never shifts around.")]
        public bool showLockedSlots = true;

        [Tooltip("Print the button under each spell. It follows whichever device is in use, so it " +
                 "reads E on a keyboard and X on a pad without anyone touching a setting.")]
        public bool showButtons = true;

        public Color lockedTint = new Color(1f, 1f, 1f, 0.18f);
        public Color readyTint = Color.white;
        public Color notReadyTint = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("Wipe drawn over a spell while it is running or cooling down.")]
        public Color chargeTint = new Color(0.25f, 0.65f, 1f, 0.55f);

        readonly List<Image> hearts = new List<Image>();
        readonly List<HudSlot> slots = new List<HudSlot>();

        PlayerCharacter bound;
        int builtHearts = -1;

        void Awake()
        {
            if (heartTemplate != null)
                heartTemplate.gameObject.SetActive(false);

            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);

            if (heartRow == null || heartTemplate == null || spellBar == null || slotTemplate == null)
                Debug.LogError("The HUD is missing part of its rig. Run " +
                               "Tools > Falling Wizard > Add HUD To Open Scene to build it.", this);
        }

        void OnEnable() => Core.Controls.SchemeChanged += RefreshButtons;

        void OnDisable() => Core.Controls.SchemeChanged -= RefreshButtons;

        void LateUpdate()
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != bound)
            {
                bound = wizard;
                builtHearts = -1;

                if (bound != null)
                {
                    BuildSpellBar();
                    RefreshButtons();
                }
            }

            bool showing = bound != null;

            if (heartRow != null)
                heartRow.gameObject.SetActive(showing);

            if (spellBar != null)
                spellBar.gameObject.SetActive(showing);

            if (!showing)
                return;

            RefreshHearts();
            RefreshSpells();
        }

        void RefreshHearts()
        {
            if (heartRow == null || heartTemplate == null)
                return;

            PlayerLogic.Health health = bound.Logic.health;

            if (builtHearts != health.Max)
            {
                foreach (Image heart in hearts)
                    Destroy(heart.gameObject);

                hearts.Clear();

                for (int i = 0; i < health.Max; i++)
                {
                    Image heart = Instantiate(heartTemplate, heartRow);
                    heart.gameObject.name = $"Heart {i + 1}";
                    heart.gameObject.SetActive(true);
                    hearts.Add(heart);
                }

                builtHearts = health.Max;
            }

            for (int i = 0; i < hearts.Count; i++)
                hearts[i].color = i < health.Current ? fullHeart : emptyHeart;
        }

        void BuildSpellBar()
        {
            if (spellBar == null || slotTemplate == null)
                return;

            foreach (HudSlot slot in slots)
                Destroy(slot.gameObject);

            slots.Clear();

            IReadOnlyList<PlayerLogic.Spellbook.Slot> book = bound.Logic.spellbook.Slots;

            for (int i = 0; i < book.Count; i++)
            {
                HudSlot slot = Instantiate(slotTemplate, spellBar);
                Ability spell = book[i].Ability;

                slot.gameObject.name = spell != null ? spell.displayName : $"Spell {i + 1}";
                slot.gameObject.SetActive(true);
                slots.Add(slot);
            }
        }

        void RefreshSpells()
        {
            IReadOnlyList<PlayerLogic.Spellbook.Slot> book = bound.Logic.spellbook.Slots;

            for (int i = 0; i < slots.Count && i < book.Count; i++)
                slots[i].Show(book[i], this);
        }

        void RefreshButtons()
        {
            if (bound == null)
                return;

            IReadOnlyList<PlayerLogic.Spellbook.Slot> book = bound.Logic.spellbook.Slots;

            for (int i = 0; i < slots.Count && i < book.Count; i++)
                slots[i].ShowButton(book[i], showButtons);
        }
    }
}
