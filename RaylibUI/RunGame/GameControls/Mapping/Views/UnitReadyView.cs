using RhyCiv.Engine.Units;
using Model.Core.Units;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

public class UnitReadyView : BaseGameView
{
    public UnitReadyView(GameScreen gameScreen, IGameView? currentView, int viewHeight,
        int viewWidth, Unit unit, bool forceRedraw, System.Numerics.Vector2? offsets = null)
        : base(gameScreen, unit.CurrentLocation,
        currentView, viewHeight, viewWidth, true, 150, new []{ unit.CurrentLocation }, forceRedraw, offsets)
    {
        this.Unit = unit;
        var activeInterface = gameScreen.Main.ActiveInterface;

        var elements = new List<IViewElement>();
        ImageUtils.GetUnitTextures(unit, activeInterface, gameScreen.Game, elements, ActivePos with{ Y = ActivePos.Y + Dimensions.TileHeight - activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom)}, useMapArt: true);

        SetAnimation(elements);

        SetAnimation([]);
    }

    public Unit Unit { get; set; }
}