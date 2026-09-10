using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Player;

namespace RhyCiv.Engine;

/// <summary>
/// Industry fouling the land around a city, and what that does to the world's
/// climate if enough of it is left uncleaned.
/// <para>
/// Every piece of this was in place except the part that made it happen. A city
/// works out how much pollution it produces, the Pollution terrain improvement
/// exists with art to draw it, settlers can be ordered to clear it, and the game
/// text carries the messages for both the local mess and the global consequence.
/// Nothing ever put a single square of it on the map, so Mass Transit, the
/// Recycling Center, the Solar Plant, Hoover Dam and Eiffel Tower were all
/// defending against something that could not occur.
/// </para>
/// </summary>
public static class PollutionFunctions
{
    /// <summary>
    /// Polluted squares the world tolerates before the climate begins to shift.
    /// Civ II counts the same way -- the skulls on the pollution display are this
    /// count in eighths -- and starts warming once the count runs past it.
    /// </summary>
    public const int WarmingThreshold = 16;

    /// <summary>
    /// Squares changed by one warming event.
    /// </summary>
    private const int TilesChangedByWarming = 8;

    /// <summary>
    /// What each kind of land becomes when the climate turns against it: wetter
    /// where there is water to be had, drier where there is not. Mountains, glacier
    /// and ocean are left alone, as in Civ II, and jungle and swamp are already
    /// the end of the road.
    /// </summary>
    private static readonly Dictionary<TerrainType, TerrainType> WarmingChanges = new()
    {
        { TerrainType.Forest, TerrainType.Jungle },
        { TerrainType.Grassland, TerrainType.Swamp },
        { TerrainType.Plains, TerrainType.Desert },
        { TerrainType.Hills, TerrainType.Forest },
        { TerrainType.Tundra, TerrainType.Desert }
    };

    /// <summary>
    /// Rolls each of a civilisation's cities for pollution, and tells the player
    /// about the squares that were fouled. A city's <c>Pollution</c> is its chance
    /// in a hundred of fouling one square of its own working radius per turn.
    /// </summary>
    public static void ResolveCityPollution(IGame game, Civilization civilization, IPlayer player)
    {
        var fouled = new List<Tile>();

        foreach (var city in civilization.Cities.ToList())
        {
            if (city.Location == null || city.Pollution <= 0 || game.Random.Next(100) >= city.Pollution)
            {
                continue;
            }

            var square = ChooseSquareToFoul(game, city);
            if (square == null)
            {
                continue;
            }

            Foul(game, square);
            fouled.Add(square);

            // The square is worth half what it was, so the city's figures are wrong
            // until they are worked out again -- and the player is about to be told
            // to go and look at it.
            city.CalculateOutput(city.Owner.Government, game);
            player.CityPolluted(city, square);
        }

        if (fouled.Count > 0)
        {
            game.UpdateTiles(fouled);
        }
    }

    /// <summary>
    /// The world's response to what has been left lying about. Called once a turn,
    /// after every civilisation's cities have had their roll, so the count covers
    /// everybody's mess rather than only the civilisation whose turn it is.
    /// </summary>
    public static bool ResolveGlobalWarming(IGame game)
    {
        if (!Settings.GlobalWarmingEnabled)
        {
            return false;
        }

        var polluted = PollutedSquares(game).Count;
        if (polluted <= WarmingThreshold)
        {
            return false;
        }

        // The further past the threshold the world is, the likelier the shift. At
        // twice the threshold it is a near certainty.
        var chance = (polluted - WarmingThreshold) * 100 / WarmingThreshold;
        if (game.Random.Next(100) >= chance)
        {
            return false;
        }

        var changed = new List<Tile>();
        var candidates = AllTiles(game)
            .Where(tile => WarmingChanges.ContainsKey(tile.Type))
            .ToList();

        for (var i = 0; i < TilesChangedByWarming && candidates.Count > 0; i++)
        {
            var index = game.Random.Next(candidates.Count);
            var tile = candidates[index];
            candidates.RemoveAt(index);

            var becomes = WarmingChanges[tile.Type];
            var terrain = game.Rules.Terrains[tile.Z].FirstOrDefault(t => t.Type == becomes);
            if (terrain == null)
            {
                continue;
            }

            tile.Terrain = terrain;
            changed.Add(tile);
        }

        if (changed.Count == 0)
        {
            return false;
        }

        game.UpdateTiles(changed);

        foreach (var player in game.Players)
        {
            player.GlobalWarming(changed.Count);
        }

        return true;
    }

    /// <summary>
    /// Every square on the map carrying pollution. Used for the warming roll, and
    /// worth having on its own: the pollution display counts these.
    /// </summary>
    public static List<Tile> PollutedSquares(IGame game)
    {
        return AllTiles(game)
            .Where(tile => tile.Improvements.Any(i => i.Improvement == ImprovementTypes.Pollution))
            .ToList();
    }

    /// <summary>
    /// Every square of every map. Pollution is a property of the world rather than
    /// of anybody's territory, so all of it counts.
    /// </summary>
    private static IEnumerable<Tile> AllTiles(IGame game)
    {
        return game.Maps.SelectMany(map => map.Tile.Cast<Tile>()).Where(tile => tile != null);
    }

    private static Tile? ChooseSquareToFoul(IGame game, City city)
    {
        var candidates = city.Location.CityRadius()
            .Where(tile => tile != null && tile.CityHere == null && tile.Type != TerrainType.Ocean &&
                           !tile.Terrain.Impassable &&
                           tile.Improvements.All(i => i.Improvement != ImprovementTypes.Pollution))
            .ToList();

        return candidates.Count == 0 ? null : candidates[game.Random.Next(candidates.Count)];
    }

    /// <summary>
    /// Fouls a square from outside a city's industry -- fallout, in practice.
    /// Cities are not poisoned by it: what a nuclear strike does to a city is
    /// measured in citizens, not in terrain.
    /// </summary>
    public static void Poison(IGame game, Tile tile)
    {
        if (tile.CityHere != null || tile.Type == TerrainType.Ocean || tile.Terrain.Impassable ||
            tile.Improvements.Any(i => i.Improvement == ImprovementTypes.Pollution))
        {
            return;
        }

        Foul(game, tile);
    }

    private static void Foul(IGame game, Tile tile)
    {
        var pollution = game.TerrainImprovements[ImprovementTypes.Pollution];
        var terrain = pollution.AllowedTerrains[tile.Z].FirstOrDefault(t => t.TerrainType == (int)tile.Type);
        if (terrain == null)
        {
            return;
        }

        tile.AddImprovement(pollution, terrain, 0, game.Rules.Terrains[tile.Z], tile.GetCivsVisibleTo(game));
    }
}
