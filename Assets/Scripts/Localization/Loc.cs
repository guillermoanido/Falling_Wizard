using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Core
{
    // The languages the game can be played in. The ORDER is the order of the settings dropdown,
    // and the NAME is what goes in the save file - so a language may be added to the end, but
    // never renamed and never reordered, or every player's saved choice points somewhere else.
    public enum Language
    {
        English,
        Spanish,
    }

    // Every word the player reads goes through here.
    //
    // English is the source and lives in this file, right next to the code that asks for it, so a
    // key can never be asked for in one place and defined in another. A TRANSLATION is a
    // LanguageTable asset in Resources/Language, so the Spanish can be reworded in the inspector
    // without a recompile, and a half finished one is safe to play: anything the table has not
    // got yet falls back to the English rather than showing a blank or a raw key.
    public static class Loc
    {
        // Resources.Load path. "Language/Spanish" is Assets/Resources/Language/Spanish.asset.
        const string TableFolder = "Language/";

        // A key filed under an ability is looked up by the spell's own id, so it can never be
        // listed here. LanguageTable's own check has to let this prefix through unquestioned.
        public const string AbilityPrefix = "ability.";

        // Fired after the language has actually changed. Anything showing words subscribes and
        // re-reads; it does not need to know what the new language is.
        public static event Action Changed;

        // Keys already complained about. Without it a bad key in the HUD, which is rebuilt in
        // LateUpdate every single frame, writes sixty warnings a second and the console is gone.
        static readonly HashSet<string> Warned = new HashSet<string>();

        static LanguageTable table;

        public static Language Language { get; private set; } = Language.English;

        // Read-only so LanguageTable can check its own keys against it at author time, and so
        // nothing can quietly add a string at runtime that no translation will ever cover.
        public static IReadOnlyDictionary<string, string> English => Source;

        // A language is always named in ITS OWN language. The player hunting through this list is
        // exactly the person who cannot read the menu it is sitting in, and "Spanish" is no use
        // to them. That is why these are here and not in the tables.
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

            // The new table has its own holes, so old complaints say nothing about it.
            Warned.Clear();

            // Through GameSettings, which already owns settings.json and already has a slot for
            // this. Written the moment it changes rather than on SettingsPanel.OnDisable, because
            // a language change repaints the menu you are standing in - there is no "apply" step
            // to hang it on.
            GameSettings.Language = CodeFor(language);
            GameSettings.Save();

            Changed?.Invoke();
        }

        // What the player reads for this key. Table, then English, then the key itself - which is
        // ugly on purpose, because a key on screen is a bug you want to see.
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

        // For a string that carries its own English on the object that shows it - a rest site's
        // own heading, a spell's displayName. Leave the key empty and the typed English is used
        // exactly as typed, which is how a one-off rest site written for one level stays written.
        public static string Text(string key, string english)
        {
            if (string.IsNullOrEmpty(key))
                return english;

            if (table != null && table.TryFind(key, out string translated))
                return translated;

            // The English typed on the OBJECT wins over anything filed under the same key here.
            // Only a caller that carries its own words reaches this method, and those words are
            // the ones a designer typed into that particular rest site - swapping in a shared
            // string because the keys happen to collide would silently rewrite their level.
            return !string.IsNullOrEmpty(english)
                ? english
                : Source.TryGetValue(key, out string source) ? source : string.Empty;
        }

        // Get, with the numbers dropped in.
        public static string Format(string key, params object[] values)
        {
            string pattern = Get(key);
            string filled = Fill(key, pattern, values);

            if (filled != null)
                return filled;

            // The translation is broken, so fall back to the English, which is ours and is the one
            // written against the values this caller actually passes.
            string source = Source.TryGetValue(key, out string english) ? english : pattern;

            return Fill(key, source, values) ?? source;
        }

        // Null rather than a throw. A translator typing {2} into a line that only ever gets two
        // values would otherwise unwind out of the middle of SkillScreen.Redraw and leave a half
        // built screen with no button on it to get back out of.
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
            // English is the source and lives in this file, so it needs no asset at all. One may
            // still be dropped in at Resources/Language/English.asset to fix a typo in a shipped
            // build without a recompile, and it will win.
            LanguageTable found = Resources.Load<LanguageTable>(TableFolder + language);

            if (found == null && language != Language.English)
                Debug.LogWarning($"There is no {language} translation at " +
                                 $"Assets/Resources/{TableFolder}{language}.asset, so the game " +
                                 "will read in English. Make one with Assets > Create > Falling " +
                                 $"Wizard > Language Table and name the file exactly '{language}'.");

            return found;
        }

        // Two-letter codes, because that is what GameSettings.Language documents itself as
        // holding and what somebody hand-editing settings.json would expect to find in it.
        static string CodeFor(Language language) => language == Language.Spanish ? "es" : "en";

        static Language FromCode(string code) => code == "es" ? Language.Spanish : Language.English;

        static Language FromSystem() =>
            Application.systemLanguage == SystemLanguage.Spanish
                ? Language.Spanish
                : Language.English;

        // Alongside GameSettings.Load and Progress.Load. Every BeforeSceneLoad method has finished
        // before the first scene's Awake runs, so a LocalizedText waking up in the main menu
        // always finds the language already chosen.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load()
        {
            // The ORDER of two BeforeSceneLoad methods in one assembly is not guaranteed, and
            // this one reads what that one loads. Asking is cheap and idempotent; guessing is a
            // language that silently resets itself on roughly half of all launches.
            if (!GameSettings.Loaded)
                GameSettings.Load();

            string saved = GameSettings.Language;

            // Empty is not "English": it is "nobody has said". A Spanish machine should open in
            // Spanish rather than be shown English once and then remembered as having asked for
            // it.
            Language = string.IsNullOrEmpty(saved) ? FromSystem() : FromCode(saved);

            table = FindTable(Language);
        }

        // Domain reload can be switched off in Enter Play Mode Settings, and then a static event
        // keeps every subscriber from the LAST play session - all of them pointing at objects that
        // were destroyed when play stopped. The first language change would throw a
        // MissingReferenceException per dead listener before reaching any live one.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Changed = null;
            table = null;
            Warned.Clear();
        }

        // The keys the C# asks for by hand. Keys that only ever get typed into an inspector field
        // - menu.*, settings.*, pause.* - are not here, because nothing would be checking them.
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

        // THE ENGLISH. This is the source text, not a translation of anything, and it is the list
        // a LanguageTable is checked against. Adding a string to the game is one line here.
        //
        // Spell names and descriptions are deliberately absent: those live on the .asset files
        // themselves, and Ability.Name passes them to Text() as the fallback, so there is only
        // ever one English for a spell and it is the one in the inspector.
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
