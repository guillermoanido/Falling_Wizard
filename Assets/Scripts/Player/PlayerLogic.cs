using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.World;
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

    // Everything the wizard is, with no Unity component around it. Movement, tumbling, health,
    // stat modifiers and the spellbook are all nested here rather than scattered across their own
    // files: they are parts of a wizard, they are meaningless on their own, and a hazard or a
    // spell should only ever have to know about PlayerLogic.
    //
    // The outside world talks to the wizard through the verbs in the middle of this file - Trip,
    // Bounce, Push, Shove, Hurt - and never by reaching in and setting a tuning number.
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
        public Modifiers Stats => spellbook.stats;
        public PlayerState State { get; private set; }
        public bool IsOnStaff => State == PlayerState.OnStaff;
        public bool IsPeeking { get; private set; }

        public void Attach(Rigidbody2D body, SpriteRenderer sprite, Collider2D hitbox, Staff.Pole staffPole)
        {
            movement.Attach(body, sprite);
            ragdoll.Attach(body);
            health.RestoreToFull();

            pole = staffPole;
            pole?.BindWielder(body, hitbox);

            spellbook.Attach(this);
        }

        // One rendered frame of input. Presses are latched because a frame can hold any number of
        // physics steps, including none at all.
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

        // One physics step. The order matters: a spell cast this step must be reflected in the
        // stats this step, and external force must land before movement gets to write velocity.
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

        // ---------------------------------------------------------------- the world's verbs ----
        // Hazards and spells act through these. Each one decides for itself whether it applies,
        // so nothing outside has to know what state the wizard is in.

        public bool Trip()
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            ragdoll.Begin(movement.Facing);
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

        // Wind, gathered from every zone the wizard is inside and spent on the next physics step.
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

        // ------------------------------------------------------------------------- the staff ----

        public bool TryPlantStaff(StaffMode mode)
        {
            if (State != PlayerState.Normal || !HasPole)
                return false;

            if (!movement.TryFindLedgeEdge(out float edgeX))
                return false;

            if (!pole.Plant(mode, movement.Facing, edgeX))
                return false;

            // A ladder takes the body over. A bridge is just scenery you can walk on.
            if (mode == StaffMode.Ladder)
                State = PlayerState.OnStaff;

            return true;
        }

        // Climbed back to the top, or picked a bridge back up. Not a fall either way.
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

        // ------------------------------------------------------------------------- internals ----

        void UpdateNormal(float fixedDeltaTime)
        {
            movement.FixedTick(input.Movement, Stats, fixedDeltaTime);

            // Movement decided which way they are looking; the staff swaps shoulders to match.
            pole?.Face(movement.Facing);

            CheckLanding();
            CheckForTrip();
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
            // No control at all here: physics owns the body until they get back up.
            movement.SenseGround(fixedDeltaTime);
            CheckLanding();

            if (ragdoll.Tick(fixedDeltaTime, movement.IsGrounded, movement.HorizontalSpeed))
                State = PlayerState.Normal;
        }

        // Wind reaches the wizard differently depending on what is holding them up.
        void ApplyExternalForce(float fixedDeltaTime)
        {
            switch (State)
            {
                case PlayerState.OnStaff:
                    // The pole is driven into the ground and they are holding on to it. Wind does
                    // not get a say, which is exactly what makes the staff a shelter.
                    break;

                case PlayerState.Ragdoll:
                    // FixedTick never runs while tumbling, so there is nothing to fold into.
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

        void CheckForTrip()
        {
            RoughGround ground = movement.Ground;

            if (ground == null || !ground.trips || !movement.IsGrounded)
                return;

            if (movement.HorizontalSpeed > ground.tripSpeed)
                Trip();
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

            // A tumble unfreezes rotation and leaves the body spinning. Dying mid trip must not
            // leave it that way, or the corpse keeps rolling until the level reloads.
            if (State == PlayerState.Ragdoll)
                ragdoll.Cancel();

            State = PlayerState.Normal;
            movement.Stop();

            // Timers and lit spells go. What the wizard KNOWS lives outside them, so it survives.
            spellbook.ResetForRespawn();

            Died?.Invoke();
        }

        // ============================================================================ Intent ====

        // One frame of what the player is asking for. Ability buttons are deliberately absent:
        // the spellbook reads its own actions, so adding a spell never touches this struct.
        public struct Intent
        {
            public Vector2 Move;
            public bool JumpPressed;
            public bool JumpHeld;
            public bool Walk;
            public bool LookingDown;
            public bool LookingUp;

            public float Steer => Move.x;
            public float Lean => Move.y;

            public Command Movement => new Command
            {
                Steer = Move.x,
                JumpHeld = JumpHeld,
                Walk = Walk,
            };
        }

        // The slice of Intent that locomotion cares about.
        public struct Command
        {
            public float Steer;
            public bool JumpHeld;
            public bool Walk;
        }

        // ========================================================================= Modifiers ====

        // What spells do to the wizard. Rebuilt from scratch every physics step, so there is no
        // such thing as a stale modifier.
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

        // ============================================================================ Health ====

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

        // ========================================================================== Movement ====

        // Locomotion, ground sensing, and the one place external force is allowed in. Run writes
        // linearVelocityX absolutely every step, so anything a hazard wants to contribute has to
        // go through the wind or impulse channel below, or it is erased within a quarter second.
        [Serializable]
        public class Movement
        {
            const float MinGravityScale = 0.01f;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>(8);
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>(4);

            // Tuned in boxes: one box is 32 px, one world unit, and about one mage.
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

            [NonSerialized] Vector2 wind;
            [NonSerialized] Vector2 impulse;
            [NonSerialized] float lockout;

            public bool IsGrounded { get; private set; }
            public bool IsAtEdge { get; private set; }

            // What they are standing on, when it is worth knowing about.
            public RoughGround Ground { get; private set; }
            public bool GroundIsRough => Ground != null;

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

                // useTriggers false is load bearing: this project queries triggers by default, so
                // without it any trigger sitting on the Ground layer becomes walkable floor.
                groundFilter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = groundLayers,
                    useTriggers = false,
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
                if (impulse != Vector2.zero)
                {
                    body.linearVelocity += impulse;
                    impulse = Vector2.zero;
                    rising = false;             // a shove is not a jump, so no short-hop clipping
                }

                lockout -= fixedDeltaTime;

                UpdateFacing(command.Steer);
                UpdateGroundedState(fixedDeltaTime);
                Run(command, stats, fixedDeltaTime);
                TryJump(stats);
                ApplyShortHop(command.JumpHeld);

                // Lift goes in before the terminal clamp, so an updraught can actually beat gravity.
                if (wind.y != 0f)
                    body.linearVelocityY += wind.y * fixedDeltaTime;

                ApplyFallGravity(stats);

                // Zones re-assert themselves every step, so this only bites once you leave one.
                wind = Vector2.MoveTowards(wind, Vector2.zero, windDecay * fixedDeltaTime);
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
                impulse = Vector2.zero;
            }

            // Ground sensing without any of the control, for states that let physics take over.
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
                impulse += velocity;
                lockout = Mathf.Max(lockout, controlLockout);
            }

            public void NudgeVelocity(Vector2 velocity)
            {
                if (body != null)
                    body.linearVelocity += velocity;
            }

            // A slime bounce. Height is in boxes and the launch speed comes from gravity, exactly
            // like a jump, so "3 boxes" really is three boxes.
            public void Launch(float heightInBoxes, float sideways, bool resetsFall)
            {
                float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;

                body.linearVelocityY = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, heightInBoxes));

                if (sideways != 0f)
                    body.linearVelocityX += sideways;

                rising = false;                 // or releasing jump would halve the bounce

                if (!resetsFall)
                    return;

                // Without this the wizard is billed for the whole flight when they finally land.
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

                // The feet are wide enough to stay grounded with the middle already out over the
                // drop, so when there is nothing underneath, back up until they find rock again.
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
                walkSpeed = Mathf.Clamp(walkSpeed, 0f, runSpeed);   // walking cannot outrun running
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

                // Every overlap, not just the first: a rock sitting on a wide platform used to be
                // a coin flip as to whether the ground underfoot read as rough.
                int count = Physics2D.OverlapBox(
                    body.position + groundCheckOffset, groundCheckSize, 0f, groundFilter, Overlaps);

                IsGrounded = count > 0;
                Ground = null;

                for (int i = 0; i < count && Ground == null; i++)
                    Ground = Overlaps[i].GetComponentInParent<RoughGround>();

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

            bool HasGroundAt(float ahead)
            {
                Vector2 probe = body.position + new Vector2(Facing * ahead, groundCheckOffset.y);
                groundFilter.layerMask = groundLayers;

                return Physics2D.Raycast(probe, Vector2.down, groundFilter, Rays, ledgeCheckDepth) > 0;
            }

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;                     // just been shoved: no steering out of it yet

                float steer = command.Steer;
                float topSpeed = command.Walk ? walkSpeed : runSpeed;
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                // Walking is careful: it refuses to carry the wizard over the lip of a drop.
                if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > steerDeadzone)
                    targetSpeed = 0f;

                // Wind belongs in the target rather than added afterwards: you can lean into it
                // and partly win, it self-limits instead of accelerating forever, and it is the
                // only form Run cannot immediately erase.
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

        // =========================================================================== Ragdoll ====

        [Serializable]
        public class Ragdoll
        {
            [Header("Tumble")]
            [Tooltip("Spin given to the wizard when they trip, in degrees per second.")]
            public float spinSpeed = 520f;

            [Tooltip("Downward kick when tripping, so they pitch forward and drop fast.")]
            public float fallKick = 4f;

            [Tooltip("How quickly the tumble spins down. 0 spins forever.")]
            [Min(0f)] public float angularDamping = 1.5f;

            [Header("Getting Up")]
            [Tooltip("Minimum seconds spent tumbling before they can start getting up.")]
            [Min(0f)] public float minimumDuration = 0.9f;

            [Tooltip("They only get up once grounded and slower than this, in boxes per second.")]
            [Min(0f)] public float recoverSpeed = 1.2f;

            [Tooltip("Seconds spent standing back upright.")]
            [Min(0.01f)] public float standUpDuration = 0.35f;

            [NonSerialized] Rigidbody2D body;
            [NonSerialized] float originalAngularDamping;
            [NonSerialized] float tumbleTimer;
            [NonSerialized] float standUpTimer;
            [NonSerialized] float standUpFrom;

            public bool IsStandingUp => standUpTimer >= 0f;

            public void Attach(Rigidbody2D rigidbody2d)
            {
                body = rigidbody2d;
                originalAngularDamping = body.angularDamping;
                standUpTimer = -1f;
            }

            public void Begin(int facing)
            {
                body.freezeRotation = false;
                body.angularDamping = angularDamping;
                body.angularVelocity = -facing * spinSpeed;
                body.linearVelocityY = -fallKick;

                tumbleTimer = minimumDuration;
                standUpTimer = -1f;
            }

            public bool Tick(float fixedDeltaTime, bool grounded, float horizontalSpeed)
            {
                if (standUpTimer >= 0f)
                    return StandUp(fixedDeltaTime);

                tumbleTimer -= fixedDeltaTime;

                if (tumbleTimer > 0f || !grounded || horizontalSpeed > recoverSpeed)
                    return false;

                body.angularVelocity = 0f;
                standUpFrom = body.rotation;
                standUpTimer = 0f;
                return false;
            }

            public void Cancel()
            {
                body.angularVelocity = 0f;
                body.rotation = 0f;
                body.freezeRotation = true;
                body.angularDamping = originalAngularDamping;
                standUpTimer = -1f;
            }

            public void Validate()
            {
                angularDamping = Mathf.Max(0f, angularDamping);
                standUpDuration = Mathf.Max(0.01f, standUpDuration);
            }

            bool StandUp(float fixedDeltaTime)
            {
                standUpTimer += fixedDeltaTime;
                float t = Mathf.Clamp01(standUpTimer / Mathf.Max(0.01f, standUpDuration));
                body.MoveRotation(Mathf.LerpAngle(standUpFrom, 0f, t));

                if (t < 1f)
                    return false;

                Cancel();
                return true;
            }
        }

        // ========================================================================= Spellbook ====

        // What the wizard knows and what it is currently doing. Ability assets are stateless
        // flyweights shared by everyone; every mutable value lives in a Slot here.
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

            // End a lit spell early - a glide that lands, for instance.
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
                foreach (Slot slot in slots)
                {
                    if (!slot.Owned || slot.Action == null)
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

                    // A refused cast keeps its buffer, so pressing the staff button a moment
                    // before reaching the ledge still works.
                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.LitLeft = slot.Ability.activeDuration;

                    // A spell that lingers starts cooling down when it ENDS. One that acts
                    // instantly has nothing to wait for, so it starts now.
                    if (slot.LitLeft <= 0f)
                        slot.CooldownLeft = slot.Ability.cooldown;
                }
            }

            // From scratch, every physics step. No dirty flags means no stale modifiers.
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

                    // OnLit is allowed to end the spell itself - a glide that touches down does
                    // exactly that - and Extinguish has already fired OnEnded. Without this
                    // check the countdown below would fire it a second time.
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

            // Death clears what is running. It never clears what is KNOWN - that is the whole
            // point of a permanent spell.
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
