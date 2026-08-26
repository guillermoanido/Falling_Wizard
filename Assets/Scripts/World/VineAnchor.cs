using System;
using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class VineAnchor : MonoBehaviour
    {
        const float Epsilon = 0.0001f;

        static readonly Color RopeColour = new Color(0.34f, 0.55f, 0.29f);
        static readonly Color LineColour = new Color(0.42f, 0.75f, 0.38f);
        static readonly Color ReachColour = new Color(0.42f, 0.75f, 0.38f, 0.25f);

        static readonly List<VineAnchor> Hanging = new List<VineAnchor>();

        [Header("Vine")]
        [Tooltip("Where the vine is tied, relative to this object. The wizard swings from here, " +
                 "so put it at the ceiling and not at the loose end.")]
        public Vector2 knotOffset = Vector2.zero;

        [Tooltip("How far the vine hangs, in boxes. A mage is one box tall.")]
        [Min(0.5f)] public float length = 5f;

        [Tooltip("How far it will lean either side of straight down, in degrees. Lower makes a " +
                 "stiff vine that barely swings.")]
        [Range(0f, 89f)] public float maxSwing = 70f;

        [Tooltip("How close the wizard has to be for the knot to light up and for the spell to " +
                 "reach it, in boxes. Measured to the nearest point ON the vine, not to the knot, " +
                 "so a long vine can be caught anywhere along its length.")]
        [Min(0.5f)] public float grabRange = 2.5f;

        [Header("The Knot")]
        [Tooltip("The bit that is always there to be seen: a knot tied to the ceiling, with no " +
                 "vine under it until the wizard calls one down. Empty and one is built.")]
        public SpriteRenderer knot;

        [Tooltip("Colour of the knot with nobody near it. Dim enough to read as scenery, bright " +
                 "enough to notice while falling past.")]
        public Color dormant = new Color(0.38f, 0.46f, 0.34f);

        [Tooltip("Colour it pulses to once the wizard is close enough to reach it. THIS is the " +
                 "tell - if a player never learns what a glowing knot means, no vine ever gets used.")]
        public Color glow = new Color(0.72f, 1f, 0.55f);

        [Tooltip("Pulses per second while lit.")]
        [Min(0f)] public float glowSpeed = 1.6f;

        [Tooltip("How big the STAND-IN knot is drawn, in boxes - the plain block built when " +
                 "there is no art here at all. A knot you supplied yourself is left exactly the " +
                 "size you made it.")]
        [Min(0.05f)] public float knotSize = 0.4f;

        [Header("The Rope")]
        [Tooltip("The vine itself. Hidden until the wizard calls it down. Empty and one is built.")]
        public SpriteRenderer rope;

        [Tooltip("How wide the STAND-IN vine is drawn, in boxes. A rope you supplied yourself " +
                 "keeps the width you gave it - only its length is ever driven, because that is " +
                 "the vine unrolling.")]
        [Min(0.02f)] public float ropeWidth = 0.2f;

        [Tooltip("Seconds the vine takes to unroll once called.")]
        [Min(0f)] public float unrollTime = 0.25f;

        [Tooltip("Once called down it stays down for the rest of the run. Off makes it roll back " +
                 "up the moment it is let go, so every swing has to be called again.")]
        public bool staysDown = true;

        [Tooltip("Sorting order for both the knot and the vine. Above the tilemap, or a vine " +
                 "hangs behind the wall it is tied to and cannot be seen at all.")]
        public int sortingOrder = 1;

        [NonSerialized] float unrolled;
        [NonSerialized] bool called;

        // Only art this component built for itself is ever resized. Anything you put here is
        // yours, and having it silently snapped back to knotSize every time OnValidate ran is
        // not a component being helpful.
        [NonSerialized] bool builtKnot;
        [NonSerialized] bool builtRope;

        [NonSerialized] float ropeThickness = 1f;

        public static IReadOnlyList<VineAnchor> All => Hanging;

        public Vector2 Knot => (Vector2)transform.position + knotOffset;

        public Vector2 Tail => Knot + Vector2.down * length;

        public bool IsDown => called;

        void OnEnable() => Hanging.Add(this);

        void OnDisable() => Hanging.Remove(this);

        void Awake()
        {
            Build();
            Dress();
        }

        void OnValidate() => Dress();

        void Update()
        {
            if (!Application.isPlaying)
                return;

            Glow();
            Unroll();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = LineColour;
            Gizmos.DrawLine(Knot, Tail);

            float sweep = maxSwing * Mathf.Deg2Rad;

            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(-sweep), -Mathf.Cos(sweep)) * length);
            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(sweep), -Mathf.Cos(sweep)) * length);

            Gizmos.color = ReachColour;
            Gizmos.DrawWireSphere(Tail, grabRange);
            Gizmos.DrawWireSphere(Knot, grabRange);
        }

        public float DistanceTo(Vector2 point)
        {
            Vector2 down = Tail - Knot;
            float run = down.sqrMagnitude;

            if (run <= Epsilon)
                return Vector2.Distance(point, Knot);

            float along = Mathf.Clamp01(Vector2.Dot(point - Knot, down) / run);

            return Vector2.Distance(point, Knot + down * along);
        }

        public bool IsWithinReach(Vector2 point) => DistanceTo(point) <= grabRange;

        // Called by the spell, not by walking past. Finding a vine is something the player does
        // on purpose with a button, so the knot is the invitation and this is accepting it.
        public void CallDown()
        {
            called = true;

            if (unrollTime <= 0f)
                unrolled = 1f;
        }

        public void RollUp()
        {
            if (staysDown)
                return;

            called = false;
            unrolled = 0f;
        }

        public static VineAnchor Nearest(Vector2 point)
        {
            VineAnchor closest = null;
            float best = float.MaxValue;

            foreach (VineAnchor vine in Hanging)
            {
                float gap = vine.DistanceTo(point);

                if (gap > vine.grabRange || gap >= best)
                    continue;

                best = gap;
                closest = vine;
            }

            return closest;
        }

        // The angle the wizard is hanging at, so the drawn vine can lean with them. Straight
        // down whenever nobody is on it.
        float Swing => BeingRidden ? PlayerCharacter.Instance.Logic.vine.Lean : 0f;

        void Glow()
        {
            if (knot == null)
                return;

            PlayerCharacter wizard = PlayerCharacter.Instance;

            bool near = wizard != null &&
                        wizard.Logic.health.IsAlive &&
                        IsWithinReach(wizard.Logic.movement.Position);

            if (!near)
            {
                knot.color = dormant;
                return;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * glowSpeed * Mathf.PI * 2f);
            knot.color = Color.Lerp(dormant, glow, pulse);
        }

        // True when the wizard is hanging on THIS vine. Asked rather than told: letting go
        // with Jump never passes through the spell, so a vine that waited to be informed would
        // stay hanging there after a jump release.
        bool BeingRidden
        {
            get
            {
                PlayerCharacter wizard = PlayerCharacter.Instance;

                return wizard != null &&
                       wizard.Logic.IsOnVine &&
                       (wizard.Logic.vine.Anchor - Knot).sqrMagnitude < Epsilon;
            }
        }

        void Unroll()
        {
            if (rope == null)
                return;

            if (called && !staysDown && !BeingRidden)
                called = false;

            float want = called ? 1f : 0f;

            unrolled = unrollTime <= 0f
                ? want
                : Mathf.MoveTowards(unrolled, want, Time.deltaTime / unrollTime);

            rope.enabled = unrolled > Epsilon;

            if (!rope.enabled)
                return;

            Stretch(unrolled, Swing);
        }

        void Build()
        {
            if (knot == null)
                knot = Make("Knot", ref builtKnot);

            if (rope == null)
                rope = Make("Rope", ref builtRope);

            if (rope != null)
            {
                rope.enabled = false;
                ropeThickness = rope.transform.localScale.x;
            }
        }

        SpriteRenderer Make(string named, ref bool built)
        {
            Transform had = transform.Find(named);

            if (had != null)
                return had.GetComponent<SpriteRenderer>();

            var go = new GameObject(named);
            go.transform.SetParent(transform, false);

            var art = go.AddComponent<SpriteRenderer>();
            art.sprite = Placeholder.Box;

            built = true;
            return art;
        }

        void Dress()
        {
            if (knot != null && knot.sprite != null)
            {
                knot.color = dormant;
                knot.sortingOrder = sortingOrder + 1;
                knot.transform.localPosition = knotOffset;

                if (builtKnot)
                    Fit(knot, knotSize);
            }

            if (rope == null || rope.sprite == null)
                return;

            rope.sortingOrder = sortingOrder;

            if (builtRope)
            {
                rope.color = RopeColour;
                Fit(rope, ropeWidth);
                ropeThickness = rope.transform.localScale.x;
            }

            Stretch(Application.isPlaying ? unrolled : 1f, 0f);
        }

        static void Fit(SpriteRenderer art, float across)
        {
            Vector2 unit = art.sprite.bounds.size;

            if (unit.x > Epsilon && unit.y > Epsilon)
                art.transform.localScale = new Vector3(across / unit.x, across / unit.y, 1f);
        }

        void Stretch(float howFar, float lean)
        {
            Vector2 unit = rope.sprite.bounds.size;

            if (unit.x <= Epsilon || unit.y <= Epsilon)
                return;

            float hangs = length * howFar;
            float tilt = lean * Mathf.Deg2Rad;

            // The way the rope actually points, which is the way the wizard hangs - so the drawn
            // vine leans with the swing instead of standing bolt upright while the wizard arcs
            // away from underneath it. Grown downward from the knot rather than around its own
            // middle, so a half-unrolled vine reaches half way down.
            var along = new Vector2(Mathf.Sin(tilt), -Mathf.Cos(tilt));

            rope.transform.position = Knot + along * (hangs * 0.5f);
            rope.transform.rotation = Quaternion.Euler(0f, 0f, lean);

            // Only the length is driven. The width stays whatever it was authored at, so making
            // a vine fatter to see it stays made.
            rope.transform.localScale =
                new Vector3(ropeThickness, Mathf.Max(Epsilon, hangs) / unit.y, 1f);
        }
    }
}
