using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Wall Growth", fileName = "Wall Growth")]
    public class WallGrowthAbility : Ability
    {
        // Half a box: the offset that puts a one-box child clear of its own root, so scaling the
        // root grows it away from the anchor instead of around its middle.
        const float HalfBox = 0.5f;

        // The floor hunt starts half a box above the feet so a wall can be rooted in the step the
        // wizard is already standing on, not only in the one below them.
        const float RayLift = 0.5f;

        // A block of literally no size has a degenerate collider, which Unity complains about
        // every frame it exists.
        const float MinGrown = 0.01f;

        static readonly RaycastHit2D[] Footings = new RaycastHit2D[1];

        [Header("Wall")]
        [Tooltip("Spawned as the block. Its FIRST CHILD carries the sprite and the collider, one " +
                 "box square and centred on itself - the spell moves that child and scales the " +
                 "root, so the block grows out of its anchor rather than around its middle. " +
                 "Leave empty and a plain one is built from the sprite below.")]
        public GameObject wallPrefab;

        [Tooltip("Used when there is no prefab. A 32x32 sprite at 32 pixels per unit is one box. " +
                 "Empty draws a flat colour.")]
        public Sprite wallArt;

        [Tooltip("Tint, and the colour of the plain block when there is no art at all.")]
        public Color tint = new Color(0.42f, 0.62f, 0.38f);

        [Tooltip("Size of the finished block in boxes - a mage is one box wide and one tall. " +
                 "Wide and thin is a ledge to walk out onto; narrow and tall is a wall to climb " +
                 "or hide behind.")]
        public Vector2 size = new Vector2(3f, 0.5f);

        [Header("Where It Grows")]
        [Tooltip("Stood at a ledge, build OUT from the lip with its top level with the floor, so " +
                 "you can walk straight onto it. This is the one that gets you over a gap, and " +
                 "it is how the old staff bridge worked. Off - or nowhere near a ledge - and it " +
                 "grows upward out of the floor ahead of you instead.")]
        public bool bridgesGaps = true;

        [Tooltip("Gap left between the lip of the ledge and the near end, in boxes. A little " +
                 "clearance stops it being born inside the tile it grows off.")]
        [Min(0f)] public float lipClearance = 0.05f;

        [Tooltip("Only for growing upward on flat ground: how far in front of the wizard it " +
                 "sprouts, in boxes. Roughly one box clears their own hitbox.")]
        [Min(0f)] public float distanceAhead = 1.1f;

        [Tooltip("Only for growing upward on flat ground: how far below the wizard's feet to " +
                 "hunt for a floor to root it in, in boxes.")]
        [Min(0f)] public float rootDepth = 2f;

        [Header("Growing")]
        [Tooltip("Seconds it takes to reach full size. It is solid the whole way, so a slow " +
                 "growth can carry you out with it.")]
        [Min(0f)] public float growTime = 0.3f;

        [Header("Physics")]
        [Tooltip("Layer the block lands on. It has to be one the wizard's ground check looks at, " +
                 "or they will walk straight through it.")]
        public string groundLayer = "Ground";

        [Tooltip("Sorting order of the built block. It has to be ABOVE the tilemap or the block " +
                 "is solid, does its job, and cannot be seen at all - which reads exactly like " +
                 "the spell not working.")]
        public int sortingOrder = 1;

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal &&
            wizard.spellbook.StateOf<Growth>(this).wall == null &&
            FindFooting(wizard, out _, out _);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and need both feet under you";

            if (wizard.spellbook.StateOf<Growth>(this).wall != null)
                return "your last one is still standing";

            if (FindFooting(wizard, out _, out _))
                return null;

            if (bridgesGaps)
                return "you are not stood at a ledge, and there is no floor on the " +
                       $"'{groundLayer}' layer within {rootDepth} boxes below a point " +
                       $"{distanceAhead} boxes ahead of you either";

            return $"there is no floor on the '{groundLayer}' layer within {rootDepth} boxes " +
                   $"below a point {distanceAhead} boxes ahead of you";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!FindFooting(wizard, out Vector2 anchor, out bool fromLedge))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            growth.wall = Build(anchor);
            growth.body = Body(growth.wall);
            growth.fromLedge = fromLedge;
            growth.facing = wizard.movement.Facing;
            growth.age = 0f;

            // Which corner the block hangs off its anchor by. Out over the drop with its top
            // flush to the floor for a bridge; straight up off the floor for a wall.
            if (growth.body != null)
                growth.body.localPosition = fromLedge
                    ? new Vector3(growth.facing * HalfBox, -HalfBox, 0f)
                    : new Vector3(0f, HalfBox, 0f);

            Shape(growth, growTime <= 0f ? 1f : 0f);
            return true;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            if (growth.wall == null)
                return;

            growth.age += fixedDeltaTime;

            Shape(growth, growTime <= 0f ? 1f : Mathf.Clamp01(growth.age / growTime));
        }

        public override void OnEnded(PlayerLogic wizard) => Clear(wizard);

        public override void OnRunReset(PlayerLogic wizard) => Clear(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => Clear(wizard);

        void Clear(PlayerLogic wizard)
        {
            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            if (growth.wall != null)
                Destroy(growth.wall);

            growth.wall = null;
            growth.body = null;
            growth.age = 0f;
        }

        bool FindFooting(PlayerLogic wizard, out Vector2 anchor, out bool fromLedge)
        {
            PlayerLogic.Movement walk = wizard.movement;

            // Stood at a lip, the edge itself is the anchor - the same trick the staff bridge
            // used. Hunting for a floor AHEAD is exactly wrong here, because the whole reason to
            // cast is that there is no floor ahead.
            if (bridgesGaps && walk.TryFindLedgeEdge(out float edgeX))
            {
                anchor = new Vector2(edgeX + walk.Facing * lipClearance, walk.FeetY);
                fromLedge = true;
                return true;
            }

            fromLedge = false;

            float x = walk.Position.x + walk.Facing * distanceAhead;
            float top = walk.FeetY + RayLift;

            anchor = new Vector2(x, walk.FeetY);

            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = walk.groundLayers,
            };

            int found = Physics2D.Raycast(new Vector2(x, top), Vector2.down, filter, Footings,
                rootDepth + RayLift);

            if (found <= 0)
                return false;

            anchor = Footings[0].point;
            return true;
        }

        GameObject Build(Vector2 anchor)
        {
            if (wallPrefab != null)
            {
                GameObject grown = Instantiate(wallPrefab, anchor, Quaternion.identity);
                grown.name = displayName;
                return grown;
            }

            var root = new GameObject(displayName);
            root.transform.position = anchor;
            root.layer = Layer();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.layer = root.layer;

            var art = body.AddComponent<SpriteRenderer>();
            art.sprite = wallArt != null ? wallArt : Placeholder.Box;
            art.color = tint;
            art.sortingOrder = sortingOrder;
            art.drawMode = SpriteDrawMode.Simple;

            body.AddComponent<BoxCollider2D>();

            return root;
        }

        static Transform Body(GameObject wall) =>
            wall != null && wall.transform.childCount > 0 ? wall.transform.GetChild(0) : null;

        void Shape(Growth growth, float grown)
        {
            if (growth.wall == null)
                return;

            // A bridge reaches outward from the lip; a wall rises out of the floor. Only the axis
            // it is actually growing along is animated, so the other one is right from the first
            // frame and there is something solid to stand on immediately.
            growth.wall.transform.localScale = growth.fromLedge
                ? new Vector3(Mathf.Max(MinGrown, size.x * grown), size.y, 1f)
                : new Vector3(size.x, Mathf.Max(MinGrown, size.y * grown), 1f);
        }

        int Layer()
        {
            int layer = LayerMask.NameToLayer(groundLayer);

            if (layer >= 0)
                return layer;

            Debug.LogWarning($"'{name}' wants to build on a layer called '{groundLayer}', which " +
                             "this project does not have. It will be built on Default and the " +
                             "wizard will fall through it.", this);
            return 0;
        }

        public class Growth
        {
            public GameObject wall;
            public Transform body;
            public bool fromLedge;
            public int facing = 1;
            public float age;
        }
    }
}
