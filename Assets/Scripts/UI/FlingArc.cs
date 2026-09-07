using System.Collections.Generic;
using FallingWizard.Core;
using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.UI
{
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
