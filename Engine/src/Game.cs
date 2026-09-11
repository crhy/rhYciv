using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.Scripting;
using RhyCiv.Engine.Units;
using Model;
using Model.Core;
using Model.Core.Cities;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Units;

namespace RhyCiv.Engine
{
    public partial class Game : IGame
    {
        private readonly Options _options;
        private readonly Rules _rules;
        // Only the load-game constructor assigned this, so every new game carried a
        // null here and the first city capture threw on ScenarioData. A plain
        // Scenario is what a non-scenario game wants anyway: every restriction off.
        private readonly Scenario _scenarioData = new();
        private readonly BarbarianActivityType _barbarianActivity;
        public FastRandom Random { get; set; } = new();
        public List<City> AllCities { get; } = new();

        public IHistory History { get; }

        public List<Civilization> AllCivilizations { get; } = new();

        public List<Civilization> ActiveCivs => AllCivilizations.Where(c => c.Alive).ToList();
        public Options Options => _options;
        public Scenario ScenarioData => _scenarioData;
        public Rules Rules => _rules;

        public IGameDate Date { get; }
        public int TurnNumber { get; private set; }

        public int DifficultyLevel { get; set; }
        public int BarbarianActivity => (int)_barbarianActivity;
        public int PollutionSkulls { get; set; }
        public int GlobalTempRiseOccured { get; set; }
        public int NoOfTurnsOfPeace { get; set; }

        public Tile ActiveTile
        {
            get => Players[_activeCiv.Id].ActiveTile;
            set => Players[_activeCiv.Id].ActiveTile = value;
        }

        public IPlayer ActivePlayer => Players[_activeCiv.Id];

        private int _activeCivId = -1;
        
        private Civilization
            _activeCiv; // ActiveCiv can be AI. PlayerCiv is human. They are equal except during enemy turns.

        public Civilization GetActiveCiv => _activeCiv;
        
        private readonly Map[] _maps;

        public IList<Map> Maps => _maps;
        public IScriptEngine Script { get; }

        public int NoMaps => _maps.Length;

        public int TotalMapArea => _maps.Select(m => m.Tile.GetLength(0) * m.Tile.GetLength(1)).Sum();
        public Dictionary<string, List<string>?> CityNames { get; set; }
        public Dictionary<Civilization, int> CitiesBuiltSoFar { get; } = new Dictionary<Civilization, int>();
        public Civilization GetPlayerCiv => AllCivilizations.First(c => c.PlayerType == PlayerType.Local);

        public IPlayer[] Players { get; }


        /// <summary>
        /// Brings one civilisation's record of some squares up to date whether or
        /// not it can currently see them.
        /// </summary>
        /// <remarks>
        /// A city changing hands is the case this exists for, and losing one is
        /// the case that was wrong. What a player sees drawn is their remembered
        /// record of a square, and that record is only refreshed for civilisations
        /// that can see the square now -- but a city stops being visible to its
        /// owner at the very moment they stop owning it: the garrison is gone and
        /// the city is somebody else's, so nothing of theirs is in sight of it any
        /// more. The map therefore went on drawing it in their own colours,
        /// indefinitely. Reported as barbarians taking a city and the city not
        /// turning red.
        ///
        /// They have just been told they lost it, so the map has no business
        /// disagreeing.
        /// </remarks>
        public void UpdateTilesFor(IList<Tile> tilesChanged, int civilizationId)
        {
            if (tilesChanged.Count == 0 || civilizationId < 0 || civilizationId >= Players.Length)
            {
                return;
            }

            foreach (var tile in tilesChanged)
            {
                tile.UpdatePlayer(civilizationId);
            }

            Players[civilizationId].MapChanged(tilesChanged.ToList());
        }

        public void UpdateTiles(IList<Tile> tilesChanged)
        {
            foreach (var player in Players)
            {
                var tiles = tilesChanged.Where(t => t.Map.IsCurrentlyVisible(t, player.Civilization.Id)).ToList();
                if (tiles.Count > 0)
                {
                    tiles.ForEach(t => t.UpdatePlayer(player.Civilization.Id));
                    player.MapChanged(tiles);
                }
            }
        }

        private double? _maxDistance;
        private ImprovementEncoder? _encoder;

        public double MaxDistance
        {
            get { return _maxDistance ??= ComputeMaxDistance(); }
        }

        public IDictionary<int, TerrainImprovement> TerrainImprovements { get; set; }

        public IImprovementEncoder ImprovementEncoder => _encoder ??= new ImprovementEncoder(TerrainImprovements);

        private double ComputeMaxDistance()
        {
            var xLength = _maps[0].Tile.GetLength(0);
            var yLength = _maps[0].Tile.GetLength(1);

            if (_options.FlatEarth)
            {
                return Utilities.DistanceTo(_maps[0].Tile[0, 0], _maps[0].Tile[xLength - 1, yLength - 1]);
            }

            return Utilities.DistanceTo(_maps[0].Tile[0, 0], _maps[0].Tile[(int)xLength / 2, yLength - 1]);
        }

        public string Order2String(int unitOrder)
        {
            var order = Rules.Orders.FirstOrDefault(t => t.Type == unitOrder);
            return order != null ? order.Name : Labels.For(LabelIndex.NoOrders);
        }
        
        public void ConnectPlayer(IPlayer player)
        {
            var id = player.Civilization.Id;
            var currentPlayer = Players[id];
            player.ActiveTile = currentPlayer.ActiveTile;
            player.SetUnitActive(currentPlayer.ActiveUnit, false);
            Players[id] = player;
            Script.Connect(player.Ui);
        }

        public string GetRealmName(int governmentLevel)
        {
            return governmentLevel switch
            {
                0 or 1 => Labels.For(LabelIndex.Empire),
                2 => Labels.For(LabelIndex.Kingdom),
                3 => Labels.For(LabelIndex.PeoplesRepublic),
                4 => Labels.For(LabelIndex.HolyEmpire),
                5 or 6 => Labels.For(LabelIndex.Republic),
                _ => throw new ArgumentOutOfRangeException($"Not expected government value: {governmentLevel}")
            };
        }
    }
}
