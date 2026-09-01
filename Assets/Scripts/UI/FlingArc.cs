using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.UI
{
    // The dotted line for a charged fling. Ported from the jump-test branch, with one change:
    // it is PUSHED a path rather than pulling one. It owns no physics and decides nothing, so
    // the picture cannot drift away from the shot it is predicting.
    //
    // The arc arrives sampled by TIME, which bunches points around the apex where the wizard is
    // slowest. Dots are re-spaced by DISTANCE on the way out, which is what makes it read as an
    // even dotted line rather than a comet.
    public class FlingArc : MonoBehaviour
    {
        readonly List<SpriteRenderer> dots = new List<SpriteRenderer>();

        SpriteRenderer marker;

        Sprite dotSprite;
        float spacing = 0.32f;
        float dotSize = 0.13f;
        int maxDots = 60;
        float taper = 0.45f;
        Color safe;
        Color danger;
        int order;

        public static FlingArc Make(Sprite art, float spacing, float dotSize, int maxDots,
            float taper, Color safe, Color danger, int sortingOrder)
        {
            var go = new GameObject("Fling Arc");
            var arc = go.AddComponent<FlingArc>();

            arc.dotSprite = art;
            arc.spacing = Mathf.Max(0.05f, spacing);
            arc.dotSize = Mathf.Max(0.01f, dotSize);
            arc.maxDots = Mathf.Clamp(maxDots, 4, 200);
            arc.taper = Mathf.Clamp01(taper);
            arc.safe = safe;
            arc.danger = danger;
            arc.order = sortingOrder;

            arc.marker = arc.MakeDot("Landing");

            return arc;
        }

        public void Hide()
        {
            for (int i = 0; i < dots.Count; i++)
                dots[i].enabled = false;

            if (marker != null)
                marker.enabled = false;
        }

        public void Show(List<Vector2> path, PlayerLogic.Movement.ArcEnd end, float charge)
        {
            if (path == null || path.Count < 2)
            {
                Hide();
                return;
            }

            Color tint = end.Hazard ? danger : safe;
            tint.a *= Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(charge));

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
                for (float along = spacing - carried; along <= length && used < maxDots;
                     along += spacing)
                {
                    Vector2 at = Vector2.Lerp(from, to, along / length);
                    float through = used / (float)maxDots;

                    Place(Dot(used), at, dotSize * Mathf.Lerp(1f, 1f - taper, through), tint);
                    used++;
                }

                carried = (carried + length) % spacing;
            }

            for (int i = used; i < dots.Count; i++)
                dots[i].enabled = false;

            marker.enabled = end.Stopped;

            if (end.Stopped)
                Place(marker, end.Point, dotSize * 2.6f, end.Hazard ? danger : safe);
        }

        SpriteRenderer Dot(int index)
        {
            while (dots.Count <= index)
                dots.Add(MakeDot($"Dot {dots.Count + 1}"));

            return dots[index];
        }

        static void Place(SpriteRenderer dot, Vector2 at, float size, Color tint)
        {
            dot.enabled = true;
            dot.color = tint;
            dot.transform.position = at;
            dot.transform.localScale = Vector3.one * size;
        }

        SpriteRenderer MakeDot(string named)
        {
            var host = new GameObject(named);
            host.transform.SetParent(transform, false);

            var art = host.AddComponent<SpriteRenderer>();
            art.sprite = dotSprite != null ? dotSprite : Placeholder.Box;
            art.sortingOrder = order;
            art.enabled = false;

            return art;
        }
    }
}
