using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;

namespace Model.Core;

public interface IGame
{
    FastRandom Random { get;  }
    Civilization GetPlayerCiv { get; }
    IDictionary<int,TerrainImprovement> TerrainImprovements { get; }
    IImprovementEncoder ImprovementEncoder { get; }
    Rules Rules { get; }
    Civilization GetActiveCiv { get; }
    Options Options { get; }
    Scenario ScenarioData { get; }
    
    IPlayer ActivePlayer { get; }
    IScriptEngine Script { get; }
    
    IList<Map> Maps { get; }
    IHistory History { get; }
    Dictionary<string, List<string>?> CityNames { get; }
    Dictionary<Civilization, int> CitiesBuiltSoFar { get; }

    void ConnectPlayer(IPlayer player);
    string Order2String(int unitOrder);
    void ChooseNextUnit();
    bool ProcessEndOfTurn();
    void ChoseNextCiv();
    void UpdateTiles(IList<Tile> tiles);

    /// <summary>
    /// Brings one civilisation's record of some squares up to date whether or not
    /// it can currently see them. See the implementation for why losing a city
    /// needs this.
    /// </summary>
    void UpdateTilesFor(IList<Tile> tiles, int civilizationId);
    double MaxDistance { get; }
    int DifficultyLevel { get; set; }
    IGameDate Date { get; }
    
    int TurnNumber { get; }
    List<City> AllCities { get; }
    IPlayer[] Players { get; }
    int PollutionSkulls { get; }
    int GlobalTempRiseOccured { get; }
    int NoOfTurnsOfPeace { get; }
    int BarbarianActivity { get; }
    int NoMaps { get; }
    List<Civilization> AllCivilizations { get; }
    void SetHumanPlayer(int playerCivId);
    void StartPlayerTurn(IPlayer activePlayer);
    void StartNextTurn();
    string GetRealmName(int government);
}