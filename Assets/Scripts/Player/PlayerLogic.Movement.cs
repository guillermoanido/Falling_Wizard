using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        // How the wizard gets about, in five files. This one is what movement IS - every
        // tunable the inspector shows, the numbers derived from them, the two ticks, and the
        // forces the world pushes in from outside. The other four are Sensing (what is under
        // and in front of the wizard), Locomotion (what moves them), and Arc (where a fling
        // would land).
        //
        // Every serialized field lives HERE, together. Unity lays the inspector out in
        // reflection order and reflection order across partial files is not promised, so
        // splitting the fields would scramble the [Header]s.
        [Serializable]
        public partial class Movement
        {
            const float MinGravityScale = 0.01f;

            // Unity has a fixed 32 layers, and the ground mask is checked against all of them
            // when the wizard reports that it cannot find any floor.
            const int LayerCount = 32;

            // A ground probe thinner than this in either direction misses the floor between
            // physics steps, so Validate refuses to let one be typed in.
            static readonly Vector2 MinGroundCheck = new Vector2(0.05f, 0.01f);

            const float MinTravelSpeed = 0.1f;

            // How far up inside the wizard the slope rays begin. queriesStartInColliders is on
            // in this project, so a ray that starts already touching the floor answers "flat"
            // whatever the floor is really doing.
            const float SlopeProbeLift = 0.25f;

            // The hair of daylight a step leaves between the soles and the lip they have just
            // been put on top of, so the move ends beside the geometry rather than inside it.
            const float StepClearance = 0.02f;

            // How far PAST the wall's face the downward probe starts when it goes looking for the
            // top of it. Cast exactly on the face and the ray skims straight down the wall it is
            // trying to measure, which answers with the floor at the bottom of it.
            const float ClimbInset = 0.05f;

            // Enough to clear the floor underfoot without meaningfully lying about where the
            // arc begins - well under a tile, so the drawn landing point is still right.
            const float ArcClearance = 0.15f;

            const float GroundlessWarning = 3f;
            const float NearbyGround = 1f;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>(8);
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>(4);

            // Triggers ON, unlike the ground filter: the arc wants to know it is going to land
            // on a slime, and every hazard in this game is a trigger you pass through.
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
            public bool canJump = true;

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

            [Header("Climbing")]
            [Tooltip("How far past the toes to look for a WALL to raise the staff against, in " +
                     "boxes. Only wide enough that the wizard does not have to be pressed " +
                     "pixel-perfectly against it - they walk into a wall and stop flush with it, " +
                     "so a quarter box is already generous. Keep it under Ledge Check Ahead, or " +
                     "a wall and a drop can both answer at once and which one you get depends on " +
                     "which probe ran first.")]
            [Min(0.05f)] public float climbReach = 0.25f;

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

            // How much of the acceleration and the ground friction below the floor underfoot
            // actually gives back, 0 to 1. Not serialized and not a Modifier: it is written every
            // fixed step from PlayerLogic.ApplyExternalForce and is 1 on any ordinary floor.
            [NonSerialized] float grip = 1f;

            // Which way the floor underfoot is tilted, and whether the last step drove the
            // wizard along that tilt. The second one is only ever read to take the climb back
            // off again the moment the ramp runs out - see Run.
            [NonSerialized] Vector2 groundNormal = Vector2.up;
            [NonSerialized] float groundAngle;
            [NonSerialized] bool climbedLastStep;

            public bool IsGrounded { get; private set; }

            // Counts up every time the wizard leaves the ground. A spell that should only fire
            // once per fall remembers the number it last fired on and compares - which needs no
            // per-frame hook, and cannot drift out of step the way a flag being cleared somewhere
            // else would.
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

            // Where the soles actually meet the world, measured off the COLLIDER.
            //
            // FeetY is the centre of the ground probe, which hangs deliberately below the feet by
            // groundCheckSkin - and drifts further the moment the collider is resized without the
            // probe being refitted, which is exactly what had happened here: the probe sat almost
            // a tenth of a box under the boots. Grid maths built on it read the FLOOR row as the
            // row the wizard was standing in, and every spell that puts a tile down was one row
            // out. Asking the collider cannot drift.
            public Vector2 Footing => hull != null
                ? new Vector2(hull.bounds.center.x, hull.bounds.min.y)
                : new Vector2(Position.x, FeetY + groundCheckSkin);

            // Half the wizard's own width, worked back out of the ground probe so it is
            // available in the editor too - FitGroundCheckTo sizes that probe as the collider's
            // width times groundCheckWidthFactor, so this undoes exactly that.
            float HalfWidth => hull != null
                ? hull.bounds.extents.x
                : (groundCheckWidthFactor > 0f
                    ? groundCheckSize.x / groundCheckWidthFactor * 0.5f
                    : 0f);

            // Where BOTH ground probes hang from: under the middle of the footprint, not under
            // the middle of the transform. A collider carrying an x offset of its own puts those
            // in different places, and the ledge check would then disagree with the ground check
            // about which foot is over the drop.
            Vector2 ProbeOrigin => body.position + groundCheckOffset;
            // What every ground query in this class asks with. Built to order rather than
            // cached in Attach - it is a struct, so there is nothing to allocate - which means a
            // mask corrected in the inspector mid-play takes effect on the very next step
            // instead of on the next scene load, and no query can be written that forgets to
            // refresh it first.
            //
            // Triggers OFF. The project queries them by default, so without this any trigger
            // sitting on the Ground layer becomes floor the wizard can stand on.
            ContactFilter2D GroundFilter => new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayers,
                useTriggers = false,
            };

            // Gravity as the wizard would fall under it at rest, in boxes per second squared.
            // The jump, the bounce and the drawn arc all size themselves off this one number, so
            // none of them can drift from the others or from Project Settings.
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

            // Presses are buffered even with canJump off. Nothing but TryJump ever reads the
            // timer, so it costs nothing, and it means the switch can be flicked back on in the
            // middle of a playtest with no state anywhere that needs resetting first.
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

                // AFTER Run, so the sideways speed it just chose is what carries the wizard
                // forward off the lip on the next physics step, and BEFORE TryJump, so a jump
                // buffered on the same step launches from the height they have just gained.
                TryStepUp(command, stats);

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

            // Set outright rather than eased into, unlike the wind above. Ice has a hard edge you
            // can see, and a grip that ramped in over a few steps would put the slippery part of
            // the patch somewhere other than where the patch is drawn.
            public void SetGrip(float value) => grip = Mathf.Clamp01(value);

            public void AddImpulse(Vector2 velocity, float controlLockout)
            {
                if (body == null)
                    return;

                body.linearVelocity += velocity;
                rising = false;

                // So the ramp's own tidy-up does not read this shove as leftover climb and take
                // it straight back off again.
                climbedLastStep = false;

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

                if (!resetsFall)
                    return;

                highestPoint = body.position.y;
                IsGrounded = false;
                coyoteTimer = 0f;
            }

            public void FitGroundCheckTo(Collider2D collider2d)
            {
                // Measured from the collider's BOTTOM and its own middle, not from half its
                // height about the transform. A collider carrying an offset - which is what
                // Unity's fit-to-sprite button writes - puts those in different places, and the
                // probe ends up floating inside the wizard or trailing below their boots.
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

                // One box is the height of a tile, so a step assist that reaches one has quietly
                // turned every wall in the game into something you walk up.
                if (stepHeight >= 1f)
                    Debug.LogWarning("Movement.stepHeight is a whole box or more, so the wizard " +
                                     "walks up any one-tile wall without jumping. It is meant " +
                                     "for tile seams and the teeth along a ramp, not for steps.");

                // No warning about climbReach against ledgeCheckAhead, deliberately. The wall
                // probe does reach further than the ledge probe once the wizard's own half-width
                // is counted, and that is fine: a drop always wins, because both CanClimbHere and
                // TryClimbStaff refuse outright while IsAtEdge is up. A warning here would be a
                // guarantee about a race that cannot happen, which is worse than none - somebody
                // would trust it.

                // A walking wizard refuses to step off a ledge, and one box is a whole tile, so
                // under that they stop dead at the top of every step of a staircase.
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

                // The tallest lip that gets walked up rather than stopped at, drawn where the
                // step actually looks for it.
                if (stepHeight <= 0f)
                    return;

                Gizmos.color = Color.green;
                float soles = origin.y + groundCheckOffset.y + groundCheckSkin;
                var ahead = new Vector2(origin.x + groundCheckOffset.x + Facing * stepReach, soles);
                Gizmos.DrawLine(ahead, ahead + Vector2.up * stepHeight);

                // How far forward the staff looks for a wall to be raised against, drawn from
                // the TOES at the height the search really starts from. Drawn from the middle it
                // sat entirely inside the collider, invisible, and read as a third of its true
                // length - which would have had somebody placing walls too close.
                Gizmos.color = new Color(0.98f, 0.86f, 0.42f);
                var reachFrom = new Vector2(
                    origin.x + groundCheckOffset.x + Facing * HalfWidth, soles + stepHeight);
                Gizmos.DrawLine(reachFrom, reachFrom + new Vector2(Facing * climbReach, 0f));
            }
        }
    }
}
