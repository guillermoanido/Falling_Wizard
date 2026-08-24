using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public abstract class Hazard : PlayerTrigger
    {
        [Header("Hazard")]
        [Tooltip("Hazards are things you pass straight through that do something to you on the " +
                 "way, rather than things you bump into. Applied on Awake, so ticking this fixes " +
                 "a hazard already placed in a scene without touching its collider by hand. " +
                 "Untick only for something meant to be solid - and if you do, keep it OFF the " +
                 "Hazard layer, because the ground check ignores that layer and a solid hazard " +
                 "there is something the wizard comes to rest on while the game still believes " +
                 "they are falling.")]
        public bool passThrough = true;

        [Tooltip("Only fires above this speed, in boxes per second. Running is 6 and walking is " +
                 "2, so 4 means a run sets it off and a walk does not. 0 always fires.")]
        [Min(0f)] public float minimumSpeed = 0f;

        [Tooltip("Seconds before it can catch the wizard again.")]
        [Min(0f)] public float rearmDelay = 0.5f;

        [Tooltip("Hearts taken on contact. 0 for hazards that only shove you around.")]
        [Min(0)] public int damage = 0;

        [Tooltip("Can this reach the wizard while they hang on their staff? Off makes the staff " +
                 "a safe perch.")]
        public bool affectsOnStaff = false;

        [Tooltip("Can this hit a wizard who is already tumbling?")]
        public bool affectsRagdolled = false;

        [NonSerialized] float readyAt;

        protected abstract void Affect(PlayerLogic wizard);

        protected virtual void Awake()
        {
            var hitbox = GetComponent<Collider2D>();

            if (hitbox != null)
                hitbox.isTrigger = passThrough;
        }

        protected sealed override void OnPlayerEntered(PlayerCharacter wizard)
        {
            if (Time.time < readyAt || !Allowed(wizard))
                return;

            if (wizard.Logic.movement.ApproachSpeed < minimumSpeed)
                return;

            readyAt = Time.time + rearmDelay;

            Affect(wizard.Logic);

            if (damage > 0)
                wizard.Logic.Hurt(damage);
        }

        protected bool Allowed(PlayerCharacter wizard)
        {
            PlayerLogic logic = wizard.Logic;

            return logic.health.IsAlive &&
                   (affectsOnStaff || logic.State != PlayerState.OnStaff) &&
                   (affectsRagdolled || logic.State != PlayerState.Ragdoll);
        }
    }
}
