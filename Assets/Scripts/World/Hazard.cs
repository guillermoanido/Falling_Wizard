using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Something in the world that does something unpleasant to the wizard. Everything shared by
    // rocks, slimes and wind lives here - speed gating, re-arming, damage, and whether it can
    // reach a wizard who is hanging off their staff or already tumbling.
    //
    // Adding a hazard is one subclass with one Affect method. Put them on the Hazard layer (8).
    public abstract class Hazard : PlayerTrigger
    {
        [Header("Hazard")]
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

        // What this particular hazard does. Speed, re-arming and damage are already handled.
        protected abstract void Affect(PlayerLogic wizard);

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
