using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(PlayerMotor))]
    [RequireComponent(typeof(PlayerPowerUps))]
    public class PlayerCharacter : MonoBehaviour
    {
        [Tooltip("Restart the level automatically when the wizard dies.")]
        [SerializeField] bool restartLevelOnDeath = true;

        [Tooltip("Seconds to wait before that restart, so the death can be seen.")]
        [SerializeField] float respawnDelay = 1.25f;

        public Health Health { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public PlayerPowerUps PowerUps { get; private set; }

        void Awake()
        {
            Health = GetComponent<Health>();
            Motor = GetComponent<PlayerMotor>();
            PowerUps = GetComponent<PlayerPowerUps>();
        }

        void OnEnable() => Health.Died += HandleDeath;

        void OnDisable() => Health.Died -= HandleDeath;

        void HandleDeath()
        {
            Motor.Stop();
            PowerUps.ClearAll();

            if (restartLevelOnDeath)
                Invoke(nameof(RestartLevel), respawnDelay);
        }

        void RestartLevel() => SceneLoader.ReloadCurrentScene();
    }
}
