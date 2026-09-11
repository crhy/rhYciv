using System;
using System.Linq;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using Model.Constants;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine;

public class Barbarians
{
    public static Civilization Civilization =>
        new()
        {
            Adjective = Labels.For(LabelIndex.Barbarian), 
            LeaderName = Labels.For(LabelIndex.Attila),
            TribeName = Labels.For(LabelIndex.Barbarians),
            Alive = true, Id = 0, TribeId = -1,
            PlayerType = PlayerType.Barbarians, 
            Advances = []
        };

    /// <summary>
    /// Puts a raider on a square.
    /// <para>
    /// Shared with the goody-hut spawn, which is where it started: barbarians now
    /// come from two places and must be built the same way in both, or one of them
    /// gets a unit with a duplicate id or no home square.
    /// </para>
    /// </summary>
    internal static Unit Create(Civilization owner, UnitDefinition unitDefinition, Tile tile, bool veteran)
    {
        var unit = new Unit
        {
            Id = owner.Units.Count != 0 ? owner.Units.Max(u => u.Id) + 1 : 0,
            Order = (int)OrderType.NoOrders,
            Owner = owner,
            Veteran = veteran,
            X = tile.X,
            Y = tile.Y,
            MapIndex = tile.Z,
            TypeDefinition = unitDefinition,
            NeedsSupport = false
        };

        owner.Units.Add(unit);
        unit.CurrentLocation = tile;
        return unit;
    }

    /// <summary>
    /// What the raiders are armed with: whatever suits the age the civilisation
    /// they are coming for has reached, so an uprising in the gunpowder era is not
    /// a handful of warriors walking into a musketeer.
    /// </summary>
    internal static UnitDefinition? UnitFor(IGame game, Civilization against)
    {
        var preferred = against.Epoch <= 1
            ? new[] { UnitType.Horsemen, UnitType.Warriors, UnitType.Archers }
            : new[] { UnitType.Dragoons, UnitType.Crusaders, UnitType.Horsemen, UnitType.Warriors };

        foreach (var type in preferred)
        {
            var index = (int)type;
            if (index >= 0 && index < game.Rules.UnitTypes.Length)
            {
                return game.Rules.UnitTypes[index];
            }
        }

        return game.Rules.UnitTypes.FirstOrDefault(definition => definition.Attack > 0);
    }
}
