using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Vine Grasp", fileName = "Vine Grasp")]
    public class VineAbility : Ability
    {
        [Header("Grasp")]
        [Tooltip("Pressing again while hanging lets go. Off means only Jump lets go, which frees " +
                 "the button up but makes a mistimed grab harder to undo.")]
        public bool pressAgainToLetGo = true;

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return pressAgainToLetGo;

            return wizard.CanGrabVine && VineAnchor.Nearest(wizard.movement.Position) != null;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return "'press again to let go' is switched off on this spell";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            if (!wizard.CanGrabVine)
                return "you only just let go of one";

            if (VineAnchor.Nearest(wizard.movement.Position) == null)
                return VineAnchor.All.Count == 0
                    ? "there is not a single Vine in this scene"
                    : "no vine is close enough - look for a knot that has started glowing";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
            {
                LetGo(wizard);
                return true;
            }

            VineAnchor vine = VineAnchor.Nearest(wizard.movement.Position);

            if (vine == null)
                return false;

            if (!wizard.TryGrabVine(vine.Knot, vine.length, vine.maxSwing))
                return false;

            // The knot has been glowing at the player since they came into range; this is the
            // moment it pays off and the vine actually drops.
            vine.CallDown();

            wizard.spellbook.StateOf<Grip>(this).held = vine;
            return true;
        }

        public override void OnRunReset(PlayerLogic wizard) => LetGo(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => LetGo(wizard);

        void LetGo(PlayerLogic wizard)
        {
            Grip grip = wizard.spellbook.StateOf<Grip>(this);

            if (grip.held != null)
            {
                grip.held.RollUp();
                grip.held = null;
            }

            if (wizard.IsOnVine)
                wizard.LetGoOfVine();
        }

        public class Grip
        {
            public VineAnchor held;
        }
    }
}
