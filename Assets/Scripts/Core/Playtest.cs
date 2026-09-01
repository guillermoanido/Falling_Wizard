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

        [Tooltip("Deal the kit again every time the level loads. OFF - the default - deals it " +
                 "once when you press Play and then leaves it alone, so a loadout you rearranged " +
                 "at the skill screen survives dying and restarting. That is what this being on " +
                 "by accident used to break. Turn it on while you are tuning the ticks.")]
        public bool redealOnEveryLoad = false;

        [Tooltip("The catalogue the list below is built from. Filled in when the component is " +
                 "added; it is a field rather than something looked up on the fly so the list " +
                 "can be kept in step from OnValidate, where changes actually get saved.")]
        public AbilityBook book;

        [Tooltip("Every spell in the book, with a box each. The list fills itself in and keeps " +
                 "itself in step with the catalogue, so a spell added later turns up on its own.")]
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

        [Tooltip("Say in the console what was handed out. Worth leaving on: if a slot comes up " +
                 "empty this is how you tell whether it was this component or something later.")]
        public bool announce = true;

        void Reset()
        {
            book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);
            Sync();
        }

        // Straight in OnValidate whenever it can be. A field written from delayCall is changed
        // on the live object ONLY - nothing marks the component dirty, so the list you are
        // looking at never reaches the scene file, and entering Play Mode deserialises an empty
        // one back over it. That is why the book is a field: with one here no Resources.Load is
        // needed, which was the only reason for deferring in the first place.
        void OnValidate()
        {
            if (book != null)
            {
                Sync();
                return;
            }

#if UNITY_EDITOR
            // No book yet - a component added before this field existed, most likely. Finding one
            // does need the asset database, so that part still waits a tick, and SetDirty is what
            // makes the result actually stick.
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || book != null)
                    return;

                book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

                if (book == null)
                    return;

                Sync();
                UnityEditor.EditorUtility.SetDirty(this);
            };
#endif
        }

        void Awake()
        {
            if (!active)
                return;

            if (book == null)
                book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

            if (book == null)
            {
                Debug.LogError("Playtest has no spellbook, so it cannot hand anything out. Put " +
                               "Assets/Resources/Spellbook.asset in its Book field.", this);
                return;
            }

            // Already dealt this session, so touch nothing: what is in Progress is the
            // loadout the player last chose, and re-dealing here is exactly what made every
            // restart forget it. The Staff still lands - Spellbook.Attach grants book.known and
            // Reload re-seeds any owned locked spell that is not in a slot.
            if (Progress.SandboxSeeded && !redealOnEveryLoad)
            {
                if (announce)
                    Debug.Log("Playtest already dealt this session - keeping the loadout you " +
                              "left with. Tick 'Redeal On Every Load' to start from the ticks " +
                              "each time.", this);
                return;
            }

            Progress.BeginSandbox(reseed: true);
            Progress.GiveWisps(wisps);
            Progress.SetHearts(bonusHearts);

            Reserve();
            Hand();
        }

        // Spells welded to a slot get theirs before anything else is placed. Otherwise the
        // Staff's own slot is filled by a ticked spell, and the spellbook evicts it a moment
        // later when it puts the Staff where it belongs.
        void Reserve()
        {
            foreach (Ability spell in book.spells)
            {
                if (spell == null || !spell.locked || spell.fixedSlot < 0)
                    continue;

                Progress.Grant(spell.Key);
                Progress.Equip(spell.fixedSlot, spell.Key);
            }
        }

        void Hand()
        {
            var carried = new List<string>();
            var missed = new List<string>();

            foreach (Pick pick in spells)
            {
                if (pick.spell == null)
                    continue;

                if (pick.bring || learnEverything)
                {
                    Progress.Grant(pick.spell.Key);

                    if (pick.rank > 1)
                        Progress.SetRank(pick.spell.Key,
                            Mathf.Min(pick.rank, pick.spell.MaxRank));
                }

                if (!pick.bring || pick.spell.locked)
                    continue;

                int slot = Progress.FirstEmptySlot();

                if (slot < 0)
                {
                    missed.Add(pick.spell.displayName);
                    continue;
                }

                Progress.Equip(slot, pick.spell.Key);
                carried.Add($"{pick.spell.displayName} in slot {slot + 1}");
            }

            if (missed.Count > 0)
                Debug.LogWarning($"Playtest could not find a slot for {string.Join(", ", missed)}. " +
                                 "There are four buttons and the Staff is welded to one of them, " +
                                 "so three ticks is the most that can be carried at once.", this);

            if (!announce)
                return;

            if (spells.Count == 0)
                Debug.LogWarning("Playtest has an empty spell list. Press the Book field's picker " +
                                 "or re-add the component to fill it in.", this);
            else if (carried.Count == 0)
                Debug.Log("Playtest brought nothing - no boxes are ticked.", this);
            else
                Debug.Log($"Playtest brought {string.Join(", ", carried)}.", this);
        }

        void Sync()
        {
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

            [Tooltip("Rank to start it at, for trying an upgrade without buying it. 1 is just " +
                     "learning it. Clamped to the spell's own top rank.")]
            [Min(1)] public int rank = 1;

            public Ability spell;
        }
    }
}
