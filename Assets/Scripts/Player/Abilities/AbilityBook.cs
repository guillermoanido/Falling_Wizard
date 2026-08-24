using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Spellbook", fileName = "Spellbook")]
    public class AbilityBook : ScriptableObject
    {
        public const string ResourcePath = "Spellbook";

        [Header("Order")]
        [Tooltip("Every spell in the game, left to right along the HUD bar. Drag to reorder - " +
                 "nothing in any scene depends on the order.")]
        public List<Ability> spells = new List<Ability>();

        [Header("Starting Kit")]
        [Tooltip("Known from the first frame of a new game. The Staff belongs here.")]
        public List<Ability> known = new List<Ability>();

        void OnValidate()
        {
            for (int i = 0; i < spells.Count; i++)
            {
                Ability spell = spells[i];
                if (spell == null || spell.IsPassive)
                    continue;

                for (int j = i + 1; j < spells.Count; j++)
                {
                    Ability other = spells[j];
                    if (other == null || other.IsPassive)
                        continue;

                    if (other.actionName == spell.actionName)
                        Debug.LogWarning($"'{spell.name}' and '{other.name}' are both on the " +
                                         $"'{spell.actionName}' button, so one press fires both.", this);
                }
            }

            foreach (Ability spell in known)
            {
                if (spell != null && !spells.Contains(spell))
                    Debug.LogWarning($"'{spell.name}' is in the starting kit but not in the " +
                                     "order list, so it would have no place on the bar.", this);
            }
        }
    }
}
