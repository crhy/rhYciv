using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Units;
using Moq;

namespace RhyCiv.Tests.Cities;

/// <summary>
/// Issue #131: the happiness split had only ever been read off one city under one
/// government. AnalyzeHappiness exposes the five rows Civ II's Happiness Analysis
/// panel shows, and these tests pin each row across the configurations the issue
/// lists - a city in actual disorder, martial law under three governments, the
/// same city at two luxury rates, three difficulty levels, empire size, and the
/// tech-dependent wonders. Every breakdown is checked against the number
/// CalculateHappiness actually hands the game, in the pass order the engine
/// documents.
/// </summary>
public class HappinessAnalysisTests
{
    [Fact]
    public void BreakdownShowsTheFivePassesInTheDocumentedOrder()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 8);
        for (var i = 0; i < 4; i++) AddWarrior(civ, city, city.Location);

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.Equal(
            [HappinessPass.BaseMood, HappinessPass.Luxuries, HappinessPass.Improvements,
             HappinessPass.Government, HappinessPass.Wonders],
            analysis.Steps.Select(step => step.Pass).ToArray());
        Assert.Equal([7, 7, 7, 4, 4], analysis.Steps.Select(step => step.Unhappy).ToArray());
        Assert.Contains("martial law: 3 units x1 (Despotism) -3", analysis.Government.Causes);
    }

    [Fact]
    public void ACityInActualDisorderShowsWhereItsUnhappyCitizensCameFrom()
    {
        var (game, city, _) = CreateCity(DifficultyType.Deity, size: 2);

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.True(analysis.Final.IsInDisorder);
        Assert.Equal(1, analysis.BaseMood.Unhappy);
        Assert.Equal(1, analysis.Luxuries.Unhappy);
        Assert.Equal(1, analysis.Improvements.Unhappy);
        Assert.Contains("difficulty Deity: 1 unhappy", analysis.BaseMood.Causes);
        Assert.Empty(analysis.Improvements.Causes);
    }

    [Theory]
    [InlineData(GovernmentType.Despotism, 1, 6, "martial law: 1 unit x1 (Despotism) -1")]
    [InlineData(GovernmentType.Monarchy, 2, 5, "martial law: 2 units x1 (Monarchy) -2")]
    [InlineData(GovernmentType.Communism, 3, 1, "martial law: 3 units x2 (Communism) -6")]
    [InlineData(GovernmentType.Despotism, 4, 4, "martial law: 3 units x1 (Despotism) -3")]
    [InlineData(GovernmentType.Communism, 4, 1, "martial law: 3 units x2 (Communism) -6")]
    public void MartialLawPacifiesTheSameCityUnderEachMilitaristicGovernment(
        GovernmentType government,
        int garrisonedUnits,
        int unhappyAfterGovernment,
        string cause)
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 8);
        civ.Government = (int)government;
        for (var i = 0; i < garrisonedUnits; i++) AddWarrior(civ, city, city.Location);

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.Equal(7, analysis.BaseMood.Unhappy);
        Assert.Equal(7, analysis.Improvements.Unhappy);
        Assert.Equal(unhappyAfterGovernment, analysis.Government.Unhappy);
        Assert.Equal(unhappyAfterGovernment, analysis.Wonders.Unhappy);
        Assert.Contains(cause, analysis.Government.Causes);
    }

    [Fact]
    public void TheSameCityAtTwoLuxuryRatesDiffersOnlyInTheLuxuriesRow()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Chieftain, size: 4);
        civ.TaxRate = 0;
        civ.ScienceRate = 0;

        city.Trade = 0;
        var withoutLuxuries = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(withoutLuxuries, city, game.Object);
        Assert.Equal(0, withoutLuxuries.Luxuries.Happy);
        Assert.Empty(withoutLuxuries.Luxuries.Causes);

        city.Trade = 4;
        var withLuxuries = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(withLuxuries, city, game.Object);
        Assert.Equal(2, withLuxuries.Luxuries.Happy);
        Assert.Contains("luxuries: 2 happy (4 points)", withLuxuries.Luxuries.Causes);
        Assert.Equal(2, withLuxuries.Final.HappyCitizens);
        Assert.Equal(2, withLuxuries.Final.ContentCitizens);

        Assert.Equal(withoutLuxuries.Improvements.Unhappy, withLuxuries.Improvements.Unhappy);
    }

    [Theory]
    [InlineData(DifficultyType.Chieftain, 6)]
    [InlineData(DifficultyType.King, 3)]
    [InlineData(DifficultyType.Deity, 1)]
    public void DifficultySetsTheBaseRowEveryLaterPassWorksFrom(
        DifficultyType difficulty,
        int bornContent)
    {
        var (game, city, _) = CreateCity(difficulty, bornContent);

        var settled = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(settled, city, game.Object);
        Assert.Equal(0, settled.BaseMood.Unhappy);
        Assert.Equal(bornContent, settled.BaseMood.Content);
        Assert.Equal(bornContent, settled.Improvements.Content);

        city.Size++;
        var grown = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(grown, city, game.Object);
        Assert.Equal(1, grown.BaseMood.Unhappy);
        Assert.Equal(bornContent, grown.BaseMood.Content);
        Assert.Contains($"difficulty {difficulty}: 1 unhappy", grown.BaseMood.Causes);
    }

    [Fact]
    public void EmpireSizeAddsUnhappinessThatCommunismEscapes()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 1);
        for (var i = 1; i < 12; i++)
        {
            civ.Cities.Add(new City
            {
                Owner = civ,
                Location = city.Location,
                Size = 1,
                Name = $"City {i}"
            });
        }

        var despotism = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(despotism, city, game.Object);
        Assert.Equal(1, despotism.BaseMood.Unhappy);
        Assert.Equal(1, despotism.BaseMood.Angry);
        Assert.Contains("difficulty Deity: 0 unhappy, empire size: 2 unhappy", despotism.BaseMood.Causes);

        civ.Government = (int)GovernmentType.Communism;
        var communism = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(communism, city, game.Object);
        Assert.Equal(0, communism.BaseMood.Unhappy);
        Assert.Empty(communism.BaseMood.Causes);
        Assert.Equal(1, communism.Final.ContentCitizens);
    }

    [Fact]
    public void TempleIsRaisedByMysticismAndDoubledByAnActiveOracle()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 4);
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Temple });

        var plain = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(plain, city, game.Object);
        Assert.Equal(3, plain.BaseMood.Unhappy);
        Assert.Equal(2, plain.Improvements.Unhappy);
        Assert.Contains("Temple -1", plain.Improvements.Causes);

        civ.Advances[(int)AdvanceType.Mysticism] = true;
        var mystic = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(mystic, city, game.Object);
        Assert.Equal(1, mystic.Improvements.Unhappy);
        Assert.Contains("Temple -2 (Mysticism)", mystic.Improvements.Causes);

        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Oracle });
        var doubled = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(doubled, city, game.Object);
        Assert.Equal(0, doubled.Improvements.Unhappy);
        Assert.Contains("Temple -4 (Mysticism) x2 (Oracle)", doubled.Improvements.Causes);
    }

    [Theory]
    [InlineData(GovernmentType.Despotism, false, 2, "Cathedral -3")]
    [InlineData(GovernmentType.Communism, false, 3, "Cathedral -2 (Communism)")]
    [InlineData(GovernmentType.Despotism, true, 1, "Cathedral -4 (Theology)")]
    public void CathedralStrengthFollowsGovernmentAndTheology(
        GovernmentType government,
        bool withTheology,
        int unhappyAfterImprovements,
        string cause)
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 6);
        civ.Government = (int)government;
        if (withTheology) civ.Advances[(int)AdvanceType.Theology] = true;
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Cathedral });

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.Equal(5, analysis.BaseMood.Unhappy);
        Assert.Equal(unhappyAfterImprovements, analysis.Improvements.Unhappy);
        Assert.Contains(cause, analysis.Improvements.Causes);
    }

    [Fact]
    public void DemocracyPaysTwoPerDeployedUnitAndAPoliceStationHalvesIt()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Chieftain, size: 3);
        civ.Government = (int)GovernmentType.Democracy;
        AddWarrior(civ, city, city.Location.Map.Tile[0, 0]);

        var deployed = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(deployed, city, game.Object);
        Assert.Equal(0, deployed.BaseMood.Unhappy);
        Assert.Equal(2, deployed.Government.Unhappy);
        Assert.Contains("Democracy: 1 deployed unit +2 unhappy", deployed.Government.Causes);

        city.AddImprovement(new Improvement { Type = (int)ImprovementType.PoliceStat });
        var relieved = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(relieved, city, game.Object);
        Assert.Equal(1, relieved.Government.Unhappy);
        Assert.Contains(
            "Democracy: 1 deployed unit +1 unhappy (Police Station or Women's Suffrage)",
            relieved.Government.Causes);
    }

    [Fact]
    public void ShakespearesTheatreClearsTheUnhappinessTheEarlierPassesBuilt()
    {
        var (game, city, _) = CreateCity(DifficultyType.Deity, size: 8);
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.ShakespTheat });

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.Equal(7, analysis.Improvements.Unhappy);
        Assert.Equal(0, analysis.Wonders.Unhappy);
        Assert.Equal(0, analysis.Final.UnhappyCitizens);
        Assert.Contains("Shakespeare's Theatre: no unhappy citizens", analysis.Wonders.Causes);
    }

    [Fact]
    public void TheBreakdownNeverDriftsFromTheNumberTheGameUses()
    {
        var (game, city, civ) = CreateCity(DifficultyType.Deity, size: 8);
        civ.Government = (int)GovernmentType.Communism;
        civ.TaxRate = 0;
        civ.ScienceRate = 0;
        city.Trade = 6;
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Colosseum });
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Temple });
        city.AddImprovement(new Improvement { Type = (int)ImprovementType.Cathedral });
        civ.Advances[(int)AdvanceType.Electronics] = true;
        civ.Advances[(int)AdvanceType.Mysticism] = true;
        civ.Advances[(int)AdvanceType.Theology] = true;
        for (var i = 0; i < 2; i++) AddWarrior(civ, city, city.Location);

        var analysis = city.AnalyzeHappiness(game.Object);
        AssertBreakdownMatchesTheGame(analysis, city, game.Object);

        Assert.Equal([7, 6, 0, 0, 0], analysis.Steps.Select(step => step.Unhappy).ToArray());
        // Six luxury points would make three citizens happy; the pass normalizes
        // them against the seven the base mood made unhappy, so the row shows two.
        Assert.Contains("luxuries: 3 happy (6 points)", analysis.Luxuries.Causes);
        Assert.Equal(2, analysis.Luxuries.Happy);
        Assert.Equal(6, analysis.Luxuries.Unhappy);
        Assert.Contains("Colosseum -4 (Electronics)", analysis.Improvements.Causes);
        Assert.Contains("Cathedral -3 (Communism) (Theology)", analysis.Improvements.Causes);
        Assert.Contains("Temple -2 (Mysticism)", analysis.Improvements.Causes);
        Assert.Contains("martial law: 2 units x2 (Communism) -4", analysis.Government.Causes);
        Assert.Equal(2, analysis.Final.HappyCitizens);
        Assert.Equal(6, analysis.Final.ContentCitizens);
    }

    // The breakdown must end where the game counts, and it must show the passes
    // in the order the original applies them. Called straight after each analysis
    // so the city is still in the state the analysis was taken from.
    private static void AssertBreakdownMatchesTheGame(
        HappinessAnalysis analysis,
        City city,
        IGame game)
    {
        Assert.Equal(
            [HappinessPass.BaseMood, HappinessPass.Luxuries, HappinessPass.Improvements,
             HappinessPass.Government, HappinessPass.Wonders],
            analysis.Steps.Select(step => step.Pass).ToArray());

        var mood = city.CalculateHappiness(game);
        Assert.Equal(mood, analysis.Final);
        Assert.Equal(mood.HappyCitizens, analysis.Wonders.Happy);
        Assert.Equal(mood.ContentCitizens, analysis.Wonders.Content);
        Assert.Equal(mood.UnhappyCitizens, analysis.Wonders.Unhappy);
        Assert.Equal(mood.AngryCitizens, analysis.Wonders.Angry);
    }

    private static (Mock<IGame> Game, City City, Civilization Civ) CreateCity(
        DifficultyType difficulty,
        int size)
    {
        var rules = new Rules
        {
            Governments = Enumerable.Range(0, 7).Select(_ => new Government()).ToArray()
        };
        rules.Cosmic.CitySizeUnhappyChieftain = 7;
        rules.Cosmic.RiotFactor = 14;

        var map = new Map(flat: false, index: 0)
        {
            XDim = 10,
            YDim = 10,
            Tile = new Tile[10, 10]
        };
        var terrain = new Terrain
        {
            Name = "Plains",
            Type = TerrainType.Plains,
            Defense = 100,
            Specials = []
        };
        for (var y = 0; y < map.YDim; y++)
        {
            for (var x = 0; x < map.YDim; x++)
            {
                map.Tile[x, y] = new Tile(2 * x + y % 2, y, terrain, 0, map, x, new bool[2]);
            }
        }

        var civ = new Civilization
        {
            Id = 1,
            Government = (int)GovernmentType.Despotism,
            PlayerType = PlayerType.Local,
            Advances = new bool[100],
            TaxRate = 40,
            ScienceRate = 60
        };
        var city = new City
        {
            Owner = civ,
            WhoBuiltIt = civ,
            Location = map.Tile[4, 4],
            Size = size,
            Name = "Test City"
        };
        city.Location.CityHere = city;
        civ.Cities.Add(city);

        var game = new Mock<IGame>();
        game.SetupGet(g => g.Rules).Returns(rules);
        game.SetupGet(g => g.DifficultyLevel).Returns((int)difficulty);
        game.SetupGet(g => g.Maps).Returns([map]);
        return (game, city, civ);
    }

    private static Unit AddWarrior(Civilization civ, City homeCity, Tile location)
    {
        var unit = new Unit
        {
            Owner = civ,
            HomeCity = homeCity,
            TypeDefinition = new UnitDefinition
            {
                Attack = 1,
                Defense = 1,
                Flags = new bool[20]
            },
            CurrentLocation = location
        };
        civ.Units.Add(unit);
        return unit;
    }
}
