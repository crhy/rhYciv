using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Terrains;
using Model;
using Model.Images;
using Model.ImageSets;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using Raylib_CSharp.Transformations;
using RaylibUI;
using RaylibUtils;
using System.Numerics;
using Model.Core.GameRules;
using Model.Core.Mapping;

namespace RhyCiv.UI.Classic.ImageLoader
{
    public static class TerrainLoader
    {
        /// <summary>
        /// Composition scale used when nothing better is known. A 2x map tile
        /// matches the physical pixel density of a 3840x2160 display while
        /// retaining Civ II's original 64x32 logical tile geometry.
        /// </summary>
        public const int DefaultTerrainRenderScale = 2;

        /// <summary>
        /// Upper bound on composition scale. A tile is composed at
        /// 64 x 32 x scale, so this caps a single tile at 512x256 pixels.
        /// </summary>
        public const int MaximumTerrainRenderScale = 8;
        private static readonly string[] FossTerrainNames =
        [
            "desert",
            "plains",
            "grassland",
            "grassland", // The forest connection overlay supplies the trees.
            "hills",
            "mountains",
            "tundra",
            "glacier",
            "swamp",
            "jungle",
            "ocean"
        ];

        /// <summary>
        /// Terrain sets already built for this interface, keyed by composition
        /// scale. Building one resizes every base texture and recomposes every
        /// overlay, and the images end up in the shared image cache either way, so
        /// a scale is built once and then reused whenever the zoom returns to it.
        /// </summary>
        private static readonly Dictionary<IUserInterface, Dictionary<int, List<TerrainSet>>> BuiltSets = new();

        /// <summary>
        /// Loads terrain for a newly selected ruleset, discarding anything built
        /// for the previous one.
        /// </summary>
        public static void LoadTerrain(Ruleset ruleset, IUserInterface active,
            int renderScale = DefaultTerrainRenderScale)
        {
            BuiltSets.Remove(active);
            UseTerrainScale(ruleset, active, renderScale);
        }

        /// <summary>
        /// Switches the active terrain to a given composition scale, building it
        /// the first time that scale is asked for.
        /// </summary>
        public static void UseTerrainScale(Ruleset ruleset, IUserInterface active, int renderScale)
        {
            renderScale = Math.Clamp(renderScale, 1, MaximumTerrainRenderScale);

            if (!BuiltSets.TryGetValue(active, out var byScale))
            {
                byScale = new Dictionary<int, List<TerrainSet>>();
                BuiltSets[active] = byScale;
            }

            if (!byScale.TryGetValue(renderScale, out var sets))
            {
                sets = new List<TerrainSet>();
                for (var i = 0; i < active.ExpectedMaps; i++)
                {
                    sets.Add(LoadTerrain(ruleset, i, active, renderScale));
                }

                byScale[renderScale] = sets;
            }

            active.TileSets.Clear();
            foreach (var set in sets)
            {
                active.TileSets.Add(set);
            }
        }

        private static TerrainSet LoadTerrain(Ruleset ruleset, int index, IUserInterface active, int renderScale)
        {
            // Initialize objects
            var terrain = new TerrainSet(64, 32, renderScale);

            // Get dither tile before making it transparent.
            // Threshold every pixel to pure black or white so AlphaMask produces
            // only fully-transparent or fully-opaque regions (no blue crosshatching).
            var ditherTile = Images.ExtractBitmap(MapIndexChange((BitmapStorage)active.PicSources["dither"][0], index, active));
            unsafe
            {
                var pixels = ditherTile.LoadColors();
                for (var i = 0; i < pixels.Length; i++)
                {
                    var c = pixels[i];
                    var lum = (c.R + c.G + c.B) / 3;
                    var bw = lum < 128 ? Color.Black : Color.White;
                    var x = i % ditherTile.Width;
                    var y = i / ditherTile.Width;
                    ditherTile.DrawPixel(x, y, bw);
                }
                Image.UnloadColors(pixels);
            }

            terrain.BaseTiles = active.PicSources["base1"].Select(t => MapIndexChange((BitmapStorage)t, index, active)).ToArray();
            var fossTerrainApplied = ApplyFossTerrainTextures(terrain, index, active);
            terrain.HighResBaseTiles = fossTerrainApplied;

            terrain.Specials = new[]
            {
                active.PicSources["special1"].Select(s => MapIndexChange((BitmapStorage) s, index, active)).ToArray(),
                active.PicSources["special2"].Select(s => MapIndexChange((BitmapStorage)s, index, active)).ToArray(),
            };

            terrain.Blank = MapIndexChange((BitmapStorage)active.PicSources["blank"][0], index, active);

            // 4 small dither tiles (base mask must be B/W)
            terrain.DitherMask = new[]
            {
                Image.FromImage(ditherTile, new Rectangle(32, 0, 32, 16)),
                Image.FromImage(ditherTile, new Rectangle(32, 16, 32, 16)),
                Image.FromImage(ditherTile, new Rectangle(0, 16, 32, 16)),
                Image.FromImage(ditherTile, new Rectangle(0, 0, 32, 16)),
            };

            terrain.DitherMaps = new[]
            {
                BuildDitherMaps(terrain.DitherMask[0], terrain.BaseTiles, 32, 0, terrain.Blank, fossTerrainApplied),
                BuildDitherMaps(terrain.DitherMask[1], terrain.BaseTiles, 32, 16, terrain.Blank, fossTerrainApplied),
                BuildDitherMaps(terrain.DitherMask[2], terrain.BaseTiles, 0, 16, terrain.Blank, fossTerrainApplied),
                BuildDitherMaps(terrain.DitherMask[3], terrain.BaseTiles, 0, 0, terrain.Blank, fossTerrainApplied),
            };

            terrain.River = active.PicSources["river"].Select(r => MapIndexChange((BitmapStorage)r, index, active)).ToArray();
            terrain.Forest = active.PicSources["forest"].Select(r => MapIndexChange((BitmapStorage)r, index, active)).ToArray();
            terrain.Mountains = active.PicSources["mountain"].Select(r => MapIndexChange((BitmapStorage)r, index, active)).ToArray();
            terrain.Hills = active.PicSources["hill"].Select(r => MapIndexChange((BitmapStorage)r, index, active)).ToArray();
            terrain.RiverMouth = active.PicSources["riverMouth"].Select(r => MapIndexChange((BitmapStorage)r, index, active)).ToArray();

            if (fossTerrainApplied)
            {
                ApplyFossOverlayArt(terrain);
                ApplyFossRiverArt(terrain);
                ApplyFossSpecialArt(terrain);
            }

            terrain.Coast = new IImageSource[8, 4];
            for (var i = 0; i < 8; i++)
            {
                terrain.Coast[i, 0] = MapIndexChange((BitmapStorage)active.PicSources["coastline"][4 * i + 0], index, active); // N
                terrain.Coast[i, 1] = MapIndexChange((BitmapStorage)active.PicSources["coastline"][4 * i + 1], index, active); // S
                terrain.Coast[i, 2] = MapIndexChange((BitmapStorage)active.PicSources["coastline"][4 * i + 2], index, active); // W
                terrain.Coast[i, 3] = MapIndexChange((BitmapStorage)active.PicSources["coastline"][4 * i + 3], index, active); // E
            }

            // Road & railroad
            terrain.ImprovementsMap = new Dictionary<int, ImprovementGraphic>();

            var roadGraphics = new ImprovementGraphic
            {
                Levels = new IImageSource[2, 9]
            };

            terrain.ImprovementsMap.Add(ImprovementTypes.Road, roadGraphics);

            for (var i = 0; i < 9; i++)
            {
                roadGraphics.Levels[0, i] = MapIndexChange((BitmapStorage)active.PicSources["road"][i], index, active);
                roadGraphics.Levels[1, i] = MapIndexChange((BitmapStorage)active.PicSources["railroad"][i], index, active);
            }

            if (fossTerrainApplied)
            {
                ApplyFossConnectionArt(terrain, roadGraphics);
            }

            var fieldGraphics = new ImprovementGraphic
            {
                Levels = new[,]
                {
                    { MapIndexChange((BitmapStorage)active.PicSources["irrigation"][0], index, active) },
                    { MapIndexChange((BitmapStorage)active.PicSources["farmland"][0], index, active) }
                }
            };
            terrain.ImprovementsMap.Add(ImprovementTypes.Irrigation, fieldGraphics);

            if (fossTerrainApplied)
            {
                ApplyFossFieldArt(terrain, fieldGraphics);
            }

            terrain.ImprovementsMap[ImprovementTypes.Mining] = new ImprovementGraphic
                { Levels = new[,] { { MapIndexChange((BitmapStorage)active.PicSources["mine"][0], index, active) } } };

            terrain.ImprovementsMap[ImprovementTypes.Pollution] = new ImprovementGraphic
                { Levels = new[,] { { MapIndexChange((BitmapStorage)active.PicSources["pollution"][0], index, active) } } };

            //Note airbase and fortress are now loaded directly by the cities loader
            terrain.GrasslandShield = MapIndexChange((BitmapStorage)active.PicSources["shield"][0], index, active);
            if (fossTerrainApplied)
            {
                var shieldPath = FindFossArtPath("grassshield.jpg")
                                 ?? FindFossArtPath("grassshield.png")
                                 ?? FindFossArtPath("shield.png");
                if (shieldPath != null)
                {
                    var composedShield = ComposeShieldTile(terrain, shieldPath);
                    if (composedShield != null)
                    {
                        terrain.GrasslandShield = new MemoryStorage(composedShield.Value,
                            $"FossShield-{terrain.RenderScale}");
                    }
                }
            }

            terrain.Huts = MapIndexChange((BitmapStorage)active.PicSources["hut"][0], index, active);
            if (fossTerrainApplied)
            {
                var hutPath = FindFossTerrainPath("goodyhut");
                if (hutPath != null)
                {
                    // A hut is a thing standing on the square, not a covering for
                    // it, so it is composed at well under tile width and sits a
                    // little above centre the way the special-resource cutouts do.
                    var composedHut = ComposeSpecialTile(terrain, hutPath, 0.44f, 0.86f);
                    if (composedHut != null)
                    {
                        terrain.Huts = new MemoryStorage(composedHut.Value,
                            $"FossHut-{terrain.RenderScale}");
                    }
                }
            }

            if (fossTerrainApplied)
            {
                terrain.CoastMarch = LoadCoastMarch(terrain);

                // The procedural shorelines are the fallback for when the
                // marching-squares tileset is not on disk.
                if (terrain.CoastMarch.Length != CoastMarchShapes.Length)
                {
                    var edgeWidth = terrain.TileWidth * terrain.RenderScale;
                    var edgeHeight = terrain.TileHeight * terrain.RenderScale;
                    terrain.ShallowEdge = new Image[4][];
                    for (var edge = 0; edge < 4; edge++)
                    {
                        terrain.ShallowEdge[edge] = new Image[CoastVariants];
                        for (var variant = 0; variant < CoastVariants; variant++)
                        {
                            terrain.ShallowEdge[edge][variant] =
                                BuildShallowEdge(edgeWidth, edgeHeight, edge, variant);
                        }
                    }
                }
            }

            return terrain;
        }

        /// <summary>
        /// Swaps the base terrain diamonds for the bundled FOSS textures, composed
        /// at the set's own render scale times Civ2's logical tile size.
        /// Returns true when the higher-resolution tiles were installed, so the
        /// connection overlays know they can be composed to match.
        /// </summary>
        private static bool ApplyFossTerrainTextures(TerrainSet terrain, int mapIndex, IUserInterface active)
        {
            // The bundled textures depict the classic Earth terrain set. Other Test of Time maps
            // retain their scenario-specific art until equivalent FOSS sets are available.
            if (mapIndex != 0)
            {
                return false;
            }

            var applied = false;
            for (var terrainIndex = 0;
                 terrainIndex < terrain.BaseTiles.Length && terrainIndex < FossTerrainNames.Length;
                 terrainIndex++)
            {
                var artPath = FindFossTerrainPath(FossTerrainNames[terrainIndex]);
                if (artPath == null)
                {
                    continue;
                }

                var replacement = Images.LoadImageFromFile(artPath).Image;
                if (replacement.Width <= 1 || replacement.Height <= 1)
                {
                    continue;
                }

                // The bundled terrain diamonds are rendered as turf slabs with a
                // dark soil rim around the edge. Zoom a little past that rim
                // before fitting the art to the tile, otherwise every tile shows
                // its own border and the map reads as a grid of separate slabs.
                const float keep = 0.82f;
                replacement.Crop(new Rectangle(
                    replacement.Width * (1f - keep) / 2f,
                    replacement.Height * (1f - keep) / 2f,
                    replacement.Width * keep,
                    replacement.Height * keep));

                replacement.Resize(terrain.TileWidth * terrain.RenderScale,
                    terrain.TileHeight * terrain.RenderScale);
                ApplyDiamondAlpha(replacement);
                terrain.BaseTiles[terrainIndex] = new MemoryStorage(replacement,
                    $"FossTerrain-{terrainIndex}-{terrain.RenderScale}-{artPath}");
                applied = true;
            }

            return applied;
        }

        /// <summary>
        /// Connection-overlay sets bundled as native 300x300 art. Each set holds
        /// eight variants; the sixteen neighbour-connection indices cycle through
        /// them, matching how the compatibility sheet is generated.
        /// </summary>
        private static readonly (string Directory, string Stem)[] FossOverlaySets =
        [
            ("Forest", "forest"),
            ("Mountains", "mountain"),
            ("Hills", "hill")
        ];

        private const int FossOverlayVariants = 8;

        /// <summary>
        /// Replaces the forest/hills/mountains/river connection overlays with the
        /// native high-resolution art, composed once at the working tile size.
        /// Routing them through the legacy 64x32 sheet cell and letting the tile
        /// compositor upscale it throws away half the detail the source carries.
        /// </summary>
        private static void ApplyFossOverlayArt(TerrainSet terrain)
        {
            foreach (var (directory, stem) in FossOverlaySets)
            {
                var variants = LoadFossOverlayVariants(terrain, directory, stem);
                if (variants == null)
                {
                    continue;
                }

                var target = stem switch
                {
                    "forest" => terrain.Forest,
                    "mountain" => terrain.Mountains,
                    "hill" => terrain.Hills,
                    _ => null
                };

                if (target == null || target.Length == 0)
                {
                    continue;
                }

                for (var i = 0; i < target.Length; i++)
                {
                    target[i] = variants[i % FossOverlayVariants];
                }
            }
        }

        private static IImageSource[]? LoadFossOverlayVariants(TerrainSet terrain, string directory, string stem)
        {
            var variants = new IImageSource[FossOverlayVariants];
            for (var variant = 0; variant < FossOverlayVariants; variant++)
            {
                var path = FindFossOverlayPath(directory, $"{stem}_{variant + 1:00}.png");
                if (path == null)
                {
                    return null;
                }

                var composed = ComposeOverlayTile(terrain, path);
                if (composed == null)
                {
                    return null;
                }

                variants[variant] = new MemoryStorage(composed.Value,
                    $"FossOverlay-{stem}-{variant}-{terrain.RenderScale}");
            }

            return variants;
        }

        /// <summary>
        /// Installs the worked-ground overlays. Irrigation and farmland stayed on
        /// the compatibility sheet's 64x32 cells, which the tile compositor
        /// point-scales up: over photographic terrain that read as a coarse blue
        /// lattice thrown across the square rather than as a ploughed field.
        /// </summary>
        private static void ApplyFossFieldArt(TerrainSet terrain, ImprovementGraphic fieldGraphics)
        {
            var names = new[] { "irrigation.png", "farmland.png" };
            for (var level = 0; level < names.Length && level < fieldGraphics.Levels.GetLength(0); level++)
            {
                var path = FindFossOverlayPath("Improvements", names[level]);
                if (path == null)
                {
                    continue;
                }

                var composed = ComposeConnectionTile(terrain, path);
                if (composed != null)
                {
                    fieldGraphics.Levels[level, 0] = new MemoryStorage(composed.Value,
                        $"FossField-{level}-{terrain.RenderScale}");
                }
            }
        }

        /// <summary>
        /// The four edge-sharing neighbours, in the order
        /// <c>MapNavigationFunctions.DirectNeighbours</c> yields them. MapImage
        /// builds the river sprite index as a bitmask over this order, bit 0
        /// first, and indexes <see cref="TerrainSet.RiverMouth"/> by position.
        /// </summary>
        private static readonly string[] FossRiverDirections = ["ne", "se", "sw", "nw"];

        /// <summary>
        /// Installs the river tiles and river mouths.
        /// <para>
        /// A river is one picture per tile, chosen by which of the four
        /// edge-sharing neighbours also carry a river -- sixteen distinct
        /// pictures. The bundled art was eight free-hand meanders, and the
        /// general overlay path assigned them as <c>index % 8</c>, so the picture
        /// drawn bore no relation to where the river actually ran and no two
        /// adjacent tiles lined up. The art is now composed per mask from
        /// half-spokes that meet on the tile boundary, so a river is continuous.
        /// </para>
        /// <para>
        /// River mouths were never replaced at all and stayed on the legacy sheet,
        /// which is why a river reaching the coast simply stopped.
        /// </para>
        /// </summary>
        /// <summary>How many gauges the banded river art is painted at.</summary>
        private const int RiverBandCount = 4;

        /// <summary>
        /// The river tiles again at each gauge, so a watercourse can be a trickle
        /// where it rises and an estuary where it reaches the sea. Leaves the bands
        /// empty if the art is not all there, and the unbanded set is then used for
        /// every tile exactly as before.
        /// </summary>
        private static void LoadRiverBands(TerrainSet terrain)
        {
            var bands = new IImageSource[RiverBandCount][];
            for (var band = 0; band < RiverBandCount; band++)
            {
                var tiles = new IImageSource[16];
                for (var mask = 0; mask < 16; mask++)
                {
                    var path = FindFossOverlayPath("Rivers", $"river_mask_{mask:00}_{band}.png");
                    var composed = path == null ? null : ComposeConnectionTile(terrain, path);
                    if (composed == null)
                    {
                        return;
                    }

                    tiles[mask] = new MemoryStorage(composed.Value,
                        $"FossRiver-{band}-{mask}-{terrain.RenderScale}");
                }

                bands[band] = tiles;
            }

            terrain.RiverBands = bands;
        }

        private static void ApplyFossRiverArt(TerrainSet terrain)
        {
            for (var mask = 0; mask < terrain.River.Length && mask < 16; mask++)
            {
                var path = FindFossOverlayPath("Rivers", $"river_mask_{mask:00}.png");
                if (path == null)
                {
                    continue;
                }

                var composed = ComposeConnectionTile(terrain, path);
                if (composed != null)
                {
                    terrain.River[mask] = new MemoryStorage(composed.Value,
                        $"FossRiver-{mask}-{terrain.RenderScale}");
                }
            }

            LoadRiverBands(terrain);

            for (var index = 0;
                 index < terrain.RiverMouth.Length && index < FossRiverDirections.Length;
                 index++)
            {
                var path = FindFossOverlayPath("Rivers",
                    $"river_mouth_{FossRiverDirections[index]}.png");
                if (path == null)
                {
                    continue;
                }

                var composed = ComposeConnectionTile(terrain, path);
                if (composed != null)
                {
                    terrain.RiverMouth[index] = new MemoryStorage(composed.Value,
                        $"FossRiverMouth-{index}-{terrain.RenderScale}");
                }
            }
        }

        /// <summary>
        /// Connection sprite order for <see cref="ImprovementGraphic.Levels"/>: slot
        /// 0 is the isolated stub drawn when a tile has no neighbour carrying the
        /// improvement, and slots 1-8 follow the neighbour order in
        /// <c>MapNavigationFunctions.Neighbours</c>, which MapImage indexes as
        /// <c>neighbour + 1</c>.
        /// </summary>
        private static readonly string[] FossConnectionNames =
            ["iso", "ne", "e", "se", "s", "sw", "w", "nw", "n"];

        private static readonly (int Level, string Directory, string Stem)[] FossConnectionSets =
        [
            (0, "Roads", "road"),
            (1, "Railroads", "railroad")
        ];

        /// <summary>
        /// Replaces the road and railroad connection sprites with the painted art.
        /// Both levels are replaced independently: if only one of the two sets is on
        /// disk, the other keeps the generated rosettes rather than the tile losing
        /// its roads entirely.
        /// </summary>
        private static void ApplyFossConnectionArt(TerrainSet terrain, ImprovementGraphic roadGraphics)
        {
            foreach (var (level, directory, stem) in FossConnectionSets)
            {
                var sprites = LoadFossConnectionSprites(terrain, directory, stem);
                if (sprites == null)
                {
                    continue;
                }

                for (var slot = 0; slot < sprites.Length; slot++)
                {
                    roadGraphics.Levels[level, slot] = sprites[slot];
                }
            }
        }

        private static IImageSource[]? LoadFossConnectionSprites(TerrainSet terrain, string directory, string stem)
        {
            var sprites = new IImageSource[FossConnectionNames.Length];
            for (var slot = 0; slot < FossConnectionNames.Length; slot++)
            {
                var path = FindFossOverlayPath(directory, $"{stem}_{FossConnectionNames[slot]}.png");
                if (path == null)
                {
                    return null;
                }

                var composed = ComposeConnectionTile(terrain, path);
                if (composed == null)
                {
                    return null;
                }

                sprites[slot] = new MemoryStorage(composed.Value,
                    $"FossConnection-{stem}-{FossConnectionNames[slot]}-{terrain.RenderScale}");
            }

            return sprites;
        }

        /// <summary>
        /// Scales connection art to fill the whole tile rectangle.
        /// <para>
        /// Unlike <see cref="ComposeOverlayTile"/>, this must not letterbox or
        /// bottom-align: the art is painted in the tile's own diamond space, where
        /// every spoke starts at the tile centre and ends on a specific corner or
        /// edge midpoint. Shifting or insetting it would move those endpoints and
        /// the halves drawn by two adjacent tiles would no longer meet.
        /// </para>
        /// </summary>
        private static Image? ComposeConnectionTile(TerrainSet terrain, string path)
        {
            var art = Images.LoadImageFromFile(path).Image;
            if (art.Width <= 1 || art.Height <= 1)
            {
                return null;
            }

            art.Resize(terrain.TileWidth * terrain.RenderScale, terrain.TileHeight * terrain.RenderScale);
            return art;
        }

        /// <summary>
        /// Fits square overlay art into a tile-sized transparent canvas, centred
        /// horizontally and resting on the bottom edge, so it lands where the
        /// classic sheet cell used to sit.
        /// </summary>
        private static Image? ComposeOverlayTile(TerrainSet terrain, string path)
        {
            var art = Images.LoadImageFromFile(path).Image;
            if (art.Width <= 1 || art.Height <= 1)
            {
                return null;
            }

            var targetWidth = terrain.TileWidth * terrain.RenderScale;
            var targetHeight = terrain.TileHeight * terrain.RenderScale;

            var scale = MathF.Min((float)targetWidth / art.Width, (float)targetHeight / art.Height);
            var drawWidth = Math.Max(1, (int)MathF.Round(art.Width * scale));
            var drawHeight = Math.Max(1, (int)MathF.Round(art.Height * scale));
            art.Resize(drawWidth, drawHeight);

            var canvas = Image.GenColor(targetWidth, targetHeight, Color.Blank);
            canvas.Draw(art,
                new Rectangle(0, 0, drawWidth, drawHeight),
                new Rectangle((targetWidth - drawWidth) / 2f, targetHeight - drawHeight, drawWidth, drawHeight),
                Color.White);
            art.Unload();
            return canvas;
        }

        /// <summary>
        /// Terrain names for the bundled special-resource paintings, indexed by
        /// <see cref="Model.Core.Mapping.TerrainType"/>. Null where the terrain
        /// carries no painted special (forest is drawn as grassland plus a tree
        /// overlay and has no cutout of its own).
        /// </summary>
        private static readonly string?[] FossSpecialTerrainNames =
        [
            // Forest has no cutout of its own; its Civ II specials are pheasant
            // and silk, so it borrows the grassland paintings (pheasant, then
            // sheep as the nearest available stand-in for silk).
            "desert", "plains", "grassland", "grassland", "hills",
            "mountains", "tundra", "glacier", "swamp", "jungle", "ocean"
        ];

        /// <summary>
        /// Swaps the special-resource cutouts (fish, whales, bison, furs, oasis,
        /// …) for the bundled high-resolution paintings, composed once at the
        /// working tile size. The classic path bakes each into a 64x32 sheet
        /// cell, so the tile compositor was upscaling a thumbnail.
        /// </summary>
        private static void ApplyFossSpecialArt(TerrainSet terrain)
        {
            if (terrain.Specials.Length < 2)
            {
                return;
            }

            for (var type = 0; type < FossSpecialTerrainNames.Length; type++)
            {
                var name = FossSpecialTerrainNames[type];
                if (name == null)
                {
                    continue;
                }

                for (var slot = 0; slot < 2; slot++)
                {
                    if (type >= terrain.Specials[slot].Length)
                    {
                        continue;
                    }

                    var path = FindFossSpecialPath($"{name}_{slot + 1}.png");
                    if (path == null)
                    {
                        continue;
                    }

                    var composed = ComposeSpecialTile(terrain, path, 0.56f, 0.72f);
                    if (composed == null)
                    {
                        continue;
                    }

                    terrain.Specials[slot][type] = new MemoryStorage(composed.Value,
                        $"FossSpecial-{name}-{slot}-{terrain.RenderScale}");
                }
            }
        }

        /// <summary>
        /// Composes the grassland-shield marker from its photo. The art is a
        /// round turf-set shield centred in a square grass frame, so it is
        /// cropped to a soft-edged disc (the grass at the rim fades into the
        /// map's own grass) and eased a little toward neutral.
        /// </summary>
        /// <summary>
        /// Width of the grassland shield marker as a fraction of the tile. It is a
        /// marker saying the square yields a shield, not a feature of the terrain,
        /// so it should read at a glance without competing with what is drawn on the
        /// tile. It was at 0.44, which covered most of the square, then 0.22, which
        /// still read as an object lying on the ground -- a stone medallion the size
        /// of a manhole cover in the middle of a field. At an eighth of the square
        /// it is a token again. The height limit is twice this, because the tile
        /// is 2:1.
        /// </summary>
        private const float GrassShieldTileFraction = 0.125f;

        private static Image? ComposeShieldTile(TerrainSet terrain, string path)
        {
            var loaded = Images.LoadImageFromFile(path).Image;
            if (loaded.Width <= 1 || loaded.Height <= 1)
            {
                return null;
            }

            // Photo has no alpha channel; re-draw onto a blank RGBA canvas.
            var art = Image.GenColor(loaded.Width, loaded.Height, Color.Blank);
            art.Draw(loaded,
                new Rectangle(0, 0, loaded.Width, loaded.Height),
                new Rectangle(0, 0, loaded.Width, loaded.Height),
                Color.White);
            loaded.Unload();

            var cx = art.Width / 2.0;
            var cy = art.Height / 2.0;
            var radius = Math.Min(cx, cy) * 0.94;
            var feather = Math.Min(cx, cy) * 0.12;

            for (var y = 0; y < art.Height; y++)
            {
                for (var x = 0; x < art.Width; x++)
                {
                    var dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    var edge = 1.0 - Math.Clamp((dist - (radius - feather)) / feather, 0.0, 1.0);
                    if (edge <= 0.0)
                    {
                        art.DrawPixel(x, y, new Color((byte)0, (byte)0, (byte)0, (byte)0));
                        continue;
                    }

                    var c = art.GetColor(x, y);

                    // A light nudge toward neutral so the marker sits back.
                    const double desat = 0.25;
                    var lum = 0.299 * c.R + 0.587 * c.G + 0.114 * c.B;
                    byte Mix(byte ch) => (byte)Math.Clamp((int)Math.Round(ch + (lum - ch) * desat), 0, 255);

                    var a = (byte)Math.Clamp((int)Math.Round(c.A * edge), 0, 255);
                    art.DrawPixel(x, y, new Color(Mix(c.R), Mix(c.G), Mix(c.B), a));
                }
            }

            var targetWidth = terrain.TileWidth * terrain.RenderScale;
            var targetHeight = terrain.TileHeight * terrain.RenderScale;

            var scale = MathF.Min(targetWidth * GrassShieldTileFraction / art.Width,
                                  targetHeight * GrassShieldTileFraction * 2f / art.Height);
            var drawWidth = Math.Max(1, (int)MathF.Round(art.Width * scale));
            var drawHeight = Math.Max(1, (int)MathF.Round(art.Height * scale));
            art.Resize(drawWidth, drawHeight);

            var canvas = Image.GenColor(targetWidth, targetHeight, Color.Blank);
            var offsetX = (targetWidth - drawWidth) / 2f;
            var offsetY = (targetHeight - drawHeight) / 2f;
            canvas.Draw(art,
                new Rectangle(0, 0, drawWidth, drawHeight),
                new Rectangle(offsetX, offsetY, drawWidth, drawHeight),
                Color.White);
            art.Unload();
            return canvas;
        }

        /// <summary>
        /// Filenames for the 16 marching-squares coastline diamonds, indexed by
        /// vertex land mask (N=8, E=4, S=2, W=1).
        /// </summary>
        private static readonly string[] CoastMarchShapes =
        [
            "coast_00_ocean.png", "coast_01_corner_land_W.png", "coast_02_corner_land_S.png",
            "coast_03_edge_land_SW.png", "coast_04_corner_land_E.png", "coast_05_diagonal_E_W.png",
            "coast_06_edge_land_SE.png", "coast_07_cove_N.png", "coast_08_corner_land_N.png",
            "coast_09_edge_land_NW.png", "coast_10_diagonal_N_S.png", "coast_11_cove_E.png",
            "coast_12_edge_land_NE.png", "coast_13_cove_S.png", "coast_14_cove_W.png",
            "coast_15_enclosed.png",
        ];

        /// <summary>
        /// Loads the coastline diamonds, each scaled to the working tile size.
        /// The art is already a 2:1 diamond with a hair of overlap dilation
        /// baked in, so it is not re-cut. Returns an empty array if any tile is
        /// missing, so the caller can fall back to the procedural shoreline.
        /// </summary>
        private static IImageSource[] LoadCoastMarch(TerrainSet terrain)
        {
            var tiles = new IImageSource[CoastMarchShapes.Length];
            var width = terrain.TileWidth * terrain.RenderScale;
            var height = terrain.TileHeight * terrain.RenderScale;

            for (var i = 0; i < CoastMarchShapes.Length; i++)
            {
                var path = FindCoastPath(CoastMarchShapes[i]);
                if (path == null)
                {
                    return [];
                }

                var img = Images.LoadImageFromFile(path).Image;
                if (img.Width <= 1 || img.Height <= 1)
                {
                    return [];
                }

                img.Resize(width, height);
                tiles[i] = new MemoryStorage(img, $"CoastMarch-{i}-{terrain.RenderScale}");
            }

            return tiles;
        }

        private static string? FindCoastPath(string fileName)
        {
            var roots = Settings.SearchPaths
                .Concat([
                    Environment.CurrentDirectory,
                    AppContext.BaseDirectory,
                    Path.Combine(Environment.CurrentDirectory, "RaylibUI")
                ])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(root, "Terrain", "Coast"),
                             Path.Combine(root, "FOSSart", "Terrain", "Coast"),
                             Path.Combine(root, "RaylibUI", "FOSSart", "Terrain", "Coast")
                         })
                {
                    var path = Path.Combine(candidate, fileName);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? FindFossArtPath(string fileName)
        {
            var roots = Settings.SearchPaths
                .Concat([
                    Environment.CurrentDirectory,
                    AppContext.BaseDirectory,
                    Path.Combine(Environment.CurrentDirectory, "RaylibUI")
                ])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(root, fileName),
                             Path.Combine(root, "FOSSart", fileName),
                             Path.Combine(root, "RaylibUI", "FOSSart", fileName)
                         })
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        private static string? FindFossSpecialPath(string fileName)
        {
            var roots = Settings.SearchPaths
                .Concat([
                    Environment.CurrentDirectory,
                    AppContext.BaseDirectory,
                    Path.Combine(Environment.CurrentDirectory, "RaylibUI")
                ])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(root, "Terrain", "Specials"),
                             Path.Combine(root, "FOSSart", "Terrain", "Specials"),
                             Path.Combine(root, "RaylibUI", "FOSSart", "Terrain", "Specials")
                         })
                {
                    var path = Path.Combine(candidate, fileName);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Fits a square painting into a tile-sized transparent canvas, centred
        /// and nudged up a little so it reads as an object standing on the
        /// ground. <paramref name="widthFrac"/> and <paramref name="heightFrac"/>
        /// bound its footprint as fractions of the tile.
        /// </summary>
        /// <summary>
        /// How far above centre a special-resource cutout sits, as a fraction of
        /// the tile's height.
        /// </summary>
        private const float SpecialLift = 0.20f;

        private static Image? ComposeSpecialTile(TerrainSet terrain, string path, float widthFrac, float heightFrac)
        {
            var art = Images.LoadImageFromFile(path).Image;
            if (art.Width <= 1 || art.Height <= 1)
            {
                return null;
            }

            var targetWidth = terrain.TileWidth * terrain.RenderScale;
            var targetHeight = terrain.TileHeight * terrain.RenderScale;

            var scale = MathF.Min(targetWidth * widthFrac / art.Width, targetHeight * heightFrac / art.Height);
            var drawWidth = Math.Max(1, (int)MathF.Round(art.Width * scale));
            var drawHeight = Math.Max(1, (int)MathF.Round(art.Height * scale));
            art.Resize(drawWidth, drawHeight);

            var canvas = Image.GenColor(targetWidth, targetHeight, Color.Blank);
            var offsetX = (targetWidth - drawWidth) / 2f;
            // Sit the cutout high in the diamond. A tile's lower half is the part
            // nearest the viewer, which for an ocean square is where its shore is
            // drawn; a whale centred in the tile came up out of the sand. Lifting
            // it puts it over open water. The tile image cannot overhang its
            // neighbours, so the art is composed a little smaller to make room
            // rather than being clipped at the top.
            var offsetY = (targetHeight - drawHeight) / 2f - targetHeight * SpecialLift;
            canvas.Draw(art,
                new Rectangle(0, 0, drawWidth, drawHeight),
                new Rectangle(offsetX, offsetY, drawWidth, drawHeight),
                Color.White);
            art.Unload();
            return canvas;
        }

        private static string? FindFossOverlayPath(string directory, string fileName)
        {
            var roots = Settings.SearchPaths
                .Concat([
                    Environment.CurrentDirectory,
                    AppContext.BaseDirectory,
                    Path.Combine(Environment.CurrentDirectory, "RaylibUI")
                ])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var candidate in new[]
                         {
                             Path.Combine(root, "Terrain", "Overlays", directory),
                             Path.Combine(root, "FOSSart", "Terrain", "Overlays", directory),
                             Path.Combine(root, "RaylibUI", "FOSSart", "Terrain", "Overlays", directory)
                         })
                {
                    var path = Path.Combine(candidate, fileName);
                    if (File.Exists(path))
                    {
                        return path;
                    }
                }
            }

            return null;
        }

        private static string? FindFossTerrainPath(string terrainName)
        {
            // PNG first: the newer terrain diamonds carry their own alpha, so they
            // do not depend on the compatibility sheet's mask to cut the corners.
            var fileNames = new[] { $"{terrainName}.png", $"{terrainName}.jpg" };
            var roots = Settings.SearchPaths
                .Concat([
                    Environment.CurrentDirectory,
                    AppContext.BaseDirectory,
                    Path.Combine(Environment.CurrentDirectory, "RaylibUI")
                ])
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                foreach (var directory in new[]
                         {
                             Path.Combine(root, "Terrain"),
                             Path.Combine(root, "FOSSart", "Terrain"),
                             Path.Combine(root, "RaylibUI", "FOSSart", "Terrain")
                         })
                {
                    foreach (var fileName in fileNames)
                    {
                        var path = Path.Combine(directory, fileName);
                        if (File.Exists(path))
                        {
                            return path;
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Cuts the diamond out of a rectangular replacement texture with a hard
        /// edge, kept a pixel or two oversized so neighbouring tiles overlap
        /// along the join instead of leaving the view canvas showing through.
        /// A soft edge here bled the darker photo pixels just outside the
        /// diamond into a visible outline once the tile was drawn.
        /// </summary>
        private static void ApplyDiamondAlpha(Image image)
        {
            var width = image.Width;
            var height = image.Height;

            // ~1.5px of outward dilation in the taxicab distance field.
            var dilate = 3.0 / height;

            for (var y = 0; y < height; y++)
            {
                var ny = (y + 0.5) / height * 2.0 - 1.0;
                for (var x = 0; x < width; x++)
                {
                    var nx = (x + 0.5) / width * 2.0 - 1.0;

                    // Taxicab distance from tile centre: 1.0 on the diamond edge.
                    var d = Math.Abs(nx) + Math.Abs(ny);
                    if (d <= 1.0 + dilate)
                    {
                        continue;
                    }

                    var src = image.GetColor(x, y);
                    if (src.A != 0)
                    {
                        image.DrawPixel(x, y, new Color(src.R, src.G, src.B, (byte)0));
                    }
                }
            }
        }

        /// <summary>Interchangeable shoreline variants generated per tile edge.</summary>
        private const int CoastVariants = 7;

        private static double CoastHash(int n)
        {
            unchecked
            {
                n = (n << 13) ^ n;
                var m = (n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff;
                return m / 2147483647.0;
            }
        }

        private static double CoastNoise(double x)
        {
            var i = (int)Math.Floor(x);
            var f = x - i;
            var u = f * f * (3.0 - 2.0 * f);
            return CoastHash(i) * (1.0 - u) + CoastHash(i + 1) * u;
        }

        private static double CoastFbm(double x) =>
            0.6 * CoastNoise(x) + 0.3 * CoastNoise(x * 2.03 + 11.1) + 0.1 * CoastNoise(x * 4.07 + 27.4);

        private static double CoastSmooth(double a, double b, double x)
        {
            if (Math.Abs(a - b) < 1e-9)
            {
                return x < a ? 0.0 : 1.0;
            }

            var t = Math.Clamp((x - a) / (b - a), 0.0, 1.0);
            return t * t * (3.0 - 2.0 * t);
        }

        private static double CoastLerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>
        /// One painted shoreline: a full tile-sized transparent image carrying a
        /// ragged surf line, a deep-to-shallow water wash, a couple of breaking
        /// wave crests and a scatter of foam-collared rocks, all along a single
        /// diagonal edge (<paramref name="quadrant"/> 0 NE, 1 SE, 2 SW, 3 NW).
        /// Everything is procedural and seeded from
        /// <paramref name="quadrant"/> and <paramref name="variant"/> so no two
        /// variants of an edge match.
        /// </summary>
        private static Image BuildShallowEdge(int width, int height, int quadrant, int variant)
        {
            var eastHalf = quadrant is 0 or 1;
            var southHalf = quadrant is 1 or 2;
            var seed = quadrant * 1013 + variant * 5779;
            var phase = seed * 0.137 + variant * 12.91;
            var swayPhase = variant * 4.19 + quadrant * 1.6;

            // Reach of the whole effect and of its zones, in taxicab-distance units.
            const double band = 0.62;
            const double foamWidth = 0.16;

            // Open-water blue the shallows must dissolve into.
            const double deepR = 40, deepG = 96, deepB = 150;

            // Roughly half the shoreline variants are rocky; the rest are clean
            // sand and surf so the coast never reads as one dark rim.
            var rocky = CoastHash(seed * 3 + 91) > 0.46;
            var rockCount = rocky ? 1 + (int)(CoastHash(seed * 61 + 7) * 2.0) : 0;
            var rocks = new (double Along, double Size, double Rough, double Reach)[rockCount];
            for (var r = 0; r < rockCount; r++)
            {
                var big = CoastHash(seed * 17 + r * 131 + 5) > 0.6;
                rocks[r] = (
                    Along: -0.46 + 0.92 * ((r + 0.2 + 0.6 * CoastHash(seed * 29 + r * 71 + 3)) / Math.Max(1, rockCount)),
                    Size: (big ? 0.30 : 0.16) + 0.06 * CoastHash(seed * 53 + r * 197),
                    Rough: CoastHash(seed * 7 + r * 313 + 1) * 25.0,
                    Reach: 0.07 + 0.08 * CoastHash(seed * 97 + r * 43));
            }

            var img = Image.GenColor(width, height, Color.Blank);
            for (var y = 0; y < height; y++)
            {
                var ny = (y + 0.5) / height * 2.0 - 1.0;
                for (var x = 0; x < width; x++)
                {
                    var nx = (x + 0.5) / width * 2.0 - 1.0;

                    var inQuadrant = (eastHalf ? nx >= 0.0 : nx <= 0.0)
                                     && (southHalf ? ny >= 0.0 : ny <= 0.0);
                    if (!inQuadrant)
                    {
                        continue;
                    }

                    var d = Math.Abs(nx) + Math.Abs(ny);
                    if (d >= 1.02)
                    {
                        continue;
                    }

                    var inward = 1.0 - d; // 0 at the waterline, grows toward the tile centre
                    var along = quadrant switch
                    {
                        0 => nx + ny,
                        1 => nx - ny,
                        2 => -(nx + ny),
                        _ => -(nx - ny),
                    };

                    // Ragged waterline: a big cove/headland sway plus finer chop
                    // and noise. Only ever bites into the water so land is never
                    // overdrawn. Eased down near the vertices so one tile's
                    // shoreline meets the next without a hard step, but not
                    // pinned flat, which reads as a regular scallop.
                    var anchor = 0.5 + 0.5 * (1.0 - Math.Abs(along));
                    var wob = (0.024 * Math.Sin(along * 1.7 + swayPhase)
                               + 0.016 * Math.Sin(along * 5.9 + phase * 1.9)
                               + 0.034 * (CoastFbm(along * 3.1 + phase) - 0.5)
                               + 0.011 * (CoastNoise(along * 26.0 + phase) - 0.5)) * anchor;
                    var local = inward - wob;

                    double rr = 0, gg = 0, bb = 0, aa = 0;

                    // Bright turquoise shallows fading through teal into open
                    // water: strong near the sand, gone by the band edge.
                    if (local >= 0.0 && local < band)
                    {
                        var k = local / band;
                        var t1 = Math.Clamp(k / 0.28, 0.0, 1.0);
                        var t2 = Math.Clamp((k - 0.28) / 0.72, 0.0, 1.0);
                        var midR = CoastLerp(120, 32, t1);
                        var midG = CoastLerp(232, 150, t1);
                        var midB = CoastLerp(222, 170, t1);
                        rr = CoastLerp(midR, deepR, t2);
                        gg = CoastLerp(midG, deepG, t2);
                        bb = CoastLerp(midB, deepB, t2);
                        aa = Math.Pow(1.0 - k, 1.25) * 0.9;
                    }

                    // Damp, dark sand right at the waterline.
                    if (local > -0.02 && local < 0.05)
                    {
                        var wet = (1.0 - Math.Clamp(local / 0.05, 0.0, 1.0)) * 0.55;
                        rr = CoastLerp(rr, 74, wet);
                        gg = CoastLerp(gg, 96, wet);
                        bb = CoastLerp(bb, 96, wet);
                        aa = Math.Max(aa, wet);
                    }

                    // Heavy broken surf washing up the shore.
                    if (local > -0.03 && local < foamWidth)
                    {
                        var f = 1.0 - Math.Clamp(local / foamWidth, 0.0, 1.0);
                        var froth = CoastNoise(along * 30.0 + phase * 3.0)
                                    * CoastNoise(along * 9.0 - phase + local * 50.0);
                        var lace = CoastNoise(along * 70.0 + phase) > 0.32 ? 1.0 : 0.15;
                        var foam = Math.Pow(f, 1.25) * (0.45 + 0.55 * froth) * lace;
                        var fa = Math.Min(1.0, foam * 1.35);
                        if (fa > aa)
                        {
                            aa = fa;
                        }

                        rr = CoastLerp(rr, 248, foam);
                        gg = CoastLerp(gg, 252, foam);
                        bb = CoastLerp(bb, 253, foam);
                    }

                    // Two or three breaking crests running parallel to the shore.
                    for (var w = 1; w <= 3; w++)
                    {
                        var crest = 0.06 + w * (band / 4.2)
                                    + 0.026 * Math.Sin(along * 6.0 + w * 2.0 + swayPhase)
                                    + 0.024 * (CoastFbm(along * 4.0 + w * 13 + phase) - 0.5);
                        var dist = Math.Abs(local - crest);
                        if (dist >= 0.028)
                        {
                            continue;
                        }

                        var lip = 1.0 - dist / 0.028;
                        var brk = CoastNoise(along * 13.0 + w * 4.0 + phase);
                        var c = lip * lip * (0.1 + 0.9 * brk) * 0.85;
                        if (c > aa)
                        {
                            aa = c;
                        }

                        rr = CoastLerp(rr, 242, c);
                        gg = CoastLerp(gg, 249, c);
                        bb = CoastLerp(bb, 252, c);
                    }

                    // Rocks straddling the waterline with a foam collar where the
                    // water breaks against them and a lit, spray-flecked crown.
                    foreach (var rock in rocks)
                    {
                        var da = along - rock.Along;
                        var db = local - 0.07;
                        var ang = Math.Atan2(db, da);
                        var radius = rock.Size * (0.60 + 0.40 * CoastNoise(ang * 3.0 + rock.Rough));
                        var rd = Math.Sqrt(da * da * 1.0 + db * db * 2.0);
                        var collar = rock.Reach + 0.06;   // wide burst of spray where the swell breaks

                        if (rd < radius)
                        {
                            // Lit from the north-west, spray-flecked, warm grey.
                            var crown = Math.Clamp((radius - rd) / radius, 0.0, 1.0);
                            var facing = 0.5 - 0.5 * Math.Cos(ang - 2.3);
                            var lit = 0.24 + 0.40 * crown + 0.34 * facing
                                      + 0.20 * CoastNoise(da * 40.0 + db * 46.0 + rock.Rough);
                            lit = Math.Clamp(lit, 0.0, 1.0);
                            rr = CoastLerp(52, 178, lit);
                            gg = CoastLerp(48, 168, lit);
                            bb = CoastLerp(44, 156, lit);
                            aa = 1.0;
                        }
                        else if (rd < radius + collar)
                        {
                            var ring = 1.0 - (rd - radius) / collar;
                            // heavier on the seaward side, torn up by noise
                            var seaward = 0.55 + 0.45 * Math.Cos(ang - 0.8);
                            var spray = Math.Pow(ring, 1.4)
                                        * (0.35 + 0.65 * CoastNoise(ang * 5.0 + rd * 60.0 + rock.Rough * 1.7))
                                        * seaward;
                            if (spray > aa)
                            {
                                aa = spray;
                            }

                            rr = CoastLerp(rr, 249, spray);
                            gg = CoastLerp(gg, 252, spray);
                            bb = CoastLerp(bb, 254, spray);
                        }
                    }

                    if (aa <= 0.004)
                    {
                        continue;
                    }

                    // Ease off toward the two vertices so neighbouring shore
                    // images do not clash, and make sure nothing reaches mid-tile.
                    aa *= 1.0 - CoastSmooth(0.88, 1.06, Math.Abs(along));
                    aa *= 1.0 - CoastSmooth(band * 0.86, band, local);

                    var a = (byte)Math.Clamp((int)Math.Round(aa * 255.0), 0, 255);
                    if (a == 0)
                    {
                        continue;
                    }

                    img.DrawPixel(x, y, new Color(
                        (byte)Math.Clamp((int)Math.Round(rr), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(gg), 0, 255),
                        (byte)Math.Clamp((int)Math.Round(bb), 0, 255),
                        a));
                }
            }

            return img;
        }

        private static DitherMap BuildDitherMaps(Image mask, IImageSource[] baseTiles, int offsetX, int offsetY,
            IImageSource terrainBlank, bool feather)
        {
            var totalTiles = baseTiles.Length + 1;
            var ditherMaps = new Image[totalTiles];
            for (var i = 0; i < baseTiles.Length; i++)
            {
                var baseImage = Images.ExtractBitmap(baseTiles[i]);
                var scaleX = baseImage.Width / 64f;
                var scaleY = baseImage.Height / 32f;
                var scaledSampleRect = new Rectangle(offsetX * scaleX, offsetY * scaleY,
                    32 * scaleX, 16 * scaleY);
                ditherMaps[i] = Image.FromImage(baseImage, scaledSampleRect);

                if (feather)
                {
                    // Multiply the quadrant's own alpha by a soft ramp that is
                    // strongest along the shared diamond edge and gone by
                    // roughly half way to the centre, so the neighbouring
                    // terrain blends across the join instead of the classic
                    // hard checkerboard stipple. Multiplying (rather than
                    // AlphaMask, which replaces) keeps the diamond cut, so the
                    // darker pixels just outside the neighbour's diamond are
                    // not resurrected into an outline.
                    const double band = 0.55;
                    const double maxStrength = 0.72;
                    var mw = ditherMaps[i].Width;
                    var mh = ditherMaps[i].Height;
                    for (var py = 0; py < mh; py++)
                    {
                        var tileY = offsetY + (py + 0.5) / mh * 16.0;
                        var ny = tileY / 16.0 - 1.0;
                        for (var px = 0; px < mw; px++)
                        {
                            var tileX = offsetX + (px + 0.5) / mw * 32.0;
                            var nx = tileX / 32.0 - 1.0;

                            var d = Math.Abs(nx) + Math.Abs(ny);
                            var ramp = 0.0;
                            if (d < 1.0)
                            {
                                var t = Math.Clamp((d - (1.0 - band)) / band, 0.0, 1.0);
                                ramp = t * t * maxStrength;
                            }

                            var src = ditherMaps[i].GetColor(px, py);
                            var a = (byte)Math.Clamp((int)Math.Round(src.A * ramp), 0, 255);
                            ditherMaps[i].DrawPixel(px, py, new Color(src.R, src.G, src.B, a));
                        }
                    }
                }
                else
                {
                    var scaledMask = mask.Copy();
                    if (scaledMask.Width != ditherMaps[i].Width || scaledMask.Height != ditherMaps[i].Height)
                    {
                        scaledMask.ResizeNN(ditherMaps[i].Width, ditherMaps[i].Height);
                    }
                    ditherMaps[i].AlphaMask(scaledMask);
                    scaledMask.Unload();
                }
            }

            // Fog-of-war edge. On the classic sheet this is a crop of the "blank"
            // tile behind the checkerboard mask; against the high-resolution art
            // that blank tile shows through as blue/black checker pixels, so use
            // a plain dark wash there instead, shaped by the same soft ramp as
            // the terrain dither.
            if (feather)
            {
                var fw = ditherMaps.Length > 1 ? ditherMaps[0].Width : mask.Width;
                var fh = ditherMaps.Length > 1 ? ditherMaps[0].Height : mask.Height;
                var fog = Image.GenColor(fw, fh, new Color((byte)8, (byte)12, (byte)26, (byte)255));
                const double band = 0.6;
                const double maxStrength = 0.78;
                for (var py = 0; py < fh; py++)
                {
                    var tileY = offsetY + (py + 0.5) / fh * 16.0;
                    var ny = tileY / 16.0 - 1.0;
                    for (var px = 0; px < fw; px++)
                    {
                        var tileX = offsetX + (px + 0.5) / fw * 32.0;
                        var nx = tileX / 32.0 - 1.0;
                        var dd = Math.Abs(nx) + Math.Abs(ny);
                        var strength = 0.0;
                        if (dd < 1.0)
                        {
                            var t = Math.Clamp((dd - (1.0 - band)) / band, 0.0, 1.0);
                            strength = t * t * maxStrength;
                        }

                        fog.DrawPixel(px, py, new Color((byte)8, (byte)12, (byte)26,
                            (byte)Math.Clamp((int)Math.Round(strength * 255.0), 0, 255)));
                    }
                }

                ditherMaps[^1] = fog;
            }
            else
            {
                var sampleRect = new Rectangle(offsetX, offsetY, 32, 16);
                ditherMaps[^1] = Image.FromImage(Images.ExtractBitmap(terrainBlank), sampleRect);
                ditherMaps[^1].AlphaMask(mask);
            }

            return new DitherMap { X = offsetX, Y = offsetY, Images = ditherMaps };
        }

        /// <summary>
        /// For TERRAIN3, 4, 5, etc.
        /// </summary>
        private static IImageSource MapIndexChange(BitmapStorage storage, int mapIndex, IUserInterface active)
        {
            var file = storage.Filename;

            if (mapIndex != 0)
            {
                int.TryParse(file[^1].ToString(), out int currentIndex);
                int newIndex = mapIndex * 2 + currentIndex;
                file = $"{file.Remove(file.Length - 1, 1)}{newIndex}";
            }

            var img = new BitmapStorage(file, storage.Location, storage.TransparencyPixel, storage.SearchFlagLoc);
            Images.ExtractBitmap(img, active);
            return img;
        }
    }
}
