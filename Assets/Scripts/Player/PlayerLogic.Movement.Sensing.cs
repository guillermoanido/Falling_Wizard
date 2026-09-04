using System.Collections.Generic;
using UnityEngine;

namespace FallingWizard.Player
{
    public partial class PlayerLogic
    {
        // What is under the wizard and what is in front of them. Every method in this file
        // answers a question and moves nothing - which is what makes them safe to ask twice, and
        // safe for a spell to ask before deciding whether its button should even light up.
        public partial class Movement
        {
            public bool TryFindLedgeEdge(out float edgeX)
            {
                edgeX = ProbeOrigin.x;

                if (!IsGrounded || !IsAtEdge)
                    return false;

                float footing = 0f;
                float air = ledgeCheckAhead;

                if (!HasGroundAt(footing))
                {
                    footing = -groundCheckSize.x * 0.5f;

                    if (!HasGroundAt(footing))
                        return false;
                }

                for (int step = 0; step < edgeSearchSteps; step++)
                {
                    float middle = (footing + air) * 0.5f;

                    if (HasGroundAt(middle))
                        footing = middle;
                    else
                        air = middle;
                }

                // Prove it. The search assumes there is a gap somewhere between footing and air
                // and closes on the boundary, but it never checks that `air` IS air - and the
                // grounded flags it started from are a physics step old, so after dropping off
                // the staff they can still say "at a ledge" while the wizard stands on solid
                // floor. Left unchecked it hands back a lip in the middle of the ground, the
                // plant then fails on a reach of nothing, and the press dies silently.
                if (HasGroundAt(air))
                    return false;

                edgeX = ProbeOrigin.x + Facing * air;
                return true;
            }

            // Where the feet are, what they are stood on, and how far the wizard fell to get
            // here. Run from FixedTick, and separately from the ragdoll - which does not steer
            // or accelerate but still has to know the moment it has landed.
            public void SenseGround(float fixedDeltaTime)
            {
                bool wasGrounded = IsGrounded;

                int count = Physics2D.OverlapBox(
                    ProbeOrigin, groundCheckSize, 0f, GroundFilter, Overlaps);

                IsGrounded = count > 0;

                SenseSlope();

                WatchForMissingGround(fixedDeltaTime);

                IsAtEdge = IsGrounded && !HasGroundAt(ledgeCheckAhead);

                if (IsGrounded)
                {
                    if (!wasGrounded)
                    {
                        pendingFallDistance = Mathf.Max(0f, highestPoint - body.position.y);
                        hasLanded = true;
                    }

                    coyoteTimer = coyoteTime;
                    highestPoint = body.position.y;
                    airJumpsUsed = 0;
                    rising = false;
                }
                else
                {
                    if (wasGrounded)
                        Airtime++;

                    coyoteTimer -= fixedDeltaTime;
                    highestPoint = Mathf.Max(highestPoint, body.position.y);
                }
            }

            void WatchForMissingGround(float fixedDeltaTime)
            {
                if (IsGrounded)
                {
                    everGrounded = true;
                    return;
                }

                if (everGrounded || warnedGroundless)
                    return;

                groundlessFor += fixedDeltaTime;
                if (groundlessFor < GroundlessWarning)
                    return;

                warnedGroundless = true;

                if (GroundIsNearby())
                {
                    Debug.LogWarning(
                        $"The wizard has not found the ground in {GroundlessWarning:0} seconds, " +
                        "but there IS something on the right layer within a box of their feet - " +
                        "so the mask is fine and the probe is missing it. Two usual causes: " +
                        "groundCheckOffset sits the probe below the surface instead of across " +
                        "it, or the ground is a CompositeCollider2D set to Outlines, which is a " +
                        "zero-thickness line the probe can sit underneath. Switch the composite " +
                        "to Polygons, or raise groundCheckOffset until the probe straddles the " +
                        "wizard's feet.");

                    return;
                }

                Debug.LogWarning(
                    $"The wizard has not found the ground in {GroundlessWarning:0} seconds, and " +
                    "there is nothing on the right layer anywhere near them. " +
                    $"Movement.groundLayers is set to [{LayerNames(groundLayers)}], and anything " +
                    "they are meant to stand on must be on one of those layers - tilemaps " +
                    "included, which start on Default. Jumping, ledge detection and the staff " +
                    "all read this one mask.");
            }

            bool GroundIsNearby()
            {
                Vector2 wide = groundCheckSize + Vector2.one * NearbyGround;

                return Physics2D.OverlapBox(
                    body.position + groundCheckOffset, wide, 0f, GroundFilter, Overlaps) > 0;
            }

            static string LayerNames(LayerMask mask)
            {
                var listed = new List<string>();

                for (int layer = 0; layer < LayerCount; layer++)
                {
                    if ((mask.value & (1 << layer)) == 0)
                        continue;

                    string name = LayerMask.LayerToName(layer);
                    listed.Add(string.IsNullOrEmpty(name) ? layer.ToString() : name);
                }

                return listed.Count > 0 ? string.Join(", ", listed) : "nothing";
            }

            bool HasGroundAt(float ahead)
            {
                Vector2 probe = ProbeOrigin + new Vector2(Facing * ahead, 0f);
                return Physics2D.Raycast(probe, Vector2.down, GroundFilter, Rays, ledgeCheckDepth) > 0;
            }

            // Which way the floor underfoot is tilted. Three rays rather than one, across the
            // width of the footprint, because a single ray under the middle reads the FLAT tile
            // for the whole first half of stepping onto a ramp - and the steepest walkable
            // answer wins, so the ramp is picked up the moment a toe is over it.
            //
            // Each ray starts a little way UP inside the wizard. Physics2D.queriesStartInColliders
            // is on in this project, so a ray beginning level with the soles and already touching
            // the floor comes back at distance zero with a normal of straight up, which reads as
            // flat no matter what is really down there.
            void SenseSlope()
            {
                groundNormal = Vector2.up;
                groundAngle = 0f;

                if (!IsGrounded || body == null)
                    return;

                float lift = Mathf.Max(SlopeProbeLift, groundCheckSize.y);
                float half = groundCheckSize.x * 0.5f;

                for (int i = -1; i <= 1; i++)
                {
                    var from = new Vector2(ProbeOrigin.x + i * half, ProbeOrigin.y + lift);

                    if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                            lift + slopeProbeDepth) <= 0)
                        continue;

                    Vector2 normal = Rays[0].normal;
                    float angle = Vector2.Angle(normal, Vector2.up);

                    if (angle > groundAngle && angle <= maxSlopeAngle)
                    {
                        groundAngle = angle;
                        groundNormal = normal;
                    }
                }
            }

            // True while the wizard is stood on something tilted enough to be worth steering
            // along rather than across.
            bool OnRamp => IsGrounded && groundAngle > flatSlopeAngle && groundAngle <= maxSlopeAngle;

            // The top of whatever is directly in front of the soles, found by casting DOWN from
            // step height onto it. Casting forward instead would answer with the face rather
            // than the surface, and the height of a lip is the only thing worth knowing here.
            bool TryFindLip(int direction, out float top)
            {
                top = 0f;

                Bounds box = hull.bounds;
                var from = new Vector2(
                    box.center.x + direction * (box.extents.x + stepReach),
                    box.min.y + stepHeight + StepClearance);

                if (Physics2D.Raycast(from, Vector2.down, GroundFilter, Rays,
                        stepHeight + StepClearance + groundCheckSkin) <= 0)
                    return false;

                top = Rays[0].point.y;
                return true;
            }

            // Is there a wall in front of the wizard with a top they could be lifted onto? This
            // is the check behind raising the staff when there is no ledge to plant it over.
            //
            // It answers with the LIP - the top corner of the wall - and with the body position
            // that standing on it would mean, already proved empty. Both come back together
            // because they are found by the same three casts and nothing else in the game has any
            // business recomputing either one.
            public bool TryFindClimb(float highestRise, out Vector2 lip, out Vector2 landing)
            {
                lip = Vector2.zero;
                landing = Vector2.zero;

                if (body == null || hull == null || !IsGrounded)
                    return false;

                // Exactly where the step assist stops, with no gap between them. Anything below
                // this is walked over without being asked, and answering with it would put the
                // staff spell in front of every tile seam in the level - but start even a hair
                // higher and a lip in between is too tall to step and too short to see, which
                // with jumping switched off is a wall nothing in the game can pass.
                float lift = stepHeight;

                if (highestRise <= lift)
                    return false;

                Bounds box = hull.bounds;
                // The FACE first, cast forward from the toes at the height the step assist gives
                // up. From the toes and not the middle, so climbReach means what its name says
                // and does not quietly grow with the wizard's width.
                var ahead = new Vector2(box.center.x + Facing * box.extents.x, box.min.y + lift);

                if (Physics2D.Raycast(ahead, new Vector2(Facing, 0f), GroundFilter, Rays,
                        climbReach) <= 0)
                    return false;

                float faceX = Rays[0].point.x;

                // Then the TOP, cast down onto it from as high as the staff can carry them, a
                // little way INSIDE the face so the ray lands on the surface rather than skimming
                // down the wall it is measuring.
                var above = new Vector2(faceX + Facing * ClimbInset, box.min.y + highestRise);

                if (Physics2D.Raycast(above, Vector2.down, GroundFilter, Rays,
                        highestRise - lift) <= 0)
                    return false;

                // Distance zero means the cast STARTED inside the wall - queriesStartInColliders
                // is on in this project - so whatever is ahead carries on up past the staff's
                // reach and there is nothing here to climb onto. Without this line a wall reports
                // a top at exactly the height it was asked about, and every tower in the level
                // reads as climbable right up until the wizard is left dangling against it.
                if (Rays[0].distance <= 0f)
                    return false;

                lip = new Vector2(faceX, Rays[0].point.y);

                // The COLUMN the wizard actually travels up has to be clear, and nothing above
                // has checked it. The climb rides a kinematic body straight up their own x with
                // MovePosition, so there is no contact resolution to stop them - without this
                // they rise clean through the underside of a balcony that overhangs where they
                // are stood but not the wall top, and pop out on the ledge. Inset on both axes so
                // the floor underfoot and the wall face itself stay out of the query.
                float rise = lip.y - box.min.y;

                var column = new Vector2(
                    box.center.x, box.min.y + StepClearance + (box.size.y + rise) * 0.5f);

                var columnSize = new Vector2(box.size.x - StepClearance * 2f, box.size.y + rise);

                if (Physics2D.OverlapBox(column, columnSize, 0f, GroundFilter, Overlaps) > 0)
                    return false;

                // Where the COLLIDER would sit stood on top of that lip, and then where the BODY
                // would have to be to put it there. The two are not the same point - the wizard's
                // hull sits a twentieth of a box left of their transform - and mixing them up
                // lands them overhanging the lip one way round and buried in the wall the other.
                var hullOnTop = new Vector2(
                    faceX + Facing * (box.extents.x + StepClearance),
                    lip.y + box.extents.y + StepClearance);

                // The whole wizard has to fit up there. The same query TryStepUp uses, doing the
                // same two jobs at once: headroom, and proof that the surface found is the top of
                // something rather than a shelf inside a hole.
                if (Physics2D.OverlapBox(hullOnTop, box.size, 0f, GroundFilter, Overlaps) > 0)
                    return false;

                landing = hullOnTop + (body.position - (Vector2)box.center);
                return true;
            }
        }
    }
}
