using System;
using UnityEngine;

namespace FallingWizard.Player
{
    [Serializable]
    public class Ragdoll
    {
        [Tooltip("Crossing rough ground faster than this trips the wizard.")]
        [SerializeField] float tripSpeed = 5f;

        [Tooltip("Spin given to the wizard when they trip, in degrees per second.")]
        [SerializeField] float spinSpeed = 520f;

        [Tooltip("Downward kick when tripping, so they pitch forward and drop fast.")]
        [SerializeField] float fallKick = 6f;

        [Tooltip("How quickly the tumble spins down. 0 spins forever.")]
        [SerializeField] float angularDamping = 1.5f;

        [Tooltip("Minimum seconds spent tumbling before they can start getting up.")]
        [SerializeField] float minimumDuration = 0.9f;

        [Tooltip("They only get up once grounded and moving slower than this.")]
        [SerializeField] float recoverSpeed = 1.5f;

        [Tooltip("Seconds spent standing back upright.")]
        [SerializeField] float standUpDuration = 0.35f;

        Rigidbody2D body;
        float originalAngularDamping;
        float tumbleTimer;
        float standUpTimer;
        float standUpFrom;

        public float TripSpeed => tripSpeed;

        public void Attach(Rigidbody2D rigidbody2d)
        {
            body = rigidbody2d;
            originalAngularDamping = body.angularDamping;
        }

        public void Begin(int facing)
        {
            body.freezeRotation = false;
            body.angularDamping = angularDamping;
            body.angularVelocity = -facing * spinSpeed;
            body.linearVelocityY = -fallKick;

            tumbleTimer = minimumDuration;
            standUpTimer = -1f;
        }

        public bool Tick(float fixedDeltaTime, bool grounded, float horizontalSpeed)
        {
            if (standUpTimer >= 0f)
                return StandUp(fixedDeltaTime);

            tumbleTimer -= fixedDeltaTime;

            if (tumbleTimer > 0f || !grounded || horizontalSpeed > recoverSpeed)
                return false;

            body.angularVelocity = 0f;
            standUpFrom = body.rotation;
            standUpTimer = 0f;
            return false;
        }

        public void Cancel()
        {
            body.angularVelocity = 0f;
            body.rotation = 0f;
            body.freezeRotation = true;
            body.angularDamping = originalAngularDamping;
        }

        bool StandUp(float fixedDeltaTime)
        {
            standUpTimer += fixedDeltaTime;
            float t = Mathf.Clamp01(standUpTimer / Mathf.Max(0.01f, standUpDuration));
            body.MoveRotation(Mathf.LerpAngle(standUpFrom, 0f, t));

            if (t < 1f)
                return false;

            Cancel();
            return true;
        }
    }
}
