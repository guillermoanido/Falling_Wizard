using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        // Airtime only ever counts up from zero, so a value nothing can reach means "not yet".
        const int NeverSpent = -1;

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

        [Tooltip("One cast per fall. Touch the ground before it comes back, however long the " +
                 "cooldown says. Turn this off and the spell can be re-cast in mid-air, which " +
                 "with 'forgives fall' on means chaining it makes every drop survivable and fall " +
                 "damage stops existing.")]
        public bool oncePerFall = true;

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal || wizard.movement.IsGrounded)
                return false;

            return !oncePerFall ||
                   wizard.spellbook.StateOf<Fall>(this).spentOn != wizard.movement.Airtime;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            if (wizard.movement.IsGrounded)
                return "you are stood on the ground, and this only slows a fall";

            if (oncePerFall &&
                wizard.spellbook.StateOf<Fall>(this).spentOn == wizard.movement.Airtime)
                return "you have already used it on this fall - touch the ground for another";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            wizard.spellbook.StateOf<Fall>(this).spentOn = wizard.movement.Airtime;
            return true;
        }

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

        public override void OnRunReset(PlayerLogic wizard) =>
            wizard.spellbook.StateOf<Fall>(this).spentOn = NeverSpent;

        public class Fall
        {
            public int spentOn = NeverSpent;
        }
    }
}
