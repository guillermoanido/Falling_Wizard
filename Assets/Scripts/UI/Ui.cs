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

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);


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
            scaler.matchWidthOrHeight = 0.5f;

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
            float width, float padding = 36f, float spacing = 16f)
        {
            var go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            Attach(go, parent);

            go.GetComponent<Image>().color = colour;

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
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
            float spacing = 18f, TextAnchor align = TextAnchor.UpperCenter)
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
            float spacing = 16f, TextAnchor align = TextAnchor.MiddleLeft)
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

        public static Image Plate(string name, Transform parent, Color colour, float width, float height)
        {
            var go = new GameObject(name, typeof(Image));
            Attach(go, parent);
            SetSize(go, width, height);

            Image art = go.GetComponent<Image>();
            art.color = colour;
            return art;
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
            float fontSize = 30f)
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

            return go.GetComponent<Button>();
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
