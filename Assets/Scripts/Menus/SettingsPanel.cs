using System;
using System.Collections.Generic;
using FallingWizard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class SettingsPanel : MonoBehaviour
    {
        // The slider runs 0 to 1; players read volume as a percentage.
        const float AsPercent = 100f;

        // Cached because Enum.GetValues allocates a fresh array every call, and this is read on
        // every open of the panel.
        static readonly Language[] Languages = (Language[])Enum.GetValues(typeof(Language));

        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

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

            if (languageDropdown != null)
                languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
        }

        void OnEnable()
        {
            ShowCurrentSettings();

            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(backButton.gameObject);
        }

        // Nothing to add for the language: Loc saves it the moment it changes, because a language
        // change repaints the panel you are standing in and there is no "apply" step to wait for.
        void OnDisable() => GameSettings.Save();

        void FillResolutionDropdown()
        {
            var names = new List<string>();
            foreach (Resolution option in GameSettings.Resolutions)
                names.Add($"{option.width} x {option.height}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(names);
        }

        // Every language is written in ITS OWN language, never translated. The player hunting
        // through this list is exactly the one who cannot read the menu it is sitting in, so a
        // list that said "Spanish" in English and "Inglés" in Spanish would be no help to anyone.
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

        // Guarded because the dropdown's index and the enum are two lists that could drift apart -
        // a stale options list left in the prefab, say. Reading past the end of Languages would
        // otherwise throw from inside a UI callback, which swallows the stack trace.
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
