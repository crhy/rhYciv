using System;
using System.Numerics;
using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Terrains;
using Model;
using Model.Core.GameRules;
using Model.Core.Mapping;
using Model.ImageSets;
using Raylib_CSharp.Transformations;
using RaylibUI;
using RaylibUtils;

namespace RhyCiv.UI.Classic.ImageLoader;

public static class CityLoader
{
    public static void LoadCities(Ruleset ruleset, CityImageSet cities, ClassicInterface active)
    {
        // Cities images
        var citySources = active.PicSources["city"];
        for (int row = 0; row < 6; row++)
        {
            if (8 * row + 7 >= citySources.Length)
            {
                break;
            }
            var sets = new CityImage[8];
            for (int col = 0; col < 8; col++)
            {
                var props = Images.ExtractBitmapData(citySources[8 * row + col], active); // put into cache
                cities.CityRectangle = new Rectangle(0, 0, props.Image.Width, props.Image.Height);

                // Keep the classic sheet sprite as the UI/dialog source and the
                // logical footprint that flag and size markers are placed against.
                // The 300x300 FOSS art is attached separately for map rendering.
                sets[col] = new CityImage()
                {
                    Image = citySources[8 * row + col],
                    MapImage = active.GetFossArtCityImage(row, col),
                    LogicalSize = new Vector2(props.Image.Width, props.Image.Height),
                    FlagLoc = props.Flag1,
                    SizeLoc = props.Flag2,
                };
            }

            cities.Sets.Add(sets);
        }

        // Colours
        active.LoadPlayerColours();

        if (active.TileSets.Count == 0)
        {
            active.TileSets.Add(new TerrainSet(64, 32));
        }

        active.UnitImages.Fortify = active.PicSources["fortify"][0];

        foreach (var terrain in active.TileSets)
        {
            terrain.ImprovementsMap[ImprovementTypes.Fortress] = new ImprovementGraphic
            { Levels = new[,] { { active.PicSources["fortress"][0] } } };

            // airbase
            terrain.ImprovementsMap[ImprovementTypes.Airbase] = new ImprovementGraphic
            {
                Levels = new[,] { { active.PicSources["airbase,empty"][0] } },
                UnitLevels = new[,] { { active.PicSources["airbase,full"][0] } }
            };
        }
    }
}