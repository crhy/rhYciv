using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.Production;
using Model;
using Model.Controls;
using Model.Core.Cities;
using Model.Core.Production;
using Model.Images;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;
using RaylibUI.BasicTypes.Controls;
using RaylibUtils;
using System.Numerics;

namespace RaylibUI.RunGame.GameControls.CityControls;

public class ProductionBox : BaseControl
{
    private readonly CityWindow _cityWindow;
    private readonly Texture2D _shieldIcon;
    private float _shieldScale = 1f;
    private readonly IUserInterface _active;
    private int _totalCost;
    private readonly int _shieldBoxRows;
    private readonly Color _pen1, _pen2;
    private readonly LabelControl _label;
    private readonly ShieldProduction _properties;
    private readonly CityButton _buyButton, _changeButton;
    private float _shieldWidth, _shieldHeight;
    private readonly City _city;
    private readonly IList<IProductionOrder> _canProduce;
    private readonly ImageBox _icon;
    private const float ShieldBoxTop = 42f;
    // The gap between the Buy and Change buttons. This was 34x28, which left the
    // item being built as a thumbnail barely larger than a shield.
    private const float ProductionIconSlotWidth = 48f;
    private const float ProductionIconSlotHeight = 40f;
    private const float BuyButtonX = 5f;
    private const float ChangeButtonX = 120f;
    private const float ProductionButtonY = 16f;
    private const float Padding = 5f;

    public ProductionBox(CityWindow cityWindow) : base(cityWindow, eventTransparent: false)
    {
        _pen1 = new Color(83, 103, 191, 255);
        _pen2 = new Color(0, 0, 95, 255);
        _shieldBoxRows = cityWindow.CurrentGameScreen.Game.Rules.Cosmic.RowsShieldBox;
        _cityWindow = cityWindow;
        _city = cityWindow.City;
        _canProduce = ProductionPossibilities.GetAllowedProductionOrders(_city);
        _active = cityWindow.MainWindow.ActiveInterface;
        _properties = _cityWindow.CityWindowProps.Production;
        _shieldIcon = TextureCache.GetImage(_active.ResourceImages
            .First(r => r.Name == "Shields")
            .LargeImage);

        _label = new CityLabel(_cityWindow, _cityWindow.CityWindowProps.Labels["ItemInProduction"]);

        _buyButton = new CityButton(cityWindow, "Buy") { ManualLayout = true };
        _buyButton.Click += (_, _) => OfferToBuy();
        _changeButton = new CityButton(cityWindow, "Change") { ManualLayout = true };
        _changeButton.Click += (_, _) => ShowChangeProductionDialog();

        _icon = new ImageBox(_cityWindow, _city.ItemInProduction.GetIcon(_active));

        // Do not draw the production icon through ImageBox. The icon source can
        // be redirected by art-replacement code, and ImageBox parent discovery has
        // repeatedly allowed 1024px FOSS unit art to escape the production panel.
        // ProductionBox draws the icon itself into a fixed slot below.
        _icon.Visible = false;
        Controls = [_label, _buyButton, _changeButton];

        // CityWindow inserts this control into the tree after the constructor.
        // Do not call OnResize here; child controls can resolve the CityWindow
        // as their parent before this ProductionBox is discoverable, which makes
        // production icons/buttons draw in the wrong coordinate space.
        UpdateData(GetDisplayedOrder());
    }

    public override void OnResize()
    {
        Location = new(_cityWindow.LayoutPadding.Left + _properties.Box.X * _cityWindow.Scale,
            _cityWindow.LayoutPadding.Top + _properties.Box.Y * _cityWindow.Scale);
        Width = (int)(_properties.Box.Width * _cityWindow.Scale);
        Height = (int)(_properties.Box.Height * _cityWindow.Scale);
        base.OnResize();

        _shieldScale = ResourceIconScale.ToHeight(_shieldIcon,
            ResourceIconScale.LargeLogicalSize * _cityWindow.Scale);
        _shieldWidth = _shieldIcon.Width * _shieldScale;
        _shieldHeight = _shieldIcon.Height * _shieldScale;

        var activeOrder = GetDisplayedOrder();
        _label.Visible = true;
        _label.Text = activeOrder.Title;

        foreach (var child in Controls)
        {
            child.OnResize();
        }

        LayoutProductionHeader(activeOrder);
        LayoutActionButtons();
    }

    private void LayoutProductionHeader(IProductionOrder activeOrder)
    {
        var scale = _cityWindow.Scale;

        // Match the original Civ2 production header: unit builds show only the
        // small unit icon between Buy and Change; buildings/wonders can still
        // show their title in the small top label area.
        _label.Visible = activeOrder.Type != ItemType.Unit;
        if (_label.Visible)
        {
            _label.Text = activeOrder.Title;
            _label.OnResize();
        }

        var slotWidth = Math.Max(1, (int)Math.Round(ProductionIconSlotWidth * scale));
        var slotHeight = Math.Max(1, (int)Math.Round(ProductionIconSlotHeight * scale));
        FitIconInSlot(activeOrder.GetIcon(_active), slotWidth, slotHeight);

        // IconLocation is already relative to the production box in ClassicInterface.
        _icon.Location = new(
            _properties.IconLocation.X * scale - _icon.Width / 2f,
            Math.Max(0, _properties.IconLocation.Y * scale - _icon.Height / 2f));
    }

    private void FitIconInSlot(IImageSource? source, int slotWidth, int slotHeight)
    {
        _icon.Image = [source];
        _icon.FitIntoSlot(slotWidth, slotHeight, padding: Math.Max(1, (int)Math.Round(_cityWindow.Scale)), maxScale: Math.Min(_cityWindow.Scale, 1.25f));
    }

    private void LayoutActionButtons()
    {
        var scale = _cityWindow.Scale;
        var buyProps = _cityWindow.CityWindowProps.Buttons["Buy"];
        var changeProps = _cityWindow.CityWindowProps.Buttons["Change"];

        LayoutButton(_buyButton, BuyButtonX, ProductionButtonY, buyProps.Box.Width, buyProps.Box.Height, scale);
        LayoutButton(_changeButton, ChangeButtonX, ProductionButtonY, changeProps.Box.Width, changeProps.Box.Height, scale);

        var fontSize = Math.Max(10, (int)Math.Round(_active.Look.CityWindowFontSize * scale * 0.78f));
        _buyButton.FontSize = fontSize;
        _changeButton.FontSize = fontSize;
    }

    private static void LayoutButton(CityButton button, float x, float y, float width, float height, float scale)
    {
        button.Location = new(x * scale, y * scale);
        button.Width = Math.Max(1, (int)Math.Round(width * scale));
        button.Height = Math.Max(1, (int)Math.Round(height * scale));
    }

    private IProductionOrder GetDisplayedOrder()
    {
        return _city.ConstructionQueue.Current?.Order ?? _city.ItemInProduction;
    }

    private int GetDisplayedProgress()
    {
        // GameTurn still applies production to City.ShieldsProgress.  The newer
        // queue object describes what is being displayed, but it is not yet the
        // authoritative per-turn shield accumulator.  Draw the same accumulated
        // shields the production-completion code uses so the city box updates
        // immediately for normal production and disband contributions.
        return _city.ShieldsProgress;
    }


    /// <summary>
    /// Opens the Change Production list. Separate from the button handler so the
    /// review harness can bring it up without a click; it is the busiest dialog in
    /// the game and needs to be inspectable.
    /// </summary>
    public void ShowChangeProductionDialog()
    {
        _cityWindow.CurrentGameScreen.ShowPopup(
                "PRODUCTION", handleButtonClick: BuildDialogClosed, replaceStrings: [_city.Name], listBox: new ListboxDefinition
                {
                    ImageShift = false,
                    Rows = Math.Min(9, _canProduce.Count),
                    Looks = new ListboxLooks
                    {
                        Font = _active.Look.CityWindowFont,
                        FontSize = 16,
                        TextColorFront = Color.Black,
                        TextColorShadow = Color.Blank,
                        TextShadowOffset = Vector2.Zero,
                        SelectedTextFont = _active.Look.DefaultFont,
                        SelectedTextBackgroundColor = new Color(107, 107, 107, 255),
                        SelectedTextColorFront = Color.White,
                        SelectedTextColorShadow = Color.Black
                    },
                    Groups = _canProduce.Select(p => p.GetBuildListEntry(_active, _city, _shieldBoxRows)).ToList(),
                    SelectedId = _canProduce.IndexOf(_city.ItemInProduction)
                });
    }

    private void ChangeProductionDisplay()
    {
        var activeOrder = GetDisplayedOrder();
        _icon.Image = [activeOrder.GetIcon(_active)];

        OnResize();

        if (_properties.Type == "Box")
        {
            UpdateData(activeOrder);
        }
    }

    private void BuildDialogClosed(string button, int selectedIndex, IList<bool>? chx, IDictionary<string, string>? txt)
    {
        if (button != Labels.Ok || _canProduce.Count == 0)
        {
            return;
        }

        selectedIndex = Math.Clamp(selectedIndex, 0, _canProduce.Count - 1);
        var selectedOrder = _canProduce[selectedIndex];
        var previousOrder = _city.ItemInProduction;
        if (previousOrder == selectedOrder)
        {
            return;
        }

        // Switching between a unit, a building and a wonder forfeits a share of the
        // work already done. The engine has always charged it; nothing said so, and
        // the shields simply vanished.
        var rules = _cityWindow.CurrentGameScreen.Game.Rules;
        var penalty = _city.ProductionChangePenalty(selectedOrder, rules);
        if (penalty > 0)
        {
            _cityWindow.CurrentGameScreen.ShowPopup("CHANGEPRODUCTION",
                handleButtonClick: (confirm, _, _, _) =>
                {
                    if (confirm == Labels.Ok)
                    {
                        ApplyProductionChange(selectedOrder);
                    }
                },
                replaceNumbers: [penalty, Math.Max(0, _city.ShieldsProgress)],
                replaceStrings: [previousOrder?.Title ?? string.Empty, selectedOrder.Title]);
            return;
        }

        ApplyProductionChange(selectedOrder);
    }

    private void ApplyProductionChange(IProductionOrder selectedOrder)
    {
            // The engine charges the change: it knows the ruleset's penalty rate,
            // counts wonders as their own category, and only charges once a turn.
            // Halving here on ItemType alone made a switch to a wonder free and
            // charged again every time the player changed their mind.
            _city.ChangeProduction(selectedOrder, _cityWindow.CurrentGameScreen.Game.Rules);
            var retainedShields = Math.Max(0, _city.ShieldsProgress);

            _city.ConstructionQueue.Clear();
            _city.ConstructionQueue.Enqueue(selectedOrder, _shieldBoxRows);
            SynchronizeQueueProgress(selectedOrder, retainedShields);

            ChangeProductionDisplay();
    }

    private void SynchronizeQueueProgress(IProductionOrder selectedOrder, int retainedShields)
    {
        var current = _city.ConstructionQueue.Current;
        if (current == null)
        {
            return;
        }

        // The rules cost is the shield cost; multiplying by the shield box's rows
        // here left the queue believing everything cost ten times its price.
        current.RemainingCost = Math.Max(0, selectedOrder.Cost - retainedShields);
        current.Status = retainedShields > 0 ? ItemStatus.InProgress : ItemStatus.Queued;
    }

    public override void Draw(bool pulse)
    {
        var activeOrder = GetDisplayedOrder();
        var progressShields = Math.Min(GetDisplayedProgress(), _totalCost);
        DrawShieldProgress(activeOrder, progressShields);
        DrawProductionIcon(activeOrder);
        base.Draw(pulse);
    }

    private void DrawProductionIcon(IProductionOrder activeOrder)
    {
        var source = activeOrder.GetIcon(_active);
        if (source == null)
        {
            return;
        }

        var texture = TextureCache.GetImage(source);
        if (texture.Width <= 0 || texture.Height <= 0)
        {
            return;
        }

        var scale = _cityWindow.Scale;
        var slotWidth = ProductionIconSlotWidth * scale;
        var slotHeight = ProductionIconSlotHeight * scale;
        var drawScale = Math.Min(slotWidth / texture.Width, slotHeight / texture.Height);
        drawScale = Math.Max(0.01f, Math.Min(0.92f * scale, drawScale));

        var drawWidth = texture.Width * drawScale;
        var drawHeight = texture.Height * drawScale;
        var center = new Vector2(
            Bounds.X + _properties.IconLocation.X * scale,
            Bounds.Y + _properties.IconLocation.Y * scale);

        Graphics.DrawTexturePro(texture,
            new Rectangle(0, 0, texture.Width, texture.Height),
            new Rectangle(center.X - drawWidth / 2f, center.Y - drawHeight / 2f, drawWidth, drawHeight),
            Vector2.Zero, 0f, Color.White);
    }

    private void DrawShieldProgress(IProductionOrder activeOrder, int progressShields)
    {
        var scale = _cityWindow.Scale;
        var shieldTop = ShieldBoxTop * scale;
        var shieldBottom = Height - Padding * scale;
        var roomForRows = (int)((shieldBottom - shieldTop) / _shieldHeight);
        if (roomForRows <= 0)
        {
            return;
        }

        var posX = Bounds.X + Padding * scale;
        var drawWidth = Width - 2 * Padding * scale;
        var inset = 3 * scale;

        // The block stands for the whole cost, so an item's progress can be read
        // against what it will take. A wonder does not fit at the shields' natural
        // size and is drawn tighter, overlapping, rather than collapsing to a single
        // row of whatever happened to fit across the panel.
        var grid = ShieldBoxLayout.Fit(_totalCost,
            drawWidth - 2 * inset, shieldBottom - shieldTop - 2 * inset,
            _shieldWidth, _shieldHeight);
        var rows = grid.Rows;
        var perRow = grid.PerRow;

        var posY = Bounds.Y + shieldTop;
        Graphics.DrawLineEx(new Vector2(posX, posY), new Vector2(posX + drawWidth, posY), 1f, _pen1);
        var lineHeight = 6 * scale + rows * grid.StepY;
        posY = Bounds.Y + shieldTop + lineHeight;
        Graphics.DrawLineEx(new Vector2(posX, posY), new Vector2(posX + drawWidth, posY), 1f, _pen2);

        posY = Bounds.Y + shieldTop;
        Graphics.DrawLineEx(new Vector2(posX, posY), new Vector2(posX, posY + lineHeight), 1f, _pen1);
        Graphics.DrawLineEx(new Vector2(posX + drawWidth, posY), new Vector2(posX + drawWidth, posY + lineHeight), 1f, _pen2);

        // The block of shields is centred in the box at its natural size. It is a
        // count, and a count reads best as a block; stretching it to the panel's
        // width made ten shields into a sparse line.
        var blockWidth = (perRow - 1) * grid.StepX + _shieldWidth;
        var firstX = posX + (drawWidth - blockWidth) / 2f;

        var count = 0;
        for (var row = 0; row < rows && count < progressShields; row++)
        {
            for (var col = 0; col < perRow && count < progressShields; col++)
            {
                Graphics.DrawTextureEx(_shieldIcon,
                    new Vector2((int)(firstX + grid.StepX * col),
                        (int)(Bounds.Y + shieldTop + inset + grid.StepY * row)),
                    0f, _shieldScale, Color.White);
                count++;
            }
        }
    }

    /// <summary>
    /// Civ II's price for finishing the current item this turn: twice the shields
    /// still owed plus a quadratic term, doubled for a wonder, and doubled again
    /// when nothing has been put toward it yet.
    /// </summary>
    public static int BuyCost(IProductionOrder order, int shieldsProgress)
    {
        var remaining = Math.Max(0, order.Cost - shieldsProgress);
        if (remaining == 0)
        {
            return 0;
        }

        var cost = 2 * remaining + remaining * remaining / 20;
        if (order is BuildingProductionOrder { Improvement.IsWonder: true })
        {
            cost *= 2;
        }

        if (shieldsProgress <= 0)
        {
            cost *= 2;
        }

        return cost;
    }

    private void OfferToBuy()
    {
        var order = _city.ItemInProduction;
        var cost = BuyCost(order, _city.ShieldsProgress);
        if (cost <= 0)
        {
            return;
        }

        var treasury = _city.Owner.Money;
        if (cost > treasury)
        {
            _cityWindow.CurrentGameScreen.ShowPopup("NOBUYGOLD",
                replaceNumbers: [cost, treasury], replaceStrings: [order.Title]);
            return;
        }

        _cityWindow.CurrentGameScreen.ShowPopup("BUYCOST", handleButtonClick: (button, _, _, _) =>
        {
            if (button != Labels.Ok)
            {
                return;
            }

            // Re-check: the dialog is not modal to the rest of the turn.
            var priceNow = BuyCost(_city.ItemInProduction, _city.ShieldsProgress);
            if (priceNow <= 0 || priceNow > _city.Owner.Money)
            {
                return;
            }

            _city.Owner.Money -= priceNow;
            _city.ShieldsProgress = _city.ItemInProduction.Cost;
            _cityWindow.UpdateProduction();
            UpdateData(_city.ItemInProduction);
            OnResize();
        }, replaceNumbers: [cost, treasury], replaceStrings: [order.Title]);
    }

    public void UpdateData(IProductionOrder itemInProduction)
    {
        // The rules cost is the shield cost. It used to be multiplied by the shield
        // box's row count here, which drew a box ten times too big for the item.
        _totalCost = itemInProduction.Cost;
    }
}
