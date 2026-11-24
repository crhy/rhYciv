using System.Numerics;
using Model;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Rendering;
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

    // The screen size the last Resize was given, so a drag can relay out with it.
    private int _screenWidth, _screenHeight;

    // The bottom-right corner the player drags to size the dialog by hand.
    private ResizeGrip? _grip;

    /// <summary>
    /// The content height the player has chosen by dragging the corner, or 0 for the
    /// auto-fit the dialog worked out for itself (the default, and every existing
    /// advisor). A non-zero value wins over the auto-fit up to the screen height.
    /// </summary>
    public int ManualHeight { get; set; }

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

        _screenWidth = width;
        _screenHeight = height;

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

        // A hand-sized advisor. When the player has set a manual content height it wins
        // over the auto-fit, clamped between the smallest scrollable panel (one row) and
        // what is left of the screen. The inner panel keeps scrolling its content when
        // that content is taller than the height the player chose. A manual height of 0
        // leaves the auto-fit above untouched, so every existing advisor is byte-identical.
        if (ManualHeight != 0)
        {
            int minScroll = _innerPanel.TableLayout.GetHeight(0, 1);
            if (minScroll <= 0)
            {
                minScroll = 1;
            }

            int screenMax = height - LayoutPadding.Top - LayoutPadding.Bottom;
            int target = ClampHeight(ManualHeight, minScroll, screenMax);

            _innerPanel.MaxControlRows = _innerPanel.TableLayout.MaxRowsWithin(0, target);
            _innerPanel.OnResize();
            _maxHeight = target;
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

        // The corner the player drags to set ManualHeight. Drawn in the active theme's
        // colours, sitting in the bottom-right of the dialog's own border.
        if (_active is not null)
        {
            _grip ??= new ResizeGrip(this, _active.Look.HeaderLabelColour);
            if (!Controls.Contains(_grip))
            {
                Controls.Add(_grip);
            }

            _grip.Location = new Vector2(Width - ResizeGrip.Size, Height - ResizeGrip.Size);
        }
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

    /// <summary>
    /// Resolves a requested content height. A manual height of 0 means "auto-fit" and is
    /// handed back unchanged; any other value is clamped into [min, max].
    /// </summary>
    public static int ClampHeight(int manual, int min, int max)
    {
        if (manual == 0)
        {
            return 0;
        }

        if (max < min)
        {
            return min;
        }

        return Math.Clamp(manual, min, max);
    }

    /// <summary>
    /// Grows (negative shrinks) the manual content height by a drag delta and lays out
    /// again, so a corner drag feeds straight back into Resize.
    /// </summary>
    public void GrowHeight(int deltaPixels)
    {
        var baseHeight = ManualHeight != 0 ? ManualHeight : _maxHeight;
        ManualHeight = baseHeight + deltaPixels;

        if (_screenWidth > 0)
        {
            Resize(_screenWidth, _screenHeight);
        }
    }

    private sealed class ResizeGrip : BaseControl
    {
        public const int Size = 22;

        private readonly DynamicSizingDialog _dialog;
        private readonly Color _colour;

        public ResizeGrip(DynamicSizingDialog dialog, Color colour) : base(dialog)
        {
            _dialog = dialog;
            _colour = colour;
            Width = Size;
            Height = Size;
        }

        public override void OnMouseMove(Vector2 moveAmount)
        {
            if (Input.IsMouseButtonDown(MouseButton.Left))
            {
                _dialog.GrowHeight((int)Math.Round(moveAmount.Y));
            }
        }

        public override void Draw(bool pulse)
        {
            int x = (int)Bounds.X;
            int y = (int)Bounds.Y;
            for (int i = 0; i < 3; i++)
            {
                int inset = i * 5;
                Graphics.DrawLine(x + Size - 3 - inset, y + 3 + inset,
                    x + Size - 8 - inset, y + Size - 3 - inset, _colour);
            }
        }
    }
}
