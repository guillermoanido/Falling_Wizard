using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour
    {
        [Tooltip("The PowerUp asset to hand over. Create one from Assets > Create > Falling Wizard.")]
        [SerializeField] PowerUp powerUp;

        [Tooltip("Optional prefab spawned where the pickup was, for a puff of sparkles.")]
        [SerializeField] GameObject collectedEffect;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null || powerUp == null)
                return;

            player.Collect(powerUp);

            if (collectedEffect != null)
                Instantiate(collectedEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
