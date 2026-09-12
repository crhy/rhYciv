using System.Collections.Generic;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.LegacySaves;
using RhyCiv.Engine.Units;
using Model;
using Model.Core;
using Model.Core.Cities;
using Model.Core.Mapping;
using Model.Core.Units;

namespace RhyCiv.Engine.SaveLoad;

public class JsonSaveObjects : ILoadedGameObjects
{
    public Unit ActiveUnit { get; set; } = null!;
    /// <summary>
    /// A game saved by this game is not a scenario, so it gets a plain one with
    /// every restriction off rather than nothing at all.
    /// </summary>
    /// <remarks>
    /// This was <c>null!</c> and nothing ever assigned it, so every game loaded
    /// from a rhYciv save carried a null scenario -- and the first city captured
    /// in that game, by anyone, threw a NullReferenceException on
    /// <c>ScenarioData.ForbidTechFromConquests</c> and took the game down. It
    /// went unnoticed because Civ II's own reader does set this, so any position
    /// imported from a .SAV was fine; only this game's own saves were affected,
    /// and only once somebody took a city.
    /// </remarks>
    public Scenario Scenario { get; } = new();
    public List<City> Cities { get; set; } = [];
    
    public List<Transporter> Transporters { get; set; } = [];
    public List<Civilization> Civilizations { get; set; } = [];
    public List<Map> Maps { get; set; } = [];
    public IGameData GameData { get; set; } = null!;
    public Options Options { get; set; } = null!;
}
