using RhyCiv.Engine.MapObjects;
using Model.Controls;
using Model.Core.Mapping;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Transformations;
using RaylibUI.RunGame.GameControls.Mapping.Views;

namespace RaylibUI.RunGame.GameModes;

public interface IGameMode
{
    /// <summary>
    /// The view this mode shows when nothing else is going on.
    /// </summary>
    /// <param name="offsets">
    /// Where the map is to sit, when the caller knows -- a zoom about the pointer
    /// says exactly where. Passing it here rather than building a plain static
    /// view is what lets a mode keep its own view across a zoom: the active unit
    /// went on blinking only while the view belonged to the mode that draws it.
    /// </param>
    IGameView GetDefaultView(GameScreen gameScreen, IGameView? currentView, int viewHeight, int viewWidth,
        bool forceRedraw, System.Numerics.Vector2? offsets = null);
    bool MapClicked(Tile tile, MouseButton mouseButton);
    bool HandleKeyPress(Shortcut key);
    bool Activate();
    void PanelClick();
    IList<IControl> GetSidePanelContents(Rectangle bounds);
    void MouseDown(Tile tile);
    void MouseClear();
}       