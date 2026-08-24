using System.Collections.Generic;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.UI
{
    // The dotted line for an aimed jump. It asks PlayerLogic where the shot would go and draws it,
    // and that is the whole division of labour: this file owns no physics and decides nothing, so
    // the picture cannot drift away from the jump it is predicting.
    //
    // The arc it gets back is sampled by TIME, which bunches points up around the apex where the
    // wizard is moving slowest. Dots are re-spaced by DISTANCE on the way out, which is what makes
    // it read as an even dotted line rather than a comet.
    public class JumpArc : MonoBehaviour
    {
        [Header("Dots")]
        [Tooltip("Leave empty for a plain square. Any sprite works - a ring or a chevron reads " +
                 "better than a dot once you have art.")]
        public Sprite dotSprite;

        [Tooltip("Gap between dots, in boxes.")]
        [Min(0.05f)] public float spacing = 0.32f;

        [Tooltip("Size of a dot, in boxes.")]
        [Min(0.01f)] public float dotSize = 0.13f;

        [Tooltip("Most dots to draw. The arc is cut short rather than crowded.")]
        [Range(4, 200)] public int maxDots = 60;

        [Tooltip("Dots shrink towards the far end, so the near ones read as the confident part.")]
        [Range(0f, 1f)] public float taper = 0.45f;

        [Header("Colour")]
        [Tooltip("A shot that lands on ordinary ground.")]
        public Color safe = new Color(0.95f, 0.93f, 0.75f, 0.85f);

        [Tooltip("A shot that ends on a hazard. The arc stops there, because a slime or a rock " +
                 "changes where you go and every dot past it would be a guess.")]
        public Color danger = new Color(0.95f, 0.35f, 0.30f, 0.9f);

        [Tooltip("Tints the whole arc towards this as the shot winds up, so power is readable " +
                 "without a bar.")]
        public Color charged = new Color(1f, 1f, 1f, 1f);

        [Header("Landing")]
        [Tooltip("Marker drawn where the shot ends. Empty uses the dot sprite.")]
        public Sprite markerSprite;

        [Min(0f)] public float markerSize = 0.34f;

        [Header("Sorting")]
        [Tooltip("Above the level so the arc is never drawn inside a wall.")]
        public int sortingOrder = 20;

        static Sprite blank;

        readonly List<Vector2> path = new List<Vector2>(256);
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();

        SpriteRenderer marker;

        void Awake() => marker = MakeDot("Landing");

        void LateUpdate()
        {
            PlayerCharacter wizard = PlayerCharacter.Instance;

            if (wizard == null)
            {
                Hide(0);
                return;
            }

            int count = wizard.Logic.PredictJumpArc(path, out PlayerLogic.Movement.ArcEnd end);

            if (count < 2)
            {
                Hide(0);
                return;
            }

            Draw(wizard.Logic.movement.aim, end);
        }

        void Draw(PlayerLogic.Aim aim, PlayerLogic.Movement.ArcEnd end)
        {
            Color tint = Color.Lerp(end.Hazard ? danger : safe, charged, aim.Charge);

            int used = 0;
            float carried = 0f;

            for (int leg = 1; leg < path.Count && used < maxDots; leg++)
            {
                Vector2 from = path[leg - 1];
                Vector2 to = path[leg];

                float length = Vector2.Distance(from, to);
                if (length <= Mathf.Epsilon)
                    continue;

                // Walk this leg placing a dot every `spacing`, carrying the remainder into the
                // next one so the spacing never resets at a sample boundary.
                for (float along = spacing - carried; along <= length && used < maxDots; along += spacing)
                {
                    Vector2 at = Vector2.Lerp(from, to, along / length);
                    float through = used / (float)maxDots;

                    Place(Dot(used), at, dotSize * Mathf.Lerp(1f, 1f - taper, through), tint);
                    used++;
                }

                carried = (carried + length) % spacing;
            }

            Hide(used);

            marker.enabled = end.Stopped;

            if (end.Stopped)
                Place(marker, end.Point, markerSize, end.Hazard ? danger : safe);
        }

        SpriteRenderer Dot(int index)
        {
            while (dots.Count <= index)
                dots.Add(MakeDot($"Dot {dots.Count + 1}"));

            return dots[index];
        }

        void Place(SpriteRenderer dot, Vector2 at, float size, Color tint)
        {
            dot.enabled = true;
            dot.color = tint;
            dot.transform.position = at;
            dot.transform.localScale = Vector3.one * size;
        }

        void Hide(int from)
        {
            for (int i = from; i < dots.Count; i++)
                dots[i].enabled = false;

            if (from == 0 && marker != null)
                marker.enabled = false;
        }

        SpriteRenderer MakeDot(string name)
        {
            var host = new GameObject(name);
            host.transform.SetParent(transform, false);

            var art = host.AddComponent<SpriteRenderer>();
            art.sprite = name == "Landing" && markerSprite != null ? markerSprite
                       : dotSprite != null ? dotSprite
                       : Blank;
            art.sortingOrder = sortingOrder;
            art.enabled = false;

            return art;
        }

        // A plain white square to draw with until there is art. Made rather than borrowed from
        // Unity's built-in UI skin, which URP projects do not ship.
        static Sprite Blank
        {
            get
            {
                if (blank != null)
                    return blank;

                var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    name = "Falling Wizard Arc Dot",
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };

                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, 255, 255, 255);

                texture.SetPixels32(pixels);
                texture.Apply();

                blank = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
                blank.name = "Falling Wizard Arc Dot";
                blank.hideFlags = HideFlags.HideAndDontSave;

                return blank;
            }
        }
    }
}
