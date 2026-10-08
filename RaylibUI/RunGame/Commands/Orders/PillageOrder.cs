using RhyCiv.Engine.Enums;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using RhyCiv.Engine.Terrains;
using JetBrains.Annotations;
using Model;
using Model.Core;
using Model.Controls;
using Model.Core.Mapping;
using Model.Input;

namespace RaylibUI.RunGame.Commands.Orders;

[UsedImplicitly]
public class PillageOrder : Order
{
    private readonly IGame _game;

    public PillageOrder(GameScreen gameScreen) : 
        base(gameScreen,  new Shortcut(Key.P, shift:true), CommandIds.PillageOrder)
    {
        _game = GameScreen.Game;
    }

    public override bool Update()
    {
        var activeTile = GameScreen.Player.ActiveTile;
        var activeUnit = GameScreen.Player.ActiveUnit;
        if (activeTile.IsCityPresent || activeUnit == null || activeUnit.AttackBase == 0)
        {
            return SetCommandState(CommandStatus.Invalid);
        }

        if (activeTile.Improvements.Count > 0 &&
            activeTile.Improvements.Any(i => GameScreen.Game.TerrainImprovements.ContainsKey(i.Improvement) &&
                !GameScreen.Game.TerrainImprovements[i.Improvement].Negative))
        {
            return SetCommandState(CommandStatus.Normal);
        }

        return SetCommandState(CommandStatus.Invalid);
    }

    public override void Action()
    {
        var improvements = GameScreen.Player.ActiveTile.Improvements.Where(i =>
            _game.TerrainImprovements.ContainsKey(i.Improvement) &&
            !_game.TerrainImprovements[i.Improvement].Negative).ToList();
        if (improvements.Count > 1)
        {
            var listbox = new ListboxDefinition
            {
                Rows = Math.Min(7, improvements.Count),
                VerticalScrollbar = improvements.Count > 7
            };
            listbox.Update(improvements.Select(i =>
                _game.TerrainImprovements[i.Improvement].Levels[i.Level].Name).ToList());

            GameScreen.ShowPopup("PILLAGEWHAT", (button, selectedIndex, _, _) =>
            {
                if (button == Labels.Ok && selectedIndex >= 0 && selectedIndex < improvements.Count)
                {
                    Pillage(improvements[selectedIndex]);
                }
            }, listBox: listbox);
        }
        else
        {
            var improvementToPillage = improvements.FirstOrDefault();
            if (improvementToPillage == null) return;
            Pillage(improvementToPillage);
        }
    }

    private void Pillage(ConstructedImprovement improvementToPillage)
    {
        var player = GameScreen.Player;
        
        var activeUnit = player.ActiveUnit;
        if (activeUnit is null)
        {
            return;
        }

        activeUnit.MovePointsLost += _game.Rules.Cosmic.MovementMultiplier;
            
        var improvement = _game.TerrainImprovements[improvementToPillage.Improvement];
        player.ActiveTile.RemoveImprovement(improvement,improvementToPillage.Level, player.ActiveTile.GetCivsVisibleTo(_game));
        var tiles = new List<Tile> { player.ActiveTile };
        if (improvement.HasMultiTile)
        {
            tiles.AddRange(player.ActiveTile.Neighbours());
        }
        _game.UpdateTiles(tiles);
        if (activeUnit.MovePoints <= 0)
        {
            _game.ChooseNextUnit();
        }
    }
}
