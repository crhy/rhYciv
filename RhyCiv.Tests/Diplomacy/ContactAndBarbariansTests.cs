using RhyCiv.Engine.Diplomacy;
using RhyCiv.Tests.Mocks;
using RhyCiv.Tests.TestFiles;
using Model.Core;

namespace RhyCiv.Tests.Diplomacy;

/// <summary>
/// Who you have met, and who is not a party to any of it.
/// <para>
/// War could be arrived at without an introduction: <c>DeclareWar</c> set the
/// contact flag on both sides directly rather than going through
/// <see cref="DiplomacyFunctions.MakeContact"/>, so neither side was ever told.
/// The player then found themselves at war with a civilisation they had never
/// seen, and receiving cease-fire offers from it. Reported as "somebody offered a
/// Cease Fire, I never saw them" and "meeting a new Civ does not bring up the
/// negotiation dialogs".
/// </para>
/// <para>
/// And the barbarians were going through all of it — a horde falling on a city
/// announced itself as a declaration of war, which Civ II never does, because
/// there they are always at war with everyone and it is never stated.
/// </para>
/// </summary>
public class ContactAndBarbariansTests
{
    [Fact]
    public void BeingAttacked_CountsAsBeingIntroduced()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        var player = new MockPlayer(game.GetPlayerCiv);
        game.ConnectPlayer(player);
        var mine = game.GetPlayerCiv;
        var theirs = Stranger(game, mine);

        Assert.False(DiplomacyFunctions.HaveMet(mine, theirs));

        DiplomacyFunctions.DeclareWar(game, theirs, mine);

        Assert.True(DiplomacyFunctions.HaveMet(mine, theirs));
        Assert.True(DiplomacyFunctions.AtWar(mine, theirs));
    }

    [Fact]
    public void TheBarbariansDoNotDeclareWar()
    {
        var (game, _, _) = CleanRoomGameFactory.CreateGame();
        game.ConnectPlayer(new MockPlayer(game.GetPlayerCiv));
        var mine = game.GetPlayerCiv;
        var horde = game.AllCivilizations.FirstOrDefault(
            civ => civ.PlayerType == PlayerType.Barbarians);
        Assert.NotNull(horde);

        DiplomacyFunctions.DeclareWar(game, horde, mine);

        // No treaty state with the barbarians at all, in either direction, and
        // nothing for the player to be told about.
        Assert.False(DiplomacyFunctions.HaveMet(mine, horde));
        Assert.False(DiplomacyFunctions.AtWar(mine, horde));
    }

    private static Civilization Stranger(Model.Core.IGame game, Civilization mine) =>
        game.AllCivilizations.First(civ =>
            civ != mine && civ.Alive && civ.PlayerType != PlayerType.Barbarians &&
            !DiplomacyFunctions.HaveMet(mine, civ));
}
