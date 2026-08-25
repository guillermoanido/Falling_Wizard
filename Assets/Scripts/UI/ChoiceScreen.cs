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

            column = Ui.Sheet("Panel", transform, Ui.Panel, PanelWidth, 40f, 16f);

            float inner = PanelWidth - 80f;

            Ui.Label(title, column, 56f, inner, 70f);

            if (!string.IsNullOrEmpty(blurb))
                Ui.Label(blurb, column, 26f, inner, 72f).color = Ui.FadedInk;

            status = Ui.Label(string.Empty, column, 28f, inner, 40f);
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
