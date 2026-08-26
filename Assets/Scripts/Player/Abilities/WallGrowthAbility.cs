using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Wall Growth", fileName = "Wall Growth")]
    public class WallGrowthAbility : Ability
    {
        [Header("Wall")]
        [Tooltip("Spawned as the wall. It should stand one box wide and one box tall with its " +
                 "feet on its own origin, and carry a BoxCollider2D. Leave empty and a plain " +
                 "block is built from the sprite below.")]
        public GameObject wallPrefab;

        [Tooltip("Used when there is no prefab. A 32x32 sprite at 32 pixels per unit is one box. " +
                 "Empty draws a flat colour.")]
        public Sprite wallArt;

        [Tooltip("Tint, and the colour of the plain block when there is no art at all.")]
        public Color tint = new Color(0.42f, 0.62f, 0.38f);

        [Tooltip("Size of the finished wall in boxes - a mage is one box wide and one tall.")]
        public Vector2 size = new Vector2(1f, 3f);

        [Tooltip("How far in front of the wizard it sprouts, in boxes. Roughly one box clears " +
                 "their own hitbox.")]
        [Min(0f)] public float distanceAhead = 1.1f;

        [Tooltip("Seconds it takes to reach full height. It is solid the whole way up, so a slow " +
                 "growth can carry you with it.")]
        [Min(0f)] public float growTime = 0.3f;

        [Tooltip("How far below the wizard's feet to hunt for a floor to root it in, in boxes. " +
                 "Casting over a drop with nothing this far down fails rather than hanging a " +
                 "wall in the air.")]
        [Min(0f)] public float rootDepth = 2f;

        [Header("Physics")]
        [Tooltip("Layer the wall lands on. It has to be one the wizard's ground check looks at, " +
                 "or they will walk straight through it.")]
        public string groundLayer = "Ground";

        [Tooltip("Sorting order of the built block. Above the tilemap, below the wizard.")]
        public int sortingOrder = -1;

        // The floor hunt starts half a box above the feet so a wall can be rooted in the step
        // the wizard is already standing on, not only in the one below them.
        const float RayLift = 0.5f;

        // Half a box up with unit size, so scaling the root grows the wall upward off its own
        // footing instead of stretching it around its middle.
        const float BodyRise = 0.5f;

        // A wall of literally no height would have a degenerate collider, which Unity complains
        // about every frame it exists.
        const float MinGrown = 0.01f;

        static readonly RaycastHit2D[] Footings = new RaycastHit2D[1];

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal &&
            wizard.spellbook.StateOf<Growth>(this).wall == null &&
            FindFooting(wizard, out _);

        public override bool OnCast(PlayerLogic wizard)
        {
            if (!FindFooting(wizard, out Vector2 footing))
                return false;

            Growth growth = wizard.spellbook.StateOf<Growth>(this);

            growth.wall = Build(footing);
            growth.age = 0f;

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

        bool FindFooting(PlayerLogic wizard, out Vector2 footing)
        {
            PlayerLogic.Movement walk = wizard.movement;

            float x = walk.Position.x + walk.Facing * distanceAhead;
            float top = walk.FeetY + RayLift;

            footing = new Vector2(x, walk.FeetY);

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

            footing = Footings[0].point;
            return true;
        }

        GameObject Build(Vector2 footing)
        {
            if (wallPrefab != null)
            {
                GameObject grown = Instantiate(wallPrefab, footing, Quaternion.identity);
                grown.name = displayName;
                return grown;
            }

            var root = new GameObject(displayName);
            root.transform.position = footing;
            root.layer = Layer();

            var body = new GameObject("Body");
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, BodyRise, 0f);
            body.layer = root.layer;

            var art = body.AddComponent<SpriteRenderer>();
            art.sprite = wallArt != null ? wallArt : Placeholder.Box;
            art.color = tint;
            art.sortingOrder = sortingOrder;
            art.drawMode = SpriteDrawMode.Simple;

            body.AddComponent<BoxCollider2D>();

            return root;
        }

        void Shape(Growth growth, float grown)
        {
            if (growth.wall == null)
                return;

            if (growth.body == null)
                growth.body = growth.wall.transform;

            growth.body.localScale = new Vector3(size.x, Mathf.Max(MinGrown, size.y * grown), 1f);
        }

        int Layer()
        {
            int layer = LayerMask.NameToLayer(groundLayer);

            if (layer >= 0)
                return layer;

            Debug.LogWarning($"'{name}' wants to build its wall on a layer called " +
                             $"'{groundLayer}', which this project does not have. It will be " +
                             "built on Default and the wizard will fall through it.", this);
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
