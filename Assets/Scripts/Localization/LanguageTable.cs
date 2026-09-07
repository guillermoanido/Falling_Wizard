using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FallingWizard.Core
{
    [CreateAssetMenu(menuName = "Falling Wizard/Language Table", fileName = "Spanish")]
    public class LanguageTable : ScriptableObject
    {
        [Header("Translation")]
        [Tooltip("One line per string the game shows. The Key is not a sentence - it is the label " +
                 "the game looks the sentence up by, and it must match one of the English keys " +
                 "exactly, letter for letter. Anything missing from this list, or left blank, is " +
                 "shown in English instead, so it is always safe to translate a few lines at a " +
                 "time and play what you have.")]
        public List<Line> lines = new List<Line>();

        readonly Dictionary<string, string> lookup = new Dictionary<string, string>();
        bool built;

        public bool TryFind(string key, out string text)
        {
            if (!built)
                Rebuild();

            return lookup.TryGetValue(key, out text) && !string.IsNullOrEmpty(text);
        }

        void OnEnable() => built = false;

        void Rebuild()
        {
            lookup.Clear();
            built = true;

            foreach (Line line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.key))
                    continue;

                lookup[line.key] = line.text;
            }
        }

        void OnValidate()
        {
            built = false;

            var seen = new HashSet<string>();

            foreach (Line line in lines)
            {
                if (line == null || string.IsNullOrEmpty(line.key))
                    continue;

                if (!seen.Add(line.key))
                    Debug.LogWarning($"'{line.key}' is in this table twice. The one lower down " +
                                     "wins and the other is dead text, which is usually a line " +
                                     "that was copied to make the next one and never renamed.",
                                     this);

                if (line.key.StartsWith(Loc.AbilityPrefix, StringComparison.Ordinal))
                    continue;

                if (!Loc.English.ContainsKey(line.key))
                    Debug.LogWarning($"'{line.key}' is not a key the game ever asks for, so this " +
                                     "line will never be shown to anybody. Check it against the " +
                                     "English table at the bottom of Loc.cs - nine times in ten " +
                                     "it is a typo or a key that has since been renamed.", this);
            }
        }

        [ContextMenu("List Missing Strings")]
        void ListMissing()
        {
            Rebuild();

            var missing = new StringBuilder();
            int count = 0;

            foreach (KeyValuePair<string, string> entry in Loc.English)
            {
                if (lookup.TryGetValue(entry.Key, out string text) && !string.IsNullOrEmpty(text))
                    continue;

                missing.Append("\n  ").Append(entry.Key).Append("  =  ").Append(entry.Value);
                count++;
            }

            if (count == 0)
            {
                Debug.Log($"'{name}' has every string Loc.cs asks for. Spell names and " +
                          "descriptions are not counted here - those are keyed by each spell's " +
                          $"own id, as {Loc.AbilityPrefix}<id>.name and .desc.", this);
                return;
            }

            Debug.Log($"'{name}' is missing {count} string(s), which will show in English:" +
                      missing, this);
        }

        [Serializable]
        public class Line
        {
            [Tooltip("The English key, copied exactly: 'skill.title', 'rest.blurb', " +
                     "'ability.fling.name'. A spell's key uses its ID, not its name - Mage Hand's " +
                     "id is 'vine' and Telekinesis's is 'hand'.")]
            public string key = "";

            [TextArea]
            [Tooltip("What the player reads. Keep every {0} and {1} the English has - those are " +
                     "where the game drops its numbers in. They may be put in whatever order " +
                     "reads well, but a number that is not there at all leaves a hole in the " +
                     "sentence, and one the game does not fill in shows the English instead.")]
            public string text = "";
        }
    }
}
