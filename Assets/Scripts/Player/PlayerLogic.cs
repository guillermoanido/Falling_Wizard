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

            movement.Tick(frame.JumpPressed, deltaTime);
            spellbook.Observe(deltaTime);

            IsPeeking = State == PlayerState.OnStaff ||
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

        public void SetStaffLength(float scale) => pole?.SetLengthScale(scale);

        public void RecoverStaff()
        {
            pole?.Release();

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

        public class Modifiers
        {
            public float MoveSpeedMultiplier;
            public float JumpHeightMultiplier;
            public float FallSpeedMultiplier;
            public float FallDamageMultiplier;
            public float WindMultiplier;

            // Air only. MoveSpeedMultiplier cannot say that, and a canopy that made the wizard
            // sprint along the floor would be a different spell.
            public float AirSpeedMultiplier;
            public float AirControlMultiplier;

            // How fast sideways speed bleeds away in the air with nothing held. Separate from
            // AirControlMultiplier on purpose: a wing should bite HARDER when steered and coast
            // LONGER when not, and one multiplier over both does the second one backwards.
            public float AirDragMultiplier;

            public int ExtraJumps;
            public bool Shielded;

            // The stick is aiming something, not steering the wizard.
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

            // Where BOTH ground probes hang from: under the middle of the footprint, not under
            // the middle of the transform. A collider carrying an x offset of its own puts those
            // in different places, and the ledge check would then disagree with the ground check
            // about which foot is over the drop.
            Vector2 ProbeOrigin => body.position + groundCheckOffset;
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

            // Presses are buffered even with canJump off. Nothing but TryJump ever reads the
            // timer, so it costs nothing, and it means the switch can be flicked back on in the
            // middle of a playtest with no state anywhere that needs resetting first.
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

            public void SenseGround(float fixedDeltaTime) => UpdateGroundedState(fixedDeltaTime);

            // Adapted from the jump-test branch, and the adaptations matter more than the port.
            // It integrates the SAME model the real flight uses - fall gravity, the terminal
            // clamp, the floatiness from Modifiers - so the picture cannot drift from the physics.
            //
            // Three things this game needs that a plain ballistic arc gets wrong:
            //
            //  * HAZARDS DO NOT STOP YOU. Every hazard here is a trigger you pass straight
            //    through, so an arc that ended at the first slime would hide where you actually
            //    land. It flies on and reports that it crossed one.
            //  * WIND PUSHES YOU MID-FLIGHT. wind.y is added outside Run (FixedTick:719), so it
            //    reaches a wizard whose steering is locked. wind.x is not - Run early-returns on
            //    lockout - so only the vertical component belongs in here.
            //  * THE FLIGHT HAS TO BE LOCKED. Run rewrites linearVelocityX every step, dragging
            //    it to the stick at airControl x groundFriction. Unlocked, a 12 b/s fling is
            //    spent inside half a second and the arc is a lie. ArcEnd.Seconds is how long the
            //    caster must lock control for the drawing to stay true.
            //
            // Sampled by TIME, so points bunch around the apex where the wizard is slowest.
            // Whatever draws it re-spaces by DISTANCE, which is what makes it read as an even
            // dotted line rather than a comet.
            public int PredictArc(Vector2 launch, Modifiers stats, in ArcSettings look,
                List<Vector2> into, out ArcEnd end)
            {
                into.Clear();
                end = default;

                if (body == null)
                    return 0;

                arcFilter.layerMask = look.Layers;

                float baseGravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;
                float floatiness = stats != null ? stats.FallSpeedMultiplier : 1f;
                float terminal = maxFallSpeed * floatiness;
                float step = Mathf.Max(0.005f, look.Step);
                float updraught = wind.y;

                // Foot height, LIFTED CLEAR of the floor. The arc is a promise about where the
                // feet land, so it starts there - but this project has
                // Physics2D.queriesStartInColliders on, so a ray beginning flush with the ground
                // the wizard is stood on reports a hit at distance zero and the whole arc
                // collapses to a single point beside them.
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
                        ? baseGravity * fallGravityMultiplier * floatiness
                        : baseGravity;

                    velocity.y += (updraught - gravity) * step;

                    if (velocity.y < -terminal)
                        velocity.y = -terminal;

                    Vector2 next = point + velocity * step;
                    Vector2 leg = next - point;
                    float length = leg.magnitude;

                    if (length > Mathf.Epsilon)
                    {
                        int found = Physics2D.Raycast(point, leg / length, arcFilter, Rays, length);

                        // Sorted by distance, so the first SOLID one is where the flight really
                        // ends. Everything before it is scenery you pass through.
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

            // What the arc ran into, if anything.
            public struct ArcEnd
            {
                public Vector2 Point;
                public bool Stopped;

                // Something that will change where you end up was crossed on the way. The flight
                // does NOT stop there - hazards in this game are things you pass through - so
                // this is a warning about the arc, not the end of it.
                public bool Hazard;
                public Collider2D What;

                // How long the flight takes. Lock control for at least this long or the drawing
                // is a lie, because Run drags horizontal speed back to the stick every step.
                public float Seconds;
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
                float gravity = Mathf.Abs(Physics2D.gravity.y) * baseGravityScale;

                body.linearVelocityY = Mathf.Sqrt(2f * gravity * Mathf.Max(0f, heightInBoxes));

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

                edgeX = ProbeOrigin.x + Facing * air;
                return true;
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

                // The lip has to be reachable from where the soles are, or nothing is ever
                // climbed and the setting looks broken rather than switched off.
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
                    ProbeOrigin, groundCheckSize, 0f, groundFilter, Overlaps);

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
                groundFilter.layerMask = groundLayers;

                Vector2 wide = groundCheckSize + Vector2.one * NearbyGround;

                return Physics2D.OverlapBox(
                    body.position + groundCheckOffset, wide, 0f, groundFilter, Overlaps) > 0;
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
                groundFilter.layerMask = groundLayers;

                return Physics2D.Raycast(probe, Vector2.down, groundFilter, Rays, ledgeCheckDepth) > 0;
            }

            // Which way the floor underfoot is tilted. Three rays rather than one, across the
            // width of the footprint, because a single ray under the middle reads the FLAT tile
            // for the whole first half of stepping onto a ramp - and the steepest walkable
            // answer wins, so the ramp is picked up the moment a toe is over it.
            //
            // Each ray starts a little way UP inside the wizard. Physics2D.queriesStartInColliders
            // is on in this project, so a ray beginning level with the soles and already touching
            // the floor comes back at distance zero with a normal of straight up, which reads as
            // flat no matter what is really down there.
            void SenseSlope()
            {
                groundNormal = Vector2.up;
                groundAngle = 0f;

                if (!IsGrounded || body == null)
                    return;

                groundFilter.layerMask = groundLayers;

                float lift = Mathf.Max(SlopeProbeLift, groundCheckSize.y);
                float half = groundCheckSize.x * 0.5f;

                for (int i = -1; i <= 1; i++)
                {
                    var from = new Vector2(ProbeOrigin.x + i * half, ProbeOrigin.y + lift);

                    if (Physics2D.Raycast(from, Vector2.down, groundFilter, Rays,
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

            // True while the wizard is stood on something tilted enough to be worth steering
            // along rather than across.
            bool OnRamp => IsGrounded && groundAngle > flatSlopeAngle && groundAngle <= maxSlopeAngle;

            // Walking up a low lip, because a BoxCollider2D cannot do it on its own.
            //
            // The wizard is a box with square corners, frozen rotation and - deliberately - no
            // friction, so a lip is a flat vertical face meeting a flat vertical face. The
            // solver's only answer to that is to delete the sideways speed, and Run puts it
            // straight back on the next step. That is what "stuck on the scenery" feels like:
            // the stick is forward, the wizard is not moving, and nothing is actually broken.
            //
            // Kept far below a whole box on purpose. This is for tile seams and the pixel-high
            // teeth along a ramp's edge, NOT for real steps - a wizard who climbs a box without
            // jumping is a wizard for whom jumping has stopped mattering.
            void TryStepUp(Command command, Modifiers stats)
            {
                if (stepHeight <= 0f || body == null || hull == null)
                    return;

                if (lockout > 0f || stats.Rooted)
                    return;

                // The same window a jump is allowed in, so a step taken just after walking off a
                // lip is no more generous than the jump they could have had instead. Anyone
                // genuinely on their way up - a jump, a slime, a fling - is left alone.
                if (coyoteTimer <= 0f || body.linearVelocityY > 0f)
                    return;

                // The STICK, not the speed. Pressed against a lip, Run only ever lands one
                // step's worth of acceleration before the solver takes it away again, so a test
                // on how fast the wizard is really travelling would be false at exactly the
                // moment it matters.
                float steer = command.Steer;

                if (Mathf.Abs(steer) <= steerDeadzone)
                    return;

                int direction = steer < 0f ? -1 : 1;

                if (!TryFindLip(direction, out float lipTop))
                    return;

                Bounds box = hull.bounds;
                float rise = lipTop - box.min.y;

                // Under the skin there is nothing to climb and the contact rides over it on its
                // own; over stepHeight it is a wall, and walls are what the jump is for.
                if (rise <= groundCheckSkin || rise > stepHeight)
                    return;

                Vector2 offset = body.position - (Vector2)box.center;
                var landing = new Vector2(
                    box.center.x + direction * stepReach,
                    box.min.y + rise + StepClearance + box.extents.y);

                // The whole wizard has to fit where they are going. This is the headroom check
                // and the "is that lip actually the top of a wall" check in one query, and it is
                // also what makes the move below safe: the destination is proved empty before
                // the body is ever put there, so it cannot be shoved back out.
                groundFilter.layerMask = groundLayers;

                if (Physics2D.OverlapBox(landing, box.size, 0f, groundFilter, Overlaps) > 0)
                    return;

                // Written straight to Rigidbody2D.position rather than added as upward speed.
                // An impulse IS a jump - it leaves the ground, counts as airtime, fights the
                // short hop, and goes as high as the number of steps the player stayed pressed
                // against the lip. This is a move of a known, already-checked distance.
                body.position = landing + offset;
            }

            // The top of whatever is directly in front of the soles, found by casting DOWN from
            // step height onto it. Casting forward instead would answer with the face rather
            // than the surface, and the height of a lip is the only thing worth knowing here.
            bool TryFindLip(int direction, out float top)
            {
                top = 0f;

                Bounds box = hull.bounds;
                groundFilter.layerMask = groundLayers;

                var from = new Vector2(
                    box.center.x + direction * (box.extents.x + stepReach),
                    box.min.y + stepHeight + StepClearance);

                if (Physics2D.Raycast(from, Vector2.down, groundFilter, Rays,
                        stepHeight + StepClearance + groundCheckSkin) <= 0)
                    return false;

                top = Rays[0].point.y;
                return true;
            }

            void Run(Command command, Modifiers stats, float fixedDeltaTime)
            {
                if (lockout > 0f)
                    return;

                float steer = stats.Rooted ? 0f : command.Steer;
                float topSpeed = command.Walk ? walkSpeed : runSpeed;
                float targetSpeed = steer * topSpeed * stats.MoveSpeedMultiplier;

                // After the move multiply and BEFORE the wind is added, so a canopy carries the
                // wizard further under their own steam without also amplifying a gale.
                if (!IsGrounded)
                    targetSpeed *= stats.AirSpeedMultiplier;

                if (command.Walk && IsGrounded && IsAtEdge && Mathf.Abs(steer) > steerDeadzone)
                    targetSpeed = 0f;

                targetSpeed += wind.x;

                bool steering = Mathf.Abs(steer) > steerDeadzone;
                float rate = steering ? acceleration : groundFriction;

                // Grip scales BOTH branches above, because ice is two things at once and one of
                // them alone is not ice: you keep going after you let go (groundFriction) and you
                // cannot turn round in a hurry (acceleration). Scaling only the first gives a
                // wizard who slides but corners like a car; only the second gives one who feels
                // heavy but stops dead, which reads as mud.
                //
                // ON THE GROUND ONLY, and deliberately. The air rate is already airControl of
                // what it was; putting sheet ice through it as well is not "slippery" but "no
                // air control", and it would mean a trigger box taller than the patch robs the
                // steering of anyone sailing through the top of it for no visible reason.
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

            // Steering ALONG a ramp instead of across it.
            //
            // Driving purely sideways into a 45-degree face cannot work here and the numbers say
            // why: acceleration is 20 boxes a second squared, of which only cos(45) - about 14 -
            // pushes up the face, while gravity pulls 9.81 x gravityScale x sin(45) - about 21 -
            // straight back down it, and there is no friction to make up the difference. The
            // wizard loses that argument every time and slides, which is what "he glides down
            // instead of going up" is. Worse, Run rewrites the sideways speed absolutely every
            // step, so the up-the-slope velocity the contact solver correctly hands back is
            // thrown away before it can ever add up.
            //
            // So on a ramp the wizard is steered along the surface, both components at once, and
            // gravity is simply not part of the sum.
            bool TryRunAlongRamp(float targetSpeed, float topSpeed, float change)
            {
                bool wasClimbing = climbedLastStep;
                climbedLastStep = false;

                if (!OnRamp)
                {
                    // The ramp has just run out. Whatever upward speed carried the wizard up it
                    // is a hop nobody asked for now the ground is level again, so it is taken
                    // back - but only if walking could plausibly have produced it. A jump or a
                    // slime is faster than any ramp can push and is left alone.
                    if (wasClimbing && IsGrounded && !rising &&
                        body.linearVelocityY > 0f && body.linearVelocityY <= topSpeed)
                        body.linearVelocityY = 0f;

                    return false;
                }

                // Already going up faster than walking ever could, so something else - a jump, a
                // bounce, a fling - owns the wizard this step and the ramp keeps out of it.
                if (rising || body.linearVelocityY > topSpeed)
                    return false;

                // Tangent to the surface, always pointing the way x grows, so a positive target
                // speed means "that way along the floor" exactly as it does on the flat.
                var along = new Vector2(groundNormal.y, -groundNormal.x);

                if (along.x < 0f)
                    along = -along;

                // How fast they are already going along the face - CLAMPED to what walking could
                // have produced. Dropping onto a ramp otherwise arrives with the whole fall
                // pointing down the slope, and a 16 b/s landing would fire the wizard away
                // downhill faster than they can ever run back up.
                float carried = Mathf.Clamp(
                    Vector2.Dot(body.linearVelocity, along), -topSpeed, topSpeed);

                float speed = Mathf.MoveTowards(carried, targetSpeed, change);

                // Both components written together, and gravity left out of the sum entirely.
                // That is the whole trick: with nothing pulling them down the face, letting go
                // of the stick leaves the wizard stood on the ramp instead of sliding off it.
                body.linearVelocity = along * speed;
                climbedLastStep = speed * along.y > 0f;
                return true;
            }

            void TryJump(Modifiers stats)
            {
                // ABOVE the air-jump count, not folded into the test below it. Spellbook.Rebuild
                // resets and re-applies every equipped spell each fixed step, so a spell granting
                // ExtraJumps hands them out continuously - and a wizard who cannot jump off the
                // floor but can still jump in mid-air is the worst of both.
                //
                // This is the ONLY thing switched off. Launch is a separate entry point that
                // nothing here reaches, so a slime, a fling and a ramp all carry on unchanged,
                // and `rising` simply never becomes true, which leaves ApplyShortHop inert rather
                // than clipping a bounce when the button comes up.
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

                // Not while stood on something. A wizard easing down a ramp has a negative
                // vertical speed without falling at all, and putting the fall multiplier under
                // them there turns every ramp into a slide they cannot walk back up.
                bool falling = !IsGrounded && body.linearVelocityY < 0f;

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
            // A stand-up of zero seconds divides by nothing, and a tumble whose ceiling equals
            // its floor leaves no room to recover in.
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

            public void Begin(int direction) => Begin(direction, true);

            // keepMomentum off drops the sideways speed they arrived with instead of adding to
            // it. That is only ever right when the tumble goes the OTHER way to the wizard - see
            // PlayerLogic.Trip(int) - and the decision is made here rather than by the caller
            // because momentumKept lives here, and there should be exactly one place that
            // decides what happens to the speed a trip inherits.
            //
            // The pleasant side effect: with nothing carried, thrown falls to -launchForward,
            // which is under minimumLaunch, so a reversed trip always comes out at exactly
            // minimumLaunch backwards however fast they hit it. A rake throws the same distance
            // every time, which is what makes it a thing you can learn.
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
                standUpDuration = Mathf.Max(MinStandUp, standUpDuration);
                maximumDuration = Mathf.Max(maximumDuration, minimumDuration + MinTumbleSpread);

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

            // Past this the rope has swung further than a rope plausibly can, and the maths for
            // a vertical vine stops behaving.
            const float MaxLean = 89f;

            // Climbing right up to the knot would put the wizard inside whatever the vine hangs
            // from, so there is always a little rope left.
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

            // Fastest the rope is ever allowed to drag the wizard, in boxes per second. A
            // guard against a single mad frame, not a tuning knob - the swing itself is capped
            // by maxReleaseSpeed long before this.
            const float MaxHaul = 60f;

            // How far the wizard can end a step from where the swing wanted them, in boxes,
            // before it counts as having hit something.
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

            // What the SPELL supplies for one grab, as opposed to what the rope itself owns.
            // These arrive per-grab because they are rankable, and a rank is a save-tier fact
            // the wizard has no business caching.
            public struct Hold
            {
                public Vector2 Anchor;
                public float Length;
                public float MaxSwingDegrees;
                public float ClimbSpeed;      // 0 means you cannot climb - that is rank 1
                public float SnapLimit;       // how far this grab may move you, in boxes
            }

            public Vector2 HangPosition => PositionAt(angle, depth);

            public float Depth => depth;

            public float Lean => angle * Mathf.Rad2Deg;

            // Which way the wizard is actually travelling along the arc, right now. Nothing
            // remembers the last button pressed: on a rope the swing is the truth.
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

            // Where a grab from `from` would ACTUALLY put the wizard: the same two clamps Grab
            // applies, run without touching any state. This is the only honest way to ask how far
            // a grab would move them, because eligibility is measured against the ROPE while the
            // place they land is on the ARC - and the gap between those two is the teleport.
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

                // Refused BEFORE any state is written, so a grab that would yank the wizard
                // simply does not happen and the press falls through to WhyNot with a reason.
                if (Vector2.Distance(from, WouldHangAt(spec, from)) > spec.SnapLimit)
                    return false;

                anchor = spec.Anchor;
                length = spec.Length;
                limit = Mathf.Min(maxSwing, Mathf.Abs(spec.MaxSwingDegrees)) * Mathf.Deg2Rad;

                // The wizard's own climbSpeed stays the ceiling, exactly as maxSwing already is.
                // Rank 1 passes 0 and the climb term goes identically to zero - no branch.
                climb = Mathf.Min(climbSpeed, Mathf.Max(0f, spec.ClimbSpeed));

                Vector2 reach = from - anchor;

                depth = Mathf.Clamp(reach.magnitude, minDepth, length);
                angle = reach.sqrMagnitude < Epsilon
                    ? 0f
                    : Mathf.Clamp(Mathf.Atan2(reach.x, -reach.y), -limit, limit);

                // Whatever they were already doing carries into the swing, so running at a vine
                // and catching it launches you rather than stopping you dead.
                Vector2 along = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                spin = depth <= Epsilon ? 0f : Vector2.Dot(carried, along) / depth;

                // The body stays DYNAMIC and is steered by velocity. Going kinematic and
                // teleporting with MovePosition - which is what the staff does, standing still
                // against a ledge it already knows about - would swing the wizard straight
                // through the level, because a kinematic body is not stopped by static geometry.
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

                // Start from where the wizard ACTUALLY is, not from where the swing left them.
                // If a wall got in the way the arc has to admit it, or they would keep grinding
                // along the inside of it while the maths insisted they were somewhere else.
                // Compared against where the last step ASKED them to be, not against the arc
                // recomputed from their own position - the rope pulling taut on the first step
                // of a grab is not the same thing as hitting a wall, and would otherwise throw
                // away the run they arrived with.
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

                // A pendulum, in one line: gravity always pulls the wizard back under the knot,
                // and the further out they are the harder it pulls. Steering only adds to that,
                // so letting go of the stick settles them at the bottom on its own rather than
                // leaving them parked out at an angle.
                float pull = -(gravity / rope) * Mathf.Sin(angle);
                float push = lean.x * (swingPush / rope);

                spin += (pull + push) * fixedDeltaTime;
                spin *= Mathf.Clamp01(1f - damping * fixedDeltaTime);

                angle += spin * fixedDeltaTime;

                if (Mathf.Abs(angle) > limit)
                {
                    angle = Mathf.Clamp(angle, -limit, limit);

                    // Only kill the swing if it is still trying to go further out. A swing that
                    // reaches the limit and is already on its way back should keep its speed,
                    // or every big swing stalls at the top of the arc.
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

                // Leave along the arc at the speed you were actually going, which is the speed
                // the swing has been showing the player for the last second or two.
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

            // The starting kit, and the buttons that spells weld themselves to. Static because
            // the skill screen can be opened from the main menu, where no wizard exists yet to
            // have done this in Attach.
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

                    // ABOVE the early-out on purpose. Buying an upgrade does not change WHICH
                    // spell is in the slot, so a rank written below this line would not land
                    // until the wizard next died - and nothing anywhere would say why.
                    slot.Rank = next != null ? Progress.Rank(next.Key) : 0;

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
                    slot.Held = false;
                    slot.HeldFor = 0f;
                    slot.ReleasedAfter = -1f;
                    slot.LitLeft = 0f;
                    slot.CooldownLeft = 0f;
                    slot.UsesLeft = next != null ? next.usesPerLevel : 0;

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

                slot.LitLeft = 0f;
                slot.CooldownLeft = spell.cooldown;
                spell.OnEnded(owner);
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

                        // A wind-up cannot survive a pause. This loop is the only place a
                        // release is ever seen and it does not run while paused, so a button let
                        // go behind a menu is an edge nobody catches - and Fling roots the
                        // wizard while it aims, so the charge staying live meant a wizard who
                        // could never walk again for the rest of the level.
                        if (slot.HeldFor > 0f && slot.Ability != null)
                        {
                            slot.Ability.OnChargeLost(owner);
                            slot.Held = false;
                            slot.HeldFor = 0f;
                            slot.ReleasedAfter = -1f;
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
                        continue;
                    }

                    if (slot.Ability.chargesOnHold)
                        continue;           // a charged spell never expires a buffered press

                    float had = slot.Buffer;
                    slot.Buffer -= deltaTime;

                    // The press has run out of patience without ever going off. This is the
                    // moment worth reporting: earlier than this it was still legitimately
                    // waiting for a ledge to arrive.
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
                foreach (Slot slot in slots)
                {
                    if (slot.Ability == null)
                        continue;

                    if (slot.Ability.chargesOnHold)
                    {
                        Wind(slot, fixedDeltaTime);
                        continue;
                    }

                    if (slot.Buffer <= 0f || !slot.IsReady)
                        continue;

                    if (!slot.Ability.CanCast(owner))
                        continue;

                    if (!slot.Ability.OnCast(owner))
                        continue;

                    slot.Buffer = 0f;
                    slot.Fired = true;
                    slot.LitLeft = slot.Ability.activeDuration;

                    if (slot.Ability.usesPerLevel > 0)
                        slot.UsesLeft = Mathf.Max(0, slot.UsesLeft - 1);

                    if (slot.LitLeft <= 0f)
                        slot.CooldownLeft = slot.Ability.cooldown;
                }
            }

            void Wind(Slot slot, float fixedDeltaTime)
            {
                if (slot.ReleasedAfter >= 0f)
                {
                    // Consumed BEFORE the hook runs, so a spell that re-enters this path from
                    // inside OnReleased cannot fire the same release twice.
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
            }

            // For a spell that goes off from OnReleased rather than OnCast: start its lit window,
            // spend a charge and set the cooldown, exactly as a normal cast would.
            public bool Fire(Ability spell)
            {
                Slot slot = SlotOf(spell);

                if (slot == null || !slot.IsReady)
                    return false;

                slot.Fired = true;
                slot.LitLeft = spell.activeDuration;

                if (spell.usesPerLevel > 0)
                    slot.UsesLeft = Mathf.Max(0, slot.UsesLeft - 1);

                if (slot.LitLeft <= 0f)
                    slot.CooldownLeft = spell.cooldown;

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
                    slot.Held = false;
                    slot.HeldFor = 0f;
                    slot.ReleasedAfter = -1f;
                    slot.LitLeft = 0f;
                    slot.CooldownLeft = 0f;
                    slot.UsesLeft = slot.Ability != null ? slot.Ability.usesPerLevel : 0;
                    slot.Ability?.OnRunReset(owner);
                }
            }

            public class Slot
            {
                public Ability Ability;
                public InputAction Action;
                public float Buffer;
                public bool Fired;

                // Cached off Progress by Reload rather than read per frame: ModifyStats runs for
                // every slot every fixed step, and Reload is the only thing that can change it.
                public int Rank;

                public bool Held;
                public float HeldFor;

                // Seconds the button was down when it came up, latched in Observe and consumed
                // by TryCast. Below zero means nothing is pending. Polling
                // WasReleasedThisFrame from a fixed-step hook would miss the edge on a slow
                // frame and fire twice on a fast one - Observe runs in Update, TryCast does not.
                public float ReleasedAfter = -1f;
                public float LitLeft;
                public float CooldownLeft;
                public int UsesLeft;

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
