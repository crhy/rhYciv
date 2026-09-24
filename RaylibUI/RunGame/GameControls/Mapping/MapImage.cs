using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Core;
using Model.Core.Mapping;
using Model.Images;
using Model.ImageSets;
using RaylibUtils;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Transformations;
using Raylib_CSharp.Images;

namespace RaylibUI.RunGame.GameControls.Mapping;

public static class MapImage
{
    private static Color _replacementColour = new (255, 0, 0, 255);
    
    private static readonly (int, int)[][] CoastMap = {
        new[]{ (0,4), (3,1) }, 
        new[]{ (3,2) }, 
        new[]{ (3,4), (1,1) }, 
        new[]{ (1,2) }, 
        new[]{ (1,4), (2,1) },
        new[]{ (2,2) },
        new[]{ (2,4), (0,1) },
        new[]{ (0,2) }
    };

    public static Rectangle TileRec = new (0, 0, 64, 32);

    internal static TileDetails MakeTileGraphic(Tile tile, Map map,
        TerrainSet terrainSet, IGame game, int civilizationId)
    {
        var directNeighbours = map.DirectNeighbours(tile, true).ToArray();

        var neighbours = map.Neighbours(tile, nullForInvalid: true).ToArray();

        // For an ocean tile, when the marching-squares coastline set is loaded,
        // the whole tile is one of 16 pre-rendered diamonds chosen by the land
        // state of its four vertices (N=8, E=4, S=2, W=1). Each vertex is land
        // if any of the three tiles that meet there is land. neighbours are in
        // NE, E, SE, S, SW, W, NW, N order.
        var useShore = tile.Type == TerrainType.Ocean
                       && terrainSet.HighResBaseTiles
                       && terrainSet.Sea.Length == 16
                       && terrainSet.Shore.Length == 256;
        var useMarchCoast = !useShore
                            && tile.Type == TerrainType.Ocean
                            && terrainSet.HighResBaseTiles
                            && terrainSet.CoastMarch.Length == 16;
        Image tilePic;
        if (useShore)
        {
            tilePic = ShoreTile(tile, map, terrainSet, directNeighbours, neighbours, civilizationId);
        }
        else if (useMarchCoast)
        {
            bool Land(int i) => neighbours[i] is { Type: not TerrainType.Ocean } n
                                && (n.IsVisible(civilizationId) || map.MapRevealed);
            var north = Land(0) || Land(7) || Land(6);
            var east = Land(0) || Land(1) || Land(2);
            var south = Land(2) || Land(3) || Land(4);
            var west = Land(4) || Land(5) || Land(6);
            var mask = (north ? 8 : 0) | (east ? 4 : 0) | (south ? 2 : 0) | (west ? 1 : 0);
            tilePic = Images.ExtractBitmap(terrainSet.CoastMarch[mask]).Copy();
        }
        else
        {
            var variant = terrainSet.VariantOf(tile);
            var paintings = terrainSet.BaseTileVariants.Length > variant
                ? terrainSet.BaseTileVariants[variant]
                : terrainSet.BaseTiles;
            tilePic = Images.ExtractBitmap(paintings[(int)tile.Type]).Copy();
        }

        // Dither
        if (tile.Type != TerrainType.Ocean)
        {
            for (var index = 0; index < directNeighbours.Length; index++)
            {
                var neighbour = directNeighbours[index];
                if (neighbour != null)
                {
                    // The shore draws this tile's own terrain into the sea tile,
                    // so land no longer takes on a band of grass towards the sea
                    // (a desert coast used to have one).
                    var shoreDrawn = neighbour.Type == TerrainType.Ocean && terrainSet.Shore.Length == 256;
                    if ((neighbour.IsVisible(civilizationId) || map.MapRevealed) && !shoreDrawn)
                    {
                        var theirVariant = terrainSet.VariantOf(neighbour);
                        var theirMaps = terrainSet.DitherMapVariants.Length > theirVariant
                            ? terrainSet.DitherMapVariants[theirVariant]
                            : terrainSet.DitherMaps;
                        if (neighbour.Type == tile.Type && theirVariant != terrainSet.VariantOf(tile)
                                                        && neighbour.Type != TerrainType.Ocean)
                        {
                            // The same terrain, painted differently: blend the join
                            // just as a join between two terrains is blended.
                            DrawLayer(tilePic, theirMaps[index].Images[(int)neighbour.Type],
                                new Rectangle(theirMaps[index].X, theirMaps[index].Y, 32, 16));
                            continue;
                        }

                        ApplyDither(tilePic, neighbour.Type, tile.Type, theirMaps[index]);
                    }
                }
            }
        }
        else if (useShore || useMarchCoast)
        {
            // The coastline sprite already carries sand, surf and open water;
            // nothing else is composited on the water body.
        }
        else
        {
            if (terrainSet.HighResBaseTiles && terrainSet.ShallowEdge.Length == 4)
            {
                // The classic coast sprites are a bright pixel-art stipple that
                // reads as a diagonal net over the photographic water. Instead,
                // paint a procedural shoreline along each diagonal edge that
                // meets land, picking a variant per tile so a long coast does
                // not repeat.
                for (var index = 0; index < directNeighbours.Length; index++)
                {
                    var neighbour = directNeighbours[index];
                    if (neighbour != null && neighbour.Type != TerrainType.Ocean
                        && (neighbour.IsVisible(civilizationId) || map.MapRevealed))
                    {
                        var variants = terrainSet.ShallowEdge[index];
                        if (variants.Length == 0)
                        {
                            continue;
                        }

                        var pick = (int)((uint)(tile.XIndex * 92821 + tile.Y * 68917 + index * 40507)
                                         % (uint)variants.Length);
                        DrawLayer(tilePic, variants[pick], TileRec);
                    }
                }
            }
            else
            {
                //drawCoasts
                var coastIndex = new[] { 0, 0, 0, 0 };
                foreach (var (neighbour, ind) in neighbours.Zip(CoastMap))
                {
                    if (neighbour != null && neighbour.Type != TerrainType.Ocean)
                    {
                        foreach (var (index, valueVariable) in ind)
                        {
                            coastIndex[index] += valueVariable;
                        }
                    }
                }

                // NW+N+NE tiles
                DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Coast[coastIndex[0], 0]),
                    new Rectangle(16, 0, 32, 16));

                // SW+S+SE tiles
                DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Coast[coastIndex[1], 1]),
                    new Rectangle(16, 16, 32, 16));

                // SW+W+NW tiles
                DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Coast[coastIndex[2], 2]),
                    new Rectangle(0, 8, 32, 16));

                // NE+E+SE tiles
                DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Coast[coastIndex[3], 3]),
                    new Rectangle(32, 8, 32, 16));
            }
        }

        // River mouth: if a river runs into this ocean tile, draw its mouth here.
        if (tile.Type == TerrainType.Ocean)
        {
            for (var index = 0; index < directNeighbours.Length; index++)
            {
                var neighbour = directNeighbours[index];
                if (neighbour is { River: true })
                {
                    DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.RiverMouth[index]), TileRec);
                }
            }
        }

        if (tile.Type is TerrainType.Forest or TerrainType.Hills or TerrainType.Mountains)
        {
            var index = 0;
            var increment = 1;
            foreach (var neighbour in directNeighbours)
            {
                if (neighbour != null && neighbour.Type == tile.Type)
                {
                    index += increment;
                }

                increment *= 2;
            }

            DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.ImagesFor(tile.Type)[index]), TileRec);
        }

        // Draw rivers
        if (tile.River)
        {
            var index = 0;
            var increment = 1;
            foreach (var neighbour in directNeighbours)
            {
                if (neighbour != null && (neighbour.River || neighbour.Type == TerrainType.Ocean))
                {
                    index += increment;
                }

                increment *= 2;
            }

            DrawLayer(tilePic, Images.ExtractBitmap(RiverTile(terrainSet, tile, index)), TileRec);
        }

        // Draw shield for grasslands
        if (tile.Type == TerrainType.Grassland)
        {
            if (tile.HasShield)
            {
                DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.GrasslandShield), TileRec);
            }
        }
        else if (tile.Special != -1)
        {
            // Draw special resources if they exist
            DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Specials[tile.Special][(int)tile.Type]), TileRec);
        }

        if(tile.HasGoodyHut)
        {
            // Add a goody hut if it exists on this tile.
            DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Huts), TileRec);
        }    

        var tileDetails = new TileDetails { Image = tilePic };
        var playerKnowledge = tile.PlayerKnowledge != null && tile.PlayerKnowledge.Length > civilizationId
            ? tile.PlayerKnowledge[civilizationId]
            : null;

        if (tile.Map.MapRevealed || playerKnowledge != null)
        {
            var improvements =
                (tile.Map.MapRevealed ? tile.Improvements : playerKnowledge!.Improvements)
                .Where(ci => game.TerrainImprovements.ContainsKey(ci.Improvement))
                .OrderBy(ci => game.TerrainImprovements[ci.Improvement].Layer).ToList();

            foreach (var construct in improvements)
            {
                var improvement = game.TerrainImprovements[construct.Improvement];
                var graphics = terrainSet.ImprovementsMap[construct.Improvement];

                if (improvement.HasMultiTile)
                {
                    bool hasNeighbours = false;

                    for (int i = 0; i < neighbours.Length; i++)
                    {
                        var neighbour = neighbours[i];

                        var neighboringImprovement =
                            neighbour?.Improvements.FirstOrDefault(i =>
                                i.Improvement == construct.Improvement);
                        if (neighboringImprovement != null)
                        {
                            var index = i + 1;
                            if (index != -1)
                            {
                                if (neighboringImprovement.Level < construct.Level)
                                {
                                    DrawLayer(tilePic,
                                        Images.ExtractBitmap(graphics.Levels[neighboringImprovement.Level, index]), TileRec);
                                }
                                else
                                {
                                    hasNeighbours = true;
                                    DrawLayer(tilePic, Images.ExtractBitmap(graphics.Levels[construct.Level, index]),
                                        TileRec);
                                }
                            }
                        }
                    }

                    if (!hasNeighbours)
                    {
                        if (tile.CityHere is null)
                        {
                            DrawLayer(tilePic, Images.ExtractBitmap(graphics.Levels[construct.Level, 0]), TileRec);
                        }
                    }
                }
                else if (playerKnowledge?.CityHere is not null)
                {
                    if (tile.Map.DirectNeighbours(tile)
                        .Any(t => t.Improvements.Any(i => i.Improvement == construct.Improvement)))
                    {
                        DrawLayer(tilePic, Images.ExtractBitmap(graphics.Levels[construct.Level, 0]), TileRec);
                    }
                }
                else
                {
                    if (improvement.HideUnits != -1)
                    {
                        tileDetails.ForegroundElement = new UnitHidingImprovement
                        {
                            UnitDomain = (UnitGas)improvement.HideUnits,
                            UnitImage = new MemoryStorage(Images.ExtractBitmap(graphics.UnitLevels[construct.Level, 0]), improvement.Name,
                                _replacementColour),
                            Image = new MemoryStorage(Images.ExtractBitmap(graphics.Levels[construct.Level, 0]), improvement.Name,
                                _replacementColour)
                        };
                    }
                    else if (improvement.Foreground)
                    {
                        tileDetails.ForegroundElement = new ForegroundImprovement
                        {
                            Image = new MemoryStorage(Images.ExtractBitmap(graphics.Levels[construct.Level, 0]), improvement.Name)
                        };
                    }
                    else
                    {
                        DrawLayer(tilePic, Images.ExtractBitmap(graphics.Levels[construct.Level, 0]), TileRec);
                    }
                }
            }
        }

        // Softening at the edge of what has been explored.
        //
        // The mask for this is a 32x16 checkerboard, drawn when Civ II's tiles were
        // 64x32 and a chequer of alternating pixels read as a shade. Terrain is
        // composed at several times that size now, so the same mask is stretched
        // until each of its pixels is a block several across -- and what was a
        // shade became a coarse dark patch covering a quarter of the square. On
        // every square along the frontier. That is the "weird diamond shadows",
        // and they follow the edge of the explored map because that is exactly
        // where this is drawn.
        //
        // Above classic resolution the softening is left out. It was never doing
        // anything the eye reads as softening there, and the edge of the known
        // world is a clean isometric boundary without it.
        if (terrainSet.RenderScale <= 1)
        {
            for (var index = 0; index < directNeighbours.Length; index++)
            {
                var directNeighbour = directNeighbours[index];
                if (directNeighbour != null && !(directNeighbour.IsVisible(civilizationId) || map.MapRevealed)) // Don't dither edge of map (neighbour=null)
                {
                    var ditherMap = terrainSet.DitherMaps[index];
                    DrawLayer(tilePic, ditherMap.Images[^1],
                        new Rectangle(ditherMap.X, ditherMap.Y, 32, 16));
                }
            }
        }

        return tileDetails;
    }

    private static void ApplyDither(Image origImg, TerrainType neighbourType, TerrainType tileType,
        DitherMap ditherMap)
    {
        if (neighbourType == TerrainType.Ocean)
        {
            neighbourType = TerrainType.Grassland;
        }

        if (neighbourType == tileType) return;
        DrawLayer(origImg, ditherMap.Images[(int)neighbourType],
            new Rectangle(ditherMap.X, ditherMap.Y, 32, 16));
    }

    /// <summary>
    /// A sea tile: the open sea for its place in the 4x4 block the sea repeats
    /// over, then the land it touches -- that land's own terrain, cut to the
    /// shape of the shore -- and the beach, surf and shallows over both.
    /// </summary>
    /// <remarks>
    /// Land comes in along each edge whose neighbour is land, and as a rounded
    /// point where land only touches a corner; see scripts/prepare_coast_tiles.py
    /// for why neighbouring sea tiles' shores then meet exactly.
    /// </remarks>
    private static Image ShoreTile(Tile tile, Map map, TerrainSet terrainSet,
        Tile?[] directNeighbours, Tile?[] neighbours, int civilizationId)
    {
        var i = (((tile.X + tile.Y) / 2) % 4 + 4) % 4;
        var j = (((tile.X - tile.Y) / 2) % 4 + 4) % 4;
        var tilePic = Images.ExtractBitmap(terrainSet.Sea[i * 4 + j]).Copy();

        bool IsLand(Tile? neighbour) => neighbour is { Type: not TerrainType.Ocean }
                                        && (neighbour.IsVisible(civilizationId) || map.MapRevealed);

        var mask = 0;
        Tile? landTile = null;
        for (var edge = 0; edge < 4 && edge < directNeighbours.Length; edge++)
        {
            if (IsLand(directNeighbours[edge]))
            {
                mask |= 1 << edge;
                landTile ??= directNeighbours[edge];
            }
        }

        // Neighbours run NE, E, SE, S, SW, W, NW, N: the corners are N, E, S, W.
        int[] cornerIndex = [7, 1, 3, 5];
        for (var corner = 0; corner < 4; corner++)
        {
            if (cornerIndex[corner] < neighbours.Length && IsLand(neighbours[cornerIndex[corner]]))
            {
                mask |= 1 << (4 + corner);
                landTile ??= neighbours[cornerIndex[corner]];
            }
        }

        if (mask == 0 || landTile == null)
        {
            return tilePic;
        }

        var landType = landTile.Type;
        var land = Images.ExtractBitmap(terrainSet.BaseTiles[(int)landType]).Copy();
        for (var edge = 0; edge < 4 && edge < directNeighbours.Length; edge++)
        {
            var neighbour = directNeighbours[edge];
            if (IsLand(neighbour) && neighbour!.Type != landType && edge < terrainSet.DitherMaps.Length)
            {
                ApplyDither(land, neighbour.Type, landType, terrainSet.DitherMaps[edge]);
            }
        }

        land.AlphaMask(Images.ExtractBitmap(terrainSet.ShoreLand[mask]));
        DrawLayer(tilePic, land, TileRec);
        land.Unload();
        DrawLayer(tilePic, Images.ExtractBitmap(terrainSet.Shore[mask]), TileRec);
        return tilePic;
    }

    /// <summary>
    /// The river picture for this tile: the right connection shape, at the gauge
    /// its distance from the sea calls for.
    /// </summary>
    /// <remarks>
    /// A river used to be one width from its spring to its mouth, because there
    /// was one picture per connection shape and nothing to choose between them.
    /// <see cref="Tile.RiverFlow"/> counts tiles along the watercourse from the
    /// sea, so the mouth is an estuary, the next stretch a river, then a stream,
    /// and the far inland end a trickle. Falls back to the unbanded picture when
    /// the banded art is not installed.
    /// </remarks>
    private static IImageSource RiverTile(TerrainSet terrainSet, Tile tile, int mask)
    {
        var bands = terrainSet.RiverBands;
        if (bands.Length == 0)
        {
            return terrainSet.River[mask];
        }

        // Distance 0 is the mouth and takes the widest band; each step inland
        // narrows it by one until the trickle, which everything beyond keeps.
        var flow = tile.RiverFlow;
        var band = flow < 0 ? 1 : Math.Max(0, bands.Length - 1 - flow);
        return bands[band][mask];
    }

    private static void DrawLayer(Image target, Image layer, Rectangle logicalDestination)
    {
        var scaleX = target.Width / TileRec.Width;
        var scaleY = target.Height / TileRec.Height;
        var destination = new Rectangle(
            logicalDestination.X * scaleX,
            logicalDestination.Y * scaleY,
            logicalDestination.Width * scaleX,
            logicalDestination.Height * scaleY);

        // Small classic-sheet sprites (shields, special resources, huts) go onto
        // a tile composed several times larger than 64x32. Left to ImageDraw's
        // bilinear scaling they come out badly blurred; point-scale them up
        // first so they stay crisp against the high-resolution terrain.
        if (layer.Width + 0.5f < destination.Width && layer.Height + 0.5f < destination.Height)
        {
            var crispWidth = (int)MathF.Round(destination.Width);
            var crispHeight = (int)MathF.Round(destination.Height);
            var crisp = layer.Copy();
            crisp.ResizeNN(crispWidth, crispHeight);
            target.Draw(crisp, new Rectangle(0, 0, crispWidth, crispHeight),
                new Rectangle(destination.X, destination.Y, crispWidth, crispHeight), Color.White);
            crisp.Unload();
            return;
        }

        var source = new Rectangle(0, 0, layer.Width, layer.Height);
        target.Draw(layer, source, destination, Color.White);
    }
}
