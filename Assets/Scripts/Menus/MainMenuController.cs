using FallingWizard.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] GameObject mainPanel;
        [SerializeField] SettingsPanel settingsPanel;

        [Header("Buttons")]
        [SerializeField] Button playButton;
        [SerializeField] Button settingsButton;
        [SerializeField] Button exitButton;

        void Awake()
        {
            playButton.onClick.AddListener(SceneLoader.StartNewGame);
            settingsButton.onClick.AddListener(OpenSettings);
            exitButton.onClick.AddListener(SceneLoader.QuitGame);
            settingsPanel.Closed += CloseSettings;
        }

        void OnDestroy() => settingsPanel.Closed -= CloseSettings;

        void Start() => CloseSettings();

        void Update()
        {
            if (MenuInput.PausePressedThisFrame && settingsPanel.gameObject.activeSelf)
                CloseSettings();
        }

        void OpenSettings()
        {
            mainPanel.SetActive(false);
            settingsPanel.gameObject.SetActive(true);
        }

        void CloseSettings()
        {
            settingsPanel.gameObject.SetActive(false);
            mainPanel.SetActive(true);
            MenuFocus.Set(playButton);
        }
    }
}
