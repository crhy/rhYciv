using RhyCiv.Engine.MapObjects;
using Model.Core.Mapping;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

public class StaticView : BaseGameView
{
    public StaticView(GameScreen gameScreen, IGameView? currentView, int viewHeight,
        int viewWidth, bool forceRedraw, Tile? anchor = null, System.Numerics.Vector2? offsets = null)
        : base(gameScreen, anchor ?? gameScreen.Player.ActiveTile,
        currentView, viewHeight, viewWidth, true, 2000, Array.Empty<Tile>(), forceRedraw, offsets)
    {
        SetAnimation([]);
    }
}
