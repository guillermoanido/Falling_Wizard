using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FallingWizard.Player
{
    public enum PlayerState
    {
        Normal,
        OnStaff,
        Ragdoll,
        OnVine,
    }

    [Serializable]
    public class PlayerLogic
    {
        [Header("Parts")]
        public Movement movement = new Movement();
        public Ragdoll ragdoll = new Ragdoll();
        public Health health = new Health();
        public Vine vine = new Vine();
        public Spellbook spellbook = new Spellbook();

        [Header("Fall Damage")]
        [Tooltip("Falls shorter than this many boxes are free.")]
        [Min(0f)] public float safeFallDistance = 3f;

        [Tooltip("Hearts lost per box fallen beyond the safe distance. At 1 a box, a wizard on " +
                 "full health dies on the eighth.")]
        [Min(0f)] public float damagePerBox = 1f;

        [NonSerialized] Staff.Pole pole;
        [NonSerialized] Intent input;
        [NonSerialized] Vector2 pendingWind;
        [NonSerialized] float pendingRampup;
        [NonSerialized] float pendingGroundScale = 1f;

        [NonSerialized] float pendingGrip = 1f;

        public event Action Died;

        [NonSerialized] public bool Invulnerable;

        public Staff.Pole Pole => pole;
        public bool HasPole => pole != null && pole.HasPole;

        public bool StaffIsFree =>
            HasPole && !pole.IsPlanted && pole.IsReady && State == PlayerState.Normal;

        public bool StaffIsPlantedAs(StaffMode mode) =>
            HasPole && pole.IsPlanted && pole.Mode == mode;
        public Modifiers Stats => spellbook.stats;
        public Intent Steering => input;
        public Transform Rig => movement.Rig;
        public PlayerState State { get; private set; }
        public bool IsOnStaff => State == PlayerState.OnStaff;
        public bool IsPeeking { get; private set; }

        public void Attach(Rigidbody2D body, SpriteRenderer sprite, Collider2D hitbox, Staff.Pole staffPole)
        {
            movement.Attach(body, sprite, hitbox);
            ragdoll.Attach(body, sprite != null ? sprite.transform : null, hitbox, movement.groundLayers);
            vine.Attach(body);

            health.SetBonus(Progress.BonusHearts);
            health.RestoreToFull();

            pole = staffPole;
            pole?.BindWielder(body, hitbox);

            spellbook.Attach(this);
        }

        public void Observe(in Intent frame, float deltaTime)
        {
            input = frame;

            if (!health.IsAlive)
                return;

            movement.BufferJump(frame.JumpPressed, deltaTime);
            spellbook.Observe(deltaTime);

            IsPeeking = (State == PlayerState.OnStaff && !(HasPole && pole.IsClimbing)) ||
                        (State == PlayerState.Normal && input.LookingDown);
        }

        public void Simulate(float fixedDeltaTime)
        {
            if (!health.IsAlive)
                return;

            spellbook.TryCast(fixedDeltaTime);
            spellbook.Rebuild();
            ApplyExternalForce(fixedDeltaTime);

            switch (State)
            {
                case PlayerState.OnStaff:
                    UpdateOnStaff(fixedDeltaTime);
                    break;

                case PlayerState.Ragdoll:
                    UpdateRagdoll(fixedDeltaTime);
                    break;

                case PlayerState.OnVine:
                    UpdateOnVine(fixedDeltaTime);
                    break;

                default:
                    UpdateNormal(fixedDeltaTime);
                    break;
            }

            spellbook.TickTimers(fixedDeltaTime);
        }

        public void Validate()
        {
            movement.Validate();
            ragdoll.Validate();
            health.Validate();
            vine.Validate();

            safeFallDistance = Mathf.Max(0f, safeFallDistance);
            damagePerBox = Mathf.Max(0f, damagePerBox);
        }

        public void DrawGizmos(Vector2 origin)
        {
            movement.DrawGizmos(origin);
            pole?.DrawGizmos();
            vine.DrawGizmos();
        }

        public bool Trip() => Trip(movement.TravelDirection);

        public bool Trip(int direction)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            int way = direction < 0 ? -1 : 1;

            ragdoll.Begin(way, way == movement.TravelDirection);

            State = PlayerState.Ragdoll;
            return true;
        }

        public bool Bounce(float heightInBoxes, float sideways, bool resetsFall)
        {
            if (!health.IsAlive || State == PlayerState.OnStaff || State == PlayerState.OnVine)
                return false;

            movement.Launch(heightInBoxes, sideways, resetsFall);
            return true;
        }

        public void Push(Vector2 boxesPerSecond, float rampup, float groundScale)
        {
            pendingWind += boxesPerSecond;
            pendingRampup = Mathf.Max(pendingRampup, rampup);
            pendingGroundScale = groundScale;
        }

        public void Slicken(float grip) => pendingGrip = Mathf.Min(pendingGrip, Mathf.Clamp01(grip));

        public void Shove(Vector2 velocity, float controlLockout)
        {
            if (State != PlayerState.OnStaff && State != PlayerState.OnVine)
                movement.AddImpulse(velocity, controlLockout);
        }

        public void Hurt(int hearts)
        {
            if (hearts <= 0 || !health.IsAlive || Invulnerable || Stats.Shielded)
                return;

            health.TakeDamage(hearts);

            if (!health.IsAlive)
                Die();
        }

        public void Heal(int hearts) => health.Heal(hearts);

        public void RestoreHealth() => health.RestoreToFull();

        public bool GrowHeart(int hearts)
        {
            int taken = Mathf.Min(hearts, health.Room);

            if (taken <= 0)
                return false;

            Progress.TakeHearts(taken);
            health.SetBonus(Progress.BonusHearts);
            health.Heal(taken);
            return true;
        }

        public void BeginFallFrom(float worldY) => movement.BeginFallFrom(worldY);

        public int PredictArc(Vector2 launch, in Movement.ArcSettings look, List<Vector2> into,
            out Movement.ArcEnd end)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
            {
                into.Clear();
                end = default;
                return 0;
            }

            return movement.PredictArc(launch, Stats, look, into, out end);
        }

        public bool Fling(Vector2 velocity, float controlLockout)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            movement.Stop();
            movement.BeginFallFrom(movement.Position.y);
            movement.AddImpulse(velocity, controlLockout);
            return true;
        }

        public bool TryPlantStaff(StaffMode mode)
        {
            if (State != PlayerState.Normal || !HasPole)
                return false;

            if (pole.IsPlanted || !pole.IsReady)
                return false;

            if (!movement.TryFindLedgeEdge(out float edgeX))
                return false;

            if (!pole.Plant(mode, movement.Facing, edgeX))
                return false;

            if (mode == StaffMode.Ladder)
                State = PlayerState.OnStaff;

            return true;
        }

        public void RaiseStaff() => pole?.Raise(true);

        public void LowerStaff() => pole?.Raise(false);

        public bool CanClimbHere =>
            StaffIsFree && !movement.IsAtEdge &&
            movement.TryFindClimb(pole.ClimbUpHeight, out _, out _);

        public bool TryClimbStaff()
        {
            if (State != PlayerState.Normal || !HasPole || pole.IsPlanted || !pole.IsReady)
                return false;

            if (movement.IsAtEdge)
                return false;

            if (!movement.TryFindClimb(pole.ClimbUpHeight, out Vector2 lip, out Vector2 landing))
                return false;

            bool caughtInTheAir = !movement.IsGrounded;

            if (!pole.PlantAsClimb(movement.Facing, lip, landing))
                return false;

            if (caughtInTheAir)
                movement.BeginFallFrom(movement.Position.y);

            State = PlayerState.OnStaff;
            return true;
        }

        public void SetStaffLength(float scale) => pole?.SetLengthScale(scale);

        public void RecoverStaff() => RecoverStaff(false);

        public void RecoverStaff(bool arrived)
        {
            pole?.Release(arrived);

            if (State == PlayerState.OnStaff)
                State = PlayerState.Normal;
        }

        public bool IsOnVine => State == PlayerState.OnVine;

        public bool CanGrabVine =>
            health.IsAlive && State == PlayerState.Normal && vine.CanGrab;

        public bool TryGrabVine(in Vine.Hold spec)
        {
            if (!CanGrabVine || !vine.Grab(spec, movement.Position, movement.Velocity))
                return false;

            State = PlayerState.OnVine;
            return true;
        }

        public float GrabSnapDistance(in Vine.Hold spec) =>
            Vector2.Distance(movement.Position, vine.WouldHangAt(spec, movement.Position));

        public void LetGoOfVine()
        {
            if (State != PlayerState.OnVine)
                return;

            float from = vine.HangPosition.y;
            Vector2 launch = vine.Release();

            State = PlayerState.Normal;

            movement.Stop();
            movement.BeginFallFrom(from);
            movement.AddImpulse(launch, 0f);
        }

        public void DropFromStaff()
        {
            if (State != PlayerState.OnStaff)
                return;

            float from = pole.HangPosition.y;

            pole.Release();
            movement.BeginFallFrom(from);
            State = PlayerState.Normal;
        }

        void UpdateNormal(float fixedDeltaTime)
        {
            movement.FixedTick(input.Movement, Stats, fixedDeltaTime);

            pole?.Face(movement.Facing);

            CheckLanding();
        }

        void UpdateOnStaff(float fixedDeltaTime)
        {
            switch (pole.Slide(input.Lean, fixedDeltaTime))
            {
                case StaffHold.BackOnLedge:
                    RecoverStaff(true);
                    break;

                case StaffHold.LetGo:
                    DropFromStaff();
                    break;
            }
        }

        void UpdateOnVine(float fixedDeltaTime)
        {
            if (input.JumpPressed || !vine.Ride(input.Move, fixedDeltaTime))
                LetGoOfVine();
        }

        void UpdateRagdoll(float fixedDeltaTime)
        {
            movement.SenseGround(fixedDeltaTime);
            CheckLanding();

            if (ragdoll.Tick(fixedDeltaTime, movement.IsGrounded, movement.HorizontalSpeed))
                State = PlayerState.Normal;
        }

        void ApplyExternalForce(float fixedDeltaTime)
        {
            movement.SetGrip(pendingGrip);

            switch (State)
            {
                case PlayerState.OnStaff:
                case PlayerState.OnVine:
                    break;

                case PlayerState.Ragdoll:
                    movement.NudgeVelocity(pendingWind * Stats.WindMultiplier * fixedDeltaTime);
                    break;

                default:
                    movement.ApplyWind(pendingWind * Stats.WindMultiplier, pendingRampup,
                        pendingGroundScale, fixedDeltaTime);
                    break;
            }

            pendingWind = Vector2.zero;
            pendingRampup = 0f;
            pendingGroundScale = 1f;

            pendingGrip = 1f;
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

            Hurt(Mathf.RoundToInt(excess * damagePerBox * Stats.FallDamageMultiplier));
        }

        void Die()
        {
            if (State == PlayerState.OnStaff)
                pole.Release();

            if (State == PlayerState.Ragdoll)
                ragdoll.Cancel();

            if (State == PlayerState.OnVine)
                vine.Cancel();

            State = PlayerState.Normal;
            movement.Stop();

            spellbook.ResetForRun();

            Died?.Invoke();
        }

        public struct Intent
        {
            public Vector2 Move;
            public bool JumpPressed;
            public bool JumpHeld;
            public bool Walk;
            public bool LookingDown;

            public float Lean => Move.y;

            public Command Movement => new Command
            {
                Steer = Move.x,
                JumpHeld = JumpHeld,
                Walk = Walk,
            };
        }

        public struct Command
        {
            public float Steer;
            public bool JumpHeld;
            public bool Walk;
        }

        public class Modifiers
        {
            public float MoveSpeedMultiplier;
            public float JumpHeightMultiplier;
            public float FallSpeedMultiplier;
            public float FallDamageMultiplier;
            public float WindMultiplier;

            public float AirSpeedMultiplier;
            public float AirControlMultiplier;

            public float AirDragMultiplier;

            public int ExtraJumps;
            public bool Shielded;

            public bool Rooted;

            public Modifiers() => Reset();

            public void Reset()
            {
                MoveSpeedMultiplier = 1f;
                JumpHeightMultiplier = 1f;
                FallSpeedMultiplier = 1f;
                FallDamageMultiplier = 1f;
                WindMultiplier = 1f;
                AirSpeedMultiplier = 1f;
                AirControlMultiplier = 1f;
                AirDragMultiplier = 1f;
                ExtraJumps = 0;
                Shielded = false;
                Rooted = false;
            }
        }

        [Serializable]
        public class Health
        {
            [Header("Health")]
            [Tooltip("Hearts a brand new save starts with, before any heart found in a level.")]
            [Min(1)] public int maxHealth = 5;

            [Tooltip("Most hearts that can ever be added on top, across the whole save. Place " +
                     "fewer hearts than this in the game and the cap never comes up - it is here " +
                     "so a generous level cannot quietly make the wizard unkillable.")]
            [Min(0)] public int maxBonusHearts = 4;

            [Tooltip("Seconds of immunity after a hit, so one hazard cannot chain-kill.")]
            [Min(0f)] public float invulnerabilityTime = 0.6f;

            [NonSerialized] float invulnerableUntil;
            [NonSerialized] int bonus;

            public int Max => maxHealth + bonus;
            public int Bonus => bonus;
            public int Current { get; private set; }
            public bool IsAlive => Current > 0;
            public bool IsInvulnerable => Time.time < invulnerableUntil;

            public int Room => Mathf.Max(0, maxBonusHearts - bonus);
            public bool HasRoomToGrow => Room > 0;

            public void SetBonus(int extra)
            {
                bonus = Mathf.Clamp(extra, 0, maxBonusHearts);
                Current = Mathf.Min(Current, Max);
            }

            public void RestoreToFull() => Current = Max;

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

                Current = Mathf.Min(Max, Current + amount);
            }

            public void Validate()
            {
                maxHealth = Mathf.Max(1, maxHealth);
                maxBonusHearts = Mathf.Max(0, maxBonusHearts);
                invulnerabilityTime = Mathf.Max(0f, invulnerabilityTime);
            }
        }

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

        [Serializable]
        public class Ragdoll
        {
            const float MinStandUp = 0.01f;
            const float MinTumbleSpread = 0.1f;

            [Header("Tumble")]
            [Tooltip("How fast the wizard spins as they go over, in degrees per second. They " +
                     "always roll the way they were going.")]
            public float spinSpeed = 520f;

            [Tooltip("How quickly that spin slows, in degrees per second squared. 0 keeps " +
                     "spinning at full rate until they get up.")]
            [Min(0f)] public float spinDown = 240f;

            [Tooltip("How much of their speed carries into the tumble. 1 keeps all of it, so a " +
                     "trip is a loss of footing rather than a wall.")]
            [Range(0f, 1f)] public float momentumKept = 1f;

            [Header("Launch")]
            [Tooltip("Shove ONWARD as they go over, in boxes per second, on top of whatever " +
                     "speed they already had. This is what makes a trip throw you rather than " +
                     "drop you.")]
            [Min(0f)] public float launchForward = 3f;

            [Tooltip("Lift as they go over, in boxes per second. A little goes a long way: it " +
                     "gets them off the floor so the launch is not immediately scrubbed off.")]
            [Min(0f)] public float launchUp = 5f;

            [Tooltip("Least speed they leave the ground with, in boxes per second. Tripping at " +
                     "a crawl and tripping at a sprint then differ in degree, not in kind.")]
            [Min(0f)] public float minimumLaunch = 4f;

            [Header("Getting Up")]
            [Tooltip("Minimum seconds spent tumbling before they can start getting up.")]
            [Min(0f)] public float minimumDuration = 0.9f;

            [Tooltip("Hard limit on a tumble, in seconds. They get up after this whether or not " +
                     "they ever found the ground. Without it, one trip somewhere the ground check " +
                     "cannot see is a wizard who never moves again.")]
            [Min(0.1f)] public float maximumDuration = 3f;

            [Tooltip("How fast the skid bleeds off once they are back on the ground, in boxes " +
                     "per second squared. This rather than physics friction, so the same trip " +
                     "always slides the same distance.")]
            [Min(0f)] public float slideFriction = 9f;

            [Tooltip("They only get up once grounded and slower than this, in boxes per second.")]
            [Min(0f)] public float recoverSpeed = 1.2f;

            [Tooltip("Seconds spent straightening back up.")]
            [Min(0.01f)] public float standUpDuration = 0.35f;

            [Header("Walls")]
            [Tooltip("How much sideways speed a tumbling wizard keeps when they hit a wall, as a " +
                     "fraction of what they arrived with. 0 is a dead stop; 1 is a full rebound.")]
            [Range(0f, 1f)] public float wallBounce = 0.55f;

            [Tooltip("Slowest arrival that still bounces, in boxes per second. Below it they come " +
                     "to rest against the wall instead of chattering off it.")]
            [Min(0f)] public float minimumBounceSpeed = 1.5f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] Transform visual;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float tumbleTimer;
            [NonSerialized] float elapsed;
            [NonSerialized] float standUpTimer;
            [NonSerialized] float standUpFrom;
            [NonSerialized] Collider2D hull;
            [NonSerialized] LayerMask walls;
            [NonSerialized] float bounceReadyAt;

            const float BounceCooldown = 0.08f;
            const float WallSkin = 0.04f;

            static readonly RaycastHit2D[] WallHits = new RaycastHit2D[2];

            public bool IsStandingUp => standUpTimer >= 0f;

            public void Attach(Rigidbody2D rigidbody2d, Transform sprite, Collider2D hitbox, LayerMask wallLayers)
            {
                body = rigidbody2d;
                visual = sprite;
                hull = hitbox;
                walls = wallLayers;
                standUpTimer = -1f;
                angle = 0f;
                bounceReadyAt = 0f;
                Show();
            }

            public void Begin(int direction) => Begin(direction, true);

            public void Begin(int direction, bool keepMomentum)
            {
                spin = -direction * spinSpeed;
                angle = 0f;

                float carried = keepMomentum ? body.linearVelocityX * momentumKept : 0f;
                float thrown = carried + direction * launchForward;

                if (Mathf.Abs(thrown) < minimumLaunch)
                    thrown = direction * minimumLaunch;

                body.linearVelocityX = thrown;

                body.linearVelocityY = Mathf.Max(body.linearVelocityY, launchUp);

                tumbleTimer = minimumDuration;
                standUpTimer = -1f;
                elapsed = 0f;
            }

            public bool Tick(float fixedDeltaTime, bool grounded, float horizontalSpeed)
            {
                if (standUpTimer >= 0f)
                    return StandUp(fixedDeltaTime);

                angle += spin * fixedDeltaTime;
                spin = Mathf.MoveTowards(spin, 0f, spinDown * fixedDeltaTime);
                Show();

                tumbleTimer -= fixedDeltaTime;
                elapsed += fixedDeltaTime;

                BounceOffWalls(fixedDeltaTime);

                if (grounded && slideFriction > 0f)
                    body.linearVelocityX =
                        Mathf.MoveTowards(body.linearVelocityX, 0f, slideFriction * fixedDeltaTime);

                bool waitedLongEnough = elapsed >= maximumDuration;

                if (!waitedLongEnough &&
                    (tumbleTimer > 0f || !grounded || horizontalSpeed > recoverSpeed))
                    return false;

                standUpFrom = angle;
                standUpTimer = 0f;
                return false;
            }

            void BounceOffWalls(float fixedDeltaTime)
            {
                if (wallBounce <= 0f || body == null || hull == null || Time.time < bounceReadyAt)
                    return;

                float speed = body.linearVelocityX;

                if (Mathf.Abs(speed) < minimumBounceSpeed)
                    return;

                var way = new Vector2(speed < 0f ? -1f : 1f, 0f);
                Bounds box = hull.bounds;

                var probe = new Vector2(box.size.x * 0.5f, box.size.y * 0.7f);
                float reach = box.extents.x * 0.5f + Mathf.Abs(speed) * fixedDeltaTime + WallSkin;

                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = walls,
                    useTriggers = false,
                };

                if (Physics2D.BoxCast(box.center, probe, 0f, way, filter, WallHits, reach) <= 0)
                    return;

                body.linearVelocityX = -speed * wallBounce;
                spin = -spin;
                bounceReadyAt = Time.time + BounceCooldown;
            }

            public void Cancel()
            {
                spin = 0f;
                angle = 0f;
                standUpTimer = -1f;
                Show();
            }

            public void Validate()
            {
                standUpDuration = Mathf.Max(MinStandUp, standUpDuration);
                maximumDuration = Mathf.Max(maximumDuration, minimumDuration + MinTumbleSpread);
                wallBounce = Mathf.Clamp01(wallBounce);
                minimumBounceSpeed = Mathf.Max(0f, minimumBounceSpeed);

                if (slideFriction <= 0f && recoverSpeed < minimumLaunch)
                    Debug.LogWarning("Ragdoll.slideFriction is 0 and recoverSpeed is below " +
                                     "minimumLaunch, so a tripped wizard can never slow down " +
                                     "enough to stand back up.");
            }

            bool StandUp(float fixedDeltaTime)
            {
                standUpTimer += fixedDeltaTime;

                float t = Mathf.Clamp01(standUpTimer / Mathf.Max(MinStandUp, standUpDuration));
                angle = Mathf.LerpAngle(standUpFrom, 0f, t);
                Show();

                if (t < 1f)
                    return false;

                Cancel();
                return true;
            }

            void Show()
            {
                if (visual != null)
                    visual.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        [Serializable]
        public class Vine
        {
            const float Epsilon = 0.0001f;

            const float MaxLean = 89f;

            const float MinReach = 0.1f;

            static readonly Color RopeColour = new Color(0.42f, 0.75f, 0.38f);
            const float GripRadius = 0.2f;

            [Header("Swinging")]
            [Tooltip("How hard the wizard hangs, against Unity's own gravity. Match it to the " +
                     "Rigidbody2D's gravity scale and a swing takes as long as a fall of the " +
                     "same size looks like it should. Lower is a slow, floaty rope.")]
            [Min(0f)] public float weight = 3f;

            [Tooltip("How hard left and right kick the swing, in boxes per second squared. This " +
                     "is a push, not a speed: you pump a swing with it the way you would on a " +
                     "real one, and how much you get out depends on when you push.")]
            [Min(0f)] public float swingPush = 26f;

            [Tooltip("How quickly a swing dies down with nobody steering it, per second. 0 swings " +
                     "forever. High numbers hang almost still. This is what settles the wizard " +
                     "back under the knot when they let the stick go.")]
            [Range(0f, 8f)] public float damping = 1.1f;

            [Tooltip("How far the vine will lean either side of straight down, in degrees. It " +
                     "stops dead at the limit rather than bouncing off it.")]
            [Range(0f, 89f)] public float maxSwing = 70f;

            [Header("Climbing")]
            [Tooltip("Fastest the wizard can ever climb, in boxes per second. A spell asks for " +
                     "its own climb speed when it grabs and the smaller of the two wins, so this " +
                     "is a ceiling rather than the number in play.")]
            [Min(0f)] public float climbSpeed = 4f;

            [Tooltip("Closest you can climb to where the vine is tied, in boxes. Keeps the wizard " +
                     "out of the ceiling.")]
            [Min(0.1f)] public float minDepth = 0.75f;

            [Header("Letting Go")]
            [Tooltip("How much of the swing you actually leave with. 1 is exactly the speed you " +
                     "were travelling, which is what the arc has been showing you all along - " +
                     "above that and a release throws further than it looked like it would.")]
            [Min(0f)] public float releaseBoost = 1f;

            [Tooltip("Extra upward speed on letting go, in boxes per second, so a release near " +
                     "the bottom of a swing still clears something.")]
            [Min(0f)] public float releaseLift = 3f;

            [Tooltip("Fastest you can be flung off, in boxes per second. A long vine swung hard " +
                     "would otherwise fire the wizard across the level.")]
            [Min(0f)] public float maxReleaseSpeed = 14f;

            [Tooltip("Seconds before another vine can be caught. Stops one press re-grabbing the " +
                     "vine you just left.")]
            [Min(0f)] public float regrabDelay = 0.35f;

            [Tooltip("Let go the moment the vine runs out under you, rather than hanging on at " +
                     "the very end.")]
            public bool letGoAtTheEnd = false;

            const float MaxHaul = 60f;

            const float Blocked = 0.25f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] float restoreGravity;

            [NonSerialized] Vector2 anchor;
            [NonSerialized] float length;
            [NonSerialized] float limit;
            [NonSerialized] float depth;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float climb;
            [NonSerialized] float readyAt;

            [NonSerialized] Vector2 wanted;
            [NonSerialized] bool steered;

            public bool IsRiding { get; private set; }

            public bool CanGrab => body != null && Time.time >= readyAt;

            public Vector2 Anchor => anchor;

            public struct Hold
            {
                public Vector2 Anchor;
                public float Length;
                public float MaxSwingDegrees;
                public float ClimbSpeed;
                public float SnapLimit;
            }

            public Vector2 HangPosition => PositionAt(angle, depth);

            public float Depth => depth;

            public float Lean => angle * Mathf.Rad2Deg;

            public int SwingDirection => spin < 0f ? -1 : 1;

            public float SwingSpeed => Mathf.Abs(spin) * depth;

            public void Attach(Rigidbody2D wielder)
            {
                body = wielder;

                if (body != null)
                    restoreGravity = body.gravityScale;

                IsRiding = false;
                readyAt = 0f;
            }

            public Vector2 WouldHangAt(in Hold spec, Vector2 from)
            {
                float cap = Mathf.Min(maxSwing, Mathf.Abs(spec.MaxSwingDegrees)) * Mathf.Deg2Rad;
                Vector2 reach = from - spec.Anchor;

                float deep = Mathf.Clamp(reach.magnitude, minDepth, spec.Length);
                float lean = reach.sqrMagnitude < Epsilon
                    ? 0f
                    : Mathf.Clamp(Mathf.Atan2(reach.x, -reach.y), -cap, cap);

                return spec.Anchor + new Vector2(Mathf.Sin(lean), -Mathf.Cos(lean)) * deep;
            }

            public bool Grab(in Hold spec, Vector2 from, Vector2 carried)
            {
                if (body == null || IsRiding || spec.Length <= minDepth)
                    return false;

                if (Vector2.Distance(from, WouldHangAt(spec, from)) > spec.SnapLimit)
                    return false;

                anchor = spec.Anchor;
                length = spec.Length;
                limit = Mathf.Min(maxSwing, Mathf.Abs(spec.MaxSwingDegrees)) * Mathf.Deg2Rad;

                climb = Mathf.Min(climbSpeed, Mathf.Max(0f, spec.ClimbSpeed));

                Vector2 reach = from - anchor;

                depth = Mathf.Clamp(reach.magnitude, minDepth, length);
                angle = reach.sqrMagnitude < Epsilon
                    ? 0f
                    : Mathf.Clamp(Mathf.Atan2(reach.x, -reach.y), -limit, limit);

                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                spin = depth <= Epsilon ? 0f : Vector2.Dot(carried, along) / depth;

                restoreGravity = body.gravityScale;
                body.gravityScale = 0f;
                body.linearVelocity = Vector2.zero;

                steered = false;
                IsRiding = true;
                return true;
            }

            public bool Ride(Vector2 lean, float fixedDeltaTime)
            {
                if (!IsRiding || body == null || fixedDeltaTime <= Epsilon)
                    return false;

                if (steered && (body.position - wanted).sqrMagnitude > Blocked * Blocked)
                    spin = 0f;

                Vector2 real = body.position - anchor;

                if (real.sqrMagnitude > Epsilon)
                {
                    depth = Mathf.Clamp(real.magnitude, minDepth, length);
                    angle = Mathf.Clamp(Mathf.Atan2(real.x, -real.y), -limit, limit);
                }

                depth = Mathf.Clamp(depth - lean.y * climb * fixedDeltaTime, minDepth, length);

                float rope = Mathf.Max(depth, MinReach);
                float gravity = Mathf.Abs(Physics2D.gravity.y) * weight;

                float pull = -(gravity / rope) * Mathf.Sin(angle);
                float push = lean.x * (swingPush / rope);

                spin += (pull + push) * fixedDeltaTime;
                spin *= Mathf.Clamp01(1f - damping * fixedDeltaTime);

                angle += spin * fixedDeltaTime;

                if (Mathf.Abs(angle) > limit)
                {
                    angle = Mathf.Clamp(angle, -limit, limit);

                    if (spin * angle > 0f)
                        spin = 0f;
                }

                wanted = HangPosition;
                steered = true;

                Vector2 haul = (wanted - body.position) / fixedDeltaTime;

                body.linearVelocity = Vector2.ClampMagnitude(haul, MaxHaul);

                return !letGoAtTheEnd || depth < length - Epsilon || lean.y >= 0f;
            }

            public Vector2 Release()
            {
                if (body != null)
                    body.gravityScale = restoreGravity;

                IsRiding = false;
                readyAt = Time.time + regrabDelay;

                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = Mathf.Clamp(spin * depth * releaseBoost,
                    -maxReleaseSpeed, maxReleaseSpeed);

                spin = 0f;

                return along * speed + Vector2.up * releaseLift;
            }

            public void Cancel()
            {
                if (body != null)
                    body.gravityScale = restoreGravity;

                IsRiding = false;
                spin = 0f;
                steered = false;
                readyAt = 0f;
            }

            public void Validate()
            {
                weight = Mathf.Max(0f, weight);
                swingPush = Mathf.Max(0f, swingPush);
                damping = Mathf.Clamp(damping, 0f, 8f);
                maxSwing = Mathf.Clamp(maxSwing, 0f, MaxLean);
                climbSpeed = Mathf.Max(0f, climbSpeed);
                minDepth = Mathf.Max(MinReach, minDepth);
                releaseBoost = Mathf.Max(0f, releaseBoost);
                releaseLift = Mathf.Max(0f, releaseLift);
                maxReleaseSpeed = Mathf.Max(0f, maxReleaseSpeed);
                regrabDelay = Mathf.Max(0f, regrabDelay);
            }

            public void DrawGizmos()
            {
                if (!IsRiding)
                    return;

                Gizmos.color = RopeColour;
                Gizmos.DrawLine(anchor, HangPosition);
                Gizmos.DrawWireSphere(HangPosition, GripRadius);
            }

            Vector2 PositionAt(float lean, float distance) =>
                anchor + new Vector2(Mathf.Sin(lean), -Mathf.Cos(lean)) * distance;
        }

        [Serializable]
        public class Spellbook
        {
            public const int SlotCount = Progress.SlotCount;

            const float ExplainAfter = 0.35f;

            public static readonly string[] SlotActions =
                { "Spell1", "Spell2", "Spell3", "Spell4" };

            [Header("Spells")]
            [Tooltip("The catalogue every slot draws from. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

            [Tooltip("Print a line to the console whenever a press comes to nothing, saying " +
                     "which spell refused and why. Editor only. Leave it on while building a " +
                     "level - a spell that silently does nothing is the hardest kind to chase.")]
            public bool explainRefusals = true;

            [NonSerialized] public Modifiers stats = new Modifiers();
            [NonSerialized] Slot[] slots = Array.Empty<Slot>();
            [NonSerialized] PlayerLogic owner;

            [NonSerialized] readonly Dictionary<Ability, object> scratch =
                new Dictionary<Ability, object>();

            public event Action Changed;

            public int Version { get; private set; }

            public IReadOnlyList<Slot> Slots => slots;

            public AbilityBook Book => book;

            public void Attach(PlayerLogic player)
            {
                owner = player;

                if (book == null)
                    book = Resources.Load<AbilityBook>(AbilityBook.ResourcePath);

                if (book == null)
                {
                    Debug.LogError("No spellbook found. Create Assets/Resources/Spellbook.asset " +
                                   "from Assets > Create > Falling Wizard > Spellbook, and put " +
                                   "the Staff in both of its lists.");
                    slots = Array.Empty<Slot>();
                    return;
                }

                slots = new Slot[SlotCount];

                for (int i = 0; i < SlotCount; i++)
                    slots[i] = new Slot { Action = Controls.Player(SlotActions[i]) };

                Seed(book);
                Reload();
            }

            public static void Seed(AbilityBook book)
            {
                if (book == null)
                    return;

                foreach (Ability spell in book.known)
                    if (spell != null)
                        Progress.Grant(spell.Key);

                foreach (Ability spell in book.spells)
                    if (spell != null && spell.locked && spell.fixedSlot >= 0 &&
                        Progress.Owns(spell.Key) && Progress.SlotHolding(spell.Key) < 0)
                        Progress.Equip(spell.fixedSlot, spell.Key);
            }

            public void Reload()
            {
                if (slots.Length == 0)
                    return;

                Seed(book);

                for (int i = 0; i < SlotCount; i++)
                {
                    Slot slot = slots[i];
                    Ability next = book.Find(Progress.EquippedIn(i));

                    if (next != null && !Progress.Owns(next.Key))
                        next = null;

                    slot.Rank = next != null ? Progress.Rank(next.Key) : 0;

                    if (slot.Ability == next)
                        continue;

                    if (slot.Ability != null)
                    {
                        if (slot.IsLit)
                            slot.Ability.OnEnded(owner);

                        slot.Ability.OnUnequipped(owner);
                    }

                    slot.Fill(next);
                    next?.OnEquipped(owner);
                }

                Version++;
                Changed?.Invoke();
            }

            public bool Equip(Ability spell, int slot)
            {
                if ((uint)slot >= SlotCount)
                    return false;

                if (spell != null && (!Progress.Owns(spell.Key) || spell.locked))
                    return false;

                Ability leaving = book.Find(Progress.EquippedIn(slot));

                if (leaving != null && leaving.locked)
                    return false;

                Progress.Place(slot, spell != null ? spell.Key : string.Empty);
                Reload();
                return true;
            }

            public T StateOf<T>(Ability spell) where T : class, new()
            {
                if (spell == null)
                    return null;

                if (scratch.TryGetValue(spell, out object held) && held is T kept)
                    return kept;

                var fresh = new T();
                scratch[spell] = fresh;
                return fresh;
            }

            public void Extinguish(Ability spell)
            {
                Slot slot = Array.Find(slots, s => s.Ability == spell);

                if (slot == null || !slot.IsLit)
                    return;

                PutOut(slot);
            }

            void PutOut(Slot slot)
            {
                slot.LitLeft = 0f;
                slot.CooldownLeft = slot.Ability.cooldown;
                slot.Ability.OnEnded(owner);
            }

            public bool Knows(Ability spell) => spell != null && Progress.Owns(spell.Key);

            public Slot SlotOf(Ability spell) =>
                spell == null ? null : Array.Find(slots, s => s.Ability == spell);

            public int RankOf(Ability spell)
            {
                Slot slot = SlotOf(spell);
                return slot != null ? slot.Rank : 0;
            }

            public bool IsEquipped(Ability spell) =>
                spell != null && Array.Exists(slots, s => s.Ability == spell);

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (paused || slot.Action == null)
                    {
                        slot.Buffer = 0f;

                        if (slot.HeldFor > 0f && slot.Ability != null)
                        {
                            slot.Ability.OnChargeLost(owner);
                            slot.DropCharge();
                        }

                        continue;
                    }

                    bool pressed = slot.Action.WasPressedThisFrame();

                    if (slot.Ability == null)
                    {
                        if (pressed)
                            Explain(i, null, "there is nothing in that slot");

                        slot.Buffer = 0f;
                        continue;
                    }

                    slot.Held = slot.Action.IsPressed();

                    if (slot.Action.WasReleasedThisFrame() && slot.HeldFor > 0f)
                        slot.ReleasedAfter = slot.HeldFor;

                    if (pressed)
                    {
                        slot.Buffer = slot.Ability.pressBuffer;
                        slot.Fired = false;
                        slot.Explained = false;
                        continue;
                    }

                    if (slot.Ability.chargesOnHold)
                        continue;

                    float had = slot.Buffer;
                    slot.Buffer -= deltaTime;

                    if (had > 0f && slot.Buffer <= 0f && !slot.Fired)
                        Explain(i, slot.Ability, Refusal(slot));
                }
            }

            string Refusal(Slot slot)
            {
                if (slot.CooldownLeft > 0f)
                    return $"it is still cooling down, {slot.CooldownLeft:0.0}s to go";

                if (!slot.HasUsesLeft)
                    return "it has no casts left in this level";

                return slot.Ability.WhyNot(owner);
            }

            void Explain(int slot, Ability spell, string reason)
            {
#if UNITY_EDITOR
                if (!explainRefusals || string.IsNullOrEmpty(reason))
                    return;

                string named = spell != null ? spell.Name : $"Slot {slot + 1}";

                Debug.LogWarning($"{named} did not cast: {reason}.");
#endif
            }

            public void TryCast(float fixedDeltaTime)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    Slot slot = slots[i];

                    if (slot.Ability == null)
                        continue;

                    if (slot.Ability.chargesOnHold)
                    {
                        AdvanceCharge(i, slot, fixedDeltaTime);
                        continue;
                    }

                    if (slot.Buffer <= 0f || !slot.IsReady)
                        continue;

                    if (!slot.Ability.CanCast(owner))
                        continue;

                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.BeginCast();
                }
            }

            void AdvanceCharge(int index, Slot slot, float fixedDeltaTime)
            {
                if (slot.ReleasedAfter >= 0f)
                {
                    float held = slot.ReleasedAfter;

                    slot.ReleasedAfter = -1f;
                    slot.HeldFor = 0f;

                    slot.Ability.OnReleased(owner, held);
                    return;
                }

                if (!slot.Held || !slot.IsReady)
                {
                    slot.HeldFor = 0f;
                    return;
                }

                slot.HeldFor += fixedDeltaTime;
                slot.Ability.OnHeld(owner, slot.HeldFor, fixedDeltaTime);

                if (slot.Explained || slot.HeldFor < ExplainAfter || slot.Ability.CanCast(owner))
                    return;

                slot.Explained = true;
                Explain(index, slot.Ability, Refusal(slot));
            }

            public bool Fire(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsReady)
                    return false;

                slot.BeginCast();
                return true;
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Ability != null)
                        slot.Ability.ModifyStats(owner, stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit)
                        slot.Ability.ModifyStatsWhileLit(owner, stats);
            }

            public void TickTimers(float fixedDeltaTime)
            {
                foreach (Slot slot in slots)
                {
                    if (slot.CooldownLeft > 0f)
                        slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.Ability.OnLit(owner, fixedDeltaTime);

                    if (!slot.IsLit)
                        continue;

                    slot.LitLeft -= fixedDeltaTime;

                    if (slot.LitLeft <= 0f)
                        PutOut(slot);
                }
            }

            public void ResetForRun()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Fill(slot.Ability);
                    slot.Ability?.OnRunReset(owner);
                }
            }

            public class Slot
            {
                public Ability Ability;
                public InputAction Action;
                public float Buffer;
                public bool Fired;

                public int Rank;

                public bool Held;
                public float HeldFor;

                public bool Explained;

                public float ReleasedAfter = -1f;
                public float LitLeft;
                public float CooldownLeft;
                public int UsesLeft;

                public void Fill(Ability spell)
                {
                    Ability = spell;
                    Buffer = 0f;
                    LitLeft = 0f;
                    CooldownLeft = 0f;
                    UsesLeft = spell != null ? spell.usesPerLevel : 0;

                    DropCharge();
                }

                public void DropCharge()
                {
                    Held = false;
                    HeldFor = 0f;
                    ReleasedAfter = -1f;
                    Explained = false;
                }

                public void BeginCast()
                {
                    Fired = true;
                    LitLeft = Ability.activeDuration;

                    if (Ability.usesPerLevel > 0)
                        UsesLeft = Mathf.Max(0, UsesLeft - 1);

                    if (LitLeft <= 0f)
                        CooldownLeft = Ability.cooldown;
                }

                public bool IsEmpty => Ability == null;

                public bool IsLit => LitLeft > 0f;

                public bool HasUsesLeft =>
                    Ability == null || Ability.usesPerLevel <= 0 || UsesLeft > 0;

                public bool IsReady => Ability != null && CooldownLeft <= 0f && HasUsesLeft;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;
            }
        }
    }
}
