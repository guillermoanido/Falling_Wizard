using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    public enum Language
    {
        English,
        Spanish,
    }

    public static class Loc
    {
        const string TableFolder = "Language/";

        public const string AbilityPrefix = "ability.";

        public static event Action Changed;

        static readonly HashSet<string> Warned = new HashSet<string>();

        static LanguageTable table;

        public static Language Language { get; private set; } = Language.English;

        public static IReadOnlyDictionary<string, string> English => Source;

        public static string NameOf(Language language)
        {
            switch (language)
            {
                case Language.Spanish: return "Español";
                default: return "English";
            }
        }

        public static void Set(Language language)
        {
            if (Language == language)
                return;

            Language = language;
            table = FindTable(language);

            Warned.Clear();

            GameSettings.Language = CodeFor(language);
            GameSettings.Save();

            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return string.Empty;

            if (table != null && table.TryFind(key, out string translated))
                return translated;

            if (Source.TryGetValue(key, out string english))
                return english;

            Warn(key, $"Nothing in the game defines the string '{key}', so the key itself is being " +
                      "shown. Either the key is misspelt where it is asked for, or it needs " +
                      "adding to the English table in Loc.cs.");

            return key;
        }

        public static string Text(string key, string english)
        {
            if (string.IsNullOrEmpty(key))
                return english;

            if (table != null && table.TryFind(key, out string translated))
                return translated;

            return !string.IsNullOrEmpty(english)
                ? english
                : Source.TryGetValue(key, out string source) ? source : string.Empty;
        }

        public static string Format(string key, params object[] values)
        {
            string pattern = Get(key);
            string filled = Fill(key, pattern, values);

            if (filled != null)
                return filled;

            string source = Source.TryGetValue(key, out string english) ? english : pattern;

            return Fill(key, source, values) ?? source;
        }

        static string Fill(string key, string pattern, object[] values)
        {
            try
            {
                return string.Format(pattern, values);
            }
            catch (FormatException)
            {
                Warn(key, $"'{key}' is written with a placeholder the game does not fill in: " +
                          $"\"{pattern}\". It is given {values.Length} value(s), so the highest " +
                          $"number it may use is {{{values.Length - 1}}}.");
                return null;
            }
        }

        static void Warn(string key, string message)
        {
            if (Warned.Add(key))
                Debug.LogWarning(message);
        }

        static LanguageTable FindTable(Language language)
        {
            LanguageTable found = Resources.Load<LanguageTable>(TableFolder + language);

            if (found == null && language != Language.English)
                Debug.LogWarning($"There is no {language} translation at " +
                                 $"Assets/Resources/{TableFolder}{language}.asset, so the game " +
                                 "will read in English. Make one with Assets > Create > Falling " +
                                 $"Wizard > Language Table and name the file exactly '{language}'.");

            return found;
        }

        static string CodeFor(Language language) => language == Language.Spanish ? "es" : "en";

        static Language FromCode(string code) => code == "es" ? Language.Spanish : Language.English;

        static Language FromSystem() =>
            Application.systemLanguage == SystemLanguage.Spanish
                ? Language.Spanish
                : Language.English;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            if (!GameSettings.Loaded)
                GameSettings.Load();

            string saved = GameSettings.Language;

            Language = string.IsNullOrEmpty(saved) ? FromSystem() : FromCode(saved);

            table = FindTable(Language);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Changed = null;
            table = null;
            Warned.Clear();
        }

        public static class Keys
        {
            public const string SkillTitle = "skill.title";
            public const string SkillPurse = "skill.purse";
            public const string SkillPrice = "skill.price";
            public const string SkillNoBook = "skill.noBook";
            public const string SkillDive = "skill.dive";
            public const string SkillBack = "skill.back";
            public const string SkillHintPick = "skill.hint.pick";
            public const string SkillHintMove = "skill.hint.move";
            public const string SkillHintLocked = "skill.hint.locked";
            public const string SkillOn = "skill.on";
            public const string SkillBench = "skill.bench";
            public const string SkillNext = "skill.next";
            public const string SkillLearn = "skill.learn";
            public const string SkillMastered = "skill.mastered";
            public const string SkillLearned = "skill.learned";

            public const string DeathTitle = "death.title";
            public const string DeathBlurb = "death.blurb";
            public const string DeathStatus = "death.status";
            public const string DeathContinue = "death.continue";
            public const string DeathGiveUp = "death.giveUp";

            public const string RestTitle = "rest.title";
            public const string RestBlurb = "rest.blurb";
            public const string RestStatus = "rest.status";
            public const string RestPressOn = "rest.pressOn";
            public const string RestTurnBack = "rest.turnBack";

            public const string HudWisps = "hud.wisps";
        }

        static readonly Dictionary<string, string> Source = new Dictionary<string, string>
        {
            { "menu.title", "Falling Wizard" },
            { "menu.play", "Play" },
            { "menu.settings", "Settings" },
            { "menu.exit", "Exit" },

            { "pause.title", "Paused" },
            { "pause.resume", "Resume" },
            { "pause.mainMenu", "Main Menu" },
            { "pause.quit", "Quit" },

            { "settings.title", "Settings" },
            { "settings.resolution", "Resolution" },
            { "settings.fullscreen", "Fullscreen" },
            { "settings.volume", "Volume" },
            { "settings.language", "Language" },
            { "settings.back", "Back" },

            { Keys.SkillTitle, "What you carry down" },
            { Keys.SkillPurse, "{0} wisps" },
            { Keys.SkillPrice, "{0} wisps" },
            { Keys.SkillNoBook, "No spellbook found at Assets/Resources/Spellbook.asset." },
            { Keys.SkillDive, "Descend" },
            { Keys.SkillBack, "Back to the fall" },
            { Keys.SkillHintPick, "Pick a spell, then press the button you want it on." },
            { Keys.SkillHintMove, "{0}: press {1} to move it." },
            { Keys.SkillHintLocked, "{0} is not learned yet." },
            { Keys.SkillOn, "On {0}." },
            { Keys.SkillBench, "On the bench." },
            { Keys.SkillNext, "{0}  Next: {1} - {2}" },
            { Keys.SkillLearn, "Learn - {0}" },
            { Keys.SkillMastered, "Mastered" },
            { Keys.SkillLearned, "Learned" },

            { Keys.DeathTitle, "You fell" },
            { Keys.DeathBlurb, "The wisps you were carrying went out with you, and are back " +
                               "where you found them." },
            { Keys.DeathStatus, "{0} wisps still banked" },
            { Keys.DeathContinue, "Take it from the last rest" },
            { Keys.DeathGiveUp, "Give up the run and go back" },

            { Keys.RestTitle, "A place to rest" },
            { Keys.RestBlurb, "Further down, or back the way you came." },
            { Keys.RestStatus, "Carrying {0} wisps    {1} already banked    {2}/{3} hearts" },
            { Keys.RestPressOn, "Rest, then press on" },
            { Keys.RestTurnBack, "Turn back and bank {0} wisps" },

            { Keys.HudWisps, "{0} carried    {1} banked" },
        };
    }
}
