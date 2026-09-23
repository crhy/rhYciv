namespace RhyCiv.Engine.Enums
{
    /// <summary>
    /// Outcome of asking the engine to put a citizen on or take one off a
    /// city-radius tile (the resource-map click). Failures carry the reason so
    /// the UI can say why the click did nothing instead of silently ignoring it.
    /// </summary>
    public enum WorkTileResult
    {
        /// <summary>Clicked the city's own centre: cleared worked tiles and re-ran AutoAdd.</summary>
        ClearedAndReassigned = 0,

        /// <summary>Took a citizen off this tile and made them a specialist.</summary>
        Released = 1,

        /// <summary>Put a free specialist onto this tile.</summary>
        Assigned = 2,

        /// <summary>No free specialist: moved a citizen from the worst worked tile onto this one.</summary>
        Reassigned = 3,

        /// <summary>Enemy units are standing on the square.</summary>
        ForeignUnits = 4,

        /// <summary>The square holds a city that is not ours.</summary>
        ForeignCity = 5,

        /// <summary>Another city of ours already works the square.</summary>
        ForeignWorked = 6,

        /// <summary>Wanted to assign a worker but there is no specialist and nothing to reassign.</summary>
        NoSpecialistAvailable = 7,

        /// <summary>Wanted to release a worker but every citizen is already a specialist.</summary>
        NoSlotForSpecialist = 8,
    }

    public static class WorkTileResultExtensions
    {
        /// <summary>True when the click changed (or deliberately re-ran) the city's work assignment.</summary>
        public static bool IsSuccess(this WorkTileResult result) =>
            result is WorkTileResult.ClearedAndReassigned
                or WorkTileResult.Released
                or WorkTileResult.Assigned
                or WorkTileResult.Reassigned;
    }
}
