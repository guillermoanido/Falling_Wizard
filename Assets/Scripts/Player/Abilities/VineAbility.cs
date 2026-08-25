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

        public override bool OnCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
            {
                wizard.LetGoOfVine();
                return true;
            }

            VineAnchor vine = VineAnchor.Nearest(wizard.movement.Position);

            if (vine == null)
                return false;

            return wizard.TryGrabVine(vine.Knot, vine.length, vine.maxSwing);
        }

        public override void OnRunReset(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                wizard.LetGoOfVine();
        }

        public override void OnUnequipped(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                wizard.LetGoOfVine();
        }
    }
}
