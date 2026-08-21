using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    // The wizard in the scene, and the scene's only one, so anything that needs them can ask for
    // Instance rather than being wired up by hand. The component itself does nothing but plug
    // Unity into PlayerLogic: components in, input in, death out.
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCharacter : SingletonBehaviour<PlayerCharacter>
    {
        [Header("Body")]
        [Tooltip("Gravity written onto the Rigidbody2D by Reset. 3 with a 2 box jump is the feel " +
                 "the rest of the tuning assumes.")]
        [Min(0.01f)] public float gravityScale = 3f;

        [Header("Stick Feel")]
        [Tooltip("Stick tilt needed before looking up or down counts. Raise it if a worn stick drifts.")]
        [Range(0f, 1f)] public float lookThreshold = 0.5f;

        [Header("Death")]
        [Tooltip("Reload the level when the wizard runs out of hearts.")]
        public bool restartLevelOnDeath = true;

        [Tooltip("Seconds to wait before that restart, so the death can be seen.")]
        [Min(0f)] public float respawnDelay = 1.25f;

        [Header("Behaviour")]
        public PlayerLogic logic = new PlayerLogic();

        Controls controls;

        public PlayerLogic Logic => logic;

        // The staff they carry, if they carry one.
        public Staff Staff { get; private set; }

        // The body collider specifically. Hazards filter on this so the staff's own trigger,
        // which shares this rigidbody, does not fire everything a second time.
        public Collider2D Hitbox { get; private set; }

        void Reset()
        {
            var rigidbody2d = GetComponent<Rigidbody2D>();
            rigidbody2d.freezeRotation = true;
            rigidbody2d.gravityScale = gravityScale;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
                logic.movement.FitGroundCheckTo(collider2d);
        }

        void OnValidate() => logic.Validate();

        protected override void OnAwake()
        {
            Hitbox = GetComponent<Collider2D>();
            Staff = GetComponentInChildren<Staff>(true);
            controls = new Controls();

            logic.Attach(GetComponent<Rigidbody2D>(), FindBodySprite(), Hitbox, Staff?.Logic);
            logic.Died += OnDied;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            logic.Died -= OnDied;
        }

        void Update() => logic.Observe(controls.Read(lookThreshold), Time.deltaTime);

        void FixedUpdate() => logic.Simulate(Time.fixedDeltaTime);

        void OnDrawGizmosSelected() => logic.DrawGizmos(transform.position);

        // The staff carries a sprite of its own and may well sit first in the hierarchy, so the
        // wizard's own renderer is the first one that is not part of it.
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

        // ========================================================================== Controls ====

        // Move, jump and walk only. Every spell reads its own button through the spellbook, so
        // adding one never touches this class.
        public class Controls
        {
            readonly InputAction move = Core.Controls.Player("Move");
            readonly InputAction jump = Core.Controls.Player("Jump");
            readonly InputAction walk = Core.Controls.Player("Walk");

            public PlayerLogic.Intent Read(float lookThreshold)
            {
                if (Game.IsPaused)
                    return default;

                Vector2 stick = move != null ? move.ReadValue<Vector2>() : Vector2.zero;

                return new PlayerLogic.Intent
                {
                    Move = stick,
                    JumpPressed = jump != null && jump.WasPressedThisFrame(),
                    JumpHeld = jump != null && jump.IsPressed(),
                    Walk = walk != null && walk.IsPressed(),
                    LookingDown = stick.y < -lookThreshold,
                };
            }
        }
    }
}
