using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Anything in the world that reacts to the wizard touching it. Hazards and shrines both sit
    // on this, so the one awkward detail lives in exactly one place:
    //
    // The wizard emits TWO colliders - their own body, and the staff's trigger, which is a child
    // with no rigidbody of its own and therefore reports the wizard's rigidbody as its own. Every
    // contact would fire twice without filtering down to the body collider.
    [RequireComponent(typeof(Collider2D))]
    public abstract class PlayerTrigger : MonoBehaviour
    {
        [NonSerialized] float lastStep = -1f;

        // Override to true to also get OnPlayerInside every step they remain in contact.
        protected virtual bool Continuous => false;

        protected abstract void OnPlayerEntered(PlayerCharacter wizard);

        protected virtual void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime) { }

        void OnTriggerEnter2D(Collider2D other) => Entered(other);

        void OnCollisionEnter2D(Collision2D collision) => Entered(collision.collider);

        void OnTriggerStay2D(Collider2D other)
        {
            if (Continuous)
                Inside(other);
        }

        void OnCollisionStay2D(Collision2D collision)
        {
            if (Continuous)
                Inside(collision.collider);
        }

        // The wizard, but only if this contact is their body rather than their staff.
        protected static PlayerCharacter Resolve(Collider2D other)
        {
            Rigidbody2D body = other.attachedRigidbody;
            if (body == null)
                return null;

            var wizard = body.GetComponent<PlayerCharacter>();
            return wizard != null && other == wizard.Hitbox ? wizard : null;
        }

        void Entered(Collider2D other)
        {
            PlayerCharacter wizard = Resolve(other);

            if (wizard == null || Mathf.Approximately(Time.fixedTime, lastStep))
                return;

            lastStep = Time.fixedTime;
            OnPlayerEntered(wizard);
        }

        void Inside(Collider2D other)
        {
            PlayerCharacter wizard = Resolve(other);

            if (wizard == null || Mathf.Approximately(Time.fixedTime, lastStep))
                return;

            lastStep = Time.fixedTime;
            OnPlayerInside(wizard, Time.fixedDeltaTime);
        }
    }
}
