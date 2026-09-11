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
    public partial class PlayerLogic
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
            StaffIsFree && movement.TryFindClimb(pole.ClimbUpHeight, out _, out _);

        public bool TryClimbStaff()
        {
            if (State != PlayerState.Normal || !HasPole || pole.IsPlanted || !pole.IsReady)
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
    }
}
