using System.Diagnostics;
using RhyCiv.Engine;
using RhyCiv.Engine.MapObjects;
using Model.Controls;
using Model.Core.Mapping;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Transformations;
using RaylibUI.RunGame.GameControls;
using RaylibUI.RunGame.GameControls.Mapping.Views;

namespace RaylibUI.RunGame.GameModes;

public class ProcessingMode : IGameMode
{
    private readonly GameScreen _gameScreen;

    public ProcessingMode(GameScreen gameScreen)
    {
        _gameScreen = gameScreen;
    }
    public IGameView GetDefaultView(GameScreen gameScreen, IGameView? currentView, int viewHeight, int viewWidth,
        bool forceRedraw, System.Numerics.Vector2? offsets = null)
    {
        if (offsets is not null || currentView is not StaticView existing)
        {
            _gameScreen.StatusPanel.Update();
            return new StaticView(gameScreen, currentView, viewHeight, viewWidth, forceRedraw,
                offsets: offsets);
        }

        existing.Reset();
        return existing;
    }

    public bool MapClicked(Tile tile, MouseButton mouseButton)
    {
        return false;
    }

    public bool HandleKeyPress(Shortcut key)
    {
        // Cannot press keys while processing return true indicate this key is handled
        return true;
    }

    public bool Activate()
    {
        Debug.WriteLine("Processing other turns");
        return true;
    }

    public void PanelClick()
    {
        // Do Nothing we can't click the panel on other players turns
    }

    public IList<IControl> GetSidePanelContents (Rectangle bounds)
    {
        return Array.Empty<IControl>();
    }

    public void MouseDown(Tile tile)
    {
        // Don't care
    }

    public void MouseClear()
    {
        Input.SetMouseCursor(MouseCursor.Arrow);
    }
}
