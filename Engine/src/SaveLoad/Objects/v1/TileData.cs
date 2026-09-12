using System.Collections.Generic;
using RhyCiv.Engine.Terrains;
using Model.Core.Mapping;

namespace RhyCiv.Engine.SaveLoad;

public class TileData
{
    /// <summary>
    /// The terrain of the tile
    /// </summary>
    public int T { get; set; }

    /// <summary>
    /// I the improvements at the tile
    /// </summary>
    public string? I { get; set; }
    
    /// <summary>
    /// The players that can see the tile and what they see A means all
    /// </summary>
    public IList<string> P { get; set; } = [];
    
    /// <summary>
    /// True if the tile has a river
    /// </summary>
    public bool R { get; set; }

    /// <summary>
    /// True if this square's goody hut is still standing.
    /// </summary>
    /// <remarks>
    /// Where the huts are is worked out from the square's coordinates and the
    /// map's seed rather than stored, which is how Civ II does it and is fine
    /// until one is taken: nothing recorded that, so every hut a game had ever
    /// entered was standing again the moment the game was loaded. Reported as
    /// huts appearing in country that had already been explored.
    /// </remarks>
    public bool H { get; set; }
}
