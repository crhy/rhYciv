using System.Numerics;
using Model.Images;

namespace Model.ImageSets;

public class CityImage
{
    public IImageSource Image { get; set; } = null!;

    /// <summary>Classic Civ2-sized sprite used by city/status/dialog UI.</summary>
    public IImageSource UiImage => Image;

    /// <summary>
    /// Optional high-resolution sprite used only by zoomable map rendering.
    /// The bundled FOSS city art is 300x300; it is drawn into
    /// <see cref="LogicalSize"/> so the map layout is unchanged at normal zoom
    /// but keeps its detail when the map is zoomed in.
    /// </summary>
    public IImageSource? MapImage { get; set; }

    /// <summary>
    /// Optional crisp full-size art used by the founding-city (FOUNDED) dialog.
    /// <para>
    /// The classic 64x48 <see cref="Image"/> cell is a 4.7x shrink of the map
    /// art; the dialog stretches it to hundreds of pixels, which is why the
    /// founding popup smeared (#176). The 900px splash cut from the same
    /// painting by scripts/build_founding_splashes.py is drawn instead when
    /// present, the map art second, and the sheet cell never.
    /// </para>
    /// </summary>
    public IImageSource? SplashImage { get; set; }

    /// <summary>
    /// Size the city occupies in Civ2's logical map coordinates. This is the
    /// footprint <see cref="FlagLoc"/> and <see cref="SizeLoc"/> are measured
    /// against, so it must stay at the classic sprite's dimensions.
    /// </summary>
    public Vector2 LogicalSize { get; set; }

    public Vector2 FlagLoc { get; set; }
    public Vector2 SizeLoc { get; set; }
}
