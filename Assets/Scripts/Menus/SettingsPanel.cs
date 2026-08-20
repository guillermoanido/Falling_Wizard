using System;
using System.Collections.Generic;
using FallingWizard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FallingWizard.Menus
{
    public class SettingsPanel : MonoBehaviour
    {
        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] TMP_Dropdown qualityDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

        [Tooltip("Rows that only make sense on desktop. Hidden on consoles, which pick their own output mode.")]
        [SerializeField] GameObject[] desktopOnlyRows;

        public event Action Closed;

        void Awake()
        {
            FillDropdowns();

            resolutionDropdown.onValueChanged.AddListener(index => GameSettings.ResolutionIndex = index);
            qualityDropdown.onValueChanged.AddListener(index => GameSettings.QualityLevel = index);
            fullscreenToggle.onValueChanged.AddListener(on => GameSettings.Fullscreen = on);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            backButton.onClick.AddListener(() => Closed?.Invoke());

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
        }

        void OnEnable()
        {
            ShowCurrentSettings();
            MenuFocus.Set(backButton);
        }

        void OnDisable() => GameSettings.Save();

        void FillDropdowns()
        {
            var resolutionNames = new List<string>();
            foreach (Resolution option in GameSettings.Resolutions)
                resolutionNames.Add($"{option.width} x {option.height}");

            resolutionDropdown.ClearOptions();
            resolutionDropdown.AddOptions(resolutionNames);

            qualityDropdown.ClearOptions();
            qualityDropdown.AddOptions(new List<string>(QualitySettings.names));
        }

        void ShowCurrentSettings()
        {
            resolutionDropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex);
            qualityDropdown.SetValueWithoutNotify(GameSettings.QualityLevel);
            fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            UpdateVolumeLabel(GameSettings.Volume);
        }

        void OnVolumeChanged(float value)
        {
            GameSettings.Volume = value;
            UpdateVolumeLabel(value);
        }

        void UpdateVolumeLabel(float value)
        {
            if (volumeValueLabel != null)
                volumeValueLabel.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
