
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Production;
using Model;
using Model.Controls;
using Model.Core;
using Model.Core.Cities;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Interact;
using RaylibUI.BasicTypes;
using RaylibUI.Controls;
using RaylibUI.RunGame.GameControls.CityControls;
using RaylibUtils;

namespace RaylibUI.RunGame.GameControls;

public class WondersWindow : BaseDialog
{
    private readonly IUserInterface _active;
    private readonly int _width, _height;
    private readonly GameScreen _gameScreen;
    private readonly Civilization _civ;

    public WondersWindow(GameScreen gameScreen) : base(gameScreen.Main)
    {
        _gameScreen = gameScreen;
        _active = gameScreen.MainWindow.ActiveInterface;
        var game = gameScreen.Game;
        _civ = game.GetPlayerCiv;

        LayoutPadding = _active.GetPadding(0, false);

        var back = _active.PicSources["worldWonders"][0];
        _width = Images.GetImageWidth(back, _active) + PaddingSide;
        _height = Images.GetImageHeight(back, _active) + LayoutPadding.Top + LayoutPadding.Bottom;

        BackgroundImage = ImageUtils.PaintDialogBase(_active, _width, _height, LayoutPadding,
            Images.ExtractBitmap(back, _active));

        // Every wonder in the ruleset, with where it stands and who holds it.
        // This report used to build one blank row per city of the player's own
        // civilisation and fill in nothing at all, so pressing F7 opened a window
        // of empty lines: the one place in the game that answers "who has built
        // what" answered nothing.
        List<ListboxGroup> groups = [];
        foreach (var (wonder, city) in WonderProgress.AllWonders(game))
        {
            var known = city != null &&
                        (city.Owner == _civ || city.WhoKnowsAboutIt.Length <= _civ.Id ||
                         city.WhoKnowsAboutIt[_civ.Id]);

            var where = city switch
            {
                null => UnderConstruction(game, wonder),
                not null when known => $"{city.Name} ({city.Owner.Adjective})",
                _ => "?"
            };

            groups.Add(new ListboxGroup
            {
                Elements =
                [
                    new() { Text = wonder.Name, Width = 240, TextSizeOverride = 16 },
                    new() { Text = where, TextSizeOverride = 16 }
                ],
                Height = 24
            });
        }

        var def = new ListboxDefinition()
        {
            Rows = 9,
            Selectable = false,
            Looks = new ListboxLooks()
            {
                FontSize = 16,
                TextColorFront = new Color(223, 223, 223, 255),
                TextColorShadow = new Color(67, 67, 67, 255)
            },
            Groups = groups
        };

        var listbox = new Listbox(this, def)
        {
            Width = _width - PaddingSide - 2 * 2,
            Height = 370,
            Location = new(LayoutPadding.Left + 2, LayoutPadding.Top + 2)
        };
        Controls.Add(listbox);

        var btn = new Button(this, Labels.For(LabelIndex.Close), _active.Look.ButtonFont, 18)
        {
            Location = new(LayoutPadding.Left + 2, _height - LayoutPadding.Bottom - 2 - 28),
            Width = _width - PaddingSide - 4,
            Height = 28
        };
        btn.Click += (_, _) => _gameScreen.CloseDialog(this); ;
        Controls.Add(btn);
    }

    /// <summary>
    /// What to say about a wonder nobody has finished: who is working on it, if
    /// the player has been told, and otherwise that it is simply not built.
    /// </summary>
    private string UnderConstruction(IGame game, Improvement wonder)
    {
        var building = game.AllCities
            .Where(city => WonderProgress.WonderUnderConstruction(city)?.Type == wonder.Type)
            .Where(city => city.Owner == _civ ||
                           city.WhoKnowsAboutIt.Length <= _civ.Id || city.WhoKnowsAboutIt[_civ.Id])
            .Select(city => $"{city.Name} ({city.Owner.Adjective})")
            .ToList();

        return building.Count > 0
            ? string.Join(", ", building)
            : Labels.For(LabelIndex.NONEYET);
    }

    public override int Width => _width;
    public override int Height => _height;

    public override void Resize(int width, int height)
    {
        SetLocation(width, Width, height, Height);

        foreach (var control in Controls)
        {
            control.OnResize();
        }
    }

    public override void OnKeyPress(KeyboardKey key)
    {
        if (key == KeyboardKey.Escape || key == KeyboardKey.Enter || key == KeyboardKey.KpEnter)
        {
            _gameScreen.CloseDialog(this);
        }
        base.OnKeyPress(key);
    }
}
