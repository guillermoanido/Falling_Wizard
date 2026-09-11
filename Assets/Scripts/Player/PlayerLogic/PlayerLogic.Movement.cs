using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        [Serializable]
        public class Movement
        {
            const float MinGravityScale = 0.01f;

            const int LayerCount = 32;

            static readonly Vector2 MinGroundCheck = new Vector2(0.05f, 0.01f);

            const float MinTravelSpeed = 0.1f;

            const float SlopeProbeLift = 0.25f;

            const float StepClearance = 0.02f;

            const float ClimbInset = 0.05f;

            const float ClimbProbeStep = 0.25f;

            const float ArcClearance = 0.15f;

            const float GroundlessWarning = 3f;
            const float NearbyGround = 1f;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>(8);
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>(4);

            [NonSerialized] ContactFilter2D arcFilter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
            };

            [Header("Speed")]
            [Tooltip("Top speed at a normal run, in boxes per second. Running off a ledge drops you.")]
            [Min(0f)] public float runSpeed = 6f;

            [Tooltip("Top speed while holding Walk. Walking also refuses to step off a ledge.")]
            [Min(0f)] public float walkSpeed = 2f;

            [Tooltip("How fast speed builds up. Lower feels heavier and takes longer to get going.")]
            [Min(0f)] public float acceleration = 20f;

            [Tooltip("How fast the wizard coasts to a stop on the ground with no input.")]
            [Min(0f)] public float groundFriction = 26f;

            [Tooltip("Scales acceleration and friction in mid-air. 1 = full control, 0 = committed.")]
            [Range(0f, 1f)] public float airControl = 0.45f;

            [Tooltip("Stick tilt below this counts as no input at all.")]
            [Range(0f, 0.5f)] public float steerDeadzone = 0.01f;

            [Header("Jumping")]
            [Tooltip("Whether the wizard can jump at all. Switch it OFF and the staff becomes the " +
                     "only way up: a lip shorter than the step assist below is walked over, and " +
                     "anything taller has to be climbed. Nothing else that throws the wizard into " +
                     "the air is affected - a slime, a fling and a bounce all go through Launch " +
                     "instead - and the Jump button keeps its other job of letting go of a vine, " +
                     "which is the only way off one. A spell handing out extra jumps cannot bring " +
                     "it back either.")]
            public bool canJump = false;

            [Tooltip("Height of a full jump, in boxes. The launch speed is worked out from gravity.")]
            [Min(0f)] public float jumpHeight = 2f;

            [Tooltip("Grace period after walking off a ledge where a jump still counts. It is " +
                     "ALSO the window the step assist below works in, so it still earns its keep " +
                     "with jumping switched off - zero it as a dead jump number and the wizard " +
                     "stops walking over tile seams as well.")]
            [Min(0f)] public float coyoteTime = 0.12f;

            [Tooltip("A jump pressed this many seconds before landing still fires on touchdown.")]
            [Min(0f)] public float jumpBuffer = 0.12f;

            [Tooltip("Upward speed kept when the jump button is released early. Lower = shorter hops.")]
            [Range(0f, 1f)] public float shortHopMultiplier = 0.45f;

            [Header("Falling")]
            [Tooltip("Gravity is multiplied by this while falling, so drops feel weighty.")]
            [Min(0f)] public float fallGravityMultiplier = 1.7f;

            [Tooltip("Fastest the wizard can fall, in boxes per second.")]
            [Min(0f)] public float maxFallSpeed = 16f;

            [Header("Ground Check")]
            [Tooltip("Which layers count as solid ground. Must NOT include the wizard's own layer, " +
                     "or they will stand on their own collider. Defaults to Ground.")]
            public LayerMask groundLayers = 1 << 6;

            [Tooltip("Where the feet probe sits, relative to the wizard's middle.")]
            public Vector2 groundCheckOffset = new Vector2(0f, -0.596875f);

            [Tooltip("Size of the feet probe. Wider is more forgiving on ledges.")]
            public Vector2 groundCheckSize = new Vector2(0.703125f, 0.1f);

            [Header("Ground Check - Auto Fit")]
            [Tooltip("Gap left under the collider when Reset refits the probe to it.")]
            [Min(0f)] public float groundCheckSkin = 0.05f;

            [Tooltip("Thickness the refitted probe gets.")]
            [Min(0.01f)] public float groundCheckThickness = 0.1f;

            [Tooltip("Fraction of the collider's width the refitted probe gets.")]
            [Range(0.1f, 1f)] public float groundCheckWidthFactor = 0.9f;

            [Header("Ledge Check")]
            [Tooltip("How far ahead of the feet to look for missing ground.")]
            [Min(0f)] public float ledgeCheckAhead = 0.5f;

            [Tooltip("A gap deeper than this counts as a ledge worth stopping at. Keep it above " +
                     "one box: the probe already hangs a skin's width below the soles, so at " +
                     "0.75 the top of every step of a staircase read as a cliff and a WALKING " +
                     "wizard refused to go down one.")]
            [Min(0f)] public float ledgeCheckDepth = 1.2f;

            [Tooltip("How finely to close in on the exact lip when planting the staff.")]
            [Range(4, 16)] public int edgeSearchSteps = 8;

            [Header("Slopes")]
            [Tooltip("Steepest ramp that counts as a floor to walk up rather than a wall to " +
                     "stop at, in degrees. The level's ramp tiles are 45, so anything comfortably " +
                     "above that takes them and still refuses a vertical face.")]
            [Range(0f, 80f)] public float maxSlopeAngle = 55f;

            [Tooltip("Tilt below this is treated as flat, so a floor that is a hair off level " +
                     "does not switch the wizard into ramp handling every other step.")]
            [Range(0f, 20f)] public float flatSlopeAngle = 3f;

            [Tooltip("How far below the soles to look for the tilt of what they are stood on. " +
                     "Needs to clear the probe's own skin without reaching the floor below.")]
            [Min(0.05f)] public float slopeProbeDepth = 0.5f;

            [Header("Steps")]
            [Tooltip("Tallest lip the wizard walks up on their own, in boxes. This is for pixel " +
                     "problems ONLY - tile seams, the teeth along a ramp, a prop set down half a " +
                     "pixel proud - and NOT for anything the player would read as a step. A tile " +
                     "is one box and a half tile is 0.5, so a quarter box is knee-high on a " +
                     "wizard, twice the 0.125 tread of a 45 degree ramp, and four times the worst " +
                     "a one-pixel sprite outline can be out by. Under 0.15 the ramps start " +
                     "stalling; over 0.35 you are eating into real geometry the staff is meant to " +
                     "be planted against. 0 turns it off.")]
            [Min(0f)] public float stepHeight = 0.25f;

            [Tooltip("How far PAST THE TOES to look for that lip, in boxes, and how far forward " +
                     "the step carries them. Roughly one physics step of running - keep it small. " +
                     "Reaching far ahead on a ramp finds a lip as tall as the reach itself, and " +
                     "the landing check then fails because the ramp goes on climbing through " +
                     "where the wizard would have stood: they stop dead at the bottom of every " +
                     "slope.")]
            [Min(0.02f)] public float stepReach = 0.1f;

            [Tooltip("How fast the step assist carries the wizard up a lip, in boxes per " +
                     "second. It used to be instant - one write to the body's position - and a " +
                     "quarter of a box in a single frame is exactly what 'the movement " +
                     "teleports' looks like, because writing a position outright also throws " +
                     "away the Rigidbody2D's interpolation for that frame. Anything from about " +
                     "3 upward is quick enough not to feel like wading; under 2 the wizard " +
                     "visibly crawls up tile seams.")]
            [Min(0.5f)] public float stepClimbSpeed = 6f;

            [Header("Climbing")]
            [Tooltip("How far past the toes to look for a WALL to raise the staff against, in " +
                     "boxes. The wizard walks into a wall and stops flush with it, so this only " +
                     "has to cover the sliver of daylight the physics solver leaves between them " +
                     "- but be generous, because a probe that is a hair short is a staff that " +
                     "refuses to climb for a reason nobody can see. Keep it under Ledge Check " +
                     "Ahead.")]
            [Min(0.05f)] public float climbReach = 0.35f;

            [Tooltip("Let the staff catch a ledge while the wizard is in the air, not just from " +
                     "standing. The lip still has to be ABOVE their feet and inside the staff's " +
                     "reach, so this catches a wall you are dropping past rather than letting you " +
                     "climb from nothing. Catching also clears the fall you had banked - without " +
                     "that, topping out bills the whole drop and the catch that saved you kills " +
                     "you instead.")]
            public bool catchLedgesInTheAir = true;

            [Header("Contact")]
            [Tooltip("Friction between the wizard and the world. 0 is right for a platformer: " +
                     "speed is driven entirely by the numbers above, so physics friction adds " +
                     "nothing except corners and seams to snag on. Raise it only if you want " +
                     "them to catch on scenery deliberately.")]
            [Range(0f, 1f)] public float surfaceFriction = 0f;

            [Header("External Force")]
            [Tooltip("How fast wind fades once you leave the zone, in boxes per second squared.")]
            [Min(0f)] public float windDecay = 24f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] SpriteRenderer sprite;
            [NonSerialized] Collider2D hull;

            [NonSerialized] float baseGravityScale;
            [NonSerialized] float coyoteTimer;
            [NonSerialized] float bufferTimer;
            [NonSerialized] float highestPoint;
            [NonSerialized] float pendingFallDistance;
            [NonSerialized] bool hasLanded;
            [NonSerialized] bool rising;
            [NonSerialized] int airJumpsUsed;

            [NonSerialized] bool everGrounded;
            [NonSerialized] bool warnedGroundless;
            [NonSerialized] float groundlessFor;

            [NonSerialized] Vector2 wind;
            [NonSerialized] float lockout;

            [NonSerialized] float grip = 1f;

            [NonSerialized] Vector2 groundNormal = Vector2.up;
            [NonSerialized] float groundAngle;
            [NonSerialized] bool climbedLastStep;

            [NonSerialized] bool steppedLastStep;

            public enum ClimbRefusal
            {
                None,
                NotStanding,
                NoWall,
                NothingOnTop,
                TooTall,
                NoRoomOnTop,
                NoHeadroom,
            }

            public ClimbRefusal WhyNoClimb { get; private set; }

            public float ClimbRise { get; private set; }
            public float ClimbCanReach { get; private set; }

            [NonSerialized] Vector2 standBox;
            [NonSerialized] Vector2 headBox;
            [NonSerialized] Vector2 headBoxSize;

            public bool IsGrounded { get; private set; }

            public int Airtime { get; private set; }
            public bool IsAtEdge { get; private set; }

            [NonSerialized] float approachVelocityX;

            public float ApproachSpeed => Mathf.Abs(approachVelocityX);

            public int TravelDirection =>
                Mathf.Abs(approachVelocityX) > MinTravelSpeed
                    ? (approachVelocityX < 0f ? -1 : 1)
                    : Facing;

            public int Facing { get; private set; } = 1;
            public Vector2 Position => body == null ? Vector2.zero : body.position;
            public SpriteRenderer Art => sprite;
            public Vector2 Wind => wind;
            public Transform Rig => body == null ? null : body.transform;
            public float FeetY => Position.y + groundCheckOffset.y;

            public Vector2 Footing => hull != null
                ? new Vector2(hull.bounds.center.x, hull.bounds.min.y)
                : new Vector2(Position.x, FeetY + groundCheckSkin);

            float HalfWidth => hull != null
                ? hull.bounds.extents.x
                : (groundCheckWidthFactor > 0f
                    ? groundCheckSize.x / groundCheckWidthFactor * 0.5f
                    : 0f);

            Vector2 ProbeOrigin => body.position + groundCheckOffset;
            ContactFilter2D GroundFilter => new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayers,
                useTriggers = false,
            };

            float BaseGravity => Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;

            public float HorizontalSpeed => body == null ? 0f : Mathf.Abs(body.linearVelocityX);
            public Vector2 Velocity => body == null ? Vector2.zero : body.linearVelocity;
            public float VerticalSpeed => body == null ? 0f : body.linearVelocityY;

            public void Attach(Rigidbody2D rigidbody2d, SpriteRenderer spriteRenderer,
                Collider2D hitbox)
            {
                body = rigidbody2d;
                sprite = spriteRenderer;
                hull = hitbox;
                baseGravityScale = Mathf.Max(MinGravityScale, body.gravityScale);
                highestPoint = body.position.y;

                ApplySurfaceFriction();
            }

            void ApplySurfaceFriction()
            {
                if (body == null)
                    return;

                body.sharedMaterial = new PhysicsMaterial2D("Wizard Contact")
                {
                    friction = surfaceFriction,
                    bounciness = 0f,
                };
            }

            public void BufferJump(bool jumpPressedThisFrame, float deltaTime)
            {
                if (jumpPressedThisFrame)
                    bufferTimer = jumpBuffer;
                else
                    bufferTimer -= deltaTime;
            }

            public void FixedTick(Command command, Modifiers stats, float fixedDeltaTime)
            {
                lockout -= fixedDeltaTime;

                UpdateFacing(command.Steer);
                SenseGround(fixedDeltaTime);
                Run(command, stats, fixedDeltaTime);

                TryStepUp(command, stats, fixedDeltaTime);

                TryJump(stats);
                ApplyShortHop(command.JumpHeld);

                if (wind.y != 0f)
                    body.linearVelocityY += wind.y * fixedDeltaTime;

                ApplyFallGravity(stats);

                approachVelocityX = body.linearVelocityX;
            }

            public bool TryGetLanding(out float fallDistance)
            {
                fallDistance = pendingFallDistance;
                bool landedThisStep = hasLanded;
                hasLanded = false;
                return landedThisStep;
            }

            public void Stop()
            {
                body.linearVelocity = Vector2.zero;
                wind = Vector2.zero;
                grip = 1f;
                climbedLastStep = false;
                steppedLastStep = false;
            }

            public void BeginFallFrom(float height)
            {
                highestPoint = height;

                if (IsGrounded)
                    Airtime++;

                IsGrounded = false;
                coyoteTimer = 0f;
                rising = false;
            }

            public void ApplyWind(Vector2 target, float rampup, float groundScale, float fixedDeltaTime)
            {
                float scale = IsGrounded ? groundScale : 1f;
                float rate = rampup > 0f ? rampup : windDecay;
                wind = Vector2.MoveTowards(wind, target * scale, rate * fixedDeltaTime);
            }

            public void SetGrip(float value) => grip = Mathf.Clamp01(value);

            public void AddImpulse(Vector2 velocity, float controlLockout)
            {
                if (body == null)
                    return;

                body.linearVelocity += velocity;
                rising = false;

                climbedLastStep = false;
                steppedLastStep = false;

                lockout = Mathf.Max(lockout, controlLockout);
            }

            public void NudgeVelocity(Vector2 velocity)
            {
                if (body != null)
                    body.linearVelocity += velocity;
            }

            public void Launch(float heightInBoxes, float sideways, bool resetsFall)
            {
                body.linearVelocityY =
                    Mathf.Sqrt(2f * BaseGravity * Mathf.Max(0f, heightInBoxes));

                if (sideways != 0f)
                    body.linearVelocityX += sideways;

                rising = false;
                climbedLastStep = false;
                steppedLastStep = false;

                if (!resetsFall)
                    return;

                highestPoint = body.position.y;
                IsGrounded = false;
                coyoteTimer = 0f;
            }

            public void FitGroundCheckTo(Collider2D collider2d)
            {
                Bounds box = collider2d.bounds;
                Vector3 middle = collider2d.transform.position;

                groundCheckOffset = new Vector2(
                    box.center.x - middle.x,
                    box.min.y - middle.y - groundCheckSkin);

                groundCheckSize = new Vector2(
                    box.size.x * groundCheckWidthFactor, groundCheckThickness);
            }

            public void Validate()
            {
                runSpeed = Mathf.Max(0f, runSpeed);
                walkSpeed = Mathf.Clamp(walkSpeed, 0f, runSpeed);
                groundCheckSize = Vector2.Max(groundCheckSize, MinGroundCheck);
                flatSlopeAngle = Mathf.Min(flatSlopeAngle, maxSlopeAngle);

                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0 && (groundLayers.value & (1 << playerLayer)) != 0)
                    Debug.LogWarning("Movement.groundLayers includes the Player layer, so the " +
                                     "wizard will try to stand on their own collider.");

                if (stepHeight >= 1f)
                    Debug.LogWarning("Movement.stepHeight is a whole box or more, so the wizard " +
                                     "walks up any one-tile wall without jumping. It is meant " +
                                     "for tile seams and the teeth along a ramp, not for steps.");

                if (stepHeight > 0f && ledgeCheckDepth <= 1f)
                    Debug.LogWarning("Movement.ledgeCheckDepth is one box or less, so the top of " +
                                     "every step down reads as a cliff and a walking wizard " +
                                     "refuses to take it. Keep it above 1.");
            }

            public void DrawGizmos(Vector2 origin)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(origin + groundCheckOffset, groundCheckSize);

                Gizmos.color = Color.yellow;
                Vector2 probe = origin + groundCheckOffset + new Vector2(Facing * ledgeCheckAhead, 0f);
                Gizmos.DrawLine(probe, probe + Vector2.down * ledgeCheckDepth);

                if (stepHeight <= 0f)
                    return;

                Gizmos.color = Color.green;
                float soles = origin.y + groundCheckOffset.y + groundCheckSkin;
                var ahead = new Vector2(origin.x + groundCheckOffset.x + Facing * stepReach, soles);
                Gizmos.DrawLine(ahead, ahead + Vector2.up * stepHeight);

                Gizmos.color = new Color(0.98f, 0.86f, 0.42f);

                float toes = origin.x + groundCheckOffset.x + Facing * HalfWidth;
                float top = hull != null ? hull.bounds.size.y : 1f;

                for (float height = stepHeight; height <= top; height += ClimbProbeStep)
                {
                    var from = new Vector2(toes, soles + height);
                    Gizmos.DrawLine(from, from + new Vector2(Facing * climbReach, 0f));
                }

                bool measured = WhyNoClimb == ClimbRefusal.None ||
                                WhyNoClimb == ClimbRefusal.NoRoomOnTop ||
                                WhyNoClimb == ClimbRefusal.NoHeadroom;

                if (hull == null || !measured)
                    return;

                Gizmos.color = WhyNoClimb == ClimbRefusal.NoRoomOnTop
                    ? Color.red
                    : new Color(0.4f, 1f, 0.5f, 0.8f);

                Gizmos.DrawWireCube(standBox, hull.bounds.size);

                if (headBoxSize.y <= 0f)
                    return;

                Gizmos.color = WhyNoClimb == ClimbRefusal.NoHeadroom
                    ? Color.red
                    : new Color(0.4f, 1f, 0.5f, 0.5f);

                Gizmos.DrawWireCube(headBox, headBoxSize);
            }

            public bool TryFindLedgeEdge(out float edgeX)
            {
                edgeX = ProbeOrigin.x;

                if (!IsGrounded || !IsAtEdge)
                    return false;

                float footing = 0f;
                float air = ledgeCheckAhead;

                if (!HasGroundAt(footing))
                {
                    footing = -groundCheckSize.x * 0.5f;

                    if (!HasGroundAt(footing))
                        return false;
                }

                for (int step = 0; step < edgeSearchSteps; step++)
                {
                    float middle = (footing + air) * 0.5f;

                    if (HasGroundAt(middle))
                        footing = middle;
                    else
                        air = middle;
                }

                if (HasGroundAt(air))
                    return false;

                edgeX = ProbeOrigin.x + Facing * air;
                return true;
            }

            public void SenseGround(float fixedDeltaTime)
            {
                bool wasGrounded = IsGrounded;

                int count = Physics2D.OverlapBox(
                    ProbeOrigin, groundCheckSize, 0f, GroundFilter, Overlaps);

                IsGrounded = count > 0;

                SenseSlope();

                WatchForMissingGround(fixedDeltaTime);

                IsAtEdge = IsGrounded && !HasGroundAt(ledgeCheckAhead);

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
                    if (wasGrounded)
                        Airtime++;

                    coyoteTimer -= fixedDeltaTime;
                    highestPoint = Mathf.Max(highestPoint, body.position.y);
                }
            }

            void WatchForMissingGround(float fixedDeltaTime)
            {
                if (IsGrounded)
                {
                    everGrounded = true;
                    return;
                }

                if (everGrounded || warnedGroundless)
                    return;

                groundlessFor += fixedDeltaTime;
                if (groundlessFor < GroundlessWarning)
                    return;

                warnedGroundless = true;

                if (GroundIsNearby())
                {
                    Debug.LogWarning(
                        $"The wizard has not found the ground in {GroundlessWarning:0} seconds, " +
                        "but there IS something on the right layer within a box of their feet - " +
                        "so the mask is fine and the probe is missing it. Two usual causes: " +
                        "groundCheckOffset sits the probe below the surface instead of across " +
                        "it, or the ground is a CompositeCollider2D set to Outlines, which is a " +
                        "zero-thickness line the probe can sit underneath. Switch the composite " +
                        "to Polygons, or raise groundCheckOffset until the probe straddles the " +
                        "wizard's feet.");

                    return;
                }

                Debug.LogWarning(
                    $"The wizard has not found the ground in {GroundlessWarning:0} seconds, and " +
                    "there is nothing on the right layer anywhere near them. " +
                    $"Movement.groundLayers is set to [{LayerNames(groundLayers)}], and anything " +
                    "they are meant to stand on must be on one of those layers - tilemaps " +
                    "included, which start on Default. Jumping, ledge detection and the staff " +
                    "all read this one mask.");
            }

            bool GroundIsNearby()
            {
                Vector2 wide = groundCheckSize + Vector2.one * NearbyGround;

                return Physics2D.OverlapBox(
                    body.position + groundCheckOffset, wide, 0f, GroundFilter, Overlaps) > 0;
            }

            static string LayerNames(LayerMask mask)
            {
                var listed = new List<string>();

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    if ((mask.value & (1 << layer)) == 0)
                        continue;

                    string name = LayerMask.LayerToName(layer);
                    listed.Add(string.IsNullOrEmpty(name) ? layer.ToString() : name);
                }

                return listed.Count > 0 ? string.Join(", ", listed) : "nothing";
            }

            bool HasGroundAt(float ahead)
            {
                Vector2 probe = ProbeOrigin + new Vector2(Facing * ahead, 0f);
                return Physics2D.Raycast(probe, Vector2.down, GroundFilter, Rays, ledgeCheckDepth) > 0;
            }

            void SenseSlope()
            {
                groundNormal = Vector2.up;
                groundAngle = 0f;

                if (!IsGrounded || body == null)
                    return;

                float lift = Mathf.Max(SlopeProbeLift, groundCheckSize.y);
                float half = groundCheckSize.x * 0.5f;

                for (int i = -1; i <= 1; i++)
                {
                    var from = new Vector2(ProbeOrigin.x + i * half, ProbeOrigin.y + lift);

                    if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                            lift + slopeProbeDepth) <= 0)
                        continue;

                    Vector2 normal = Rays[0].normal;
                    float angle = Vector2.Angle(normal, Vector2.up);

                    if (angle > groundAngle && angle <= maxSlopeAngle)
                    {
                        groundAngle = angle;
                        groundNormal = normal;
                    }
                }
            }

            bool OnRamp => IsGrounded && groundAngle > flatSlopeAngle && groundAngle <= maxSlopeAngle;

            bool TryFindLip(int direction, out float top)
            {
                top = 0f;

                Bounds box = hull.bounds;
                var from = new Vector2(
                    box.center.x + direction * (box.extents.x + stepReach),
                    box.min.y + stepHeight + StepClearance);

                if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                        stepHeight + StepClearance + groundCheckSkin) <= 0)
                    return false;

                top = Rays[0].point.y;
                return true;
            }

            public bool TryFindClimb(float highestRise, out Vector2 lip, out Vector2 landing)
            {
                lip = Vector2.zero;
                landing = Vector2.zero;

                ClimbRise = 0f;
                ClimbCanReach = highestRise;

                if (!TryFindWall(highestRise, out float faceX))
                    return false;

                Bounds box = hull.bounds;
                float soles = box.min.y;

                var above = new Vector2(faceX + Facing * ClimbInset, soles + highestRise);

                if (Physics2D.Raycast(above, Vector2.down, GroundFilter, Rays,
                        highestRise - stepHeight) <= 0)
                {
                    WhyNoClimb = ClimbRefusal.NothingOnTop;
                    return false;
                }

                if (Rays[0].distance <= 0f)
                {
                    WhyNoClimb = ClimbRefusal.TooTall;
                    return false;
                }

                lip = new Vector2(faceX, Rays[0].point.y);
                ClimbRise = lip.y - soles;

                var hullOnTop = new Vector2(
                    faceX + Facing * (box.extents.x + StepClearance),
                    lip.y + box.extents.y + StepClearance);

                standBox = hullOnTop;

                var standing = (Vector2)box.size - Vector2.one * (StepClearance * 2f);

                if (Physics2D.OverlapBox(hullOnTop, standing, 0f, GroundFilter, Overlaps) > 0)
                {
                    WhyNoClimb = ClimbRefusal.NoRoomOnTop;
                    return false;
                }

                float headroom = hullOnTop.y + box.extents.y - box.max.y;

                headBox = new Vector2(box.center.x, box.max.y + headroom * 0.5f);
                headBoxSize = new Vector2(box.size.x * 0.6f, Mathf.Max(0f, headroom));

                if (headroom > 0f &&
                    Physics2D.OverlapBox(headBox, headBoxSize, 0f, GroundFilter, Overlaps) > 0)
                {
                    WhyNoClimb = ClimbRefusal.NoHeadroom;
                    return false;
                }

                landing = hullOnTop + (body.position - (Vector2)box.center);
                WhyNoClimb = ClimbRefusal.None;
                return true;
            }

            public bool TryFindWall(float highestRise, out float faceX)
            {
                faceX = 0f;

                if (body == null || hull == null || highestRise <= stepHeight ||
                    (!IsGrounded && !catchLedgesInTheAir))
                {
                    WhyNoClimb = ClimbRefusal.NotStanding;
                    return false;
                }

                Bounds box = hull.bounds;
                var forward = new Vector2(Facing, 0f);
                float toes = box.center.x + Facing * box.extents.x;
                bool found = false;

                for (float height = stepHeight; height <= highestRise; height += ClimbProbeStep)
                {
                    var from = new Vector2(toes, box.min.y + height);

                    if (Physics2D.Raycast(from, forward, GroundFilter, Rays, climbReach) <= 0)
                        continue;

                    float hitX = Rays[0].point.x;

                    if (!found || Facing * (hitX - faceX) < 0f)
                        faceX = hitX;

                    found = true;
                }

                if (!found)
                    WhyNoClimb = ClimbRefusal.NoWall;

                return found;
            }

            void UpdateFacing(float steer)
            {
                if (Mathf.Abs(steer) > steerDeadzone)
                    Facing = steer < 0f ? -1 : 1;

                if (sprite != null)
                    sprite.flipX = Facing < 0;
            }

            void TryStepUp(Command command, Modifiers stats, float fixedDeltaTime)
            {
                bool stepping = StepUp(command, stats, fixedDeltaTime);

                if (steppedLastStep && !stepping && !rising &&
                    body.linearVelocityY > 0f && body.linearVelocityY <= stepClimbSpeed)
                    body.linearVelocityY = 0f;

                steppedLastStep = stepping;
            }

            bool StepUp(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (stepHeight <= 0f || body == null || hull == null)
                    return false;

                if (lockout > 0f || stats.Rooted)
                    return false;

                if (coyoteTimer <= 0f || (body.linearVelocityY > 0f && !steppedLastStep))
                    return false;

                float steer = command.Steer;

                if (Mathf.Abs(steer) <= steerDeadzone)
                    return false;

                int direction = steer < 0f ? -1 : 1;

                if (!TryFindLip(direction, out float lipTop))
                    return false;

                Bounds box = hull.bounds;
                float rise = lipTop - box.min.y;

                if (rise <= StepClearance || rise > stepHeight)
                    return false;

                float sliver = stepClimbSpeed * fixedDeltaTime;

                var slab = new Vector2(box.center.x, box.max.y + sliver * 0.5f);
                var slabSize = new Vector2(box.size.x - StepClearance * 2f, sliver);

                if (Physics2D.OverlapBox(slab, slabSize, 0f, GroundFilter, Overlaps) > 0)
                    return false;

                body.linearVelocityY = stepClimbSpeed;
                return true;
            }

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;

                float steer = stats.Rooted ? 0f : command.Steer;
                float topSpeed = command.Walk ? walkSpeed : runSpeed;
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                if (!IsGrounded)
                    targetSpeed *= stats.AirSpeedMultiplier;

                if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > steerDeadzone)
                    targetSpeed = 0f;

                targetSpeed += wind.x;

                bool steering = Mathf.Abs(steer) > steerDeadzone;
                float rate = steering ? acceleration : groundFriction;

                if (!IsGrounded)
                    rate *= airControl *
                            (steering ? stats.AirControlMultiplier : stats.AirDragMultiplier);
                else
                    rate *= grip;

                if (TryRunAlongRamp(targetSpeed, topSpeed * stats.MoveSpeedMultiplier,
                        rate * fixedDeltaTime))
                    return;

                body.linearVelocityX =
                    Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * fixedDeltaTime);
            }

            bool TryRunAlongRamp(float targetSpeed, float topSpeed, float change)
            {
                bool wasClimbing = climbedLastStep;
                climbedLastStep = false;

                if (!OnRamp)
                {
                    if (wasClimbing && IsGrounded && !rising &&
                        body.linearVelocityY > 0f && body.linearVelocityY <= topSpeed)
                        body.linearVelocityY = 0f;

                    return false;
                }

                if (rising || body.linearVelocityY > topSpeed)
                    return false;

                var along = new Vector2(groundNormal.y, -groundNormal.x);

                if (along.x < 0f)
                    along = -along;

                float lean = Mathf.Max(along.x, Mathf.Cos(maxSlopeAngle * Mathf.Deg2Rad));

                float cap = topSpeed / lean;

                float carried = Mathf.Clamp(
                    Vector2.Dot(body.linearVelocity, along), -cap, cap);

                float speed = Mathf.MoveTowards(carried, targetSpeed / lean, change / lean);

                body.linearVelocity = along * speed;
                climbedLastStep = speed * along.y > 0f;
                return true;
            }

            void TryJump(Modifiers stats)
            {
                if (!canJump)
                    return;

                bool onGroundOrCoyote = coyoteTimer > 0f;
                bool hasAirJump = airJumpsUsed < stats.ExtraJumps;

                if (bufferTimer <= 0f || (!onGroundOrCoyote && !hasAirJump))
                    return;

                if (!onGroundOrCoyote)
                    airJumpsUsed++;

                bufferTimer = 0f;
                coyoteTimer = 0f;
                rising = true;

                body.linearVelocityY =
                    Mathf.Sqrt(2f * BaseGravity * jumpHeight * stats.JumpHeightMultiplier);
            }

            void ApplyShortHop(bool jumpHeld)
            {
                if (!rising || jumpHeld)
                    return;

                if (body.linearVelocityY > 0f)
                    body.linearVelocityY *= shortHopMultiplier;

                rising = false;
            }

            void ApplyFallGravity(Modifiers stats)
            {
                float floatiness = stats.FallSpeedMultiplier;

                bool falling = !IsGrounded && body.linearVelocityY < 0f;

                body.gravityScale = falling
                    ? baseGravityScale * fallGravityMultiplier * floatiness
                    : baseGravityScale;

                float terminalSpeed = maxFallSpeed * floatiness;
                if (body.linearVelocityY < -terminalSpeed)
                    body.linearVelocityY = -terminalSpeed;
            }

            public int PredictArc(Vector2 launch, Modifiers stats, in ArcSettings look,
                List<Vector2> into, out ArcEnd end)
            {
                into.Clear();
                end = default;

                if (body == null)
                    return 0;

                arcFilter.layerMask = look.Layers;

                float floatiness = stats != null ? stats.FallSpeedMultiplier : 1f;
                float terminal = maxFallSpeed * floatiness;
                float step = Mathf.Max(0.005f, look.Step);
                float updraught = wind.y;

                var point = new Vector2(body.position.x, FeetY + ArcClearance);
                Vector2 velocity = launch;

                float travelled = 0f;
                float flown = 0f;

                bool crossed = false;
                Collider2D met = null;

                into.Add(point);

                for (int i = 0; i < look.Steps && travelled < look.Distance; i++)
                {
                    float gravity = velocity.y < 0f
                        ? BaseGravity * fallGravityMultiplier * floatiness
                        : BaseGravity;

                    velocity.y += (updraught - gravity) * step;

                    if (velocity.y < -terminal)
                        velocity.y = -terminal;

                    Vector2 next = point + velocity * step;
                    Vector2 leg = next - point;
                    float length = leg.magnitude;

                    if (length > Mathf.Epsilon)
                    {
                        int found = Physics2D.Raycast(point, leg / length, arcFilter, Rays, length);

                        for (int hit = 0; hit < found; hit++)
                        {
                            Collider2D what = Rays[hit].collider;

                            if ((groundLayers.value & (1 << what.gameObject.layer)) != 0)
                            {
                                end = new ArcEnd
                                {
                                    Point = Rays[hit].point,
                                    Stopped = true,
                                    Hazard = crossed,
                                    What = crossed ? met : what,
                                    Seconds = flown + step,
                                };

                                into.Add(end.Point);
                                return into.Count;
                            }

                            if (crossed)
                                continue;

                            crossed = true;
                            met = what;
                        }
                    }

                    travelled += length;
                    flown += step;
                    point = next;
                    into.Add(point);
                }

                end = new ArcEnd { Point = point, Hazard = crossed, What = met, Seconds = flown };
                return into.Count;
            }

            public struct ArcSettings
            {
                public LayerMask Layers;
                public float Step;
                public int Steps;
                public float Distance;
            }

            public struct ArcEnd
            {
                public Vector2 Point;
                public bool Stopped;

                public bool Hazard;
                public Collider2D What;

                public float Seconds;
            }
        }
    }
}
