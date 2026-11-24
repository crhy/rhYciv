using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Production;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Tests.TestFiles;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;

namespace RhyCiv.Tests.Production;

/// <summary>
/// Civ II lets a city raise only one power plant -- Power, Hydro, Nuclear or
/// Solar -- and only if it has a Factory first (docs/CIV2-PARITY-DEEP-AUDIT.md
/// TR2, crhy/rhYciv#28). The four used to be +25% each and stack in a single
/// city with no Factory prerequisite.
/// <para>
/// The whole rule lives in <see cref="BuildingProductionOrder.IsValidBuild"/>,
/// the one hook every consumer shares: the build list and the computer player
/// both go through <see cref="ProductionPossibilities.GetAllowedProductionOrders"/>,
/// so these facts pin the shared behaviour rather than anything copied into the
/// UI or the AI.
/// </para>
/// </summary>
public class PowerPlantCityLimitTests
{
    private static readonly ImprovementType[] Plants =
    [
        ImprovementType.PowerPlant,
        ImprovementType.HydroPlant,
        ImprovementType.NuclearPlant,
        ImprovementType.SolarPlant,
    ];

    private sealed record Fixture(
        Game Game,
        City City,
        Civilization Civ,
        Rules Rules,
        BuildingProductionOrder Factory,
        BuildingProductionOrder MfgPlant,
        Dictionary<ImprovementType, BuildingProductionOrder> Plants);

    /// <summary>
    /// A clean-room city whose civilisation knows the Factory and every plant, so
    /// the only thing standing between it and a plant is the rule under test.
    /// Nothing is added to the city itself: the caller decides what it holds.
    /// </summary>
    private static Fixture CityWithPlantsUnlocked()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var civ = game.GetPlayerCiv;
        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Alesia");
        // Enough to pay upkeep, so a finished building is not sold on the spot.
        civ.Money = 500;

        var factory = OrderFor(rules, ImprovementType.Factory);
        var mfg = OrderFor(rules, ImprovementType.MfgPlant);
        var plants = Plants.ToDictionary(type => type, type => OrderFor(rules, type));

        foreach (var order in new[] { factory, mfg }.Concat(plants.Values))
        {
            if (order.RequiredTech >= 0)
            {
                game.GiveAdvance(order.RequiredTech, civ);
            }
        }

        return new Fixture(game, city, civ, rules, factory, mfg, plants);
    }

    private static BuildingProductionOrder OrderFor(Rules rules, ImprovementType type) =>
        ProductionOrder.GetAll(rules)
            .OfType<BuildingProductionOrder>()
            .First(order => order.Improvement.Type == (int)type);

    private static bool Offers(ImprovementType type, City city) =>
        ProductionPossibilities.GetAllowedProductionOrders(city)
            .OfType<BuildingProductionOrder>()
            .Any(order => order.Improvement.Type == (int)type);

    [Fact]
    public void WithAFactory_AndNoPlant_EachPlantCanBeBuilt()
    {
        var f = CityWithPlantsUnlocked();
        f.City.AddImprovement(f.Factory.Improvement);

        foreach (var type in Plants)
        {
            Assert.True(f.Plants[type].IsValidBuild(f.City), $"{type} refused by a Factory city with no plant");
            Assert.True(Offers(type, f.City), $"{type} not offered to a Factory city with no plant");
        }
    }

    [Fact]
    public void OnceOnePlantIsUp_AnotherOfAnyKindIsRefused()
    {
        var f = CityWithPlantsUnlocked();
        f.City.AddImprovement(f.Factory.Improvement);
        f.City.AddImprovement(f.Plants[ImprovementType.PowerPlant].Improvement);

        foreach (var type in Plants)
        {
            Assert.False(f.Plants[type].IsValidBuild(f.City), $"{type} was still buildable with a plant up");
            Assert.False(f.Plants[type].CompleteProduction(f.City, f.Rules),
                $"{type} completed while the city already had a plant");
        }
    }

    [Fact]
    public void OnceOnePlantIsUp_TheBuildListOffersNoOtherPlant()
    {
        var f = CityWithPlantsUnlocked();
        f.City.AddImprovement(f.Factory.Improvement);
        f.City.AddImprovement(f.Plants[ImprovementType.PowerPlant].Improvement);

        foreach (var type in Plants)
        {
            Assert.False(Offers(type, f.City), $"{type} still offered once a plant was up");
        }
    }

    [Fact]
    public void WithoutAFactory_NoPlantIsOfferedOrCompleted()
    {
        var f = CityWithPlantsUnlocked();

        foreach (var type in Plants)
        {
            Assert.False(f.Plants[type].IsValidBuild(f.City), $"{type} buildable without a Factory");
            Assert.False(Offers(type, f.City), $"{type} offered without a Factory");
            Assert.False(f.Plants[type].CompleteProduction(f.City, f.Rules),
                $"{type} completed without a Factory");
        }
    }

    [Fact]
    public void LosingTheFactory_StopsAPlantMidProduction()
    {
        var f = CityWithPlantsUnlocked();
        f.City.AddImprovement(f.Plants[ImprovementType.PowerPlant].Improvement);
        // A city that somehow holds a plant but no Factory must not be allowed to
        // raise a second one, and must not stay producing one it can no longer have.
        Assert.False(ProductionPossibilities.ProductionValid(
            WithProducing(f.City, f.Plants[ImprovementType.HydroPlant])));
    }

    [Fact]
    public void TheFactory_IsUnaffectedByThePlantRule()
    {
        var f = CityWithPlantsUnlocked();
        // A city that already has a plant and no Factory can still raise the
        // Factory itself: the rule gates plants, not the Factory.
        f.City.AddImprovement(f.Plants[ImprovementType.NuclearPlant].Improvement);
        Assert.True(f.Factory.IsValidBuild(f.City), "the Factory was refused beside a plant");
        Assert.True(Offers(ImprovementType.Factory, f.City), "the Factory not offered beside a plant");

        // And once built it cannot be built again, exactly as before.
        f.City.AddImprovement(f.Factory.Improvement);
        Assert.False(f.Factory.IsValidBuild(f.City), "a second Factory was allowed");
    }

    [Fact]
    public void AProductionBuildingThatIsNotAPlant_IsUnaffected()
    {
        var f = CityWithPlantsUnlocked();
        f.City.AddImprovement(f.Factory.Improvement);
        f.City.AddImprovement(f.Plants[ImprovementType.SolarPlant].Improvement);

        // Manufacturing Plant is a +50% production building but not one of the four
        // plants; a plant already up must not suppress it.
        Assert.True(f.MfgPlant.IsValidBuild(f.City), "Manufacturing Plant was caught by the plant rule");
        Assert.True(Offers(ImprovementType.MfgPlant, f.City), "Manufacturing Plant not offered with a plant up");
    }

    [Fact]
    public void TheComputerPlayerNeverQueuesASecondPlant()
    {
        var (game, _, rules) = CleanRoomGameFactory.CreateGame();
        var civ = game.AllCivilizations.First(c => c.PlayerType == PlayerType.Ai);
        var aiPlayer = (AiPlayer)game.Players[civ.Id];
        var city = CityActions.BuildCity(civ.Units.First(unit => !unit.Dead), game, "Ai-Alesia");
        civ.Money = 500;

        // Put the Factory and every plant directly into this civilisation's build
        // list rather than researching them, so the only thing that can remove a
        // plant is the one-per-city rule -- not an advance the AI has not reached.
        var factory = OrderFor(rules, ImprovementType.Factory);
        var plants = Plants.ToDictionary(type => type, type => OrderFor(rules, type));
        ProductionPossibilities.AddItems(civ.Id, new[] { factory }.Concat(plants.Values).ToList());

        city.AddImprovement(factory.Improvement);

        // With just a Factory the plants really are candidates for the AI; if this
        // passed vacuously the rest of the test would prove nothing.
        Assert.True(plants.Values.All(plant => Offers((ImprovementType)plant.Improvement.Type, city)),
            "the AI city with a Factory was not offered any plant to begin with");

        city.AddImprovement(plants[ImprovementType.PowerPlant].Improvement);

        // The computer player chooses from the shared allowed list, so no plant may
        // reach it once the city holds one.
        var allowed = ProductionPossibilities.GetAllowedProductionOrders(city);
        Assert.DoesNotContain(allowed, order =>
            order is BuildingProductionOrder b && Plants.Any(t => b.Improvement.Type == (int)t));

        city.ItemInProduction = null!;
        aiPlayer.CantProduce(city, null);
        if (city.ItemInProduction is BuildingProductionOrder chosen)
        {
            Assert.DoesNotContain(Plants, type => chosen.Improvement.Type == (int)type);
        }
    }

    private static City WithProducing(City city, BuildingProductionOrder order)
    {
        city.ItemInProduction = order;
        return city;
    }
}
