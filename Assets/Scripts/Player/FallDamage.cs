using System;
using UnityEngine;

namespace FallingWizard.Player
{
    // The core rule: short drops are free, long ones hurt. Turns PlayerMotor.Landed into damage.
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(PlayerPowerUps))]
    public class FallDamage : MonoBehaviour
    {
        [Tooltip("Falls shorter than this many units are free.")]
        [SerializeField] float safeFallDistance = 5f;

        [Tooltip("Damage taken per unit fallen beyond the safe distance.")]
        [SerializeField] float damagePerUnit = 0.6f;

        PlayerMotor motor;
        Health health;
        PlayerPowerUps powerUps;

        public event Action<int> Taken;

        void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            health = GetComponent<Health>();
            powerUps = GetComponent<PlayerPowerUps>();
        }

        void OnEnable() => motor.Landed += HandleLanded;

        void OnDisable() => motor.Landed -= HandleLanded;

        void HandleLanded(float fallDistance)
        {
            float excess = fallDistance - safeFallDistance;
            if (excess <= 0f)
                return;

            int damage = Mathf.RoundToInt(excess * damagePerUnit * powerUps.FallDamageMultiplier);
            if (damage <= 0)
                return;

            health.TakeDamage(damage);
            Taken?.Invoke(damage);
        }
    }
}
