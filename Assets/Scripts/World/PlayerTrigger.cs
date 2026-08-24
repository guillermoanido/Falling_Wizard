using System;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Collider2D))]
    public abstract class PlayerTrigger : MonoBehaviour
    {
        [NonSerialized] float lastStep = -1f;

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
