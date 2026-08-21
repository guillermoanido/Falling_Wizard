using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCharacter : SingletonBehaviour<PlayerCharacter>
    {
        const float DefaultGravityScale = 3f;

        [SerializeField] PlayerLogic logic = new PlayerLogic();

        [Header("Death")]
        [SerializeField] bool restartLevelOnDeath = true;

        [Tooltip("Seconds to wait before that restart, so the death can be seen.")]
        [SerializeField] float respawnDelay = 1.25f;

        PlayerInputReader input;

        public PlayerLogic Logic => logic;

        public Staff Staff { get; private set; }

        void Reset()
        {
            var rigidbody2d = GetComponent<Rigidbody2D>();
            rigidbody2d.freezeRotation = true;
            rigidbody2d.gravityScale = DefaultGravityScale;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
                logic.Movement.FitGroundCheckTo(collider2d);
        }

        protected override void OnAwake()
        {
            input = new PlayerInputReader();
            Staff = GetComponentInChildren<Staff>(true);

            logic.Attach(
                GetComponent<Rigidbody2D>(),
                FindBodySprite(),
                GetComponent<Collider2D>(),
                Staff != null ? Staff.Logic : null);

            logic.Died += OnDied;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            logic.Died -= OnDied;
        }

        void Update() => logic.Observe(input.Read(), Time.deltaTime);

        void FixedUpdate() => logic.Simulate(Time.fixedDeltaTime);

        void OnDrawGizmosSelected() => logic.DrawGizmos(transform.position);

        // The staff carries a sprite of its own and may well sit first in the hierarchy, so
        // the wizard's own renderer is the first one that is not part of it.
        SpriteRenderer FindBodySprite()
        {
            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sprite.GetComponentInParent<Staff>() == null)
                    return sprite;
            }

            return null;
        }

        void OnDied()
        {
            if (restartLevelOnDeath)
                Invoke(nameof(RestartLevel), respawnDelay);
        }

        void RestartLevel() => Game.ReloadCurrentScene();
    }
}
