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
        // Above the Pause Menu's canvas, which sits at 100.
        const int DefaultSortingOrder = 200;

        const float PanelWidth = 760f;
        const float PanelPadding = 40f;
        const float PanelSpacing = 16f;

        const float ButtonWidth = 640f;
        const float ButtonHeight = 68f;
        const float ButtonFontSize = 28f;

        const float TitleSize = 56f;
        const float TitleHeight = 70f;

        // Two lines' worth: the blurb wraps, and a clipped explanation is worse than none.
        const float BlurbSize = 26f;
        const float BlurbHeight = 72f;

        const float StatusSize = 28f;
        const float StatusHeight = 40f;

        readonly List<Button> buttons = new List<Button>();

        RectTransform column;
        TextMeshProUGUI status;

        public static ChoiceScreen Open(string title, string blurb,
            int sortingOrder = DefaultSortingOrder)
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

            column = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth,
                PanelPadding, PanelSpacing);

            float inner = PanelWidth - PanelPadding * 2f;

            Ui.Label(title, column, TitleSize, inner, TitleHeight);

            if (!string.IsNullOrEmpty(blurb))
                Ui.Label(blurb, column, BlurbSize, inner, BlurbHeight).color = Ui.FadedInk;

            status = Ui.Label(string.Empty, column, StatusSize, inner, StatusHeight);
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
            Button button = Ui.CreateButton(text, column, ButtonWidth, ButtonHeight,
                ButtonFontSize);
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
