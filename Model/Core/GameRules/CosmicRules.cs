namespace Model.Core.GameRules
{
    public class CosmicRules
    {
        public int MovementMultiplier { get; set; } = 3;
        
        public int ChanceTriremeLost { get; set; } = 2;
        public int FoodEatenPerTurn { get; set; } = 2;
        public int RowsFoodBox { get; set; } = 10;
        public int RowsShieldBox { get; set; } = 10;
        public int SettlersEatTillMonarchy { get; set; } = 1;
        public int SettlersEatFromCommunism { get; set; } = 2;
        public int CitySizeUnhappyChieftain { get; set; } = 7;
        public int RiotFactor { get; set; } = 14;
        public int ToExceedCitySizeAqueductNeeded { get; set; } = 8;
        public int SewerNeeded { get; set; } = 12;
        public int TechParadigm { get; set; } = 10;
        public int BaseTimeEngineersTransform { get; set; } = 20;
        public int MonarchyPaysSupport { get; set; } = 3;
        public int CommunismPaysSupport { get; set; } = 3;
        public int FundamentalismPaysSupport { get; set; } = 10;
        public int CommunismEquivalentPalaceDistance { get; set; } = 0;
        public int FundamentalismScienceLost { get; private set; } = 50;
        public int ShieldPenaltyTypeChange { get; set; } = 50;
        public int MaxParadropRange { get; set; } = 10;
        public int MassThrustParadigm { get; set; } = 75;

        // Max effective science rate in fundamentalism (x10, so 5 = 50%)
        public int MaxEffectiveScienceRate { get; private set; } = 5;

        /// Value of each citizen to the Civilization Score.
        public int CitizenValue { get; set; } = 1;

        ///	 Value of each wonder to the Civilization Score.
        public int WonderValue { get; set; } = 20;

        /// Reward for landing on A. Centauri first = this multiplier*(# of habitats)*(prob. of success) 
        public int AlphaCentariLandingValue { get; set; } = 1;

        /// Cost to Civilization Score (+ or -) for each extant non-AI controlled polluted tile.
        public int PolutionValue { get; set; } = -10;

        /// For each turn of peace after turn 199, this amount *3 is added to Civilization Score.
        public int PeaceValue { get; set; } = 1;

        /// Value to the Civilization Score of each future tech researched.
        public int FutureTechValue { get; set; } = 5;

        /// Penalty assessed to Civilization Score each time player betrays another race.
        public int BetrayalValue { get; set; } = 0;

        /// Cost to Civilization Score (+ or -) for each unit destroyed.
        public int UnitDestroyedValue { get; set; } = 0;

        /// <summary>
        /// bitmask for goodie huts, right bit =map0, 0=no goodie huts
        /// </summary>
        public bool[] MapHasGoddyHuts { get; set; } = [];

        /// Helicopters pick up huts 0=no 1=yes
        public bool HelicoptersCanCollectHuts { get; set; } = false;

        public int RoadMovement { get; set; } = 1;
        public int RiverMovement { get; set; } = 1;
        public int AlpineMovement { get; set; } = 1;
        public int RailroadMovement { get; set; } = 0;

        // The improvement that lifts the aqueduct size cap and the one that lifts the
        // sewer size cap. They default to the hardcoded ids ImprovementType.Aqueduct (9)
        // and ImprovementType.SewerSystem (23) so today's behaviour is preserved, and a
        // scenario can remap them through the cosmic script API (see CosmicScripts and
        // CityExtensions.CanGrow). The Model assembly cannot reference the Engine
        // ImprovementType enum (Engine references Model, not the reverse), so the enum's
        // pinned numeric value is used.
        //
        // IMPORTANT: these are appended at the end on purpose. RulesParser.ProcessCosmicRules
        // maps the standalone @COSMIC section onto CosmicRules properties positionally by
        // declaration order; inserting a property in the middle would shift every later
        // rule and silently corrupt the ruleset. Do not move these above the movement block.
        public int AqueductImprovementType { get; set; } = 9;
        public int SewerImprovementType { get; set; } = 23;
    }
}
