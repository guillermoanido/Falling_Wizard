using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class FollowCamera : MonoBehaviour
    {
        [Tooltip("Where the camera sits relative to the wizard.")]
        [SerializeField] Vector2 offset = new Vector2(0f, 1.5f);

        [Tooltip("Lower is snappier. Seconds the camera takes to catch up.")]
        [SerializeField] float smoothTime = 0.18f;

        [Header("Peeking")]
        [Tooltip("How far the camera drops while the wizard looks over an edge.")]
        [SerializeField] float peekDistance = 4f;

        [SerializeField] float peekSmoothTime = 0.35f;

        Vector3 followVelocity;
        float peekOffset;
        float peekVelocity;

        void LateUpdate()
        {
            // The wizard is a singleton, so the camera finds them itself rather than being
            // wired up in every scene, and picks them up again after a respawn.
            PlayerCharacter target = PlayerCharacter.Instance;
            if (target == null)
                return;

            float wantedPeek = target.Logic.IsPeeking ? -peekDistance : 0f;
            peekOffset = Mathf.SmoothDamp(peekOffset, wantedPeek, ref peekVelocity, peekSmoothTime);

            Vector3 wanted = new Vector3(
                target.transform.position.x + offset.x,
                target.transform.position.y + offset.y + peekOffset,
                transform.position.z);

            transform.position = Vector3.SmoothDamp(transform.position, wanted, ref followVelocity, smoothTime);
        }
    }
}
