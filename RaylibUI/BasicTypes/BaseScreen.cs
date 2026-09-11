using Model;
using Raylib_CSharp;
using Raylib_CSharp.Collision;
using Raylib_CSharp.Interact;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.Controls;
using System.Diagnostics;
using System.Numerics;

namespace RaylibUI;

public abstract class BaseScreen : BaseLayoutController, IScreen
{
    public override void Draw(bool pulse)
    {
        var layoutController = _dialogs.LastOrDefault(this);
        var width = DisplayScale.Width;
        var height = DisplayScale.Height;

        if (_renderedWidth != width || _renderedHeight != height || DisplayScale.Changed)
        {
            _renderedWidth = width;
            _renderedHeight = height;
            Resize(width, height);
        }
        else
        {
            ControlEvents(layoutController);
        }

        foreach (var control in Controls.Where(c => c.Visible))
        {
            control.Draw(pulse);
        }

        foreach (var dialog in _dialogs)
        {
            dialog.Draw(pulse);
        }
    }

    public abstract void InterfaceChanged(Sound soundManager);

    public override void Resize(int width, int height)
    {
        foreach (var control in Controls)
        {
            control.OnResize();
        }

        foreach (var dialog in _dialogs)
        {
            dialog.Resize(width, height);
        }
    }

    public override void Move(Vector2 moveAmount)
    {
    }

    /// <summary>
    /// Most keys and characters taken from the window in one frame. raylib's own
    /// queues hold sixteen of each, so this empties them.
    /// </summary>
    private const int InputQueueLength = 16;

    private void ControlEvents(IControlLayout layoutController)
    {
        // Handle up to 16 characters per frame
        for (int i = 0; i < InputQueueLength; i++)
        {
            var charPressed = Convert.ToChar(Input.GetCharPressed());
            if (charPressed > char.MinValue)
            {
                layoutController.Focused?.OnCharPressed(charPressed);
            }
        }

        // Keys are taken from the window's queue of what was actually pressed since
        // the last frame, rather than by asking about every key on the keyboard in
        // turn. Asking was both slower and, more importantly, lossy: a key pressed
        // and let go inside a single frame is already back up by the time it is
        // asked about, so on any frame that ran long the press simply never
        // happened. That is precisely what a game feels like when Enter has to be
        // pressed several times before anything moves. The queue remembers presses
        // however briefly they were held and however long the frame took.
        for (int i = 0; i < InputQueueLength; i++)
        {
            var pressed = Input.GetKeyPressed();
            if (pressed == 0)
            {
                break;
            }

            var key = (KeyboardKey)pressed;
            if (layoutController.Focused == null || !layoutController.Focused.OnKeyPressed(key))
            {
                layoutController.OnKeyPress(key);
            }
        }

        var mousePos = Input.GetMousePosition();
        var control = layoutController.Hovered;
        if (control != null)
        {
            control.OnMouseMove(Input.GetMouseDelta());
            if (control.Controls != null)
            {
                var hoverChild = FindControl(control.Controls,
                    child => ShapeHelper.CheckCollisionPointRec(mousePos, child.Bounds) && child.Visible);
                if (hoverChild != null)
                {
                    control.OnMouseLeave();
                    layoutController.Hovered = hoverChild;
                    hoverChild.OnMouseEnter();
                }
            }
            if (!ShapeHelper.CheckCollisionPointRec(mousePos, control.Bounds))
            {
                control.OnMouseLeave();
                FindHovered(layoutController, mousePos);
            }
        }
        else
        {
            FindHovered(layoutController, mousePos);
        }

        if (layoutController.Hovered == null)
        {
            layoutController.MouseOutsideControls(mousePos);
        }

        var wheel = Input.GetMouseWheelMove();
        if (wheel != 0 && layoutController.Hovered != null)
        {
            IControl? wheelTarget = layoutController.Hovered;
            while (wheelTarget != null && !wheelTarget.OnMouseWheel(wheel))
            {
                wheelTarget = wheelTarget.Parent as IControl;
            }
        }
    }

    private static void FindHovered(IControlLayout layoutController, Vector2 mousePos)
    {
        layoutController.Hovered = FindControl(layoutController.Controls,
            control => ShapeHelper.CheckCollisionPointRec(mousePos, control.Bounds) && control.Visible);
        layoutController.Hovered?.OnMouseEnter();
    }

    /// <summary>
    /// Whether any window is open over the screen -- a message, a city, the
    /// Civilopedia. Used to decide whether the player is in the middle of something
    /// that a queued message should wait for.
    /// </summary>
    public bool HasOpenDialog => _dialogs.Count > 0;

    public void CloseDialog(IControlLayout? dialog)
    {
        if (dialog != null && _dialogs.Remove(dialog))
        {
            // The window is gone from the screen; its textures should go with it.
            dialog.ReleaseTextures();
        }
    }
    

    public void ShowDialog(IControlLayout dialog, bool stack = false)
    {
        if (!stack)
        {
            foreach (var replaced in _dialogs)
            {
                replaced.ReleaseTextures();
            }

            _dialogs.Clear();
        }

        _dialogs.Add(dialog);
        if (_renderedWidth > 0 && _renderedHeight > 0)
        {
            dialog.Resize(_renderedWidth, _renderedHeight);
        }
    }

    private readonly List<IControlLayout> _dialogs = new();
    
    private int _renderedWidth;
    private int _renderedHeight;

    protected BaseScreen(Main main) : base(main, Padding.None)
    {
    }
}
