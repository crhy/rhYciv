using RhyCiv.Engine;
using Model.Core.Mapping;
using Model.Core.Production;
using Model.Core.Units;

namespace Model.Core.Cities
{
    public class City : IMapItem
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int MapIndex { get; set; } = 0;
        public bool CanBuildCoastal { get; set; }
        public bool AutobuildMilitaryRule { get; set; }
        public bool StolenTech { get; set; }
        public bool ImprovementSold { get; set; }

        /// <summary>
        /// Whether this city has already paid the penalty for switching what it
        /// builds this turn. Civ II charges it once, so flipping back and forth in
        /// one turn costs no more than a single change.
        /// </summary>
        public bool ProductionChanged { get; set; }
        public bool WeLoveKingDay { get; set; }
        public bool CivilDisorder { get; set; }
        public bool CanBuildHydro { get; set; }
        public bool CanBuildShips { get; set; }
        public int Objective { get; set; }
        public bool AutobuildDomesticAdvisor { get; set; }
        public bool AutobuildMilitaryAdvisor { get; set; }
        public Civilization Owner { get; set; } = null!;
        public int OwnerId => Owner.Id;
        public int Size { get; set; }
        public Civilization WhoBuiltIt { get; set; } = null!;
        public bool[] WhoKnowsAboutIt { get; set; } = [];
        public int[] LastSizeRevealedToCivs { get; set; } = [];
        public int FoodInStorage { get; set; }
        public int NetTrade { get; set; }
        public string Name { get; set; } = string.Empty;
        public int NoOfSpecialistsx4 { get; set; }
        // Civ II stores specialist kind separately from the quarter-citizen count.
        // Values use the classic people indexes: 8 entertainer, 9 taxman, 10 scientist.
        public int[] SpecialistTypes { get; set; } = [];
        public IProductionOrder ItemInProduction { get; set; } = null!;
        public int ActiveTradeRoutes { get; set; }
        public Commodity[]? CommoditySupplied { get; set; }
        public Commodity[]? CommodityDemanded { get; set; }
        public Commodity[]? CommodityInRoute { get; set; }
        public int[]? TradeRoutePartnerCity { get; set; }
        
        public TradeRoute[] TradeRoutes { get; set; } = [];
        
        public int NoOfTradeIcons { get; set; }

        public int HappyCitizens { get; set; }
        public int UnhappyCitizens { get; set; }

        public readonly SortedList<int, Improvement> OrderedImprovements = new();
        public IReadOnlyList<Improvement> Improvements => OrderedImprovements.Values.ToArray();
        public List<Unit> UnitsInCity => Location.UnitsHere;
        /// <summary>
        /// The units this city pays for.
        /// <para>
        /// A unit killed in combat is marked dead and taken off the map, but it is
        /// left in its owner's unit list -- only disbanding removes it. Without the
        /// check for that, a city went on listing its dead in the support box and,
        /// worse, went on paying their shield and food upkeep and counting them
        /// towards the unhappiness of troops in the field.
        /// </para>
        /// </summary>
        public List<Unit> SupportedUnits =>
            Owner.Units.Where(unit => !unit.Dead && unit.HomeCity == this).ToList();
        public bool AnyUnitsPresent() => Location.UnitsHere.Count > 0;

        public int FoodProduction { get; set; }

        public int Food => Math.Min(FoodProduction, FoodConsumption);

        public int FoodConsumption { get; set; }

        public int SurplusHunger { get; set; }

        public int Trade { get; set; }
        public int Corruption { get; set; }
        
        // PRODUCTION
        public int TotalProduction { get; set; }
        public int Production { get; set; }
        public int ShieldsProgress { get; set; }

        /// <summary>
        /// Whether somebody has already taken an advance out of this city.
        /// <para>
        /// Civ II allows one theft per city and no more, however many agents are
        /// sent afterwards. Without that a rival capital is an endless supply of
        /// technology to anybody willing to keep building Diplomats.
        /// </para>
        /// </summary>
        public bool TechnologyStolen { get; set; }
        public ProductionQueue ConstructionQueue { get; set; } = new();

        
        public int Support { get; set; }
        public int Waste { get; set; }
        
        public Tile Location { get; set; } = null!;
        public List<Tile> WorkedTiles { get; } = [];
        public int Pollution { get; set; }
        
    }
}
