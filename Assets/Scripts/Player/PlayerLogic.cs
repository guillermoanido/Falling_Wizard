using System;
using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

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

        // Sits at 1 - ordinary ground - unless something outside pushed a lower number in during
        // the last physics step. ApplyExternalForce hands it to Movement and puts it straight
        // back, so a floor stops being slippery by simply not calling any more.
        [NonSerialized] float pendingGrip = 1f;

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
            movement.Attach(body, sprite, hitbox);
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

            movement.BufferJump(frame.JumpPressed, deltaTime);
            spellbook.Observe(deltaTime);

            // Peeking drops the camera four boxes to show you where you are GOING, which is
            // right for hanging off a ledge and exactly wrong for climbing a wall - it slides the
            // one thing you need to see, the top, off the screen for the whole ascent.
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

        // The same trip, aimed. A hazard that wants to put the wizard down somewhere other than
        // straight ahead - a rake whose handle comes up in their face - passes the direction it
        // wants rather than reaching into the ragdoll itself, so the State and IsAlive guards
        // stay in exactly one place and cannot be forgotten by the next hazard somebody writes.
        public bool Trip(int direction)
        {
            if (State != PlayerState.Normal || !health.IsAlive)
                return false;

            // 0 is not a direction. Ragdoll.Begin multiplies BOTH the spin and the minimum
            // launch by this, so a caller that worked its direction out from something which can
            // come back zero would get a wizard lying on the floor, spinning at nothing, going
            // nowhere - and nothing on screen to say why.
            int way = direction < 0 ? -1 : 1;

            // Carry the speed they arrived with into the tumble only when the tumble goes the
            // way they were already going. Thrown BACK, that carry is a subtraction: Begin adds
            // launchForward to whatever they had, so a wizard running in at 4 boxes a second and
            // thrown "backwards" comes out still going forwards, spinning the wrong way, which
            // reads as a broken hazard rather than a rake.
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

        // Ground that does not hold you - water, wet flagstones, ice. 1 is a normal floor and 0
        // is glass. It scales both halves of Movement.Run's rate at once: how hard the wizard can
        // push off, and how hard they can dig in to stop.
        //
        // Pushed in from outside exactly the way Push does with wind, and for exactly the same
        // reason. Spellbook.Rebuild calls stats.Reset() every fixed step and re-applies the
        // equipped abilities on top, so a hazard that wrote a multiplier into Modifiers would
        // have it thrown away before Run ever looked at it. This is per-step state instead: the
        // patch calls every step the wizard is stood in it, ApplyExternalForce spends it and
        // clears it, and stepping off the ice needs no exit event and no decay timer.
        //
        // The LOWEST grip wins rather than the sum, so a wizard straddling a puddle and a sheet
        // of ice is on ice. Adding them the way wind adds would make two overlapping patches
        // more slippery than either one, which is the sort of thing a designer only finds out by
        // dragging a prefab a tile too far.
        public void Slicken(float grip) => pendingGrip = Mathf.Min(pendingGrip, Mathf.Clamp01(grip));

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

        public int PredictArc(Vector2 launch, in Movement.ArcSettings look, List<Vector2> into,
            out Movement.ArcEnd end)
        {
            // Only ever while they are stood there winding one up. Tumbling or hanging off the
            // staff, there is no shot to draw.
            if (State != PlayerState.Normal || !health.IsAlive)
            {
                into.Clear();
                end = default;
                return 0;
            }

            return movement.PredictArc(launch, Stats, look, into, out end);
        }

        // One shove, aimed. Unlike Shove this clears whatever the wizard was already doing, so
        // the launch is exactly the one the dotted line was drawing and nothing is added to it.
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

        // Is there a wall in front of them the staff could get them up? Asked by the spell to
        // decide whether the button does anything, and asked again by the cast itself - it is
        // three casts and an overlap, cheap enough to run twice and far cheaper than caching a
        // lip that the wizard has since walked away from.
        public bool CanClimbHere =>
            StaffIsFree && !movement.IsAtEdge &&
            movement.TryFindClimb(pole.RawReach, out _, out _);

        // The same ladder, raised from the bottom. TryPlantStaff hangs the pole off a ledge the
        // wizard is stood ON; this raises it against a wall they are stood UNDER.
        public bool TryClimbStaff()
        {
            if (State != PlayerState.Normal || !HasPole || pole.IsPlanted)
                return false;

            // A drop in front of them is a descent, whatever is on the far side of it. Checked
            // here rather than left to the two probes to disagree about: a ledge and a wall can
            // both answer within a quarter box of each other at the lip of a step, and which one
            // won would otherwise come down to which cast happened to run first.
            if (movement.IsAtEdge)
                return false;

            if (!movement.TryFindClimb(pole.RawReach, out Vector2 lip, out Vector2 landing))
                return false;

            if (!pole.PlantAsClimb(movement.Facing, lip, landing))
                return false;

            State = PlayerState.OnStaff;
            return true;
        }

        public void SetStaffLength(float scale) => pole?.SetLengthScale(scale);

        public void RecoverStaff() => RecoverStaff(false);

        // arrived says the wizard got here by climbing to the very top and pushing up - the one
        // case where a climb is allowed to set them down over the lip. Everything else that
        // releases the pole while the depth happens to be near zero - dying, pressing the staff
        // button again - must NOT be given the top of the wall for free.
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

        // So a spell can say WHY a grab was refused without duplicating the rope's own clamps.
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
            // Before the switch and OUTSIDE it, so the grip is refreshed in EVERY state,
            // including the two the switch does nothing for. A hazard cannot reach the wizard on
            // their staff, so nothing calls Slicken up there and this puts the grip back to 1 -
            // which is the point. Set it inside the default case instead and Movement would keep
            // whatever the last patch wrote, so a wizard who climbed off an ice sheet and rode
            // their staff across the room would come down onto ordinary stone that was still
            // slippery.
            //
            // Simulate reaches this before the state switch, which is before UpdateNormal,
            // FixedTick and Run. Unity runs every FixedUpdate, THEN the physics step, THEN the
            // trigger callbacks - so the grip a patch pushed in during the previous physics step
            // is on Movement before this step's Run reads it. One fixed step of lag, the same lag
            // wind already has.
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

            // Cleared HERE and nowhere else - after the consume, in the same call. Clearing it at
            // the top of Simulate, or from Update, wipes the value between the trigger callback
            // that set it and the Run that wants it, and ice then does nothing at all.
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
    }
}
