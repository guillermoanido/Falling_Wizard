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
                 "first one that will have it.")]
        [Range(1, 6)] public int placeInTiles = 3;

        [Tooltip("Which ROW, counted from the one your body is in. 0 is the row you are stood " +
                 "in, so it goes down on the same floor you are walking on. 1 is head height, " +
                 "-1 puts it in the floor and is only useful over a drop.")]
        [Range(-2, 2)] public int liftInTiles = 0;

        [Tooltip("Insist on a floor underneath. ON, it only goes somewhere it can rest, which " +
                 "means at a drop it walks out to the far side and can look like it refuses to " +
                 "put things down nearby. OFF, it goes wherever there is room - including out " +
                 "over a gap, where a slime hangs in mid-air and becomes a platform you aimed. " +
                 "Off is the more useful spell; on is the tidier one.")]
        public bool needsAFloor = false;

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
                    : needsAFloor
                        ? $"nowhere to rest it - it wants an empty tile WITH A FLOOR under it " +
                          $"within {placeInTiles} of you. Turn off 'needs a floor' to set it " +
                          "down over a drop"
                        : $"every tile within {placeInTiles} ahead of you is already filled";

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

                hands.thing.PutDownOn(TileGrid.CentreOf(cell).x, TileGrid.FloorOf(cell));
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

                // With needsAFloor off this is just "is there room", which is what makes the
                // spell feel like placing rather than like asking permission. A hazard set down
                // over a gap simply hangs there - none of them fall, they are triggers - and a
                // slime hung over a drop is a trampoline the player put where they wanted it.
                bool willHaveIt = needsAFloor
                    ? TileGrid.IsShelf(cell, walk.groundLayers)
                    : TileGrid.IsFree(cell, walk.groundLayers);

                if (willHaveIt)
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
