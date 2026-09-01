using System;
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
    public class Staff : MonoBehaviour
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
            box.offset = new Vector2(0f, -defaultLength / 2f);

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

        [Serializable]
        public class Pole
        {
            public const float Epsilon = 0.01f;

            const float MinScale = 0.0001f;

            [Header("Climbing")]
            [Tooltip("How fast the wielder slides along the pole, in boxes per second.")]
            [Min(0.01f)] public float slideSpeed = 3f;

            [Tooltip("Depth over which the wielder swings from the ledge onto the pole, so joining " +
                     "it is not a snap. How far out they end up is where the pole was driven in.")]
            [Min(0f)] public float swingDepth = 0.5f;

            [Tooltip("Stick tilt needed before up or down counts as climbing.")]
            [Range(0f, 1f)] public float leanThreshold = 0.5f;

            [Tooltip("Seconds of held down input at the very bottom before letting go. Short " +
                     "enough to feel instant, long enough that sliding down is not a drop.")]
            [Min(0f)] public float dropHoldTime = 0.2f;

            [Header("Planting")]
            [Tooltip("How far past the lip of the ledge the pole is driven in, so it hangs clear " +
                     "of the ledge face instead of scraping down it.")]
            public float lipClearance = 0.15f;

            [Tooltip("How far above their middle the wielder grips. They can lower themselves " +
                     "until that grip reaches the very end of the pole, so the last stretch is a " +
                     "hand hang with the body dangling past the tip. Higher grip, lower hang.")]
            public float gripHeight = 0.25f;

            [Tooltip("Which layers the staff can find footing on, so it never lowers into solid " +
                     "ground. Defaults to Ground.")]
            public LayerMask groundLayers = 1 << 6;

            [NonSerialized] Collider2D hitbox;
            [NonSerialized] Collider2D bridge;
            [NonSerialized] Transform pole;
            [NonSerialized] SpriteRenderer visual;
            [NonSerialized] Transform carriedParent;
            [NonSerialized] Vector3 restPosition;
            [NonSerialized] float sideOffset;

            [NonSerialized] Rigidbody2D wielder;
            [NonSerialized] Collider2D wielderHitbox;
            [NonSerialized] RigidbodyType2D wielderBodyType;

            [NonSerialized] Vector2 anchor;
            [NonSerialized] Vector3 plantedPosition;
            [NonSerialized] Quaternion plantedRotation = Quaternion.identity;
            [NonSerialized] float reach;
            [NonSerialized] float depth;
            [NonSerialized] float dropTimer;
            [NonSerialized] int facing = 1;
            [NonSerialized] Vector3 authoredScale = Vector3.one;
            [NonSerialized] float lengthScale = 1f;

            // Planted flat, the pole turns a quarter turn - which way depends on which side
            // of the wizard it is being laid out on.
            const float QuarterTurn = 90f;

            const float MinSlideSpeed = 0.01f;
            const float TipMarkerRadius = 0.12f;

            public bool IsPlanted { get; private set; }
            public StaffMode Mode { get; private set; } = StaffMode.Ladder;

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

            public void BindPole(Collider2D poleHitbox, SpriteRenderer poleVisual, Collider2D bridgeCollider)
            {
                hitbox = poleHitbox;
                visual = poleVisual;
                bridge = bridgeCollider;
                pole = poleHitbox != null ? poleHitbox.transform : null;

                if (pole == null)
                    return;

                restPosition = pole.localPosition;
                carriedParent = pole.parent;
                sideOffset = Mathf.Abs(restPosition.x);
                authoredScale = pole.localScale;
            }

            public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
            {
                wielder = body;
                wielderHitbox = bodyHitbox;

                if (body != null)
                    wielderBodyType = body.bodyType;
            }

            public void Face(int wielderFacing)
            {
                if (IsPlanted || wielderFacing == 0)
                    return;

                facing = wielderFacing < 0 ? -1 : 1;
                ShoulderPole();
            }

            // The reach is measured off the hitbox times the pole's own scale, so lengthening
            // the staff is one transform write and nothing else has to be told - MeasureReach,
            // the hang, the bridge span and the drawn pole all follow on their own.
            public float LengthScale => lengthScale;

            public void SetLengthScale(float scale)
            {
                scale = Mathf.Max(0.1f, scale);

                if (pole == null || Mathf.Approximately(scale, lengthScale))
                    return;

                lengthScale = scale;
                pole.localScale = new Vector3(authoredScale.x, authoredScale.y * scale,
                    authoredScale.z);
            }

            public float MeasureReach()
            {
                if (hitbox == null || pole == null)
                    return 0f;

                return LocalSpan(hitbox).y * Mathf.Abs(pole.lossyScale.y);
            }

            public bool Plant(StaffMode mode, int wielderFacing, float edgeX) =>
                mode == StaffMode.Bridge
                    ? PlantAsBridge(wielderFacing, edgeX)
                    : PlantAsLadder(wielderFacing, edgeX);

            public StaffHold Slide(float lean, float fixedDeltaTime)
            {
                if (!IsPlanted || !HasWielder || Mode != StaffMode.Ladder)
                    return StaffHold.LetGo;

                depth = Mathf.Clamp(depth - lean * slideSpeed * fixedDeltaTime, 0f, reach);
                wielder.MovePosition(PositionAt(depth));

                if (AtTop && lean > leanThreshold)
                    return StaffHold.BackOnLedge;

                if (AtBottom && lean < -leanThreshold)
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
                bool wasLadder = IsPlanted && Mode == StaffMode.Ladder;

                IsPlanted = false;
                dropTimer = 0f;

                if (bridge != null)
                    bridge.enabled = false;

                if (pole != null)
                {
                    pole.localRotation = Quaternion.identity;

                    if (pole.parent != carriedParent)
                        pole.SetParent(carriedParent, false);
                }

                plantedRotation = Quaternion.identity;
                Mode = StaffMode.Ladder;
                ShoulderPole();

                if (!HasWielder)
                    return;

                if (wasLadder)
                {
                    wielder.bodyType = wielderBodyType;
                    wielder.linearVelocity = Vector2.zero;
                }
            }

            public void HoldPolePosition()
            {
                if (IsPlanted && pole != null)
                    pole.SetPositionAndRotation(plantedPosition, plantedRotation);
            }

            public Vector2 PositionAt(float atDepth)
            {
                float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

                return new Vector2(
                    Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                    anchor.y - atDepth);
            }

            public void Validate()
            {
                slideSpeed = Mathf.Max(MinSlideSpeed, slideSpeed);
                swingDepth = Mathf.Max(0f, swingDepth);
                dropHoldTime = Mathf.Max(0f, dropHoldTime);
            }

            public void DrawGizmos()
            {
                if (!IsPlanted || Mode != StaffMode.Ladder)
                    return;

                Gizmos.color = Color.green;
                Gizmos.DrawLine(PositionAt(0f), PositionAt(reach));

                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(PositionAt(reach), TipMarkerRadius);
            }

            public static Vector2 LocalSpan(Collider2D collider2d)
            {
                LocalBox(collider2d, out Vector2 centre, out Vector2 size);
                return new Vector2(centre.y, size.y);
            }

            public static void LocalBox(Collider2D collider2d, out Vector2 centre, out Vector2 size)
            {
                centre = Vector2.zero;
                size = Vector2.zero;

                if (collider2d == null)
                    return;

                if (collider2d is BoxCollider2D box)
                {
                    centre = box.offset;
                    size = box.size;
                    return;
                }

                Bounds bounds = collider2d.bounds;
                Transform owner = collider2d.transform;

                centre = owner.InverseTransformPoint(bounds.center);
                size = new Vector2(
                    bounds.size.x / Mathf.Max(MinScale, Mathf.Abs(owner.lossyScale.x)),
                    bounds.size.y / Mathf.Max(MinScale, Mathf.Abs(owner.lossyScale.y)));
            }

            bool PlantAsLadder(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = wielder.position;
                depth = 0f;
                dropTimer = 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                float topAboveOrigin = TopAboveOrigin();

                plantedRotation = Quaternion.identity;
                plantedPosition = new Vector3(
                    edgeX + facing * lipClearance,
                    surfaceY - topAboveOrigin,
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                reach = ClearReach(MeasureReach() + HangBelowTip);

                if (reach <= Epsilon)
                {
                    ShoulderPole();
                    return false;
                }

                wielderBodyType = wielder.bodyType;
                wielder.linearVelocity = Vector2.zero;
                wielder.bodyType = RigidbodyType2D.Kinematic;

                Mode = StaffMode.Ladder;
                IsPlanted = true;
                return true;
            }

            bool PlantAsBridge(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                if (bridge == null)
                {
                    Debug.LogWarning("The staff has no bridge collider, so there is nothing to " +
                                     "stand on. Assign one on the Staff component.");
                    return false;
                }

                Face(wielderFacing);

                anchor = wielder.position;
                float surfaceY = anchor.y - WielderFeetOffset;

                LocalBox(bridge, out Vector2 localCentre, out Vector2 localSize);

                float scaleX = Mathf.Abs(pole.lossyScale.x);
                float scaleY = Mathf.Abs(pole.lossyScale.y);
                float length = localSize.y * scaleY;
                float thickness = localSize.x * scaleX;

                if (pole.parent != null)
                    pole.SetParent(null, true);

                plantedRotation = Quaternion.Euler(0f, 0f, facing > 0 ? -QuarterTurn : QuarterTurn);

                var carried = new Vector2(
                    facing * localCentre.y * scaleY,
                    -facing * localCentre.x * scaleX);

                var wanted = new Vector2(
                    edgeX + facing * (lipClearance + length * 0.5f),
                    surfaceY - thickness * 0.5f);

                plantedPosition = new Vector3(
                    wanted.x - carried.x,
                    wanted.y - carried.y,
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                bridge.enabled = true;
                reach = 0f;
                depth = 0f;

                Mode = StaffMode.Bridge;
                IsPlanted = true;
                return true;
            }

            float TopAboveOrigin()
            {
                Vector2 span = LocalSpan(hitbox);
                float scale = Mathf.Abs(pole.lossyScale.y);
                return (span.x + span.y * 0.5f) * scale;
            }

            void ShoulderPole()
            {
                if (pole == null)
                    return;

                pole.localPosition = new Vector3(sideOffset * facing, restPosition.y, restPosition.z);

                if (visual != null)
                    visual.flipX = facing < 0;
            }

            float ClearReach(float rawReach)
            {
                if (rawReach <= Epsilon)
                    return 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                var origin = new Vector2(plantedPosition.x, surfaceY);

                var filter = new ContactFilter2D
                {
                    useLayerMask = true,
                    layerMask = groundLayers,
                    useTriggers = false,
                };

                var hits = new RaycastHit2D[1];

                if (Physics2D.Raycast(origin, Vector2.down, filter, hits, rawReach) == 0)
                    return rawReach;

                return Mathf.Clamp(surfaceY - hits[0].point.y, 0f, rawReach);
            }
        }
    }
}
