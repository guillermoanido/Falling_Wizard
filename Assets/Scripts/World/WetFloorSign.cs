using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // A yellow A-frame sign, and the wet floor it is standing on. Clip it at a run and your
    // feet go out from under you; pick your way past at a walk and nothing happens - which is
    // exactly what a wet floor does, and is why this kept the trip behaviour it had when it was
    // a rock. Only the theming changed.
    //
    // The sign is drawn TALLER than its hitbox on purpose. The player has to be able to read the
    // warning a moment before they are standing in the slippery part, or the trip is something
    // that happened to them rather than something they walked into.
    public class WetFloorSign : Hazard
    {
        // Between Level 1's walk (2 boxes a second) and its run (4), with a box a second of
        // margin either side.
        //
        // This was 4, which is Level 1's run speed EXACTLY - and Hazard gates on
        // ApproachSpeed < minimumSpeed, so any wizard who had shaved a sliver off top speed (a
        // ramp, the last of the ground friction, a MoveSpeedMultiplier under 1) walked straight
        // through the sign at a dead sprint. Never set this TO the run speed again: the wizard
        // is almost never at exactly top speed, so a gate there fires on the fourth decimal
        // place. Note also that the class default in PlayerLogic is runSpeed 6 while the Level 1
        // wizard overrides it to 4 - the level is what this number has to match.
        const float RunningOnly = 3f;

        // Long enough that one slip does not immediately become a second one when they land on
        // the same wet patch.
        const float SlipCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = SlipCooldown;
        }

        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
