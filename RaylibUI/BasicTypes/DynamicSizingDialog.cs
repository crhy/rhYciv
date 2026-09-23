using System.Numerics;
using Model;
using RaylibUI.BasicTypes;
using RaylibUI.BasicTypes.Controls;
using Button = RaylibUI.Controls.Button;

namespace RaylibUI;

public class DynamicSizingDialog : BaseDialog
{
    private readonly int _requestedWidth;

    private readonly HeaderLabel? _headerLabel;
    private TableLayoutPanel _innerPanel = null!;
    private int _maxHeight;

    private ControlGroup? _buttons;

    private readonly IUserInterface? _active;

    protected DynamicSizingDialog(Main host, string title, int requestedWidth, Point? position = null) :
        base(host, position)
    {
        _active = host.ActiveInterface;

        _requestedWidth = requestedWidth;
        if (!string.IsNullOrEmpty(title))
        {
            _headerLabel = _active != null ?
                new HeaderLabel(this, _active.Look, title, fontSize: _active?.Look.HeaderLabelFontSizeNormal ?? 28) :
                new HeaderLabel(this, title, 28);
            Controls.Add(_headerLabel);
        }

        LayoutPadding = _active?.GetPadding(_headerLabel?.TextSize.Y ?? 0, true) ?? new Padding(28, 11, 11, 11);
    }


    /// <summary>The widest a deliberately sized side picture, such as a portrait, is drawn.</summary>
    public const int MaxPortraitWidth = 480;

    public override void Resize(int width, int height)
    {
        var innerPanel = Controls.OfType<TableLayoutPanel>().FirstOrDefault();
        if (innerPanel is null)
        {
            return;
        }

        _innerPanel = innerPanel;
        _innerPanel.MaxControlRows = 999;

        var imageBox = _innerPanel.Controls.OfType<ImageBox>().FirstOrDefault();
        if (imageBox != null)
        {
            // The picture occupies row 0, column 0, with the dialog's text in
            // column 1, and is meant to run down the full height of the dialog
            // behind the text. Telling the table the box is one pixel high is how
            // that is arranged -- otherwise row 0 would be as tall as the picture
            // and the first line of text would sit alone beside it. The height the
            // table is given is therefore not a limit on the picture, and the
            // dialog is grown to the picture's own height further down.
            imageBox.ClampHeight = false;
            imageBox.Height = 1;
        }
        // A side-image column is for an icon (Civ2 tiles are 64px). Interfaces that
        // map the key to a full wallpaper would otherwise reserve ~1280px for the
        // picture and crush the text; cap the column and let ImageBox scale the art
        // down into it.
        // A picture the caller has sized on purpose -- a leader's portrait in an
        // audience (#183) -- is drawn at that size, up to a bound that still
        // leaves the text a column of its own.
        var maxSideImageWidth = imageBox is { Scale: not 1f } ? MaxPortraitWidth : 256;
        var imageWidth = imageBox?.GetPreferredWidth() ?? 0;
        if (imageBox != null && imageWidth > maxSideImageWidth)
        {
            imageBox.Width = maxSideImageWidth;
            imageWidth = maxSideImageWidth;
        }
        int maxTextWidth = 0;
        var labels = _innerPanel.Controls.OfType<LabelControl>();
        if (labels.Any())
        {
            maxTextWidth = labels.Max(c => c.Width);
        }
        _innerPanel.Width = Math.Max(_headerLabel?.Width ?? 0 - PaddingSide, imageWidth + Math.Max(maxTextWidth, _requestedWidth));

        // Beside a portrait the text is centred in whatever the right-hand side
        // has come to, not in the column it was wrapped for (#183).
        if (imageBox is { Scale: not 1f })
        {
            var rightSide = _innerPanel.Width - imageWidth;
            foreach (var cell in _innerPanel.TableLayout.Cells.Where(c => c is { Column: 1, Control: LabelControl }))
            {
                cell.Control!.Width = rightSide;
            }
        }

        var options = _innerPanel.Controls.OfType<OptionsPanel>().FirstOrDefault();
        if (options is not null)
        {
            options.Width = _innerPanel.Width - imageWidth;
            options.OnResize();
        }

        var listbox = _innerPanel.Controls.OfType<Listbox>().FirstOrDefault();
        if (listbox is not null)
        {
            var cell = _innerPanel.TableLayout.Cells.FirstOrDefault(c => c.Control == listbox);
            if (cell is not null)
            {
                listbox.Width = _innerPanel.Width - cell.Padding.Left - cell.Padding.Right - imageWidth;
                listbox.OnResize();
            }
        }

        _innerPanel.OnResize();

        _maxHeight = _innerPanel.Height;
        if (imageBox != null && _innerPanel.Height < imageBox.GetPreferredHeight())
        {
            _maxHeight = imageBox.GetPreferredHeight();
        }

        var menu = Controls.OfType<ControlGroup>().FirstOrDefault();
        if (menu is not null)
        {
            menu.ResizeChildWidths(_innerPanel.Width);
            menu.OnResize();
            menu.Location = new Vector2(LayoutPadding.Left, _innerPanel.Location.Y + _maxHeight + 3);
        }

        // Adjust header label position
        if (_headerLabel is not null)
        {
            _headerLabel.Location = new Vector2((_innerPanel.Width + PaddingSide) / 2 - _headerLabel.Width / 2, _headerLabel.Location.Y);
        }

        SetLocation(width, Width, height, Height);
        BackgroundImage = ImageUtils.PaintDialogBase(_active, Width, Height, LayoutPadding);
    }

    public override int Width => _innerPanel.Width + PaddingSide;
    public override int Height => _maxHeight + LayoutPadding.Top + LayoutPadding.Bottom;


    protected void SetButtons(ControlGroup buttons)
    {
        _buttons = buttons;
    }

    protected bool ButtonExists(string text)
    {
        return _buttons?.Controls?.OfType<Button>().Any(b => b.Text == text) ?? false;
    }
}
