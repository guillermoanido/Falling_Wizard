using UnityEngine;

namespace FallingWizard.Player
{
    public partial class Staff
    {
        // What happens while the wizard is on the pole: the slide, where that puts them, and
        // letting go - including the one case that has earned being set down over the lip.
        public partial class Pole
        {
            public StaffHold Slide(float lean, float fixedDeltaTime)
            {
                if (!IsPlanted || !HasWielder || Mode != StaffMode.Ladder)
                    return StaffHold.LetGo;

                // The SAME threshold the two exits below use. Driven by the raw lean, a stick
                // resting at 0.3 climbed the wizard all the way to the top and then left them
                // there - moving, but never far enough to count as asking to get off, with the
                // only way out being to push harder. On a descent that was survivable because
                // you could still drop; on a climb it is the only exit there is.
                float pull = Mathf.Abs(lean) > leanThreshold ? lean : 0f;

                depth = Mathf.Clamp(depth - pull * slideSpeed * fixedDeltaTime, 0f, reach);
                wielder.MovePosition(PositionAt(depth));

                if (AtTop && lean > leanThreshold)
                    return StaffHold.BackOnLedge;

                if (AtBottom && lean < -leanThreshold)
                {
                    dropTimer += fixedDeltaTime;

                    if (dropTimer >= dropHoldTime)
                        return StaffHold.LetGo;
                }
                else
                {
                    dropTimer = 0f;
                }

                return StaffHold.Holding;
            }

            // arrived is TOLD, not worked out from the depth. A climb ends at depth zero, and so
            // does dying at the top of one, and so does pressing the staff button up there to let
            // go - and only the first of those has earned the far side of the wall. Inferring it
            // handed the other two a free arrival, one of them over a corpse.
            public void Release(bool arrived = false)
            {
                bool wasLadder = IsPlanted && Mode == StaffMode.Ladder;

                // Read BEFORE IsPlanted goes down.
                bool toppedOut = arrived && IsPlanted && climbing && AtTop;

                IsPlanted = false;
                climbing = false;
                dropTimer = 0f;

                if (bridge != null)
                    bridge.enabled = false;

                if (pole != null)
                {
                    pole.localRotation = Quaternion.identity;

                    if (pole.parent != carriedParent)
                        pole.SetParent(carriedParent, false);
                }

                plantedRotation = Quaternion.identity;
                Mode = StaffMode.Ladder;
                ShoulderPole();

                if (!HasWielder)
                    return;

                if (wasLadder)
                {
                    // Stepped over the lip while still kinematic, so nothing has to be resolved
                    // out of the wall afterwards. Re-checked rather than trusted: TryFindClimb
                    // proved that spot empty when the pole went in, and a climb takes seconds -
                    // long enough for a carried rock to be set down in it. Refusing leaves them
                    // at the top of the pole, which is somewhere they demonstrably fit.
                    if (toppedOut && LandingIsClear())
                        wielder.position = climbLanding;

                    wielder.bodyType = wielderBodyType;
                    wielder.linearVelocity = Vector2.zero;
                }
            }

            public void HoldPolePosition()
            {
                if (IsPlanted && pole != null)
                    pole.SetPositionAndRotation(plantedPosition, plantedRotation);
            }

            // Would the wizard fit where the climb wants to set them down? The same question
            // Movement.TryFindClimb asked before the pole went in, asked again at the moment it
            // matters, and about the COLLIDER rather than the body - the two are not the same
            // point and the landing was recorded as a body position.
            bool LandingIsClear()
            {
                if (wielderHitbox == null || wielder == null)
                    return true;

                Bounds box = wielderHitbox.bounds;
                Vector2 hullAt = climbLanding + ((Vector2)box.center - wielder.position);

                return Physics2D.OverlapBox(hullAt, box.size, 0f, GroundFilter, Overlaps) == 0;
            }

            public Vector2 PositionAt(float atDepth)
            {
                // A climb is a straight line up, with no swing onto the pole at all. The swing
                // below lerps the wizard towards where the POLE is, and on a descent that is out
                // over the drop - away from anything solid. Raised against a wall the pole is ON
                // the face, so the same lerp would walk them into it, and it would do it while
                // they are still below the lip with nowhere for the collider to go. They go up
                // the near side instead, and step over the lip once, on release.
                if (climbing)
                    return new Vector2(climbHangX, anchor.y - atDepth);

                float ontoPole = swingDepth <= Epsilon ? 1f : Mathf.Clamp01(atDepth / swingDepth);

                return new Vector2(
                    Mathf.Lerp(anchor.x, plantedPosition.x, ontoPole),
                    anchor.y - atDepth);
            }
        }
    }
}
