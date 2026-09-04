using FallingWizard.Core;
using FallingWizard.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
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

        [Tooltip("Ask where to go instead of dropping straight back at the last rest: carry on " +
                 "from there, or give the run up and go back to spend what is already banked.")]
        public bool offerChoiceOnDeath = true;

        [Header("Behaviour")]
        public PlayerLogic logic = new PlayerLogic();

        Controls controls;

        public PlayerLogic Logic => logic;

        public Staff Staff { get; private set; }

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

            MoveToCheckpoint();

            logic.Attach(GetComponent<Rigidbody2D>(), FindBodySprite(), Hitbox, Staff?.Logic);
            logic.Died += OnDied;
        }

        void MoveToCheckpoint()
        {
            if (!Progress.CheckpointIsHere)
                return;

            var body = GetComponent<Rigidbody2D>();

            body.position = Progress.CheckpointPoint;
            body.linearVelocity = Vector2.zero;
            transform.position = Progress.CheckpointPoint;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            logic.Died -= OnDied;
        }

        void Update() => logic.Observe(controls.Read(lookThreshold), Time.deltaTime);

        void FixedUpdate() => logic.Simulate(Time.fixedDeltaTime);

        void OnDrawGizmosSelected() => logic.DrawGizmos(transform.position);

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
            Progress.LoseCarried();

            if (offerChoiceOnDeath)
                Invoke(nameof(AskWhereToGo), respawnDelay);
            else if (restartLevelOnDeath)
                Invoke(nameof(RestartLevel), respawnDelay);
        }

        void AskWhereToGo()
        {
            // Built once and never rebuilt, which is safe: ChoiceScreen.Open calls Screens.Claim,
            // and MenuScreen.Update refuses to open the pause menu while ModalOpen - so there is
            // no way to reach the settings panel and change language over the top of this.
            ChoiceScreen screen = ChoiceScreen.Open(Loc.Get(Loc.Keys.DeathTitle),
                                                    Loc.Get(Loc.Keys.DeathBlurb));

            screen.Status(Loc.Format(Loc.Keys.DeathStatus, Progress.Wisps));

            if (Progress.HasCheckpoint)
                screen.Choice(Loc.Get(Loc.Keys.DeathContinue),
                    () => screen.CloseThen(Game.ReloadCurrentScene));

            screen.Choice(Loc.Get(Loc.Keys.DeathGiveUp), () => screen.CloseThen(() =>
            {
                Progress.EndRun();
                SkillScreen.Open(Game.LoadFirstLevel);
            }));
        }

        void RestartLevel() => Game.ReloadCurrentScene();

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
