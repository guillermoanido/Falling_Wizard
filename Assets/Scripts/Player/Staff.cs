using UnityEngine;

namespace FallingWizard.Player
{
    public enum StaffMode
    {
        Ladder,

        Bridge,
    }

    public enum StaffHold
    {
        Holding,
        BackOnLedge,
        LetGo,
    }

    [RequireComponent(typeof(BoxCollider2D))]
    public partial class Staff : MonoBehaviour
    {
        [Header("Rig")]
        [Tooltip("The pole's hitbox. Its height is the mechanic: it decides how far the wielder " +
                 "travels down or back up. Empty uses the collider on this object.")]
        public Collider2D hitbox;

        [Tooltip("The pole's sprite. Positioned by hand - the code only ever flips it.")]
        public SpriteRenderer visual;

        [Tooltip("A SOLID collider on a child, on the Ground layer, switched on only while the " +
                 "staff is a bridge. Empty means the bridge spell has nothing to stand on.")]
        public Collider2D bridgeCollider;

        [Header("Defaults For New Staves")]
        [Tooltip("Height Reset gives a fresh hitbox, in boxes.")]
        [Min(0.01f)] public float defaultLength = 1.0625f;

        [Tooltip("Width Reset gives a fresh hitbox, in boxes.")]
        [Min(0.01f)] public float defaultWidth = 0.4375f;

        [Header("Behaviour")]
        public Pole pole = new Pole();

        bool bound;

        public Pole Logic
        {
            get
            {
                Bind();
                return pole;
            }
        }

        public float Length => hitbox != null ? Pole.LocalSpan(hitbox).y : 0f;

        void Reset()
        {
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(defaultWidth, defaultLength);

            // The pole STANDS on its own origin instead of hanging from it, because that origin
            // is parked on the wizard's feet and every rank of the Staff spell grows the box
            // upwards out of it. Authored the other way up, a longer staff grows down through the
            // floor - and a fresh one has to start out the same way round as the one in the level,
            // or Reset would quietly hand the designer the bug back.
            box.offset = new Vector2(0f, defaultLength / 2f);

            hitbox = box;
            visual = GetComponentInChildren<SpriteRenderer>();
        }

        void OnValidate()
        {
            if (hitbox == null)
                hitbox = GetComponent<Collider2D>();

            pole.Validate();
        }

        void Awake()
        {
            Bind();

            if (bridgeCollider != null)
                bridgeCollider.enabled = false;
        }

        void LateUpdate() => pole.HoldPolePosition();

        void OnDrawGizmosSelected() => pole.DrawGizmos();

        void Bind()
        {
            if (bound)
                return;

            bound = true;

            if (hitbox == null)
                hitbox = GetComponent<Collider2D>();

            if (hitbox == null)
            {
                Debug.LogError($"'{name}' has no hitbox, so it has no reach and cannot be climbed. " +
                               "Add a Collider2D to it.", this);
                return;
            }

            hitbox.isTrigger = true;
            pole.BindPole(hitbox, visual, bridgeCollider);
        }
    }
}
