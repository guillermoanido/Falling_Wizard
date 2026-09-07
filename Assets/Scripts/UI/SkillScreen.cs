using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class SkillScreen : MonoBehaviour
    {
        const int SortingOrder = 220;

        const float PanelWidth = 1180f;
        const float PanelPadding = 24f;
        const float PanelSpacing = 8f;

        const float RowHeight = 84f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float ActionHeight = 46f;
        const float ActionFontSize = 22f;
        const float SlotSize = 92f;

        const float SlotCaption = 30f;

        const float TitleSize = 44f;
        const float TitleHeight = 52f;
        const float PurseSize = 28f;
        const float PurseHeight = 36f;
        const float HintSize = 20f;
        const float HintHeight = 28f;
        const float NoticeSize = 26f;
        const float NoticeHeight = 44f;

        const float NameSize = 24f;
        const float NameHeight = 28f;
        const float BlurbSize = 19f;
        const float BlurbHeight = 30f;

        const float GlyphSize = 34f;
        const float CaptionSize = 17f;
        const float CaptionHeight = 26f;

        const float PipSize = 12f;

        const float DiveWidth = 420f;
        const float DiveHeight = 58f;
        const float DiveFontSize = 28f;

        const float CardPadding = 18f;
        const float CardTopPadding = 8f;
        const float CardSpacing = 20f;
        const float SlotSpacing = 16f;
        const float CellSpacing = 4f;
        const float WordSpacing = 2f;

        const float RowFurniture = IconSize + ActionWidth + CardPadding * 2f + CardSpacing * 2f;

        const float Inner = PanelWidth - PanelPadding * 2f;

        static readonly Color UnownedIcon = new Color(1f, 1f, 1f, 0.3f);

        readonly Dictionary<GameObject, Ability> rows = new Dictionary<GameObject, Ability>();

        AbilityBook book;
        Action dive;
        Action closed;

        string diveKey = Loc.Keys.SkillDive;

        RectTransform body;
        InputAction[] slotKeys;

        string focusKey = string.Empty;

        [NonSerialized] int openedOn;

        public static SkillScreen Open(Action onDive, string labelKey = null) =>
            Raise(onDive, labelKey, null);

        static SkillScreen Raise(Action onDive, string labelKey, Action onClose)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Skill Screen", SortingOrder);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

            screen.dive = onDive;
            screen.diveKey = string.IsNullOrEmpty(labelKey) ? Loc.Keys.SkillDive : labelKey;
            screen.closed = onClose;
            screen.openedOn = Time.frameCount;
            screen.book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);
            screen.Build();

            return screen;
        }

        void Build()
        {
            Ui.Shroud(transform);

            body = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth, PanelPadding, PanelSpacing);

            slotKeys = new InputAction[PlayerLogic.Spellbook.SlotCount];

            for (int i = 0; i < slotKeys.Length; i++)
                slotKeys[i] = Controls.Player(PlayerLogic.Spellbook.SlotActions[i]);

            if (book != null)
                PlayerLogic.Spellbook.Seed(book);

            Core.Controls.SchemeChanged += Redraw;
            Loc.Changed += Redraw;

            Redraw();
        }

        void OnDestroy()
        {
            Core.Controls.SchemeChanged -= Redraw;
            Loc.Changed -= Redraw;
        }

        void Update()
        {
            if (Time.frameCount != openedOn &&
                (Core.Controls.PausePressed || Core.Controls.CancelPressed ||
                 Core.Controls.LoadoutPressed))
            {
                Leave();
                return;
            }

            Ability picked = Focused();

            if (picked == null || slotKeys == null)
                return;

            for (int i = 0; i < slotKeys.Length; i++)
                if (slotKeys[i] != null && slotKeys[i].WasPressedThisFrame())
                {
                    Assign(picked, i);
                    return;
                }
        }

        void Leave()
        {
            Screens.Release();

            Action after = dive;
            Action back = closed;

            Destroy(gameObject);

            after?.Invoke();
            back?.Invoke();
        }

        void Redraw()
        {
            rows.Clear();

            for (int i = body.childCount - 1; i >= 0; i--)
            {
                GameObject old = body.GetChild(i).gameObject;
                old.transform.SetParent(null, false);
                Destroy(old);
            }

            Ui.Label(Loc.Get(Loc.Keys.SkillTitle), body, TitleSize, Inner, TitleHeight);

            TextMeshProUGUI purse = Ui.Label(Loc.Format(Loc.Keys.SkillPurse, Progress.Wisps),
                body, PurseSize, Inner, PurseHeight);
            purse.color = Ui.Wisp;

            DrawSlots();
            DrawHint();

            GameObject first = null;

            if (book == null)
            {
                Ui.Label(Loc.Get(Loc.Keys.SkillNoBook), body,
                    NoticeSize, Inner, NoticeHeight).color = Ui.Warning;
            }
            else
            {
                foreach (Ability spell in book.spells)
                {
                    if (spell == null)
                        continue;

                    GameObject card = DrawSpell(spell);

                    if (first == null || spell.Key == focusKey)
                        first = card;
                }
            }

            Ui.CreateButton(Loc.Get(diveKey), body, DiveWidth, DiveHeight, DiveFontSize)
                .onClick.AddListener(Leave);

            Ui.Focus(first);
        }

        void DrawHint()
        {
            Ability picked = book != null ? book.Find(focusKey) : null;

            string words = picked == null
                ? Loc.Get(Loc.Keys.SkillHintPick)
                : Progress.Owns(picked.Key)
                    ? Loc.Format(Loc.Keys.SkillHintMove, picked.Name, Buttons())
                    : Loc.Format(Loc.Keys.SkillHintLocked, picked.Name);

            Ui.Label(words, body, HintSize, Inner, HintHeight).color = Ui.FadedInk;
        }

        string Buttons()
        {
            var free = new List<string>();

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
            {
                Ability held = book != null ? book.Find(Progress.EquippedIn(i)) : null;

                if (held == null || !held.locked)
                    free.Add(Glyph(i));
            }

            return string.Join(" ", free);
        }

        void DrawSlots()
        {
            RectTransform strip = Ui.Row("Slots", body, Inner, SlotSize + SlotCaption,
                SlotSpacing, TextAnchor.MiddleCenter);

            for (int i = 0; i < PlayerLogic.Spellbook.SlotCount; i++)
            {
                Ability held = book != null ? book.Find(Progress.EquippedIn(i)) : null;
                bool locked = held != null && held.locked;

                RectTransform cell = Ui.Column($"Slot {i + 1}", strip, SlotSize, CellSpacing,
                    TextAnchor.UpperCenter);
                Ui.SetSize(cell.gameObject, SlotSize, SlotSize + SlotCaption);

                Image plate = Ui.Plate("Plate", cell, held != null ? Ui.CardLit : Ui.Card,
                    SlotSize, SlotSize);

                int index = i;
                Button press = Ui.Pressable(plate);
                press.interactable = !locked;
                press.onClick.AddListener(() => Assign(Focused(), index));

                if (held != null && held.icon != null)
                    Ui.Icon(plate.transform, held.icon, IconSize, Color.white);
                else
                    Ui.Label(Glyph(i), plate.transform, GlyphSize, SlotSize, SlotSize)
                        .color = Ui.FadedInk;

                TextMeshProUGUI caption = Ui.Label(
                    held != null ? $"{Glyph(i)}  {held.Name}" : Glyph(i), cell,
                    CaptionSize, SlotSize, CaptionHeight);

                caption.color = locked ? Ui.Warning : held != null ? Ui.Ink : Ui.FadedInk;
            }
        }

        GameObject DrawSpell(Ability spell)
        {
            bool owned = Progress.Owns(spell.Key);
            int rank = Progress.Rank(spell.Key);
            int slot = Progress.SlotHolding(spell.Key);
            bool equipped = slot >= 0;

            RectTransform row = Ui.Row(spell.displayName, body, Inner, RowHeight, CardSpacing);

            Image card = Ui.Plate("Card", row, equipped ? Ui.CardLit : Ui.Card, Inner, RowHeight);

            Button press = Ui.Pressable(card);
            press.interactable = owned;
            press.onClick.AddListener(() => { focusKey = spell.Key; Redraw(); });

            rows[card.gameObject] = spell;

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
            Ui.SetSize(words.gameObject, wordsWidth, RowHeight - CardTopPadding * 2f);

            RectTransform heading = Ui.Row("Heading", words, wordsWidth, NameHeight, 10f);

            TextMeshProUGUI name = Ui.Label(spell.Name, heading, NameSize,
                wordsWidth - (spell.HasUpgrades ? 90f : 0f), NameHeight,
                TextAlignmentOptions.Left);
            name.color = owned ? Ui.Ink : Ui.FadedInk;

            if (spell.HasUpgrades)
                Ui.Pips(heading, rank, spell.MaxRank, PipSize, owned ? Ui.Wisp : Ui.FadedInk);

            Ui.Label(Blurb(spell, owned, rank, slot), words, BlurbSize, wordsWidth, BlurbHeight,
                TextAlignmentOptions.TopLeft).color = Ui.FadedInk;

            DrawAction(spell, card.transform, owned, rank);

            return card.gameObject;
        }

        string Blurb(Ability spell, bool owned, int rank, int slot)
        {
            if (!owned)
                return spell.Description;

            Ability.Upgrade next = spell.NextUpgrade(rank);

            string where = slot >= 0
                ? Loc.Format(Loc.Keys.SkillOn, Glyph(slot))
                : Loc.Get(Loc.Keys.SkillBench);

            return next != null
                ? Loc.Format(Loc.Keys.SkillNext, where, spell.UpgradeTitle(rank),
                             spell.UpgradeDescription(rank))
                : $"{where}  {spell.Description}";
        }

        void DrawAction(Ability spell, Transform parent, bool owned, int rank)
        {
            if (!owned)
            {
                bool affordable = Progress.CanAfford(spell.cost);

                Button buy = Ui.CreateButton(
                    affordable ? Loc.Format(Loc.Keys.SkillLearn, spell.cost)
                               : Loc.Format(Loc.Keys.SkillPrice, spell.cost),
                    parent, ActionWidth, ActionHeight, ActionFontSize);

                buy.interactable = affordable;

                if (affordable)
                    buy.onClick.AddListener(() =>
                    {
                        if (!Progress.Buy(spell.Key, spell.cost))
                            return;

                        int free = Progress.FirstEmptySlot();

                        if (free >= 0 && !spell.locked)
                            Progress.Equip(free, spell.Key);

                        focusKey = spell.Key;
                        Apply();
                    });

                return;
            }

            Ability.Upgrade step = spell.NextUpgrade(rank);

            if (step == null)
            {
                Ui.Label(Loc.Get(spell.HasUpgrades ? Loc.Keys.SkillMastered : Loc.Keys.SkillLearned),
                    parent, ActionFontSize, ActionWidth, ActionHeight).color = Ui.FadedInk;
                return;
            }

            bool canPay = Progress.CanAfford(step.cost);

            Button raise = Ui.CreateButton(
                canPay ? $"{spell.UpgradeTitle(rank)} - {step.cost}"
                       : Loc.Format(Loc.Keys.SkillPrice, step.cost),
                parent, ActionWidth, ActionHeight, ActionFontSize);

            raise.interactable = canPay;

            if (canPay)
                raise.onClick.AddListener(() =>
                {
                    if (!Progress.Upgrade(spell.Key, step.cost, spell.MaxRank))
                        return;

                    focusKey = spell.Key;
                    Apply();
                });
        }

        void Assign(Ability spell, int slot)
        {
            if (spell == null || !Progress.Owns(spell.Key) || spell.locked)
                return;

            Ability resident = book != null ? book.Find(Progress.EquippedIn(slot)) : null;

            if (resident != null && resident.locked)
                return;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.Equip(spell, slot);
            else
                Progress.Place(slot, spell.Key);

            focusKey = spell.Key;
            Apply();
        }

        Ability Focused()
        {
            GameObject picked = EventSystem.current != null
                ? EventSystem.current.currentSelectedGameObject
                : null;

            if (picked != null && rows.TryGetValue(picked, out Ability on))
            {
                focusKey = on.Key;
                return on;
            }

            return book != null ? book.Find(focusKey) : null;
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

            return Core.Controls.Glyph(
                Core.Controls.Player(PlayerLogic.Spellbook.SlotActions[slot]));
        }

        class Door : MonoBehaviour
        {
            static Door live;

            SkillScreen open;

            bool wasPaused;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            static void Install()
            {
                if (live != null)
                    return;

                var go = new GameObject("Loadout Door", typeof(Door));
                DontDestroyOnLoad(go);
                live = go.GetComponent<Door>();
            }

            void Update()
            {
                if (open != null || Screens.ModalOpen || !Core.Controls.LoadoutPressed)
                    return;

                wasPaused = Game.IsPaused;
                open = Raise(null, Loc.Keys.SkillBack, Close);
            }

            void Close()
            {
                open = null;

                if (!wasPaused)
                    Game.SetPaused(false);
            }
        }
    }
}
