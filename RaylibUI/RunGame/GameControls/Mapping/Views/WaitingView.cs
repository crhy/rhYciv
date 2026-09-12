using System.Numerics;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Units;
using Model.Core.Mapping;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

public class WaitingView : BaseGameView
{
    // gameScreen.Player, not Game.ActivePlayer. ActivePlayer is whichever
    // civilisation the engine is currently processing, which during another
    // civilisation's turn is not the human at all -- and its active tile is
    // wherever its last unit moved, frequently somewhere the player has never
    // seen. Anchoring the view there is what threw the map into unexplored black
    // on pressing Turn.
    public WaitingView(GameScreen gameScreen, IGameView? currentView, int viewHeight,
        int viewWidth, bool forceRedraw, System.Numerics.Vector2? offsets = null)
        : base(gameScreen, gameScreen.Player.ActiveTile,
        currentView, viewHeight, viewWidth, true, 200, Array.Empty<Tile>(), forceRedraw, offsets)
    {
        var activeInterface = gameScreen.Main.ActiveInterface;

        // The marker art is a full map tile now, not the classic 64x32 sprite, so it
        // has to be told the footprint it stands for. Drawn raw it came out five
        // times the size of its tile and streaked across the map.
        var marker = TextureCache.GetImage(activeInterface.MapImages.ViewPiece);
        var logicalSize = new Vector2(MapImage.TileRec.Width, MapImage.TileRec.Height);
        var renderScale = marker.Width > 0 ? logicalSize.X / marker.Width : 1f;

        SetAnimation(new[]
        {
            new TextureElement(texture: marker,
                location: ActivePos, gameScreen.Player.ActiveTile,
                renderScale: renderScale, maxDrawSize: logicalSize)
        });


        SetAnimation(Array.Empty<TextureElement>());
    }
}