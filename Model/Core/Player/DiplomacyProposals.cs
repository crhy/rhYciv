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
    /// The mutual trade, as distinct from the one-way gift above: both courts take
    /// what the other knows. Civ II's parley offers both, and names them differently
    /// -- "wish to offer you a gift" and "ask to exchange knowledge".
    /// </summary>
    public const string ExchangeKnowledge = "EXCHTECHNOLOGY";
}
