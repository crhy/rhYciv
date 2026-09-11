using Model.Images;
using System.Numerics;
using RhyCiv.Engine;
using RhyCiv.Engine.Units;
using RhyCiv.Engine.Enums;
using Raylib_CSharp.Images;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Transformations;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Rendering;
using Model;
using Model.Core;
using Model.Core.Mapping;
using Model.Core.Units;
using Model.ImageSets;
using Model.Interface;
using RaylibUI.RunGame.GameControls.Mapping;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;
using RaylibUtils;
using RaylibUI.BasicTypes.Controls;

namespace RaylibUI;

public static class ImageUtils
{
    private const int HighResolutionShieldScale = 8;
    private static readonly Dictionary<string, Texture2D> HighResolutionUnitShields = new();

    private enum ShieldLayer
    {
        Front,
        Back,
        Shadow,
    }

    private static Image _innerWallpaper;
    private static Image _outerWallpaper;
    private static Image _outerTitleTopWallpaper;

    public static void PaintPanelBorders(IUserInterface? active, ref Image image, int width, int height, Padding padding, bool statusPanel = false, bool toTStatusPanelLayout = false)
    {
        if (active == null)
        {
            image.DrawRectangle(0, 0, width, height, Color.Gray);
        }
        else
        {
            active.DrawBorderWallpaper(Wallpaper!, ref image, height, width, padding, statusPanel && !toTStatusPanelLayout);
            active.DrawBorderLines(ref image, height, width, padding, statusPanel);
        }
    }

    ///// <summary>
    ///// Draw tiles within a rectangle (chose tiles randomly)
    ///// </summary>
    ///// <param name="tiles">Wallpaper tile images</param>
    ///// <param name="dest">Destination image</param>
    ///// <param name="rect">Rectangle within the image where tiles are to be drawn</param>
    //public static void DrawTiledRectangle(Image[] tiles, ref Image dest, Rectangle rect)
    //{
    //    var rnd = new Random();
    //    var len = tiles.Length;

    //    var totalColumns = Math.Ceiling(rect.Width / tiles[0].Width);
    //    var totalRows = Math.Ceiling(rect.Height / tiles[0].Height);

    //    for (int row = 0; row < totalRows; row++)
    //    {
    //        for (int col = 0; col < totalColumns; col++)
    //        {
    //            var srcRec = new Rectangle { Height = tiles[0].Height, Width = tiles[0].Width };
    //            var destRec = new Rectangle(rect.X + col * tiles[0].Width, rect.Y + row * tiles[0].Height, tiles[0].Width, tiles[0].Height);

    //            if (col == totalColumns - 1)
    //            {
    //                srcRec.Width = rect.Width - tiles[0].Width * col;
    //                destRec.Width = srcRec.Width;
    //            }

    //            if (row == totalRows - 1)
    //            {
    //                srcRec.Height = rect.Height - tiles[0].Height * row;
    //                destRec.Height = srcRec.Height;
    //            }

    //            dest.Draw(tiles[rnd.Next(len)], srcRec, destRec, Color.White);
    //        }
    //    }
    //}


    public static void DrawTiledImage(Wallpaper? wp, ref Image destination, int height, int width, Padding padding, bool statusPanel = false, bool ToTStatusPanelLayout = false)
    {
        // MGE uses inner wallpaper from ICONS for all dialogs
        // TOT uses inner wallpaper from ICONS only for status panel, otherwise uses tiles from dialog image file
        var tiles = wp != null ? (statusPanel && wp.InnerAlt.Width > 0 ? new[] { wp.InnerAlt } : wp.Inner) : new[] { InnerWallpaper };

        if (!statusPanel)
        {
            DrawUtils.TileFill(tiles, ref destination,
                new Rectangle(padding.Left, padding.Top, width - padding.Left - padding.Right, height - padding.Top - padding.Bottom));
        }
        else
        {
            if (ToTStatusPanelLayout)
            {
                DrawUtils.TileFill(tiles, ref destination,
                    new Rectangle(padding.Left, padding.Top, 0.25f * width - padding.Left - padding.Right, height - padding.Top - padding.Bottom));
                DrawUtils.TileFill(tiles, ref destination,
                    new Rectangle(padding.Left + (0.25f * width - padding.Left - padding.Right) + 8, padding.Top, width - 0.25f * width - 8, height - padding.Top - padding.Bottom));
            }
            else
            {
                DrawUtils.TileFill(tiles, ref destination,
                    new Rectangle(padding.Left, padding.Top, width - padding.Left - padding.Right, 60));
                DrawUtils.TileFill(tiles, ref destination,
                    new Rectangle(padding.Left, padding.Top + 68, width - padding.Left - padding.Right, height - padding.Top - padding.Bottom - 68));
            }
        }
    }

    /// <summary>
    /// Paint base screen of a dialog
    /// </summary>
    /// <param name="width">Width of dialog</param>
    /// <param name="height"></param>
    /// <param name="padding"></param>
    /// <param name="centerImage">Image to place in centre of dialog</param>
    /// <param name="noWallpaper">true to not draw inner wallpaper defaults to false</param>
    /// <param name="statusPanel">true to draw status panel-style background</param>
    public static Texture2D? PaintDialogBase(IUserInterface? active, int width, int height, Padding padding, Image? centerImage = null, bool noWallpaper = false, bool statusPanel = false, bool ToTStatusPanelLayout = false)
    {
        // Outer wallpaper
        var image = Image.GenColor(width, height, new Color(0, 0, 0, 0));
        PaintPanelBorders(active, ref image, width, height, padding, statusPanel: statusPanel, toTStatusPanelLayout: ToTStatusPanelLayout);
        if (centerImage != null)
        {
            var innerWidth = width - padding.Left - padding.Right;
            var innerHeight = height - padding.Top - padding.Bottom;
            var imgW = centerImage.Value.Width;
            var imgH = centerImage.Value.Height;
            if (imgW > 0 && imgH > 0)
            {
                var scaleX = innerWidth / (float)imgW;
                var scaleY = innerHeight / (float)imgH;
                var scale = MathF.Min(scaleX, scaleY);
                var drawW = (int)(imgW * scale);
                var drawH = (int)(imgH * scale);
                var offsetX = padding.Left + (innerWidth - drawW) / 2;
                var offsetY = padding.Top + (innerHeight - drawH) / 2;
                image.Draw(centerImage.Value,
                    new Rectangle(0, 0, imgW, imgH),
                    new Rectangle(offsetX, offsetY, drawW, drawH),
                    Color.White);
            }
        }
        else if (!noWallpaper)
        {
            DrawTiledImage(Wallpaper, ref image, height, width, padding, statusPanel: statusPanel, ToTStatusPanelLayout: ToTStatusPanelLayout);
        }

        var texture = Texture2D.LoadFromImage(image);
        image.Unload();
        return texture;
    }

    public static Texture2D PaintButtonBase(int width, int height)
    {
        var rnd = new Random();
        var btn = Wallpaper?.Button;
        if (btn is not { Length: >= 3 } || btn.Any(part => part.Width <= 0 || part.Height <= 0))
        {
            return PaintFallbackButtonBase(width, height);
        }

        var len = btn.Length - 2;  // variations of inner texture
        var cols = Math.Ceiling(width / (double)btn[0].Width);

        var image = Image.GenColor(width, height, new Color(0, 0, 0, 0));
        image.Draw(btn[0], new Rectangle(0, 0, btn[0].Width, btn[0].Height), new Rectangle(0, 0, btn[0].Width, btn[0].Height), Color.White);
        for (int col = 1; col < cols - 1; col++)
        {
            image.Draw(btn[rnd.Next(1, len + 1)], new Rectangle(0, 0, btn[0].Width, btn[0].Height), new Rectangle(btn[0].Width * col, 0, btn[0].Width, btn[0].Height), Color.White);
        }
        image.Draw(btn[^1], new Rectangle(0, 0, btn[0].Width, btn[0].Height), new Rectangle(width - btn[0].Width, 0, btn[0].Width, btn[0].Height), Color.White);
        var texture = Texture2D.LoadFromImage(image);
        image.Unload();
        return texture;
    }

    private static Texture2D PaintFallbackButtonBase(int width, int height)
    {
        var image = Image.GenColor(width, height, new Color(192, 192, 192, 255));

        image.DrawLine(0, 0, width - 1, 0, Color.White);
        image.DrawLine(0, 0, 0, height - 1, Color.White);
        image.DrawLine(1, 1, width - 2, 1, new Color(224, 224, 224, 255));
        image.DrawLine(1, 1, 1, height - 2, new Color(224, 224, 224, 255));

        image.DrawLine(0, height - 1, width - 1, height - 1, new Color(64, 64, 64, 255));
        image.DrawLine(width - 1, 0, width - 1, height - 1, new Color(64, 64, 64, 255));
        image.DrawLine(1, height - 2, width - 2, height - 2, new Color(128, 128, 128, 255));
        image.DrawLine(width - 2, 1, width - 2, height - 2, new Color(128, 128, 128, 255));

        var texture = Texture2D.LoadFromImage(image);
        image.Unload();
        return texture;
    }

    public static Wallpaper? Wallpaper { get; set; }

    public static Image InnerWallpaper
    {
        get => _innerWallpaper;
        set
        {
            _innerWallpaper = value;
            InnerWallpaperTexture = Texture2D.LoadFromImage(value);
        }
    }

    public static Image OuterWallpaper
    {
        get => _outerWallpaper;
        set
        {
            _outerWallpaper = value;
            OuterWallpaperTexture = Texture2D.LoadFromImage(value);
        }
    }

    public static Image OuterTitleTopWallpaper
    {
        get => _outerTitleTopWallpaper;
        set
        {
            _outerTitleTopWallpaper = value;
            OuterTitleTopWallpaperTexture = Texture2D.LoadFromImage(value);
        }
    }

    public static Texture2D InnerWallpaperTexture { get; private set; }
    public static Texture2D OuterWallpaperTexture { get; private set; }
    public static Texture2D OuterTitleTopWallpaperTexture { get; private set; }

    public static void SetLook(IUserInterface active)
    {
        var wallpaper = new Wallpaper();
        Wallpaper = wallpaper;
        _look = active.Look;
        if (_look.Outer is null)  // TOT
        {
            wallpaper.OuterTitleTop = _look.OuterTitleTop!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterThinTop = _look.OuterThinTop!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterBottom = _look.OuterBottom!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterMiddle = _look.OuterMiddle!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterLeft = _look.OuterLeft!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterRight = _look.OuterRight!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.OuterTitleTopLeft = Images.ExtractBitmap(_look.OuterTitleTopLeft!, active);
            wallpaper.OuterTitleTopRight = Images.ExtractBitmap(_look.OuterTitleTopRight!, active);
            wallpaper.OuterThinTopLeft = Images.ExtractBitmap(_look.OuterThinTopLeft!, active);
            wallpaper.OuterThinTopRight = Images.ExtractBitmap(_look.OuterThinTopRight!, active);
            wallpaper.OuterMiddleLeft = Images.ExtractBitmap(_look.OuterMiddleLeft!, active);
            wallpaper.OuterMiddleRight = Images.ExtractBitmap(_look.OuterMiddleRight!, active);
            wallpaper.OuterBottomLeft = Images.ExtractBitmap(_look.OuterBottomLeft!, active);
            wallpaper.OuterBottomRight = Images.ExtractBitmap(_look.OuterBottomRight!, active);
            wallpaper.Inner = _look.Inner!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.InnerAlt = Images.ExtractBitmap(_look.InnerAlt!, active);
            wallpaper.Button = _look.Button!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
            wallpaper.ButtonClicked = _look.ButtonClicked!.Select(img => Images.ExtractBitmap(img, active)).ToArray();
        }
        else    // MGE
        {
            var outerSrc = Images.ExtractBitmap(_look.Outer, active);
            var srcTile = Images.ExtractBitmap(_look.Inner![0], active);

            if (TryBuildStoneWallpaper(out var stoneInner, out var stoneOuter))
            {
                wallpaper.Inner = stoneInner;
                wallpaper.Outer = stoneOuter;
            }
            else
            {
                var outerColor = new Color(65, 69, 78, 255);
                wallpaper.Outer = Image.GenColor(outerSrc.Width, outerSrc.Height, outerColor);
                var color = srcTile.GetColor(srcTile.Width / 2, srcTile.Height / 2);
                wallpaper.Inner = new[] { Image.GenColor(srcTile.Width, srcTile.Height, color) };
            }
        }

    }

    // Path of the painted slate sheet, relative to the RaylibUI asset root. When it
    // is present (every build from this repo) the MGE-style dialog and side-panel
    // chrome is cut from it instead of being flat-filled.
    private const string StoneTextureAsset = "FOSSart/Standalone/Backgrounds/stonetexture.png";

    // A uniform tile size for the interior fill. Four crops at this size, taken
    // from well-separated regions of the sheet, give DrawUtils.TileFill enough
    // variety that the repeat is not readable at dialog scale.
    private const int StoneInnerTileSize = 224;

    /// <summary>
    /// Builds the interior and border wallpaper tiles for the standalone look from
    /// the painted stone sheet. The interior crops are lightened toward warm
    /// limestone so the near-black dialog text keeps its contrast; the border crop
    /// is darkened so the bevel drawn over it reads as a carved stone frame.
    /// Returns false, leaving the caller on its flat-colour fallback, when the
    /// sheet is not on disk (a plain Civ II install).
    /// </summary>
    private static bool TryBuildStoneWallpaper(out Image[] inner, out Image outer)
    {
        inner = [];
        outer = default;

        var path = AssetPaths.Resolve(StoneTextureAsset);
        if (!File.Exists(path))
        {
            return false;
        }

        var sheet = Image.Load(path);
        try
        {
            if (sheet.Width < StoneInnerTileSize * 2 || sheet.Height < StoneInnerTileSize * 2)
            {
                return false;
            }

            var maxX = sheet.Width - StoneInnerTileSize;
            var maxY = sheet.Height - StoneInnerTileSize;
            var offsets = new[]
            {
                (X: (int)(maxX * 0.05f), Y: (int)(maxY * 0.08f)),
                (X: (int)(maxX * 0.62f), Y: (int)(maxY * 0.12f)),
                (X: (int)(maxX * 0.28f), Y: (int)(maxY * 0.70f)),
                (X: (int)(maxX * 0.88f), Y: (int)(maxY * 0.82f)),
            };

            var limestone = new Color(234, 228, 214, 255);
            inner = offsets
                .Select(o =>
                {
                    var tile = Image.FromImage(sheet,
                        new Rectangle(o.X, o.Y, StoneInnerTileSize, StoneInnerTileSize));
                    // Match every crop to the same mean brightness first, so the
                    // seam between two neighbouring tiles does not read as a patch,
                    // then lift the whole thing toward warm limestone.
                    NormaliseBrightness(ref tile, 150f);
                    TintImage(ref tile, limestone, 0.58f);
                    return tile;
                })
                .ToArray();

            // A large crop for the frame: it is tiled across strips only a few tens
            // of pixels wide, so the bigger the source the less any repeat reads.
            var borderSize = Math.Min(Math.Min(sheet.Width, sheet.Height), 480);
            outer = Image.FromImage(sheet,
                new Rectangle((sheet.Width - borderSize) / 2, (sheet.Height - borderSize) / 2,
                    borderSize, borderSize));
            TintImage(ref outer, new Color(42, 40, 41, 255), 0.3f);
            return true;
        }
        finally
        {
            sheet.Unload();
        }
    }

    /// <summary>
    /// Blends every pixel of <paramref name="image"/> a fixed fraction of the way
    /// toward <paramref name="target"/>, in place.
    /// </summary>
    private static void TintImage(ref Image image, Color target, float amount)
    {
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                image.DrawPixel(x, y, Blend(image.GetColor(x, y), target, amount));
            }
        }
    }

    /// <summary>
    /// Rescales <paramref name="image"/> so its mean channel value lands on
    /// <paramref name="target"/>, in place. Used to bring independently chosen
    /// crops of one sheet to a common exposure before they are tiled together.
    /// </summary>
    private static void NormaliseBrightness(ref Image image, float target)
    {
        double sum = 0;
        var count = image.Width * image.Height;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var c = image.GetColor(x, y);
                sum += (c.R + c.G + c.B) / 3.0;
            }
        }

        var mean = count > 0 ? sum / count : target;
        if (mean < 1)
        {
            return;
        }

        var scale = target / (float)mean;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var c = image.GetColor(x, y);
                image.DrawPixel(x, y, new Color(
                    (byte)Math.Clamp(c.R * scale, 0, 255),
                    (byte)Math.Clamp(c.G * scale, 0, 255),
                    (byte)Math.Clamp(c.B * scale, 0, 255),
                    (byte)255));
            }
        }
    }

    private static InterfaceStyle _look = null!;

    public static Image[] GetScrollImages(int dim, bool vertical)
    {
        Image image = vertical ? 
            Image.GenColor(dim, ScrollBar.ScrollbarDimDefault, new Color(240, 240, 240, 255)) :
            Image.GenColor(ScrollBar.ScrollbarDimDefault, dim, new Color(240, 240, 240, 255));
        var w = image.Width;
        var h = image.Height;

        var color1 = new Color(227, 227, 227, 255);
        image.DrawLine(0, 0, w - 1, 0, color1);
        image.DrawLine(0, 0, 0, h - 1, color1);

        var color2 = Color.White;
        image.DrawLine(1, 1, w - 2, 1, color2);
        image.DrawLine(1, 1, 1, h - 2, color2);

        var color3 = new Color(240, 240, 240, 255);
        image.DrawLine(3, 3, w - 4, h - 4, color3);

        var color4 = new Color(160, 160, 160, 255);
        image.DrawLine(w - 2, 1, w - 2, h - 1, color4);
        image.DrawLine(1, h - 2, w - 2, h - 2, color4);

        var color5 = new Color(105, 105, 105, 255);
        image.DrawLine(0, h - 1, w, h - 1, color5);
        image.DrawLine(w - 1, 0, w - 1, h - 1, color5);

        var left = image.Copy();
        if (vertical)
        {
            left.DrawPixel(w / 2, 6, Color.Black);
            left.DrawLine(w / 2 - 1, 7, w / 2 + 2, 7, Color.Black);
            left.DrawLine(w / 2 - 2, 8, w / 2 + 3, 8, Color.Black);
            left.DrawLine(w / 2 - 3, 9, w / 2 + 4, 9, Color.Black);
        }
        else
        {
            left.DrawPixel(6, h / 2, Color.Black);
            left.DrawLine(7, h / 2 - 1, 7, h / 2 + 2, Color.Black);
            left.DrawLine(8, h / 2 - 2, 8, h / 2 + 3, Color.Black);
            left.DrawLine(9, h / 2 - 3, 9, h / 2 + 4, Color.Black);
        }

        var right = image.Copy();
        if (vertical)
        {
            right.DrawPixel(w / 2, h - 7, Color.Black);
            right.DrawLine(w / 2 - 1, h - 8, w / 2 + 2, h - 8, Color.Black);
            right.DrawLine(w / 2 - 2, h - 9, w / 2 + 3, h - 9, Color.Black);
            right.DrawLine(w / 2 - 3, h - 10, w / 2 + 4, h - 10, Color.Black);
        }
        else
        {
            right.DrawPixel(w - 7, h / 2, Color.Black);
            right.DrawLine(w - 8, h / 2 - 1, w - 8, h / 2 + 2, Color.Black);
            right.DrawLine(w - 9, h / 2 - 2, w - 9, h / 2 + 3, Color.Black);
            right.DrawLine(w - 10, h / 2 - 3, w - 10, h / 2 + 4, Color.Black);
        }

        return [left, image, right];
    }

    public static Vector2 GetUnitTextures(IUnit unit, IUserInterface active, IGame game,
        List<IViewElement> viewElements, Vector2 loc, bool noStacking = false, bool useMapArt = false)
    {
        var unitImage = active.UnitImages.Units[(int)unit.Type];
        var sourceImage = GetUnitSourceImage(unit, active, unitImage, useMapArt);
        var unitTexture = TextureCache.GetImage(sourceImage);
        var shield = active.UnitShield((int)unit.Type);
        var baseShieldTexture = TextureCache.GetImage(active.UnitImages.Shields, active, unit.Owner.Id);
        var shieldTexture = GetHighResolutionUnitShieldTexture(active, unit.Owner.Id, baseShieldTexture,
            shield, ShieldLayer.Front);
        var shieldRenderScale = GetHighResolutionShieldRenderScale(baseShieldTexture, shieldTexture);
        var shieldLogicalSize = new Vector2(baseShieldTexture.Width, baseShieldTexture.Height);

        var logicalSize = GetLogicalUnitSize(active, unitImage, unitTexture);
        var unitRenderScale = GetUnitRenderScale(unitImage, unitTexture, logicalSize);
        var unitDrawOffset = GetUnitDrawOffset(unitImage, unitTexture, logicalSize, unitRenderScale);

        var tile = unit.CurrentLocation;

        if (shield.ShieldInFrontOfUnit)
        {
            // Unit
            viewElements.Add(new TextureElement(location: loc, texture: unitTexture,
                tile: tile, isShaded: unit.Order == (int)OrderType.Sleep,
                offset: unitDrawOffset, renderScale: unitRenderScale, maxDrawSize: logicalSize));
        }

        // Stacked shield
        if (unit.IsInStack && !noStacking)
        {
            if (shield.DrawShadow)
            {
                var stackShadowOffset = shield.StackingOffset + shield.ShadowOffset;
                viewElements.Add(new TextureElement(
                    location: loc,
                    texture: GetHighResolutionUnitShieldTexture(active, unit.Owner.Id, baseShieldTexture,
                        shield, ShieldLayer.Shadow),
                    tile: tile, offset: shield.Offset + stackShadowOffset,
                    renderScale: shieldRenderScale, maxDrawSize: shieldLogicalSize));
            }
            viewElements.Add(new TextureElement(
                location: loc,
                texture: GetHighResolutionUnitShieldTexture(active, unit.Owner.Id, baseShieldTexture,
                    shield, ShieldLayer.Back),
                tile: tile, offset: shield.Offset + shield.StackingOffset,
                renderScale: shieldRenderScale, maxDrawSize: shieldLogicalSize));
        }

        // Shield shadow
        if (shield.DrawShadow)
        {
            viewElements.Add(new TextureElement(location: loc,
                texture: GetHighResolutionUnitShieldTexture(active, unit.Owner.Id, baseShieldTexture,
                    shield, ShieldLayer.Shadow),
                tile: tile, offset: shield.Offset + shield.ShadowOffset,
                renderScale: shieldRenderScale, maxDrawSize: shieldLogicalSize));
        }

        // Front shield. Draw a generated high-resolution shield scaled back to the
        // original Civ2 logical shield size so maximum zoom does not magnify the
        // original low-resolution shield pixels.
        viewElements.Add(new TextureElement(location: loc,
            texture: shieldTexture, tile: tile, offset: shield.Offset,
            renderScale: shieldRenderScale, maxDrawSize: shieldLogicalSize));

        // Health bar
        viewElements.Add(new HealthBar(location: loc,
            tile: tile, unit.RemainingHitpoints, unit.HitpointsBase,
            offset: shield.Offset + shield.HPbarOffset, shield));

        // Orders text
        var shieldText = GetOrderShieldText(unit, game);
        viewElements.Add(new TextElement(shieldText, loc, shield.OrderTextHeight,
            tile, shield.Offset + shield.OrderOffset));

        // UI Additions-style exact work counter for settlers and engineers.
        if (useMapArt && unit is Unit { Counter: > 0, AiRole: Model.Constants.AiRoleType.Settle } worker)
        {
            viewElements.Add(new TextElement(worker.Counter.ToString(), loc, 12, tile,
                new Vector2(logicalSize.X / 2f, 3), Color.White, new Color(0, 0, 0, 210)));
        }

        if (!shield.ShieldInFrontOfUnit)
        {
            // Unit
            viewElements.Add(new TextureElement(location: loc, texture: unitTexture,
                tile: tile, isShaded: unit.Order == (int)OrderType.Sleep,
                offset: unitDrawOffset, renderScale: unitRenderScale, maxDrawSize: logicalSize));
        }

        if (unit.Order == (int)OrderType.Fortified)
        {
            var fortifyTexture = TextureCache.GetImage(active.UnitImages.Fortify);

            // On the map the emplacement belongs to the ground the unit is standing
            // on, so it is fitted to the tile. Away from the map there is no tile:
            // the city window's Units Present and Units Supported rows draw the
            // small classic sprite in a cell, and fitting a full-tile marker to that
            // cell put a fortification around the unit wider and taller than the
            // unit itself. Off the map it is fitted to the unit as drawn instead,
            // with enough margin to read as something the unit is standing in.
            float fortifyScale;
            Vector2 fortifyMaxSize;
            if (useMapArt)
            {
                fortifyScale = GetUnitRenderScale(unitImage, fortifyTexture, logicalSize);
                fortifyMaxSize = logicalSize;
            }
            else
            {
                fortifyMaxSize = new Vector2(
                    unitTexture.Width * unitRenderScale * FortifyMarkerOfUnit,
                    unitTexture.Height * unitRenderScale * FortifyMarkerOfUnit);
                fortifyScale = GetUnitRenderScale(unitImage, fortifyTexture, fortifyMaxSize);
            }

            viewElements.Add(new TextureElement(location: loc, texture: fortifyTexture,
                tile: tile,
                offset: new Vector2(
                    MathF.Max(0, (logicalSize.X - fortifyTexture.Width * fortifyScale) / 2f),
                    MathF.Max(0, logicalSize.Y - fortifyTexture.Height * fortifyScale)),
                renderScale: fortifyScale,
                maxDrawSize: fortifyMaxSize));
        }

        return logicalSize;
    }

    private static Texture2D GetHighResolutionUnitShieldTexture(IUserInterface active, int ownerId,
        Texture2D sourceShield, UnitShield shield, ShieldLayer layer)
    {
        var width = Math.Max(1, sourceShield.Width) * HighResolutionShieldScale;
        var height = Math.Max(1, sourceShield.Height) * HighResolutionShieldScale;
        var key = $"UnitShield-{layer}-{ownerId}-{width}x{height}-" +
                  $"{shield.HPbarOffset.X},{shield.HPbarOffset.Y}-{shield.HPbarSize.X}x{shield.HPbarSize.Y}";

        if (HighResolutionUnitShields.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var fill = ownerId >= 0 && ownerId < active.PlayerColours.Length
            ? active.PlayerColours[ownerId].LightColour
            : Color.White;
        var shade = ownerId >= 0 && ownerId < active.PlayerColours.Length
            ? active.PlayerColours[ownerId].DarkColour
            : new Color(64, 64, 64, 255);

        if (layer == ShieldLayer.Back)
        {
            fill = shade;
            shade = Blend(shade, new Color(8, 8, 12, 255), 0.52f);
        }

        var image = Image.GenColor(width, height, new Color(0, 0, 0, 0));
        PaintHighResolutionShield(ref image, fill, shade, layer, sourceShield.Width, sourceShield.Height, shield);

        var texture = Texture2D.LoadFromImage(image);
        image.Unload();
        texture.SetFilter(TextureFilter.Bilinear);
        HighResolutionUnitShields[key] = texture;
        return texture;
    }

    public static void ClearGeneratedTextures()
    {
        foreach (var texture in HighResolutionUnitShields.Values)
        {
            texture.Unload();
        }
        HighResolutionUnitShields.Clear();
    }

    private static float GetHighResolutionShieldRenderScale(Texture2D sourceShield, Texture2D highResolutionShield)
    {
        if (highResolutionShield.Width <= 0 || highResolutionShield.Height <= 0)
        {
            return 1f;
        }

        var widthScale = sourceShield.Width / (float)highResolutionShield.Width;
        var heightScale = sourceShield.Height / (float)highResolutionShield.Height;
        return MathF.Max(0.01f, MathF.Min(widthScale, heightScale));
    }

    private static void PaintHighResolutionShield(ref Image image, Color fill, Color shade, ShieldLayer layer,
        int logicalWidth, int logicalHeight, UnitShield shield)
    {
        var width = image.Width;
        var height = image.Height;
        var pixelScale = MathF.Max(1f, MathF.Min(
            width / (float)Math.Max(1, logicalWidth),
            height / (float)Math.Max(1, logicalHeight)));
        var outerEdge = 0.78f * pixelScale;
        var innerKeyline = 1.58f * pixelScale;
        var black = new Color(7, 7, 9, 255);
        var keyline = Blend(fill, new Color(12, 12, 15, 255), 0.68f);
        var wideBadge = logicalWidth > logicalHeight * 1.7f;

        for (var y = 0; y < height; y++)
        {
            var ny = (y + 0.5f) / height;
            var halfWidth = ShieldHalfWidthAt(ny, wideBadge);
            for (var x = 0; x < width; x++)
            {
                var nx = (x + 0.5f) / width;
                var distanceFromCentre = MathF.Abs(nx - 0.5f);
                if (distanceFromCentre > halfWidth)
                {
                    continue;
                }

                var edgeDistance = (halfWidth - distanceFromCentre) * width;
                var topDistance = y;
                var bottomDistance = height - y - 1;
                var outline = Math.Min(Math.Min(edgeDistance, topDistance), bottomDistance);

                if (layer == ShieldLayer.Shadow)
                {
                    image.DrawPixel(x, y, new Color(5, 5, 7, outline < pixelScale ? (byte)130 : (byte)170));
                }
                else if (outline <= outerEdge)
                {
                    image.DrawPixel(x, y, black);
                }
                else if (outline <= innerKeyline)
                {
                    image.DrawPixel(x, y, keyline);
                }
                else
                {
                    // Stay close to Civ II's flat player-colour marker.  The
                    // restrained shading only becomes apparent when zoomed in.
                    var verticalShade = Math.Clamp((ny - 0.14f) * 0.22f, 0f, 0.18f);
                    var highlight = MathF.Max(0f, 1f - MathF.Abs(nx - 0.37f) * 6.5f) *
                                    MathF.Max(0f, 1f - MathF.Abs(ny - 0.27f) * 7f);
                    var color = Blend(fill, shade, verticalShade);
                    if (highlight > 0)
                    {
                        color = Blend(color, Color.White, Math.Min(0.07f, highlight * 0.055f));
                    }
                    image.DrawPixel(x, y, color);
                }
            }
        }

        if (layer != ShieldLayer.Front)
        {
            return;
        }

        var scaleX = width / (float)Math.Max(1, logicalWidth);
        var scaleY = height / (float)Math.Max(1, logicalHeight);
        var barLeft = Math.Clamp((int)MathF.Round(shield.HPbarOffset.X * scaleX), 0, width - 1);
        var barTop = Math.Clamp((int)MathF.Round(shield.HPbarOffset.Y * scaleY), 0, height - 1);
        var barWidth = Math.Max(1, (int)MathF.Round(shield.HPbarSize.X * scaleX));
        var barHeight = Math.Max(1, (int)MathF.Round(shield.HPbarSize.Y * scaleY));
        var barRight = Math.Min(width, barLeft + barWidth);
        var barBottom = Math.Min(height, barTop + barHeight);
        for (var y = barTop; y < barBottom; y++)
        {
            for (var x = barLeft; x < barRight; x++)
            {
                if (image.GetColor(x, y).A != 0)
                {
                    image.DrawPixel(x, y, black);
                }
            }
        }
    }

    private static float ShieldHalfWidthAt(float normalizedY, bool wideBadge)
    {
        if (wideBadge)
        {
            // Test of Time places the order glyph and health strip side by side
            // in a broad chamfered badge rather than Gold's heraldic shield.
            var cornerDistance = MathF.Min(normalizedY, 1f - normalizedY);
            var corner = Math.Clamp(cornerDistance / 0.16f, 0f, 1f);
            return 0.455f + 0.03f * MathF.Sqrt(corner);
        }

        if (normalizedY < 0.12f)
        {
            var shoulder = normalizedY / 0.12f;
            return 0.39f + 0.055f * MathF.Sqrt(MathF.Max(0f, 1f - (1f - shoulder) * (1f - shoulder)));
        }

        if (normalizedY < 0.66f)
        {
            return 0.445f - (normalizedY - 0.12f) * 0.075f;
        }

        return MathF.Max(0.018f, 0.405f * (1f - (normalizedY - 0.66f) / 0.34f));
    }

    private static Color Blend(Color start, Color end, float amount)
    {
        amount = Math.Clamp(amount, 0f, 1f);
        return new Color(
            (byte)Math.Clamp(start.R + (end.R - start.R) * amount, 0, 255),
            (byte)Math.Clamp(start.G + (end.G - start.G) * amount, 0, 255),
            (byte)Math.Clamp(start.B + (end.B - start.B) * amount, 0, 255),
            255);
    }

    private static IImageSource GetUnitSourceImage(IUnit unit, IUserInterface active, UnitImage unitImage, bool useMapArt)
    {
        if (useMapArt)
        {
            return unitImage.MapImage ?? unitImage.Image;
        }

        // The UI used to insist on the small classic sheet sprite here while the map
        // drew the 1024px FOSS art. That was wrong twice over. The sprite was blurry
        // wherever the city window scaled it up, and worse, DrawScale, DrawOffset
        // and LogicalSize are all derived for the FOSS art - DrawScale is roughly
        // 48/1024 - so applying them to a sprite that was already the logical size
        // drew it at a twentieth of its proper size. That is why units were tiny in
        // the production box and in the change-production list. Use the same art the
        // map uses and the metadata matches the texture it was measured against.
        if (unitImage.MapImage is { } highResolution)
        {
            return highResolution;
        }

        if (unitImage.UiImage is { } uiImage)
        {
            return uiImage;
        }

        var unitIndex = (int)unit.Type;
        if (active.PicSources.TryGetValue("unit", out var unitIcons) &&
            unitIndex >= 0 && unitIndex < unitIcons.Length)
        {
            return unitIcons[unitIndex];
        }

        return unitImage.Image;
    }

    private static Vector2 GetLogicalUnitSize(IUserInterface active, UnitImage unitImage, Texture2D unitTexture)
    {
        if (unitImage.LogicalSize != Vector2.Zero)
        {
            return unitImage.LogicalSize;
        }

        var unitRectangle = active.UnitImages.UnitRectangle;
        if (unitRectangle.Width > 0 && unitRectangle.Height > 0)
        {
            return new Vector2(unitRectangle.Width, unitRectangle.Height);
        }

        return new Vector2(unitTexture.Width, unitTexture.Height);
    }

    /// <summary>
    /// How much of the unit's drawn size a fortification marker takes up where
    /// there is no map tile to fit it to. Slightly over one so it reads as a work
    /// the unit is standing in rather than a box drawn on top of it.
    /// </summary>
    private const float FortifyMarkerOfUnit = 0.8f;

    private static float GetUnitRenderScale(UnitImage unitImage, Texture2D unitTexture, Vector2 logicalSize)
    {
        var renderScale = unitImage.DrawScale > 0 ? unitImage.DrawScale : 1f;
        if (unitTexture.Width <= 0 || unitTexture.Height <= 0 || logicalSize.X <= 0 || logicalSize.Y <= 0)
        {
            return renderScale;
        }

        var drawWidth = unitTexture.Width * renderScale;
        var drawHeight = unitTexture.Height * renderScale;
        if (drawWidth <= logicalSize.X && drawHeight <= logicalSize.Y)
        {
            return renderScale;
        }

        // Defensive fallback for any UI path that receives a high-resolution FOSS
        // texture before UnitLoader has populated DrawScale metadata.
        return MathF.Max(0.01f, MathF.Min(logicalSize.X / unitTexture.Width, logicalSize.Y / unitTexture.Height));
    }

    private static Vector2 GetUnitDrawOffset(UnitImage unitImage, Texture2D unitTexture, Vector2 logicalSize, float renderScale)
    {
        if (unitImage.DrawOffset != Vector2.Zero)
        {
            return unitImage.DrawOffset;
        }

        var drawWidth = unitTexture.Width * renderScale;
        var drawHeight = unitTexture.Height * renderScale;
        return new Vector2(
            MathF.Max(0, (logicalSize.X - drawWidth) / 2f),
            MathF.Max(0, logicalSize.Y - drawHeight));
    }

    private static string GetOrderShieldText(IUnit unit, IGame game)
    {
        if (unit is Unit { Building: > 0 } worker)
        {
            if (worker.Building == ImprovementTypes.Road)
            {
                return "R";
            }

            if (worker.Building == ImprovementTypes.Irrigation)
            {
                return "I";
            }

            if (worker.Building == ImprovementTypes.Mining)
            {
                return "M";
            }
        }

        if (unit.Order == (int)OrderType.Automate)
        {
            return "A";
        }

        return unit.Order > 0 && unit.Order <= game.Rules.Orders.Length
            ? game.Rules.Orders[unit.Order - 1].Key
            : "-";
    }

    // public static Image GetUnitImage(IUserInterface active, Unit unit, bool noStacking = false)
    // {
    //     int w = (int)active.UnitImages.UnitRectangle.Width - 1;
    //     int h = (int)active.UnitImages.UnitRectangle.Height - 1;
    //     var image = NewImage(w, h);
    //     var rect = new Rectangle(0, 0, w, h);
    //     var flagLoc = active.UnitImages.Units[(int)unit.Type].FlagLoc;
    //     var shldSrc = new Rectangle(0, 0, active.UnitImages.ShieldShadow.Width, active.UnitImages.ShieldShadow.Height);
    //     var shldDes = new Rectangle(flagLoc.X, flagLoc.Y, shldSrc.Width, shldSrc.Height);
    //     int stackingDir = (int)active.UnitImages.Units[(int)unit.Type].FlagLoc.X < 32 ? -1 : 1;
    //     var shldShadowDes = new Rectangle(flagLoc.X + stackingDir, flagLoc.Y + 1, shldSrc.Width, shldSrc.Height);
    //     if (unit.IsInStack && !noStacking)
    //     {
    //         var shldStackShadowDes = new Rectangle(flagLoc.X + 5 * stackingDir, flagLoc.Y + 1, shldSrc.Width, shldSrc.Height);
    //         var shldStackDes = new Rectangle(flagLoc.X + 4 * stackingDir, flagLoc.Y, shldSrc.Width, shldSrc.Height);
    //         Raylib.ImageDraw(ref image, active.UnitImages.ShieldShadow, shldSrc, shldStackShadowDes, Color.WHITE);
    //         Raylib.ImageDraw(ref image, active.UnitImages.ShieldBack[unit.Owner.Id], shldSrc, shldStackDes, Color.WHITE);
    //     }
    //     Raylib.ImageDraw(ref image, active.UnitImages.ShieldShadow, shldSrc, shldShadowDes, Color.WHITE);
    //     Raylib.ImageDraw(ref image, GetShieldWithHP(active.UnitImages.Shields[unit.Owner.Id], unit), shldSrc, shldDes, Color.WHITE);
    //     var shldTxt = (int)unit.Order <= 11 ? Game.Instance.Rules.Orders[(int)unit.Order - 1].Key : string.Empty;
    //     if ((int)unit.Order == 255) shldTxt = "-";
    //     var txtMeasr = Raylib.MeasureTextEx(Fonts.AlternativeFont, shldTxt, 12, 0.0f);
    //     var txtLoc = new Vector2(shldDes.X + shldDes.Width / 2 - txtMeasr.X / 2 + 1, 
    //         shldDes.Y + shldDes.Height / 2 - txtMeasr.Y / 2 + 3);
    //     Raylib.ImageDrawTextEx(ref image, Fonts.AlternativeFont, shldTxt, txtLoc, 12, 0.0f, Color.Black);
    //     Raylib.ImageDraw(ref image, active.UnitImages.Units[(int)unit.Type].Image, rect, rect, Color.WHITE);
    //     return image;
    // }

    public static Image GetShieldWithHp(Image shield, Unit unit)
    {
        var hpShield = shield.Copy();
        var hpBarX = (int)Math.Floor((float)unit.RemainingHitpoints * 12 / unit.HitpointsBase);
        var hpColor = hpBarX switch
        {
            <= 3 => new Color(243, 0, 0, 255),
            >= 4 and <= 8 => new Color(255, 223, 79, 255),
            _ => new Color(87, 171, 39, 255)
        };
        hpShield.DrawRectangle(0, 2, hpBarX, 3, hpColor);
        hpShield.DrawRectangle(hpBarX, 2, hpShield.Width - hpBarX, 3, Color.Black);
        return hpShield;
    }

    public static Texture2D GetImpImage(IUserInterface activeInterface, IImageSource imageSource, int owner)
    {
        return TextureCache.GetImage(imageSource, activeInterface, owner);
    }

    /// <summary>
    /// How much bigger one step of zoom draws the map.
    /// </summary>
    /// <remarks>
    /// A constant proportion, so a step of the wheel does the same thing wherever
    /// you happen to be. The scale used to be linear in (8 + zoom) / 8, which made
    /// the steps wildly uneven: from the furthest out, one step *doubled* the
    /// scale, while up close one step changed it by two and a half per cent. That
    /// is a factor of nearly forty between the biggest step and the smallest, and
    /// it is what "the zoom is all over the place" describes -- zoomed out it
    /// leaps, zoomed in the wheel appears to do nothing.
    ///
    /// At 1.09 a step is nine per cent and eight of them double the scale. The
    /// reachable range is kept roughly as it was, about an eighth up to five
    /// times, by moving the ends of the zoom range to suit (see GameScreen).
    /// </remarks>
    private const double ZoomStep = 1.09;

    public static int ZoomScale(this int i, int zoom)
    {
        return (int)(ZoomScale(zoom) * i);
    }

    public static int ZoomScale(this float i, int zoom)
    {
        return (int)(ZoomScale(zoom) * i);
    }

    public static float ZoomScale(int zoom)
    {
        return (float)Math.Pow(ZoomStep, zoom);
    }

    public static Rectangle ZoomScale(this Rectangle rect, int zoom)
    {
        return new Rectangle(0, 0, rect.Width.ZoomScale(zoom), rect.Height.ZoomScale(zoom));
    }

    public static Vector2 ZoomScale(this Vector2 coords, int zoom)
    {
        return new Vector2(coords.X.ZoomScale(zoom), coords.Y.ZoomScale(zoom));
    }

    /// <summary>
    /// Scale location & dimensions of a rectangle
    /// </summary>
    /// <param name="rect">Rectangle to be scaled</param>
    /// <param name="scale">Scale factor</param>
    /// <returns></returns>
    public static Rectangle ScaleAll(this Rectangle rect, float scale) =>
        new Rectangle(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
}
