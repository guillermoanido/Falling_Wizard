using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        // Where a fling would land, integrated against the same model the real flight uses.
        public partial class Movement
        {
            // Adapted from the jump-test branch, and the adaptations matter more than the port.
            // It integrates the SAME model the real flight uses - fall gravity, the terminal
            // clamp, the floatiness from Modifiers - so the picture cannot drift from the physics.
            //
            // Three things this game needs that a plain ballistic arc gets wrong:
            //
            //  * HAZARDS DO NOT STOP YOU. Every hazard here is a trigger you pass straight
            //    through, so an arc that ended at the first slime would hide where you actually
            //    land. It flies on and reports that it crossed one.
            //  * WIND PUSHES YOU MID-FLIGHT. wind.y is added outside Run (FixedTick:719), so it
            //    reaches a wizard whose steering is locked. wind.x is not - Run early-returns on
            //    lockout - so only the vertical component belongs in here.
            //  * THE FLIGHT HAS TO BE LOCKED. Run rewrites linearVelocityX every step, dragging
            //    it to the stick at airControl x groundFriction. Unlocked, a 12 b/s fling is
            //    spent inside half a second and the arc is a lie. ArcEnd.Seconds is how long the
            //    caster must lock control for the drawing to stay true.
            //
            // Sampled by TIME, so points bunch around the apex where the wizard is slowest.
            // Whatever draws it re-spaces by DISTANCE, which is what makes it read as an even
            // dotted line rather than a comet.
            public int PredictArc(Vector2 launch, Modifiers stats, in ArcSettings look,
                List<Vector2> into, out ArcEnd end)
            {
                into.Clear();
                end = default;

                if (body == null)
                    return 0;

                arcFilter.layerMask = look.Layers;

                float floatiness = stats != null ? stats.FallSpeedMultiplier : 1f;
                float terminal = maxFallSpeed * floatiness;
                float step = Mathf.Max(0.005f, look.Step);
                float updraught = wind.y;

                // Foot height, LIFTED CLEAR of the floor. The arc is a promise about where the
                // feet land, so it starts there - but this project has
                // Physics2D.queriesStartInColliders on, so a ray beginning flush with the ground
                // the wizard is stood on reports a hit at distance zero and the whole arc
                // collapses to a single point beside them.
                var point = new Vector2(body.position.x, FeetY + ArcClearance);
                Vector2 velocity = launch;

                float travelled = 0f;
                float flown = 0f;

                bool crossed = false;
                Collider2D met = null;

                into.Add(point);

                for (int i = 0; i < look.Steps && travelled < look.Distance; i++)
                {
                    float gravity = velocity.y < 0f
                        ? BaseGravity * fallGravityMultiplier * floatiness
                        : BaseGravity;

                    velocity.y += (updraught - gravity) * step;

                    if (velocity.y < -terminal)
                        velocity.y = -terminal;

                    Vector2 next = point + velocity * step;
                    Vector2 leg = next - point;
                    float length = leg.magnitude;

                    if (length > Mathf.Epsilon)
                    {
                        int found = Physics2D.Raycast(point, leg / length, arcFilter, Rays, length);

                        // Sorted by distance, so the first SOLID one is where the flight really
                        // ends. Everything before it is scenery you pass through.
                        for (int hit = 0; hit < found; hit++)
                        {
                            Collider2D what = Rays[hit].collider;

                            if ((groundLayers.value & (1 << what.gameObject.layer)) != 0)
                            {
                                end = new ArcEnd
                                {
                                    Point = Rays[hit].point,
                                    Stopped = true,
                                    Hazard = crossed,
                                    What = crossed ? met : what,
                                    Seconds = flown + step,
                                };

                                into.Add(end.Point);
                                return into.Count;
                            }

                            if (crossed)
                                continue;

                            crossed = true;
                            met = what;
                        }
                    }

                    travelled += length;
                    flown += step;
                    point = next;
                    into.Add(point);
                }

                end = new ArcEnd { Point = point, Hazard = crossed, What = met, Seconds = flown };
                return into.Count;
            }

            public struct ArcSettings
            {
                public LayerMask Layers;
                public float Step;
                public int Steps;
                public float Distance;
            }

            // What the arc ran into, if anything.
            public struct ArcEnd
            {
                public Vector2 Point;
                public bool Stopped;

                // Something that will change where you end up was crossed on the way. The flight
                // does NOT stop there - hazards in this game are things you pass through - so
                // this is a warning about the arc, not the end of it.
                public bool Hazard;
                public Collider2D What;

                // How long the flight takes. Lock control for at least this long or the drawing
                // is a lie, because Run drags horizontal speed back to the stick every step.
                public float Seconds;
            }
        }
    }
}
