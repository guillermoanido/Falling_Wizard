using FallingWizard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class PauseMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject pausePanel;
        [SerializeField] SettingsPanel settingsPanel;

        [Header("Buttons")]
        [SerializeField] Button resumeButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button mainMenuButton;
        [SerializeField] Button quitButton;

        void Awake()
        {
            resumeButton.onClick.AddListener(Resume);
            settingsButton.onClick.AddListener(OpenSettings);
            mainMenuButton.onClick.AddListener(SceneLoader.LoadMainMenu);
            quitButton.onClick.AddListener(SceneLoader.QuitGame);
            settingsPanel.Closed += CloseSettings;
        }

        void OnDestroy() => settingsPanel.Closed -= CloseSettings;

        void Start()
        {
            settingsPanel.gameObject.SetActive(false);
            pausePanel.SetActive(false);
        }

        void Update()
        {
            if (!MenuInput.PausePressedThisFrame)
                return;

            if (settingsPanel.gameObject.activeSelf)
                CloseSettings();
            else if (GamePause.IsPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            GamePause.SetPaused(true);
            pausePanel.SetActive(true);
            MenuFocus.Set(resumeButton);
        }

        public void Resume()
        {
            settingsPanel.gameObject.SetActive(false);
            pausePanel.SetActive(false);
            GamePause.SetPaused(false);
        }

        void OpenSettings()
        {
            pausePanel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        void CloseSettings()
        {
            settingsPanel.gameObject.SetActive(false);
            pausePanel.SetActive(true);
            MenuFocus.Set(resumeButton);
        }
    }
}
