using System;
using FallingWizard.Core;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class SkillScreen : MonoBehaviour
    {
        // Above the Pause Menu (100) and above a rest or death screen (200), since this is
        // what those open on top of themselves.
        const int SortingOrder = 220;

        // Sized so eight spells still fit a 1080-tall canvas without a scroll view. Past that the
        // panel starts running off the bottom and this wants a real one.
        const float PanelWidth = 1180f;
        const float PanelPadding = 24f;
        const float PanelSpacing = 8f;

        const float RowHeight = 72f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float ActionHeight = 46f;
        const float ActionFontSize = 22f;
        const float SlotActionFontSize = 20f;
        const float SlotSize = 92f;

        // Room under a slot for the button glyph and whatever is sitting on it.
        const float SlotCaption = 30f;

        const float TitleSize = 44f;
        const float TitleHeight = 52f;
        const float PurseSize = 28f;
        const float PurseHeight = 36f;
        const float NoticeSize = 26f;
        const float NoticeHeight = 44f;

        const float NameSize = 24f;
        const float NameHeight = 28f;
        const float BlurbSize = 19f;
        const float BlurbHeight = 32f;

        const float GlyphSize = 34f;
        const float CaptionSize = 17f;
        const float CaptionHeight = 26f;

        const float DiveWidth = 420f;
        const float DiveHeight = 58f;
        const float DiveFontSize = 28f;

        const float CardPadding = 18f;
        const float CardTopPadding = 10f;
        const float CardSpacing = 20f;
        const float SlotSpacing = 16f;
        const float CellSpacing = 4f;
        const float WordSpacing = 2f;

        // What the icon, the action button, the card's own padding and the gaps between them take
        // out of a row. Whatever is left is where the words go.
        const float RowFurniture = IconSize + ActionWidth + CardPadding * 2f + CardSpacing * 2f;

        // The panel's content area, inside its padding.
        const float Inner = PanelWidth - PanelPadding * 2f;

        static readonly Color UnownedIcon = new Color(1f, 1f, 1f, 0.3f);

        AbilityBook book;
        Action dive;

        RectTransform body;

        public static SkillScreen Open(Action onDive)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Skill Screen", SortingOrder);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

            screen.dive = onDive;
            screen.book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);
            screen.Build();

            return screen;
        }

        void Build()
        {
            Ui.Shroud(transform);

            body = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth, PanelPadding, PanelSpacing);

            Redraw();
        }

        void Redraw()
        {
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                GameObject old = body.GetChild(i).gameObject;
                old.transform.SetParent(null, false);
                Destroy(old);
            }

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            Ui.Label("What you carry down", body, TitleSize, Inner, TitleHeight);

            TextMeshProUGUI purse = Ui.Label($"{Progress.Wisps} wisps", body, PurseSize,
                Inner, PurseHeight);
            purse.color = Ui.Wisp;

            DrawSlots();

            if (book == null)
            {
                Ui.Label("No spellbook found at Assets/Resources/Spellbook.asset.", body,
                    NoticeSize, Inner, NoticeHeight).color = Ui.Warning;
            }
            else
            {
                foreach (Ability spell in book.spells)
                    if (spell != null)
                        DrawSpell(spell);
            }

            Ui.CreateButton("Descend", body, DiveWidth, DiveHeight, DiveFontSize)
                .onClick.AddListener(() =>
                {
                    Screens.Release();
                    Destroy(gameObject);
                    dive?.Invoke();
                });
        }

        void DrawSlots()
        {
            RectTransform strip = Ui.Row("Slots", body, Inner, SlotSize + SlotCaption,
                SlotSpacing, TextAnchor.MiddleCenter);

            for (int i = 0; i < Progress.SlotCount; i++)
            {
                Ability held = book != null ? book.Find(Progress.EquippedIn(i)) : null;
                bool locked = held != null && held.locked;

                RectTransform cell = Ui.Column($"Slot {i + 1}", strip, SlotSize, CellSpacing,
                    TextAnchor.UpperCenter);
                Ui.SetSize(cell.gameObject, SlotSize, SlotSize + SlotCaption);

                Image plate = Ui.Plate("Plate", cell, held != null ? Ui.CardLit : Ui.Card,
                    SlotSize, SlotSize);

                if (held != null && held.icon != null)
                    Ui.Icon(plate.transform, held.icon, IconSize, Color.white);
                else
                    Ui.Label(Glyph(i), plate.transform, GlyphSize, SlotSize, SlotSize)
                        .color = Ui.FadedInk;

                TextMeshProUGUI caption = Ui.Label(
                    held != null ? $"{Glyph(i)}  {held.displayName}" : Glyph(i), cell,
                    CaptionSize, SlotSize, CaptionHeight);

                caption.color = locked ? Ui.Warning : held != null ? Ui.Ink : Ui.FadedInk;
            }
        }

        void DrawSpell(Ability spell)
        {
            bool owned = Progress.Owns(spell.Key);
            int slot = Progress.SlotHolding(spell.Key);
            bool equipped = slot >= 0;

            RectTransform row = Ui.Row(spell.displayName, body, Inner, RowHeight, CardSpacing);

            Image card = Ui.Plate("Card", row, equipped ? Ui.CardLit : Ui.Card, Inner, RowHeight);

            var inner = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            inner.childAlignment = TextAnchor.MiddleLeft;
            inner.spacing = CardSpacing;
            inner.padding = new RectOffset((int)CardPadding, (int)CardPadding,
                                           (int)CardTopPadding, (int)CardTopPadding);
            inner.childControlWidth = true;
            inner.childControlHeight = true;
            inner.childForceExpandWidth = false;
            inner.childForceExpandHeight = false;

            Ui.Icon(card.transform, spell.icon, IconSize, owned ? Color.white : UnownedIcon);

            float wordsWidth = Inner - RowFurniture;

            RectTransform words = Ui.Column("Words", card.transform, wordsWidth, WordSpacing,
                TextAnchor.MiddleLeft);
            Ui.SetSize(words.gameObject, wordsWidth, NameHeight + BlurbHeight);

            TextMeshProUGUI name = Ui.Label(spell.displayName, words, NameSize, wordsWidth,
                NameHeight, TextAlignmentOptions.Left);
            name.color = owned ? Ui.Ink : Ui.FadedInk;

            string blurb = spell.IsPassive && !string.IsNullOrEmpty(spell.description)
                ? spell.description + "  (always on)"
                : spell.description;

            Ui.Label(blurb, words, BlurbSize, wordsWidth, BlurbHeight,
                TextAlignmentOptions.TopLeft).color = Ui.FadedInk;

            DrawAction(spell, card.transform, owned, equipped, slot);
        }

        void DrawAction(Ability spell, Transform parent, bool owned, bool equipped, int slot)
        {
            if (owned && spell.locked)
            {
                Ui.Label($"always {Glyph(slot)}", parent, NameSize, ActionWidth, NoticeHeight)
                    .color = Ui.Warning;
                return;
            }

            if (!owned)
            {
                bool affordable = Progress.CanAfford(spell.cost);
                string text = affordable
                    ? $"Learn - {spell.cost}"
                    : $"{spell.cost} wisps";

                Button buy = Ui.CreateButton(text, parent, ActionWidth, ActionHeight, ActionFontSize);
                buy.interactable = affordable;

                if (affordable)
                    buy.onClick.AddListener(() =>
                    {
                        if (!Progress.Buy(spell.Key, spell.cost))
                            return;

                        int free = Progress.FirstEmptySlot();

                        if (free >= 0)
                            Progress.Equip(free, spell.Key);

                        Apply();
                    });

                return;
            }

            if (equipped)
            {
                Button drop = Ui.CreateButton($"{Glyph(slot)} - take out", parent, ActionWidth, ActionHeight, SlotActionFontSize);
                drop.onClick.AddListener(() =>
                {
                    Progress.Equip(slot, string.Empty);
                    Apply();
                });

                return;
            }

            int empty = Progress.FirstEmptySlot();

            Button carry = Ui.CreateButton(empty >= 0 ? "Bring it" : "No slot free",
                parent, ActionWidth, ActionHeight, ActionFontSize);

            carry.interactable = empty >= 0;

            if (empty >= 0)
                carry.onClick.AddListener(() =>
                {
                    Progress.Equip(empty, spell.Key);
                    Apply();
                });
        }

        void Apply()
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.Reload();

            Redraw();
        }

        static string Glyph(int slot)
        {
            if ((uint)slot >= PlayerLogic.Spellbook.SlotActions.Length)
                return string.Empty;

            return Controls.Glyph(Controls.Player(PlayerLogic.Spellbook.SlotActions[slot]));
        }
    }
}
