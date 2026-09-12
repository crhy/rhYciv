using System;
using System.Threading.Tasks;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.MapObjects;
using Model.Core;
using Model.Core.Mapping;

namespace RhyCiv.Engine
{
    public class MapGenerator
    {
        public static Task<Map[]> GenerateMap(GameInitializationConfig config)
        {
            return Task.Run(() =>
            {
                var area = config.WorldSize;
                // Rules.Maps already contains the standard map descriptor. Extra
                // entries represent actual secondary maps, so adding one here
                // created an unintended duplicate map for every new game.
                var maps = new Map[Math.Max(1, config.Rules.Maps?.Length ?? 0)];

                var width = area[0];
                var height = area[1];

                if (config.TerrainData != null)
                {
                    var mainMap = new Map(config.FlatWorld, 0)
                    {
                    
                        XDim = width,
                        YDim = height,
                        ResourceSeed = config.ResourceSeed ?? config.Random.Next(64),
                        Tile = new Tile[width, height]
                    };
                    var terrains = config.Rules.Terrains;
                    var index = 0;


                    var yMax = mainMap.Tile.GetLength(1) - 1;
                    for (int y = 0; y < mainMap.Tile.GetLength(1); y++)
                    {
                        var odd = y % 2;
                        for (int x = 0; x < mainMap.Tile.GetLength(0); x++)
                        {
                            var terra = config.TerrainData[index++];
                            var tile = new Tile(2 * x + odd, y, terrains[0][terra & 0xF], mainMap.ResourceSeed, mainMap, x,
                                new bool[config.NumberOfCivs + 1])
                            {
                                River = terra > 100
                            };
                            mainMap.Tile[x, y] = tile;
                        }
                    }

                    if (!config.FlatWorld)
                    {
                        var ocean = terrains[0][(int) TerrainType.Ocean];
                        var arctic = terrains[0][(int) TerrainType.Glacier];
                        for (int x = 0; x < mainMap.Tile.GetLength(0); x++)
                        {
                            if (mainMap.Tile[x, 0].Terrain == ocean)
                            {
                                mainMap.Tile[x, 0].Terrain = arctic;
                            }

                            if (mainMap.Tile[x, yMax].Terrain == ocean)
                            {
                                mainMap.Tile[x, yMax].Terrain = arctic;
                            }
                        }
                    }
                    
                    mainMap.NormalizeIslands();
                    
                    mainMap.CalculateFertility(terrains[0]);
                    MapObjects.RiverFlow.Compute(mainMap);

                    maps[0] = mainMap;
                }

                for (int i = 0; i < maps.Length; i++)
                {
                    if (maps[i] == null)
                    {
                        maps[i] = GenerateMap(config, width, height);
                    }
                }

                return maps;
            });
        }

        private static Map GenerateMap(GameInitializationConfig config, int width, int height)
        {
            var mainMap = new Map(config.FlatWorld, 0)
            {
                XDim = width,
                YDim = height,
                ResourceSeed = config.ResourceSeed ?? config.Random.Next(64),
                Tile = new Tile[width, height]
            };
            var terrains = config.Rules.Terrains;
            var world = ClassicWorldGenerator.Generate(config, width, height);
            for (var y = 0; y < height; y++)
            {
                var odd = y % 2;
                for (var x = 0; x < width; x++)
                {
                    mainMap.Tile[x, y] = new Tile(2 * x + odd, y,
                        terrains[0][(int)world.Terrain[x, y]], mainMap.ResourceSeed, mainMap, x,
                        new bool[config.NumberOfCivs + 1])
                    {
                        River = world.Rivers[x, y],
                        Island = -1
                    };
                }
            }

            mainMap.NormalizeIslands();
            mainMap.CalculateFertility(terrains[0]);

            // Which gauge each river tile is drawn at comes from how far it sits
            // from the sea along its own watercourse.
            MapObjects.RiverFlow.Compute(mainMap);

            return mainMap;
        }
    }
}

//                  function flood_Generator(data: MapData, options: { [key: string]: string }, mapRandom: FastRandom): void {
//
//     const avaliableLand: Set<Tile> = new Set(createTiles(data, mapRandom));
//
//     const halfSize = data.height / 2;
//
//     data.regions.push({ name: "Open Sea", locations: [] })
//
//     let landUsed = 0
//     const landRequired = avaliableLand.size / 4;
//
//     let continents = 0
//
//     const minIslandSize = 3;
//
//     const maxIslandSize = 300;
//
//     while (landUsed < landRequired || avaliableLand.size > 0) {
//         const candidate: Tile = mapRandom.take(avaliableLand);
//
//         const coastSet: Set<Tile> = new Set()
//         const edgeSet: Set<Tile> = new Set();
//         candidate.continent = continents++;
//         candidate.terrain = selectTerrain(candidate, halfSize, mapRandom);
//         candidate.modifiers = selectModifier(candidate, mapRandom);
//         const islandTiles = [candidate];
//
//         const size = mapRandom.nextRange(minIslandSize, maxIslandSize)
//         let [minX, maxX, minY, maxY] = [candidate.x, candidate.x, candidate.y, candidate.y];
//
//         let xRange = (maxX - minX + 1) * 2
//         let [minYLim, maxYLim] = [minY - xRange, maxY + xRange ]
//
//         let yRange = (maxY - minY + 1) * 2
//         let [minXLim, maxXLim] = [minX - yRange, maxX + yRange]
//
//         for (const tile of neighbours(candidate, data)) {
//             if (tile && tile.continent === -1) {
//                 edgeSet.add(tile)
//                 avaliableLand.delete(tile)
//             }
//         }
//         while (islandTiles.length < size && edgeSet.size > 0) {
//             const choice = mapRandom.take(edgeSet)
//             islandTiles.push(choice)
//             if (choice.x < minX) {
//                 minX = choice.x
//                 xRange = (maxX - minX + 1) * 2;
//                 [minYLim, maxYLim] = [minY - xRange, maxY + xRange]
//             } else if (choice.x > maxX) {
//                 maxX = choice.x
//                 xRange = (maxX - minX + 1) * 2;
//                 [minYLim, maxYLim] = [minY - xRange, maxY + xRange]
//             } else if (choice.y < minY) {
//                 minY = choice.y
//                 yRange = (maxY - minY + 1) * 2;
//                 [minXLim, maxXLim] = [minX - yRange, maxX + yRange];
//             } else if (choice.y > maxY) {
//                 maxY = choice.y;
//                 yRange = (maxY - minY + 1) * 2;
//                 [minXLim, maxXLim] = [minX - yRange, maxX + yRange];
//             }
//             choice.continent = candidate.continent;
//             choice.terrain = selectTerrain(choice, halfSize, mapRandom);
//             choice.modifiers = selectModifier(choice, mapRandom);
//             for (const tile of neighbours(choice, data)) {
//                 if (tile && tile.continent === -1) {
//                     if (tile.y > minYLim && tile.y < maxYLim && tile.x < maxXLim && tile.x > minXLim) {
//                         edgeSet.add(tile);
//                     } else {
//                         coastSet.add(tile);
//                     }
//                     tile.continent = 0;
//                     avaliableLand.delete(tile);                    
//                 }
//             }
//         }
//         
//         // Add back reserved coast tiles
//         for (let coast of coastSet) {
//             edgeSet.add(coast)
//         }
//
//         if (islandTiles.length < minIslandSize) {
//             continents--;
//             for (let tile of islandTiles) {
//                 tile.continent = 0;
//                 tile.terrain = Ocean;
//                 tile.modifiers = []
//             }
//         } else {
//
//             //fill tiny lakes
//             for (let tile of edgeSet) {
//                 let scene = 0
//                 let neighbour: Tile | null = null
//                 for (let n of neighbours(tile, data)) {
//                     if (!n) continue
//                     if (n.terrain === Ocean) {
//                         neighbour = null;
//                         break;
//                     }
//                     scene++
//                     if (mapRandom.nextFloat() < 1 / scene) {
//                         neighbour = n;
//                     }
//                 }
//                 if (neighbour !== null) {
//                     tile.terrain = neighbour.terrain;
//                     tile.continent = neighbour.continent;
//                     islandTiles.push(tile)
//                     edgeSet.delete(tile)
//                 }
//             }
//         }
//         landUsed += islandTiles.length;
//
//         //reserve edge tiles
//         let extraEdge = islandTiles.length * 3 - edgeSet.size
//         while (extraEdge > 0 && edgeSet.size > 0) {
//             const tile = mapRandom.take(edgeSet)
//             for (let n of neighbours(tile, data)) {                
//                 if (n && n.continent === -1) {
//                     edgeSet.add(n);
//                     n.continent = 0
//                     avaliableLand.delete(n)
//                     extraEdge -= 1;
//                 }
//             }
//         }
//     }
// }
