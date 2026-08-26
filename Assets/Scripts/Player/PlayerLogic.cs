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

        public event Action Died;

        public Staff.Pole Pole => pole;
        public bool HasPole => pole != null && pole.HasPole;

        public bool StaffIsFree => HasPole && !pole.IsPlanted && State == PlayerState.Normal;

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
            movement.Attach(body, sprite);
            ragdoll.Attach(body, sprite != null ? sprite.transform : null);
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

            movement.Tick(frame.JumpPressed, deltaTime);
            spellbook.Observe(deltaTime);

            IsPeeking = State == PlayerState.OnStaff ||
                        (State == PlayerState.Normal && input.LookingDown);
        }

        public void Simulate(float fixedDeltaTime)
        {
            if (!health.IsAlive)
                return;

            spellbook.TryCast();
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

        public bool Trip()
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            ragdoll.Begin(movement.TravelDirection);
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

        public void Shove(Vector2 velocity, float controlLockout)
        {
            if (State != PlayerState.OnStaff && State != PlayerState.OnVine)
                movement.AddImpulse(velocity, controlLockout);
        }

        public void Hurt(int hearts)
        {
            if (hearts <= 0 || !health.IsAlive || Stats.Shielded)
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

        public bool TryPlantStaff(StaffMode mode)
        {
            if (State != PlayerState.Normal || !HasPole)
                return false;

            if (pole.IsPlanted)
                return false;

            if (!movement.TryFindLedgeEdge(out float edgeX))
                return false;

            if (!pole.Plant(mode, movement.Facing, edgeX))
                return false;

            if (mode == StaffMode.Ladder)
                State = PlayerState.OnStaff;

            return true;
        }

        public void RecoverStaff()
        {
            pole?.Release();

            if (State == PlayerState.OnStaff)
                State = PlayerState.Normal;
        }

        public bool IsOnVine => State == PlayerState.OnVine;

        public bool CanGrabVine =>
            health.IsAlive && State == PlayerState.Normal && vine.CanGrab;

        public bool TryGrabVine(Vector2 anchor, float length, float maxSwingDegrees)
        {
            if (!CanGrabVine)
                return false;

            if (!vine.Grab(anchor, length, maxSwingDegrees, movement.Position))
                return false;

            State = PlayerState.OnVine;
            return true;
        }

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
                    RecoverStaff();
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
            public int ExtraJumps;
            public bool Shielded;

            public Modifiers() => Reset();

            public void Reset()
            {
                MoveSpeedMultiplier = 1f;
                JumpHeightMultiplier = 1f;
                FallSpeedMultiplier = 1f;
                FallDamageMultiplier = 1f;
                WindMultiplier = 1f;
                ExtraJumps = 0;
                Shielded = false;
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

            const float MinTravelSpeed = 0.1f;

            const float GroundlessWarning = 3f;
            const float NearbyGround = 1f;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>(8);
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>(4);

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
            [Tooltip("Height of a full jump, in boxes. The launch speed is worked out from gravity.")]
            [Min(0f)] public float jumpHeight = 2f;

            [Tooltip("Grace period after walking off a ledge where a jump still counts.")]
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

            [Tooltip("A gap deeper than this counts as a ledge worth stopping at.")]
            [Min(0f)] public float ledgeCheckDepth = 0.75f;

            [Tooltip("How finely to close in on the exact lip when planting the staff.")]
            [Range(4, 16)] public int edgeSearchSteps = 8;

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
            [NonSerialized] ContactFilter2D groundFilter;

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

            public bool IsGrounded { get; private set; }
            public bool IsAtEdge { get; private set; }

            [NonSerialized] float approachVelocityX;

            public float ApproachSpeed => Mathf.Abs(approachVelocityX);

            public int TravelDirection =>
                Mathf.Abs(approachVelocityX) > MinTravelSpeed
                    ? (approachVelocityX < 0f ? -1 : 1)
                    : Facing;

            public int Facing { get; private set; } = 1;
            public Vector2 Position => body == null ? Vector2.zero : body.position;
            public Transform Rig => body == null ? null : body.transform;
            public float FeetY => Position.y + groundCheckOffset.y;
            public float HorizontalSpeed => body == null ? 0f : Mathf.Abs(body.linearVelocityX);
            public float VerticalSpeed => body == null ? 0f : body.linearVelocityY;

            public void Attach(Rigidbody2D rigidbody2d, SpriteRenderer spriteRenderer)
            {
                body = rigidbody2d;
                sprite = spriteRenderer;
                baseGravityScale = Mathf.Max(MinGravityScale, body.gravityScale);
                highestPoint = body.position.y;

                groundFilter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = groundLayers,
                    useTriggers = false,
                };

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

            public void Tick(bool jumpPressedThisFrame, float deltaTime)
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
                UpdateGroundedState(fixedDeltaTime);
                Run(command, stats, fixedDeltaTime);
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
            }

            public void SenseGround(float fixedDeltaTime) => UpdateGroundedState(fixedDeltaTime);

            public void BeginFallFrom(float height)
            {
                highestPoint = height;
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

            public void AddImpulse(Vector2 velocity, float controlLockout)
            {
                if (body == null)
                    return;

                body.linearVelocity += velocity;
                rising = false;
                lockout = Mathf.Max(lockout, controlLockout);
            }

            public void NudgeVelocity(Vector2 velocity)
            {
                if (body != null)
                    body.linearVelocity += velocity;
            }

            public void Launch(float heightInBoxes, float sideways, bool resetsFall)
            {
                float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;

                body.linearVelocityY = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, heightInBoxes));

                if (sideways != 0f)
                    body.linearVelocityX += sideways;

                rising = false;

                if (!resetsFall)
                    return;

                highestPoint = body.position.y;
                IsGrounded = false;
                coyoteTimer = 0f;
            }

            public void FitGroundCheckTo(Collider2D collider2d)
            {
                groundCheckOffset = new Vector2(0f, -collider2d.bounds.extents.y - groundCheckSkin);
                groundCheckSize = new Vector2(
                    collider2d.bounds.size.x * groundCheckWidthFactor, groundCheckThickness);
            }

            public bool TryFindLedgeEdge(out float edgeX)
            {
                edgeX = body.position.x;

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

                edgeX = body.position.x + Facing * air;
                return true;
            }

            public void Validate()
            {
                runSpeed = Mathf.Max(0f, runSpeed);
                walkSpeed = Mathf.Clamp(walkSpeed, 0f, runSpeed);
                groundCheckSize = Vector2.Max(groundCheckSize, new Vector2(0.05f, 0.01f));

                int playerLayer = LayerMask.NameToLayer("Player");
                if (playerLayer >= 0 && (groundLayers.value & (1 << playerLayer)) != 0)
                    Debug.LogWarning("Movement.groundLayers includes the Player layer, so the " +
                                     "wizard will try to stand on their own collider.");
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
                if (Mathf.Abs(steer) > steerDeadzone)
                    Facing = steer < 0f ? -1 : 1;

                if (sprite != null)
                    sprite.flipX = Facing < 0;
            }

            void UpdateGroundedState(float fixedDeltaTime)
            {
                bool wasGrounded = IsGrounded;

                groundFilter.layerMask = groundLayers;

                int count = Physics2D.OverlapBox(
                    body.position + groundCheckOffset, groundCheckSize, 0f, groundFilter, Overlaps);

                IsGrounded = count > 0;

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
                groundFilter.layerMask = groundLayers;

                Vector2 wide = groundCheckSize + Vector2.one * NearbyGround;

                return Physics2D.OverlapBox(
                    body.position + groundCheckOffset, wide, 0f, groundFilter, Overlaps) > 0;
            }

            static string LayerNames(LayerMask mask)
            {
                var listed = new List<string>();

                for (int layer = 0; layer < 32; layer++)
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
                Vector2 probe = body.position + new Vector2(Facing * ahead, groundCheckOffset.y);
                groundFilter.layerMask = groundLayers;

                return Physics2D.Raycast(probe, Vector2.down, groundFilter, Rays, ledgeCheckDepth) > 0;
            }

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;

                float steer = command.Steer;
                float topSpeed = command.Walk ? walkSpeed : runSpeed;
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > steerDeadzone)
                    targetSpeed = 0f;

                targetSpeed += wind.x;

                float rate = Mathf.Abs(steer) > steerDeadzone ? acceleration : groundFriction;
                if (!IsGrounded)
                    rate *= airControl;

                body.linearVelocityX =
                    Mathf.MoveTowards(body.linearVelocityX, targetSpeed, rate * fixedDeltaTime);
            }

            void TryJump(Modifiers stats)
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
                body.linearVelocityY =
                    Mathf.Sqrt(2f * gravity * jumpHeight * stats.JumpHeightMultiplier);
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
        public class Ragdoll
        {
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

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] Transform visual;
            [NonSerialized] float angle;
            [NonSerialized] float spin;
            [NonSerialized] float tumbleTimer;
            [NonSerialized] float elapsed;
            [NonSerialized] float standUpTimer;
            [NonSerialized] float standUpFrom;

            public bool IsStandingUp => standUpTimer >= 0f;

            public void Attach(Rigidbody2D rigidbody2d, Transform sprite)
            {
                body = rigidbody2d;
                visual = sprite;
                standUpTimer = -1f;
                angle = 0f;
                Show();
            }

            public void Begin(int direction)
            {
                spin = -direction * spinSpeed;
                angle = 0f;

                float thrown = body.linearVelocityX * momentumKept + direction * launchForward;

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

            public void Cancel()
            {
                spin = 0f;
                angle = 0f;
                standUpTimer = -1f;
                Show();
            }

            public void Validate()
            {
                standUpDuration = Mathf.Max(0.01f, standUpDuration);
                maximumDuration = Mathf.Max(maximumDuration, minimumDuration + 0.1f);

                if (slideFriction <= 0f && recoverSpeed < minimumLaunch)
                    Debug.LogWarning("Ragdoll.slideFriction is 0 and recoverSpeed is below " +
                                     "minimumLaunch, so a tripped wizard can never slow down " +
                                     "enough to stand back up.");
            }

            bool StandUp(float fixedDeltaTime)
            {
                standUpTimer += fixedDeltaTime;

                float t = Mathf.Clamp01(standUpTimer / Mathf.Max(0.01f, standUpDuration));
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

            [Header("Riding")]
            [Tooltip("Speed left and right along the swing, in boxes per second. It is the same " +
                     "however high up the vine you are, so a swing always takes as long as it " +
                     "looks like it should.")]
            [Min(0f)] public float swingSpeed = 4f;

            [Tooltip("Speed climbing up and down the vine, in boxes per second.")]
            [Min(0f)] public float climbSpeed = 4f;

            [Tooltip("How far the vine will lean either side of straight down, in degrees. " +
                     "Past about 80 it starts to look like a pole rather than a rope.")]
            [Range(0f, 89f)] public float maxSwing = 65f;

            [Tooltip("Closest you can climb to where the vine is tied, in boxes. Keeps the wizard " +
                     "out of the ceiling.")]
            [Min(0.1f)] public float minDepth = 0.75f;

            [Header("Letting Go")]
            [Tooltip("Speed you leave at, in boxes per second, along whichever way you were " +
                     "swinging. Fixed on purpose - a swing you can measure by eye is a swing you " +
                     "can aim.")]
            [Min(0f)] public float releaseSpeed = 7f;

            [Tooltip("Extra upward speed on letting go, in boxes per second, so a release near " +
                     "the bottom still clears something.")]
            [Min(0f)] public float releaseLift = 3f;

            [Tooltip("Seconds before another vine can be caught. Stops one press re-grabbing the " +
                     "vine you just left.")]
            [Min(0f)] public float regrabDelay = 0.35f;

            [Tooltip("Let go the moment the vine runs out under you, rather than hanging on at " +
                     "the very end.")]
            public bool letGoAtTheEnd = false;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] RigidbodyType2D restoreType;

            [NonSerialized] Vector2 anchor;
            [NonSerialized] float length;
            [NonSerialized] float limit;
            [NonSerialized] float depth;
            [NonSerialized] float angle;
            [NonSerialized] float lastLean;
            [NonSerialized] float readyAt;

            public bool IsRiding { get; private set; }

            public bool CanGrab => body != null && Time.time >= readyAt;

            public Vector2 Anchor => anchor;

            public Vector2 HangPosition => PositionAt(angle, depth);

            public float Depth => depth;

            public int SwingDirection => lastLean < 0f ? -1 : 1;

            public void Attach(Rigidbody2D wielder)
            {
                body = wielder;

                if (body != null)
                    restoreType = body.bodyType;

                IsRiding = false;
                readyAt = 0f;
            }

            public bool Grab(Vector2 anchorPoint, float vineLength, float maxSwingDegrees,
                Vector2 from)
            {
                if (body == null || IsRiding || vineLength <= minDepth)
                    return false;

                anchor = anchorPoint;
                length = vineLength;
                limit = Mathf.Min(maxSwing, Mathf.Abs(maxSwingDegrees)) * Mathf.Deg2Rad;

                Vector2 reach = from - anchor;

                depth = Mathf.Clamp(reach.magnitude, minDepth, length);
                angle = reach.sqrMagnitude < Epsilon
                    ? 0f
                    : Mathf.Clamp(Mathf.Atan2(reach.x, -reach.y), -limit, limit);

                lastLean = 0f;

                restoreType = body.bodyType;
                body.bodyType = RigidbodyType2D.Kinematic;
                body.linearVelocity = Vector2.zero;
                body.MovePosition(HangPosition);

                IsRiding = true;
                return true;
            }

            public bool Ride(Vector2 lean, float fixedDeltaTime)
            {
                if (!IsRiding || body == null)
                    return false;

                if (Mathf.Abs(lean.x) > Epsilon)
                    lastLean = lean.x;

                float sweep = depth <= Epsilon ? 0f : swingSpeed / depth;

                angle = Mathf.Clamp(angle + lean.x * sweep * fixedDeltaTime, -limit, limit);
                depth = Mathf.Clamp(depth - lean.y * climbSpeed * fixedDeltaTime, minDepth, length);

                body.MovePosition(HangPosition);

                return !letGoAtTheEnd || depth < length - Epsilon || lean.y >= 0f;
            }

            public Vector2 Release()
            {
                if (body != null)
                    body.bodyType = restoreType;

                IsRiding = false;
                readyAt = Time.time + regrabDelay;

                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SwingDirection;

                return along * releaseSpeed + Vector2.up * releaseLift;
            }

            public void Cancel()
            {
                if (body != null)
                    body.bodyType = restoreType;

                IsRiding = false;
                readyAt = 0f;
            }

            public void Validate()
            {
                swingSpeed = Mathf.Max(0f, swingSpeed);
                climbSpeed = Mathf.Max(0f, climbSpeed);
                maxSwing = Mathf.Clamp(maxSwing, 0f, 89f);
                minDepth = Mathf.Max(0.1f, minDepth);
                releaseSpeed = Mathf.Max(0f, releaseSpeed);
                releaseLift = Mathf.Max(0f, releaseLift);
                regrabDelay = Mathf.Max(0f, regrabDelay);
            }

            public void DrawGizmos()
            {
                if (!IsRiding)
                    return;

                Gizmos.color = new Color(0.42f, 0.75f, 0.38f);
                Gizmos.DrawLine(anchor, HangPosition);
                Gizmos.DrawWireSphere(HangPosition, 0.2f);
            }

            Vector2 PositionAt(float lean, float distance) =>
                anchor + new Vector2(Mathf.Sin(lean), -Mathf.Cos(lean)) * distance;
        }

        [Serializable]
        public class Spellbook
        {
            public const int SlotCount = Progress.SlotCount;

            public static readonly string[] SlotActions =
                { "Spell1", "Spell2", "Spell3", "Spell4" };

            [Header("Spells")]
            [Tooltip("The catalogue every slot draws from. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

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

                foreach (Ability spell in book.known)
                    if (spell != null)
                        Progress.Grant(spell.Key);

                Reload();
            }

            public void Reload()
            {
                if (slots.Length == 0)
                    return;

                foreach (Ability spell in book.spells)
                    if (spell != null && spell.locked && spell.fixedSlot >= 0 &&
                        Progress.Owns(spell.Key) && Progress.SlotHolding(spell.Key) < 0)
                        Progress.Equip(spell.fixedSlot, spell.Key);

                for (int i = 0; i < SlotCount; i++)
                {
                    Slot slot = slots[i];
                    Ability next = book.Find(Progress.EquippedIn(i));

                    if (next != null && !Progress.Owns(next.Key))
                        next = null;

                    if (slot.Ability == next)
                        continue;

                    if (slot.Ability != null)
                    {
                        if (slot.IsLit)
                            slot.Ability.OnEnded(owner);

                        slot.Ability.OnUnequipped(owner);
                    }

                    slot.Ability = next;
                    slot.Buffer = 0f;
                    slot.LitLeft = 0f;
                    slot.CooldownLeft = 0f;
                    slot.UsesLeft = next != null ? next.usesPerRun : 0;

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

                Progress.Equip(slot, spell != null ? spell.Key : string.Empty);
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

                slot.LitLeft = 0f;
                slot.CooldownLeft = spell.cooldown;
                spell.OnEnded(owner);
            }

            public bool Knows(Ability spell) => spell != null && Progress.Owns(spell.Key);

            public bool IsEquipped(Ability spell) =>
                spell != null && Array.Exists(slots, s => s.Ability == spell);

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                foreach (Slot slot in slots)
                {
                    if (paused || slot.Ability == null || slot.Action == null)
                    {
                        slot.Buffer = 0f;
                        continue;
                    }

                    slot.Buffer = slot.Action.WasPressedThisFrame()
                        ? slot.Ability.pressBuffer
                        : slot.Buffer - deltaTime;
                }
            }

            public void TryCast()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.Ability == null || slot.Buffer <= 0f || !slot.IsReady)
                        continue;

                    if (!slot.Ability.CanCast(owner))
                        continue;

                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.LitLeft = slot.Ability.activeDuration;

                    if (slot.Ability.usesPerRun > 0)
                        slot.UsesLeft = Mathf.Max(0, slot.UsesLeft - 1);

                    if (slot.LitLeft <= 0f)
                        slot.CooldownLeft = slot.Ability.cooldown;
                }
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Ability != null)
                        slot.Ability.ModifyStats(stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit)
                        slot.Ability.ModifyStatsWhileLit(stats);
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

                    if (slot.LitLeft > 0f)
                        continue;

                    slot.LitLeft = 0f;
                    slot.CooldownLeft = slot.Ability.cooldown;
                    slot.Ability.OnEnded(owner);
                }
            }

            public void ResetForRun()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit)
                        slot.Ability.OnEnded(owner);

                    slot.Buffer = 0f;
                    slot.LitLeft = 0f;
                    slot.CooldownLeft = 0f;
                    slot.UsesLeft = slot.Ability != null ? slot.Ability.usesPerRun : 0;
                    slot.Ability?.OnRunReset(owner);
                }
            }

            public class Slot
            {
                public Ability Ability;
                public InputAction Action;
                public float Buffer;
                public float LitLeft;
                public float CooldownLeft;
                public int UsesLeft;

                public bool IsEmpty => Ability == null;

                public bool IsLit => LitLeft > 0f;

                public bool HasUsesLeft =>
                    Ability == null || Ability.usesPerRun <= 0 || UsesLeft > 0;

                public bool IsReady => Ability != null && CooldownLeft <= 0f && HasUsesLeft;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;
            }
        }
    }
}
