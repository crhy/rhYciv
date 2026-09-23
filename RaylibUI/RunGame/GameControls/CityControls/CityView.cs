using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using Model;
using Model.Controls;
using Model.Images;
using Raylib_CSharp.Collision;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Transformations;
using RaylibUtils;
using System.Numerics;
using Model.Core.Cities;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class CityView : FullscreenView
{
    private readonly IUserInterface _active;
    private readonly int _backId, _picWidth, _picHeight, _offsetX, _offsetY;
    private readonly string _backBase;
    private readonly City _city;
    private readonly List<(IImageSource Source, Vector2 Pos)> _drawTiles = [];

    public CityView(GameScreen gameScreen, City city) : base(gameScreen)
    {
        _city = city;
        _active = gameScreen.MainWindow.ActiveInterface;
        var tiles = _active.GetCityViewTiles();
        var altTiles = _active.GetCityViewAltTiles();

        // The standalone ruleset ships no scenery layer (the legacy table reads
        // cv.dll, which this build never loads), so the panorama is just its
        // background. Guard every index so an empty table shows that background
        // instead of taking the game down on the View button (#145).
        if (tiles.Count > 0)
        {
            for (var id = 0; id < Math.Min(56, tiles.Count); id++)
            {
                if (_city.IsNextToOcean() && id > 49) continue;
                if (_city.IsNextToRiver() && id > 49 && id < 54) continue;

                if (_city.ImprovementExists(tiles[id].RulesId))
                {
                    _drawTiles.Add(new(tiles[id].Source, tiles[id].Position));
                }
                else if (tiles[id].AlternativeTileId >= 0 && tiles[id].AlternativeTileId < altTiles.Count)
                {
                    _drawTiles.Add(new(altTiles[tiles[id].AlternativeTileId], tiles[id].Position));
                }
            }

            // City walls & Offshore platrofrm
            foreach (var id in new int[] { 8, 31 })
            {
                if (_city.ImprovementExists(id))
                {
                    var tile = tiles.FirstOrDefault(t => t.RulesId == id);
                    if (tile is null || tile.Id < 0 || tile.Id >= tiles.Count) continue;
                    _drawTiles.Add(new(tiles[tile.Id].Source, tiles[tile.Id].Position));
                }
            }

            // Harbor/port fac.
            if (_city.ImprovementExists(34))    // port fac.
            {
                var tile = tiles.FirstOrDefault(t => t.RulesId == 34);
                if (tile is not null && tile.Id >= 0 && tile.Id < tiles.Count)
                {
                    _drawTiles.Add(new(tiles[tile.Id].Source, tiles[tile.Id].Position));
                }
            }
            else if (_city.ImprovementExists(30))    // harbor
            {
                var tile = tiles.FirstOrDefault(t => t.RulesId == 30);
                if (tile is not null && tile.Id >= 0 && tile.Id < tiles.Count)
                {
                    _drawTiles.Add(new(tiles[tile.Id].Source, tiles[tile.Id].Position));
                }
            }

            // Colossus
            if (_city.ImprovementExists(41) && tiles.Count > 61)
            {
                if (_city.IsNextToRiver())
                {
                    _drawTiles.Add(new(tiles[61].Source, tiles[61].Position));
                }
                else
                {
                    _drawTiles.Add(new(tiles[60].Source, tiles[60].Position));
                }
            }

            // Lighthouse
            if (_city.ImprovementExists(42) && tiles.Count > 63)
            {
                if (_city.IsNextToRiver())
                {
                    _drawTiles.Add(new(tiles[63].Source, tiles[63].Position));
                }
                else
                {
                    _drawTiles.Add(new(tiles[62].Source, tiles[62].Position));
                }
            }

            // Statue liberty
            // TODO: find out where to draw statue of liberty in city view

            // Great wall
            var _id = 45;
            if (_city.ImprovementExists(_id))
            {
                var tile = tiles.FirstOrDefault(t => t.RulesId == _id);
                if (tile is not null && tile.Id >= 0 && tile.Id < tiles.Count)
                {
                    _drawTiles.Add(new(tiles[tile.Id].Source, tiles[tile.Id].Position));
                }
            }
        }

        // Epoch 4/5 are Industrial/Modern in the engine; map them to the
        // last panorama (same as Civ II) instead of throwing (#145).
        _backId = city.Owner.Epoch switch
        {
            0 or 1 => 0,
            2 => 1,
            3 or 4 or 5 => 2,
            _ => 2
        };

        if (city.Improvements.Any(i => i.Type == (int)ImprovementType.Superhighways))
        {
            _backId = 3;
        }

        if (city.IsNextToOcean())
        {
            _backBase = "cvOcean";
        }
        else if (city.IsNextToRiver())
        {
            _backBase = "cvRiver";
        }
        else
        {
            _backBase = "cvContinent";
        }

        // Standalone/ToT rulesets may not ship the cv* panoramas at all.
        // Guard the lookup so the View button shows at least the black
        // background instead of throwing KeyNotFound (#145).
        if (_active.PicSources.TryGetValue(_backBase, out var backSources) && backSources.Length > 0)
        {
            var idx = Math.Clamp(_backId, 0, backSources.Length - 1);
            _picWidth = Images.GetImageWidth(backSources[idx], _active);
            _picHeight = Images.GetImageHeight(backSources[idx], _active);
        }
        else
        {
            _picWidth = gameScreen.Width;
            _picHeight = gameScreen.Height;
        }
        _offsetX = (gameScreen.Width - _picWidth) / 2;
        _offsetY = (gameScreen.Height - _picHeight) / 2;
    }

    public override void OnKeyPress(KeyboardKey key)
    {
        base.Close();
    }

    public override void MouseOutsideControls(Vector2 mousePos)
    {
        if (Input.IsMouseButtonReleased(MouseButton.Left) &&
            ShapeHelper.CheckCollisionPointRec(mousePos, new(_offsetX, _offsetY, _picWidth, _picHeight)))
        {
            base.Close();
        }
    }

    public override void Draw(bool pulse)
    {
        Graphics.DrawRectangle(0, 0, Width, Height, Color.Black);

        if (_active.PicSources.TryGetValue(_backBase, out var backSources) && backSources.Length > 0)
        {
            var idx = Math.Clamp(_backId, 0, backSources.Length - 1);
            Graphics.DrawTexture(TextureCache.GetImage(backSources[idx]),
                _offsetX, _offsetY, Color.White);
        }

        foreach (var (Source, Pos) in _drawTiles)
        {
            Graphics.DrawTextureEx(TextureCache.GetImage(Source), 
                Pos + new Vector2(_offsetX, _offsetY), 0f, 1f, Color.White);
        }

        base.Draw(pulse);
    }
}
