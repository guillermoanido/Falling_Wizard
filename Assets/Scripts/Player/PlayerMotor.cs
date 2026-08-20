using System;
using UnityEngine;

namespace FallingWizard.Player
{
    // Momentum based 2D movement. Horizontal velocity is nudged towards a target every physics
    // step rather than snapping, so the wizard builds up speed and keeps sliding when you let go.
    // Falling is heavier than rising, and every landing reports the distance fallen.
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerInputReader))]
    [RequireComponent(typeof(PlayerPowerUps))]
    public class PlayerMotor : MonoBehaviour
    {
        const float InputDeadzone = 0.01f;
        const float MinGravityScale = 0.01f;
        const float DefaultGravityScale = 3f;
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
        [Tooltip("Which layers count as solid ground.")]
        [SerializeField] LayerMask groundLayers = ~0;

        [Tooltip("A thin box just under the feet. Draw it with the cyan gizmo when the object is selected.")]
        [SerializeField] Vector2 groundCheckOffset = new Vector2(0f, -0.9f);
        [SerializeField] Vector2 groundCheckSize = new Vector2(0.7f, 0.1f);

        Rigidbody2D body;
        PlayerInputReader input;
        PlayerPowerUps powerUps;
        SpriteRenderer sprite;

        float baseGravityScale;
        float coyoteTimer;
        float bufferTimer;
        float highestPoint;
        bool rising;
        int airJumpsUsed;

        public bool IsGrounded { get; private set; }

        public Vector2 Velocity => body.linearVelocity;

        // Raised on touchdown; the argument is how far the wizard fell, in units.
        public event Action<float> Landed;

        // Sensible rigidbody defaults the first time the component is added in the editor.
        void Reset()
        {
            var rigidbody2d = GetComponent<Rigidbody2D>();
            rigidbody2d.freezeRotation = true;
            rigidbody2d.gravityScale = DefaultGravityScale;
            rigidbody2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rigidbody2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Sit the ground check just under whatever collider is on the object.
            var body2d = GetComponent<Collider2D>();
            if (body2d != null)
            {
                groundCheckOffset = new Vector2(0f, -body2d.bounds.extents.y - GroundCheckSkin);
                groundCheckSize = new Vector2(body2d.bounds.size.x * GroundCheckWidthFactor, GroundCheckThickness);
            }
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            input = GetComponent<PlayerInputReader>();
            powerUps = GetComponent<PlayerPowerUps>();
            sprite = GetComponentInChildren<SpriteRenderer>();

            baseGravityScale = Mathf.Max(MinGravityScale, body.gravityScale);
            highestPoint = transform.position.y;
        }

        // Button presses last a single frame, so they have to be caught in Update.
        void Update()
        {
            if (input.JumpPressedThisFrame)
                bufferTimer = jumpBuffer;
            else
                bufferTimer -= Time.deltaTime;
        }

        void FixedUpdate()
        {
            UpdateGroundedState();
            Run();
            TryJump();
            ApplyShortHop();
            ApplyFallGravity();
        }

        public void Stop()
        {
            body.linearVelocity = Vector2.zero;
            enabled = false;
        }

        void UpdateGroundedState()
        {
            bool wasGrounded = IsGrounded;
            IsGrounded = Physics2D.OverlapBox(GroundCheckCenter, groundCheckSize, 0f, groundLayers) != null;

            if (IsGrounded)
            {
                if (!wasGrounded)
                    Landed?.Invoke(Mathf.Max(0f, highestPoint - transform.position.y));

                coyoteTimer = coyoteTime;
                highestPoint = transform.position.y;
                airJumpsUsed = 0;
                rising = false;
            }
            else
            {
                coyoteTimer -= Time.fixedDeltaTime;
                highestPoint = Mathf.Max(highestPoint, transform.position.y);
            }
        }

        void Run()
        {
            float steer = input.Move.x;
            float targetSpeed = steer * maxSpeed * powerUps.SpeedMultiplier;

            // Climb towards the target speed while there is input, coast to a stop when there is not.
            float rate = Mathf.Abs(steer) > InputDeadzone ? acceleration : groundFriction;
            if (!IsGrounded)
                rate *= airControl;

            body.linearVelocityX = Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * Time.fixedDeltaTime);

            if (sprite != null && Mathf.Abs(steer) > InputDeadzone)
                sprite.flipX = steer < 0f;
        }

        void TryJump()
        {
            bool onGroundOrCoyote = coyoteTimer > 0f;
            bool hasAirJump = airJumpsUsed < powerUps.ExtraJumps;

            if (bufferTimer <= 0f || (!onGroundOrCoyote && !hasAirJump))
                return;

            if (!onGroundOrCoyote)
                airJumpsUsed++;

            bufferTimer = 0f;
            coyoteTimer = 0f;
            rising = true;
            body.linearVelocityY = JumpVelocity;
        }

        // Letting go of the button on the way up cuts the jump short.
        void ApplyShortHop()
        {
            if (!rising || input.JumpHeld)
                return;

            if (body.linearVelocityY > 0f)
                body.linearVelocityY *= shortHopMultiplier;

            rising = false;
        }

        void ApplyFallGravity()
        {
            float floatiness = powerUps.FallSpeedMultiplier;
            bool falling = body.linearVelocityY < 0f;

            body.gravityScale = falling
                ? baseGravityScale * fallGravityMultiplier * floatiness
                : baseGravityScale;

            float terminalSpeed = maxFallSpeed * floatiness;
            if (body.linearVelocityY < -terminalSpeed)
                body.linearVelocityY = -terminalSpeed;
        }

        // v = sqrt(2 * g * h), so the jump height set above is the height actually reached.
        float JumpVelocity
        {
            get
            {
                float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;
                return Mathf.Sqrt(2f * gravity * jumpHeight * powerUps.JumpMultiplier);
            }
        }

        Vector2 GroundCheckCenter => (Vector2)transform.position + groundCheckOffset;

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(GroundCheckCenter, groundCheckSize);
        }
    }
}
