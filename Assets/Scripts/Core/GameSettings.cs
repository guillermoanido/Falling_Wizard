using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    public static class GameSettings
    {
        const string FileName = "settings.json";

        // Stamped into every file written, so the day the shape of this changes an old file can be
        // converted rather than discarded.
        const int Format = 1;

        // The old PlayerPrefs settings, kept only so a machine that last played the previous build
        // can be read once and moved into the file. Nothing writes these any more.
        const string LegacyVolumeKey = "settings.volume";
        const string LegacyFullscreenKey = "settings.fullscreen";
        const string LegacyResolutionWidthKey = "settings.resolution.width";
        const string LegacyResolutionHeightKey = "settings.resolution.height";

        static float volume = 1f;
        static bool fullscreen = true;
        static int resolutionIndex;
        static string language = string.Empty;
        static List<Resolution> resolutions;

        // Set when the settings file is there and would not open. While it is up, Save() refuses
        // to write, for the same reason Progress does: see Load().
        static bool settingsAreUnreadable;
        static bool warnedAboutUnreadable;

        // Whether Load() has run yet. Loc.Load reads Language from here and the ORDER of two
        // BeforeSceneLoad methods in one assembly is not guaranteed, so it asks this rather than
        // assuming - see Loc.Load.
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

        // The language the player chose, as whatever code the localisation work settles on -
        // "en", "es", a locale identifier. EMPTY means "nobody has chosen", which is the signal to
        // follow the system language; it is deliberately not defaulted to English, so that a
        // Spanish player is not shown English once and then remembered as having asked for it.
        // Nothing reads it yet. Setting it and calling Save() is all the language screen has to do;
        // it rides along in the same file as the volume and the resolution.
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
                    // Start on the defaults and refuse to write. Losing a session's volume slider
                    // is nothing; writing default settings over a file we could not open would
                    // throw away a resolution somebody had to fight with to set.
                    settingsAreUnreadable = true;
                    break;

                case SaveRead.Missing:
                    data = ImportLegacyPlayerPrefs();
                    break;
            }

            // Whatever is on this machine right now is the right default: the window Unity just
            // opened, at the size it opened it.
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

            // A width and height, never an index. The index into the resolution list depends on
            // which monitor is plugged in, so a saved index sets the wrong resolution - or an
            // out-of-range one - the first time the game is opened on a different screen. A zero
            // is a hand-edited or half-written file, and means "use the window we already have".
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

        // A one-off rescue for anyone who played the build before settings became a file. Returns
        // what it found so Load can apply it, or null when there is nothing to import.
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

            // Delete the old keys only once the new file is on disk, so a failed write leaves the
            // settings where they are and the import simply runs again next launch.
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

        // The settings file, laid out the way it appears on disk. Public fields and [Serializable]
        // are what JsonUtility can see.
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
