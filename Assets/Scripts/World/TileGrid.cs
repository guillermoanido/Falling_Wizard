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

        static readonly Collider2D[] Room = new Collider2D[4];

        public static Vector2Int CellOf(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        public static Vector2 CentreOf(Vector2Int cell) =>
            new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        // The cell the wizard's FEET are in, not their middle. Standing on a floor puts the feet
        // fractionally inside the tile below, so the probe is lifted a whisker first.
        public static Vector2Int StandingCell(PlayerLogic.Movement walk) =>
            CellOf(new Vector2(walk.Position.x, walk.FeetY + 0.05f));

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
    }
}
