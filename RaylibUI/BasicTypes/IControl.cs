using System.Net;
using System.Numerics;
using Model;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Transformations;
using RaylibUI.BasicTypes;

namespace RaylibUI;

public interface IControl : IComponent
{
    int Width { get; set; }
    
    int Height { get; set; }

    /// <summary>
    /// Bounds of control relative to game window.
    /// </summary>
    new Rectangle Bounds { get; }

    bool CanFocus { get; }
    new IList<IControl>? Controls { get; }

    IComponent Parent { get; }

    /**
     * Accepts a keystroke. Keystrokes are used for shortcuts and keyboard-based navigation.
     * Keep in mind that keystrokes are not the same as chars; for example LEFT_ARROW is not
     * associated with any char, and KeyboardKey.S may be used for entering lowercase 's' or uppercase 'S'.
     */
    bool OnKeyPressed(KeyboardKey key);
    /**
     * Accepts a UTF-16 character.
     * Keep in mind that keystrokes are not the same as chars; for example LEFT_ARROW is not
     * associated with any char, and KeyboardKey.S may be used for entering lowercase 's' or uppercase 'S'.
     * This is used by textboxes; most Controls should hook into OnKeyPressed rather than OnCharPressed.
     */
    bool OnCharPressed(char charPressed);
    /// <summary>
    /// Releases any GPU texture this control painted for itself.
    /// </summary>
    /// <remarks>
    /// A raylib texture is a GPU allocation with nothing to free it when the
    /// managed object is collected, so a control that paints its own background
    /// or button face has to be told when it is finished with. Nothing told them,
    /// and a session leaked every texture every dialog had ever painted: one
    /// afternoon's play left almost four thousand textures and about half a
    /// gigabyte of video memory held by windows that had been closed long before.
    /// Eventually the driver refuses, and refusing kills the process outright --
    /// which is the "hard crash" with no managed exception and nothing on stderr.
    /// </remarks>
    void ReleaseTextures();

    bool OnMouseWheel(float amount);
    void OnMouseMove(Vector2 moveAmount);
    void OnMouseLeave();
    void OnMouseEnter();

    void OnFocus();

    void OnBlur();
    void Draw(bool pulse);

    int GetPreferredWidth();

    int GetPreferredHeight();
    void OnResize();
    
    bool EventTransparent { get; }
    new bool Visible { get; set; }
}
