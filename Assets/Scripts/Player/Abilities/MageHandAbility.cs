using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    // A spectral hand that hangs where a vine hangs and swings like one.
    //
    // The range that decides whether you CAN call it and the range over which a grab may MOVE you
    // are two different numbers, and conflating them was the teleport bug: eligibility is measured
    // against the rope as a line segment, but the grab then lands you on a circular arc about the
    // knot, clamped to the swing limit. Stood on the ledge the vine is tied to, those two points
    // were four boxes apart - and the rope hauled you across the gap at fifteen times a run.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Mage Hand", fileName = "Mage Hand")]
    public class MageHandAbility : Ability
    {
        [Header("Grasp")]
        [Tooltip("How far a grab is allowed to MOVE you, in boxes, measured to the point on the " +
                 "arc you would actually end up at. One box is one mage. Keep it under about 1.2: " +
                 "above that no grab can finish inside a single physics step, and the swing " +
                 "reads its own overshoot as having hit a wall and throws your speed away.")]
        [Range(0.1f, 1.2f)] public float graspRange = 1f;

        [Tooltip("Pressing again while hanging lets go. Off means only Jump lets go, which frees " +
                 "the button up but makes a mistimed grab harder to undo.")]
        public bool pressAgainToLetGo = true;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return pressAgainToLetGo;

            if (!wizard.CanGrabVine)
                return false;

            VineAnchor vine = VineAnchor.Nearest(wizard.movement.Position);

            return vine != null && wizard.GrabSnapDistance(Spec(wizard, vine)) <= graspRange;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.IsOnVine)
                return "'press again to let go' is switched off on this spell";

            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            if (!wizard.CanGrabVine)
                return "you only just let go of one";

            VineAnchor vine = VineAnchor.Nearest(wizard.movement.Position);

            if (vine == null)
                return VineAnchor.All.Count == 0
                    ? "there is not a single Vine in this scene"
                    : "no vine is close enough - look for a knot that has started glowing";

            float snap = wizard.GrabSnapDistance(Spec(wizard, vine));

            if (snap > graspRange)
                return $"the hand would have to drag you {snap:0.0} boxes to reach it - get " +
                       $"level with the rope rather than standing over where it is tied";

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

            if (vine == null || !wizard.TryGrabVine(Spec(wizard, vine)))
                return false;

            // The knot has been glowing at the player since they came into range; this is the
            // moment it pays off and the hand actually reaches out.
            vine.CallDown();

            wizard.spellbook.StateOf<Grip>(this).held = vine;
            return true;
        }

        public override void OnRunReset(PlayerLogic wizard) => LetGo(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => LetGo(wizard);

        protected override void Validate()
        {
            graspRange = Mathf.Clamp(graspRange, 0.1f, 1.2f);

            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        PlayerLogic.Vine.Hold Spec(PlayerLogic wizard, VineAnchor vine)
        {
            Tier tier = TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

            return new PlayerLogic.Vine.Hold
            {
                Anchor = vine.Knot,
                Length = vine.length,

                // The vine keeps its own ceiling and so does the wizard; the smallest of the
                // three wins, so a rank can only ever open up what the level already allows.
                MaxSwingDegrees = Mathf.Min(vine.maxSwing, tier.maxSwing),
                ClimbSpeed = tier.climbSpeed,
                SnapLimit = graspRange,
            };
        }

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

        [System.Serializable]
        public class Tier
        {
            [Tooltip("How far the hand leans either side of straight down, in degrees. The vine " +
                     "and the wizard both keep their own ceiling; the smallest of the three wins.")]
            [Range(0f, 89f)] public float maxSwing = 30f;

            [Tooltip("Climbing up and down, in boxes per second. 0 means you cannot - that is " +
                     "rank 1, and unlocking it is the upgrade.")]
            [Min(0f)] public float climbSpeed = 0f;

            // OnValidate does not reach into a nested class, so the block clamps itself.
            public void Validate()
            {
                maxSwing = Mathf.Clamp(maxSwing, 0f, 89f);
                climbSpeed = Mathf.Max(0f, climbSpeed);
            }
        }

        public class Grip
        {
            public VineAnchor held;
        }
    }
}
