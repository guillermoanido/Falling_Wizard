using System;
using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.World
{
    // Something the wizard can pick up and set down somewhere else - a slime, a rock, anything
    // the level would rather they moved than avoided.
    //
    // Carried by DEACTIVATING the object rather than destroying and re-spawning it. That keeps
    // every field the level author set, keeps its icon available to the HUD while it is stowed,
    // and means putting it down cannot lose anything. Death reloads the scene, which restores it
    // to where it was authored - a carried hazard is not something you get to keep.
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

        // Set down STANDING ON a line rather than floating on a point. A slime's hitbox is 0.7
        // of a box tall and a rock's is 0.6: dropped on the middle of a cell they hang a sixth of
        // a box off the floor, which reads as the spell having fumbled rather than having placed.
        // Measured after it is switched back on, because a disabled collider has no bounds to
        // ask.
        public void PutDownOn(float middleX, float floorY)
        {
            PutDown(new Vector2(middleX, floorY + 0.5f));

            var footprint = GetComponent<Collider2D>();

            if (footprint == null)
                return;

            // A transform move does not reach the physics shapes until the next step unless it
            // is asked to, and bounds read off a stale shape would settle the object against
            // wherever it used to be standing.
            Physics2D.SyncTransforms();

            float sunk = footprint.bounds.min.y - floorY;

            if (Mathf.Abs(sunk) > 0.001f)
                transform.position -= new Vector3(0f, sunk, 0f);
        }

        // Where it was standing when the level started, for putting a carried thing back if the
        // spell is dropped rather than spent.
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
