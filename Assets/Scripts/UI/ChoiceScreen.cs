using System;
using System.Collections.Generic;
using FallingWizard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.UI
{
    public class ChoiceScreen : MonoBehaviour
    {
        const float PanelWidth = 760f;
        const float ButtonWidth = 640f;
        const float ButtonHeight = 68f;

        readonly List<Button> buttons = new List<Button>();

        RectTransform column;
        TextMeshProUGUI status;

        public static ChoiceScreen Open(string title, string blurb, int sortingOrder = 200)
        {
            Game.SetPaused(true);
            Screens.Claim();

            Canvas canvas = Ui.CreateCanvas(title, sortingOrder);
            var screen = canvas.gameObject.AddComponent<ChoiceScreen>();
            screen.Build(title, blurb);

            return screen;
        }

        void Build(string title, string blurb)
        {
            Ui.Shroud(transform);

            Image panel = Ui.Plate("Panel", transform, Ui.Panel, PanelWidth, 0f);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 16f;
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            column = (RectTransform)panel.transform;

            Ui.Label(title, column, 56f, PanelWidth - 80f, 70f);

            if (!string.IsNullOrEmpty(blurb))
                Ui.Label(blurb, column, 26f, PanelWidth - 80f, 40f).color = Ui.FadedInk;

            status = Ui.Label(string.Empty, column, 28f, PanelWidth - 80f, 40f);
            status.color = Ui.Wisp;
        }

        public ChoiceScreen Status(string text)
        {
            if (status != null)
                status.text = text;

            return this;
        }

        public ChoiceScreen Choice(string text, Action chosen, bool enabled = true)
        {
            Button button = Ui.CreateButton(text, column, ButtonWidth, ButtonHeight, 28f);
            button.interactable = enabled;

            if (enabled && chosen != null)
                button.onClick.AddListener(() => chosen());

            buttons.Add(button);

            if (enabled && EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == null)
                EventSystem.current.SetSelectedGameObject(button.gameObject);

            return this;
        }

        public void Close()
        {
            Screens.Release();
            Game.SetPaused(false);
            Destroy(gameObject);
        }

        public void CloseThen(Action next)
        {
            Screens.Release();
            Destroy(gameObject);
            next?.Invoke();
        }
    }
}
