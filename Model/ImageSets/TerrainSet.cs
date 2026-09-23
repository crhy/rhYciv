using Model.Core;
using Model.Core.Mapping;
using Model.Images;
using Raylib_CSharp.Images;
using RaylibUI;

namespace Model.ImageSets
{
    public class TerrainSet
    {
        public TerrainSet(int tileWidth, int tileHeight, int renderScale = 1)
        {
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            HalfWidth = tileWidth / 2;
            HalfHeight = tileHeight / 2;
            DiagonalCut = HalfHeight * HalfWidth;
            RenderScale = renderScale < 1 ? 1 : renderScale;
        }

        /// <summary>
        /// How many source pixels this set composes per logical Civ2 tile pixel.
        /// Tile graphics are built at <see cref="TileWidth"/> x <see cref="RenderScale"/>
        /// so that a zoomed-in map is composed at the resolution it will be drawn
        /// at, rather than being upscaled from a fixed 64x32 grid.
        /// </summary>
        public int RenderScale { get; }


        public int DiagonalCut { get; }

        public int HalfHeight { get; }

        public int HalfWidth { get; }

        public IImageSource[] BaseTiles { get; set; } = [];

        /// <summary>
        /// Every painting of each terrain, <c>[variant][terrain]</c>; variant 0 is
        /// <see cref="BaseTiles"/>. The map picks one per tile from its position
        /// so a field of one terrain is not a grid of identical tiles. Empty when
        /// only the classic sheet is loaded.
        /// </summary>
        public IImageSource[][] BaseTileVariants { get; set; } = [];

        /// <summary>
        /// <see cref="DitherMaps"/> built from each of <see cref="BaseTileVariants"/>,
        /// <c>[variant][edge]</c>.
        /// </summary>
        public DitherMap[][] DitherMapVariants { get; set; } = [];

        /// <summary>
        /// Which of <see cref="BaseTileVariants"/> this tile draws: a hash of its
        /// position, so neighbours rarely match and nothing lines up in rows.
        /// </summary>
        public int VariantOf(Tile tile)
        {
            if (BaseTileVariants.Length <= 1)
            {
                return 0;
            }

            var hash = (uint)(tile.X * 73856093 ^ tile.Y * 19349663 ^ tile.Z * 83492791);
            hash ^= hash >> 13;
            hash *= 0x5bd1e995;
            hash ^= hash >> 15;
            return (int)(hash % (uint)BaseTileVariants.Length);
        }

        /// <summary>
        /// True once <see cref="BaseTiles"/> hold the bundled high-resolution
        /// photographic diamonds rather than the classic 8-bit sheet cells. The
        /// tile compositor softens terrain dithering and skips the legacy coast
        /// stipple on open water when this is set, both of which only read well
        /// against the low-contrast classic art.
        /// </summary>
        public bool HighResBaseTiles { get; set; }
        public IImageSource[][] Specials { get; set; } = [];
        public IImageSource Blank { get; set; } = null!;
        public DitherMap[] DitherMaps { get; set; } = [];
        public IImageSource[] RiverMouth { get; set; } = [];
        public IImageSource[] River { get; set; } = [];

        /// <summary>
        /// The river connection tiles again, at four gauges: a trickle, a stream, a
        /// river and an estuary, indexed <c>RiverBands[band][mask]</c>. Which band a
        /// tile draws comes from <see cref="Model.Core.Mapping.Tile.RiverFlow"/> --
        /// how far it sits from the sea along its own watercourse.
        /// </summary>
        /// <remarks>
        /// Empty when the banded art is not present, in which case
        /// <see cref="River"/> is drawn for every tile as it always was.
        /// </remarks>
        public IImageSource[][] RiverBands { get; set; } = [];
        public IImageSource[] Forest { get; set; } = [];
        public IImageSource[] Mountains { get; set; } = [];
        public IImageSource[] Hills { get; set; } = [];
        public IImageSource[,] Coast { get; set; } = new IImageSource[0, 0];

        /// <summary>
        /// The 16 marching-squares coastline diamonds, indexed by the land mask
        /// of the tile's four vertices (N=8, E=4, S=2, W=1). When present, an
        /// ocean tile is drawn as <c>CoastMarch[mask]</c> in full instead of a
        /// water base plus <see cref="ShallowEdge"/> overlays; each sprite
        /// already carries its own sand, surf and open water.
        /// </summary>
        public IImageSource[] CoastMarch { get; set; } = [];

        /// <summary>
        /// The open sea, seamless over a 4x4 block of tiles and cut into the
        /// sixteen tiles of that block, indexed <c>i * 4 + j</c> where i and j are
        /// the tile's position along the two diagonals, (X + Y) / 2 and (X - Y) / 2,
        /// each modulo 4.
        /// </summary>
        public IImageSource[] Sea { get; set; } = [];

        /// <summary>
        /// The shore drawn over a sea tile, indexed by a mask of what land it
        /// touches: bits 0-3 for land across its NE, SE, SW, NW edge, bits 4-7 for
        /// land touching only its N, E, S, W corner. Transparent where the land is:
        /// <see cref="ShoreLand"/> is the matching land mask, and the land's own
        /// terrain is drawn through it first. All 256 masks are filled; masks that
        /// draw alike share an image.
        /// </summary>
        public IImageSource[] Shore { get; set; } = [];

        /// <summary>The land part of each <see cref="Shore"/> image, white on black.</summary>
        public IImageSource[] ShoreLand { get; set; } = [];

        /// <summary>
        /// Procedurally painted shorelines, indexed [edge][variant]. Edge is the
        /// diagonal of the tile the shore runs along (0 NE, 1 SE, 2 SW, 3 NW);
        /// each edge carries several interchangeable variants so a long coast
        /// does not visibly repeat. Used in place of <see cref="Coast"/> when
        /// <see cref="HighResBaseTiles"/> is set. Every image is full tile sized
        /// and transparent except for the surf, shallows and rocks along its own
        /// edge, composed onto an ocean tile wherever that edge meets land.
        /// </summary>
        public Image[][] ShallowEdge { get; set; } = [];
        public IImageSource Pollution { get; set; } = null!;
        public IImageSource GrasslandShield { get; set; } = null!;

        public IImageSource Huts { get; set; } = null!;
        public Image[] DitherMask { get; set; } = [];
        
        public Dictionary<int, ImprovementGraphic> ImprovementsMap { get; set; } = new();

        public int TileWidth { get; }

        public int TileHeight { get; }

        public IImageSource[] ImagesFor(TerrainType terrain)
        {
            switch (terrain)
            {
                case TerrainType.Forest:
                    return Forest;
                case TerrainType.Hills:
                    return Hills;
                case TerrainType.Mountains:
                    return Mountains;
                case TerrainType.Desert:
                case TerrainType.Plains:
                case TerrainType.Grassland:
                case TerrainType.Tundra:
                case TerrainType.Glacier:
                case TerrainType.Swamp:
                case TerrainType.Jungle:
                case TerrainType.Ocean:
                default:
                    throw new ArgumentOutOfRangeException(nameof(terrain), terrain, null);
            }
        }
    }
}
