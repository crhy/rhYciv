using RhyCiv.Engine.IO;
using Model.Core;

namespace RhyCiv.UI.Classic.Dialogs;

/// <summary>
/// Clean-room dialog copy used by the standalone ruleset.  Compatibility
/// rulesets may still override any entry through their own GAME.txt file.
/// </summary>
internal static class BuiltInDialogs
{
    public static void AddFallbacks(Dictionary<string, PopupBox> dialogs)
    {
        Add(dialogs, "MAINMENU", "rhYciv", ["New game", "Load map", "Custom world", "Load scenario", "Load game", "Quick start"], ["OK", "Exit"]);
        Add(dialogs, "SIZEOFMAP", "World size", ["Small", "Medium", "Large"], ["Custom", "OK", "Cancel"]);
        Add(dialogs, "CUSTOMSIZE", "Custom world size", ["Width", "Height"], ["OK", "Cancel"]);
        Add(dialogs, "DIFFICULTY", "Difficulty", ["Chieftain", "Warlord", "Prince", "King", "Emperor", "Deity"], ["OK", "Cancel"]);
        Add(dialogs, "ENEMIES", "Rival civilizations", ["8 Civilizations"], ["OK", "Cancel"]);
        Add(dialogs, "BARBARITY", "Barbarian activity", ["Villages only", "Roving bands", "Restless tribes", "Raging hordes"], ["OK", "Cancel"]);
        Add(dialogs, "RULES", "Game rules", ["Standard conquest", "Advanced options"], ["OK", "Cancel"]);
        Add(dialogs, "ADVANCED", "Advanced rules",
            ["Simplified combat", "Flat world", "Choose computer rivals", "Accelerated start", "Conquest-only victory", "Permanent elimination"],
            ["OK", "Cancel"], checkbox: true);
        Add(dialogs, "ACCELERATED", "Starting era", ["4000 BC", "3000 BC", "2000 BC"], ["OK", "Cancel"]);
        Add(dialogs, "GENDER", "Leader", ["Male", "Female"], ["OK", "Cancel"]);
        Add(dialogs, "TRIBE", "Choose a civilization", [], ["OK", "Custom", "Cancel"]);
        Add(dialogs, "NAME", "Name your leader", ["Leader name"], ["OK", "Cancel"]);
        Add(dialogs, "CUSTOMTRIBE", "Customize civilization", ["Leader", "Civilization", "Adjective"], ["OK", "Titles", "Cancel"]);
        Add(dialogs, "CUSTOMTRIBE2", "Government titles", [], ["OK", "Cancel"]);
        Add(dialogs, "CUSTOMCITY", "City style", [], ["OK", "Cancel"]);
        Add(dialogs, "OPPONENT", "Choose a rival", ["Random civilization"], ["OK", "Cancel"]);
        Add(dialogs, "INIT", "The first turn", [], ["Begin"]);

        AddRandom(dialogs, "CUSTOMLAND", "Land coverage", ["Sparse", "Balanced", "Abundant"]);
        AddRandom(dialogs, "CUSTOMFORM", "Land form", ["Archipelagos", "Continents", "Pangaea"]);
        AddRandom(dialogs, "CUSTOMCLIMATE", "Climate", ["Dry", "Temperate", "Wet"]);
        AddRandom(dialogs, "CUSTOMTEMP", "Temperature", ["Cool", "Temperate", "Warm"]);
        AddRandom(dialogs, "CUSTOMAGE", "World age", ["Young", "Mature", "Old"]);
        Add(dialogs, "USESEED", "Map resources", ["Randomize resources", "Keep map resources"], ["OK", "Cancel"]);
        Add(dialogs, "USESTARTLOC", "Starting locations", ["Randomize starts", "Keep map starts"], ["OK", "Cancel"]);
        Add(dialogs, "FAILEDTOLOAD", "Could not load map", [], ["OK"]);
        AddMessage(dialogs, "FAILEDTOLOADGAME", "Could not load game",
            ["%STRING0 could not be loaded.", "", "%STRING1"]);
        Add(dialogs, "LOADOK", "Game loaded", [], ["Continue"]);
        Add(dialogs, "SCENCHOSECIV", "Choose a civilization", [], ["OK", "Cancel"]);
        Add(dialogs, "SCENINTRO", "Scenario", [], ["Continue"]);
        Add(dialogs, "SCENCUSTOMINTRO", "Scenario", [], ["OK", "Cancel"]);
        Add(dialogs, "SCENARIOLOADED", "Scenario loaded", [], ["Continue"]);

        // Diplomats. GAME.TXT has no wording for these because the original game
        // asked them through its own dialog resources, so the fallbacks carry the
        // whole text.
        Add(dialogs, "DIPLOMATACTION", "Diplomatic mission",
            ["Incite a revolt in the city", "Bribe the unit"], ["OK", "Cancel"]);
        AddMessage(dialogs, "BRIBEUNIT", "Bribe unit",
            ["The %STRING0 commander will change sides for %NUMBER0 gold.",
             "You have %NUMBER1 gold.", "", "Pay?"], ["OK", "Cancel"]);
        AddMessage(dialogs, "INCITEREVOLT", "Incite revolt",
            ["The citizens of %STRING0 will rise against %STRING1 for %NUMBER0 gold,",
             "and the city and its garrison will be yours.",
             "You have %NUMBER1 gold.", "", "Pay?"], ["Incite Revolt!", "Save My Money!"]);
        AddMessage(dialogs, "NODIPLOMATGOLD", "Not enough gold",
            ["This will cost %NUMBER0 gold and your treasury holds %NUMBER1."]);
        AddMessage(dialogs, "CANNOTINCITE", "The capital will not be bought",
            ["%STRING0 is the seat of government. No amount of gold will turn it;",
             "it has to be taken."]);
        AddMessage(dialogs, "CHANGEPRODUCTION", "Change production",
            ["Switching from %STRING0 to %STRING1 is a change of trade, and the",
             "work already done does not carry over in full.",
             "%NUMBER0 of the %NUMBER1 shields gathered will be lost.", "", "Change anyway?"],
            ["OK", "Cancel"]);
        AddMessage(dialogs, "CANNOTBRIBE", "Nothing to buy here",
            ["A single unit in the open can be bought. A garrison watching each",
             "other, or one inside a city, cannot."]);
    }

    public static PopupBox Generic(string name) => new()
    {
        Name = name,
        Width = 440,
        Title = name,
        Button = [Labels.Ok],
        Text = [],
        Options = []
    };

    /// <summary>
    /// A plain message box: body text and an OK button, with no options to choose
    /// between. The Add overload above always leaves Text empty, because the
    /// dialogs it builds get their wording from GAME.TXT.
    /// </summary>
    private static void AddMessage(Dictionary<string, PopupBox> dialogs, string name, string title,
        IList<string> text, IList<string>? buttons = null)
    {
        if (dialogs.ContainsKey(name)) return;
        dialogs[name] = new PopupBox
        {
            Name = name,
            Width = 440,
            Title = title,
            Button = buttons ?? [Labels.Ok],
            Options = [],
            Text = text
        };
    }

    private static void AddRandom(Dictionary<string, PopupBox> dialogs, string name, string title, IList<string> options) =>
        Add(dialogs, name, title, options, ["Random", "OK", "Cancel"]);

    private static void Add(Dictionary<string, PopupBox> dialogs, string name, string title,
        IList<string> options, IList<string> buttons, bool checkbox = false)
    {
        if (dialogs.ContainsKey(name)) return;
        dialogs[name] = new PopupBox
        {
            Name = name,
            Width = 440,
            Title = title,
            Button = buttons,
            Options = options,
            Checkbox = checkbox,
            Text = []
        };
    }
}
