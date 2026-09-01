using System.Collections.Generic;
using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.World
{
    // A trail of the wizard, stamped behind them and left to fade. Built the same way WindZone2D
    // builds its streaks: throwaway SpriteRenderers under one container at the scene root, so
    // nothing inherits a scale or a flip from whatever it was copied off.
    public class Afterimages : MonoBehaviour
    {
        readonly List<Ghost> ghosts = new List<Ghost>();

        SpriteRenderer source;
        float tint_r, tint_g, tint_b, tint_a;
        float every;
        float life;
        int order;
        float due;
        bool running;

        public static Afterimages For(SpriteRenderer art, Color tint, float every, float life,
            int sortingOrder)
        {
            if (art == null)
                return null;

            var go = new GameObject("Afterimages");
            var trail = go.AddComponent<Afterimages>();

            trail.source = art;
            trail.tint_r = tint.r;
            trail.tint_g = tint.g;
            trail.tint_b = tint.b;
            trail.tint_a = tint.a;
            trail.every = Mathf.Max(0.01f, every);
            trail.life = Mathf.Max(0.05f, life);
            trail.order = sortingOrder;

            return trail;
        }

        public void Run(bool on) => running = on;

        // Stop stamping but let what is already out fade, rather than blinking the trail away.
        public void Retire()
        {
            running = false;
            source = null;
        }

        void Update()
        {
            if (running && source != null && source.sprite != null && Time.time >= due)
            {
                due = Time.time + every;
                Stamp();
            }

            for (int i = ghosts.Count - 1; i >= 0; i--)
            {
                Ghost ghost = ghosts[i];
                ghost.left -= Time.deltaTime;

                if (ghost.left <= 0f || ghost.art == null)
                {
                    if (ghost.art != null)
                        Destroy(ghost.art.gameObject);

                    ghosts.RemoveAt(i);
                    continue;
                }

                Color faded = ghost.art.color;
                faded.a = tint_a * (ghost.left / life);
                ghost.art.color = faded;
            }

            if (!running && ghosts.Count == 0 && source == null)
                Destroy(gameObject);
        }

        void Stamp()
        {
            var go = new GameObject("Ghost");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(source.transform.position,
                source.transform.rotation);
            go.transform.localScale = source.transform.lossyScale;

            var art = go.AddComponent<SpriteRenderer>();
            art.sprite = source.sprite;
            art.flipX = source.flipX;
            art.flipY = source.flipY;
            art.sortingLayerID = source.sortingLayerID;
            art.sortingOrder = order;
            art.color = new Color(tint_r, tint_g, tint_b, tint_a);

            ghosts.Add(new Ghost { art = art, left = life });
        }

        class Ghost
        {
            public SpriteRenderer art;
            public float left;
        }
    }
}
