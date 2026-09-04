using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        [Serializable]
        public class Health
        {
            [Header("Health")]
            [Tooltip("Hearts a brand new save starts with, before any heart found in a level.")]
            [Min(1)] public int maxHealth = 5;

            [Tooltip("Most hearts that can ever be added on top, across the whole save. Place " +
                     "fewer hearts than this in the game and the cap never comes up - it is here " +
                     "so a generous level cannot quietly make the wizard unkillable.")]
            [Min(0)] public int maxBonusHearts = 4;

            [Tooltip("Seconds of immunity after a hit, so one hazard cannot chain-kill.")]
            [Min(0f)] public float invulnerabilityTime = 0.6f;

            [NonSerialized] float invulnerableUntil;
            [NonSerialized] int bonus;

            public int Max => maxHealth + bonus;
            public int Bonus => bonus;
            public int Current { get; private set; }
            public bool IsAlive => Current > 0;
            public bool IsInvulnerable => Time.time < invulnerableUntil;

            public int Room => Mathf.Max(0, maxBonusHearts - bonus);
            public bool HasRoomToGrow => Room > 0;

            public void SetBonus(int extra)
            {
                bonus = Mathf.Clamp(extra, 0, maxBonusHearts);
                Current = Mathf.Min(Current, Max);
            }

            public void RestoreToFull() => Current = Max;

            public void TakeDamage(int amount)
            {
                if (amount <= 0 || !IsAlive || IsInvulnerable)
                    return;

                Current = Mathf.Max(0, Current - amount);
                invulnerableUntil = Time.time + invulnerabilityTime;
            }

            public void Heal(int amount)
            {
                if (amount <= 0 || !IsAlive)
                    return;

                Current = Mathf.Min(Max, Current + amount);
            }

            public void Validate()
            {
                maxHealth = Mathf.Max(1, maxHealth);
                maxBonusHearts = Mathf.Max(0, maxBonusHearts);
                invulnerabilityTime = Mathf.Max(0f, invulnerabilityTime);
            }
        }
    }
}
