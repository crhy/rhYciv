using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.LegacySaves;
using RhyCiv.Engine.Scripting;
using RhyCiv.Engine.Scripting.ScriptObjects;
using RhyCiv.Engine.Statistics;
using RhyCiv.Engine.Terrains;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;

namespace RhyCiv.Engine
{
    public partial class Game
    {
        public Game(Map[] maps, Rules configRules, IList<Civilization> civilizations, Options options,
            string[] gamePaths, int difficulty, int barbarianActivity)
        {
            Script = new ScriptEngine(this, gamePaths);
            _options = options;
            _maps = maps;
            _rules = configRules;
            TurnNumber = 0;
            Date = new Date(0, 0, difficulty);
            _barbarianActivity = (BarbarianActivityType)barbarianActivity;
            // This used to set a private field that shadowed the public property,
            // so every new game reported Chieftain however it was configured, and
            // everything reading Game.DifficultyLevel - barbarian veterans, city
            // happiness, corruption distance - played at the easiest setting.
            DifficultyLevel = difficulty;
            
            var tile0 = _maps[0].Tile[0, 0];
            
            AllCivilizations.AddRange(civilizations);

            CityNames = NameLoader.LoadCityNames(gamePaths);

            Players = civilizations.Select(c => new AiPlayer(DifficultyLevel, c, tile0, this, new AiInterface( this,c, DifficultyLevel, Script))).Cast<IPlayer>()
                .ToArray();

            TerrainImprovements = TerrainImprovementFunctions.GetStandardImprovements(Rules); 
            
            Script.RunScript("game_setup.lua");

            Script.RunScript("tile_improvements.lua");
            
            Script.RunScript("improvements.lua");
            Script.RunScript("advances.lua");
            Script.RunScript("units.lua");

            foreach (var player in Players)
            {
                Script.RunPlayerScript(player);
            }

            AllCivilizations.ForEach((civ) =>
            {
                OnCivEvent?.Invoke(this, new CivEventArgs(CivEventType.Created, civ));
            });
            
            this.SetupTech();
            
            Power.CalculatePowerRatings(this);

            History = HistoryUtils.ReconstructHistory(this);
        }

        public Game(Rules rules, ILoadedGameObjects objects, string[] rulesetPaths)
            : this(objects.Maps.ToArray(), rules, objects.Civilizations, objects.Options,
                  rulesetPaths, objects.GameData.DifficultyLevel, objects.GameData.BarbarianActivity)
        {
            // The shared constructor records an initial power sample for new games.
            // Loaded games already contain their history, so discard that synthetic sample.
            foreach (var civilization in AllCivilizations)
            {
                if (civilization.PowerRating.Count > 0)
                {
                    civilization.PowerRating.RemoveAt(civilization.PowerRating.Count - 1);
                }
            }

            // Never null, whatever a loader hands over: a game with no scenario
            // is a game with every scenario restriction off, and the alternative
            // is a crash the first time a city changes hands.
            _scenarioData = objects.Scenario ?? new Scenario();

            var gameData = objects.GameData;
            TurnNumber = gameData.TurnNumber;
            DifficultyLevel = gameData.DifficultyLevel;
            Date = new Date(gameData.StartingYear, gameData.TurnYearIncrement, gameData.DifficultyLevel);

            PollutionSkulls = gameData.NoPollutionSkulls;
            
            GlobalTempRiseOccured = gameData.GlobalTempRiseOccured;
            NoOfTurnsOfPeace = gameData.NoOfTurnsOfPeace;

            var playerCiv = GetPlayerCiv;
            var activePlayer = Players[playerCiv.Id];
            
            var firstUnit = objects.ActiveUnit is { Dead: false } ? objects.ActiveUnit : playerCiv.Units.FirstOrDefault(u=>u.AwaitingOrders);

            if (firstUnit == null)
            {
                activePlayer.ActiveTile = playerCiv.Cities[0].Location;
            }
            else
            {
                activePlayer.SetUnitActive(firstUnit, false);
            }

            _activeCiv = playerCiv;
            AllCities.AddRange(objects.Cities);
            if (gameData.CitiesBuiltSoFar == null)
            {
                foreach (Civilization civ in AllCivilizations)
                {
                    CitiesBuiltSoFar[civ] = 0;
                }
            } else {
                for (int tribeN = 0; tribeN < gameData.CitiesBuiltSoFar.Length; tribeN++)
                {
                    int citiesBuilt = gameData.CitiesBuiltSoFar[tribeN];
                    Civilization? civ = AllCivilizations.Find(
                        civ => civ.TribeId == tribeN && civ.PlayerType != PlayerType.Barbarians);
                    if (civ != null)
                    {
                        CitiesBuiltSoFar[civ] = citiesBuilt;
                    }
                }
            }

            for (var index = 0; index < _maps.Length; index++)
            {
                var map = _maps[index];
                map.NormalizeIslands();
                map.CalculateFertility(Rules.Terrains[index]);
                AllCities.ForEach(c =>
                {
                    map.AdjustFertilityForCity(c.Location);
                });
            }

            foreach (var civilization in AllCivilizations)
            {
                this.SetImprovementsForCities(civilization);
            }

            foreach (var map in _maps)
            {
                foreach (var tile in map.Tile)
                {
                    if (tile is { CityHere: null, Improvements.Count: > 0 })
                    {
                        foreach (var construct in tile.Improvements.Where(c=>TerrainImprovements.ContainsKey(c.Improvement)))
                        {
                            var improvement = TerrainImprovements[construct.Improvement];
                            var terrain = improvement.AllowedTerrains[tile.Z]
                                .FirstOrDefault(t => t.TerrainType == (int)tile.Type);
                            if (terrain is not null)
                            {
                                tile.BuildEffects(improvement, terrain, construct.Level);
                            }
                        }
                    }
                }
            }

            foreach (var city in AllCities)
            {
                var government = Rules.Governments[city.Owner.Government];
                city.SetUnitSupport(government);
                city.CalculateOutput(city.Owner.Government, this);
            }

            Power.AssignPowerRanks(this);
            History = HistoryUtils.ReconstructHistory(this);
        }
    }
}
