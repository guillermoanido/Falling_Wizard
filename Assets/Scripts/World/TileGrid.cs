using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    // Cell maths for the spells that put things down. The Grid in Level 1 is 1x1 with a tile
    // anchor of (0.5, 0.5), so a cell's centre sits on a half-integer and its corners on whole
    // ones - which is why CellOf floors and CentreOf adds a half back.
    //
    // Two spells share this and want OPPOSITE answers from it: Wall Growth wants an empty cell,
    // because putting a block where there already is one is the whole spell doing nothing;
    // Telekinesis wants an empty cell with a floor under it, because a slime set down in mid-air
    // just falls.
    public static class TileGrid
    {
        // A hair under half a cell, so the test asks "is anything in this cell" without catching
        // the neighbours it shares an edge with.
        const float Inset = 0.45f;

        // Standing puts the soles exactly on the line between two cells, and floor() on a line
        // is a coin toss decided by float error. Lifting the probe well clear of it - but nowhere
        // near a whole cell - makes the answer the same every frame.
        const float OffTheLine = 0.1f;

        static readonly Collider2D[] Room = new Collider2D[4];

        public static Vector2Int CellOf(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        public static Vector2 CentreOf(Vector2Int cell) =>
            new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        // The cell the wizard's BODY is in - the empty one they are stood in, never the solid
        // one holding them up. Both spells count rows from this, so being one out here put every
        // block and every hazard a row into the floor: Telekinesis reported every tile ahead as
        // filled because it was asking about the ground itself, and Wall Growth hunted for a lip
        // in the rock BELOW the floor and never found one.
        //
        // Movement.Footing is the collider's own underside. It used to be FeetY + 0.05, which is
        // the ground PROBE lifted by the default skin - two guesses that only agree while nobody
        // resizes the wizard.
        public static Vector2Int StandingCell(PlayerLogic.Movement walk)
        {
            Vector2 soles = walk.Footing;

            return CellOf(new Vector2(soles.x, soles.y + OffTheLine));
        }

        public static bool IsSolid(Vector2Int cell, LayerMask groundLayers)
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = groundLayers,
            };

            return Physics2D.OverlapBox(CentreOf(cell), Vector2.one * (Inset * 2f), 0f,
                filter, Room) > 0;
        }

        public static bool IsFree(Vector2Int cell, LayerMask groundLayers) =>
            !IsSolid(cell, groundLayers);

        // Somewhere you could set an object down: nothing in the cell, and something under it.
        public static bool IsShelf(Vector2Int cell, LayerMask groundLayers) =>
            IsFree(cell, groundLayers) &&
            IsSolid(new Vector2Int(cell.x, cell.y - 1), groundLayers);

        // The surface something standing in this cell actually rests on - which is NOT the
        // grid line.
        //
        // mainlev_build.png is sliced 34x35 px on a 32 px grid, so every tile is drawn, and
        // collides, one pixel proud of its cell on all four sides. That inflated surface is what
        // the rest of the world has to line up with: a prop built to the bare grid sits a pixel
        // low, and the wizard catches on the step walking back onto the tilemap - which reads as
        // the spell having dropped its block into the floor.
        //
        // Re-slice the sheet to 32x32 one day and this becomes 0. Nothing else has to change.
        public const float TileBleed = 1f / 32f;

        public static float FloorOf(Vector2Int cell) => cell.y + TileBleed;
    }
}
