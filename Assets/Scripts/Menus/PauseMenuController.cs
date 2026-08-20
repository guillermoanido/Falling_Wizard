using FallingWizard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class PauseMenuController : MenuScreen
    {
        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] Button quitButton;

        protected override Selectable DefaultSelection => resumeButton;

        protected override void WireButtons()
        {
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(OpenSettings);
            mainMenuButton.onClick.AddListener(Game.LoadMainMenu);
            quitButton.onClick.AddListener(Game.Quit);
        }

        protected override void OnBackPressed()
        {
            if (IsSettingsOpen)
                CloseSettings();
            else if (Game.IsPaused)
                Resume();
            else
                Pause();
        }

        void Start() => HidePanel();

        public void Pause()
        {
            Game.SetPaused(true);
            ShowPanel();
        }

        public void Resume()
        {
            HidePanel();
            Game.SetPaused(false);
        }
    }
}
