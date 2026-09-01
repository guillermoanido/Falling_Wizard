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
        [Tooltip("Spawned as the block. Its FIRST CHILD carries the sprite and the collider, " +
                 "ONE BOX SQUARE and centred on itself - the spell scales the root, and works out " +
                 "where to stand the root from that one box. Leave empty and a plain one is " +
                 "built from the sprite below.")]
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
        [Tooltip("How many tiles ahead to hunt for the lip of a drop. The block is placed " +
                 "relative to THAT, not to where you happen to be stood, so it lands in the same " +
                 "spot whether you walked right up to the edge or stopped a tile short.")]
        [Range(1, 6)] public int reachInTiles = 3;

        [Tooltip("Tiles past the lip. 0 is the first empty column - the block hugs the ledge. " +
                 "1 leaves a one-tile gap you have to jump.")]
        [Range(0, 3)] public int outFromEdge = 0;

        [Tooltip("Which ROW, counted from the one your body is in. -1 is the floor row, so the " +
                 "block comes out level with the ground you are stood on and you can walk " +
                 "straight onto it. -2 is one below that, a step down into the drop. 0 is knee " +
                 "height, which is almost never what you want.")]
        [Range(-4, 1)] public int liftInTiles = -1;

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
            wizard.movement.IsGrounded &&
            wizard.spellbook.StateOf<Growth>(this).wall == null &&
            FindCell(wizard, out _);

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and need both feet under you";

            // The line above has always promised this. Without the check the spell also grew a
            // block under a FALLING wizard, one step at a time, which is a flying spell.
            if (!wizard.movement.IsGrounded)
                return "you are in the air - this grows out of the ground you are stood on";

            if (wizard.spellbook.StateOf<Growth>(this).wall != null)
                return "your last block is still standing";

            if (FindCell(wizard, out _))
                return null;

            return $"no lip within {reachInTiles} tiles ahead of you, or the tile past it is " +
                   "already filled - this wants an edge to build off";
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!FindCell(wizard, out Vector2Int cell))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            growth.wall = Build(new Vector2(
                TileGrid.CentreOf(cell).x,
                TileGrid.FloorOf(cell) + size.y * 0.5f));
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
            int floorRow = stood.y - 1;

            // Find the LIP first - the nearest column ahead whose floor has run out - and place
            // relative to that. Placing relative to the wizard instead was the bug: standing at
            // the very edge put the block one tile out, standing a tile back put it two, and
            // which of those you got depended on exactly where you stopped walking.
            for (int step = 0; step <= reachInTiles; step++)
            {
                int x = stood.x + walk.Facing * step;

                if (TileGrid.IsSolid(new Vector2Int(x, floorRow), walk.groundLayers))
                    continue;

                cell = new Vector2Int(x + walk.Facing * outFromEdge, stood.y + liftInTiles);

                // No floor test, unlike Telekinesis. Putting a block where there ISN'T one is
                // the entire spell - a block grown over solid ground would be grown over nothing.
                return TileGrid.IsFree(cell, walk.groundLayers);
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

            // Grown about its own middle. The root is already stood on the floor line by
            // OnCast, so the finished block rests exactly where the tilemap's own tiles rest -
            // the few frames of growing under that are three fiftieths of a second.
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
