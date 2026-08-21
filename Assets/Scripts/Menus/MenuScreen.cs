using FallingWizard.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public abstract class MenuScreen : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject panel;
        [SerializeField] SettingsPanel settingsPanel;

        protected bool IsSettingsOpen => settingsPanel.gameObject.activeSelf;

        protected abstract Selectable DefaultSelection { get; }

        protected abstract void WireButtons();

        protected abstract void OnBackPressed();

        void Awake()
        {
            settingsPanel.Closed += CloseSettings;
            WireButtons();
        }

        void OnDestroy() => settingsPanel.Closed -= CloseSettings;

        void Update()
        {
            if (Controls.PausePressed)
                OnBackPressed();
        }

        protected void ShowPanel()
        {
            settingsPanel.gameObject.SetActive(false);
            panel.SetActive(true);
            Focus();
        }

        protected void HidePanel()
        {
            settingsPanel.gameObject.SetActive(false);
            panel.SetActive(false);
        }

        protected void OpenSettings()
        {
            panel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        protected void CloseSettings() => ShowPanel();

        void Focus()
        {
            if (DefaultSelection == null || EventSystem.current == null)
                return;

            EventSystem.current.SetSelectedGameObject(DefaultSelection.gameObject);
        }
    }
}
