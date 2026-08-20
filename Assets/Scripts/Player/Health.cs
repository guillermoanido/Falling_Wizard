using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public class Health : MonoBehaviour
    {
        [SerializeField] int maxHealth = 5;

        [Tooltip("Seconds of immunity after taking a hit.")]
        [SerializeField] float invulnerabilityTime = 0.6f;

        float invulnerableUntil;

        public int Max => maxHealth;
        public int Current { get; private set; }
        public bool IsAlive => Current > 0;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        // Raised with (current, max) whenever the value changes. Hook a health bar here.
        public event Action<int, int> Changed;

        public event Action Died;

        void Awake() => Current = maxHealth;

        // Let anything that subscribed in OnEnable draw its starting state.
        void Start() => Changed?.Invoke(Current, maxHealth);

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive || IsInvulnerable)
                return;

            Current = Mathf.Max(0, Current - amount);
            invulnerableUntil = Time.time + invulnerabilityTime;
            Changed?.Invoke(Current, maxHealth);

            if (!IsAlive)
                Died?.Invoke();
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return;

            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }
    }
}
