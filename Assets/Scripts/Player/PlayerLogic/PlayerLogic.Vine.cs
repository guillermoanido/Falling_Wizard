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
    }
}
