using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.SaveLoad.SerializationUtils;
using RhyCiv.Engine.Terrains;
using Model.Core;
using Model.Core.GameRules;
using Model.Core.Mapping;

namespace RhyCiv.Engine.SaveLoad;

public class MapSerializer
{

    public static List<Map> Read(JsonElement jsonElement, Rules rules, ImprovementEncoder? improvementEncoder)
    {
        var maps = new List<Map>();
        foreach (var mapElement in jsonElement.EnumerateArray())
        {
            var mapData = JsonSerializer.Deserialize<JsonMapData>(mapElement.GetRawText());
            if(mapData == null) continue;

            var index = maps.Count;
            var map = new Map(mapData.FlatWorld, index)
            {
                MapRevealed = mapData.MapRevealed,
                WhichCivsMapShown = mapData.WhichCivsMapShown,
                StartingClickedXy = mapData.ClickedXy,
                XDim = mapData.Xdim,
                YDim = mapData.Ydim,
                ResourceSeed = mapData.ResourceSeed,
                LocatorXdim = mapData.LocatorXdim,
                LocatorYdim = mapData.LocatorYdim
            };
            map.Tile = new Tile[map.XDim, map.YDim];
            var tileEnumerator = mapData.Tiles.GetEnumerator();
            for (var col = 0; col < map.XDim; col++)
            {
                for (var row = 0; row < map.YDim; row++)
                {
                    tileEnumerator.MoveNext();
                    var tileData = tileEnumerator.Current;
                    IList<ConstructedImprovement>? improvements = null;
                    PlayerTile?[]? playerKnowledge = null;
                    if (improvementEncoder != null)
                    {
                        improvements = improvementEncoder.Decode(tileData.I);
                        playerKnowledge = improvementEncoder.DecodePlayer(tileData.P, improvements);
                    }

                    var tile = new Tile(2 * col + (row % 2), row, rules.Terrains[index][tileData.T], map.ResourceSeed,
                        map, col, playerKnowledge?.Select(playerTile => playerTile!= null ).ToArray() ?? [])
                    {
                        River = tileData.R,
                        PlayerKnowledge = playerKnowledge,
                    };
                    if (improvements != null)
                    {
                        tile.Improvements.AddRange(improvements);
                    }

                    // The constructor put a hut back wherever the seed says one
                    // belongs. If this save knows which are still standing, the
                    // ones already taken are taken away again.
                    if (mapData.HutsRecorded && !tileData.H)
                    {
                        tile.HasGoodieHut = false;
                    }

                    map.Tile[col, row] = tile;
                }
            }

            maps.Add(map);
        }
        return maps;
    }

    public static void Write(Utf8JsonWriter writer, IList<Map> maps, IImprovementEncoder? improvementEncoder = null)
    {
        writer.WriteStartArray();
        foreach (var map in maps)
        {
            writer.WriteNonDefaultFields(new JsonMapData(map, improvementEncoder));
        }
        writer.WriteEndArray();
    }
}
