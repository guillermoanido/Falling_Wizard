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
    // Two jobs, kept apart on purpose. The ROWS are the shop: learn a spell, raise its rank. The
    // RAIL along the top is the loadout: which button each spell answers to. Mixing them is what
    // the old screen did, and it is why there was no way to say "put this one on R" - every path
    // went through FirstEmptySlot.
    public class SkillScreen : MonoBehaviour
    {
        // Above the Pause Menu (100) and above a rest or death screen (200), since this is what
        // those open on top of themselves.
        const int SortingOrder = 220;

        // Sized so eight spells still fit a 1080-tall canvas without a scroll view. Past that the
        // panel starts running off the bottom and this wants a real one.
        const float PanelWidth = 1180f;
        const float PanelPadding = 24f;
        const float PanelSpacing = 8f;

        const float RowHeight = 84f;
        const float IconSize = 52f;
        const float ActionWidth = 250f;
        const float ActionHeight = 46f;
        const float ActionFontSize = 22f;
        const float SlotSize = 92f;

        // Room under a slot for the button glyph and whatever is sitting on it.
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

        // What the icon, the action button, the card's own padding and the gaps between them take
        // out of a row. Whatever is left is where the words go.
        const float RowFurniture = IconSize + ActionWidth + CardPadding * 2f + CardSpacing * 2f;

        // The panel's content area, inside its padding.
        const float Inner = PanelWidth - PanelPadding * 2f;

        static readonly Color UnownedIcon = new Color(1f, 1f, 1f, 0.3f);

        readonly Dictionary<GameObject, Ability> rows = new Dictionary<GameObject, Ability>();

        AbilityBook book;
        Action dive;
        Action closed;
        string diveText = "Descend";

        RectTransform body;
        InputAction[] slotKeys;

        // Survives a Redraw, which throws every row away and builds new ones.
        string focusKey = string.Empty;

        [NonSerialized] int openedOn;

        public static SkillScreen Open(Action onDive, string label = "Descend") =>
            Raise(onDive, label, null);

        static SkillScreen Raise(Action onDive, string label, Action onClose)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas("Skill Screen", SortingOrder);
            var screen = canvas.gameObject.AddComponent<SkillScreen>();

            screen.dive = onDive;
            screen.diveText = label;
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

            // Reachable from the main menu, where no wizard exists to have done this in Attach.
            if (book != null)
                PlayerLogic.Spellbook.Seed(book);

            Core.Controls.SchemeChanged += Redraw;

            Redraw();
        }

        void OnDestroy() => Core.Controls.SchemeChanged -= Redraw;

        void Update()
        {
            // Not paranoia: the door and this screen both read WasPressedThisFrame, and execution
            // order between two arbitrary MonoBehaviours is undefined - without the guard the
            // screen closes on the very press that opened it.
            if (Time.frameCount != openedOn &&
                (Core.Controls.PausePressed || Core.Controls.CancelPressed))
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

            Ui.Label("What you carry down", body, TitleSize, Inner, TitleHeight);

            TextMeshProUGUI purse = Ui.Label($"{Progress.Wisps} wisps", body, PurseSize,
                Inner, PurseHeight);
            purse.color = Ui.Wisp;

            DrawSlots();
            DrawHint();

            GameObject first = null;

            if (book == null)
            {
                Ui.Label("No spellbook found at Assets/Resources/Spellbook.asset.", body,
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

            Ui.CreateButton(diveText, body, DiveWidth, DiveHeight, DiveFontSize)
                .onClick.AddListener(Leave);

            // Without this a gamepad is stuck: Navigate has nowhere to move from, and the old
            // screen actively cleared the selection on every rebuild.
            Ui.Focus(first);
        }

        void DrawHint()
        {
            Ability picked = book != null ? book.Find(focusKey) : null;

            string words = picked == null
                ? "Pick a spell, then press the button you want it on."
                : Progress.Owns(picked.Key)
                    ? $"{picked.displayName}: press {Buttons()} to move it."
                    : $"{picked.displayName} is not learned yet.";

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
                    held != null ? $"{Glyph(i)}  {held.displayName}" : Glyph(i), cell,
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

            TextMeshProUGUI name = Ui.Label(spell.displayName, heading, NameSize,
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
                return spell.description;

            Ability.Upgrade next = spell.NextUpgrade(rank);
            string where = slot >= 0 ? $"On {Glyph(slot)}." : "On the bench.";

            return next != null
                ? $"{where}  Next: {next.title} - {next.description}"
                : $"{where}  {spell.description}";
        }

        void DrawAction(Ability spell, Transform parent, bool owned, int rank)
        {
            if (!owned)
            {
                bool affordable = Progress.CanAfford(spell.cost);

                Button buy = Ui.CreateButton(
                    affordable ? $"Learn - {spell.cost}" : $"{spell.cost} wisps",
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
                Ui.Label(spell.HasUpgrades ? "Mastered" : "Learned", parent, ActionFontSize,
                    ActionWidth, ActionHeight).color = Ui.FadedInk;
                return;
            }

            bool canPay = Progress.CanAfford(step.cost);

            Button raise = Ui.CreateButton(
                canPay ? $"{step.title} - {step.cost}" : $"{step.cost} wisps",
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

        // The rule about what may go where lives in ONE place. The screen asks; it does not decide.
        void Assign(Ability spell, int slot)
        {
            if (spell == null || !Progress.Owns(spell.Key) || spell.locked)
                return;

            Ability resident = book != null ? book.Find(Progress.EquippedIn(slot)) : null;

            if (resident != null && resident.locked)
                return;                              // the Staff's button stays shut

            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard != null)
                wizard.Logic.spellbook.Equip(spell, slot);   // swaps, honours locked, reloads
            else
                Progress.Place(slot, spell.Key);             // main menu: nobody to tell

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

        // The two real doors into this screen both cost the run and both dive to level one, and
        // a level with no rest site leaves dying as the only way in. This is the playtest door.
        // It installs ONLY while the sandbox is on, so it cannot exist in a real playthrough and
        // the "decide before you go down" rule is untouched.
        class Door : MonoBehaviour
        {
            static Door live;

            SkillScreen open;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
            static void Install()
            {
                // Playtest.Awake runs at -100 and AfterSceneLoad runs after every Awake, so the
                // sandbox flag is already settled by the time this asks.
                if (live != null || !Progress.Sandbox)
                    return;

                var go = new GameObject("Loadout Door", typeof(Door));
                DontDestroyOnLoad(go);
                live = go.GetComponent<Door>();
            }

            void Update()
            {
                if (open != null || Screens.ModalOpen || !Core.Controls.PausePressed)
                    return;

                open = Raise(null, "Back to the fall", () => open = null);
            }
        }
    }
}
