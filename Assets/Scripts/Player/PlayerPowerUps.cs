using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    // Holds the power ups the wizard is carrying and boils them down to the multipliers that
    // PlayerMotor and FallDamage read. With nothing active every multiplier is 1.
    [DisallowMultipleComponent]
    public class PlayerPowerUps : MonoBehaviour
    {
        readonly List<Active> active = new List<Active>();

        Health health;

        public float SpeedMultiplier { get; private set; } = 1f;
        public float JumpMultiplier { get; private set; } = 1f;
        public float FallSpeedMultiplier { get; private set; } = 1f;
        public float FallDamageMultiplier { get; private set; } = 1f;
        public int ExtraJumps { get; private set; }

        public event Action Changed;

        void Awake() => health = GetComponent<Health>();

        public void Apply(PowerUpEffect effect)
        {
            if (effect == null)
                return;

            if (effect.healAmount > 0 && health != null)
                health.Heal(effect.healAmount);

            if (!effect.IsTimed)
                return;

            // Picking up the same power up again refreshes it rather than stacking it.
            Active existing = active.Find(entry => entry.Effect.displayName == effect.displayName);
            if (existing != null)
                existing.SecondsLeft = effect.duration;
            else
                active.Add(new Active { Effect = effect, SecondsLeft = effect.duration });

            Recalculate();
        }

        public void ClearAll()
        {
            if (active.Count == 0)
                return;

            active.Clear();
            Recalculate();
        }

        void Update()
        {
            // Time.deltaTime is 0 while paused, so power ups do not burn away in the menu.
            bool anyExpired = false;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                active[i].SecondsLeft -= Time.deltaTime;
                if (active[i].SecondsLeft > 0f)
                    continue;

                active.RemoveAt(i);
                anyExpired = true;
            }

            if (anyExpired)
                Recalculate();
        }

        void Recalculate()
        {
            SpeedMultiplier = 1f;
            JumpMultiplier = 1f;
            FallSpeedMultiplier = 1f;
            FallDamageMultiplier = 1f;
            ExtraJumps = 0;

            foreach (Active entry in active)
            {
                SpeedMultiplier *= entry.Effect.speedMultiplier;
                JumpMultiplier *= entry.Effect.jumpMultiplier;
                FallSpeedMultiplier *= entry.Effect.fallSpeedMultiplier;
                FallDamageMultiplier *= entry.Effect.fallDamageMultiplier;
                ExtraJumps += entry.Effect.extraJumps;
            }

            Changed?.Invoke();
        }

        class Active
        {
            public PowerUpEffect Effect;
            public float SecondsLeft;
        }
    }
}
