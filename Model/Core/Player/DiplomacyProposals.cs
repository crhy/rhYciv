namespace Model.Core.Player;

/// <summary>
/// The names of the things one civilisation can put to another. Shared between
/// the engine that acts on them and the interface that offers them.
/// </summary>
public static class DiplomacyProposals
{
    public const string CeaseFire = "CEASEFIRE";
    public const string Peace = "PEACE";
    public const string Alliance = "ALLIANCE";
    public const string GiveGold = "GIVEGOLD";
    public const string GiveTechnology = "GIVETECHNOLOGY";

    /// <summary>
    /// Both sides offer their picture of the world and take the other's. Civ II's
    /// "Have a proposal to make" list carries it as "ask to share world maps".
    /// </summary>
    public const string ShareWorldMaps = "SHAREWORLDMAPS";
}
