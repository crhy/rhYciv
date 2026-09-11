using RhyCiv.Engine;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Transformations;
using RaylibUI.BasicTypes;
using RaylibUI.BasicTypes.Controls;
using RaylibUI.Controls;
using System.Diagnostics;
using System.Numerics;

namespace RaylibUI;

public abstract class BaseControl : IControl
{
    private Vector2 _location;
    private bool _clickPossible;
    private bool _clickStart;
    private MouseButton _clickButton;
    private bool _visible;
    protected IControlLayout Controller { get; }
    
    public bool EventTransparent { get; }

    protected BaseControl(IControlLayout controller, bool eventTransparent = false)
    {
        Controller = controller;
        EventTransparent = eventTransparent;
        Visible = true;
    }

    public Vector2 Location
    {
        get => _location;
        set
        {
            _location = value;
        }
    }

    public virtual int Width { get; set; }

    public virtual int Height { get; set; }

    public Rectangle Bounds => new(
        Parent.Bounds.X + Location.X, 
        Parent.Bounds.Y + Location.Y,
        Width,
        Height);

    public bool Visible
    {
        get 
        {
            if (Location.X + Width <= 0 ||
                Location.X > Parent.Bounds.Width ||
                Location.Y + Height <= 0 ||
                Location.Y > Parent.Bounds.Height)
            {
                return false; 
            }
            else if (!Parent.Visible)
            {
                return false;
            }
            else
            {
                return _visible;
            }
            
        }
        set => _visible = value;
    }

    public virtual bool CanFocus => false;
    public IList<IControl> Controls { get; protected set; } = [];

    private IComponent? _parent;
    public IComponent Parent
    {
        get
        {
            if (_parent != null)
            {
                return _parent;
            }

            var parent = SearchForParent(Controller);
            if (parent != null)
            {
                _parent = parent;
                return parent;
            }

            // Do not cache this fallback. Many controls are sized before they are
            // inserted into their real parent; caching Controller here makes their
            // bounds permanently relative to the wrong window and lets large art
            // escape city/civilopedia panels.
            return Controller;
        }
    }

    /// <summary>
    /// Search for this control's parent control.
    /// </summary>
    private IComponent? SearchForParent(IComponent parent)
    {
        foreach (IControl c in parent.Controls ?? [])
        {
            if (c == this)
            {
                return parent;
            }
            else
            {
                var candidate = SearchForParent(c);
                if (candidate != null)
                    return candidate;
            }
        }

        return null;
    }


    public virtual bool OnKeyPressed(KeyboardKey key)
    {
        return false;
    }

    public virtual bool OnCharPressed(Char charPressed)
    {
        return false;
    }

    public virtual bool OnMouseWheel(float amount)
    {
        return false;
    }

    /// <summary>
    /// Hands back whatever this control painted for itself, and asks its children
    /// to do the same. A control that owns no texture of its own needs only the
    /// walk, which is what this does.
    /// </summary>
    public virtual void ReleaseTextures()
    {
        if (Controls == null)
        {
            return;
        }

        foreach (var child in Controls)
        {
            child.ReleaseTextures();
        }
    }

    public virtual void OnMouseMove(Vector2 moveAmount)
    {
        if (!Visible) return;

        if (_clickStart)
        {
            if (!Input.IsMouseButtonDown(_clickButton))
            {
                _clickStart = false;
                Click?.Invoke(this, new MouseEventArgs { Button = _clickButton});
            }   
        }
        // Whether a click can begin here is decided every frame, not once on the
        // way in. It used to be settled by OnMouseEnter alone, and entering with a
        // button already down -- which is what happens on the very click that
        // brings the window back to the front, or at the end of a drag begun
        // somewhere else -- left it false with no way back: nothing re-armed it
        // until the pointer left the control and returned. On the map, which is one
        // control filling most of the window, that meant clicking away to another
        // window and clicking back stopped units being selectable at all until the
        // player happened to sweep the pointer over the menu bar and back.
        //
        // A click is possible again as soon as every button is up over this
        // control. A press still in progress is left alone, so releasing a drag
        // that started elsewhere is not mistaken for a click here.
        if (!_clickStart && !Input.IsMouseButtonDown(MouseButton.Left) &&
            !Input.IsMouseButtonDown(MouseButton.Right))
        {
            _clickPossible = true;
        }

        if (_clickPossible)
        {
            _clickStart = Input.IsMouseButtonDown(MouseButton.Left);
            if (_clickStart)
            {
                _clickButton = MouseButton.Left;
                MouseDown?.Invoke(this, new MouseEventArgs { Button = _clickButton});
            }
            else
            {
                _clickStart = Input.IsMouseButtonDown(MouseButton.Right);
                if (_clickStart)
                {
                    _clickButton = MouseButton.Right;
                    MouseDown?.Invoke(this, new MouseEventArgs { Button = _clickButton});
                }
            }
        }
    }

    public virtual void OnMouseLeave()
    {
        if (!Visible) return;

        _clickPossible = false;
        _clickStart = false;
    }

    public virtual void OnMouseEnter()
    {
        if (!Visible) return;

        _clickPossible = !Input.IsMouseButtonDown(MouseButton.Left) && !Input.IsMouseButtonDown(MouseButton.Right);

        _clickStart = false;
    }

    public virtual void OnFocus()
    {
        
    }

    public virtual void OnBlur()
    {
    }

    public virtual void OnResize()
    {
    }

    public event EventHandler<MouseEventArgs>? MouseDown;

    public event EventHandler<MouseEventArgs>? Click;

    public virtual void Draw(bool pulse)
    {
        if (!Visible) return;

        // This is used for debugging layout issues by drawing a box around the controls we can see where they think they are suppose to be and which is in the wrong place
        //Graphics.DrawRectangleLinesEx(Bounds, 1f, Color.Magenta);

        foreach (var control in Controls ?? Enumerable.Empty<IControl>())
        {
            control.Draw(pulse);
        }
    }

    public virtual int GetPreferredWidth()
    {
        return -1;
    }

    public virtual int GetPreferredHeight()
    {
        return -1;
    }

    protected Vector2 GetRelativeMousePosition()
    {
        return Input.GetMousePosition() - new Vector2(Bounds.X, Bounds.Y);
    }
}

public class MouseEventArgs
{
    public MouseButton Button { get; set; }
}
