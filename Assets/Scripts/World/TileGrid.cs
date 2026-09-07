using FallingWizard.Player;
using UnityEngine;

namespace FallingWizard.World
{
    public static class TileGrid
    {
        const float Inset = 0.45f;

        const float OffTheLine = 0.1f;

        const int LookDown = 6;

        const int StepOver = 2;

        static readonly Collider2D[] Room = new Collider2D[4];
        static readonly RaycastHit2D[] Below = new RaycastHit2D[2];

        static ContactFilter2D Solid(LayerMask groundLayers) => new ContactFilter2D
        {
            useTriggers = false,
            useLayerMask = true,
            layerMask = groundLayers,
        };

        public static Vector2Int CellOf(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));

        public static Vector2 CentreOf(Vector2Int cell) =>
            new Vector2(cell.x + 0.5f, cell.y + 0.5f);

        public static Vector2Int StandingCell(PlayerLogic.Movement walk)
        {
            Vector2 soles = walk.Footing;

            return CellOf(new Vector2(soles.x, soles.y + OffTheLine));
        }

        public static bool IsSolid(Vector2Int cell, LayerMask groundLayers) =>
            Physics2D.OverlapBox(CentreOf(cell), Vector2.one * (Inset * 2f), 0f,
                Solid(groundLayers), Room) > 0;

        public static bool IsFree(Vector2Int cell, LayerMask groundLayers) =>
            !IsSolid(cell, groundLayers);

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

        public static bool RestingCell(int column, int fromRow, LayerMask groundLayers,
            bool mustLand, out Vector2Int cell)
        {
            int row = fromRow;

            for (int climb = 0; climb < StepOver &&
                 IsSolid(new Vector2Int(column, row), groundLayers); climb++)
                row++;

            cell = new Vector2Int(column, row);

            if (IsSolid(cell, groundLayers))
                return false;

            for (int drop = 0; drop < LookDown; drop++)
            {
                if (IsSolid(new Vector2Int(column, row - 1), groundLayers))
                {
                    cell = new Vector2Int(column, row);
                    return true;
                }

                row--;
            }

            cell = new Vector2Int(column, fromRow);
            return !mustLand && IsFree(cell, groundLayers);
        }

        public static bool SurfaceUnder(Vector2Int cell, LayerMask groundLayers, out float top)
        {
            var from = new Vector2(cell.x + 0.5f, cell.y + 0.95f);

            if (Physics2D.Raycast(from, Vector2.down, Solid(groundLayers), Below, LookDown) > 0)
            {
                top = Below[0].point.y;
                return true;
            }

            top = cell.y;
            return false;
        }
    }
}
