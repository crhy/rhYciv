namespace Model.Core.Mapping
{
    public class ImprovementLevel
    {
        /// <summary>
        /// The name for this level Eg Road/Railroad
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Tech required to build this level
        /// </summary>
        public int RequiredTech { get; set; }

        /// <summary>
        /// A city improvement the civilisation must own somewhere before this
        /// level may be built, or 0 (ImprovementType.Nothing) for none. Only
        /// Farmland uses it: in Civ II farmland needs a Supermarket as well as
        /// Refrigeration, and every other level is untouched by the default.
        /// </summary>
        public int RequiredCityImprovement { get; set; }

        /// <summary>
        /// Error popup to show when build is attempted without required tech
        /// </summary>
        public string MissingRequiredTechMessage { get; set; } = string.Empty;

        /// <summary>
        /// Error popup to show when build is attempted without the required city
        /// improvement
        /// </summary>
        public string MissingRequiredImprovementMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// Message to show when required tech is gained
        /// </summary>
        public string EnabledMessage { get; set; } = string.Empty;
        
        /// <summary>
        /// The label for building this
        /// </summary>
        public string BuildLabel { get; set; } = string.Empty;
        
        /// <summary>
        /// Extra multiplier if this level is harder or easier to build
        /// </summary>
        public int BuildCostMultiplier { get; set; }
        
        public List<TerrainImprovementAction> Effects { get; set; } = [];
    }
}
