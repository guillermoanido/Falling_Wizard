using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.World
{
    public class VineAnchor : MonoBehaviour
    {
        static readonly List<VineAnchor> Hanging = new List<VineAnchor>();

        [Header("Vine")]
        [Tooltip("Where the vine is tied, relative to this object. The wizard swings from here, " +
                 "so put it at the ceiling and not at the loose end.")]
        public Vector2 knotOffset = Vector2.zero;

        [Tooltip("How far the vine hangs, in boxes. A mage is one box tall.")]
        [Min(0.5f)] public float length = 5f;

        [Tooltip("How far it will lean either side of straight down, in degrees. Lower makes a " +
                 "stiff vine that barely swings.")]
        [Range(0f, 89f)] public float maxSwing = 65f;

        [Tooltip("How close the wizard has to be to catch it, in boxes, measured to the nearest " +
                 "point on the vine rather than to the knot.")]
        [Min(0.5f)] public float grabRange = 2f;

        [Header("Look")]
        [Tooltip("Optional. Stretched down the vine's length so it is visible without art being " +
                 "drawn to size. Leave empty if the object already looks like a vine.")]
        public SpriteRenderer rope;

        public static IReadOnlyList<VineAnchor> All => Hanging;

        public Vector2 Knot => (Vector2)transform.position + knotOffset;

        public Vector2 Tail => Knot + Vector2.down * length;

        void OnEnable() => Hanging.Add(this);

        void OnDisable() => Hanging.Remove(this);

        void Awake()
        {
            if (rope == null)
                rope = GetComponentInChildren<SpriteRenderer>();

            if (rope != null && rope.sprite == null)
            {
                rope.sprite = Placeholder.Box;
                rope.color = new Color(0.34f, 0.55f, 0.29f);
            }

            Dress();
        }

        void OnValidate() => Dress();

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.42f, 0.75f, 0.38f);
            Gizmos.DrawLine(Knot, Tail);

            float sweep = maxSwing * Mathf.Deg2Rad;

            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(-sweep), -Mathf.Cos(sweep)) * length);
            Gizmos.DrawLine(Knot, Knot + new Vector2(Mathf.Sin(sweep), -Mathf.Cos(sweep)) * length);

            Gizmos.color = new Color(0.42f, 0.75f, 0.38f, 0.25f);
            Gizmos.DrawWireSphere(Tail, grabRange);
        }

        public float DistanceTo(Vector2 point)
        {
            Vector2 down = Tail - Knot;
            float run = down.sqrMagnitude;

            if (run <= 0.0001f)
                return Vector2.Distance(point, Knot);

            float along = Mathf.Clamp01(Vector2.Dot(point - Knot, down) / run);

            return Vector2.Distance(point, Knot + down * along);
        }

        public bool IsWithinReach(Vector2 point) => DistanceTo(point) <= grabRange;

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

        void Dress()
        {
            if (rope == null || rope.sprite == null)
                return;

            float tall = rope.sprite.bounds.size.y;

            if (tall <= 0.0001f)
                return;

            rope.transform.position = Knot + Vector2.down * length * 0.5f;
            rope.transform.localScale = new Vector3(rope.transform.localScale.x, length / tall, 1f);
        }
    }
}
