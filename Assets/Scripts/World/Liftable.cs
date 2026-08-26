using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.World
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Liftable : MonoBehaviour
    {
        static readonly List<Liftable> Loose = new List<Liftable>();

        [Header("Lifting")]
        [Tooltip("How close the wizard has to be for this to be worth reaching for, in boxes. " +
                 "The spell has its own reach as well, and the shorter of the two wins.")]
        [Min(0.5f)] public float grabRange = 6f;

        [Tooltip("Turned solid again the moment it is let go. Off leaves it a trigger, for " +
                 "something meant to be carried through walls.")]
        public bool solidWhenDropped = true;

        [NonSerialized] Rigidbody2D body;
        [NonSerialized] RigidbodyType2D restoreType;

        public bool IsHeld { get; private set; }

        public Rigidbody2D Body => body;

        public static IReadOnlyList<Liftable> All => Loose;

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            restoreType = body.bodyType;
        }

        void OnEnable() => Loose.Add(this);

        void OnDisable() => Loose.Remove(this);

        public void Grab()
        {
            if (IsHeld)
                return;

            restoreType = body.bodyType;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            IsHeld = true;
        }

        public void CarryTo(Vector2 point, float speed, float fixedDeltaTime)
        {
            if (!IsHeld)
                return;

            body.MovePosition(Vector2.MoveTowards(body.position, point, speed * fixedDeltaTime));
        }

        public void Release(Vector2 velocity)
        {
            if (!IsHeld)
                return;

            IsHeld = false;

            body.bodyType = restoreType == RigidbodyType2D.Static
                ? RigidbodyType2D.Dynamic
                : restoreType;

            if (solidWhenDropped)
                foreach (Collider2D shape in GetComponentsInChildren<Collider2D>())
                    shape.isTrigger = false;

            body.linearVelocity = velocity;
        }

        public static Liftable Nearest(Vector2 point, float reach)
        {
            Liftable closest = null;
            float best = float.MaxValue;

            foreach (Liftable stone in Loose)
            {
                if (stone.IsHeld)
                    continue;

                float gap = Vector2.Distance(point, stone.body.position);

                if (gap > Mathf.Min(reach, stone.grabRange) || gap >= best)
                    continue;

                best = gap;
                closest = stone;
            }

            return closest;
        }
    }
}
