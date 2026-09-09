using Model;
using Model.Controls;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Rendering;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.RunGame.GameControls;
using RaylibUI.RunGame.GameControls.CityControls;
using RaylibUtils;
using System.Diagnostics;
using System.Numerics;

namespace RaylibUI.BasicTypes;

public class ListboxControlGroup : ControlGroup
{
    public Action<ListboxControlGroup, bool> Selected { get; set; } = (_, _) => { };
    private readonly List<ListboxGroupElement> _elements;

    /// <summary>
    /// The row's labels paired with the element each came from, so a colour a row
    /// asked for can be put back after the selection has moved. Changing the
    /// selection repaints every row from the list's own palette, which used to
    /// throw away any colour an individual row had chosen the first time the
    /// selection moved -- so a row could be coloured, but only until the player
    /// touched the list.
    /// </summary>
    private readonly List<(LabelControl Label, ListboxGroupElement Element)> _labels = [];
    private bool _softSelection;    // true = don't make final selection based on this
    private readonly IUserInterface _active;
    private readonly ListboxLooks _looks;
    public bool IsSelected { get; set; }

    public ListboxControlGroup(IControlLayout controller, ListboxDefinition def, int index) :
        base(controller, eventTransparent: false)
    {
        _active = controller.MainWindow.ActiveInterface!;
        var group = def.Groups[index];
        _elements = group.Elements;
        _looks = def.Looks;

        if (group.Height != null)
        {
            Height = (int)group.Height;
        }

        // Make controls
        for (int i = 0; i < _elements.Count; i++)
        {
            var element = _elements[i];
            if (element.Text != string.Empty)
            {
                var fontSize = element.TextSizeOverride != null ? element.TextSizeOverride : def.Looks.FontSize;
                var colorFront = element.FrontColorOverride != null ? element.FrontColorOverride : def.Looks.TextColorFront;
                var colorShadow = element.ShadowColorOverride != null ? element.ShadowColorOverride : def.Looks.TextColorShadow;

                var label = new LabelControl(controller, element.Text, true, font: def.Looks.Font, fontSize: (int)fontSize,
                    colorFront: colorFront, colorShadow: colorShadow, shadowOffset: def.Looks.TextShadowOffset, 
                    horizontalAlignment: element.HorizontalAlignment, verticalAlignment: element.VerticalAlignment);
                if (group.Height != null)
                {
                    label.Height = (int)group.Height;
                }
                Controls.Add(label);
                _labels.Add((label, element));
            }
            else if (element.Icon is not null)
            {
                var imagebox = new ImageBox(controller, element.Icon, element.ScaleIcon);
                if (element.Width != null)
                {
                    imagebox.Width = (int)element.Width;
                }
                if (group.Height != null)
                {
                    imagebox.Height = (int)group.Height;
                }

                FitIcon(imagebox, element.ScaleIcon, def.ImageShift && index % 2 == 1, element.FitIconToCell);
                Controls.Add(imagebox);
            }
            else if (element.Unit is not null)
            {
                // Centre the unit in the cell it was given. It used to be pinned to
                // the corner, so a sprite smaller than its cell sat up and to the
                // left of where the box implied it was.
                var display = new UnitDisplay(controller, element.Unit, element.Game!, new(0, 0),
                    _active, element.ScaleIcon, true);
                var cellWidth = element.Width ?? display.Width;
                var cellHeight = group.Height ?? display.Height;
                display.Location = new(Math.Max(0, (cellWidth - display.Width) / 2f),
                    Math.Max(0, (cellHeight - display.Height) / 2f));
                Controls.Add(display);
            }
        }

        _softSelection = true;

        Click += OnClick;
    }

    public override bool CanFocus => false;

    private int _width;
    public override int Width
    {
        get
        {
            if (_width == 0)
            {
                _width = base.GetPreferredWidth();
            }

            return _width;
        }
        set { _width = value; }
    }

    private int _height;
    public override int Height
    {
        get
        {
            if (_height == 0)
            {
                _height = base.GetPreferredHeight();
            }

            return _height;
        }
        set { _height = value; }
    }


    private void OnClick(object? sender, MouseEventArgs e)
    {
        SelectThis(false);
    }

    public void SelectThis(bool softSelection)
    {
        _softSelection = softSelection;
        Selected(this, _softSelection);
    }

    public override void OnResize()
    {
        int offset = 0;
        for (int i = 0; i < Controls.Count; i++)
        {
            var child = Controls[i];
            var element = _elements[i];
            offset = _elements[i].Xoffset ?? offset;
            child.Location = new Vector2(offset, 0);
            child.Width = element.Width ?? child.GetPreferredWidth();
            child.Height = element.Height ?? (Height > 0 ? Height : child.GetPreferredHeight());
            if (child is ImageBox imageBox)
            {
                FitIcon(imageBox, element.ScaleIcon, false, element.FitIconToCell);
            }
            offset += child.Width;
        }

        // Stretch last control to get desired width
        if (Width > offset && Controls.Count > 0 && Controls[^1] is LabelControl)
        {
            Controls[^1].Width += Width - offset;
        }
    }

    /// <summary>
    /// Paints this row for the selection, keeping any colour the row asked for.
    /// </summary>
    public void ApplySelection(bool selected, ListboxLooks looks)
    {
        IsSelected = selected;

        foreach (var (label, element) in _labels)
        {
            label.BackgroundColor = null;
            label.Font = selected ? looks.SelectedTextFont : looks.Font;
            label.ShadowOffset = selected ? looks.SelectedTextShadowOffset : looks.TextShadowOffset;

            // A row's own colour wins when it is not the selected row. The selected
            // row takes the list's highlight colours, because the point of them is
            // to read against the selection band.
            label.ColorFront = selected
                ? looks.SelectedTextColorFront
                : element.FrontColorOverride ?? looks.TextColorFront;
            label.ColorShadow = selected
                ? looks.SelectedTextColorShadow
                : element.ShadowColorOverride ?? looks.TextColorShadow;
        }
    }

    public override void Draw(bool pulse)
    {
        // The visibility check belongs here, not only in base.Draw. A row scrolled
        // out of the top of the list still has bounds, and painting its selection
        // band before asking whether it is on screen put a grey rectangle out over
        // the map that slid about as the list was scrolled.
        if (!Visible)
        {
            return;
        }

        if (IsSelected)
        {
            Graphics.DrawRectangleRec(Bounds, _looks.SelectedTextBackgroundColor);
        }

        base.Draw(pulse);
    }

    private void FitIcon(ImageBox imageBox, float baseScale, bool shiftToRightHalf, bool fitToCell = false)
    {
        var slotWidth = imageBox.Width;
        var slotHeight = imageBox.Height;
        imageBox.Scale = baseScale;
        imageBox.Width = slotWidth;
        imageBox.Height = slotHeight;

        var imageWidth = Images.GetImageWidth(imageBox.Image[0], _active, imageBox.Scale);
        var imageHeight = Images.GetImageHeight(imageBox.Image[0], _active, imageBox.Scale);
        if (imageWidth > 0 && imageHeight > 0)
        {
            var maxWidth = Math.Max(1, slotWidth - 4);
            var maxHeight = Math.Max(1, slotHeight - 4);
            // Without fitToCell an icon is only ever shrunk, so art smaller than
            // its cell stays small however much room it has.
            if (fitToCell || imageWidth > maxWidth || imageHeight > maxHeight)
            {
                var fitScale = Math.Min(maxWidth / (float)imageWidth, maxHeight / (float)imageHeight);
                imageBox.Scale *= Math.Max(0.05f, fitScale);
                imageBox.Width = slotWidth;
                imageBox.Height = slotHeight;
                imageWidth = Images.GetImageWidth(imageBox.Image[0], _active, imageBox.Scale);
                imageHeight = Images.GetImageHeight(imageBox.Image[0], _active, imageBox.Scale);
            }
        }

        var coordX = Math.Max(0, slotWidth / 2 - imageWidth / 2);
        if (shiftToRightHalf && imageWidth <= slotWidth / 2)
        {
            coordX = Math.Max(0, Math.Min(slotWidth - imageWidth, coordX + slotWidth / 2));
        }
        var coordY = Math.Max(0, slotHeight / 2 - imageHeight / 2);
        imageBox.Coords = new int[,] { { coordX, coordY } };
    }
}
