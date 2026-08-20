using UnityEngine;

namespace FallingWizard.Player
{
    public class Staff : MonoBehaviour
    {
        [Tooltip("How far down the staff can lower whoever is holding it, in units. " +
                 "This is the whole mechanic: it decides how much of a drop the staff removes.")]
        [SerializeField] float length = 2.5f;

        [Tooltip("Optional sprite. It is resized and positioned to match the length above, " +
                 "so the staff can grow without touching the wielder's own sprite.")]
        [SerializeField] SpriteRenderer visual;

        [Tooltip("How far past the lip of the ledge the wielder shuffles while lowering.")]
        [SerializeField] float ledgeOffset = 0.4f;

        [Tooltip("Seconds to lower the full length, or to climb back up.")]
        [SerializeField] float moveDuration = 0.4f;

        [Tooltip("Which layers the staff can find footing on.")]
        [SerializeField] LayerMask groundLayers = ~0;

        Rigidbody2D wielder;
        Collider2D wielderCollider;
        RigidbodyType2D originalBodyType;
        Vector2 ledgePosition;
        Vector2 hangPosition;
        float progress;
        bool climbing;

        public float Length => length;
        public bool HasWielder => wielder != null;

        void OnValidate() => MatchVisualToLength();

        void Awake()
        {
            wielder = GetComponentInParent<Rigidbody2D>();

            if (wielder != null)
            {
                wielderCollider = wielder.GetComponent<Collider2D>();
                originalBodyType = wielder.bodyType;
            }

            MatchVisualToLength();
        }

        public void BeginDescent(int facing)
        {
            if (wielder == null)
                return;

            ledgePosition = wielder.position;
            hangPosition = ledgePosition + new Vector2(facing * ledgeOffset, -length);
            hangPosition.y = Mathf.Max(hangPosition.y, LowestClearHeight(hangPosition.x));

            progress = 0f;
            climbing = false;

            wielder.linearVelocity = Vector2.zero;
            wielder.bodyType = RigidbodyType2D.Kinematic;
        }

        public void BeginClimb()
        {
            progress = 0f;
            climbing = true;
        }

        public bool Tick(float fixedDeltaTime)
        {
            if (wielder == null)
                return true;

            progress += fixedDeltaTime / Mathf.Max(0.01f, moveDuration);
            float t = Mathf.Clamp01(progress);

            wielder.MovePosition(climbing
                ? Vector2.Lerp(hangPosition, ledgePosition, t)
                : Vector2.Lerp(ledgePosition, hangPosition, t));

            return t >= 1f;
        }

        public void Release()
        {
            if (wielder == null)
                return;

            wielder.bodyType = originalBodyType;
            wielder.linearVelocity = Vector2.zero;
        }

        // MovePosition on a kinematic body drives straight through solid ground, so work out
        // where the feet would land and never lower past it.
        float LowestClearHeight(float x)
        {
            float halfHeight = wielderCollider != null ? wielderCollider.bounds.extents.y : 0f;
            var origin = new Vector2(x, ledgePosition.y);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, length + halfHeight, groundLayers);
            return hit.collider == null ? float.NegativeInfinity : hit.point.y + halfHeight;
        }

        void MatchVisualToLength()
        {
            if (visual == null)
                return;

            if (visual.drawMode != SpriteDrawMode.Simple)
                visual.size = new Vector2(visual.size.x, length);

            visual.transform.localPosition = new Vector3(0f, -length / 2f, 0f);
        }
    }
}
