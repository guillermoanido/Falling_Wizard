using System;
using System.Collections.Generic;
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

        void OnDrawGizmosSelected()
        {
            if (hitbox != null)
            {
                Gizmos.color = new Color(0.4f, 0.8f, 1f);
                Gizmos.DrawWireCube(hitbox.bounds.center, hitbox.bounds.size);
            }

            if (bridgeCollider != null && bridgeCollider.enabled)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f);
                Gizmos.DrawWireCube(bridgeCollider.bounds.center, bridgeCollider.bounds.size);
            }

            pole.DrawGizmos();
        }

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
            const float MinSlideSpeed = 0.01f;
            const float MinLengthScale = 0.1f;
            const float TipMarkerRadius = 0.12f;

            const float QuarterTurn = 90f;

            const float LandingSlack = 0.06f;

            static readonly List<Collider2D> Overlaps = new List<Collider2D>(4);
            static readonly List<RaycastHit2D> Rays = new List<RaycastHit2D>(4);

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

            [Tooltip("Seconds after the staff is released before it can be planted again.")]
            [Min(0f)] public float cooldown = 0.5f;

            [Tooltip("How far the staff is lifted overhead, in boxes. Adds to climb reach.")]
            [Min(0f)] public float raiseHeight = 0.6f;

            [Tooltip("Seconds spent stepping over the lip at the top of a climb. The wizard rises " +
                     "clear of the edge before sliding across it, so they arc over the corner " +
                     "instead of cutting through it - the same easing the descent uses to swing " +
                     "onto the pole, run the other way. 0 snaps them over as it used to.")]
            [Min(0f)] public float mountSeconds = 0.18f;

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
            [NonSerialized] float mountTimer = -1f;
            [NonSerialized] Vector2 mountFrom;

            [NonSerialized] float readyAt;

            [NonSerialized] bool raised;

            [NonSerialized] bool climbing;
            [NonSerialized] Vector2 climbLanding;

            [NonSerialized] float climbHangX;

            [NonSerialized] int facing = 1;

            [NonSerialized] Vector2 authoredHitbox;
            [NonSerialized] Vector2 authoredBridge;
            [NonSerialized] Vector3 authoredVisualPosition;
            [NonSerialized] Vector3 authoredVisualScale = Vector3.one;
            [NonSerialized] Vector2 authoredVisualSize = Vector2.one;
            [NonSerialized] float lengthScale = 1f;

            public bool IsPlanted { get; private set; }

            public bool IsReady => Time.time >= readyAt;

            public float CooldownLeft => Mathf.Max(0f, readyAt - Time.time);

            public bool IsClimbing => IsPlanted && climbing;

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

            ContactFilter2D GroundFilter => new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = groundLayers,
                useTriggers = false,
            };

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

                authoredHitbox = LocalSpan(hitbox);
                authoredBridge = LocalSpan(bridge);

                if (visual != null)
                {
                    authoredVisualPosition = visual.transform.localPosition;
                    authoredVisualScale = visual.transform.localScale;
                    authoredVisualSize = visual.size;
                }

                lengthScale = 1f;
            }

            public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
            {
                wielder = body;
                wielderHitbox = bodyHitbox;

                if (body != null)
                    wielderBodyType = body.bodyType;

                readyAt = 0f;
            }

            public void Raise(bool up)
            {
                if (IsPlanted || raised == up)
                    return;

                raised = up;
                ShoulderPole();
            }

            public void Face(int wielderFacing)
            {
                if (IsPlanted || wielderFacing == 0)
                    return;

                facing = wielderFacing < 0 ? -1 : 1;
                ShoulderPole();
            }

            public float LengthScale => lengthScale;

            public void SetLengthScale(float scale)
            {
                scale = Mathf.Max(MinLengthScale, scale);

                if (pole == null || Mathf.Approximately(scale, lengthScale))
                    return;

                lengthScale = scale;

                Stretch(hitbox as BoxCollider2D, authoredHitbox, scale);
                Stretch(bridge as BoxCollider2D, authoredBridge, scale);
                StretchVisual(scale);
            }

            static void Stretch(BoxCollider2D box, Vector2 authored, float scale)
            {
                if (box == null || authored.y <= Epsilon)
                    return;

                float length = authored.y * scale;

                box.size = new Vector2(box.size.x, length);
                box.offset = new Vector2(box.offset.x, authored.x + (length - authored.y) * 0.5f);
            }

            void StretchVisual(float scale)
            {
                if (visual == null)
                    return;

                float growth = authoredHitbox.y * (scale - 1f);

                if (visual.drawMode != SpriteDrawMode.Simple)
                {
                    visual.transform.localScale = authoredVisualScale;
                    visual.size = new Vector2(authoredVisualSize.x, authoredVisualSize.y + growth);

                    visual.transform.localPosition =
                        authoredVisualPosition + Vector3.up * (growth * FractionOfArtBelowPivot);

                    return;
                }

                visual.transform.localScale = new Vector3(authoredVisualScale.x,
                    authoredVisualScale.y * scale, authoredVisualScale.z);

                visual.transform.localPosition = new Vector3(authoredVisualPosition.x,
                    authoredVisualPosition.y * scale, authoredVisualPosition.z);
            }

            float FractionOfArtBelowPivot
            {
                get
                {
                    Sprite art = visual != null ? visual.sprite : null;

                    return art != null && art.rect.height > 0f
                        ? art.pivot.y / art.rect.height
                        : 0f;
                }
            }

            public float ClimbHeight => MeasureReach() + HangBelowTip;

            public float ClimbUpHeight => ClimbHeight + raiseHeight;

            public float MeasureReach()
            {
                if (hitbox == null || pole == null)
                    return 0f;

                return LocalSpan(hitbox).y * Mathf.Abs(pole.lossyScale.y);
            }

            public void Validate()
            {
                slideSpeed = Mathf.Max(MinSlideSpeed, slideSpeed);
                raiseHeight = Mathf.Max(0f, raiseHeight);
                swingDepth = Mathf.Max(0f, swingDepth);
                dropHoldTime = Mathf.Max(0f, dropHoldTime);
                cooldown = Mathf.Max(0f, cooldown);
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

                pole.localPosition = new Vector3(
                    sideOffset * facing,
                    restPosition.y + (raised ? raiseHeight : 0f),
                    restPosition.z);

                if (visual != null)
                    visual.flipX = facing < 0;
            }

            public bool Plant(StaffMode mode, int wielderFacing, float edgeX) =>
                mode == StaffMode.Bridge
                    ? PlantAsBridge(wielderFacing, edgeX)
                    : PlantAsLadder(wielderFacing, edgeX);

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

            public bool PlantAsClimb(int wielderFacing, Vector2 lip, Vector2 landing)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = new Vector2(wielder.position.x, lip.y + WielderFeetOffset);

                plantedRotation = Quaternion.identity;
                plantedPosition = new Vector3(
                    lip.x - facing * lipClearance,
                    lip.y - TopAboveOrigin(),
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                depth = anchor.y - wielder.position.y;
                float climbHeight = ClimbUpHeight;

                if (depth <= Epsilon || depth > climbHeight)
                {
                    ShoulderPole();
                    return false;
                }

                reach = depth;

                climbing = true;
                mountTimer = -1f;
                climbLanding = landing;
                climbHangX = wielder.position.x;

                dropTimer = -dropHoldTime;

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

            float ClearReach(float rawReach)
            {
                if (rawReach <= Epsilon)
                    return 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                var origin = new Vector2(plantedPosition.x, surfaceY);

                if (Physics2D.Raycast(origin, Vector2.down, GroundFilter, Rays, rawReach) == 0)
                    return rawReach;

                return Mathf.Clamp(surfaceY - Rays[0].point.y, 0f, rawReach);
            }

            public StaffHold Slide(float lean, float fixedDeltaTime)
            {
                if (!IsPlanted || !HasWielder || Mode != StaffMode.Ladder)
                    return StaffHold.LetGo;

                if (mountTimer >= 0f)
                    return Mount(fixedDeltaTime);

                float pull = Mathf.Abs(lean) > leanThreshold ? lean : 0f;

                depth = Mathf.Clamp(depth - pull * slideSpeed * fixedDeltaTime, 0f, reach);
                wielder.MovePosition(PositionAt(depth));

                if (AtTop && lean > leanThreshold)
                {
                    if (!climbing || mountSeconds <= Epsilon || !LandingIsClear())
                        return StaffHold.BackOnLedge;

                    mountFrom = wielder.position;
                    mountTimer = 0f;
                    return StaffHold.Holding;
                }

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

            public void Release(bool arrived = false)
            {
                bool wasLadder = IsPlanted && Mode == StaffMode.Ladder;
                bool wasPlanted = IsPlanted;

                bool toppedOut = arrived && IsPlanted && climbing && AtTop;

                IsPlanted = false;
                climbing = false;
                mountTimer = -1f;
                raised = false;
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

                if (wasPlanted)
                    readyAt = Time.time + cooldown;

                if (!HasWielder)
                    return;

                if (wasLadder)
                {
                    if (toppedOut && LandingIsClear())
                        wielder.position = climbLanding;

                    wielder.bodyType = wielderBodyType;
                    wielder.linearVelocity = Vector2.zero;
                }
            }

            public void HoldPolePosition()
            {
                if (IsPlanted && pole != null)
                    pole.SetPositionAndRotation(plantedPosition, plantedRotation);
            }

            bool LandingIsClear()
            {
                if (wielderHitbox == null || wielder == null)
                    return true;

                Bounds box = wielderHitbox.bounds;
                Vector2 hullAt = climbLanding + ((Vector2)box.center - wielder.position);

                var room = (Vector2)box.size - Vector2.one * LandingSlack;

                if (Physics2D.OverlapBox(hullAt, room, 0f, GroundFilter, Overlaps) == 0)
                    return true;

#if UNITY_EDITOR
                Debug.LogWarning("A staff climb reached the top and then could not be set down " +
                                 "on it, so the wizard was let go in mid-air and fell. Something " +
                                 "on the ground layer is in the way just past the lip.", pole);
#endif

                return false;
            }

            StaffHold Mount(float fixedDeltaTime)
            {
                mountTimer += fixedDeltaTime;

                float t = Mathf.Clamp01(mountTimer / mountSeconds);
                float eased = t * t * (3f - 2f * t);

                wielder.MovePosition(new Vector2(
                    Mathf.Lerp(mountFrom.x, climbLanding.x, eased * eased),
                    Mathf.Lerp(mountFrom.y, climbLanding.y, Mathf.Sqrt(eased))));

                if (t < 1f)
                    return StaffHold.Holding;

                mountTimer = -1f;
                return StaffHold.BackOnLedge;
            }

            public Vector2 PositionAt(float atDepth)
            {
                if (climbing)
                    return new Vector2(climbHangX, anchor.y - atDepth);

                float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

                return new Vector2(
                    Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                    anchor.y - atDepth);
            }
        }
    }
}
