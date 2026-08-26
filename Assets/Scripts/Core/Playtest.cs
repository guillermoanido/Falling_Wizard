using System;
using System.Collections.Generic;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.Core
{
    [DefaultExecutionOrder(-100)]
    public class Playtest : MonoBehaviour
    {
        [Header("Playtest")]
        [Tooltip("Tick a spell and press Play with it already learned and in a slot, so you can " +
                 "try it without earning it first. Untick this whole component to play the game " +
                 "properly. Nothing here is ever written to the save - the real one is left " +
                 "exactly as it was.")]
        public bool active = true;

        [Tooltip("Every spell in the book, with a box each. The list fills itself in and keeps " +
                 "itself in step with Assets/Resources/Spellbook.asset, so a spell added later " +
                 "turns up here on its own.")]
        public List<Pick> spells = new List<Pick>();

        [Header("Purse")]
        [Tooltip("Wisps to start with, for trying the skill screen without going and earning them.")]
        [Min(0)] public int wisps = 5;

        [Tooltip("Extra hearts to start with, on top of the base bar.")]
        [Min(0)] public int bonusHearts = 0;

        [Header("Everything")]
        [Tooltip("Learn every spell in the book, whether or not its box is ticked. The ticks " +
                 "still decide which ones are actually IN a slot - there are only four of those, " +
                 "and one of them is the Staff.")]
        public bool learnEverything = false;

        void OnValidate()
        {
#if UNITY_EDITOR
            // Resources.Load during deserialisation is refused with a console warning, and
            // OnValidate runs inside it. Wait a tick and the asset database is open for business.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                    Sync();
            };
#endif
        }

        void Awake()
        {
            if (!active)
                return;

            Sync();

            Progress.BeginSandbox();
            Progress.GiveWisps(wisps);
            Progress.SetHearts(bonusHearts);

            var brought = new List<Ability>();

            foreach (Pick pick in spells)
            {
                if (pick.spell == null)
                    continue;

                if (pick.bring || learnEverything)
                    Progress.Grant(pick.spell.Key);

                if (pick.bring)
                    brought.Add(pick.spell);
            }

            Equip(brought);
        }

        void Equip(List<Ability> brought)
        {
            var missed = new List<string>();

            foreach (Ability spell in brought)
            {
                if (spell.locked)
                    continue;

                int slot = Progress.FirstEmptySlot();

                if (slot < 0)
                {
                    missed.Add(spell.displayName);
                    continue;
                }

                Progress.Equip(slot, spell.Key);
            }

            if (missed.Count > 0)
                Debug.LogWarning($"Playtest could not find a slot for {string.Join(", ", missed)}. " +
                                 "There are four buttons and the Staff is welded to one of them, " +
                                 "so three ticks is the most that can be carried at once.", this);
        }

        void Sync()
        {
            AbilityBook book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

            if (book == null)
                return;

            var kept = new List<Pick>(book.spells.Count);

            foreach (Ability spell in book.spells)
            {
                if (spell == null)
                    continue;

                Pick had = spells.Find(pick => pick.spell == spell);

                kept.Add(had ?? new Pick { spell = spell });
            }

            spells = kept;
        }

        [Serializable]
        public class Pick
        {
            [Tooltip("Bring this one down with you.")]
            public bool bring;

            public Ability spell;
        }
    }
}
