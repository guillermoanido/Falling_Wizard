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

            [Tooltip("Stand the carried staff on the wizard's soles, measured off their collider, " +
                     "instead of trusting the height the Staff object was dragged to. The pole is " +
                     "drawn and measured from its own origin upwards, so half a box of slack here " +
                     "is invisible on a short staff and becomes a quarter of a box of float on a " +
                     "long one. Turn it off only if you deliberately want the staff carried at " +
                     "some other height.")]
            public bool anchorToFeet = true;

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

            // The pole as it was built, in boxes: x is the box's centre height above the staff's
            // origin and y is the box's height. Held so that going back to rank 1 puts the exact
            // authored numbers back rather than a value that has been multiplied and divided.
            [NonSerialized] Vector2 authoredHitbox;
            [NonSerialized] Vector2 authoredBridge;
            [NonSerialized] Vector3 authoredVisualPosition;
            [NonSerialized] Vector3 authoredVisualScale = Vector3.one;
            [NonSerialized] Vector2 authoredVisualSize = Vector2.one;
            [NonSerialized] float lengthScale = 1f;

            // Planted flat, the pole turns a quarter turn - which way depends on which side
            // of the wizard it is being laid out on.
            const float QuarterTurn = 90f;

            const float MinSlideSpeed = 0.01f;
            const float TipMarkerRadius = 0.12f;
            const float MinLengthScale = 0.1f;

            // A third of a pixel at 32 pixels per box. Under this and nobody can see it; over it
            // and a long staff will magnify it into something they can.
            const float MaxFootDrift = 0.01f;

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

                authoredHitbox = LocalSpan(hitbox);
                authoredBridge = LocalSpan(bridge);

                if (visual != null)
                {
                    authoredVisualPosition = visual.transform.localPosition;
                    authoredVisualScale = visual.transform.localScale;
                    authoredVisualSize = visual.size;
                }

                lengthScale = 1f;
                WarnIfThePoleDoesNotStandOnItsOrigin();
                WarnIfTheArtDoesNotStandOnItsOrigin();
            }

            public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
            {
                wielder = body;
                wielderHitbox = bodyHitbox;

                if (body != null)
                    wielderBodyType = body.bodyType;

                AnchorRestToFeet();
            }

            // Where the staff is CARRIED, read off the wizard's collider rather than off wherever
            // the Staff object happened to be dragged to. This is the same honest answer
            // Movement.Footing gives for the soles, and for the same reason: the pole is drawn and
            // measured from its own origin upwards, so any slack between that origin and the feet
            // is multiplied by the length. Half a box of it was invisible on a rank-1 staff and
            // lifted a rank-2 staff a quarter of a box off the floor.
            void AnchorRestToFeet()
            {
                if (!anchorToFeet || pole == null || wielder == null || wielderHitbox == null)
                    return;

                // Asked of the collider's AUTHORED numbers, not of its bounds. Physics2D has
                // m_AutoSyncTransforms off in this project, and PlayerCharacter teleports the
                // wizard to their checkpoint immediately before this runs - so the fixture AABB
                // bounds is still reports may be the one from the old spawn point, which would
                // carry the staff tens of boxes away for the whole run and print a warning
                // telling the designer to make it permanent. LocalBox does no physics query at
                // all, and it is also immune to the wizard ever being rotated or scaled.
                LocalBox(wielderHitbox, out Vector2 hull, out Vector2 hullSize);
                float feet = hull.y - hullSize.y * 0.5f;
                float drift = Mathf.Abs(feet - restPosition.y);

                if (drift > MaxFootDrift)
                    Debug.LogWarning($"The staff was carried {drift:0.000} boxes off the wizard's " +
                                     "feet, so every rank of the Staff spell would have floated or " +
                                     "sunk it by that much again. Stood it on them for this run - " +
                                     $"set the Staff object's local Y to {feet:0.000} in the scene " +
                                     "to make that stick.", pole);

                restPosition = new Vector3(restPosition.x, feet, restPosition.z);

                if (!IsPlanted)
                    ShoulderPole();
            }

            // The same rule for the DRAWING. StretchVisual's Simple branch grows the art about the
            // pole's foot by lifting the renderer as fast as it stretches, and that only cancels
            // out when the foot of the art is already on the staff's origin. Authored anywhere
            // else it stays pinned wherever it was, at every rank, silently - which is the one
            // failure here that neither of the other two warnings would catch.
            void WarnIfTheArtDoesNotStandOnItsOrigin()
            {
                if (visual == null || visual.sprite == null)
                    return;

                float foot = authoredVisualPosition.y +
                             visual.sprite.bounds.min.y * authoredVisualScale.y;

                if (Mathf.Abs(foot) <= MaxFootDrift)
                    return;

                Debug.LogWarning($"The staff's art sits {foot:0.000} boxes off the pole's own " +
                                 "origin, so a longer staff will draw with its foot in the wrong " +
                                 "place however well the hitbox lines up. Set the Visual child's " +
                                 $"local Y to {authoredVisualPosition.y - foot:0.000}.", pole);
            }

            // The rule the rest of this class leans on: the BOTTOM of the pole's box is the staff's
            // origin. Growing the staff keeps that bottom edge nailed down and pushes the top up,
            // and the bottom edge is stood on the wizard's soles - so break this and a longer staff
            // stops touching the floor, which is exactly the bug the resize replaced.
            void WarnIfThePoleDoesNotStandOnItsOrigin()
            {
                if (!(hitbox is BoxCollider2D))
                {
                    Debug.LogWarning($"'{pole.name}' uses a {hitbox.GetType().Name} for its pole " +
                                     "instead of a Box Collider 2D, so it cannot be made longer - " +
                                     "the Staff spell's second rank will buy the wizard nothing. " +
                                     "Swap it for a Box Collider 2D.", pole);
                    return;
                }

                float foot = authoredHitbox.x - authoredHitbox.y * 0.5f;

                if (Mathf.Abs(foot) <= MaxFootDrift)
                    return;

                Debug.LogWarning($"The staff's hitbox sits {foot:0.000} boxes off its own origin, " +
                                 "so the foot of the pole is not where the game grows it from and " +
                                 "a longer staff will drift by that much per rank. Set the " +
                                 $"collider's Offset Y to {authoredHitbox.y * 0.5f:0.000} - half " +
                                 "its Size Y - and move the whole Staff object down by the same " +
                                 "amount so nothing appears to shift.", pole);
            }

            public void Face(int wielderFacing)
            {
                if (IsPlanted || wielderFacing == 0)
                    return;

                facing = wielderFacing < 0 ? -1 : 1;
                ShoulderPole();
            }

            // The reach is measured off the hitbox's own height, so lengthening the staff is one
            // resize and nothing else has to be told - MeasureReach, the hang, the bridge span and
            // the drawn pole all follow on their own.
            //
            // It RESIZES rather than scaling the transform, which is what it used to do. A
            // transform scale runs about the object's origin, and everything the staff is made of
            // sits half a box above that origin, so the pole grew away from the wizard's feet as
            // fast as it grew towards the sky: a rank-2 staff floated a quarter of a box off the
            // floor and its head shot past the hand holding it. The art came out stretched on top
            // of that, because only Y was ever scaled. A resize can pin the bottom edge, and the
            // bottom edge is stood on the wizard's soles.
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

            // Keeps the box's bottom edge exactly where it was built and pushes the top up. Worked
            // from the AUTHORED numbers every time rather than from the current ones, so dropping
            // back to rank 1 puts the original box back to the last decimal instead of a value
            // that has been through a multiply and a divide.
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

                // Sliced or Tiled art carries a size of its own, so the crook at the head and the
                // ferrule at the foot keep the pixels they were drawn with and only the shaft in
                // between gets longer. Set the sprite up that way - Full Rect mesh, a border above
                // the crook and below the ferrule - and a long staff stops looking melted.
                if (visual.drawMode != SpriteDrawMode.Simple)
                {
                    visual.transform.localScale = authoredVisualScale;
                    visual.transform.localPosition = authoredVisualPosition;
                    visual.size = new Vector2(authoredVisualSize.x,
                        authoredVisualSize.y + authoredHitbox.y * (scale - 1f));
                    return;
                }

                // Simple art has no size of its own, so it can only be stretched - but stretch it
                // about the pole's FOOT, not about the sprite's pivot. Lifting the renderer by the
                // same factor it grows cancels the pivot out, which is why the Visual has to be
                // authored with the bottom of the ART on the staff's origin: the art's foot lands
                // at (local position + pivot-to-art-foot) times the scale, and that only stays put
                // when the two of them add up to nothing.
                visual.transform.localScale = new Vector3(authoredVisualScale.x,
                    authoredVisualScale.y * scale, authoredVisualScale.z);

                visual.transform.localPosition = new Vector3(authoredVisualPosition.x,
                    authoredVisualPosition.y * scale, authoredVisualPosition.z);
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
