using RhyCiv.Engine;
using RhyCiv.Engine.Diagnostics;

namespace RaylibUI.RunGame;

/// <summary>
/// The advice a new player is given as they meet each part of the game for the
/// first time.
/// <para>
/// Civ II offers this on the levels people learn on, and the option that governs
/// it -- Tutorial help, in Game Options -- was written into every save here and
/// consulted by nothing, because there was no tutorial to govern. A player who
/// had never seen the game was shown a flashing unit and left to work out that
/// the number pad moves it.
/// </para>
/// <para>
/// Each piece is shown once and then remembered, between games as well as within
/// one: advice given twice is a nuisance rather than help.
/// </para>
/// </summary>
public static class Tutorial
{
    /// <summary>How to move at all. The first thing anybody needs.</summary>
    public const string Movement = "FIRSTMOVE";

    /// <summary>What a Settlers unit is for.</summary>
    public const string FoundCity = "BUILDCITY";

    /// <summary>The city window, the first time a city exists to open.</summary>
    public const string FirstCity = "FIRSTPRODUCT";

    /// <summary>Research, the first time it is asked about.</summary>
    public const string Research = "TUTORIALRESEARCH";

    /// <summary>
    /// Offers one piece of advice, if the player is being given advice at all and
    /// has not had this piece before. Returns whether anything was shown.
    /// </summary>
    public static bool Offer(GameScreen gameScreen, string key)
    {
        if (!gameScreen.Game.Options.TutorialHelp || Settings.HasSeenTutorial(key))
        {
            return false;
        }

        // Marked before it is shown rather than after: the dialog may be queued
        // behind another, and the same piece of advice must not queue up twice.
        Settings.MarkTutorialSeen(key);
        SessionLog.Record($"tutorial: {key}");
        return gameScreen.ShowPopup(key);
    }
}
