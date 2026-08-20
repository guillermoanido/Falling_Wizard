using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    public static class GameSettings
    {
        const string VolumeKey = "settings.volume";
        const string FullscreenKey = "settings.fullscreen";
        const string QualityKey = "settings.quality";
        const string ResolutionWidthKey = "settings.resolution.width";
        const string ResolutionHeightKey = "settings.resolution.height";

        static float volume = 1f;
        static bool fullscreen = true;
        static int resolutionIndex;
        static List<Resolution> resolutions;

        public static bool DisplaySettingsSupported =>
            Application.isEditor || Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.LinuxPlayer;

        public static IReadOnlyList<Resolution> Resolutions => resolutions ??= BuildResolutionList();

        public static float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                AudioListener.volume = volume;
            }
        }

        public static bool Fullscreen
        {
            get => fullscreen;
            set
            {
                fullscreen = value;
                ApplyResolution();
            }
        }

        public static int ResolutionIndex
        {
            get => resolutionIndex;
            set
            {
                resolutionIndex = Mathf.Clamp(value, 0, Resolutions.Count - 1);
                ApplyResolution();
            }
        }

        public static int QualityLevel
        {
            get => QualitySettings.GetQualityLevel();
            set => QualitySettings.SetQualityLevel(Mathf.Clamp(value, 0, QualitySettings.names.Length - 1), true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            Volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
            QualityLevel = PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel());

            fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
            resolutionIndex = FindResolutionIndex(
                PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width),
                PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height));

            if (DisplaySettingsSupported)
                ApplyResolution();
        }

        public static void Save()
        {
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(QualityKey, QualitySettings.GetQualityLevel());

            Resolution chosen = Resolutions[resolutionIndex];
            PlayerPrefs.SetInt(ResolutionWidthKey, chosen.width);
            PlayerPrefs.SetInt(ResolutionHeightKey, chosen.height);
            PlayerPrefs.Save();
        }

        static void ApplyResolution()
        {
            if (!DisplaySettingsSupported)
                return;

            Resolution chosen = Resolutions[resolutionIndex];
            FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            Screen.SetResolution(chosen.width, chosen.height, mode, chosen.refreshRateRatio);
        }

        static int FindResolutionIndex(int width, int height)
        {
            for (int i = 0; i < Resolutions.Count; i++)
                if (Resolutions[i].width == width && Resolutions[i].height == height)
                    return i;

            return Resolutions.Count - 1;
        }

        static List<Resolution> BuildResolutionList()
        {
            var best = new Dictionary<(int, int), Resolution>();

            foreach (Resolution option in Screen.resolutions)
            {
                var size = (option.width, option.height);
                if (!best.TryGetValue(size, out Resolution current) ||
                    option.refreshRateRatio.value > current.refreshRateRatio.value)
                    best[size] = option;
            }

            if (best.Count == 0)
            {
                Resolution current = Screen.currentResolution;
                best[(current.width, current.height)] = current;
            }

            var list = new List<Resolution>(best.Values);
            list.Sort((a, b) => a.width != b.width ? a.width.CompareTo(b.width) : a.height.CompareTo(b.height));
            return list;
        }
    }
}
