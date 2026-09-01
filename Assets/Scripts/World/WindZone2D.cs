using System;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public class WindZone2D : Hazard
    {
        const float Epsilon = 0.0001f;

        // Gizmo shape, all as fractions of the zone or of the push, so an arrow is readable at
        // any size of zone and any strength of wind.
        const float ArrowLengthPerBox = 0.25f;
        const float ArrowLengthOfZone = 0.4f;
        const float ArrowSpreadOfZone = 0.3f;
        const float ArrowHeadLength = 0.4f;
        const float ArrowHeadWidth = 0.25f;
        const int ArrowRows = 1;

        const float UnselectedGizmo = 0.5f;
        const float SelectedGizmo = 1f;

        static readonly Color ZoneColour = new Color(0.55f, 0.85f, 1f, 0.35f);
        static readonly Color ArrowColour = new Color(0.55f, 0.85f, 1f, 0.9f);

        [Header("Wind")]
        [Tooltip("Push in boxes per second. (-6,0) is a hard leftward gale, (0,4) an updraught. " +
                 "Compare against a run of 6 to judge how hard it fights the player.")]
        public Vector2 push = new Vector2(-4f, 0f);

        [Tooltip("How quickly the wind takes hold once you step in, in boxes per second squared.")]
        [Min(0f)] public float rampup = 20f;

        [Tooltip("How much of it is felt with both feet on the ground. 0 means it only pushes " +
                 "you while you are in the air.")]
        [Range(0f, 1f)] public float groundScale = 0.35f;

        [Header("Haze")]
        [Tooltip("The flat rectangle showing where the wind reaches. Empty uses the first sprite " +
                 "found underneath. It is resized to match the collider, so stretching the zone " +
                 "is the only thing you have to do.")]
        public SpriteRenderer haze;

        [Tooltip("Colour of that rectangle. Keep the alpha low - it covers whatever is behind it.")]
        public Color hazeTint = new Color(0.62f, 0.82f, 0.95f, 0.14f);

        [Tooltip("Resize the haze to match the collider whenever the zone changes, so stretching " +
                 "the zone is the only thing you have to do. Turn it OFF the moment you want to " +
                 "size or place that art yourself - it will stop touching your transform.")]
        public bool fitHazeToZone = true;

        [Header("Streaks")]
        [Tooltip("Drifting streaks, so the wind is something you can see moving rather than a " +
                 "tinted box you have to walk into to discover. They are built when the level " +
                 "starts, so the editor shows the arrows instead.")]
        public bool showStreaks = true;

        [Tooltip("How many. A dozen reads as wind without turning the screen into soup.")]
        [Range(0, 60)] public int streaks = 16;

        [Tooltip("Art for one streak. Empty draws a thin bar.")]
        public Sprite streakArt;

        [Tooltip("Colour of a streak at its brightest. They fade in and out as they cross, so " +
                 "none of them pops into being.")]
        public Color streakTint = new Color(0.85f, 0.95f, 1f, 0.55f);

        [Tooltip("Size of one streak in boxes, before it is turned to face the wind. A mage is " +
                 "one box, so this is a short scratch of white.")]
        public Vector2 streakSize = new Vector2(0.9f, 0.05f);

        [Tooltip("How fast they travel against the push itself. Above 1 they outrun the wizard, " +
                 "which reads as faster than the zone really is.")]
        [Min(0f)] public float streakSpeed = 1.4f;

        [Tooltip("Sorting order. Below the wizard so they blow past behind them.")]
        public int sortingOrder = -2;

        [Header("Editor")]
        [Tooltip("Draw the arrows in the scene view without having to select the zone first, " +
                 "which is what you want while laying a level out.")]
        public bool alwaysShowArrows = true;

        [NonSerialized] Collider2D area;
        [NonSerialized] Transform gust;
        [NonSerialized] Transform[] blown = Array.Empty<Transform>();
        [NonSerialized] SpriteRenderer[] blownArt = Array.Empty<SpriteRenderer>();

        protected override bool Continuous => true;

        public Vector2 Drift => push * streakSpeed * Haste.WorldScale;

        void Reset()
        {
            rearmDelay = 0f;
            damage = 0;
            affectsRagdolled = true;
        }

        void OnValidate() => FitHaze();

        protected override void Awake()
        {
            base.Awake();

            area = GetComponent<Collider2D>();

            if (haze == null)
                haze = GetComponentInChildren<SpriteRenderer>();

            if (haze != null && haze.sprite == null)
                haze.sprite = Placeholder.Box;

            FitHaze();
            BuildStreaks();
        }

        protected override void Affect(PlayerLogic wizard) { }

        protected override void OnPlayerInside(PlayerCharacter wizard, float fixedDeltaTime)
        {
            if (!Allowed(wizard))
                return;

            // Scaled by Haste: a gale you cannot walk into is exactly what the spell is for,
            // and the streaks below slow with it so the two never disagree.
            wizard.Logic.Push(push * Haste.WorldScale, rampup, groundScale);
        }

        void Update()
        {
            if (blown.Length == 0 || area == null)
                return;

            Vector2 drift = Drift;

            if (drift.sqrMagnitude < Epsilon)
                return;

            Bounds zone = area.bounds;
            Vector2 step = drift * Time.deltaTime;

            for (int i = 0; i < blown.Length; i++)
            {
                Vector2 point = (Vector2)blown[i].position + step;
                point = Wrap(point, zone, drift);

                blown[i].position = new Vector3(point.x, point.y, transform.position.z);
                blownArt[i].color = Fade(point, zone, drift);
            }
        }

        static Vector2 Wrap(Vector2 point, Bounds zone, Vector2 drift)
        {
            if (drift.x > 0f && point.x > zone.max.x) point.x = zone.min.x;
            if (drift.x < 0f && point.x < zone.min.x) point.x = zone.max.x;
            if (drift.y > 0f && point.y > zone.max.y) point.y = zone.min.y;
            if (drift.y < 0f && point.y < zone.min.y) point.y = zone.max.y;

            return point;
        }

        Color Fade(Vector2 point, Bounds zone, Vector2 drift)
        {
            // Fade in from the edge it enters by and out at the one it leaves by, so nothing
            // blinks into existence mid-air. Measured along whichever way the wind mostly blows.
            bool sideways = Mathf.Abs(drift.x) >= Mathf.Abs(drift.y);

            float low = sideways ? zone.min.x : zone.min.y;
            float high = sideways ? zone.max.x : zone.max.y;
            float here = sideways ? point.x : point.y;

            float span = high - low;
            float along = span <= Epsilon ? 0.5f : Mathf.Clamp01((here - low) / span);

            Color tint = streakTint;
            tint.a *= Mathf.Sin(along * Mathf.PI);
            return tint;
        }

        void FitHaze()
        {
            if (haze == null)
                haze = GetComponentInChildren<SpriteRenderer>();

            // No art is made here: OnValidate calls this, and building a texture inside a
            // serialisation callback is how you earn a console full of warnings. Awake fills
            // in a stand-in before the first call.
            if (haze == null || haze.sprite == null)
                return;

            haze.color = hazeTint;
            haze.sortingOrder = sortingOrder - 1;

            var shape = GetComponent<BoxCollider2D>();

            if (shape == null)
                return;

            Vector2 tall = haze.sprite.bounds.size;

            if (tall.x <= Epsilon || tall.y <= Epsilon)
                return;

            if (!fitHazeToZone)
                return;

            haze.transform.localPosition = shape.offset;
            haze.transform.localScale = new Vector3(shape.size.x / tall.x, shape.size.y / tall.y, 1f);
        }

        void BuildStreaks()
        {
            if (!showStreaks || streaks <= 0 || area == null)
                return;

            Bounds zone = area.bounds;

            // Kept at the scene root under one container rather than parented to the zone: a
            // stretched wind zone would otherwise stretch every streak with it, and these are
            // driven in world space anyway.
            gust = new GameObject($"{name} Streaks").transform;

            blown = new Transform[streaks];
            blownArt = new SpriteRenderer[streaks];

            float turn = Mathf.Atan2(push.y, push.x) * Mathf.Rad2Deg;

            for (int i = 0; i < streaks; i++)
            {
                var streak = new GameObject($"Streak {i + 1}");
                streak.transform.SetParent(gust, false);

                streak.transform.position = new Vector3(
                    UnityEngine.Random.Range(zone.min.x, zone.max.x),
                    UnityEngine.Random.Range(zone.min.y, zone.max.y),
                    transform.position.z);

                streak.transform.rotation = Quaternion.Euler(0f, 0f, turn);
                streak.transform.localScale = Vector3.one;

                var art = streak.AddComponent<SpriteRenderer>();
                art.sprite = streakArt != null ? streakArt : Placeholder.Box;
                art.color = streakTint;
                art.sortingOrder = sortingOrder;

                Vector2 unit = art.sprite.bounds.size;

                if (unit.x > Epsilon && unit.y > Epsilon)
                    streak.transform.localScale =
                        new Vector3(streakSize.x / unit.x, streakSize.y / unit.y, 1f);

                blown[i] = streak.transform;
                blownArt[i] = art;
            }
        }

        void OnDestroy()
        {
            if (gust != null)
                Destroy(gust.gameObject);
        }

        void OnDrawGizmos()
        {
            if (alwaysShowArrows)
                DrawArrows(UnselectedGizmo);
        }

        void OnDrawGizmosSelected() => DrawArrows(SelectedGizmo);

        void DrawArrows(float strength)
        {
            var shape = GetComponent<BoxCollider2D>();

            if (shape == null || push.sqrMagnitude < Epsilon)
                return;

            Bounds zone = shape.bounds;

            Gizmos.color = Faded(ZoneColour, strength);
            Gizmos.DrawWireCube(zone.center, zone.size);

            Vector2 way = push.normalized;
            Vector2 across = new Vector2(-way.y, way.x);

            // Arrow length reads the strength: a run is 6 boxes a second, so a gale you cannot
            // walk out of draws longer than the wizard is tall.
            float narrow = Mathf.Min(zone.size.x, zone.size.y);

            float length = Mathf.Min(push.magnitude * ArrowLengthPerBox,
                                     narrow * ArrowLengthOfZone);
            float spread = narrow * ArrowSpreadOfZone;

            Gizmos.color = Faded(ArrowColour, strength);

            for (int row = -ArrowRows; row <= ArrowRows; row++)
            {
                Vector2 middle = (Vector2)zone.center + across * (spread * row);
                Vector2 tip = middle + way * length;

                Gizmos.DrawLine(middle - way * length, tip);
                Vector2 barb = tip - way * (length * ArrowHeadLength);
                Vector2 flare = across * (length * ArrowHeadWidth);

                Gizmos.DrawLine(tip, barb + flare);
                Gizmos.DrawLine(tip, barb - flare);
            }
        }

        static Color Faded(Color colour, float strength)
        {
            colour.a *= strength;
            return colour;
        }
    }
}
