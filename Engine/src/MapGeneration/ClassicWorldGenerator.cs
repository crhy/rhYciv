using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.Mapping;

namespace RhyCiv.Engine;

internal sealed record GeneratedWorld(TerrainType[,] Terrain, bool[,] Rivers);

/// <summary>
/// Generates Civ II-style worlds in distinct passes: land shape, climate,
/// erosion/roughness, then connected rivers. Keeping these passes separate is
/// important because the Customize World settings describe different aspects
/// of a world rather than independent per-tile probabilities.
/// </summary>
internal static class ClassicWorldGenerator
{
    private readonly record struct Cell(int X, int Y);

    internal static GeneratedWorld Generate(GameInitializationConfig config, int width, int height)
    {
        var wrapX = !config.FlatWorld;
        var shapeNoise = CreateNoise(config.Random, width, height, wrapX, 5);
        var elevation = CreateElevation(config, width, height, wrapX, shapeNoise);
        var land = SelectLand(config, elevation, width, height, wrapX);

        var moisturePasses = config.Age switch { 0 => 6, 2 => 2, _ => 4 };
        var moisture = CreateNoise(config.Random, width, height, wrapX, moisturePasses);
        var temperature = CreateNoise(config.Random, width, height, wrapX, 4);
        var roughness = CreateNoise(config.Random, width, height, wrapX,
            config.Age switch { 0 => 5, 2 => 2, _ => 3 });

        var terrain = ClassifyTerrain(config, land, elevation, moisture, temperature, roughness,
            width, height);
        var rivers = GenerateRivers(config, terrain, elevation, width, height, wrapX);
        return new GeneratedWorld(terrain, rivers);
    }

    private static double[,] CreateElevation(GameInitializationConfig config, int width, int height,
        bool wrapX, double[,] shapeNoise)
    {
        var elevation = new double[width, height];
        var area = width * height;
        var targetRatio = GetLandRatio(config.PropLand);
        var anchorCount = config.Landform switch
        {
            0 => Math.Max(8, area / 350),  // Archipelago
            2 => 2,                       // Continents
            _ => Math.Max(4, area / 900)  // Varied
        };
        var targetPerAnchor = area * targetRatio / anchorCount;
        var baseRadius = Math.Max(3.0, Math.Sqrt(targetPerAnchor / Math.PI));

        for (var anchor = 0; anchor < anchorCount; anchor++)
        {
            var marginX = wrapX ? 0 : Math.Max(2, width / 12);
            var centreX = config.Random.Next(marginX, Math.Max(marginX + 1, width - marginX));
            var centreY = config.Random.Next(Math.Max(2, height / 9), Math.Max(3, height - height / 9));

            var landformScale = config.Landform switch { 0 => 0.72, 2 => 1.35, _ => 1.0 };
            var radiusX = baseRadius * landformScale * (1.15 + config.Random.NextFloat() * 0.85);
            var radiusY = baseRadius * landformScale * (0.75 + config.Random.NextFloat() * 0.65);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var dx = Math.Abs(x - centreX);
                    if (wrapX) dx = Math.Min(dx, width - dx);
                    var dy = Math.Abs(y - centreY);
                    var distance = Math.Sqrt(dx * dx / (radiusX * radiusX) + dy * dy / (radiusY * radiusY));
                    var warpedDistance = distance - (shapeNoise[x, y] - 0.5) * 0.9;
                    var influence = Math.Max(0, 1.45 - warpedDistance) / 1.45;
                    influence *= influence;
                    elevation[x, y] += influence;
                }
            }

            // Long, wandering ridges create peninsulas and irregular coastlines instead of ellipses.
            var branches = config.Landform switch { 0 => 2, 2 => 7, _ => 4 };
            for (var branch = 0; branch < branches; branch++)
            {
                var x = centreX;
                var y = centreY;
                var direction = config.Random.Next(8);
                var length = Math.Max(4, (int)(baseRadius * (1.1 + config.Random.NextFloat() * 1.8)));
                for (var step = 0; step < length; step++)
                {
                    PaintElevation(elevation, x, y, width, height, wrapX,
                        config.Landform == 0 ? 1 : 2, 0.28);
                    if (config.Random.Next(100) < 32)
                    {
                        direction = (direction + (config.Random.NextBool() ? 1 : 7)) % 8;
                    }
                    var (dx, dy) = Direction(direction);
                    x += dx;
                    y += dy;
                    if (wrapX) x = Wrap(x, width);
                    else x = Math.Clamp(x, 1, width - 2);
                    y = Math.Clamp(y, 2, height - 3);
                }
            }
        }

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                elevation[x, y] += shapeNoise[x, y] * 0.22;
            }
        }
        return elevation;
    }

    private static bool[,] SelectLand(GameInitializationConfig config, double[,] elevation, int width,
        int height, bool wrapX)
    {
        var land = new bool[width, height];
        var candidates = new List<(double Height, Cell Cell)>(width * height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (config.FlatWorld && (x == 0 || x == width - 1 || y == 0 || y == height - 1))
                    continue;
                if (!config.FlatWorld && (y == 0 || y == height - 1))
                    continue;
                candidates.Add((elevation[x, y], new Cell(x, y)));
            }
        }

        var polarTiles = config.FlatWorld ? 0 : width * 2;
        var desiredLand = Math.Clamp((int)Math.Round(width * height * GetLandRatio(config.PropLand)) - polarTiles,
            1, candidates.Count);
        foreach (var candidate in candidates.OrderByDescending(c => c.Height).Take(desiredLand))
        {
            land[candidate.Cell.X, candidate.Cell.Y] = true;
        }

        SmoothLand(land, width, height, wrapX, config.FlatWorld);
        if (!config.FlatWorld)
        {
            for (var x = 0; x < width; x++)
            {
                land[x, 0] = true;
                land[x, height - 1] = true;
            }
        }
        return land;
    }

    private static void SmoothLand(bool[,] land, int width, int height, bool wrapX, bool flatWorld)
    {
        var next = (bool[,])land.Clone();
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (flatWorld && (x == 0 || x == width - 1)) continue;
                var neighbours = Neighbours(x, y, width, height, wrapX).Count(cell => land[cell.X, cell.Y]);
                if (!land[x, y] && neighbours >= 6) next[x, y] = true;
                else if (land[x, y] && neighbours <= 1) next[x, y] = false;
            }
        }
        Array.Copy(next, land, next.Length);
    }

    private static TerrainType[,] ClassifyTerrain(GameInitializationConfig config, bool[,] land,
        double[,] elevation, double[,] moisture, double[,] temperature, double[,] roughness,
        int width, int height)
    {
        var terrain = new TerrainType[width, height];
        var warmthBias = config.Temperature switch { 0 => -0.14, 2 => 0.14, _ => 0.0 };
        var moistureBias = config.Climate switch { 0 => -0.20, 2 => 0.20, _ => 0.0 };
        var mountainThreshold = config.Age switch { 0 => 0.74, 2 => 0.90, _ => 0.82 };
        var hillThreshold = config.Age switch { 0 => 0.57, 2 => 0.72, _ => 0.64 };

        var landHeights = new List<double>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            if (land[x, y]) landHeights.Add(elevation[x, y]);
        var minElevation = landHeights.Count == 0 ? 0 : landHeights.Min();
        var maxElevation = landHeights.Count == 0 ? 1 : landHeights.Max();
        var elevationRange = Math.Max(0.001, maxElevation - minElevation);

        for (var y = 0; y < height; y++)
        {
            var latitude = Math.Abs(y / (double)Math.Max(1, height - 1) - 0.5) * 2.0;
            for (var x = 0; x < width; x++)
            {
                if (!land[x, y])
                {
                    terrain[x, y] = TerrainType.Ocean;
                    continue;
                }

                var heat = 1.0 - latitude + warmthBias + (temperature[x, y] - 0.5) * 0.20;
                var wet = Math.Clamp(moisture[x, y] + moistureBias, 0, 1);
                var normalizedElevation = (elevation[x, y] - minElevation) / elevationRange;
                var rugged = roughness[x, y] * 0.72 + normalizedElevation * 0.28;

                if (y == 0 || y == height - 1 || heat < 0.08)
                    terrain[x, y] = TerrainType.Glacier;
                else if (heat < 0.22)
                    terrain[x, y] = wet > 0.62 ? TerrainType.Forest : TerrainType.Tundra;
                else if (rugged > mountainThreshold)
                    terrain[x, y] = TerrainType.Mountains;
                else if (rugged > hillThreshold)
                    terrain[x, y] = TerrainType.Hills;
                else if (heat > 0.72 && wet > 0.68)
                    terrain[x, y] = TerrainType.Jungle;
                else if (heat > 0.56 && wet > 0.82)
                    terrain[x, y] = TerrainType.Swamp;
                else if (heat > 0.66 && wet < 0.34)
                    terrain[x, y] = TerrainType.Desert;
                else if (wet > 0.65)
                    terrain[x, y] = TerrainType.Forest;
                else if (wet < 0.34)
                    terrain[x, y] = heat > 0.54 ? TerrainType.Desert : TerrainType.Plains;
                else
                    terrain[x, y] = wet > 0.50 ? TerrainType.Grassland : TerrainType.Plains;
            }
        }
        return terrain;
    }

    private static bool[,] GenerateRivers(GameInitializationConfig config, TerrainType[,] terrain,
        double[,] elevation, int width, int height, bool wrapX)
    {
        var rivers = new bool[width, height];
        var distanceToOcean = DistanceToOcean(terrain, width, height, wrapX);
        var sources = new List<Cell>();
        var landCount = 0;
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            if (terrain[x, y] == TerrainType.Ocean) continue;
            landCount++;
            if (distanceToOcean[x, y] >= 3 && terrain[x, y] is not TerrainType.Glacier and not TerrainType.Mountains)
                sources.Add(new Cell(x, y));
        }

        // How much of the land carries a river, by climate. Counted off the same
        // twenty Civ II worlds as the land ratio above: they run 2.3% to 3.4% of
        // their land, so an ordinary world is a little under three in a hundred.
        var riverRatio = config.Climate switch { 0 => 0.014, 2 => 0.045, _ => 0.028 };
        var targetTiles = Math.Max(1, (int)Math.Round(landCount * riverRatio));
        var riverTiles = 0;
        var attempts = 0;
        while (riverTiles < targetTiles && sources.Count > 0 && attempts++ < targetTiles * 20)
        {
            var source = sources[config.Random.Next(sources.Count)];
            if (rivers[source.X, source.Y]) continue;

            var path = new List<Cell>();
            var visited = new HashSet<Cell>();
            var current = source;
            var maximumLength = Math.Min(24, distanceToOcean[source.X, source.Y] * 3 + 4);
            for (var step = 0; step < maximumLength; step++)
            {
                if (!visited.Add(current)) break;
                path.Add(current);
                if (distanceToOcean[current.X, current.Y] <= 1) break;

                var choices = EdgeNeighbours(current.X, current.Y, width, height, wrapX)
                    .Where(cell => terrain[cell.X, cell.Y] != TerrainType.Ocean && !visited.Contains(cell))
                    .Where(cell => terrain[cell.X, cell.Y] is not TerrainType.Glacier and not TerrainType.Mountains)
                    .Select(cell => new
                    {
                        Cell = cell,
                        Score = distanceToOcean[cell.X, cell.Y] * 3.0 + elevation[cell.X, cell.Y] * 0.25 +
                                config.Random.NextFloat() * 1.4
                    })
                    .OrderBy(choice => choice.Score)
                    .ToList();
                if (choices.Count == 0) break;
                current = choices[0].Cell;
                if (rivers[current.X, current.Y])
                {
                    path.Add(current);
                    break;
                }
            }

            if (path.Count < 3 || distanceToOcean[path[^1].X, path[^1].Y] > 1 && !rivers[path[^1].X, path[^1].Y])
                continue;
            foreach (var cell in path)
            {
                if (!rivers[cell.X, cell.Y]) riverTiles++;
                rivers[cell.X, cell.Y] = true;
            }
        }
        return rivers;
    }

    private static int[,] DistanceToOcean(TerrainType[,] terrain, int width, int height, bool wrapX)
    {
        var distance = new int[width, height];
        var queue = new Queue<Cell>();
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            distance[x, y] = int.MaxValue;
            if (terrain[x, y] != TerrainType.Ocean) continue;
            distance[x, y] = 0;
            queue.Enqueue(new Cell(x, y));
        }
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            // Edge-sharing, to match the river walk: "next to the sea" has to mean
            // the same thing to the generator as it does to the renderer, or a river
            // stops one tile short of a mouth it thinks it has reached.
            foreach (var neighbour in EdgeNeighbours(current.X, current.Y, width, height, wrapX))
            {
                if (distance[neighbour.X, neighbour.Y] <= distance[current.X, current.Y] + 1) continue;
                distance[neighbour.X, neighbour.Y] = distance[current.X, current.Y] + 1;
                queue.Enqueue(neighbour);
            }
        }
        return distance;
    }

    private static double[,] CreateNoise(FastRandom random, int width, int height, bool wrapX, int passes)
    {
        var values = new double[width, height];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            values[x, y] = random.NextFloat();

        for (var pass = 0; pass < passes; pass++)
        {
            var next = new double[width, height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var total = values[x, y] * 4;
                var weight = 4;
                foreach (var neighbour in Neighbours(x, y, width, height, wrapX))
                {
                    total += values[neighbour.X, neighbour.Y];
                    weight++;
                }
                next[x, y] = total / weight;
            }
            values = next;
        }
        return Normalize(values, width, height);
    }

    private static double[,] Normalize(double[,] values, int width, int height)
    {
        var minimum = values.Cast<double>().Min();
        var maximum = values.Cast<double>().Max();
        var range = Math.Max(0.000001, maximum - minimum);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            values[x, y] = (values[x, y] - minimum) / range;
        return values;
    }

    /// <summary>
    /// The four tiles that share an <em>edge</em> with this one: NE, SE, SW, NW.
    /// </summary>
    /// <remarks>
    /// These are the offsets <c>MapNavigationFunctions.DirectNeighbours</c> yields,
    /// and they are the only steps a river may take. A river is drawn as one
    /// picture per tile chosen by which of those four neighbours also carry a
    /// river, so two river tiles that share only a <em>corner</em> cannot be joined
    /// by any picture: neither appears in the other's mask, and both are drawn as a
    /// stub running to an edge with nothing on the far side.
    ///
    /// The river walk used the eight-neighbour set, which includes the four
    /// corner-touching tiles, so roughly half of every river's steps landed
    /// somewhere the renderer had no way to connect to. That is why rivers came out
    /// as a scatter of short disjointed squiggles -- they were generated in one
    /// adjacency and drawn in another.
    /// </remarks>
    private static IEnumerable<Cell> EdgeNeighbours(int x, int y, int width, int height, bool wrapX)
    {
        var odd = y & 1;
        int[][] offsets = [[odd, -1], [odd, 1], [-1 + odd, 1], [-1 + odd, -1]];
        foreach (var offset in offsets)
        {
            var nx = x + offset[0];
            var ny = y + offset[1];
            if (ny < 0 || ny >= height) continue;
            if (wrapX) nx = Wrap(nx, width);
            else if (nx < 0 || nx >= width) continue;
            yield return new Cell(nx, ny);
        }
    }

    private static IEnumerable<Cell> Neighbours(int x, int y, int width, int height, bool wrapX)
    {
        var odd = y & 1;
        int[][] offsets =
        [
            [odd, -1], [1, 0], [odd, 1], [0, 2],
            [-1 + odd, 1], [-1, 0], [-1 + odd, -1], [0, -2]
        ];
        var seen = new HashSet<Cell>();
        foreach (var offset in offsets)
        {
            var nx = x + offset[0];
            var ny = y + offset[1];
            if (ny < 0 || ny >= height) continue;
            if (wrapX) nx = Wrap(nx, width);
            else if (nx < 0 || nx >= width) continue;
            var cell = new Cell(nx, ny);
            if (seen.Add(cell)) yield return cell;
        }
    }

    private static void PaintElevation(double[,] elevation, int centreX, int centreY, int width, int height,
        bool wrapX, int radius, double amount)
    {
        for (var dy = -radius; dy <= radius; dy++)
        for (var dx = -radius; dx <= radius; dx++)
        {
            var y = centreY + dy;
            if (y < 1 || y >= height - 1) continue;
            var x = centreX + dx;
            if (wrapX) x = Wrap(x, width);
            else if (x < 1 || x >= width - 1) continue;
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance > radius + 0.25) continue;
            elevation[x, y] += amount * (1.0 - distance / (radius + 0.5));
        }
    }

    private static (int X, int Y) Direction(int direction) => direction switch
    {
        0 => (1, 0), 1 => (1, 1), 2 => (0, 1), 3 => (-1, 1),
        4 => (-1, 0), 5 => (-1, -1), 6 => (0, -1), _ => (1, -1)
    };

    /// <summary>
    /// How much of a world is land, by the land-mass setting chosen at the start.
    /// </summary>
    /// <remarks>
    /// Measured against Civilization II rather than chosen. Twenty of its own saved
    /// worlds at 75x120 were opened here and counted: they run from 23.9% land to
    /// 42.8%, and most of them sit between 24 and 31 -- so an ordinary Civ II world
    /// is around three tiles of sea to one of land, with the outliers above 36%
    /// presumably started on the large setting.
    /// <para>
    /// The middle setting here was 0.49, and it hit it exactly: a generated world
    /// was half land, about twice Civ II's, and the seas were correspondingly
    /// cramped. Reported as "land masses seem bigger than Civ 2, I think there's
    /// more water in Civ 2", which is precisely what the counting says.
    /// </para>
    /// <para>
    /// <c>RHYCIV_REPORT_MAP=1</c> prints the share for any world, generated or
    /// loaded from a .SAV, so this can be re-measured rather than argued about.
    /// </para>
    /// </remarks>
    private static double GetLandRatio(int setting) => setting switch
    {
        0 => 0.20,
        2 => 0.42,
        _ => 0.30
    };

    private static int Wrap(int value, int maximum) => (value % maximum + maximum) % maximum;
}
