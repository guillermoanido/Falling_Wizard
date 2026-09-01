using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public static class Ui
    {
        public static readonly Color Ink = new Color(0.93f, 0.92f, 0.98f);
        public static readonly Color FadedInk = new Color(0.62f, 0.60f, 0.72f);
        public static readonly Color ButtonInk = new Color(0.11f, 0.10f, 0.14f);

        public static readonly Color Shade = new Color(0.02f, 0.02f, 0.04f, 0.78f);
        public static readonly Color Panel = new Color(0.07f, 0.06f, 0.11f, 0.97f);
        public static readonly Color Card = new Color(0.13f, 0.12f, 0.19f, 0.95f);
        public static readonly Color CardLit = new Color(0.20f, 0.19f, 0.30f, 0.98f);

        public static readonly Color Wisp = new Color(0.55f, 0.85f, 1f);
        public static readonly Color Heart = new Color(0.88f, 0.30f, 0.38f);
        public static readonly Color Warning = new Color(0.95f, 0.72f, 0.36f);

        // Everything below is in reference-resolution pixels, and the scaler maps them onto
        // whatever the player's screen actually is.
        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        // Split the difference between matching width and matching height, so an ultrawide and a
        // tall window both keep the panels on screen.
        const float ScalerBalance = 0.5f;

        public const float SheetPadding = 36f;
        public const float SheetSpacing = 16f;
        public const float ColumnSpacing = 18f;
        public const float RowSpacing = 16f;
        public const float ButtonFontSize = 30f;

        static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        public static readonly Color PipEmpty = new Color(1f, 1f, 1f, 0.16f);

        // Unity's defaultColorBlock steps normal (1,1,1) to selected (0.961,...) - a four per
        // cent change nobody can see, and on a gamepad the selection IS the cursor.
        public static ColorBlock Tints
        {
            get
            {
                ColorBlock block = ColorBlock.defaultColorBlock;
                block.normalColor = new Color(1f, 1f, 1f, 1f);
                block.highlightedColor = new Color(0.84f, 0.90f, 1f, 1f);
                block.selectedColor = new Color(0.72f, 0.84f, 1f, 1f);
                block.pressedColor = new Color(0.60f, 0.72f, 0.95f, 1f);
                block.disabledColor = new Color(1f, 1f, 1f, 0.35f);
                block.fadeDuration = 0.08f;
                return block;
            }
        }

        // For a plate already tinted Ui.Card: multiplier 2 with a normal of 0.5 lands it back on
        // exactly Ui.Card when idle and near Ui.CardLit when selected, which is the colour this
        // project already uses everywhere for "this is the one".
        public static ColorBlock CardTints
        {
            get
            {
                ColorBlock block = ColorBlock.defaultColorBlock;
                block.colorMultiplier = 2f;
                block.normalColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                block.highlightedColor = new Color(0.62f, 0.62f, 0.7f, 0.5f);
                block.selectedColor = new Color(0.78f, 0.78f, 0.92f, 0.52f);
                block.pressedColor = new Color(0.9f, 0.9f, 1f, 0.55f);
                block.disabledColor = new Color(0.4f, 0.4f, 0.45f, 0.4f);
                block.fadeDuration = 0.08f;
                return block;
            }
        }


        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScalerBalance;

            EnsureEventSystem();

            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("Event System", typeof(EventSystem));
            go.AddComponent<InputSystemUIInputModule>();
        }

        public static Image Shroud(Transform parent)
        {
            var go = new GameObject("Shroud", typeof(Image));
            Attach(go, parent);
            Stretch(go);

            Image art = go.GetComponent<Image>();
            art.color = Shade;
            return art;
        }

        public static RectTransform Sheet(string name, Transform parent, Color colour,
            float width, float padding = SheetPadding, float spacing = SheetSpacing)
        {
            var go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            Attach(go, parent);

            go.GetComponent<Image>().color = colour;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Centre;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, 0f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = spacing;
            layout.padding = new RectOffset((int)padding, (int)padding, (int)padding, (int)padding);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rect;
        }

        public static RectTransform Column(string name, Transform parent, float width,
            float spacing = ColumnSpacing, TextAnchor align = TextAnchor.UpperCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            Attach(go, parent);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, 0f);

            return rect;
        }

        public static RectTransform Row(string name, Transform parent, float width, float height,
            float spacing = RowSpacing, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Attach(go, parent);
            SetSize(go, width, height);

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = align;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return (RectTransform)go.transform;
        }

        public static Image Plate(string name, Transform parent, Color colour, float width,
            float height, bool clickable = true)
        {
            var go = new GameObject(name, typeof(Image));
            Attach(go, parent);
            SetSize(go, width, height);

            Image art = go.GetComponent<Image>();
            art.color = colour;
            art.raycastTarget = clickable;
            return art;
        }

        // Turn a plate into something you can click or navigate to. Its children are already
        // raycast-deaf (Icon and Label both switch theirs off), so the plate is what the pointer
        // hits and there is nothing to fight over.
        public static Button Pressable(Image plate)
        {
            var button = plate.gameObject.AddComponent<Button>();
            button.targetGraphic = plate;
            button.transition = Selectable.Transition.ColorTint;
            button.colors = CardTints;
            return button;
        }

        // Deselect first: SetSelectedGameObject early-outs when the object is already selected,
        // so re-selecting the same row after a rebuild would never fire OnSelect and the
        // highlight would be lost.
        public static void Focus(GameObject what)
        {
            if (EventSystem.current == null || what == null)
                return;

            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(what);
        }

        public static RectTransform Pips(Transform parent, int filled, int total, float size,
            Color lit)
        {
            RectTransform strip = Row("Pips", parent, (size + 4f) * Mathf.Max(1, total), size, 4f);

            for (int i = 0; i < total; i++)
                Plate($"Pip {i + 1}", strip, i < filled ? lit : PipEmpty, size, size,
                    clickable: false);

            return strip;
        }

        public static Image Icon(Transform parent, Sprite sprite, float size, Color tint)
        {
            var go = new GameObject("Icon", typeof(Image));
            Attach(go, parent);
            SetSize(go, size, size);

            Image art = go.GetComponent<Image>();
            art.sprite = sprite;
            art.color = tint;
            art.preserveAspect = true;
            art.raycastTarget = false;
            return art;
        }

        public static TextMeshProUGUI Label(string text, Transform parent, float size, float width,
            float height, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            GameObject go = TMP_DefaultControls.CreateText(Plain);
            go.name = "Label";
            Attach(go, parent);
            SetSize(go, width, height);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = Ink;
            label.alignment = align;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;

            return label;
        }

        public static Button CreateButton(string text, Transform parent, float width, float height,
            float fontSize = ButtonFontSize)
        {
            GameObject go = TMP_DefaultControls.CreateButton(Plain);
            go.name = text + " Button";
            Attach(go, parent);
            SetSize(go, width, height);

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = ButtonInk;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var button = go.GetComponent<Button>();
            button.colors = Tints;

            return button;
        }

        public static void Retext(Button button, string text)
        {
            if (button == null)
                return;

            var label = button.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
                label.text = text;
        }

        public static void SetSize(GameObject go, float width, float height)
        {
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var element = go.GetComponent<LayoutElement>();

            if (element == null)
                element = go.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.preferredHeight = height;

            element.minWidth = width;
            element.minHeight = height;
        }

        public static void Attach(GameObject go, Transform parent) =>
            go.transform.SetParent(parent, false);

        public static void Stretch(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static readonly TMP_DefaultControls.Resources Plain = new TMP_DefaultControls.Resources();
    }
}
