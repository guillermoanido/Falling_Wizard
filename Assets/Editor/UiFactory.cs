using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.EditorTools
{
    static class UiFactory
    {
        static readonly Color PanelColor = new Color(0.07f, 0.06f, 0.11f, 0.92f);
        static readonly Color TextColor = new Color(0.93f, 0.92f, 0.98f);
        static readonly Color ButtonTextColor = new Color(0.11f, 0.10f, 0.14f);

        static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        const float ScalerWidthHeightBalance = 0.5f;

        const float PanelSpacing = 18f;

        const float TitleFontSize = 86f;
        const float TitleWidth = 1000f;
        const float TitleHeight = 130f;

        const float HeadingFontSize = 68f;
        const float HeadingWidth = 1000f;
        const float HeadingHeight = 110f;

        const float ButtonWidth = 360f;
        const float ButtonHeight = 62f;
        const float ButtonFontSize = 30f;

        const float RowWidth = 760f;
        const float RowHeight = 50f;
        const float RowSpacing = 24f;
        const float CaptionWidth = 280f;
        const float CaptionHeight = 44f;
        const float CaptionFontSize = 28f;

        const float ValueWidth = 100f;
        const float ValueHeight = 44f;
        const float ValueFontSize = 26f;

        const float ControlWidth = 340f;
        const float SliderHeight = 26f;
        const float DropdownHeight = 46f;
        const float DropdownFontSize = 24f;
        const float ToggleBoxSize = 38f;

        static DefaultControls.Resources standard;
        static TMP_DefaultControls.Resources textMeshPro;

        static UiFactory()
        {
            standard = new DefaultControls.Resources
            {
                standard = Builtin("UI/Skin/UISprite.psd"),
                background = Builtin("UI/Skin/Background.psd"),
                inputField = Builtin("UI/Skin/InputFieldBackground.psd"),
                knob = Builtin("UI/Skin/Knob.psd"),
                checkmark = Builtin("UI/Skin/Checkmark.psd"),
                dropdown = Builtin("UI/Skin/DropdownArrow.psd"),
                mask = Builtin("UI/Skin/UIMask.psd"),
            };

            textMeshPro = new TMP_DefaultControls.Resources
            {
                standard = standard.standard,
                background = standard.background,
                inputField = standard.inputField,
                knob = standard.knob,
                checkmark = standard.checkmark,
                dropdown = standard.dropdown,
                mask = standard.mask,
            };
        }

        static Sprite Builtin(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

        public static GameObject CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = ScalerWidthHeightBalance;

            return go;
        }

        public static GameObject CreatePanel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(Image), typeof(VerticalLayoutGroup));
            Attach(go, parent);
            Stretch(go);

            go.GetComponent<Image>().color = PanelColor;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = PanelSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return go;
        }

        public static TextMeshProUGUI CreateTitle(string text, Transform parent) =>
            CreateLabel(text, parent, TitleFontSize, TitleWidth, TitleHeight);

        public static TextMeshProUGUI CreateHeading(string text, Transform parent) =>
            CreateLabel(text, parent, HeadingFontSize, HeadingWidth, HeadingHeight);

        public static TextMeshProUGUI CreateValueLabel(string text, Transform parent) =>
            CreateLabel(text, parent, ValueFontSize, ValueWidth, ValueHeight);

        public static Button CreateButton(string text, Transform parent)
        {
            GameObject go = TMP_DefaultControls.CreateButton(textMeshPro);
            go.name = text + " Button";
            Attach(go, parent);
            SetSize(go, ButtonWidth, ButtonHeight);

            var label = go.GetComponentInChildren<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = ButtonFontSize;
            label.color = ButtonTextColor;

            return go.GetComponent<Button>();
        }

        public static GameObject CreateRow(string caption, Transform parent)
        {
            var row = new GameObject(caption + " Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Attach(row, parent);
            SetSize(row, RowWidth, RowHeight);

            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = RowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateLabel(caption, row.transform, CaptionFontSize, CaptionWidth, CaptionHeight,
                TextAlignmentOptions.Left);

            return row;
        }

        public static Slider CreateSlider(Transform parent)
        {
            GameObject go = DefaultControls.CreateSlider(standard);
            go.name = "Slider";
            Attach(go, parent);
            SetSize(go, ControlWidth, SliderHeight);

            var slider = go.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = slider.maxValue;
            return slider;
        }

        public static Toggle CreateToggle(Transform parent)
        {
            GameObject go = DefaultControls.CreateToggle(standard);
            go.name = "Toggle";
            Attach(go, parent);
            SetSize(go, ToggleBoxSize, ToggleBoxSize);

            Transform label = go.transform.Find("Label");
            if (label != null)
                Object.DestroyImmediate(label.gameObject);

            var background = go.transform.Find("Background") as RectTransform;
            if (background != null)
            {
                var box = new Vector2(ToggleBoxSize, ToggleBoxSize);
                background.sizeDelta = box;
                background.anchoredPosition = new Vector2(box.x * 0.5f, -box.y * 0.5f);

                if (background.Find("Checkmark") is RectTransform checkmark)
                    checkmark.sizeDelta = box;
            }

            return go.GetComponent<Toggle>();
        }

        public static TMP_Dropdown CreateDropdown(Transform parent)
        {
            GameObject go = TMP_DefaultControls.CreateDropdown(textMeshPro);
            go.name = "Dropdown";
            Attach(go, parent);
            SetSize(go, ControlWidth, DropdownHeight);

            foreach (TextMeshProUGUI text in go.GetComponentsInChildren<TextMeshProUGUI>(true))
                text.fontSize = DropdownFontSize;

            return go.GetComponent<TMP_Dropdown>();
        }

        static TextMeshProUGUI CreateLabel(string text, Transform parent, float fontSize,
            float width, float height, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            GameObject go = TMP_DefaultControls.CreateText(textMeshPro);
            go.name = text + " Label";
            Attach(go, parent);
            SetSize(go, width, height);

            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.color = TextColor;
            label.alignment = alignment;
            label.raycastTarget = false;

            return label;
        }

        static void SetSize(GameObject go, float width, float height)
        {
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, height);

            var element = go.GetComponent<LayoutElement>();
            if (element == null)
                element = go.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        static void Attach(GameObject go, Transform parent) => go.transform.SetParent(parent, false);

        static void Stretch(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
