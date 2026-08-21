using System;
using UnityEngine;

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
        const float StaffPressBuffer = 0.12f;

        [SerializeField] PlayerMovement movement = new PlayerMovement();
        [SerializeField] Ragdoll ragdoll = new Ragdoll();
        [SerializeField] Health health = new Health();

        [Header("Fall Damage")]
        [Tooltip("Falls shorter than this many boxes are free.")]
        [SerializeField] float safeFallDistance = 3f;

        [Tooltip("Damage taken per box fallen beyond the safe distance. At one heart a box, a " +
                 "full health wizard dies on the eighth.")]
        [SerializeField] float damagePerUnit = 1f;

        readonly ActivePowerUps powerUps;

        StaffLogic staff;
        PlayerInput input;
        float staffPressTimer;

        public PlayerLogic() => powerUps = new ActivePowerUps(this);

        public event Action Died;

        public PlayerMovement Movement => movement;

        public Ragdoll Ragdoll => ragdoll;

        public Health Health => health;

        public PlayerStats Stats => powerUps.Stats;

        public StaffLogic Staff => staff;

        public bool HasStaff => staff != null && staff.HasPole;

        public PlayerState State { get; private set; }

        public bool IsOnStaff => State == PlayerState.OnStaff;

        public bool IsPeeking { get; private set; }

        public void Attach(Rigidbody2D body, SpriteRenderer sprite, Collider2D hitbox, StaffLogic staffLogic)
        {
            movement.Attach(body, sprite);
            ragdoll.Attach(body);
            health.RestoreToFull();

            staff = staffLogic;
            staff?.BindWielder(body, hitbox);
        }

        public void Collect(PowerUp powerUp) => powerUps.Add(powerUp);

        public void Observe(in PlayerInput frame, float deltaTime)
        {
            input = frame;

            if (!health.IsAlive)
                return;

            movement.Tick(frame.JumpPressed, deltaTime);
            powerUps.Tick(deltaTime);

            staffPressTimer = frame.StaffPressed ? StaffPressBuffer : staffPressTimer - deltaTime;

            UpdatePeeking();
        }

        public void Simulate(float fixedDeltaTime)
        {
            if (!health.IsAlive)
                return;

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
        }

        public void DrawGizmos(Vector2 origin)
        {
            movement.DrawGizmos(origin);
            staff?.DrawGizmos();
        }

        void UpdateNormal(float fixedDeltaTime)
        {
            movement.FixedTick(input.Movement, powerUps.Stats, fixedDeltaTime);

            // Movement decided which way they are looking; the staff swaps shoulders to match.
            staff?.Face(movement.Facing);

            CheckLanding();

            if (TryTakeToStaff())
                return;

            CheckForTrip();
        }

        void UpdateOnStaff(float fixedDeltaTime)
        {
            // Tapping the staff button again is always "let go from where I am".
            if (ConsumeStaffPress())
            {
                DropFromStaff();
                return;
            }

            switch (staff.Slide(input.Lean, fixedDeltaTime))
            {
                case StaffHold.BackOnLedge:
                    ClimbBackOnLedge();
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

        // The press is only spent once it actually plants the staff, so pressing a moment
        // early on the way to a ledge still works.
        bool TryTakeToStaff()
        {
            if (staffPressTimer <= 0f || !HasStaff)
                return false;

            // The pole goes in at the lip itself, not at wherever the walk happened to stop.
            if (!movement.TryFindLedgeEdge(out float edgeX))
                return false;

            staffPressTimer = 0f;

            if (!staff.Plant(movement.Facing, edgeX))
                return false;

            State = PlayerState.OnStaff;
            return true;
        }

        void ClimbBackOnLedge()
        {
            // They never left the ledge they are standing on, so this is not a fall.
            staff.Release();
            State = PlayerState.Normal;
        }

        void DropFromStaff()
        {
            float from = staff.HangPosition.y;

            staff.Release();
            movement.BeginFallFrom(from);
            State = PlayerState.Normal;
        }

        void UpdatePeeking() =>
            IsPeeking = State == PlayerState.OnStaff ||
                        (State == PlayerState.Normal && input.LookingDown);

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
            if (State == PlayerState.OnStaff)
                staff.Release();

            State = PlayerState.Normal;

            movement.Stop();
            powerUps.Clear();

            Died?.Invoke();
        }

        bool ConsumeStaffPress()
        {
            if (staffPressTimer <= 0f)
                return false;

            staffPressTimer = 0f;
            return true;
        }
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
    }
}
