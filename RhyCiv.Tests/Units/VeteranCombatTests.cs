using RhyCiv.Engine.Units;
using Model.Constants;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.Units;

/// <summary>
/// What being a veteran is worth. Reported against 0.1.5 as a unit that won a
/// dozen fights and never seemed to become one, so the rule is pinned here:
/// Civ II gives a veteran half again its attack and half again its defence, and
/// nothing else about the unit changes.
/// </summary>
public class VeteranCombatTests
{
    [Fact]
    public void AVeteranAttacksAtHalfAgain()
    {
        var regular = Unit(attack: 4, defence: 1);
        var veteran = Unit(attack: 4, defence: 1);
        veteran.Veteran = true;
        var target = Unit(attack: 1, defence: 2);

        Assert.Equal(4d, regular.AttackFactor(target));
        Assert.Equal(6d, veteran.AttackFactor(target));
    }

    [Fact]
    public void AVeteranDefendsAtHalfAgain()
    {
        var regular = Unit(attack: 1, defence: 4);
        var veteran = Unit(attack: 1, defence: 4);
        veteran.Veteran = true;
        var attacker = Unit(attack: 4, defence: 1);

        // Terrain multiplies the defence, and Tile.Defense is the terrain's own
        // figure halved, so a value of 2 leaves the unit's defence untouched and
        // the veteran bonus is the only thing being measured.
        var map = new Map(true, 0) { Tile = new Tile[1, 1], XDim = 1, YDim = 1 };
        var tile = new Tile(0, 0, new Terrain { Specials = [], Defense = 2 }, 0, map, 0, new bool[2]);

        Assert.Equal(4, regular.DefenseFactor(attacker, tile, 0));
        Assert.Equal(6, veteran.DefenseFactor(attacker, tile, 0));
    }

    private static Unit Unit(int attack, int defence)
    {
        var civ = new Civilization { Id = 1, PlayerType = PlayerType.Ai };
        return new Unit
        {
            Owner = civ,
            TypeDefinition = new UnitDefinition
            {
                Attack = attack,
                Defense = defence,
                AIrole = AiRoleType.Attack,
                Flags = Enumerable.Repeat(false, 13).ToArray(),
            },
        };
    }
}
