using System.Diagnostics;
using System.Linq;
using System.Numerics;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using Model.Core.Units;
using RaylibUI.RunGame.GameControls.Mapping.Views.ViewElements;
using ExtensionMethods;

namespace RaylibUI.RunGame.GameControls.Mapping.Views;

internal class MoveAnimation : BaseGameView
{
    public MoveAnimation(GameScreen gameScreen, MovementEventArgs moveEvent, IGameView? previousView, int viewHeight,
        int viewWidth, bool forceRedraw) : base(gameScreen, moveEvent.Location.First(), previousView, viewHeight, viewWidth, false, 12, moveEvent.Location, forceRedraw)
    {
        var activeInterface = gameScreen.Main.ActiveInterface;
        var activeUnit = moveEvent.Unit;
        var noFramesForOneMove = 4;
        var map = activeUnit.CurrentLocation.Map;
        float[] unitDrawOffset = { activeUnit.X - activeUnit.PrevXy[0], activeUnit.Y - activeUnit.PrevXy[1] };
        if (!map.Flat && Math.Abs(unitDrawOffset[0]) >= map.XDimMax - 2)
        {
            if (unitDrawOffset[0] < 0)
            {
                unitDrawOffset[0] += map.XDimMax;
            }
            else
            {
                unitDrawOffset[0] -= map.XDimMax;
            }
        }
        
        // Get view elements of units on previous tile of moving unit.
        //
        // Nothing is drawn for a square with a city on it. The map never shows a
        // city's garrison -- the flag over the city says one is there -- but the
        // bystanders in a move were drawn whatever they were standing on, so a unit
        // walking out of a city made its garrison flash into view for the length of
        // the step and then vanish again.
        var viewElementsPrevTileUnits = new List<IViewElement>();
        var previousTile = map.TileC2(activeUnit.PrevXy[0], activeUnit.PrevXy[1]);
        var prevTileUnit = previousTile.CityHere == null ? previousTile.UnitsHere.FirstOrDefault() : null;
        if (prevTileUnit != null)
        {
            ImageUtils.GetUnitTextures(prevTileUnit, activeInterface, gameScreen.Game, viewElementsPrevTileUnits,
                ActivePos with { Y = ActivePos.Y - activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom) + Dimensions.TileHeight }, useMapArt: true);
        }

        // Get view elements of units on next tile of moving unit
        var viewElementsNextTileUnits = new List<IViewElement>();
        var nextTileUnit = activeUnit.CurrentLocation.CityHere != null
            ? null
            : activeUnit.CurrentLocation.UnitsHere.FirstOrDefault(u => u != activeUnit && !activeUnit.CarriedUnits.Contains(u));
        if (nextTileUnit != null)
        {
            ImageUtils.GetUnitTextures(nextTileUnit, activeInterface, gameScreen.Game, viewElementsNextTileUnits,
                new Vector2(unitDrawOffset[0] * (4 * (gameScreen.Zoom + 8)), unitDrawOffset[1] * (2 * (gameScreen.Zoom + 8))) + ActivePos with { Y = ActivePos.Y - activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom) + Dimensions.TileHeight }, useMapArt: true);
        }

        // Moving unit view elements
        var viewElementsActiveUnit = new List<IViewElement>();
        ImageUtils.GetUnitTextures(activeUnit, activeInterface, gameScreen.Game, viewElementsActiveUnit,
            ActivePos with { Y = ActivePos.Y - activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom) + Dimensions.TileHeight }, true, useMapArt: true);

        SetAnimation(viewElementsPrevTileUnits.Concat(viewElementsNextTileUnits).Concat(viewElementsActiveUnit).ToList());

        var totalFrames = activeUnit.CurrentLocation.CityHere == null ? noFramesForOneMove : noFramesForOneMove - 1;
        for (var frame = 1; frame < totalFrames; frame++)
        {
            var offsetVector = new Vector2(unitDrawOffset[0] * (4 * (gameScreen.Zoom + 8)) / noFramesForOneMove * frame,
                +unitDrawOffset[1] * (2 * (gameScreen.Zoom + 8)) / noFramesForOneMove * frame);
            var animPrevUnit = viewElementsPrevTileUnits.Select(ve => ve.CloneForLocation(ve.Location));
            var animNextUnit = viewElementsNextTileUnits.Select(ve => ve.CloneForLocation(ve.Location));
            var animActiveUnit = viewElementsActiveUnit.Select(ve => ve.CloneForLocation(ve.Location + offsetVector));
            SetAnimation(animPrevUnit.Concat(animNextUnit).Concat(animActiveUnit).ToList());
        }

        if (totalFrames != noFramesForOneMove)
        {
            SetAnimation([]);
        }

        HoldOnSomeoneElsesMove(gameScreen, activeUnit,
            viewElementsPrevTileUnits.Concat(viewElementsNextTileUnits).ToList());
    }

    /// <summary>
    /// Frames the last position of another civilisation's unit is held for. This
    /// view runs at a 12ms interval, so a move takes about fifty milliseconds --
    /// fine when you are the one moving and watching the square you chose, far too
    /// quick to follow when something steps out of the trees during someone else's
    /// turn.
    /// </summary>
    private const int ForeignMoveHoldFrames = 26;

    /// <summary>
    /// Lets the player see a move that was not theirs.
    /// <para>
    /// Movement the player can watch is now only movement they can actually see, so
    /// when an enemy unit does appear it is worth stopping for. Without this the
    /// barbarians crossed open ground and were back to the player's own turn before
    /// the eye could follow what had happened.
    /// </para>
    /// </summary>
    private void HoldOnSomeoneElsesMove(GameScreen gameScreen, Unit movingUnit,
        List<IViewElement> bystanders)
    {
        if (movingUnit.Owner.Id == gameScreen.Player.Civilization.Id)
        {
            return;
        }

        var settled = new List<IViewElement>(bystanders);
        var activeInterface = gameScreen.Main.ActiveInterface;
        var restingPos = GetPosForTile(movingUnit.CurrentLocation);
        ImageUtils.GetUnitTextures(movingUnit, activeInterface, gameScreen.Game, settled,
            restingPos with
            {
                Y = restingPos.Y + Dimensions.TileHeight -
                    activeInterface.UnitImages.UnitRectangle.Height.ZoomScale(gameScreen.Zoom)
            }, useMapArt: true);

        for (var frame = 0; frame < ForeignMoveHoldFrames; frame++)
        {
            SetAnimation(settled.Select(element => element.CloneForLocation(element.Location)).ToList());
        }
    }
}
