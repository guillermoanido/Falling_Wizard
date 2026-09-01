using FallingWizard.Core;
using UnityEngine;

namespace FallingWizard.Player
{
    // A paraglider, not a parachute. It barely slows the drop - the point is where you come down,
    // not how hard. Note what it deliberately does NOT do: forgive fall damage. That was the old
    // Feather Fall's whole reason to exist, and giving this spell both would make it two spells
    // fighting over three buttons. Here you survive by going somewhere else.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Glide", fileName = "Glide")]
    public class GlideAbility : Ability
    {
        [Header("Descent")]
        [Tooltip("Fall speed with the canopy out, as a fraction of normal. Set it as low as " +
                 "reads well: fall damage counts BOXES fallen rather than how fast you were " +
                 "going, so however slow this makes the descent, the drop bills you exactly the " +
                 "same. It cannot turn this into a survival tool. It is also where the REACH " +
                 "comes from: distance across a fall is airtime times sideways speed, so halving " +
                 "the descent is what gives the wing its arc. At 0.8 the spell was just a sprint " +
                 "that happened in the air.")]
        [Range(0.2f, 1f)] public float fallSpeed = 0.5f;

        [Header("Reach")]
        [Tooltip("Top sideways speed in the air, against a run. About 1.9 turns a running jump " +
                 "from a bit over two boxes into nearly four. It does nothing on the ground - a " +
                 "canopy needs air under it.")]
        [Min(1f)] public float airSpeed = 1.9f;

        [Tooltip("How hard the stick bites in the air. Steering only - it does not touch how " +
                 "fast you coast to a stop.")]
        [Min(0.5f)] public float airControl = 1.6f;

        [Tooltip("How fast you slow down in the air with NO stick held, against normal. Below 1 " +
                 "you coast, which is the point of a wing: a canopy that braked harder than bare " +
                 "air would drop you straight down the moment you stopped steering, which is the " +
                 "opposite of gliding.")]
        [Range(0.1f, 1f)] public float drag = 0.45f;

        [Header("Canopy")]
        [Tooltip("Drawn above the wizard while it is out. Empty draws a flat tinted block.")]
        public Sprite canopyArt;

        public Color tint = new Color(0.95f, 0.78f, 0.42f, 0.85f);

        [Tooltip("Width and height in boxes, then how high above the wizard's middle it rides. " +
                 "A mage is one box, so a wing wants to be wider than they are.")]
        public Vector3 canopy = new Vector3(2.2f, 0.5f, 0.9f);

        [Tooltip("Sorting order. Above the tilemap, or the canopy is out, working, and invisible.")]
        public int sortingOrder = 1;

        [Header("Folding")]
        [Tooltip("Touching down folds it and starts the cooldown.")]
        public bool foldsOnLanding = true;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        // Castable from the GROUND deliberately: every multiplier here is air-only, so throwing
        // the canopy out and THEN running off a ledge is how you get the jump case. Gating on
        // being airborne, the way the old Feather Fall did, would cost the spell half its job.
        public override bool CanCast(PlayerLogic wizard) => wizard.State == PlayerState.Normal;

        public override string WhyNot(PlayerLogic wizard) =>
            wizard.State == PlayerState.Normal ? null : $"you are {wizard.State}";

        public override bool OnCast(PlayerLogic wizard)
        {
            wizard.spellbook.StateOf<Wing>(this).flown = false;
            Show(wizard, true);
            return true;
        }

        public override void ModifyStatsWhileLit(PlayerLogic wizard, PlayerLogic.Modifiers stats)
        {
            Tier tier = Of(wizard);

            stats.FallSpeedMultiplier *= tier.fallSpeed;
            stats.AirSpeedMultiplier *= tier.airSpeed;
            stats.AirControlMultiplier *= tier.airControl;
            stats.AirDragMultiplier *= drag;
        }

        public override void OnLit(PlayerLogic wizard, float fixedDeltaTime)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (!wizard.movement.IsGrounded)
            {
                wing.flown = true;
                return;
            }

            // Fold on LANDING, which means having been in the air since this cast - not
            // "Movement.Airtime > 0", which is a lifetime counter that never goes back down.
            // That guard only ever protected the first cast of a session: after one jump it is
            // true forever, so throwing the canopy out while stood at a ledge folded it on the
            // next physics step, put it on cooldown, and left the wizard falling at normal
            // speed with no sign anything had happened.
            if (foldsOnLanding && wing.flown)
                wizard.spellbook.Extinguish(this);
        }

        public override void OnEnded(PlayerLogic wizard) => Show(wizard, false);

        public override void OnEquipped(PlayerLogic wizard) => Show(wizard, false);

        public override void OnRunReset(PlayerLogic wizard) => Show(wizard, false);

        public override void OnUnequipped(PlayerLogic wizard) => Fold(wizard);

        protected override void Validate()
        {
            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        void Show(PlayerLogic wizard, bool out_)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (wing.art == null)
                wing.art = Build(wizard);

            if (wing.art != null)
                wing.art.enabled = out_;
        }

        void Fold(PlayerLogic wizard)
        {
            Wing wing = wizard.spellbook.StateOf<Wing>(this);

            if (wing.art != null)
                Destroy(wing.art.gameObject);

            wing.art = null;
        }

        SpriteRenderer Build(PlayerLogic wizard)
        {
            Transform rig = wizard.Rig;

            if (rig == null)
                return null;

            var go = new GameObject(displayName);
            go.transform.SetParent(rig, false);
            go.transform.localPosition = new Vector3(0f, canopy.z, 0f);
            go.transform.localScale = new Vector3(canopy.x, canopy.y, 1f);

            var art = go.AddComponent<SpriteRenderer>();
            art.sprite = canopyArt != null ? canopyArt : Placeholder.Box;
            art.color = tint;
            art.sortingOrder = sortingOrder;
            art.enabled = false;

            return art;
        }

        [System.Serializable]
        public class Tier
        {
            [Range(0.2f, 1f)] public float fallSpeed = 0.5f;
            [Min(1f)] public float airSpeed = 1.9f;
            [Min(0.5f)] public float airControl = 1.6f;

            // OnValidate does not reach into a nested class, so the block clamps itself.
            public void Validate()
            {
                fallSpeed = Mathf.Clamp(fallSpeed, 0.2f, 1f);
                airSpeed = Mathf.Max(1f, airSpeed);
                airControl = Mathf.Max(0.5f, airControl);
            }
        }

        public class Wing
        {
            public SpriteRenderer art;

            // Airborne at some point since this cast, so touching down counts as landing.
            public bool flown;
        }
    }
}
