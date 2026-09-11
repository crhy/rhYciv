using RhyCiv.Engine;
using RhyCiv.Engine.Units;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.UnitActions;
using Model.Constants;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Tests.Units;

/// <summary>
/// What a Settlers unit is worth in a fight.
/// <para>
/// Reported twice on 0.1.9: a settler in a city killing two attacking horsemen,
/// and a horseman dying to a settler in the open. One of those is Civ II working
/// and one was a fault.
/// </para>
/// <para>
/// Working: a Settlers unit has twenty hit points, double every other unit of its
/// age -- "the only Ancient-era unit with two hitpoints, giving them an effective
/// Defense strength of 2 against Ancient and early Renaissance units". An early
/// attacker really does lose to one about half the time, and the shipped ruleset
/// already carries the right number.
/// </para>
/// <para>
/// The fault: they were being allowed to fortify, which is half again on top of
/// that. Civ II's combat guide names Settlers and Engineers as "the only units
/// incapable of fortifying".
/// </para>
/// </summary>
public class SettlerDefenceTests
{
    [Fact]
    public void ASettler_CannotFortify()
    {
        Assert.False(UnitFunctions.CanFortifyHere(Settler(), Grassland()));
    }

    [Fact]
    public void AFightingUnit_StillCan()
    {
        Assert.True(UnitFunctions.CanFortifyHere(Warrior(), Grassland()));
    }

    [Fact]
    public void ASettlerCarryingTheOrder_IsStillNotDugIn()
    {
        // A saved game from an older build can have the order on one, so the bonus
        // is refused at the sum as well as at the order.
        var settler = Settler();
        settler.Order = (int)OrderType.Fortified;

        Assert.Equal(1d, settler.DefenseFactor(Warrior(), Grassland(), 0));
    }

    [Fact]
    public void AFightingUnitCarryingTheOrder_IsDugIn()
    {
        var warrior = Warrior();
        warrior.Order = (int)OrderType.Fortified;

        Assert.Equal(1.5d, warrior.DefenseFactor(Warrior(), Grassland(), 0));
    }

    [Fact]
    public void ASettlerInAFort_StillGetsTheFort()
    {
        // Settlers cannot dig in, but they build forts and they shelter in them.
        // Civ II gives the fortress bonus to a land unit occupying one "whether
        // given the order to fortify or not", so refusing them the fortify bonus
        // must not refuse them this.
        Assert.Equal(2d, Settler().DefenseFactor(Warrior(), Grassland(), 100));
    }

    private static Unit Settler() => new()
    {
        TypeDefinition = new UnitDefinition
        {
            Name = "Settlers", Domain = UnitGas.Ground, Flags = new bool[15],
            Attack = 0, Defense = 1, Hitp = 2, AIrole = AiRoleType.Settle
        }
    };

    private static Unit Warrior() => new()
    {
        TypeDefinition = new UnitDefinition
        {
            Name = "Warriors", Domain = UnitGas.Ground, Flags = new bool[15],
            Attack = 1, Defense = 1, Hitp = 1, AIrole = AiRoleType.Attack
        }
    };

    private static Tile Grassland() =>
        new(0, 0, new Terrain { Defense = 2, Specials = [] }, 0, new Map(true, 0), 0, new bool[2]);
}
