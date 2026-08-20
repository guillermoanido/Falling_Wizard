using System;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    public struct MoveCommand
    {
        public float Steer;
        public bool JumpHeld;
        public bool Walk;
    }

    [Serializable]
    public class PlayerMovement
    {
        const float InputDeadzone = 0.01f;
        const float MinGravityScale = 0.01f;
        const float GroundCheckSkin = 0.05f;
        const float GroundCheckThickness = 0.1f;
        const float GroundCheckWidthFactor = 0.9f;

        [Header("Speed")]
        [Tooltip("Top speed at a normal run, in units per second. Running off a ledge drops you.")]
        [SerializeField] float runSpeed = 8f;

        [Tooltip("Top speed while holding Walk. Walking also refuses to step off a ledge.")]
        [SerializeField] float walkSpeed = 2.6f;

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

        [Header("Ledge Check")]
        [Tooltip("How far ahead of the feet to look for missing ground.")]
        [SerializeField] float ledgeCheckAhead = 0.6f;

        [Tooltip("A gap deeper than this counts as a ledge worth stopping at.")]
        [SerializeField] float ledgeCheckDepth = 0.8f;

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

        Collider2D ground;

        public bool IsGrounded { get; private set; }
        public bool IsAtEdge { get; private set; }
        public bool GroundIsRough { get; private set; }
        public LayerMask GroundLayers => groundLayers;
        public int Facing { get; private set; } = 1;
        public float HorizontalSpeed => body == null ? 0f : Mathf.Abs(body.linearVelocityX);

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

        public void FixedTick(MoveCommand command, PlayerStats stats, float fixedDeltaTime)
        {
            UpdateFacing(command.Steer);
            UpdateGroundedState(fixedDeltaTime);
            Run(command, stats, fixedDeltaTime);
            TryJump(stats);
            ApplyShortHop(command.JumpHeld);
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

        // Ground sensing without any of the control, for states that let physics take over.
        public void SenseGround(float fixedDeltaTime) => UpdateGroundedState(fixedDeltaTime);

        public void BeginFallFrom(float height)
        {
            highestPoint = height;
            IsGrounded = false;
            coyoteTimer = 0f;
            rising = false;
        }

        public void FitGroundCheckTo(Collider2D collider2d)
        {
            groundCheckOffset = new Vector2(0f, -collider2d.bounds.extents.y - GroundCheckSkin);
            groundCheckSize = new Vector2(collider2d.bounds.size.x * GroundCheckWidthFactor, GroundCheckThickness);
        }

        public void DrawGizmos(Vector2 origin)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(origin + groundCheckOffset, groundCheckSize);

            Gizmos.color = Color.yellow;
            Vector2 probe = origin + new Vector2(Facing * ledgeCheckAhead, groundCheckOffset.y);
            Gizmos.DrawLine(probe, probe + Vector2.down * ledgeCheckDepth);
        }

        void UpdateFacing(float steer)
        {
            if (Mathf.Abs(steer) > InputDeadzone)
                Facing = steer < 0f ? -1 : 1;

            if (sprite != null)
                sprite.flipX = Facing < 0;
        }

        void UpdateGroundedState(float fixedDeltaTime)
        {
            bool wasGrounded = IsGrounded;
            Vector2 center = body.position + groundCheckOffset;
            Collider2D hit = Physics2D.OverlapBox(center, groundCheckSize, 0f, groundLayers);

            IsGrounded = hit != null;
            SetGround(hit);
            IsAtEdge = IsGrounded && !HasGroundAhead();

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

        void SetGround(Collider2D hit)
        {
            if (hit == ground)
                return;

            ground = hit;
            GroundIsRough = hit != null && hit.GetComponentInParent<RoughGround>() != null;
        }

        bool HasGroundAhead()
        {
            Vector2 probe = body.position + new Vector2(Facing * ledgeCheckAhead, groundCheckOffset.y);
            return Physics2D.Raycast(probe, Vector2.down, ledgeCheckDepth, groundLayers).collider != null;
        }

        void Run(MoveCommand command, PlayerStats stats, float fixedDeltaTime)
        {
            float steer = command.Steer;
            float topSpeed = command.Walk ? walkSpeed : runSpeed;
            float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

            // Walking is careful: it refuses to carry the wizard over the lip of a drop.
            if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > InputDeadzone)
                targetSpeed = 0f;

            float rate = Mathf.Abs(steer) > InputDeadzone ? acceleration : groundFriction;
            if (!IsGrounded)
                rate *= airControl;

            body.linearVelocityX = Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * fixedDeltaTime);
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
}
