using UnityEngine;

namespace FallingWizard.Player
{
    // The staff is the wizard's legs. Jumping is switched off in Movement, so this is the only
    // thing in the game that takes them upward under their own power.
    //
    // It is a HELD spell, not a pressed one - chargesOnHold on the asset - and the reason is one
    // specific failure. A press is a single instant, and if the wall was a finger's width too far
    // away on that instant, nothing happened and nothing said why. A hold asks again every
    // physics step: raise the staff, look, and the moment the wizard shuffles into range they go
    // up. The wizard holding the staff in the air with nothing in front of them is not a bug -
    // it is the spell telling them there is nothing here to climb.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Staff", fileName = "Staff")]
    public class StaffAbility : Ability
    {
        [Header("Ranks")]
        [Tooltip("How long the staff is at each rank, against the length you built it. Element 0 " +
                 "is rank 1. Everything is measured off the pole's own scale, so this is the only " +
                 "number that has to change - how tall a wall it will climb, how deep a drop it " +
                 "will reach down and where the hand-hang ends all follow.")]
        public float[] lengthByRank = { 1f, 1.5f };

        // Applied from here rather than OnEquipped because buying a rank does not change WHICH
        // spell is in the slot, so OnEquipped would not fire and the staff would stay short until
        // the wizard next died. SetLengthScale early-outs when the number has not moved.
        public override void ModifyStats(PlayerLogic wizard, PlayerLogic.Modifiers stats) =>
            wizard.SetStaffLength(TierFor(lengthByRank, wizard.spellbook.RankOf(this)));

        public override void OnUnequipped(PlayerLogic wizard) => wizard.SetStaffLength(1f);

        // One button, two directions, and which one you get is decided by the ground rather than
        // by a second button: a drop in front of you means down, a wall means up. Both end with
        // the wizard hanging on the same pole, driven by the same stick.
        public override bool CanCast(PlayerLogic wizard) =>
            wizard.IsOnStaff ||
            (wizard.StaffIsFree && (wizard.movement.IsAtEdge || wizard.CanClimbHere));

        public override string WhyNot(PlayerLogic wizard)
        {
            if (!wizard.HasPole)
                return "there is no Staff object under the wizard to plant";

            if (wizard.Pole.IsPlanted && wizard.Pole.Mode != StaffMode.Ladder)
                return "the staff is already out, laid flat";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State}";

            // Asked POSITIVELY, and never off IsAtEdge - that flag is a physics step old and it
            // says only that ground is MISSING ahead, not that a pole can be driven in there.
            if (wizard.movement.TryFindLedgeEdge(out _))
                return "the drop here is too shallow for the staff to reach down into";

            // Split so the console names the ACTUAL refusal. "Nothing to climb" and "too tall to
            // climb" are the same silence to a player and completely different problems to fix.
            if (!wizard.movement.TryFindWall(wizard.Pole.ClimbHeight, out _))
                return "there is nothing within reach in front of you to raise the staff against";

            return "what is ahead of you is too tall for the staff, or there is no room to stand " +
                   "on top of it";
        }

        // Every fixed step the button is down.
        public override void OnHeld(PlayerLogic wizard, float heldSeconds, float fixedDeltaTime)
        {
            // Already hanging on the pole. The stick drives it from there - up at the top steps
            // off onto the ledge, down at the bottom lets go - and this button has nothing to
            // add. Releasing it does NOT drop them off, because a climb is a place you are, not
            // a button you are holding.
            if (wizard.IsOnStaff || !wizard.StaffIsFree)
                return;

            // The staff goes up whether or not there turns out to be anything to climb. That is
            // the whole of what the player is promised for holding the button, and it is what
            // makes a refusal legible: staff up and going nowhere means nothing here, rather
            // than a press that vanished.
            wizard.RaiseStaff();

            // A drop wins. The pole goes over the lip and they climb down it - asked every step
            // rather than only the first, so walking up to a ledge with the button already held
            // plants the moment the ledge arrives.
            if (wizard.movement.IsAtEdge && wizard.TryPlantStaff(StaffMode.Ladder))
                return;

            wizard.TryClimbStaff();
        }

        public override void OnReleased(PlayerLogic wizard, float heldSeconds) =>
            wizard.LowerStaff();

        // The button came up behind a pause menu, where nothing is watching for the release.
        public override void OnChargeLost(PlayerLogic wizard) => wizard.LowerStaff();
    }
}
