using Model.Core.Units;
using Model.Images;

namespace Model.Controls;

public class DialogImageElements
{
    public IImageSource[]? Image { get; }
    public float Scale { get; } = 1f;
    public int[,] Coords { get; }

    /// <summary>
    /// A person the dialog is spoken by -- a leader in an audience. The dialog is
    /// then laid out around them: the portrait fills the left half, and what is
    /// said is centred in the right half with room either side (#183).
    /// </summary>
    public bool Portrait { get; init; }
    
    public DialogImageElements(IImageSource[]? image, float scale = 1f, int[,]? coords = null)
    {
        Image = image;
        Scale = scale;
        Coords = coords ?? new int[image?.GetLength(0) ?? 1, 2];
    }

    public DialogImageElements(IImageSource? image, float scale = 1f) :
        this(image is null ? null : [image], scale: scale, coords: new int[,] { { 0, 0 } })
    {
    }

    public DialogImageElements(IUnit unit, IUserInterface active)
    {
        var unitImage = unit.Type >= 0 && active.UnitImages.Units.Length > unit.Type &&
                        active.UnitImages.Units[unit.Type].Image is { } highResolutionImage
            ? highResolutionImage
            : active.PicSources["unit"][unit.Type];

        Image = [unitImage];
        Coords = new int[,] { { 0, 0 } };
    }
}
