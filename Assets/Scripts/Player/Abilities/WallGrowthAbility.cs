using FallingWizard.Core;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Wall Growth", fileName = "Wall Growth")]
    public class WallGrowthAbility : Ability
    {
        // A block of literally no size has a degenerate collider, which Unity complains about
        // every frame it exists.
        const float MinGrown = 0.01f;

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

        [Tooltip("Size of the finished block in boxes. One by one is a single tile, which is " +
                 "what the grid is built out of and what snapping expects.")]
        public Vector2 size = Vector2.one;

        [Header("Where It Grows")]
        [Tooltip("How many tiles ahead it will look for somewhere to put the block. 1 is the " +
                 "tile you are about to step into. It takes the first free one it finds.")]
        [Range(1, 4)] public int reachInTiles = 2;

        [Tooltip("Tiles above your own feet, and it is usually NEGATIVE. -1 is one below - a " +
                 "step down into the drop, which is what you want in a game about going down: " +
                 "walk to a lip, put a stone under it, step off onto your own staircase. 0 is " +
                 "level with the floor, which bridges a gap instead. Positive is a step up. " +
                 "Below zero this becomes a LEDGE spell: on flat ground every tile at that " +
                 "height is already floor, so it refuses and says so.")]
        [Range(-3, 2)] public int liftInTiles = -1;

        [Header("Growing")]
        [Tooltip("Seconds it takes to reach full size. Keep it SHORT: the collider follows " +
                 "the scale, so this is also how long the cell reads as half-empty to the next " +
                 "cast and half-solid to a wizard stood on it.")]
        [Min(0f)] public float growTime = 0.05f;

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
            FindCell(wizard, out _);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and need both feet under you";

            if (wizard.spellbook.StateOf<Growth>(this).wall != null)
                return "your last block is still standing";

            if (FindCell(wizard, out _))
                return null;

            // Below foot level the usual reason is that there is no ledge, not that the place is
            // crowded - and "every tile is filled" while stood in the open reads as a bug.
            return liftInTiles < 0
                ? $"there is solid ground under every tile within {reachInTiles} ahead of you - " +
                  "this wants a ledge to build under"
                : $"every tile within {reachInTiles} ahead of you is already filled";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!FindCell(wizard, out Vector2Int cell))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            growth.wall = Build(TileGrid.CentreOf(cell));
            growth.body = Body(growth.wall);
            growth.age = 0f;

            if (growth.body != null)
                growth.body.localPosition = Vector3.zero;

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

        bool FindCell(PlayerLogic wizard, out Vector2Int cell)
        {
            PlayerLogic.Movement walk = wizard.movement;
            Vector2Int stood = TileGrid.StandingCell(walk);

            // Walking outward one tile at a time finds the LEDGE on its own: standing back
            // from a lip, the near tiles at this height are still floor and get skipped, and the
            // first free one is the far side of the edge. No edge-finding maths, no continuous
            // edgeX to snap, and it behaves the same whether you are at the lip or a step back.
            for (int step = 1; step <= reachInTiles; step++)
            {
                cell = new Vector2Int(stood.x + walk.Facing * step, stood.y + liftInTiles);

                // No floor test, unlike Telekinesis. Putting a block where there ISN'T one is
                // the entire spell - a block grown over solid ground would be grown over nothing.
                if (TileGrid.IsFree(cell, walk.groundLayers))
                    return true;
            }

            cell = default;
            return false;
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

            // Grown about its own middle, so it is centred on its cell for EVERY frame of the
            // growth rather than only at the end. There is no anchor to keep an axis flush
            // against any more.
            growth.wall.transform.localScale = new Vector3(
                Mathf.Max(MinGrown, size.x * grown),
                Mathf.Max(MinGrown, size.y * grown), 1f);
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
            public float age;
        }
    }
}
