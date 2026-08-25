using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Spellbook", fileName = "Spellbook")]
    public class AbilityBook : ScriptableObject
    {
        public const string ResourcePath = "Spellbook";

        [Header("Catalogue")]
        [Tooltip("Every spell in the game, in the order they are listed on the skill screen. " +
                 "Drag to reorder - nothing in any scene depends on the order.")]
        public List<Ability> spells = new List<Ability>();

        [Header("Starting Kit")]
        [Tooltip("Owned from the first frame of a new game, free. The Staff belongs here.")]
        public List<Ability> known = new List<Ability>();

        public Ability Find(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            foreach (Ability spell in spells)
                if (spell != null && spell.Key == key)
                    return spell;

            return null;
        }

        void OnValidate()
        {
            for (int i = 0; i < spells.Count; i++)
            {
                Ability spell = spells[i];

                if (spell == null || !spell.locked || spell.fixedSlot < 0)
                    continue;

                for (int j = i + 1; j < spells.Count; j++)
                {
                    Ability other = spells[j];

                    if (other != null && other.locked && other.fixedSlot == spell.fixedSlot)
                        Debug.LogWarning($"'{spell.name}' and '{other.name}' both claim slot " +
                                         $"{spell.fixedSlot}, so one will push the other out.", this);
                }
            }

            foreach (Ability spell in known)
            {
                if (spell != null && !spells.Contains(spell))
                    Debug.LogWarning($"'{spell.name}' is in the starting kit but not in the " +
                                     "catalogue, so the skill screen would never list it.", this);
            }
        }
    }
}
