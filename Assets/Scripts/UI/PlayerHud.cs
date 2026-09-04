using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using TMPro;
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

        [Tooltip("One spell slot, copied once per button. Restyle this and they all follow.")]
        public HudSlot slotTemplate;

        [Tooltip("Reads out what is being risked and what is safe. Leave it empty and one is " +
                 "built in the top right corner on its own, so a scene needs no wiring to show " +
                 "the number that matters most.")]
        public TextMeshProUGUI wispLabel;

        [Tooltip("Do not build that corner label. For a scene that shows the count some other " +
                 "way, or one that would rather not show it at all.")]
        public bool hideWispCount = false;

        [Header("Hearts")]
        public Color fullHeart = new Color(0.85f, 0.22f, 0.30f);
        public Color emptyHeart = new Color(0.20f, 0.16f, 0.22f);

        [Header("Wisps")]
        [Tooltip("How the counter reads, for a scene that wants its own wording. {0} is what you " +
                 "are carrying and would lose, {1} is what is already safely banked. " +
                 "LEAVE IT " +
                 "EMPTY, which is what you want almost always: the counter then uses the " +
                 "translated line and follows the player's language. Anything typed here is used " +
                 "exactly as typed, in every language.")]
        public string wispFormat = "";

        [Header("Spells")]
        [Tooltip("Draw a dimmed slot for an empty button, so the bar never shifts around and the " +
                 "player can see there is room for more.")]
        public bool showEmptySlots = true;

        [Tooltip("Print the button under each spell. It follows whichever device is in use, so it " +
                 "reads E on a keyboard and X on a pad without anyone touching a setting.")]
        public bool showButtons = true;

        [Tooltip("Optional art for a button with nothing in it. Empty draws a plain box.")]
        public Sprite emptySlotIcon;

        public Color emptyTint = new Color(1f, 1f, 1f, 0.18f);
        public Color readyTint = Color.white;
        public Color notReadyTint = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("Wipe drawn over a spell while it is running or cooling down.")]
        public Color chargeTint = new Color(0.25f, 0.65f, 1f, 0.55f);

        [Tooltip("The same wipe, while a spell is being WOUND UP rather than recovering. A " +
                 "different colour because the two mean opposite things: one is filling up to " +
                 "something you choose to release, the other is draining back to ready.")]
        public Color windupTint = new Color(1f, 0.85f, 0.35f, 0.7f);

        // The corner counter, in reference-resolution pixels, when no label was wired by hand.
        static readonly Vector2 WispCorner = new Vector2(-24f, -20f);
        static readonly Vector2 WispSize = new Vector2(360f, 40f);
        const float WispFontSize = 26f;
        static readonly Color WispColour = new Color(0.55f, 0.85f, 1f);

        readonly List<Image> hearts = new List<Image>();
        readonly List<HudSlot> slots = new List<HudSlot>();

        PlayerCharacter bound;
        int builtHearts = -1;
        int builtVersion = -1;

        void Awake()
        {
            if (heartTemplate != null)
                heartTemplate.gameObject.SetActive(false);

            if (slotTemplate != null)
                slotTemplate.gameObject.SetActive(false);

            if (wispLabel == null && !hideWispCount)
                wispLabel = BuildWispLabel();

            if (heartRow == null || heartTemplate == null || spellBar == null || slotTemplate == null)
                Debug.LogError("The HUD is missing part of its rig. It wants a heart row with one " +
                               "heart image to copy, and a spell bar with one HudSlot to copy. " +
                               "Both templates stay switched off.", this);
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
                builtVersion = -1;
            }

            bool showing = bound != null;

            if (heartRow != null)
                heartRow.gameObject.SetActive(showing);

            if (spellBar != null)
                spellBar.gameObject.SetActive(showing);

            if (wispLabel != null)
            {
                wispLabel.enabled = showing;

                // No caching and no subscription to Loc.Changed: this already runs every frame
                // and rebuilds the string every frame, so it picks a language change up on its own.
                if (showing)
                    wispLabel.text = string.IsNullOrEmpty(wispFormat)
                        ? Loc.Format(Loc.Keys.HudWisps, Progress.CarriedWisps, Progress.Wisps)
                        : string.Format(wispFormat, Progress.CarriedWisps, Progress.Wisps);
            }

            if (!showing)
                return;

            if (builtVersion != bound.Logic.spellbook.Version)
            {
                builtVersion = bound.Logic.spellbook.Version;
                BuildSpellBar();
                RefreshButtons();
            }

            RefreshHearts();
            RefreshSpells();
        }

        TextMeshProUGUI BuildWispLabel()
        {
            var canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
                return null;

            var go = new GameObject("Wisp Count", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = WispCorner;
            rect.sizeDelta = WispSize;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.fontSize = WispFontSize;
            label.color = WispColour;
            label.alignment = TMPro.TextAlignmentOptions.Right;
            label.raycastTarget = false;

            return label;
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
                slots[i].Show(book[i], this, bound.Logic);
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
