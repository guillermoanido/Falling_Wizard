using System;
using UnityEngine;

namespace FallingWizard.Player
{
    /// <summary>What a step of holding on to the staff produced.</summary>
    public enum StaffHold
    {
        /// <summary>Still on the pole, somewhere between the top and the bottom.</summary>
        Holding,

        /// <summary>Climbed back to the top of the hitbox and stepped off onto the ledge.</summary>
        BackOnLedge,

        /// <summary>Reached the bottom of the hitbox and kept pushing down: let go and fall.</summary>
        LetGo,
    }

    /// <summary>
    /// The staff mechanic, with no Unity component around it. The pole's hitbox is the whole
    /// rule: its vertical span is exactly how far the wielder may travel, so a taller collider
    /// is a longer descent and nothing else has to be told about it.
    /// </summary>
    [Serializable]
    public class StaffLogic
    {
        public const float Epsilon = 0.01f;

        const float InputThreshold = 0.5f;
        const float MinScale = 0.0001f;

        [Tooltip("How fast the wielder slides along the pole, in units per second.")]
        [SerializeField] float slideSpeed = 4f;

        [Tooltip("Depth over which the wielder swings from the ledge onto the pole, so joining " +
                 "it is not a snap. How far out they end up is the staff's own offset.")]
        [SerializeField] float swingDepth = 0.5f;

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

        /// <summary>True while the pole is driven in at a ledge and being climbed.</summary>
        public bool IsPlanted { get; private set; }

        public bool HasPole => hitbox != null;

        public bool HasWielder => wielder != null;

        /// <summary>How far this descent may travel, once ground below has been accounted for.</summary>
        public float Reach => reach;

        /// <summary>How far down the pole the wielder currently is, in units.</summary>
        public float Depth => depth;

        /// <summary>0 at the top of the hitbox, 1 at the bottom.</summary>
        public float Progress => reach <= Epsilon ? 1f : Mathf.Clamp01(depth / reach);

        public bool AtTop => depth <= Epsilon;

        public bool AtBottom => depth >= reach - Epsilon;

        /// <summary>Where the wielder hangs right now, so a drop can be measured from it.</summary>
        public Vector2 HangPosition => PositionAt(depth);

        /// <summary>Where the wielder came from, and where climbing back up returns them.</summary>
        public Vector2 Anchor => anchor;

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

        /// <summary>
        /// Carry the staff on the side the wielder is looking, sprite flipped to match. A planted
        /// pole ignores this: it has been driven in and the wielder is hanging off it.
        /// </summary>
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

        /// <summary>
        /// The height of the hitbox, measured fresh each time, so growing the collider at
        /// runtime grows the descent with it.
        /// </summary>
        public float MeasureReach() => hitbox != null ? hitbox.bounds.size.y : 0f;

        /// <summary>
        /// Plant the pole at the wielder's ledge and take over their body. Returns false when
        /// there is no pole, no wielder, or no room under the ledge worth descending into.
        /// </summary>
        public bool Plant(int wielderFacing)
        {
            if (!HasPole || !HasWielder)
                return false;

            // Side first, so the pole is already where it belongs when it is driven in.
            Face(wielderFacing);

            anchor = wielder.position;
            depth = 0f;
            dropTimer = 0f;

            // The pole now belongs to the world rather than to the wielder: it stays exactly
            // where it was planted while they slide along it, and everything below is measured
            // against that line rather than against where they happened to be standing.
            plantedPosition = pole.position;

            reach = ClearReach(MeasureReach());
            if (reach <= Epsilon)
                return false;

            wielderBodyType = wielder.bodyType;
            wielder.linearVelocity = Vector2.zero;
            wielder.bodyType = RigidbodyType2D.Kinematic;

            IsPlanted = true;
            return true;
        }

        /// <summary>
        /// Move along the pole. Positive <paramref name="lean"/> climbs and negative lowers.
        /// Both ends of the hitbox are hard stops, not places you slide past.
        /// </summary>
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

        /// <summary>Hand the body back to physics and shoulder the staff again.</summary>
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

        /// <summary>
        /// Called once the wielder has moved. A planted pole lives in world space, so it has to
        /// shrug off the parent transform that would otherwise carry it down the drop.
        /// </summary>
        public void HoldPolePosition()
        {
            if (IsPlanted && pole != null)
                pole.position = plantedPosition;
        }

        /// <summary>
        /// Where the wielder sits at a given depth. The planted pole is the fixed thing here:
        /// they let go of the ledge and swing onto its line, then follow it down.
        /// </summary>
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

        // MovePosition on a kinematic body drives straight through solid ground, so find where
        // the feet would land under the hang position and shorten the reach to stop there.
        float ClearReach(float rawReach)
        {
            if (rawReach <= Epsilon)
                return 0f;

            float halfHeight = wielderHitbox != null ? wielderHitbox.bounds.extents.y : 0f;
            var origin = new Vector2(plantedPosition.x, anchor.y);

            RaycastHit2D hit = Physics2D.Raycast(
                origin, Vector2.down, rawReach + halfHeight, groundLayers);

            if (hit.collider == null)
                return rawReach;

            return Mathf.Clamp(anchor.y - (hit.point.y + halfHeight), 0f, rawReach);
        }

        /// <summary>
        /// The hitbox's vertical span in its own local space, as (centre, height), for fitting
        /// the sprite to whatever the collider says the staff is.
        /// </summary>
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
