using RhyCiv.Engine;
using Model.Core;
using Model.Images;
using Model.ImageSets;

namespace Model.Controls;

public class DialogElements
{
    /// <summary>
    /// Name of popupbox from GAME.TXT.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Requested width of inner panel (should be 1.5x larger than this value).
    /// </summary>
    public int? Width { get; set; }

    /// <summary>
    /// Text in dialog's header.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Dialog offset in pixels (neg.=from right edge, 0=center)
    /// </summary>
    public int? X { get; set; }

    /// <summary>
    /// Popup offset in pixels (neg.=from bottom edge, 0=center)
    /// </summary>
    public int? Y { get; set; }

    /// <summary>
    /// Relative dialog position on screen (use this if X & Y are not defined).
    /// </summary>
    public Point DialogPos { get; init; } = new Point(0, 0);

    /// <summary>
    /// Text in buttons.
    /// </summary>
    public IList<string>? Button { get; set; }

    /// <summary>
    /// Asks for the dialog to be laid out at the width it requested rather than at
    /// the wide default a text-only message otherwise gets.
    /// <para>
    /// The wide default suits a proclamation of several sentences, which is what
    /// most text-only dialogs in this game are. A short remark reads badly stretched
    /// across two thirds of the screen on one line, so a dialog that is a sentence
    /// or two can ask to keep its own width and wrap sooner instead.
    /// </para>
    /// </summary>
    public bool Compact { get; set; }

    public List<Decoration> Decorations { get; } = new();

    /// <summary>
    /// Texts for TextBoxDefinition.
    /// </summary>
    public IList<string>? Text { get; set; }

    /// <summary>
    /// Line styles for TextBoxDefinition.
    /// </summary>
    public IList<TextStyles>? LineStyles { get; set; }

    public List<TextBoxDefinition>? TextBoxes { get; set; }
    public ListboxDefinition? Listbox { get; set; }
    public OptionsDefinition? Options { get; set; }
    public IList<int>? ReplaceNumbers { get; set; }
    public IList<string>? ReplaceStrings { get; set; }
    public DialogImageElements? Image { get; set; }

    public DialogElements(PopupBox? popupBox = null)
    {
        Name = popupBox?.Name;
        Width = popupBox?.Width;
        Title = popupBox?.Title;
        if (popupBox?.Default is not null)
        {
            Options ??= new();
            Options.SelectedId = popupBox.Default ?? 0;
        }
        X = popupBox?.X;
        Y = popupBox?.Y;
        Button = popupBox?.Button;
        if (popupBox?.Options is not null)
        {
            Options ??= new();
            Options.Texts = popupBox?.Options ?? [];
            Options.IsCheckbox = popupBox?.Checkbox ?? false;
        }
        if (popupBox is not null && popupBox.Listbox)
        {
            Listbox = new();
        }
        if (popupBox?.ListboxLines is not null)
        {
            Listbox ??= new();
            Listbox.Rows = (int)popupBox.ListboxLines;
        }
        // Copies, never the cached definition's own lists. The dialog that gets
        // built from this merges adjacent lines and removes the ones it has merged
        // away, and writing that back would change the game's copy of the dialog
        // for every later showing of it -- the same reason the buttons are copied
        // where they are overridden.
        if (popupBox?.Text is not null)
        {
            Text = new List<string>(popupBox.Text);
        }
        if (popupBox?.LineStyles is not null)
        {
            LineStyles = new List<TextStyles>(popupBox.LineStyles);
        }
    }
}