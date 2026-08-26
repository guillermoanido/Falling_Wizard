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
        // Sized so eight spells still fit a 1080-tall canvas without a scroll view.
        // Past that the panel starts running off the bottom and this wants a real one.
        const float PanelWidth = 1180f;
        const float RowHeight = 72f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float SlotSize = 92f;

        AbilityBook book;
        Action dive;

        RectTransform body;

        public static SkillScreen Open(Action onDive)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Skill Screen", 220);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

            screen.dive = onDive;
            screen.book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);
            screen.Build();

            return screen;
        }

        void Build()
        {
            Ui.Shroud(transform);

            body = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth, 24f, 8f);

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

            Ui.Label("What you carry down", body, 44f, PanelWidth - 72f, 52f);

            TextMeshProUGUI purse = Ui.Label($"{Progress.Wisps} wisps", body, 28f,
                PanelWidth - 72f, 36f);
            purse.color = Ui.Wisp;

            DrawSlots();

            if (book == null)
            {
                Ui.Label("No spellbook found at Assets/Resources/Spellbook.asset.", body, 26f,
                    PanelWidth - 72f, 44f).color = Ui.Warning;
            }
            else
            {
                foreach (Ability spell in book.spells)
                    if (spell != null)
                        DrawSpell(spell);
            }

            Ui.CreateButton("Descend", body, 420f, 58f, 28f)
                .onClick.AddListener(() =>
                {
                    Screens.Release();
                    Destroy(gameObject);
                    dive?.Invoke();
                });
        }

        void DrawSlots()
        {
            RectTransform strip = Ui.Row("Slots", body, PanelWidth - 72f, SlotSize + 30f, 16f,
                TextAnchor.MiddleCenter);

            for (int i = 0; i < Progress.SlotCount; i++)
            {
                Ability held = book != null ? book.Find(Progress.EquippedIn(i)) : null;
                bool locked = held != null && held.locked;

                RectTransform cell = Ui.Column($"Slot {i + 1}", strip, SlotSize, 4f,
                    TextAnchor.UpperCenter);
                Ui.SetSize(cell.gameObject, SlotSize, SlotSize + 30f);

                Image plate = Ui.Plate("Plate", cell, held != null ? Ui.CardLit : Ui.Card,
                    SlotSize, SlotSize);

                if (held != null && held.icon != null)
                    Ui.Icon(plate.transform, held.icon, IconSize, Color.white);
                else
                    Ui.Label(Glyph(i), plate.transform, 34f, SlotSize, SlotSize).color = Ui.FadedInk;

                TextMeshProUGUI caption = Ui.Label(
                    held != null ? $"{Glyph(i)}  {held.displayName}" : Glyph(i), cell, 17f,
                    SlotSize, 26f);

                caption.color = locked ? Ui.Warning : held != null ? Ui.Ink : Ui.FadedInk;
            }
        }

        void DrawSpell(Ability spell)
        {
            bool owned = Progress.Owns(spell.Key);
            int slot = Progress.SlotHolding(spell.Key);
            bool equipped = slot >= 0;

            RectTransform row = Ui.Row(spell.displayName, body, PanelWidth - 72f, RowHeight, 20f);

            Image card = Ui.Plate("Card", row, equipped ? Ui.CardLit : Ui.Card,
                PanelWidth - 72f, RowHeight);

            var inner = card.gameObject.AddComponent<HorizontalLayoutGroup>();
            inner.childAlignment = TextAnchor.MiddleLeft;
            inner.spacing = 20f;
            inner.padding = new RectOffset(18, 18, 10, 10);
            inner.childControlWidth = true;
            inner.childControlHeight = true;
            inner.childForceExpandWidth = false;
            inner.childForceExpandHeight = false;

            Ui.Icon(card.transform, spell.icon, IconSize,
                owned ? Color.white : new Color(1f, 1f, 1f, 0.3f));

            float wordsWidth = PanelWidth - 72f - IconSize - ActionWidth - 100f;

            RectTransform words = Ui.Column("Words", card.transform, wordsWidth, 2f,
                TextAnchor.MiddleLeft);
            Ui.SetSize(words.gameObject, wordsWidth, RowHeight - 20f);

            TextMeshProUGUI name = Ui.Label(spell.displayName, words, 24f, wordsWidth, 28f,
                TextAlignmentOptions.Left);
            name.color = owned ? Ui.Ink : Ui.FadedInk;

            string blurb = spell.IsPassive && !string.IsNullOrEmpty(spell.description)
                ? spell.description + "  (always on)"
                : spell.description;

            Ui.Label(blurb, words, 19f, wordsWidth, 32f, TextAlignmentOptions.TopLeft)
                .color = Ui.FadedInk;

            DrawAction(spell, card.transform, owned, equipped, slot);
        }

        void DrawAction(Ability spell, Transform parent, bool owned, bool equipped, int slot)
        {
            if (owned && spell.locked)
            {
                Ui.Label($"always {Glyph(slot)}", parent, 24f, ActionWidth, 44f).color = Ui.Warning;
                return;
            }

            if (!owned)
            {
                bool affordable = Progress.CanAfford(spell.cost);
                string text = affordable
                    ? $"Learn - {spell.cost}"
                    : $"{spell.cost} wisps";

                Button buy = Ui.CreateButton(text, parent, ActionWidth, 46f, 22f);
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
                Button drop = Ui.CreateButton($"{Glyph(slot)} - take out", parent, ActionWidth, 46f, 20f);
                drop.onClick.AddListener(() =>
                {
                    Progress.Equip(slot, string.Empty);
                    Apply();
                });

                return;
            }

            int empty = Progress.FirstEmptySlot();

            Button carry = Ui.CreateButton(empty >= 0 ? "Bring it" : "No slot free",
                parent, ActionWidth, 46f, 22f);

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
