using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // A garden rake left lying tines up. Tread on it, the handle comes round, and you go down
    // BACKWARDS - back the way you came, not onward the way every other hazard in this game
    // throws you.
    //
    // That reversal is a deliberate exception to the rule written up in Scripts/README.md, and
    // the rule is a good one: being thrown back costs the player ground they had already won and
    // lands them somewhere they were not looking. The rake earns the exception because it is the
    // one gag where losing the ground IS the joke, and because the player reads exactly what
    // happened the instant it happens. Do not add a second hazard that does this. One is a
    // punchline; two is a movement system that takes things back.
    //
    // No gizmo arrow, unlike WindZone2D. Which way this throws you depends on which way YOU were
    // going, so an arrow drawn on the rake would be wrong half the time - worse than none.
    public class Rake : Hazard
    {
        // Under a walk (2 boxes a second), so simply stepping on it is enough - but comfortably
        // over the tenth of a box a second at which Movement.TravelDirection stops trusting the
        // velocity and answers with Facing instead. That floor matters more here than anywhere
        // else in the game: this hazard's whole job is to send you back the way you CAME, and
        // Facing is merely the way you are looking, which a wizard turning on the spot changes
        // for free.
        //
        // It also settles the standing-still and straight-drop cases without any special case in
        // the code. Hazard gates on ApproachSpeed, which is HORIZONTAL speed only, so a wizard
        // dropping vertically onto a rake never reaches the gate and the rake simply does not
        // fire. Set this to 0 and you get that case back - a rake that trips a motionless wizard
        // in whichever direction they happen to be facing.
        const float StepOnIt = 1.5f;

        // Long enough that the handle has plainly come back round before it can catch you a
        // second time - including the moment you land from the throw it just gave you.
        const float HandleCooldown = 1.25f;

        [Header("Rake")]
        [Tooltip("Extra shove BACKWARDS on top of the ordinary trip, in boxes per second. Leave " +
                 "this at 0 and a rake throws exactly as hard as every other trip in the game, " +
                 "which is almost always what you want - one number, the wizard's own " +
                 "Ragdoll launch, then tunes them all together. Raise it only to turn one " +
                 "particular rake into something the player has to solve rather than survive: 3 " +
                 "costs them roughly an extra box of ground.")]
        [Min(0f)] public float extraKick = 0f;

        void Reset()
        {
            minimumSpeed = StepOnIt;
            rearmDelay = HandleCooldown;
            damage = 0;
        }

        protected override void Affect(PlayerLogic wizard)
        {
            // The direction they ARRIVED in, negated. Not away from the rake's own centre: that
            // would throw two wizards who stepped on the same rake in opposite directions, which
            // is the same object behaving differently for no reason the player can see.
            int back = -wizard.movement.TravelDirection;

            // Trip refuses when the wizard is not in Normal state - on the staff, on a vine,
            // already tumbling - or is dead. Hazard.Allowed has already filtered most of that,
            // but affectsOnStaff ticked ON would get past it and Trip would still refuse, so the
            // extra kick has to hang off the trip actually taking. A shove with no tumble
            // attached is a push out of nowhere with nothing on screen to explain it.
            if (!wizard.Trip(back))
                return;

            if (extraKick > 0f)
                wizard.Shove(new Vector2(back * extraKick, 0f), 0f);
        }
    }
}
