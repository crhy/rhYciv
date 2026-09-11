using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Constants;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine;

/// <summary>
/// Barbarians who were not in a village.
/// </summary>
/// <remarks>
/// Until now the only barbarians in the game came out of goody huts: a unit
/// walked into a village and something unpleasant came out of it. Nothing ever
/// rose out of empty country or came ashore from the sea, so a civilisation that
/// had cleared the huts near it was never troubled again, and the Barbarity
/// setting in the new-game questions chose between four levels of a thing that
/// only happened when you went looking for it.
///
/// This is Civ II's other half: a force appears near somebody's territory every
/// so often, more often the higher the setting, and goes for the nearest thing
/// worth taking. It deliberately leaves the early game alone -- a size-one city
/// with one warrior in it has no answer to a horde, and losing to one before the
/// game has started is not a difficulty setting, it is a waste of a session.
/// </remarks>
internal static class BarbarianUprisings
{
    /// <summary>
    /// Turns of quiet before the first uprising. Long enough to have founded a
    /// city or two and put something in them.
    /// </summary>
    private const int FirstTurn = 24;

    /// <summary>How far from the city they land: close enough to threaten it.</summary>
    private const int MinimumDistance = 3;

    private const int MaximumDistance = 5;

    /// <summary>
    /// One turn in this many carries an uprising, by Barbarity setting. Villages
    /// Only never does, which is what it says.
    /// </summary>
    private static int TurnsBetweenUprisings(BarbarianActivityType activity) => activity switch
    {
        BarbarianActivityType.RovingBands => 20,
        BarbarianActivityType.RestlessTribes => 12,
        BarbarianActivityType.RagingHordes => 7,
        _ => 0
    };

    /// <summary>How many raiders arrive at once.</summary>
    private static int PartySize(BarbarianActivityType activity) => activity switch
    {
        BarbarianActivityType.RovingBands => 2,
        BarbarianActivityType.RestlessTribes => 3,
        BarbarianActivityType.RagingHordes => 4,
        _ => 0
    };

    internal static void Resolve(Game game)
    {
        var activity = (BarbarianActivityType)game.BarbarianActivity;
        var interval = TurnsBetweenUprisings(activity);
        if (interval == 0 || game.TurnNumber < FirstTurn)
        {
            return;
        }

        if (game.Random.Next(interval) != 0)
        {
            return;
        }

        var barbarians = game.AllCivilizations.FirstOrDefault(c => c.PlayerType == PlayerType.Barbarians);
        if (barbarians == null)
        {
            return;
        }

        var target = ChooseTarget(game);
        if (target == null)
        {
            return;
        }

        var city = game.Random.ChooseFrom(target.Cities);
        var landing = ChooseLandingSite(game, city, barbarians);
        if (landing == null)
        {
            return;
        }

        var definition = Barbarians.UnitFor(game, target);
        if (definition == null)
        {
            return;
        }

        barbarians.Alive = true;
        var fromTheSea = landing.Neighbours().Any(neighbour => neighbour.Type == TerrainType.Ocean);
        var landed = new List<Tile>();

        foreach (var square in SpawnSquares(landing, barbarians, PartySize(activity)))
        {
            var raider = Barbarians.Create(barbarians, definition, square,
                DifficultyRules.BarbariansAreVeterans(game));

            // They have crossed the country or the sea to get here; they attack from
            // next turn, which gives the city a turn to prepare.
            raider.MovePointsLost = raider.MaxMovePoints;
            landed.Add(square);
        }

        if (landed.Count == 0)
        {
            return;
        }

        game.UpdateTiles(landed);
        if (target.Id >= 0 && target.Id < game.Players.Length)
        {
            game.Players[target.Id].BarbarianUprising(landed[0], fromTheSea);
        }
    }

    /// <summary>
    /// Whose land they appear in. Civ II sends them after whoever has most worth
    /// taking, so a civilisation is chosen in proportion to the number of cities
    /// it holds rather than uniformly.
    /// </summary>
    private static Civilization? ChooseTarget(Game game)
    {
        var candidates = game.AllCivilizations
            .Where(civ => civ.Alive && civ.PlayerType != PlayerType.Barbarians && civ.Cities.Count > 0)
            .ToList();
        if (candidates.Count == 0)
        {
            return null;
        }

        var total = candidates.Sum(civ => civ.Cities.Count);
        var pick = game.Random.Next(total);
        foreach (var civ in candidates)
        {
            pick -= civ.Cities.Count;
            if (pick < 0)
            {
                return civ;
            }
        }

        return candidates[^1];
    }

    /// <summary>
    /// Open country within striking distance of the city, and not on top of
    /// anybody. Null when there is nowhere -- an island city with no room around
    /// it is simply left alone rather than having raiders appear inside it.
    /// </summary>
    private static Tile? ChooseLandingSite(Game game, City city, Civilization barbarians)
    {
        var map = city.Location.Map;
        var sites = new List<Tile>();

        for (var x = -MaximumDistance; x <= MaximumDistance; x++)
        {
            for (var y = -MaximumDistance; y <= MaximumDistance; y++)
            {
                var distance = Math.Max(Math.Abs(x), Math.Abs(y));
                if (distance < MinimumDistance || distance > MaximumDistance)
                {
                    continue;
                }

                // Squares of a Civ II map are staggered, so only those whose
                // coordinates have the same parity exist at all.
                if (((city.Location.X + x) + (city.Location.Y + y)) % 2 != 0)
                {
                    continue;
                }

                if (!map.IsValidTileC2(city.Location.X + x, city.Location.Y + y))
                {
                    continue;
                }

                var tile = map.TileC2(city.Location.X + x, city.Location.Y + y);
                if (CanLandOn(tile, barbarians))
                {
                    sites.Add(tile);
                }
            }
        }

        return sites.Count == 0 ? null : game.Random.ChooseFrom(sites);
    }

    private static bool CanLandOn(Tile tile, Civilization barbarians) =>
        tile.Type != TerrainType.Ocean &&
        !tile.Terrain.Impassable &&
        tile.CityHere == null &&
        tile.UnitsHere.All(unit => unit.Owner == barbarians);

    /// <summary>
    /// The landing square and as many squares beside it as the party needs.
    /// </summary>
    private static IEnumerable<Tile> SpawnSquares(Tile landing, Civilization barbarians, int wanted)
    {
        yield return landing;

        var remaining = wanted - 1;
        foreach (var neighbour in landing.Neighbours())
        {
            if (remaining <= 0)
            {
                yield break;
            }

            if (CanLandOn(neighbour, barbarians))
            {
                remaining--;
                yield return neighbour;
            }
        }
    }
}
