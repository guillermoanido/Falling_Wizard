using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class SettingsPanel : MonoBehaviour
    {
        const float AsPercent = 100f;

        const float ResetButtonWidth = 260f;
        const float ResetButtonHeight = 56f;

        static readonly Language[] Languages = (Language[])Enum.GetValues(typeof(Language));

        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

        [Tooltip("Wipes every spell, wisp and heart and returns to the main menu, after asking. " +
                 "Leave it empty and the panel builds its own beside the Back button, so the " +
                 "Pause Menu prefab and the Main Menu scene both get one without being rewired.")]
        [SerializeField] Button resetSaveButton;

        [Tooltip("The language row's dropdown. This rig exists twice - once inside the Pause Menu " +
                 "prefab and once inside the Main Menu scene - so leaving it empty is allowed and " +
                 "the panel simply carries on with no language row, rather than throwing on Awake " +
                 "while the second copy is still being wired up.")]
        [SerializeField] TMP_Dropdown languageDropdown;

        [Tooltip("Rows that only make sense on desktop. Hidden on consoles, which pick their own " +
                 "output mode. The language row does NOT belong in here - a console player picks " +
                 "their language too.")]
        [SerializeField] GameObject[] desktopOnlyRows;

        public event Action Closed;

        void Awake()
        {
            FillResolutionDropdown();
            FillLanguageDropdown();

            resolutionDropdown.onValueChanged.AddListener(index => GameSettings.ResolutionIndex = index);
            fullscreenToggle.onValueChanged.AddListener(on => GameSettings.Fullscreen = on);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            backButton.onClick.AddListener(() => Closed?.Invoke());

            EnsureResetButton();
            resetSaveButton.onClick.AddListener(AskToResetSave);

            if (languageDropdown != null)
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
        }

        void EnsureResetButton()
        {
            if (resetSaveButton != null)
                return;

            resetSaveButton = UI.Ui.CreateButton(Loc.Text("settings.resetSave", "Reset Save"),
                backButton.transform.parent, ResetButtonWidth, ResetButtonHeight);

            resetSaveButton.transform.SetSiblingIndex(backButton.transform.GetSiblingIndex());
        }

        void AskToResetSave()
        {
            UI.ChoiceScreen screen = UI.ChoiceScreen.Open(
                Loc.Text("settings.resetSave.title", "Reset save?"),
                Loc.Text("settings.resetSave.blurb",
                    "Every spell, wisp and heart you have earned is erased, and the run starts " +
                    "again from the beginning. This cannot be undone."));

            screen.Status(string.Format(
                Loc.Text("settings.resetSave.status", "You have {0} wisps banked."),
                Progress.Wisps));

            screen.Choice(Loc.Text("settings.resetSave.confirm", "Erase everything"),
                () => screen.CloseThen(Wipe));

            screen.Choice(Loc.Text("settings.resetSave.cancel", "Keep my save"), screen.Close);
        }

        static void Wipe()
        {
            Progress.ResetSave();
            Game.LoadMainMenu();
        }

        void OnEnable()
        {
            ShowCurrentSettings();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
        }

        void OnDisable() => GameSettings.Save();

        void FillResolutionDropdown()
        {
            var names = new List<string>();
            foreach (Resolution option in GameSettings.Resolutions)
                names.Add($"{option.width} x {option.height}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(names);
        }

        void FillLanguageDropdown()
        {
            if (languageDropdown == null)
                return;

            var names = new List<string>();
            foreach (Language option in Languages)
                names.Add(Loc.NameOf(option));

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(names);
        }

        void ShowCurrentSettings()
        {
            resolutionDropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            UpdateVolumeLabel(GameSettings.Volume);

            if (languageDropdown != null)
                languageDropdown.SetValueWithoutNotify(Array.IndexOf(Languages, Loc.Language));
        }

        void OnVolumeChanged(float value)
        {
            GameSettings.Volume = value;
            UpdateVolumeLabel(value);
        }

        void OnLanguageChanged(int index)
        {
            if ((uint)index >= Languages.Length)
                return;

            Loc.Set(Languages[index]);
        }

        void UpdateVolumeLabel(float value)
        {
            if (volumeValueLabel != null)
                volumeValueLabel.text = $"{Mathf.RoundToInt(value * AsPercent)}%";
        }
    }
}
