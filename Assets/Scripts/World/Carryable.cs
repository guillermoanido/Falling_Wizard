using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.World
{
    public class Carryable : MonoBehaviour
    {
        static readonly List<Carryable> Loose = new List<Carryable>();

        [Header("Carrying")]
        [Tooltip("How close the wizard has to be to take this, in boxes. The spell has its own " +
                 "reach as well and the shorter of the two wins.")]
        [Min(0.5f)] public float takeRange = 6f;

        [Tooltip("Shown in the spell's slot while this is stowed, so the player can tell a slime " +
                 "from a rock at a glance. Empty uses whatever sprite this object is drawing.")]
        public Sprite icon;

        [Tooltip("Tint for that icon. Give a slime and a rock different colours and the slot " +
                 "reads without any art at all.")]
        public Color tint = Color.white;

        [Tooltip("Seconds it stays inert after being set down. Without this a slime put down " +
                 "beside you bounces you on the next physics step - Awake does not run again " +
                 "when an object is switched back on, so its own re-arm timer is stale.")]
        [Min(0f)] public float settleTime = 0.4f;

        [NonSerialized] Vector2 home;
        [NonSerialized] bool knowsHome;

        [NonSerialized] float underside;

        public static IReadOnlyList<Carryable> All => Loose;

        public bool IsStowed => !gameObject.activeSelf;

        public Sprite Icon
        {
            get
            {
                if (icon != null)
                    return icon;

                var art = GetComponentInChildren<SpriteRenderer>(true);
                return art != null ? art.sprite : null;
            }
        }

        void Awake()
        {
            home = transform.position;
            knowsHome = true;
        }

        void OnEnable() => Loose.Add(this);

        void OnDisable() => Loose.Remove(this);

        public void Stow()
        {
            if (!knowsHome)
            {
                home = transform.position;
                knowsHome = true;
            }

            var footprint = GetComponent<Collider2D>();

            if (footprint != null)
                underside = transform.position.y - footprint.bounds.min.y;

            gameObject.SetActive(false);
        }

        public void PutDown(Vector2 where)
        {
            transform.position = where;
            gameObject.SetActive(true);

            var body = GetComponent<Rigidbody2D>();

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }

            foreach (Hazard hazard in GetComponentsInChildren<Hazard>(true))
                hazard.Disarm(settleTime);
        }

        public void PutDownOn(float middleX, float floorY) =>
            PutDown(new Vector2(middleX, floorY + underside));

        public void GoHome() => PutDown(home);

        public static Carryable Nearest(Vector2 point, float reach)
        {
            Carryable closest = null;
            float best = float.MaxValue;

            foreach (Carryable thing in Loose)
            {
                float gap = Vector2.Distance(point, thing.transform.position);

                if (gap > Mathf.Min(reach, thing.takeRange) || gap >= best)
                    continue;

                best = gap;
                closest = thing;
            }

            return closest;
        }
    }
}
