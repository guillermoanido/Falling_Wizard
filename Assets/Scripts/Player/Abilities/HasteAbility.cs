using FallingWizard.Core;
using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    // The wizard speeds up and the world wades. Everything that moves under its own steam reads
    // Haste.WorldScale and slows itself; the wizard does not, and neither does their ragdoll,
    // because neither of them asks. That is the whole trick, and it is why this is a flag rather
    // than Time.timeScale - see Core/Haste.cs for why scaling time would have been worse.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Haste", fileName = "Haste")]
    public class HasteAbility : Ability
    {
        [Header("The Wizard")]
        [Tooltip("Top speed while hasted, against a normal run.")]
        [Min(1f)] public float moveSpeed = 1.5f;

        [Tooltip("How hard the stick bites, so the speed is reachable rather than something you " +
                 "slide up to over a second.")]
        [Min(1f)] public float control = 1.6f;

        [Header("The World")]
        [Tooltip("How fast everything ELSE runs while this is up. 0.35 is a third speed, which " +
                 "is enough to walk into a gale or across a moving hazard that would otherwise " +
                 "have the timing on you. The wizard's own ragdoll is never scaled by this.")]
        [Range(0.05f, 1f)] public float worldScale = 0.35f;

        [Header("Trail")]
        [Tooltip("Stamp copies of the wizard behind them, fading out. This is the only thing " +
                 "telling the player the spell is up, so leave it on unless you have another.")]
        public bool afterimages = true;

        [Tooltip("Seconds between one ghost and the next.")]
        [Min(0.01f)] public float every = 0.06f;

        [Tooltip("Seconds a ghost takes to fade out.")]
        [Min(0.05f)] public float ghostLife = 0.3f;

        public Color ghostTint = new Color(0.62f, 0.86f, 1f, 0.5f);

        [Tooltip("Sorting order for the trail. Behind the wizard.")]
        public int sortingOrder = -1;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal || wizard.State == PlayerState.OnVine;

        public override string WhyNot(PlayerLogic wizard) =>
            CanCast(wizard) ? null : $"you are {wizard.State}";

        public override bool OnCast(PlayerLogic wizard)
        {
            Tier tier = Of(wizard);

            Haste.Begin(tier.worldScale);

            if (!afterimages)
                return true;

            Trail trail = wizard.spellbook.StateOf<Trail>(this);

            if (trail.images == null)
                trail.images = Afterimages.For(wizard.movement.Art, ghostTint, every, ghostLife,
                    sortingOrder);

            trail.images?.Run(true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            Tier tier = Of(wizard);

            stats.MoveSpeedMultiplier *= tier.moveSpeed;
            stats.AirControlMultiplier *= control;
        }

        public override void OnEnded(PlayerLogic wizard) => Stop(wizard);

        public override void OnRunReset(PlayerLogic wizard) => Stop(wizard);

        public override void OnUnequipped(PlayerLogic wizard) => Stop(wizard);

        protected override void Validate()
        {
            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        void Stop(PlayerLogic wizard)
        {
            Haste.End();

            Trail trail = wizard.spellbook.StateOf<Trail>(this);

            // Retire rather than Destroy: the ghosts already out finish fading instead of
            // blinking away the instant the spell ends.
            trail.images?.Retire();
            trail.images = null;
        }

        [System.Serializable]
        public class Tier
        {
            [Min(1f)] public float moveSpeed = 1.5f;

            [Range(0.05f, 1f)] public float worldScale = 0.35f;

            // OnValidate does not reach into a nested class, so the block clamps itself.
            public void Validate()
            {
                moveSpeed = Mathf.Max(1f, moveSpeed);
                worldScale = Mathf.Clamp(worldScale, 0.05f, 1f);
            }
        }

        public class Trail
        {
            public Afterimages images;
        }
    }
}
