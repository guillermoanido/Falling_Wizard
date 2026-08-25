using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        [Header("Glide")]
        [Tooltip("Fall speed while gliding, as a fraction of normal. 0.25 is a quarter speed.")]
        [Range(0.05f, 1f)] public float fallSpeed = 0.25f;

        [Tooltip("Forget the drop so far for as long as this is lit. Without it a slow fall is " +
                 "not a safe one - fall damage counts boxes fallen, not how fast, so gliding " +
                 "down a killing drop only kills you later. With it on, catching a long fall " +
                 "late still saves you, and how late you dare leave it is the whole skill.")]
        public bool forgivesFall = true;

        [Tooltip("Touching down ends the glide early and starts the cooldown.")]
        public bool endsOnLanding = true;

        [Tooltip("Sideways speed multiplier while gliding, so a long float can be steered " +
                 "somewhere. 1 leaves air control alone.")]
        [Min(0f)] public float steering = 1f;

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal && !wizard.movement.IsGrounded;

        public override bool OnCast(PlayerLogic wizard) => true;

        public override void ModifyStatsWhileLit(PlayerLogic.Modifiers stats)
        {
            stats.FallSpeedMultiplier *= fallSpeed;
            stats.MoveSpeedMultiplier *= steering;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            if (endsOnLanding && wizard.movement.IsGrounded)
            {
                wizard.spellbook.Extinguish(this);
                return;
            }

            if (forgivesFall)
                wizard.BeginFallFrom(wizard.movement.Position.y);
        }
    }
}
