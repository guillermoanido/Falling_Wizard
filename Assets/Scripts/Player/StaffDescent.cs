using System;
using UnityEngine;

namespace FallingWizard.Player
{
    [Serializable]
    public class StaffDescent
    {
        [Tooltip("How far down the wizard can lower themselves on the staff, in units. " +
                 "This is what turns a killing drop into a survivable one.")]
        [SerializeField] float staffLength = 2.5f;

        [Tooltip("How far past the lip of the ledge they shuffle while lowering.")]
        [SerializeField] float ledgeOffset = 0.4f;

        [Tooltip("Seconds to lower the full length, or to climb back up.")]
        [SerializeField] float moveDuration = 0.4f;

        [Tooltip("Seconds of holding Down at an edge before committing to the descent. " +
                 "A shorter hold just looks over the edge.")]
        [SerializeField] float holdToDescend = 0.35f;

        Rigidbody2D body;
        Collider2D ownCollider;
        LayerMask groundLayers;
        RigidbodyType2D originalBodyType;
        Vector2 ledgePosition;
        Vector2 hangPosition;
        float progress;
        bool climbing;

        public float StaffLength => staffLength;
        public float HoldToDescend => holdToDescend;

        public void Attach(Rigidbody2D rigidbody2d, Collider2D collider2d, LayerMask ground)
        {
            body = rigidbody2d;
            ownCollider = collider2d;
            groundLayers = ground;
            originalBodyType = body.bodyType;
        }

        public void BeginDescent(int facing)
        {
            ledgePosition = body.position;
            hangPosition = ledgePosition + new Vector2(facing * ledgeOffset, -staffLength);
            hangPosition.y = Mathf.Max(hangPosition.y, LowestClearHeight(hangPosition.x));

            progress = 0f;
            climbing = false;

            body.linearVelocity = Vector2.zero;
            body.bodyType = RigidbodyType2D.Kinematic;
        }

        // MovePosition on a kinematic body drives straight through solid ground, so work out
        // where the feet would land and never lower past it.
        float LowestClearHeight(float x)
        {
            float halfHeight = ownCollider != null ? ownCollider.bounds.extents.y : 0f;
            var origin = new Vector2(x, ledgePosition.y);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down,
                staffLength + halfHeight, groundLayers);

            return hit.collider == null ? float.NegativeInfinity : hit.point.y + halfHeight;
        }

        public void BeginClimb()
        {
            progress = 0f;
            climbing = true;
        }

        public bool Tick(float fixedDeltaTime)
        {
            progress += fixedDeltaTime / Mathf.Max(0.01f, moveDuration);
            float t = Mathf.Clamp01(progress);

            body.MovePosition(climbing
                ? Vector2.Lerp(hangPosition, ledgePosition, t)
                : Vector2.Lerp(ledgePosition, hangPosition, t));

            return t >= 1f;
        }

        public void Release()
        {
            body.bodyType = originalBodyType;
            body.linearVelocity = Vector2.zero;
        }
    }
}
