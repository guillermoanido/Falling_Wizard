using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // A flight of steps. Take them at a walk or an easy jog and you go up or down without
    // thinking about it; come at them flat out and your feet go from under you and you tumble on
    // down them the way you were already travelling.
    //
    // This is a VOLUME, not the steps themselves. The tilemap already carries the shape the
    // wizard walks on - PlayerLogic.Movement steers them along it and the step assist carries
    // them over the treads - and this is a trigger box laid over the top of that, saying "this
    // stretch of floor is a staircase". So a staircase is two things a level author places: the
    // tiles, and one of these across them.
    //
    // No gizmo arrow, unlike WindZone2D. Which way this throws you depends on which way YOU were
    // going, so an arrow drawn on the stairs would be wrong half the time - worse than none.
    public class Stairs : Hazard
    {
        // Between Level 1's walk (2 boxes a second) and its run (4), with a box a second of
        // margin either side.
        //
        // Never set this TO the run speed. The wizard is almost never at exactly top speed once
        // a slope, the last of the ground friction or a MoveSpeedMultiplier has touched them, so
        // a gate there fires on the fourth decimal place - which is how the Wet Floor Sign ended
        // up letting a dead sprint walk straight through it.
        const float RunningOnly = 3f;

        // Long enough that one tumble does not become a second the moment they stand up, and
        // that a wizard sliding to a halt on the steps is left alone while they do it.
        const float TumbleCooldown = 1f;

        void Reset()
        {
            minimumSpeed = RunningOnly;
            rearmDelay = TumbleCooldown;
            damage = 0;

            // Stairs are a PLACE, so speeding up half way down them counts as much as arriving
            // fast. Every other hazard in the game is a thing you hit and fires on the way in.
            everyStep = true;
        }

        // Trip and nothing else. It throws the wizard the way they were already travelling -
        // Ragdoll reads Movement.TravelDirection - which on a staircase is down it, and it keeps
        // the speed they arrived with, so a fast run turns into a long tumble and a merely brisk
        // one into a short one. All of that is Ragdoll's tuning, shared with every other trip in
        // the game, and deliberately not repeated here.
        protected override void Affect(PlayerLogic wizard) => wizard.Trip();
    }
}
