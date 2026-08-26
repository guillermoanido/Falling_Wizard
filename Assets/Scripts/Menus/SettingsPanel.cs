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

        [SerializeField] TMP_Dropdown resolutionDropdown;
        [SerializeField] Toggle fullscreenToggle;
        [SerializeField] Slider volumeSlider;
        [SerializeField] TMP_Text volumeValueLabel;
        [SerializeField] Button backButton;

        [Tooltip("Rows that only make sense on desktop. Hidden on consoles, which pick their own output mode.")]
        [SerializeField] GameObject[] desktopOnlyRows;

        public event Action Closed;

        void Awake()
        {
            FillResolutionDropdown();

            resolutionDropdown.onValueChanged.AddListener(index => GameSettings.ResolutionIndex = index);
            fullscreenToggle.onValueChanged.AddListener(on => GameSettings.Fullscreen = on);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
            backButton.onClick.AddListener(() => Closed?.Invoke());

            foreach (GameObject row in desktopOnlyRows)
                row.SetActive(GameSettings.DisplaySettingsSupported);
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

        void ShowCurrentSettings()
        {
            resolutionDropdown.SetValueWithoutNotify(GameSettings.ResolutionIndex);
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
                volumeValueLabel.text = $"{Mathf.RoundToInt(value * AsPercent)}%";
        }
    }
}
