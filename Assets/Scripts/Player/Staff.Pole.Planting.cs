using UnityEngine;

namespace FallingWizard.Player
{
    public partial class Staff
    {
        // How the pole goes in. Three ways, and which one is a decision made before this file:
        // hung off a ledge to climb DOWN, raised against a wall to climb UP, or laid flat as a
        // bridge. All three end with the same two lines, because everything after them - the
        // slide, the release, the drawing - only knows that a pole is planted.
        public partial class Pole
        {
            public bool Plant(StaffMode mode, int wielderFacing, float edgeX) =>
                mode == StaffMode.Bridge
                    ? PlantAsBridge(wielderFacing, edgeX)
                    : PlantAsLadder(wielderFacing, edgeX);

            bool PlantAsLadder(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                anchor = wielder.position;
                depth = 0f;
                dropTimer = 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                float topAboveOrigin = TopAboveOrigin();

                plantedRotation = Quaternion.identity;
                plantedPosition = new Vector3(
                    edgeX + facing * lipClearance,
                    surfaceY - topAboveOrigin,
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                reach = ClearReach(MeasureReach() + HangBelowTip);

                if (reach <= Epsilon)
                {
                    ShoulderPole();
                    return false;
                }

                wielderBodyType = wielder.bodyType;
                wielder.linearVelocity = Vector2.zero;
                wielder.bodyType = RigidbodyType2D.Kinematic;

                Mode = StaffMode.Ladder;
                IsPlanted = true;
                return true;
            }

            // The same ladder, driven in from the BOTTOM. PlantAsLadder hangs the pole off a
            // ledge the wizard is stood on; this raises it against a wall they are stood under,
            // which is the only way up anything taller than the step assist.
            //
            // Deliberately not routed through Plant(): that dispatcher takes an edgeX, which is a
            // ledge the wizard is already on top of, and there is no honest value to pass it
            // here. The lip arrives already measured instead.
            public bool PlantAsClimb(int wielderFacing, Vector2 lip, Vector2 landing)
            {
                if (!HasPole || !HasWielder)
                    return false;

                Face(wielderFacing);

                // The top of the climb is directly above where they are ALREADY stood, level with
                // the lip. Not over the lip: the swing in PositionAt lerps sideways as the depth
                // closes, and on a descent that lerp moves the wizard AWAY from the cliff while
                // this one would move them INTO the wall - the same interpolation is not safe in
                // both directions. Keeping the whole climb on the near face means the only
                // sideways travel is the short one onto the pole, and stepping over the lip is
                // done once, on release, to a spot already proved empty.
                anchor = new Vector2(wielder.position.x, lip.y + WielderFeetOffset);
                dropTimer = 0f;

                plantedRotation = Quaternion.identity;
                plantedPosition = new Vector3(
                    lip.x - facing * lipClearance,
                    lip.y - TopAboveOrigin(),
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                // How far down the pole they start: the drop from standing on top back to where
                // they actually are. Refusing here rather than snapping them to the bottom of the
                // pole is the whole gate - it is what stops a staff too short for the wall
                // hauling the wizard up it anyway.
                depth = anchor.y - wielder.position.y;
                float rawReach = RawReach;

                if (depth <= Epsilon || depth > rawReach)
                {
                    ShoulderPole();
                    return false;
                }

                // ClearReach stops the pole reaching down through the floor - which on a climb is
                // the floor the wizard is stood on, so it answers with their own starting depth to
                // the last decimal place. Taking whichever is larger means a rounding error there
                // cannot leave them a hair past the bottom of their own pole, where Slide's clamp
                // would snap them upward on the very first step.
                reach = Mathf.Max(depth, ClearReach(rawReach));

                climbing = true;
                climbLanding = landing;
                climbHangX = wielder.position.x;

                // A climb starts AT the bottom - reach and depth are both the height of the wall
                // - so the drop-out timer is already armed on the very first step. Started in
                // debt, it takes a fresh, deliberate hold to let go, instead of a stick that
                // happened to be resting downward cancelling the cast half a second after it
                // was paid for.
                dropTimer = -dropHoldTime;

                wielderBodyType = wielder.bodyType;
                wielder.linearVelocity = Vector2.zero;
                wielder.bodyType = RigidbodyType2D.Kinematic;

                Mode = StaffMode.Ladder;
                IsPlanted = true;
                return true;
            }

            bool PlantAsBridge(int wielderFacing, float edgeX)
            {
                if (!HasPole || !HasWielder)
                    return false;

                if (bridge == null)
                {
                    Debug.LogWarning("The staff has no bridge collider, so there is nothing to " +
                                     "stand on. Assign one on the Staff component.");
                    return false;
                }

                Face(wielderFacing);

                anchor = wielder.position;
                float surfaceY = anchor.y - WielderFeetOffset;

                LocalBox(bridge, out Vector2 localCentre, out Vector2 localSize);

                float scaleX = Mathf.Abs(pole.lossyScale.x);
                float scaleY = Mathf.Abs(pole.lossyScale.y);
                float length = localSize.y * scaleY;
                float thickness = localSize.x * scaleX;

                if (pole.parent != null)
                    pole.SetParent(null, true);

                plantedRotation = Quaternion.Euler(0f, 0f, facing > 0 ? -QuarterTurn : QuarterTurn);

                var carried = new Vector2(
                    facing * localCentre.y * scaleY,
                    -facing * localCentre.x * scaleX);

                var wanted = new Vector2(
                    edgeX + facing * (lipClearance + length * 0.5f),
                    surfaceY - thickness * 0.5f);

                plantedPosition = new Vector3(
                    wanted.x - carried.x,
                    wanted.y - carried.y,
                    pole.position.z);

                pole.SetPositionAndRotation(plantedPosition, plantedRotation);

                bridge.enabled = true;
                reach = 0f;
                depth = 0f;

                Mode = StaffMode.Bridge;
                IsPlanted = true;
                return true;
            }

            float ClearReach(float rawReach)
            {
                if (rawReach <= Epsilon)
                    return 0f;

                float surfaceY = anchor.y - WielderFeetOffset;
                var origin = new Vector2(plantedPosition.x, surfaceY);

                if (Physics2D.Raycast(origin, Vector2.down, GroundFilter, Rays, rawReach) == 0)
                    return rawReach;

                return Mathf.Clamp(surfaceY - Rays[0].point.y, 0f, rawReach);
            }
        }
    }
}
