using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    /// <summary>
    /// The wizard's staff: a child of whoever carries it, and the scene's one and only, so
    /// anything that needs it can ask for <see cref="SingletonBehaviour{T}.Instance"/> rather
    /// than being wired up. All the behaviour lives in <see cref="Logic"/>; this is the Unity
    /// shell that owns the hitbox and keeps the pole where it was planted.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class Staff : SingletonBehaviour<Staff>
    {
        const float DefaultLength = 2.5f;
        const float DefaultWidth = 0.12f;

        [Tooltip("The pole's hitbox. Its height is the mechanic: it decides how far the wielder " +
                 "can travel down or back up the staff. Left empty, the collider on this object " +
                 "is used.")]
        [SerializeField] Collider2D hitbox;

        [Tooltip("Optional sprite. It is resized to match the hitbox, so the staff can grow " +
                 "without anyone touching the wielder's own sprite.")]
        [SerializeField] SpriteRenderer visual;

        [SerializeField] StaffLogic logic = new StaffLogic();

        bool bound;

        /// <summary>Everything the staff actually does.</summary>
        public StaffLogic Logic
        {
            get
            {
                Bind();
                return logic;
            }
        }

        /// <summary>The pole's height, straight off the hitbox.</summary>
        public float Length => hitbox != null ? hitbox.bounds.size.y : 0f;

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(DefaultWidth, DefaultLength);
            box.offset = new Vector2(0f, -DefaultLength / 2f);

            hitbox = box;
            visual = GetComponentInChildren<SpriteRenderer>();
            MatchVisualToHitbox();
        }

        void OnValidate()
        {
            if (hitbox == null)
                hitbox = GetComponent<Collider2D>();

            MatchVisualToHitbox();
        }

        protected override void OnAwake() => Bind();

        // The wielder moves in FixedUpdate and their transform drags every child along with
        // them, so a planted pole is put back afterwards, once per rendered frame.
        void LateUpdate() => logic.HoldPolePosition();

        void OnDrawGizmosSelected() => logic.DrawGizmos();

        // Idempotent, and reachable from the Logic getter, so it does not matter whether the
        // staff or the wielder wakes up first.
        void Bind()
        {
            if (bound)
                return;

            bound = true;

            if (hitbox == null)
                hitbox = GetComponent<Collider2D>();

            if (hitbox == null)
            {
                Debug.LogError($"'{name}' has no hitbox, so it has no reach and cannot be used " +
                               "to climb down. Add a Collider2D to it.", this);
                return;
            }

            // The pole measures a descent, it does not push anything around.
            hitbox.isTrigger = true;
            logic.BindPole(hitbox, visual);
        }

        void MatchVisualToHitbox()
        {
            if (visual == null || hitbox == null)
                return;

            Vector2 span = StaffLogic.LocalSpan(hitbox);

            if (visual.drawMode != SpriteDrawMode.Simple)
                visual.size = new Vector2(visual.size.x, span.y);

            Vector3 local = visual.transform.localPosition;
            visual.transform.localPosition = new Vector3(local.x, span.x, local.z);
        }
    }
}
