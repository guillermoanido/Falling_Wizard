using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    public static class GameSettings
    {
        const string FileName = "settings.json";

        const int Format = 1;

        const string LegacyVolumeKey = "settings.volume";
        const string LegacyFullscreenKey = "settings.fullscreen";
        const string LegacyResolutionWidthKey = "settings.resolution.width";
        const string LegacyResolutionHeightKey = "settings.resolution.height";

        static float volume = 1f;
        static bool fullscreen = true;
        static int resolutionIndex;
        static string language = string.Empty;
        static List<Resolution> resolutions;

        static bool settingsAreUnreadable;
        static bool warnedAboutUnreadable;

        public static bool Loaded { get; private set; }

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

        public static string Language
        {
            get => language;
            set => language = value ?? string.Empty;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            settingsAreUnreadable = false;
            warnedAboutUnreadable = false;

            SettingsData data = null;

            switch (SaveFile.Read(FileName, out SettingsData stored))
            {
                case SaveRead.Loaded:
                    data = stored;
                    break;

                case SaveRead.Unreadable:
                    settingsAreUnreadable = true;
                    break;

                case SaveRead.Missing:
                    data = ImportLegacyPlayerPrefs();
                    break;
            }

            data ??= new SettingsData
            {
                version = Format,
                volume = 1f,
                fullscreen = Screen.fullScreen,
                resolutionWidth = Screen.width,
                resolutionHeight = Screen.height,
            };

            Volume = data.volume;
            fullscreen = data.fullscreen;
            Language = data.language;

            resolutionIndex = FindResolutionIndex(
                data.resolutionWidth > 0 ? data.resolutionWidth : Screen.width,
                data.resolutionHeight > 0 ? data.resolutionHeight : Screen.height);

            if (DisplaySettingsSupported)
                ApplyResolution();

            Loaded = true;
        }

        public static void Save()
        {
            if (settingsAreUnreadable)
            {
                if (!warnedAboutUnreadable)
                {
                    warnedAboutUnreadable = true;
                    Debug.LogWarning($"Not saving settings: the file at {SaveFile.PathFor(FileName)} " +
                                     "could not be read when the game started, and writing over it " +
                                     "would throw away whatever is in there. Changes made this " +
                                     "session will not be kept.");
                }

                return;
            }

            Resolution chosen = Resolutions[resolutionIndex];

            SaveFile.Write(FileName, new SettingsData
            {
                version = Format,
                volume = volume,
                fullscreen = fullscreen,
                resolutionWidth = chosen.width,
                resolutionHeight = chosen.height,
                language = language,
            });
        }

        static SettingsData ImportLegacyPlayerPrefs()
        {
            if (!PlayerPrefs.HasKey(LegacyVolumeKey) && !PlayerPrefs.HasKey(LegacyFullscreenKey) &&
                !PlayerPrefs.HasKey(LegacyResolutionWidthKey))
                return null;

            var data = new SettingsData
            {
                version = Format,
                volume = PlayerPrefs.GetFloat(LegacyVolumeKey, 1f),
                fullscreen = PlayerPrefs.GetInt(LegacyFullscreenKey, Screen.fullScreen ? 1 : 0) == 1,
                resolutionWidth = PlayerPrefs.GetInt(LegacyResolutionWidthKey, Screen.width),
                resolutionHeight = PlayerPrefs.GetInt(LegacyResolutionHeightKey, Screen.height),
                language = string.Empty,
            };

            if (SaveFile.Write(FileName, data))
            {
                PlayerPrefs.DeleteKey(LegacyVolumeKey);
                PlayerPrefs.DeleteKey(LegacyFullscreenKey);
                PlayerPrefs.DeleteKey(LegacyResolutionWidthKey);
                PlayerPrefs.DeleteKey(LegacyResolutionHeightKey);
                PlayerPrefs.Save();
            }

            return data;
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

        [Serializable]
        class SettingsData
        {
            public int version;
            public float volume = 1f;
            public bool fullscreen = true;
            public int resolutionWidth;
            public int resolutionHeight;
            public string language = string.Empty;
        }
    }
}
