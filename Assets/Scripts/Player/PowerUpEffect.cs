using System;
using UnityEngine;

namespace FallingWizard.Player
{
    [Serializable]
    public class PowerUpEffect
    {
        [Tooltip("Shown in the log and used to recognise the same power up being picked up twice.")]
        public string displayName = "Power Up";

        [Tooltip("How long the multipliers below last, in seconds. 0 means instant, nothing to expire.")]
        public float duration = 10f;

        [Tooltip("Hit points restored the moment it is picked up.")]
        public int healAmount;

        [Tooltip("Multiplies top running speed.")]
        public float speedMultiplier = 1f;

        [Tooltip("Multiplies jump height.")]
        public float jumpMultiplier = 1f;

        [Tooltip("Multiplies fall speed. Below 1 makes the wizard drift down gently.")]
        public float fallSpeedMultiplier = 1f;

        [Tooltip("Multiplies fall damage. 0 makes long drops harmless.")]
        public float fallDamageMultiplier = 1f;

        [Tooltip("Extra mid-air jumps while it lasts.")]
        public int extraJumps;

        public bool IsTimed => duration > 0f;
    }
}
