using System;
using System.Collections.Generic;

namespace RhyCiv.Engine.Notifications
{
    /// <summary>
    /// Where an in-game event's message is shown: the existing blocking modal popup,
    /// or the new display-only corner overlay. Civ2-UI-Additions row 33 wants the
    /// "simple" informational events (an event that only tells the player something
    /// happened, and which the player never has to answer) shown on the map instead
    /// of interrupting play with a modal dialog, while the events that genuinely need
    /// an answer keep the popup.
    /// </summary>
    public enum MapOverlayRoute
    {
        /// <summary>Keeps the existing modal popup (the old behaviour).</summary>
        Modal,

        /// <summary>Goes to the non-blocking map overlay instead of a popup.</summary>
        Overlay
    }

    /// <summary>
    /// A single notification the map overlay will render: the event that produced it
    /// and the line of text to show. It carries no answer and no input, because a
    /// notification is display-only -- that is the whole point of suppressing the
    /// popup rather than dressing it up differently.
    /// </summary>
    public sealed record MapOverlayNotification(string EventKind, string Text);

    /// <summary>
    /// The classification shared by the client and the tests. It lives in the engine,
    /// not the RaylibUI client, so the decision can be asserted without a window and
    /// read by the client that actually draws the result.
    /// </summary>
    public static class MapOverlayNotificationPolicy
    {
        /// <summary>
        /// The event kinds that are "simple": the city announcements the player reads
        /// as information and does not reply to. Every one of them is already a modal
        /// city dialog today (the City Report Options messages), which is exactly what
        /// row 33 asks to move onto the map. Everything not listed here -- the warnings
        /// and the questions -- is treated as blocking and keeps its popup.
        /// </summary>
        private static readonly HashSet<string> SimpleEvents = new(StringComparer.Ordinal)
        {
            "DISORDER",
            "RESTORED",
            "WELOVEKING",
            "WEDONTLOVEKING"
        };

        /// <summary>Whether this event kind counts as a simple informational event.</summary>
        public static bool IsSimpleEvent(string eventKind) => SimpleEvents.Contains(eventKind);

        /// <summary>
        /// Where this event goes. With the overlay turned off (the advanced setting)
        /// every event falls back to the popup, which is the old behaviour. With it on,
        /// only the simple events move to the overlay and everything else stays modal.
        /// </summary>
        public static MapOverlayRoute Classify(string eventKind, bool overlayEnabled) =>
            overlayEnabled && IsSimpleEvent(eventKind)
                ? MapOverlayRoute.Overlay
                : MapOverlayRoute.Modal;

        /// <summary>
        /// Builds the notification to queue when the event is routed to the overlay,
        /// and returns null when it is not -- meaning the caller keeps the popup. A null
        /// return is the caller's signal that this is still a blocking modal event.
        /// </summary>
        public static MapOverlayNotification? TryQueueNotification(string eventKind, string text, bool overlayEnabled) =>
            Classify(eventKind, overlayEnabled) == MapOverlayRoute.Overlay
                ? new MapOverlayNotification(eventKind, text)
                : null;
    }
}
