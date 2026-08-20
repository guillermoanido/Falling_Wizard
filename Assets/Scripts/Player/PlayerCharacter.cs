using System;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public enum PlayerState
    {
        Normal,
        Descending,
        Hanging,
        Climbing,
        Ragdoll,
    }

    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCharacter : MonoBehaviour
    {
        const string MoveActionPath = "Player/Move";
        const string JumpActionPath = "Player/Jump";
        const string WalkActionPath = "Player/Walk";
        const string StaffActionPath = "Player/Staff";

        const float DefaultGravityScale = 3f;
        const float StickThreshold = 0.5f;

        [SerializeField] PlayerMovement movement = new PlayerMovement();
        [Tooltip("The Staff entity, normally a child of the wizard. No staff means no descent.")]
        [SerializeField] Staff staff;
        [SerializeField] Ragdoll ragdoll = new Ragdoll();
        [SerializeField] Health health = new Health();

        [Header("Fall Damage")]
        [Tooltip("Falls shorter than this many units are free.")]
        [SerializeField] float safeFallDistance = 5f;

        [Tooltip("Damage taken per unit fallen beyond the safe distance.")]
        [SerializeField] float damagePerUnit = 0.6f;

        [Header("Death")]
        [SerializeField] bool restartLevelOnDeath = true;

        [Tooltip("Seconds to wait before that restart, so the death can be seen.")]
        [SerializeField] float respawnDelay = 1.25f;

        ActivePowerUps powerUps;
        InputAction moveAction;
        InputAction jumpAction;
        InputAction walkAction;
        InputAction staffAction;

        public Health Health => health;
        public PlayerStats Stats => powerUps.Stats;
        public PlayerState State { get; private set; }
        public bool IsPeeking { get; private set; }

        void Reset()
        {
            var rigidbody2d = GetComponent<Rigidbody2D>();
            rigidbody2d.freezeRotation = true;
            rigidbody2d.gravityScale = DefaultGravityScale;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
                movement.FitGroundCheckTo(collider2d);
        }

        void Awake()
        {
            var body = GetComponent<Rigidbody2D>();

            powerUps = new ActivePowerUps(this);
            movement.Attach(body, GetComponentInChildren<SpriteRenderer>());
            ragdoll.Attach(body);
            health.RestoreToFull();

            moveAction = FindAction(MoveActionPath);
            jumpAction = FindAction(JumpActionPath);
            walkAction = FindAction(WalkActionPath);
            staffAction = FindAction(StaffActionPath);
        }

        void Update()
        {
            if (!health.IsAlive)
                return;

            movement.Tick(JumpPressedThisFrame, Time.deltaTime);
            powerUps.Tick(Time.deltaTime);

            if (StaffPressedThisFrame)
                TryBeginDescent();

            UpdatePeeking();
        }

        void FixedUpdate()
        {
            if (!health.IsAlive)
                return;

            switch (State)
            {
                case PlayerState.Descending:
                    if (staff.Tick(Time.fixedDeltaTime))
                        State = PlayerState.Hanging;
                    break;

                case PlayerState.Hanging:
                    UpdateHanging();
                    break;

                case PlayerState.Climbing:
                    if (staff.Tick(Time.fixedDeltaTime))
                    {
                        staff.Release();
                        State = PlayerState.Normal;
                    }
                    break;

                case PlayerState.Ragdoll:
                    // No control at all here: physics owns the body until they get back up.
                    movement.SenseGround(Time.fixedDeltaTime);
                    CheckLanding();

                    if (ragdoll.Tick(Time.fixedDeltaTime, movement.IsGrounded, movement.HorizontalSpeed))
                        State = PlayerState.Normal;
                    break;

                default:
                    movement.FixedTick(BuildCommand(), powerUps.Stats, Time.fixedDeltaTime);
                    CheckLanding();
                    CheckForTrip();
                    break;
            }
        }

        public void Collect(PowerUp powerUp) => powerUps.Add(powerUp);

        MoveCommand BuildCommand() => new MoveCommand
        {
            Steer = MoveInput.x,
            JumpHeld = JumpHeld,
            Walk = WalkHeld,
        };

        void TryBeginDescent()
        {
            if (staff == null || State != PlayerState.Normal ||
                !movement.IsGrounded || !movement.IsAtEdge)
                return;

            staff.BeginDescent(movement.Facing);
            State = PlayerState.Descending;
        }

        void UpdateHanging()
        {
            if (LookingUp)
            {
                staff.BeginClimb();
                State = PlayerState.Climbing;
                return;
            }

            if (!StaffHeld || LookingDown)
            {
                staff.Release();
                movement.BeginFallFrom(transform.position.y);
                State = PlayerState.Normal;
            }
        }

        void UpdatePeeking()
        {
            IsPeeking = State == PlayerState.Hanging ||
                        (State == PlayerState.Normal && LookingDown);
        }

        void CheckForTrip()
        {
            if (!movement.IsGrounded || !movement.GroundIsRough ||
                movement.HorizontalSpeed <= ragdoll.TripSpeed)
                return;

            ragdoll.Begin(movement.Facing);
            State = PlayerState.Ragdoll;
        }

        void CheckLanding()
        {
            if (movement.TryGetLanding(out float fallDistance))
                TakeFallDamage(fallDistance);
        }

        void TakeFallDamage(float fallDistance)
        {
            float excess = fallDistance - safeFallDistance;
            if (excess <= 0f)
                return;

            int damage = Mathf.RoundToInt(excess * damagePerUnit * powerUps.Stats.FallDamageMultiplier);
            if (damage <= 0)
                return;

            health.TakeDamage(damage);

            if (!health.IsAlive)
                Die();
        }

        void Die()
        {
            movement.Stop();
            powerUps.Clear();

            if (restartLevelOnDeath)
                Invoke(nameof(RestartLevel), respawnDelay);
        }

        void RestartLevel() => Game.ReloadCurrentScene();

        Vector2 MoveInput =>
            moveAction != null && !Game.IsPaused ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        bool JumpPressedThisFrame =>
            jumpAction != null && !Game.IsPaused && jumpAction.WasPressedThisFrame();

        bool JumpHeld => jumpAction != null && !Game.IsPaused && jumpAction.IsPressed();

        bool WalkHeld => walkAction != null && !Game.IsPaused && walkAction.IsPressed();

        bool StaffHeld => staffAction != null && !Game.IsPaused && staffAction.IsPressed();

        bool LookingDown => MoveInput.y < -StickThreshold;

        bool LookingUp => MoveInput.y > StickThreshold;

        bool StaffPressedThisFrame =>
            staffAction != null && !Game.IsPaused && staffAction.WasPressedThisFrame();

        static InputAction FindAction(string path)
        {
            InputActionAsset actions = InputSystem.actions;
            InputAction action = actions != null ? actions.FindAction(path) : null;

            if (action == null)
                Debug.LogError($"Input action '{path}' is missing from the project-wide actions asset.");
            else
                action.Enable();

            return action;
        }

        void OnDrawGizmosSelected() => movement.DrawGizmos(transform.position);
    }

    public class PlayerStats
    {
        public float MoveSpeedMultiplier;
        public float JumpHeightMultiplier;
        public float FallSpeedMultiplier;
        public float FallDamageMultiplier;
        public int ExtraJumps;

        public PlayerStats() => Reset();

        public void Reset()
        {
            MoveSpeedMultiplier = 1f;
            JumpHeightMultiplier = 1f;
            FallSpeedMultiplier = 1f;
            FallDamageMultiplier = 1f;
            ExtraJumps = 0;
        }
    }

    [Serializable]
    public class Health
    {
        [SerializeField] int maxHealth = 5;

        [Tooltip("Seconds of immunity after taking a hit.")]
        [SerializeField] float invulnerabilityTime = 0.6f;

        float invulnerableUntil;

        public int Current { get; private set; }
        public bool IsAlive => Current > 0;
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        public void RestoreToFull() => Current = maxHealth;

        public void TakeDamage(int amount)
        {
            if (amount <= 0 || !IsAlive || IsInvulnerable)
                return;

            Current = Mathf.Max(0, Current - amount);
            invulnerableUntil = Time.time + invulnerabilityTime;
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive)
                return;

            Current = Mathf.Min(maxHealth, Current + amount);
        }
    }
}
