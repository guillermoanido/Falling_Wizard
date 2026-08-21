using UnityEngine;

namespace FallingWizard.Player
{
    // Slows a fall for a second. Needs no new movement code at all: FallSpeedMultiplier is
    // already threaded through both the gravity scale and the terminal speed clamp, and jump
    // height is worked out from the BASE gravity, so a glide never makes jumps taller.
    //
    // Never write body.gravityScale from a spell - movement reassigns it 50 times a second.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        [Header("Glide")]
        [Tooltip("Fall speed while gliding, as a fraction of normal. 0.25 is a quarter speed.")]
        [Range(0.05f, 1f)] public float fallSpeed = 0.25f;

        [Tooltip("Touching down ends the glide early and starts the cooldown.")]
        public bool endsOnLanding = true;

        // Only worth casting in the air, and only when actually falling.
        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal && !wizard.movement.IsGrounded;

        public override bool OnCast(PlayerLogic wizard) => true;

        public override void ModifyStatsWhileLit(PlayerLogic.Modifiers stats) =>
            stats.FallSpeedMultiplier *= fallSpeed;

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            if (endsOnLanding && wizard.movement.IsGrounded)
                wizard.spellbook.Extinguish(this);
        }
    }
}
