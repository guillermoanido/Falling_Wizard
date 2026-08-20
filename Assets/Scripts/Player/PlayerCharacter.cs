using System;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerCharacter : MonoBehaviour
    {
        const string MoveActionPath = "Player/Move";
        const string JumpActionPath = "Player/Jump";
        const float DefaultGravityScale = 3f;

        [SerializeField] PlayerMovement movement = new PlayerMovement();
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

        public Health Health => health;
        public PlayerStats Stats => powerUps.Stats;

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
            powerUps = new ActivePowerUps(this);
            movement.Attach(GetComponent<Rigidbody2D>(), GetComponentInChildren<SpriteRenderer>());
            health.RestoreToFull();

            moveAction = FindAction(MoveActionPath);
            jumpAction = FindAction(JumpActionPath);
        }

        void Update()
        {
            if (!health.IsAlive)
                return;

            movement.Tick(JumpPressedThisFrame, Time.deltaTime);
            powerUps.Tick(Time.deltaTime);
        }

        void FixedUpdate()
        {
            if (!health.IsAlive)
                return;

            movement.FixedTick(MoveInput.x, JumpHeld, powerUps.Stats, Time.fixedDeltaTime);

            if (movement.TryGetLanding(out float fallDistance))
                TakeFallDamage(fallDistance);
        }

        public void Collect(PowerUp powerUp) => powerUps.Add(powerUp);

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

        bool JumpHeld =>
            jumpAction != null && !Game.IsPaused && jumpAction.IsPressed();

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
    public class PlayerMovement
    {
        const float InputDeadzone = 0.01f;
        const float MinGravityScale = 0.01f;
        const float GroundCheckSkin = 0.05f;
        const float GroundCheckThickness = 0.1f;
        const float GroundCheckWidthFactor = 0.9f;

        [Header("Running")]
        [Tooltip("Top running speed, in units per second.")]
        [SerializeField] float maxSpeed = 8f;

        [Tooltip("How fast speed builds up. Lower feels heavier and takes longer to get going.")]
        [SerializeField] float acceleration = 26f;

        [Tooltip("How fast the wizard coasts to a stop on the ground with no input.")]
        [SerializeField] float groundFriction = 34f;

        [Tooltip("Scales acceleration and friction in mid-air. 1 = full control, 0 = committed to your jump.")]
        [Range(0f, 1f)]
        [SerializeField] float airControl = 0.45f;

        [Header("Jumping")]
        [Tooltip("Height of a full jump, in units. The launch speed is worked out from gravity.")]
        [SerializeField] float jumpHeight = 3.2f;

        [Tooltip("Grace period after walking off a ledge where a jump still counts.")]
        [SerializeField] float coyoteTime = 0.12f;

        [Tooltip("A jump pressed this many seconds before landing still fires on touchdown.")]
        [SerializeField] float jumpBuffer = 0.12f;

        [Tooltip("Upward speed kept when the jump button is released early. Lower = shorter hops.")]
        [Range(0f, 1f)]
        [SerializeField] float shortHopMultiplier = 0.45f;

        [Header("Falling")]
        [Tooltip("Gravity is multiplied by this while falling, so drops feel weighty.")]
        [SerializeField] float fallGravityMultiplier = 1.7f;

        [Tooltip("Fastest the wizard can fall, in units per second.")]
        [SerializeField] float maxFallSpeed = 22f;

        [Header("Ground Check")]
        [Tooltip("Which layers count as solid ground. Must not include the player's own layer.")]
        [SerializeField] LayerMask groundLayers = ~0;

        [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.9f);
        [SerializeField] Vector2 groundCheckSize = new Vector2(0.7f, 0.1f);

        Rigidbody2D body;
        SpriteRenderer sprite;

        float baseGravityScale;
        float coyoteTimer;
        float bufferTimer;
        float highestPoint;
        float pendingFallDistance;
        bool hasLanded;
        bool rising;
        int airJumpsUsed;

        public bool IsGrounded { get; private set; }

        public void Attach(Rigidbody2D rigidbody2d, SpriteRenderer spriteRenderer)
        {
            body = rigidbody2d;
            sprite = spriteRenderer;
            baseGravityScale = Mathf.Max(MinGravityScale, body.gravityScale);
            highestPoint = body.position.y;
        }

        public void Tick(bool jumpPressedThisFrame, float deltaTime)
        {
            if (jumpPressedThisFrame)
                bufferTimer = jumpBuffer;
            else
                bufferTimer -= deltaTime;
        }

        public void FixedTick(float steer, bool jumpHeld, PlayerStats stats, float fixedDeltaTime)
        {
            UpdateGroundedState(fixedDeltaTime);
            Run(steer, stats, fixedDeltaTime);
            TryJump(stats);
            ApplyShortHop(jumpHeld);
            ApplyFallGravity(stats);
        }

        public bool TryGetLanding(out float fallDistance)
        {
            fallDistance = pendingFallDistance;
            bool landedThisStep = hasLanded;
            hasLanded = false;
            return landedThisStep;
        }

        public void Stop() => body.linearVelocity = Vector2.zero;

        public void FitGroundCheckTo(Collider2D collider2d)
        {
            groundCheckOffset = new Vector2(0f, -collider2d.bounds.extents.y - GroundCheckSkin);
            groundCheckSize = new Vector2(collider2d.bounds.size.x * GroundCheckWidthFactor, GroundCheckThickness);
        }

        public void DrawGizmos(Vector2 origin)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(origin + groundCheckOffset, groundCheckSize);
        }

        void UpdateGroundedState(float fixedDeltaTime)
        {
            bool wasGrounded = IsGrounded;
            Vector2 center = body.position + groundCheckOffset;
            IsGrounded = Physics2D.OverlapBox(center, groundCheckSize, 0f, groundLayers) != null;

            if (IsGrounded)
            {
                if (!wasGrounded)
                {
                    pendingFallDistance = Mathf.Max(0f, highestPoint - body.position.y);
                    hasLanded = true;
                }

                coyoteTimer = coyoteTime;
                highestPoint = body.position.y;
                airJumpsUsed = 0;
                rising = false;
            }
            else
            {
                coyoteTimer -= fixedDeltaTime;
                highestPoint = Mathf.Max(highestPoint, body.position.y);
            }
        }

        void Run(float steer, PlayerStats stats, float fixedDeltaTime)
        {
            float targetSpeed = steer * maxSpeed * stats.MoveSpeedMultiplier;
            float rate = Mathf.Abs(steer) > InputDeadzone ? acceleration : groundFriction;

            if (!IsGrounded)
                rate *= airControl;

            body.linearVelocityX = Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * fixedDeltaTime);

            if (sprite != null && Mathf.Abs(steer) > InputDeadzone)
                sprite.flipX = steer < 0f;
        }

        void TryJump(PlayerStats stats)
        {
            bool onGroundOrCoyote = coyoteTimer > 0f;
            bool hasAirJump = airJumpsUsed < stats.ExtraJumps;

            if (bufferTimer <= 0f || (!onGroundOrCoyote && !hasAirJump))
                return;

            if (!onGroundOrCoyote)
                airJumpsUsed++;

            bufferTimer = 0f;
            coyoteTimer = 0f;
            rising = true;

            float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;
            body.linearVelocityY = Mathf.Sqrt(2f * gravity * jumpHeight * stats.JumpHeightMultiplier);
        }

        void ApplyShortHop(bool jumpHeld)
        {
            if (!rising || jumpHeld)
                return;

            if (body.linearVelocityY > 0f)
                body.linearVelocityY *= shortHopMultiplier;

            rising = false;
        }

        void ApplyFallGravity(PlayerStats stats)
        {
            float floatiness = stats.FallSpeedMultiplier;
            bool falling = body.linearVelocityY < 0f;

            body.gravityScale = falling
                ? baseGravityScale * fallGravityMultiplier * floatiness
                : baseGravityScale;

            float terminalSpeed = maxFallSpeed * floatiness;
            if (body.linearVelocityY < -terminalSpeed)
                body.linearVelocityY = -terminalSpeed;
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
