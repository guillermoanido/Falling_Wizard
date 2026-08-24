using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Stat", fileName = "Stat Spell")]
    public class StatAbility : Ability
    {
        [Header("Multipliers (1 = unchanged)")]
        [Tooltip("Top running and walking speed.")]
        [Min(0f)] public float moveSpeed = 1f;

        [Tooltip("How high a full jump goes. 1.5 is half again as high.")]
        [Min(0f)] public float jumpHeight = 1f;

        [Tooltip("How fast falls are. Below 1 is floatier and also lowers terminal speed.")]
        [Min(0f)] public float fallSpeed = 1f;

        [Tooltip("Hearts lost to a long fall. 0 makes the wizard immune to falling.")]
        [Min(0f)] public float fallDamage = 1f;

        [Header("Extras")]
        [Tooltip("Mid-air jumps granted on top of the one off the ground.")]
        [Min(0)] public int extraJumps = 0;

        public override void ModifyStats(PlayerLogic.Modifiers stats)
        {
            stats.MoveSpeedMultiplier *= moveSpeed;
            stats.JumpHeightMultiplier *= jumpHeight;
            stats.FallSpeedMultiplier *= fallSpeed;
            stats.FallDamageMultiplier *= fallDamage;
            stats.ExtraJumps += extraJumps;
        }
    }
}
