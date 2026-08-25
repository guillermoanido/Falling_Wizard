using FallingWizard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class MainMenuController : MenuScreen
    {
        [Header("Buttons")]
        [SerializeField] Button playButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        protected override Selectable DefaultSelection => playButton;

        protected override void WireButtons()
        {
            playButton.onClick.AddListener(Game.StartRun);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(Game.Quit);
        }

        protected override void OnBackPressed()
        {
            if (IsSettingsOpen)
                CloseSettings();
        }

        void Start() => ShowPanel();
    }
}
