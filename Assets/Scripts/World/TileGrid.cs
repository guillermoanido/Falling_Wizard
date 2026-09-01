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

        // How far down a spell will look for the ground before deciding this column has
        // none. Six boxes is further than any step the level asks you to take and short of
        // the drops it wants you to fall down.
        const int LookDown = 6;

        // And how far UP it will carry something to get over what is in the way. A step or a
        // low wall, not a tower - past this the column is simply refused.
        const int StepOver = 2;

        static readonly Collider2D[] Room = new Collider2D[4];
        static readonly RaycastHit2D[] Below = new RaycastHit2D[2];

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

        // The row of the floor a wizard in this cell is stood on, found by LOOKING rather than
        // by counting one row off StandingCell. A spell that counts is only ever as right as
        // that one guess, and one row out is the whole difference between extending a platform
        // and burying a block inside it.
        public static bool FloorRowUnder(Vector2Int from, LayerMask groundLayers, out int row)
        {
            for (int drop = 0; drop < LookDown; drop++)
            {
                row = from.y - drop;

                if (IsSolid(new Vector2Int(from.x, row), groundLayers))
                    return true;
            }

            row = from.y - 1;
            return false;
        }

        // Where something set down in this column comes to rest, starting from the height it
        // was aimed at: OVER whatever is in the way, then DOWN onto the first floor beneath it.
        //
        // That is "on top of the tiles, as close to the floor as possible", and the point of
        // doing it this way is that it does not care whether the row it started from was
        // exactly right. Aiming at the floor row itself used to mean the spell only ever found
        // room at a ledge, and dropped what it was carrying into the empty air past it.
        public static bool RestingCell(int column, int fromRow, LayerMask groundLayers,
            bool mustLand, out Vector2Int cell)
        {
            int row = fromRow;

            for (int climb = 0; climb < StepOver &&
                 IsSolid(new Vector2Int(column, row), groundLayers); climb++)
                row++;

            cell = new Vector2Int(column, row);

            if (IsSolid(cell, groundLayers))
                return false;                   // walled in as high as it will reach

            for (int drop = 0; drop < LookDown; drop++)
            {
                if (IsSolid(new Vector2Int(column, row - 1), groundLayers))
                {
                    cell = new Vector2Int(column, row);
                    return true;
                }

                row--;
            }

            // Nothing under this column within reach. Hang it where it was aimed - being able
            // to set something down over a drop is the whole of `needsAFloor` being off.
            cell = new Vector2Int(column, fromRow);
            return !mustLand && IsFree(cell, groundLayers);
        }

        // The top of the first ground under this cell, MEASURED rather than assumed.
        //
        // mainlev_build.png is sliced 34x35 px on a 32 px grid, so a tile is drawn and collides
        // about a pixel proud of its cell. The grid line is therefore not where anything comes
        // to rest, and by how much is a property of the art - so this casts for the real surface
        // instead of carrying a constant that would go stale the day the sheet is re-sliced.
        public static bool SurfaceUnder(Vector2Int cell, LayerMask groundLayers, out float top)
        {
            var filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = groundLayers,
            };

            // Begun just inside the top of the cell, which the caller has already found to be
            // empty, so the cast cannot start inside the very thing it is looking for - and
            // carried the full LookDown rather than a single box. Stopping at one box meant a
            // cell chosen even one row high found nothing under it, fell back to its own grid
            // line, and left what it was carrying hanging there. Reaching further cannot pick
            // the wrong floor: it reports the FIRST thing it meets on the way down.
            var from = new Vector2(cell.x + 0.5f, cell.y + 0.95f);

            if (Physics2D.Raycast(from, Vector2.down, filter, Below, LookDown) > 0)
            {
                top = Below[0].point.y;
                return true;
            }

            top = cell.y;
            return false;
        }
    }
}
