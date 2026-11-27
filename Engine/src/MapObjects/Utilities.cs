using System;
using Model.Core.Mapping;

namespace RhyCiv.Engine.MapObjects
{
    public static class Utilities
    {
        /// <summary>
        /// Compute the Euclidean distance between two tiles -- the same unit on
        /// both map kinds (rhYciv#127: a flat map used to return the square, so
        /// the corruption/waste DistanceFactor read 100 for a city 10 squares
        /// from the capital and capped at 32/16 almost everywhere).
        /// </summary>
        /// <param name="startTile"></param>
        /// <param name="tile"></param>
        /// <returns></returns>
        internal static double DistanceTo(Tile startTile, Tile tile)
        {
            if(startTile == tile) return 0;
            if (startTile.Map.Flat)
            {
                // No wrap on a flat earth; otherwise the plain Euclidean distance.
                return Math.Sqrt(Math.Pow(startTile.X - tile.X, 2) + Math.Pow(startTile.Y - tile.Y, 2));
            }
            var diffX = Math.Abs(startTile.X - tile.X);
            var diffY = startTile.Y - tile.Y;
            return Math.Sqrt(Math.Pow(Math.Min(diffX, startTile.Map.XDim - diffX), 2) + Math.Pow(diffY, 2));
        }
    }
}