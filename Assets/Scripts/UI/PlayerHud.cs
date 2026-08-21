using System.Collections.Generic;
using FallingWizard.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    // Hearts along the top, spells along the bottom. It builds its own canvas on Awake, so the
    // scene needs nothing but this one component on an empty object and there is no prefab to
    // drift out of step with the code.
    //
    // It finds the wizard through the singleton and re-finds them every frame, because dying
    // reloads the level and builds a brand new one - anything that subscribed to the old wizard
    // would be holding a destroyed object.
    public class PlayerHud : MonoBehaviour
    {

        [Header("Canvas")]
        [Tooltip("Draw order. Above the game, below the pause menu, which sits at 100.")]
        public int sortingOrder = 10;

        [Tooltip("Layout is designed against this screen size and scaled to fit.")]
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        [Tooltip("Must match the art's pixels per unit or every icon comes out the wrong size.")]
        [Min(1f)] public float referencePixelsPerUnit = 32f;

        [Tooltip("Gap between the HUD and the edge of the screen, in reference pixels.")]
        public Vector2 margin = new Vector2(48f, 48f);

        [Header("Hearts")]
        [Tooltip("Leave empty for a plain box. Any sprite works.")]
        public Sprite heartSprite;

        [Min(1f)] public float heartSize = 40f;
        [Min(0f)] public float heartSpacing = 6f;
        public Color fullHeart = new Color(0.85f, 0.22f, 0.30f);
        public Color emptyHeart = new Color(0.20f, 0.16f, 0.22f);

        [Header("Spell Bar")]
        [Tooltip("Leave empty for a plain box behind each spell icon.")]
        public Sprite slotBackground;

        [Min(1f)] public float slotSize = 64f;
        [Min(0f)] public float slotSpacing = 14f;

        [Tooltip("Show a dimmed slot for spells not learned yet, so the bar never shifts around.")]
        public bool showLockedSlots = true;

        public Color slotFrame = new Color(0.12f, 0.10f, 0.16f, 0.75f);
        public Color lockedTint = new Color(1f, 1f, 1f, 0.18f);
        public Color readyTint = Color.white;
        public Color notReadyTint = new Color(1f, 1f, 1f, 0.45f);

        [Tooltip("Wipe drawn over a spell while it is cooling down or running.")]
        public Color chargeTint = new Color(0.25f, 0.65f, 1f, 0.55f);

        [Header("Button Prompt")]
        [Tooltip("Print the button under each spell. Follows whichever device is in use, so it " +
                 "reads E on a keyboard and X on a pad without anyone pressing a setting.")]
        public bool showButtons = true;

        [Min(6f)] public float buttonTextSize = 22f;
        public Color buttonTextColor = new Color(0.92f, 0.90f, 0.86f);

        readonly List<Image> hearts = new List<Image>();
        readonly List<Slot> slots = new List<Slot>();

        RectTransform heartRow;
        RectTransform spellBar;
        PlayerCharacter bound;
        int builtHearts = -1;

        static Sprite blank;

        // A plain white square to stand in for art nobody has drawn yet. Made here rather than
        // borrowed from Unity's built-in UI skin, which URP projects do not ship. It also has to
        // be a real sprite and not just a null one: an Image with no sprite draws a plain quad
        // and quietly ignores its fill mode, which would leave every cooldown wipe frozen.
        static Sprite Blank
        {
            get
            {
                if (blank != null)
                    return blank;

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "Falling Wizard HUD Blank",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);

                texture.SetPixels32(pixels);
                texture.Apply();

                blank = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                blank.name = "Falling Wizard HUD Blank";
                blank.hideFlags = HideFlags.HideAndDontSave;

                return blank;
            }
        }

        void Awake() => Build();

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

            bool alive = bound != null;
            heartRow.gameObject.SetActive(alive);
            spellBar.gameObject.SetActive(alive);

            if (!alive)
                return;

            RefreshHearts();
            RefreshSpells();
        }

        // ------------------------------------------------------------------------- building ----

        void Build()
        {
            var canvas = gameObject.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameObject.AddComponent<Canvas>();

            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = referencePixelsPerUnit;

            // Deliberately no GraphicRaycaster: the HUD must never swallow a pause-menu click or
            // steal controller focus from the menus.

            heartRow = Row("Hearts", new Vector2(0f, 1f), new Vector2(margin.x, -margin.y), heartSpacing);
            spellBar = Row("Spell Bar", new Vector2(0f, 0f), new Vector2(margin.x, margin.y), slotSpacing);
        }

        RectTransform Row(string name, Vector2 anchor, Vector2 offset, float spacing)
        {
            var host = new GameObject(name, typeof(RectTransform));
            var rect = host.GetComponent<RectTransform>();
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = Vector2.zero;

            var layout = host.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = anchor.y > 0.5f ? TextAnchor.UpperLeft : TextAnchor.LowerLeft;

            host.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rect;
        }

        void BuildSpellBar()
        {
            foreach (Slot slot in slots)
                if (slot.Root != null)
                    Destroy(slot.Root);

            slots.Clear();

            IReadOnlyList<PlayerLogic.Spellbook.Slot> book = bound.Logic.spellbook.Slots;

            for (int i = 0; i < book.Count; i++)
                slots.Add(new Slot(this, spellBar, book[i]));
        }

        void RefreshHearts()
        {
            PlayerLogic.Health health = bound.Logic.health;

            if (builtHearts != health.Max)
            {
                foreach (Image heart in hearts)
                    Destroy(heart.gameObject);

                hearts.Clear();

                for (int i = 0; i < health.Max; i++)
                    hearts.Add(Box("Heart", heartRow, heartSize, heartSprite));

                builtHearts = health.Max;
            }

            for (int i = 0; i < hearts.Count; i++)
                hearts[i].color = i < health.Current ? fullHeart : emptyHeart;
        }

        void RefreshSpells()
        {
            IReadOnlyList<PlayerLogic.Spellbook.Slot> book = bound.Logic.spellbook.Slots;

            for (int i = 0; i < slots.Count && i < book.Count; i++)
                slots[i].Refresh(this, book[i]);
        }

        void RefreshButtons()
        {
            foreach (Slot slot in slots)
                slot.RefreshButton(this);
        }

        Image Box(string name, Transform parent, float size, Sprite sprite)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var image = host.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : Blank;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;

            var rect = host.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(size, size);

            return image;
        }

        // =========================================================================== the slot ==

        // One spell on the bar: a frame, the icon, a wipe for cooldowns and lit time, and the
        // button to press.
        class Slot
        {
            public readonly GameObject Root;

            readonly Image frame;
            readonly Image icon;
            readonly Image charge;
            readonly TextMeshProUGUI button;

            PlayerLogic.Spellbook.Slot source;

            public Slot(PlayerHud hud, Transform parent, PlayerLogic.Spellbook.Slot spell)
            {
                source = spell;
                frame = hud.Box("Spell", parent, hud.slotSize, hud.slotBackground);
                frame.color = hud.slotFrame;
                Root = frame.gameObject;

                icon = hud.Box("Icon", frame.transform, hud.slotSize, spell.Ability?.icon);
                Stretch(icon.rectTransform, 6f);

                charge = hud.Box("Charge", frame.transform, hud.slotSize, null);
                Stretch(charge.rectTransform, 6f);
                charge.type = Image.Type.Filled;
                charge.fillMethod = Image.FillMethod.Radial360;
                charge.fillOrigin = (int)Image.Origin360.Top;
                charge.fillClockwise = false;
                charge.color = hud.chargeTint;

                var host = new GameObject("Button", typeof(RectTransform));
                host.transform.SetParent(frame.transform, false);

                button = host.AddComponent<TextMeshProUGUI>();
                button.fontSize = hud.buttonTextSize;
                button.color = hud.buttonTextColor;
                button.alignment = TextAlignmentOptions.Center;
                button.raycastTarget = false;

                var rect = host.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(0f, hud.buttonTextSize * 1.4f);
                rect.anchoredPosition = new Vector2(0f, -2f);
            }

            public void Refresh(PlayerHud hud, PlayerLogic.Spellbook.Slot spell)
            {
                source = spell;
                bool known = spell.Owned && spell.Ability != null;

                Root.SetActive(known || hud.showLockedSlots);

                if (!Root.activeSelf)
                    return;

                icon.color = !known ? hud.lockedTint
                           : spell.IsReady ? hud.readyTint
                           : hud.notReadyTint;

                // The wipe shows whichever clock is running: the spell's own lit window while it
                // is going, then the cooldown until it can be cast again.
                float fill = spell.IsLit ? spell.LitProgress
                           : spell.CooldownLeft > 0f ? spell.CooldownProgress
                           : 0f;

                charge.enabled = fill > 0f;
                charge.fillAmount = fill;

                button.enabled = hud.showButtons && known && spell.Action != null;
            }

            public void RefreshButton(PlayerHud hud)
            {
                if (button == null)
                    return;

                // Asked for by device, so it says E on a keyboard and X on a pad. Never both.
                button.text = hud.showButtons && source != null
                    ? Core.Controls.Glyph(source.Action)
                    : string.Empty;
            }

            static void Stretch(RectTransform rect, float inset)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(inset, inset);
                rect.offsetMax = new Vector2(-inset, -inset);
            }
        }
    }
}
