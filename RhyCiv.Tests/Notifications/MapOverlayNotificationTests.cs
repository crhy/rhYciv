using System;
using System.IO;
using RhyCiv.Engine;
using RhyCiv.Engine.Notifications;

namespace RhyCiv.Tests.Notifications;

/// <summary>
/// Civ2-UI-Additions row 33: simple informational events are routed to a non-blocking
/// map-corner notification overlay, blocking events keep their modal popup, and the
/// whole thing sits behind an advanced setting that restores the old popups when off.
/// <para>
/// The decision under test lives in the engine's <see cref="MapOverlayNotificationPolicy"/>
/// and <see cref="Settings"/>, not the RaylibUI client, so these run headless: no window,
/// no display, no window server. The client (GameScreen/LocalPlayer) only draws what
/// this policy has already decided, and passes the advanced setting in as the bool.
/// </para>
/// </summary>
public class MapOverlayNotificationTests
{
    // The representative simple announcement the client now routes to the overlay, and
    // a blocking warning the client has always raised as a modal popup.
    private const string SimpleEvent = "RESTORED";
    private const string BlockingEvent = "INHOCK";

    [Fact]
    public void ASimpleEventIsRoutedToTheOverlayNotThePopup()
    {
        Assert.True(MapOverlayNotificationPolicy.IsSimpleEvent(SimpleEvent));
        Assert.Equal(MapOverlayRoute.Overlay, MapOverlayNotificationPolicy.Classify(SimpleEvent, overlayEnabled: true));

        // A queued notification, not a modal flag: the helper returns the notification
        // that goes to the overlay rather than signalling "keep the popup".
        var notification = MapOverlayNotificationPolicy.TryQueueNotification(
            SimpleEvent, "Rome: order restored.", overlayEnabled: true);
        Assert.NotNull(notification);
        Assert.Equal(SimpleEvent, notification!.EventKind);
        Assert.Equal("Rome: order restored.", notification.Text);
    }

    [Fact]
    public void ABlockingEventStillRoutesToThePopup()
    {
        Assert.False(MapOverlayNotificationPolicy.IsSimpleEvent(BlockingEvent));
        Assert.Equal(MapOverlayRoute.Modal, MapOverlayNotificationPolicy.Classify(BlockingEvent, overlayEnabled: true));

        // No notification means the caller keeps the existing modal popup.
        Assert.Null(MapOverlayNotificationPolicy.TryQueueNotification(
            BlockingEvent, "Rome cannot maintain the Wharf.", overlayEnabled: true));
    }

    [Fact]
    public void WhenTheToggleIsOffEveryEventRoutesToThePopup()
    {
        foreach (var simple in new[] { "DISORDER", "RESTORED", "WELOVEKING", "WEDONTLOVEKING" })
        {
            Assert.Equal(MapOverlayRoute.Modal,
                MapOverlayNotificationPolicy.Classify(simple, overlayEnabled: false));
            Assert.Null(MapOverlayNotificationPolicy.TryQueueNotification(simple, "news", overlayEnabled: false));
        }

        Assert.Equal(MapOverlayRoute.Modal, MapOverlayNotificationPolicy.Classify(BlockingEvent, overlayEnabled: false));
    }

    [Fact]
    public void TheOverlayIsOnByDefaultAndTheToggleRestoresTheOldPopups()
    {
        // Same isolation PollutionTests use: the toggle writes settings, so point the
        // data folder at a throwaway directory rather than the player's own.
        var folder = Path.Combine(Path.GetTempPath(), "rhyciv-overlay-tests-" + Guid.NewGuid().ToString("N"));
        Settings.DataFolder = () => folder;
        try
        {
            Assert.True(Settings.MapOverlayNotificationsEnabled, "the overlay is not on by default");

            Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
                mapOverlayNotifications: false);
            Assert.False(Settings.MapOverlayNotificationsEnabled);
            Assert.Equal(MapOverlayRoute.Modal,
                MapOverlayNotificationPolicy.Classify(SimpleEvent, Settings.MapOverlayNotificationsEnabled));

            Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
                mapOverlayNotifications: true);
            Assert.True(Settings.MapOverlayNotificationsEnabled);
            Assert.Equal(MapOverlayRoute.Overlay,
                MapOverlayNotificationPolicy.Classify(SimpleEvent, Settings.MapOverlayNotificationsEnabled));
        }
        finally
        {
            Settings.SetAdvancedSettings(globalWarming: false, cheatMenu: false, editorMenu: false,
                mapOverlayNotifications: true);
            Settings.DataFolder = () => Path.Combine(Path.GetTempPath(), "rhyciv-tests-unset");
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
