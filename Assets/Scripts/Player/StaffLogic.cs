using System;
using UnityEngine;

namespace FallingWizard.Player
{
    public enum StaffHold
    {
        Holding,
        BackOnLedge,
        LetGo,
    }

    [Serializable]
    public class StaffLogic
    {
        public const float Epsilon = 0.01f;

        const float InputThreshold = 0.5f;
        const float MinScale = 0.0001f;

        [Tooltip("How fast the wielder slides along the pole, in boxes per second.")]
        [SerializeField] float slideSpeed = 3f;

        [Tooltip("Depth over which the wielder swings from the ledge onto the pole, so joining " +
                 "it is not a snap. How far out they end up is where the pole was driven in.")]
        [SerializeField] float swingDepth = 0.5f;

        [Tooltip("How far past the lip of the ledge the pole is driven in, so it hangs clear of " +
                 "the ledge face instead of scraping down it.")]
        [SerializeField] float lipClearance = 0.15f;

        [Tooltip("How far above their middle the wielder grips. They can lower themselves until " +
                 "that grip reaches the very end of the pole, so the last stretch is a hand hang " +
                 "with the body dangling past the tip. Higher grip, lower hang.")]
        [SerializeField] float gripHeight = 0.25f;

        [Tooltip("Seconds of held down input at the very bottom of the pole before letting go. " +
                 "Short enough to feel instant, long enough that sliding down is not a drop.")]
        [SerializeField] float dropHoldTime = 0.2f;

        [Tooltip("Which layers the staff can find footing on, so it never lowers into solid ground.")]
        [SerializeField] LayerMask groundLayers = ~0;

        Collider2D hitbox;
        Transform pole;
        SpriteRenderer visual;
        Vector3 restPosition;
        float sideOffset;

        Rigidbody2D wielder;
        Collider2D wielderHitbox;
        RigidbodyType2D wielderBodyType;

        Vector2 anchor;
        Vector3 plantedPosition;
        float reach;
        float depth;
        float dropTimer;
        int facing = 1;

        public bool IsPlanted { get; private set; }

        public bool HasPole => hitbox != null;

        public bool HasWielder => wielder != null;

        public float Reach => reach;

        public float Depth => depth;

        public float Progress => reach <= Epsilon ? 1f : Mathf.Clamp01(depth / reach);

        public bool AtTop => depth <= Epsilon;

        public bool AtBottom => depth >= reach - Epsilon;

        public Vector2 HangPosition => PositionAt(depth);

        public Vector2 Anchor => anchor;

        float WielderFeetOffset =>
            wielder != null && wielderHitbox != null
                ? wielder.position.y - wielderHitbox.bounds.min.y
                : 0f;

        float HangBelowTip => WielderFeetOffset + gripHeight;

        public void BindPole(Collider2D poleHitbox, SpriteRenderer poleVisual)
        {
            hitbox = poleHitbox;
            visual = poleVisual;
            pole = poleHitbox != null ? poleHitbox.transform : null;

            if (pole == null)
                return;

            // Authored on one side; which side it is actually carried on is up to the wielder.
            restPosition = pole.localPosition;
            sideOffset = Mathf.Abs(restPosition.x);
        }

        public void Face(int wielderFacing)
        {
            if (IsPlanted || wielderFacing == 0)
                return;

            facing = wielderFacing < 0 ? -1 : 1;
            ShoulderPole();
        }

        public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
        {
            wielder = body;
            wielderHitbox = bodyHitbox;

            if (body != null)
                wielderBodyType = body.bodyType;
        }

        public float MeasureReach() => hitbox != null ? hitbox.bounds.size.y : 0f;

        public bool Plant(int wielderFacing, float edgeX)
        {
            if (!HasPole || !HasWielder)
                return false;

            // Side first, so the pole is already on the correct shoulder when it swings out.
            Face(wielderFacing);

            anchor = wielder.position;
            depth = 0f;
            dropTimer = 0f;

            // The pole stops being something the wielder carries and becomes part of the world:
            // hung just past the lip with its top flush to the ledge, so its far end is exactly
            // where the feet will end up and the drop can be read straight off it.
            float surfaceY = anchor.y - WielderFeetOffset;
            float topAboveOrigin = hitbox.bounds.max.y - pole.position.y;

            plantedPosition = new Vector3(
                edgeX + facing * lipClearance,
                surfaceY - topAboveOrigin,
                pole.position.z);

            pole.position = plantedPosition;

            reach = ClearReach(MeasureReach() + HangBelowTip);
            if (reach <= Epsilon)
            {
                ShoulderPole();
                return false;
            }

            wielderBodyType = wielder.bodyType;
            wielder.linearVelocity = Vector2.zero;
            wielder.bodyType = RigidbodyType2D.Kinematic;

            IsPlanted = true;
            return true;
        }

        public StaffHold Slide(float lean, float fixedDeltaTime)
        {
            if (!IsPlanted || !HasWielder)
                return StaffHold.LetGo;

            depth = Mathf.Clamp(depth - lean * slideSpeed * fixedDeltaTime, 0f, reach);
            wielder.MovePosition(PositionAt(depth));

            if (AtTop && lean > InputThreshold)
                return StaffHold.BackOnLedge;

            // Reaching the bottom is not the drop. Still pushing down once there is.
            if (AtBottom && lean < -InputThreshold)
            {
                dropTimer += fixedDeltaTime;

                if (dropTimer >= dropHoldTime)
                    return StaffHold.LetGo;
            }
            else
            {
                dropTimer = 0f;
            }

            return StaffHold.Holding;
        }

        public void Release()
        {
            IsPlanted = false;
            dropTimer = 0f;
            ShoulderPole();

            if (!HasWielder)
                return;

            wielder.bodyType = wielderBodyType;
            wielder.linearVelocity = Vector2.zero;
        }

        public void HoldPolePosition()
        {
            if (IsPlanted && pole != null)
                pole.position = plantedPosition;
        }

        public Vector2 PositionAt(float atDepth)
        {
            float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

            return new Vector2(
                Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                anchor.y - atDepth);
        }

        void ShoulderPole()
        {
            if (pole == null)
                return;

            pole.localPosition = new Vector3(sideOffset * facing, restPosition.y, restPosition.z);

            if (visual != null)
                visual.flipX = facing < 0;
        }

        public void DrawGizmos()
        {
            if (!IsPlanted)
                return;

            Gizmos.color = Color.green;
            Gizmos.DrawLine(PositionAt(0f), PositionAt(reach));

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(PositionAt(reach), 0.12f);
        }

        // MovePosition on a kinematic body drives straight through solid ground, so look down the
        // pole from the ledge surface and shorten the reach wherever the feet would land first.
        float ClearReach(float rawReach)
        {
            if (rawReach <= Epsilon)
                return 0f;

            float surfaceY = anchor.y - WielderFeetOffset;
            var origin = new Vector2(plantedPosition.x, surfaceY);

            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, rawReach, groundLayers);

            if (hit.collider == null)
                return rawReach;

            return Mathf.Clamp(surfaceY - hit.point.y, 0f, rawReach);
        }

        public static Vector2 LocalSpan(Collider2D collider2d)
        {
            if (collider2d == null)
                return Vector2.zero;

            if (collider2d is BoxCollider2D box)
                return new Vector2(box.offset.y, box.size.y);

            Bounds bounds = collider2d.bounds;
            float scale = Mathf.Max(MinScale, Mathf.Abs(collider2d.transform.lossyScale.y));
            float centre = collider2d.transform.InverseTransformPoint(bounds.center).y;

            return new Vector2(centre, bounds.size.y / scale);
        }
    }
}
