using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
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
    }
}
