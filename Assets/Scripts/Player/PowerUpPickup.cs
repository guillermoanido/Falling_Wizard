using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] PowerUpEffect effect = new PowerUpEffect();

        [Tooltip("Optional prefab spawned where the pickup was, for a puff of sparkles.")]
        [SerializeField] GameObject collectedEffect;

        void Reset() => GetComponent<Collider2D>().isTrigger = true;

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerCharacter>();
            if (player == null)
                return;

            player.PowerUps.Apply(effect);

            if (collectedEffect != null)
                Instantiate(collectedEffect, transform.position, Quaternion.identity);

            Destroy(gameObject);
        }
    }
}
