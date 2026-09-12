using System;
using System.Collections.Generic;
using System.Linq;
using Model.Core;
using Model.Core.Player;
using Model.Core.Units;

namespace RhyCiv.Engine.Diplomacy;

/// <summary>
/// What one civilisation is to another: whether they have met, whether they are
/// at war, and what they think of each other.
/// <para>
/// The data for all of this was already carried in a save and shown to Lua
/// scripts -- contact, cease-fire, peace, alliance, embassy, vendetta,
/// reputation, attitude -- and not one of those flags was ever set or read by the
/// game itself. Civilisations met by walking into each other's units and there
/// was nothing to be done about it: no talking, no treaties, no way to stop a
/// war or to start one deliberately.
/// </para>
/// </summary>
public static class DiplomacyFunctions
{
    /// <summary>
    /// Where an attitude starts. Civ II's scale runs from hostility to worship;
    /// this is the middle of it, where a civilisation has no particular opinion.
    /// </summary>
    public const int NeutralAttitude = 50;

    /// <summary>How warmly a civilisation must feel to agree to each treaty.</summary>
    private const int CeaseFireAttitude = 25;
    private const int PeaceAttitude = 45;
    private const int AllianceAttitude = 75;

    /// <summary>
    /// The relation from one civilisation towards another, created on demand.
    /// Relations are held per civilisation rather than per pair, so both sides are
    /// updated together by everything in here.
    /// </summary>
    public static Relation Between(Civilization from, Civilization to)
    {
        if (from.Relations.Length <= to.Id)
        {
            var grown = new Relation?[to.Id + 1];
            Array.Copy(from.Relations, grown, from.Relations.Length);
            from.Relations = grown;
        }

        return from.Relations[to.Id] ??= new Relation();
    }

    public static bool HaveMet(Civilization a, Civilization b) => Between(a, b).Contact;

    public static bool AtWar(Civilization a, Civilization b) => Between(a, b).War;

    /// <summary>
    /// Whether something stands between these two: a cease-fire, a peace treaty or
    /// an alliance. What it means in play is that neither side's forces will attack
    /// the other of their own accord.
    /// </summary>
    public static bool UnderTreaty(Civilization a, Civilization b)
    {
        var relation = Between(a, b);
        return relation.CeaseFire || relation.Peace || relation.Alliance;
    }

    public static bool Allied(Civilization a, Civilization b) => Between(a, b).Alliance;

    public static bool HasEmbassyWith(Civilization observer, Civilization host) =>
        Between(observer, host).Embassy;

    /// <summary>
    /// What <paramref name="from"/> thinks of <paramref name="to"/>, on Civ II's
    /// hundred-point scale.
    /// </summary>
    public static int Attitude(Civilization from, Civilization to)
    {
        if (from.Attitude.Length <= to.Id)
        {
            var grown = new int[to.Id + 1];
            Array.Fill(grown, NeutralAttitude);
            Array.Copy(from.Attitude, grown, from.Attitude.Length);
            from.Attitude = grown;
        }

        // A civilisation that has never had an opinion recorded has no opinion.
        return from.Attitude[to.Id] == 0 ? NeutralAttitude : from.Attitude[to.Id];
    }

    /// <summary>
    /// What a civilisation's court thinks of another, in Civ II's own words.
    /// </summary>
    /// <remarks>
    /// Civ II scores attitude out of a hundred and reports it as one of nine
    /// ranks, best to worst: Worshipful, Enthusiastic, Cordial, Receptive,
    /// Neutral, Uncooperative, Icy, Hostile, Enraged. Its Foreign Minister prints
    /// the word beside each civilisation, and players read it the way they read a
    /// weather forecast -- Uncooperative in particular is the warning that an
    /// attack may be coming.
    ///
    /// This game had five words of its own invention, two of which ("friendly",
    /// "uneasy") do not appear in Civ II at all, so a player who knew the original
    /// could not read the state of a relationship from it.
    /// </remarks>
    public static string AttitudeName(Civilization from, Civilization to) => Attitude(from, to) switch
    {
        >= 89 => "Worshipful",
        >= 78 => "Enthusiastic",
        >= 67 => "Cordial",
        >= 56 => "Receptive",
        >= 45 => "Neutral",
        >= 34 => "Uncooperative",
        >= 23 => "Icy",
        >= 12 => "Hostile",
        _ => "Enraged"
    };

    /// <summary>
    /// The treaty in force between two civilisations, as the Foreign Minister
    /// names it.
    /// </summary>
    public static string StandingName(Civilization us, Civilization them) =>
        Between(us, them) switch
        {
            { Alliance: true } => "Alliance",
            { Peace: true } => "Peace",
            { CeaseFire: true } => "Cease-Fire",
            { War: true } => "War",
            _ => "No Treaty"
        };

    public static void AdjustAttitude(Civilization from, Civilization to, int change)
    {
        var attitude = Math.Clamp(Attitude(from, to) + change, 1, 100);
        from.Attitude[to.Id] = attitude;
    }

    /// <summary>
    /// Two civilisations have seen each other for the first time. Told to both, and
    /// only ever once: everything else in diplomacy needs somebody to have met.
    /// </summary>
    public static bool MakeContact(IGame game, Civilization a, Civilization b)
    {
        if (a == b || !a.Alive || !b.Alive || Between(a, b).Contact)
        {
            return false;
        }

        Between(a, b).Contact = true;
        Between(b, a).Contact = true;

        game.Players[a.Id].ContactMade(b);
        game.Players[b.Id].ContactMade(a);
        return true;
    }

    /// <summary>
    /// War, declared or fallen into. Any treaty between the two is torn up, and
    /// tearing one up is remembered: a civilisation that breaks a treaty is trusted
    /// less by everybody, which is what reputation is for.
    /// </summary>
    public static void DeclareWar(IGame game, Civilization aggressor, Civilization victim)
    {
        var betrayal = UnderTreaty(aggressor, victim);

        foreach (var (from, to) in new[] { (aggressor, victim), (victim, aggressor) })
        {
            var relation = Between(from, to);
            relation.Contact = true;
            relation.War = true;
            relation.CeaseFire = false;
            relation.Peace = false;
            relation.Alliance = false;
        }

        AdjustAttitude(victim, aggressor, -30);

        if (!betrayal)
        {
            return;
        }

        Between(victim, aggressor).Vendetta = true;
        aggressor.Betrayals += BlackMarksPerBetrayal;

        // Word gets round. Everybody who has met the aggressor thinks less of them
        // for breaking their word, which is what makes a treaty worth anything.
        foreach (var onlooker in game.AllCivilizations.Where(civ =>
                     civ != aggressor && civ.Alive && HaveMet(civ, aggressor)))
        {
            AdjustAttitude(onlooker, aggressor, -10);
        }
    }

    /// <summary>Stops the shooting without settling anything.</summary>
    public static void AgreeCeaseFire(Civilization a, Civilization b)
    {
        SetTreaty(a, b, ceaseFire: true, peace: false, alliance: false);
        AdjustAttitude(a, b, 5);
        AdjustAttitude(b, a, 5);
    }

    public static void AgreePeace(Civilization a, Civilization b)
    {
        SetTreaty(a, b, ceaseFire: false, peace: true, alliance: false);
        AdjustAttitude(a, b, 10);
        AdjustAttitude(b, a, 10);
    }

    public static void FormAlliance(Civilization a, Civilization b)
    {
        SetTreaty(a, b, ceaseFire: false, peace: true, alliance: true);
        AdjustAttitude(a, b, 15);
        AdjustAttitude(b, a, 15);
    }

    private static void SetTreaty(Civilization a, Civilization b, bool ceaseFire, bool peace, bool alliance)
    {
        foreach (var (from, to) in new[] { (a, b), (b, a) })
        {
            var relation = Between(from, to);
            relation.Contact = true;
            relation.War = false;
            relation.Vendetta = false;
            relation.CeaseFire = ceaseFire;
            relation.Peace = peace;
            relation.Alliance = alliance;
        }
    }

    /// <summary>A diplomat has opened an embassy, which is one-way.</summary>
    public static void EstablishEmbassy(Civilization observer, Civilization host)
    {
        Between(observer, host).Contact = true;
        Between(host, observer).Contact = true;
        Between(observer, host).Embassy = true;
    }

    /// <summary>The treaties one civilisation can offer another, given where they stand.</summary>
    public static IEnumerable<Proposal> AvailableProposals(Civilization from, Civilization to)
    {
        var relation = Between(from, to);

        if (relation.War)
        {
            yield return Proposal.CeaseFire;
        }
        else
        {
            if (!relation.Peace && !relation.Alliance)
            {
                yield return Proposal.Peace;
            }
            else if (!relation.Alliance)
            {
                yield return Proposal.Alliance;
            }

            yield return Proposal.DeclareWar;
        }

        yield return Proposal.GiveGold;
        yield return Proposal.GiveTechnology;
    }

    /// <summary>
    /// Black marks against a civilisation's name. Civ II gives two for every
    /// treaty broken and lets one fade every twenty-four turns times the
    /// difficulty, so a reputation is slow to lose and slower to rebuild.
    /// </summary>
    public static int BlackMarks(Civilization civ) => civ.Betrayals;

    /// <summary>Black marks earned by one treaty broken, as in Civ II.</summary>
    public const int BlackMarksPerBetrayal = 2;

    /// <summary>
    /// The point at which nobody expects a civilisation to keep its word. Civ II
    /// puts it plainly: against an atrocious reputation the computer players feel
    /// no obligation to honour cease-fires or treaties either.
    /// </summary>
    public const int AtrociousReputation = 8;

    /// <summary>What a reputation is called, for the diplomacy screen.</summary>
    public static string ReputationName(Civilization civ) => BlackMarks(civ) switch
    {
        0 => "spotless",
        <= 2 => "honourable",
        <= 4 => "questionable",
        <= 6 => "dishonourable",
        < AtrociousReputation => "poor",
        _ => "atrocious"
    };

    /// <summary>
    /// Lets one black mark fade. Civ II uses twenty-four turns times the difficulty
    /// level, so a treacherous reign is remembered for a very long time on the
    /// harder levels.
    /// </summary>
    public static void FadeReputations(IGame game)
    {
        var period = 24 * Math.Max(1, game.DifficultyLevel);
        if (game.TurnNumber <= 0 || game.TurnNumber % period != 0)
        {
            return;
        }

        foreach (var civ in game.AllCivilizations.Where(civ => civ.Betrayals > 0))
        {
            civ.Betrayals--;
        }
    }

    /// <summary>
    /// Whether a computer civilisation would tear up a treaty and attack.
    /// <para>
    /// Civ II's computer players are not bound by their treaties, and this is the
    /// single most-reported thing about playing against them. What governs it is
    /// not their leader's temperament but the balance of power and the standing of
    /// the civilisation opposite: no attack comes while their attitude is above
    /// neutral, and below it they turn on the weak, and on anyone whose economy has
    /// eclipsed theirs. A civilisation whose own word is worthless gets no
    /// protection from a treaty at all.
    /// </para>
    /// </summary>
    public static bool WouldBreakTreaty(IGame game, Civilization aggressor, Civilization victim)
    {
        if (!UnderTreaty(aggressor, victim))
        {
            return true;
        }

        if (aggressor.PlayerType == PlayerType.Barbarians)
        {
            // The barbarians sign nothing and honour nothing.
            return true;
        }

        // A civilisation holding warheads it has gone out of its way to build is
        // not waiting for a reason. This is Civ II's most famous accident and it is
        // kept deliberately: Gandhi's India, having built the bomb, will use it on
        // people it is at peace with.
        if (Ai.AiPersonality.WantsTheBomb(aggressor) && HasWarheads(aggressor))
        {
            return game.Random.Next(100) < NuclearImpatience;
        }

        // Nobody else moves against a civilisation they still think well of.
        var attitude = Attitude(aggressor, victim);
        if (attitude > NeutralAttitude)
        {
            return false;
        }

        var strength = RelativeStrength(aggressor, victim);

        // Two temptations, both of them Civ II's: a neighbour too weak to hold what
        // they have, and a neighbour who has pulled so far ahead that waiting only
        // makes it worse.
        var opportunity = strength >= WeakEnoughToAttack;
        var jealousy = strength <= FarEnoughAheadToFear;
        var worthless = BlackMarks(victim) >= AtrociousReputation;

        if (!opportunity && !jealousy && !worthless)
        {
            return false;
        }

        // How far below neutral they have sunk decides how likely it is in any
        // given turn, so a treaty with a cooling neighbour buys time rather than
        // safety.
        var chance = Math.Clamp(NeutralAttitude - attitude, 1, 50);
        if (worthless)
        {
            chance *= 2;
        }

        return game.Random.Next(100) < chance;
    }

    /// <summary>
    /// The chance per turn that a nuclear power with warheads in hand decides a
    /// treaty is no longer interesting.
    /// </summary>
    private const int NuclearImpatience = 40;

    private static bool HasWarheads(Civilization civ) =>
        civ.Units.Any(unit => !unit.Dead && Ai.AiPersonality.IsNuclearMissile(unit.TypeDefinition));

    /// <summary>
    /// How much stronger the aggressor must be before a weak neighbour becomes a
    /// target, and how far ahead a rival must get before they are feared instead.
    /// </summary>
    private const double WeakEnoughToAttack = 1.5;
    private const double FarEnoughAheadToFear = 0.6;

    /// <summary>
    /// Whether a computer civilisation agrees to what is being put to it.
    /// <para>
    /// It weighs how it feels about the proposer against how the war is going: a
    /// civilisation that is losing will take a cease-fire it would otherwise
    /// refuse, and one that is winning will not. An alliance is asked of a friend
    /// or not at all.
    /// </para>
    /// </summary>
    public static bool WouldAccept(IGame game, Civilization proposer, Civilization receiver, Proposal proposal)
    {
        var attitude = Attitude(receiver, proposer);
        var standing = RelativeStrength(receiver, proposer);

        return proposal switch
        {
            // Losing badly is itself an argument.
            Proposal.CeaseFire => attitude >= CeaseFireAttitude || standing < 0.6,
            Proposal.Peace => attitude >= PeaceAttitude && standing < 1.6,
            Proposal.Alliance => attitude >= AllianceAttitude,
            _ => true
        };
    }

    /// <summary>
    /// How one civilisation's forces compare with another's, as a ratio. Power
    /// ratings are recalculated every other turn; a civilisation with none yet is
    /// treated as an equal.
    /// </summary>
    private static double RelativeStrength(Civilization civ, Civilization other)
    {
        var mine = civ.PowerRating.LastOrDefault();
        var theirs = other.PowerRating.LastOrDefault();

        if (mine <= 0 || theirs <= 0)
        {
            return 1;
        }

        return (double)mine / theirs;
    }

    /// <summary>Something one civilisation can put to another.</summary>
    public enum Proposal
    {
        CeaseFire,
        Peace,
        Alliance,
        DeclareWar,
        GiveGold,
        GiveTechnology
    }
}
