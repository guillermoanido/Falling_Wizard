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
    }

    [Serializable]
    public class PlayerLogic
    {
        [Header("Parts")]
        public Movement movement = new Movement();
        public Ragdoll ragdoll = new Ragdoll();
        public Health health = new Health();
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
        public PlayerState State { get; private set; }
        public bool IsOnStaff => State == PlayerState.OnStaff;
        public bool IsPeeking { get; private set; }

        public void Attach(Rigidbody2D body, SpriteRenderer sprite, Collider2D hitbox, Staff.Pole staffPole)
        {
            movement.Attach(body, sprite);
            ragdoll.Attach(body, sprite != null ? sprite.transform : null);
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

            safeFallDistance = Mathf.Max(0f, safeFallDistance);
            damagePerBox = Mathf.Max(0f, damagePerBox);
        }

        public void DrawGizmos(Vector2 origin)
        {
            movement.DrawGizmos(origin);
            pole?.DrawGizmos();
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
            if (!health.IsAlive || State == PlayerState.OnStaff)
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
            if (State != PlayerState.OnStaff)
                movement.AddImpulse(velocity, controlLockout);
        }

        public void Hurt(int hearts)
        {
            if (hearts <= 0 || !health.IsAlive)
                return;

            health.TakeDamage(hearts);

            if (!health.IsAlive)
                Die();
        }

        public void Heal(int hearts) => health.Heal(hearts);

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
                    break;

                case PlayerState.Ragdoll:
                    movement.NudgeVelocity(pendingWind * fixedDeltaTime);
                    break;

                default:
                    movement.ApplyWind(pendingWind, pendingRampup, pendingGroundScale, fixedDeltaTime);
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

            State = PlayerState.Normal;
            movement.Stop();

            spellbook.ResetForRespawn();

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
            public int ExtraJumps;

            public Modifiers() => Reset();

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
            [Header("Health")]
            [Tooltip("Hearts the wizard starts and tops out at.")]
            [Min(1)] public int maxHealth = 5;

            [Tooltip("Seconds of immunity after a hit, so one hazard cannot chain-kill.")]
            [Min(0f)] public float invulnerabilityTime = 0.6f;

            [NonSerialized] float invulnerableUntil;

            public int Max => maxHealth;
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

            public void Validate()
            {
                maxHealth = Mathf.Max(1, maxHealth);
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
        public class Spellbook
        {
            [Header("Spells")]
            [Tooltip("The order of the spell bar. Leave empty and the wizard loads " +
                     "Assets/Resources/Spellbook.asset.")]
            public AbilityBook book;

            [NonSerialized] public Modifiers stats = new Modifiers();
            [NonSerialized] Slot[] slots = Array.Empty<Slot>();
            [NonSerialized] PlayerLogic owner;

            public event Action Changed;

            public IReadOnlyList<Slot> Slots => slots;

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

                slots = new Slot[book.spells.Count];

                for (int i = 0; i < slots.Length; i++)
                {
                    Ability ability = book.spells[i];

                    slots[i] = new Slot
                    {
                        Ability = ability,
                        Action = ability == null || ability.IsPassive
                            ? null
                            : Controls.Player(ability.actionName),
                        Owned = ability != null &&
                                (book.known.Contains(ability) || Progress.Knows(ability.Key)),
                    };

                    if (slots[i].Owned)
                        ability.OnLearned(owner);
                }

                Changed?.Invoke();
            }

            public bool Learn(Ability ability)
            {
                if (ability == null)
                    return false;

                Slot slot = Array.Find(slots, s => s.Ability == ability);

                if (slot == null)
                {
                    Debug.LogWarning($"'{ability.name}' is not listed in the spellbook, so it has " +
                                     "no place on the bar and cannot be learned.", ability);
                    return false;
                }

                if (slot.Owned)
                    return false;

                slot.Owned = true;
                Progress.Learn(ability.Key);
                ability.OnLearned(owner);
                Changed?.Invoke();
                return true;
            }

            public void Extinguish(Ability ability)
            {
                Slot slot = Array.Find(slots, s => s.Ability == ability);

                if (slot == null || !slot.IsLit)
                    return;

                slot.LitLeft = 0f;
                slot.CooldownLeft = ability.cooldown;
                ability.OnEnded(owner);
            }

            public bool Knows(Ability ability)
            {
                Slot slot = Array.Find(slots, s => s.Ability == ability);
                return slot != null && slot.Owned;
            }

            public void Observe(float deltaTime)
            {
                bool paused = Game.IsPaused;

                foreach (Slot slot in slots)
                {
                    if (paused || !slot.Owned || slot.Action == null)
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
                    if (!slot.Owned || slot.Buffer <= 0f || !slot.IsReady || slot.Ability == null)
                        continue;

                    if (!slot.Ability.CanCast(owner))
                        continue;

                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.LitLeft = slot.Ability.activeDuration;

                    if (slot.LitLeft <= 0f)
                        slot.CooldownLeft = slot.Ability.cooldown;
                }
            }

            public void Rebuild()
            {
                stats.Reset();

                foreach (Slot slot in slots)
                    if (slot.Owned && slot.Ability != null)
                        slot.Ability.ModifyStats(stats);

                foreach (Slot slot in slots)
                    if (slot.IsLit && slot.Ability != null)
                        slot.Ability.ModifyStatsWhileLit(stats);
            }

            public void TickTimers(float fixedDeltaTime)
            {
                foreach (Slot slot in slots)
                {
                    if (slot.CooldownLeft > 0f)
                        slot.CooldownLeft = Mathf.Max(0f, slot.CooldownLeft - fixedDeltaTime);

                    if (!slot.IsLit || slot.Ability == null)
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

            public void ResetForRespawn()
            {
                foreach (Slot slot in slots)
                {
                    if (slot.IsLit && slot.Ability != null)
                        slot.Ability.OnEnded(owner);

                    slot.Buffer = 0f;
                    slot.LitLeft = 0f;
                    slot.CooldownLeft = 0f;
                    slot.Ability?.OnRunReset(owner);
                }
            }

            public class Slot
            {
                public Ability Ability;
                public InputAction Action;
                public bool Owned;
                public float Buffer;
                public float LitLeft;
                public float CooldownLeft;

                public bool IsLit => LitLeft > 0f;
                public bool IsReady => Owned && CooldownLeft <= 0f;

                public float CooldownProgress =>
                    Ability == null || Ability.cooldown <= 0f ? 0f : CooldownLeft / Ability.cooldown;

                public float LitProgress =>
                    Ability == null || Ability.activeDuration <= 0f ? 0f : LitLeft / Ability.activeDuration;
            }
        }
    }
}
