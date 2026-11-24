using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Fonts;
using Raylib_CSharp.Rendering;
using RhyCiv.Engine.Notifications;

namespace RaylibUI.RunGame.GameControls.Notifications;

/// <summary>
/// A stack of short, self-dismissing notifications drawn into a corner of the map.
/// <para>
/// This is the display half of the Civ2-UI-Additions row 33 "suppress simple popups
/// into a map overlay" feature. It deliberately owns no input and touches no game
/// state: it never grabs focus, never pauses the turn and never modifies a unit, city
/// or the AI. Each entry fades itself out after a fixed lifetime, so the player never
/// has to click anything to get rid of one -- the whole point is that a piece of news
/// that used to stop the game now just appears on the map and leaves.
/// </para>
/// <para>
/// The decision of which events belong here (as opposed to a real modal popup) lives
/// in <see cref="MapOverlayNotificationPolicy"/> in the engine, so it can be tested
/// without a window. This class only draws whatever the caller has already decided
/// belongs on the overlay.
/// </para>
/// </summary>
public sealed class MapOverlayNotifications
{
    /// <summary>How long a notification is fully on screen before it starts fading.</summary>
    private const int LifetimeMs = 5000;

    /// <summary>How long the trailing fade takes.</summary>
    private const int FadeMs = 900;

    /// <summary>Most recent notifications kept on screen at once; older ones are dropped.</summary>
    private const int MaxStack = 5;

    private const int FontSize = 18;
    private const int Padding = 10;
    private const int Margin = 16;
    private const int Gap = 6;

    private readonly List<Entry> _entries = new();

    private readonly struct Entry
    {
        public Entry(MapOverlayNotification notification, long shownAt)
        {
            Notification = notification;
            ShownAt = shownAt;
        }

        public MapOverlayNotification Notification { get; }
        public long ShownAt { get; }
    }

    /// <summary>
    /// Puts one already-classified notification onto the stack. It does not block,
    /// pause or return anything an answer could wait on: the caller has already decided
    /// the event is simple, so nothing is coming back.
    /// </summary>
    public void Enqueue(MapOverlayNotification notification)
    {
        if (string.IsNullOrWhiteSpace(notification.Text))
        {
            return;
        }

        _entries.Add(new Entry(notification, Now()));

        // Keep the stack bounded. If more arrive than fit, the oldest scroll off
        // rather than the newest being refused -- the news the player just earned is
        // the news they should see.
        while (_entries.Count > MaxStack)
        {
            _entries.RemoveAt(0);
        }
    }

    /// <summary>
    /// Draws the stack into the top-right corner of the map region and fades out the
    /// entries whose lifetime has run. It reads nothing but the font and the map
    /// rectangle it is handed, so it never needs a window to build against and never
    /// inspects game state.
    /// </summary>
    public void Draw(Font font, int mapLeft, int mapTop, int mapWidth, int mapHeight)
    {
        var now = Now();
        _entries.RemoveAll(entry => now - entry.ShownAt >= LifetimeMs);
        if (_entries.Count == 0)
        {
            return;
        }

        var y = mapTop + Margin;

        foreach (var entry in _entries)
        {
            var age = now - entry.ShownAt;

            // Full opacity through the lifetime, then a linear fade for the last
            // stretch before it is removed. Self-dismissing: the fade is driven by the
            // clock, not by any player action.
            var alpha = age > LifetimeMs - FadeMs
                ? (int)(255f * (LifetimeMs - age) / FadeMs)
                : 255;
            alpha = Math.Clamp(alpha, 0, 255);

            var text = entry.Notification.Text;
            var measured = TextRendering.Measure(font, text, FontSize, 1);
            var boxWidth = (int)measured.X + Padding * 2;
            var boxHeight = (int)measured.Y + Padding * 2;
            var x = mapLeft + mapWidth - Margin - boxWidth;

            Graphics.DrawRectangle(x, y, boxWidth, boxHeight, new Color(12, 12, 12, (byte)alpha));
            Graphics.DrawRectangleLines(x, y, boxWidth, boxHeight, new Color(60, 60, 60, (byte)alpha));
            TextRendering.Draw(font, text, new Vector2(x + Padding, y + Padding), FontSize, 1,
                new Color(240, 240, 240, (byte)alpha));

            y += boxHeight + Gap;
        }
    }

    private static long Now() => Environment.TickCount64;
}
