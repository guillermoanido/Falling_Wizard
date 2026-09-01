using FallingWizard.World;
using UnityEngine;

namespace FallingWizard.Player
{
    // Take a hazard with you and put it down where you want it. The slot shows what you are
    // carrying, so a stowed slime and a stowed rock are told apart without a menu.
    //
    // This is the one spell that edits the level rather than the wizard. A slime moved to the
    // bottom of a drop is a trampoline you placed; a rock moved out of a corridor is a corridor
    // you can run down. Everything else here changes how you move - this changes what you are
    // moving through.
    [CreateAssetMenu(menuName = "Falling Wizard/Abilities/Telekinesis", fileName = "Telekinesis")]
    public class TelekinesisAbility : Ability
    {
        [Header("Reach")]
        [Tooltip("How far the wizard can take hold of something, in boxes. A mage is one box.")]
        [Min(1f)] public float reach = 6f;

        [Tooltip("How many tiles ahead it will look for somewhere to set it down. It takes the " +
                 "first tile that is empty AND has a floor under it, so nothing is ever left " +
                 "hanging in the air.")]
        [Range(1, 5)] public int placeInTiles = 2;

        [Tooltip("Tiles above your own feet to place it. 0 is the floor you are stood on.")]
        [Range(0, 2)] public int liftInTiles = 0;

        [Header("Ranks")]
        [Tooltip("One block per rank. Element 0 is what learning it gives you.")]
        public Tier[] tiers = { new Tier() };

        public override bool CanCast(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return false;

            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            return hands.thing != null
                ? FindShelf(wizard, out _)
                : Carryable.Nearest(wizard.movement.Position, Of(wizard).reach) != null;
        }

        public override string WhyNot(PlayerLogic wizard)
        {
            if (wizard.State != PlayerState.Normal)
                return $"you are {wizard.State} and cannot reach for anything";

            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            if (hands.thing != null)
                return FindShelf(wizard, out _)
                    ? null
                    : $"nowhere to set it down - it wants an empty tile with a floor under it, " +
                      $"within {placeInTiles} of you";

            if (Carryable.Nearest(wizard.movement.Position, Of(wizard).reach) == null)
                return Carryable.All.Count == 0
                    ? "there is nothing carryable in this scene - put a Carryable on a slime"
                    : $"nothing within {Of(wizard).reach} boxes of you to take";

            return null;
        }

        public override bool OnCast(PlayerLogic wizard)
        {
            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            if (hands.thing != null)
            {
                if (!FindShelf(wizard, out Vector2Int cell))
                    return false;

                hands.thing.PutDown(TileGrid.CentreOf(cell));
                hands.thing = null;
                return true;
            }

            Carryable found = Carryable.Nearest(wizard.movement.Position, Of(wizard).reach);

            if (found == null)
                return false;

            found.Stow();
            hands.thing = found;
            return true;
        }

        // What is in the slot IS what is in your hands. With nothing stowed it falls back to the
        // spell's own icon.
        public override Sprite IconFor(PlayerLogic wizard)
        {
            Carryable held = wizard.spellbook.StateOf<Hands>(this).thing;

            return held != null && held.Icon != null ? held.Icon : icon;
        }

        public override Color IconTintFor(PlayerLogic wizard)
        {
            Carryable held = wizard.spellbook.StateOf<Hands>(this).thing;

            return held != null ? held.tint : Color.white;
        }

        // Dying reloads the level, which puts everything back where it was authored - so a
        // carried thing is never destroyed, only forgotten. Dropping the SPELL is different:
        // that has to put the thing back, or it is gone with nothing to show where.
        public override void OnRunReset(PlayerLogic wizard) =>
            wizard.spellbook.StateOf<Hands>(this).thing = null;

        public override void OnUnequipped(PlayerLogic wizard)
        {
            Hands hands = wizard.spellbook.StateOf<Hands>(this);

            hands.thing?.GoHome();
            hands.thing = null;
        }

        protected override void Validate()
        {
            if (tiers != null)
                foreach (Tier tier in tiers)
                    tier?.Validate();

            CheckTiers(tiers != null ? tiers.Length : 0);
        }

        bool FindShelf(PlayerLogic wizard, out Vector2Int cell)
        {
            PlayerLogic.Movement walk = wizard.movement;
            Vector2Int stood = TileGrid.StandingCell(walk);

            for (int step = 1; step <= placeInTiles; step++)
            {
                cell = new Vector2Int(stood.x + walk.Facing * step, stood.y + liftInTiles);

                // Empty AND with something under it. Wall Growth wants the opposite test - it
                // puts a block where there ISN'T one - and that contrast is the whole difference
                // between the two spells.
                if (TileGrid.IsShelf(cell, walk.groundLayers))
                    return true;
            }

            cell = default;
            return false;
        }

        Tier Of(PlayerLogic wizard) =>
            TierFor(tiers, wizard.spellbook.RankOf(this)) ?? new Tier();

        [System.Serializable]
        public class Tier
        {
            [Min(1f)] public float reach = 6f;

            public void Validate() => reach = Mathf.Max(1f, reach);
        }

        public class Hands
        {
            public Carryable thing;
        }
    }
}
