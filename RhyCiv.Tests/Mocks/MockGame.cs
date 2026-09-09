using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;

namespace RhyCiv.Tests.Mocks;

internal class MockGame : IGame
{
    // Seeded so any engine code that reaches for randomness stays deterministic
    // across test runs instead of throwing.
    public FastRandom Random { get; } = new(42);
    public Civilization GetPlayerCiv => AllCivilizations.First(c => c.PlayerType == PlayerType.Local);

    // Empty rather than throwing: granting an advance walks these looking for
    // improvements the advance unlocks, and most tests have none.
    public IDictionary<int, TerrainImprovement> TerrainImprovements { get; set; } =
        new Dictionary<int, TerrainImprovement>();
    public IImprovementEncoder ImprovementEncoder { get; }
    public Rules Rules { get; set; }
    public Civilization GetActiveCiv => throw new NotImplementedException();
    public Options Options => throw new NotImplementedException();
    public Scenario ScenarioData => throw new NotImplementedException();
    public IPlayer ActivePlayer => Players[0];
    public IScriptEngine Script => throw new NotImplementedException();
    public IList<Map> Maps { get; init; }

    public IHistory History { get; set; } = new Moq.Mock<IHistory>().Object;
    public Dictionary<string, List<string>?> CityNames => throw new NotImplementedException();
    public Dictionary<Civilization, int> CitiesBuiltSoFar { get; }

    public void ConnectPlayer(IPlayer player) => throw new NotImplementedException();
    public string Order2String(int unitOrder) => throw new NotImplementedException();
    public bool ChooseNextUnitCalled { get; private set; }
    public void ChooseNextUnit()
    {
        ChooseNextUnitCalled = true;
    }
    public bool ProcessEndOfTurn() => throw new NotImplementedException();
    public void ChoseNextCiv() => throw new NotImplementedException();

    public void UpdateTiles(IList<Tile> tiles)
    {

    }

    public double MaxDistance => throw new NotImplementedException();

    public int DifficultyLevel
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public IGameDate Date => throw new NotImplementedException();
    public int TurnNumber => throw new NotImplementedException();
    // The cities of every civilisation, which is how the world-level rules --
    // wonders being unique, pollution accumulating -- see the board.
    public List<City> AllCities => AllCivilizations?.SelectMany(civ => civ.Cities).ToList() ?? [];
    public IPlayer[] Players { get; set; }

    public int PollutionSkulls => throw new NotImplementedException();
    public int GlobalTempRiseOccured => throw new NotImplementedException();
    public int NoOfTurnsOfPeace => throw new NotImplementedException();
    public int BarbarianActivity => throw new NotImplementedException();
    public int NoMaps => throw new NotImplementedException();
    public List<Civilization> AllCivilizations { get; set; }

    public void SetHumanPlayer(int playerCivId) => throw new NotImplementedException();
    public void StartPlayerTurn(IPlayer activePlayer) => throw new NotImplementedException();
    public void StartNextTurn() => throw new NotImplementedException();
    public string GetRealmName(int government) => throw new NotImplementedException();
}
