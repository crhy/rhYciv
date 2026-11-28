using RhyCiv.Engine;
using Model.Controls;
using Model.Controls.Civilopedia;
using RaylibUI.BasicTypes;
using RaylibUI.BasicTypes.Controls;

namespace RaylibUI.RunGame.GameControls.Civilopedia;

public class CivilopediaDescription : Listbox
{
    public CivilopediaDescription(CivilopediaWindow window, GameScreen gameScreen, CivilopediaEntry pedia, int id) : base(window)
    {
        Width = window.Width - window.LayoutPadding.Left - window.LayoutPadding.Right - 4;
        Height = window.Height - window.LayoutPadding.Top - window.LayoutPadding.Bottom - 4;
        Location = new System.Numerics.Vector2(window.LayoutPadding.Left + 2, window.LayoutPadding.Top + 2);
        
        var active = gameScreen.MainWindow.ActiveInterface;
        var fontSize = TextRendering.LegibleUiFontSize(active.Look.CivilopediaFontSize);

        string text = CivilopediaLoader.GetDescription(pedia, id);
        var wrappedTexts = DialogUtils.GetWrappedTexts(text, Width, active.Look.CivilopediaFont, fontSize);
        var textHeight = Math.Max(1, (int)MathF.Ceiling(
            TextRendering.Measure(active.Look.CivilopediaFont, "Ag", fontSize, 0f).Y));
        var rows = Math.Max(1, Height / textHeight);
        if (wrappedTexts.Count > rows)
        {
            wrappedTexts = DialogUtils.GetWrappedTexts(text, Width - ScrollBar.ScrollbarDimDefault, 
                active.Look.CivilopediaFont, fontSize);
        }

        List<ListboxGroup> groups = [];
        foreach (var txt in wrappedTexts)
        {
            groups.Add(new ListboxGroup(txt));
        }

        Definition = new ListboxDefinition
        {
            Groups = groups,
            Rows = rows,
            Selectable = false,
            Type = ListboxType.Civilopedia
        };
    }
}
