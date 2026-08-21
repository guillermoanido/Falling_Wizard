using System;
using UnityEngine;

namespace FallingWizard.Player
{
    // What the staff is currently being used as.
    public enum StaffMode
    {
        // Driven in over the lip of a ledge and climbed down. The wizard hangs off it.
        Ladder,

        // Laid flat over the drop as a plank. The wizard walks on it.
        Bridge,
    }

    // What a step of holding on to the staff produced.
    public enum StaffHold
    {
        Holding,
        BackOnLedge,
        LetGo,
    }

    // The wizard's staff: a child of whoever carries it, and the Unity shell around Pole, which
    // is where the whole mechanic lives. The pole's hitbox height is its reach, so a taller
    // collider is a longer climb and nothing else has to be told about it.
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

        // Everything the staff actually does.
        public Pole Logic
        {
            get
            {
                Bind();
                return pole;
            }
        }

        // The pole's height, straight off the hitbox.
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

            // A bridge you never cast must not be standing there holding the wizard up.
            if (bridgeCollider != null)
                bridgeCollider.enabled = false;
        }

        // The wielder moves in FixedUpdate and drags every child along, so a planted pole is put
        // back afterwards, once per rendered frame.
        void LateUpdate() => pole.HoldPolePosition();

        void OnDrawGizmosSelected() => pole.DrawGizmos();

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
                Debug.LogError($"'{name}' has no hitbox, so it has no reach and cannot be climbed. " +
                               "Add a Collider2D to it.", this);
                return;
            }

            // The pole measures a descent, it does not push anything around.
            hitbox.isTrigger = true;
            pole.BindPole(hitbox, visual, bridgeCollider);
        }

        // ============================================================================== Pole ====

        // The staff mechanic, with no Unity component around it.
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

            // How far under the wielder's middle their feet are.
            float WielderFeetOffset =>
                wielder != null && wielderHitbox != null
                    ? wielder.position.y - wielderHitbox.bounds.min.y
                    : 0f;

            // How far below the end of the pole the wielder finishes: everything between their
            // grip and their feet still hangs past it.
            float HangBelowTip => WielderFeetOffset + gripHeight;

            public void BindPole(Collider2D poleHitbox, SpriteRenderer poleVisual, Collider2D bridgeCollider)
            {
                hitbox = poleHitbox;
                visual = poleVisual;
                bridge = bridgeCollider;
                pole = poleHitbox != null ? poleHitbox.transform : null;

                if (pole == null)
                    return;

                // Authored on one side; which side it is carried on is up to the wielder.
                restPosition = pole.localPosition;
                carriedParent = pole.parent;
                sideOffset = Mathf.Abs(restPosition.x);
            }

            public void BindWielder(Rigidbody2D body, Collider2D bodyHitbox)
            {
                wielder = body;
                wielderHitbox = bodyHitbox;

                if (body != null)
                    wielderBodyType = body.bodyType;
            }

            // Carry the staff on the side the wielder is looking, sprite flipped to match. A
            // planted pole ignores this: it has been driven in and is part of the world.
            public void Face(int wielderFacing)
            {
                if (IsPlanted || wielderFacing == 0)
                    return;

                facing = wielderFacing < 0 ? -1 : 1;
                ShoulderPole();
            }

            // The height of the hitbox in its own local space, scaled. Deliberately NOT
            // bounds.size.y: bounds is a world AABB, so once the pole is laid flat as a bridge
            // that would silently start reporting the pole's thickness as its reach.
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

            // Move along the pole. Positive lean climbs and negative lowers. Both ends of the
            // hitbox are hard stops, not places you slide past.
            public StaffHold Slide(float lean, float fixedDeltaTime)
            {
                if (!IsPlanted || !HasWielder || Mode != StaffMode.Ladder)
                    return StaffHold.LetGo;

                depth = Mathf.Clamp(depth - lean * slideSpeed * fixedDeltaTime, 0f, reach);
                wielder.MovePosition(PositionAt(depth));

                if (AtTop && lean > leanThreshold)
                    return StaffHold.BackOnLedge;

                // Reaching the bottom is not the drop. Still pushing down once there is.
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

            // Hand the body back to physics and shoulder the staff again.
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

                // Bridge mode never took the body, so it has nothing to hand back.
                if (wasLadder)
                {
                    wielder.bodyType = wielderBodyType;
                    wielder.linearVelocity = Vector2.zero;
                }
            }

            // Called once the wielder has moved. A planted pole lives in world space, so it has
            // to shrug off the parent transform that would otherwise carry it down the drop.
            public void HoldPolePosition()
            {
                if (IsPlanted && pole != null)
                    pole.SetPositionAndRotation(plantedPosition, plantedRotation);
            }

            // Where the wielder sits at a given depth. The planted pole is the fixed thing: they
            // let go of the ledge, swing onto its line, then follow it down.
            public Vector2 PositionAt(float atDepth)
            {
                float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

                return new Vector2(
                    Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                    anchor.y - atDepth);
            }

            public void Validate()
            {
                slideSpeed = Mathf.Max(0.01f, slideSpeed);
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
                Gizmos.DrawWireSphere(PositionAt(reach), 0.12f);
            }

            // The hitbox's vertical span in its own local space, as (centre, height). Rotation
            // independent, unlike bounds.
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

            // Drive the pole in over the lip of the ledge and take over the wielder's body.
            bool PlantAsLadder(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = wielder.position;
                depth = 0f;
                dropTimer = 0f;

                // The pole stops being something the wielder carries and becomes part of the
                // world: hung just past the lip with its top flush to the ledge, so its far end
                // is where the feet will end up and the drop can be read straight off it.
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

            // Lay the staff flat over the drop as a plank. The wizard keeps their body and just
            // walks out onto it.
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
                float span = MeasureReach();
                float thickness = LocalSpan(hitbox).x * Mathf.Abs(pole.lossyScale.x);
                float surfaceY = anchor.y - WielderFeetOffset;

                // The wizard stands on it, so it must not be dragged around by their own motion.
                if (pole.parent != null)
                    pole.SetParent(null, true);

                plantedRotation = Quaternion.Euler(0f, 0f, 90f);
                plantedPosition = new Vector3(
                    edgeX + facing * span * 0.5f,
                    surfaceY - thickness * 0.5f,
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                bridge.enabled = true;
                reach = 0f;
                depth = 0f;

                Mode = StaffMode.Bridge;
                IsPlanted = true;
                return true;       // body stays dynamic: a bridge is scenery, not a ride
            }

            // How far the top of the hitbox sits above the pole's own origin, in world units.
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

            // MovePosition on a kinematic body drives straight through solid ground, so look down
            // the pole from the ledge surface and shorten the reach where the feet would land.
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
