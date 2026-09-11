using RhyCiv.Engine;
using Model;
using Model.Controls;
using Model.Core;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Interact;
using RaylibUI.BasicTypes;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.Controls;
using RaylibUI.Dialogs;
using System.Linq;
using System.Numerics;
using RhyCiv.Engine.IO;

namespace RaylibUI;

public class CivDialog : DynamicSizingDialog
{
    private readonly IUserInterface _active;
    private readonly TableLayoutPanel _innerPanel;
    private readonly IList<LabeledTextBox> _textBoxes;
    private readonly Action<string, int, IList<bool>?, IDictionary<string, string>?> _handleButtonClick;
    private readonly OptionsPanel? _optionsPanel;
    private readonly Listbox? _listbox;
    private int _selectedIndex = -1;
    private readonly ImageBox _imageBox;
    private bool _closed;

    /// <summary>
    /// A plain message / choice popup: it carries body text and, at most, radio
    /// options, with no text entry, list, or picture. These are the classic
    /// GAME.TXT event prompts, and we render them as a large, centred, legible
    /// panel rather than a small strip.
    /// </summary>
    private static bool IsMessageLayout(DialogElements d) =>
        !d.Compact
        && d.Text is { Count: > 0 }
        && (d.TextBoxes is null || d.TextBoxes.Count == 0)
        && d.Listbox is null
        && (d.Image is null || d.Image.Image.Any(n => n is null));

    /// <summary>
    /// Inner-panel width to request for a text dialog, in logical pixels.
    /// <para>
    /// This was 62% of the window, which on a 1080p screen is eleven hundred pixels
    /// and about a hundred and forty characters to a line. Prose that wide is hard
    /// to read -- the eye loses its place coming back to the left margin -- and it
    /// left most of the game's messages as one long band across the middle of the
    /// screen. A third of the window puts a line at roughly sixty characters, which
    /// is the measure a book is set to, and the dialog grows downwards into a
    /// paragraph instead.
    /// </para>
    /// </summary>
    private static int MessagePanelWidth() =>
        Math.Clamp((int)(DisplayScale.Width * 0.34f), 420, 620);

    private static int RequestedWidth(Main host, DialogElements d)
    {
        if (!IsMessageLayout(d))
        {
            return d.Width == null
                ? host.ActiveInterface.DefaultDialogWidth
                : (int)(1.5 * d.Width);
        }

        // A comfortable measure, but never narrower than a line that is not allowed
        // to break. Text marked as being on its own line is never wrapped, so a long
        // one -- a web address, say -- used to run straight out through the side of
        // the dialog and get cut off by the frame.
        var width = MessagePanelWidth();
        if (d.Text is { Count: > 0 } && d.LineStyles is { Count: > 0 })
        {
            var look = host.ActiveInterface.Look;
            var fontSize = look.LabelFontSize + 4;
            for (var i = 0; i < d.Text.Count && i < d.LineStyles.Count; i++)
            {
                if (d.LineStyles[i] == TextStyles.Left)
                {
                    continue;
                }

                var line = DialogUtils.ReplacePlaceholders(d.Text[i], d.ReplaceStrings, d.ReplaceNumbers) ?? string.Empty;
                var measured = (int)MathF.Ceiling(TextRendering.Measure(look.LabelFont, line, fontSize, 1f).X);
                width = Math.Max(width, measured + 24);
            }
        }

        return Math.Min(width, Math.Max(320, DisplayScale.Width - 48));
    }

    public CivDialog(Main host, DialogElements dialog, Action<string, int, IList<bool>?, IDictionary<string, string>?> handleButtonClick) :
        base(host, DialogUtils.ReplacePlaceholders(dialog.Title, dialog.ReplaceStrings, dialog.ReplaceNumbers),
            RequestedWidth(host, dialog),
            dialog.X != null || dialog.Y != null ? new Point(
                (dialog.X ?? 0) / DisplayScale.Width,
                (dialog.Y ?? 0) / DisplayScale.Height) : dialog.DialogPos)
    {
        _active = host.ActiveInterface;
        _handleButtonClick = handleButtonClick;
        var normalizedDialogName = dialog.Name?.Trim() ?? string.Empty;
        var title = DialogUtils.ReplacePlaceholders(dialog.Title, dialog.ReplaceStrings, dialog.ReplaceNumbers)
                    ?? string.Empty;
        var isIntroDialog = string.Equals(normalizedDialogName, "INIT", StringComparison.OrdinalIgnoreCase) ||
                            title.Contains("In the Beginning", StringComparison.OrdinalIgnoreCase);
        var isNameDialog = string.Equals(normalizedDialogName, "NAME", StringComparison.OrdinalIgnoreCase) ||
                           title.Contains("Enter Your Name", StringComparison.OrdinalIgnoreCase);
        var isMessageDialog = IsMessageLayout(dialog);
        var isBigDialog = isIntroDialog || isMessageDialog;
        var dialogFontSize = _active.Look.LabelFontSize + (isBigDialog ? 4 : 1);
        var textFront = TextRendering.StrongBlack;
        var textShadow = new Color(255, 255, 245, (byte)(isBigDialog ? 220 : 160));
        var textShadowOffset = new Vector2(-1, -1);

        var innerLayout = new TableLayout();
        var layoutRow = 0;

        if (dialog.Image != null && dialog.Image.Image.All(n => n != null))
        {
            _imageBox = new ImageBox(this, dialog.Image);
            innerLayout.Add(_imageBox, 0, 0);
        }

        var maxTextWidth = 0;
        if (dialog.Text?.Count > 0)
        {
            // Lists of this dialog's own, because the grouping below merges lines
            // and removes the ones it merged away. Callers hand this in as whatever
            // they had -- a cached definition's list, or a fixed-size array from a
            // script -- and neither is safe to write to: one changes the game's copy
            // of the dialog for every later showing of it, the other throws.
            var texts = new List<string>(dialog.Text);

            // A style for every line, whatever the caller supplied. A dialog built
            // in code can set its text and leave the styles out, and the grouping
            // below indexed them without looking: the "could not load that save"
            // message took the game down on the way to telling the player their
            // save was unreadable, which is the worst possible moment for a second
            // fault. Left is what an unstyled line has always been rendered as.
            var styles = new List<TextStyles>(dialog.LineStyles ?? []);
            while (styles.Count < texts.Count)
            {
                styles.Add(TextStyles.Left);
            }

            // Group left-aligned texts
            int i = 0;
            while (i < texts.Count - 1)
            {
                i++;
                if (styles[i - 1] == TextStyles.Left && styles[i] == TextStyles.Left)
                {
                    texts[i] = $"{texts[i - 1]} {texts[i]}";
                    texts.RemoveAt(i - 1);
                    styles.RemoveAt(i - 1);
                    i = 0;
                }
            }

            // Replace %STRING, %NUMBER
            texts = texts.Select(t => DialogUtils.ReplacePlaceholders(t, dialog.ReplaceStrings, dialog.ReplaceNumbers)).ToList();
            texts = texts.Select(t => t.Replace("_", " ")).ToList();

            // First make center and own line labels as they determine dialog width
            List<LabelControl> nonWrappedLabels = [];
            for (var j = 0; j < texts.Count; j++)
            {
                if (styles[j] != TextStyles.Left)
                {
                    nonWrappedLabels.Add(new LabelControl(this,
                        string.IsNullOrEmpty(texts[j]) && styles[j] == TextStyles.LeftOwnLine ? " " : texts[j],    // Add space if ^ is the only character 
                        false,
                        horizontalAlignment: styles[j] == TextStyles.Centered || isBigDialog ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                        font: _active.Look.LabelFont, fontSize: dialogFontSize, colorFront: textFront, colorShadow: textShadow, shadowOffset: textShadowOffset));
                }
            }

            maxTextWidth = GetInnerPanelWidthFromText(nonWrappedLabels, dialog.Width ?? 0);
            if (isMessageDialog)
            {
                // Wrap the body to the full requested panel so the message fills
                // the dialog instead of clumping to one side of it.
                maxTextWidth = Math.Max(maxTextWidth, MessagePanelWidth());
            }

            // Make wrapped labels and adjust width of all labels
            List<LabelControl> textLabels = [];
            for (var j = 0; j < texts.Count; j++)
            {
                if (styles[j] == TextStyles.Left)
                {
                    var wrappedTexts = DialogUtils.GetWrappedTexts(texts[j], maxTextWidth, _active.Look.LabelFont, dialogFontSize);

                    foreach (var text in wrappedTexts)
                    {
                        var wrappedLabel = new LabelControl(this,
                            string.IsNullOrEmpty(text) ? " " : text,    // Add space if ^ is the only character 
                            false,
                            horizontalAlignment: styles[j] == TextStyles.Centered || isBigDialog ? HorizontalAlignment.Center : HorizontalAlignment.Left,
                            font: _active.Look.LabelFont, fontSize: dialogFontSize,
                            colorFront: textFront, colorShadow: textShadow, shadowOffset: textShadowOffset);

                        wrappedLabel.Width = maxTextWidth;
                        innerLayout.Add(wrappedLabel, layoutRow++, 1);
                    }

                }
                else
                {
                    var label = nonWrappedLabels[0];
                    label.Width = maxTextWidth;
                    innerLayout.Add(label, layoutRow++, 1);
                    nonWrappedLabels.RemoveAt(0);
                }
            }
        }

        if (dialog.Listbox != null)
        {
            _listbox = new Listbox(this, dialog.Listbox);
            innerLayout.Add(_listbox, layoutRow++, 1, new Padding(2, 2, 2, 2));
            _listbox.ItemSelected += ListboxOnItemSelected;
            _selectedIndex = 0;
        }

        if (dialog.TextBoxes is { Count: > 0 })
        {
            _textBoxes = new List<LabeledTextBox>();
            List<string> textBoxLabels;
            if (dialog.TextBoxes.Any(t => string.IsNullOrWhiteSpace(t.Description)) && dialog.Options != null && dialog.Options.Texts.Count == dialog.TextBoxes.Count)
            {
                textBoxLabels = new List<string>(dialog.Options.Texts);
                dialog.Options = null;
            }
            else
            {
                textBoxLabels = dialog.TextBoxes.Select(t =>
                    DialogUtils.ReplacePlaceholders(t.Description, dialog.ReplaceStrings, dialog.ReplaceNumbers)).ToList();
            }

            var textBoxLabelFontSize = TextRendering.LegibleUiFontSize(Styles.BaseFontSize);
            var labelSize = (int)textBoxLabels.Max(l => TextRendering.Measure(
                host.ActiveInterface.Look.DefaultFont, l, textBoxLabelFontSize, 1.0f).X) +
                (isNameDialog ? 30 : 24);

            for (int i = 0; i < dialog.TextBoxes.Count; i++)
            {
                var labeledBox = new LabeledTextBox(this, dialog.TextBoxes[i], textBoxLabels[i], labelSize);
                labeledBox.Width = labeledBox.GetPreferredWidth();
                innerLayout.Add(labeledBox, layoutRow++, 1, isNameDialog ? new Padding(18, 5, 18, 5) : new Padding(2, 2, 2, 2));
                _textBoxes.Add(labeledBox);
            }
        }

        if (dialog.Options is { Texts: not null })
        {
            dialog.Options.ReplacedTexts = [];
            for (int i = 0; i < dialog.Options.Texts.Count; i++)
            {
                dialog.Options.ReplacedTexts.Add(DialogUtils.ReplacePlaceholders(dialog.Options.Texts[i], dialog.ReplaceStrings, dialog.ReplaceNumbers));
            }
            _optionsPanel = new OptionsPanel(this, dialog.Options);

            innerLayout.Add(_optionsPanel, layoutRow++, 1);
        }

        _innerPanel = new TableLayoutPanel(this)
        {
            Location = new Vector2(LayoutPadding.Left, LayoutPadding.Top),
            TableLayout = innerLayout
        };
        Controls.Add(_innerPanel);

        var menuBar = new ControlGroup(this);
        foreach (var button in dialog.Button)
        {
            var actionButton = new Button(this, button);

            actionButton.Click += OnActionButtonOnClick;
            if (dialog.Name == "MAINMENU" && button == Labels.Cancel)
            {
                actionButton.MouseDown += OnActionButtonOnClick;
            }
            menuBar.AddChild(actionButton);
        }

        Controls.Add(menuBar);
        SetButtons(menuBar);

        // Determine which control is focused at game start
        if (_optionsPanel != null)
        {
            Focused = _optionsPanel;
        }
        else if (_listbox != null)
        {
            Focused = _listbox;
        }
    }

    public override void OnKeyPress(KeyboardKey key)
    {
        switch (key)
        {
            case KeyboardKey.Enter or KeyboardKey.KpEnter when ButtonExists(Labels.Ok):
                CloseDialog(Labels.Ok);
                return;
            case KeyboardKey.Escape when ButtonExists(Labels.Cancel):
                CloseDialog(Labels.Cancel);
                return;
        }

        base.OnKeyPress(key);
    }

    private void OnActionButtonOnClick(object? sender, MouseEventArgs mouseEventArgs)
    {
        if (sender is not Button button) return;

        CloseDialog(button.Text);
    }

    private void ListboxOnItemSelected(object? sender, ListboxSelectionEventArgs e)
    {
        _selectedIndex = e.Index;
    }

    private void CloseDialog(string buttonText)
    {
        if (_closed)
        {
            return;
        }

        _closed = true;
        if (_optionsPanel != null)
        {
            _selectedIndex = _optionsPanel?.SelectedId ?? -1;
        }
        _handleButtonClick(buttonText, _selectedIndex, _optionsPanel?.CheckboxStates ?? [], FormatTextBoxReturn());
    }

    private IDictionary<string, string>? FormatTextBoxReturn()
    {
        return _textBoxes?.Select(box => new { box.Name, Value = box.Text })
            .ToDictionary(k => k.Name, v => v.Value);
    }

    private int GetInnerPanelWidthFromText(IList<LabelControl> labels, int popupboxWidth)
    {
        var centredTextMaxWidth = 0.0;
        var _labels = labels.Where(l => l != null).ToList();
        if (_labels.Count != 0)
            centredTextMaxWidth = (from label in _labels
                                   orderby label.TextSize.X descending
                                   select label).ToList().FirstOrDefault().TextSize.X;

        if (popupboxWidth != 0)
            return (int)Math.Ceiling(Math.Max(centredTextMaxWidth, 1.5 * popupboxWidth));
        else
            return (int)Math.Ceiling(Math.Max(centredTextMaxWidth, _active.DefaultDialogWidth));    // 660=440*1.5
    }
}
