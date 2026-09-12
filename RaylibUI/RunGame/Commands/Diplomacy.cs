using JetBrains.Annotations;
using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Diplomacy;
using RhyCiv.Engine.Diagnostics;
using RhyCiv.Engine.IO;
using Model;
using Model.Controls;
using Model.Core;
using Model.Core.Player;
using Model.Images;
using Model.Input;

namespace RaylibUI.RunGame.Commands;

/// <summary>
/// The foreign ministry: talking to the civilisations this one has met.
/// <para>
/// There was no way to talk to anybody at all. Civilisations walked into each
/// other and fought until one of them was gone, because nothing could end a war,
/// begin one deliberately, or make a gift -- although the save format has carried
/// cease-fires, treaties, alliances, embassies and reputation from the start.
/// </para>
/// </summary>
[UsedImplicitly]
public class Diplomacy(GameScreen gameScreen) : IGameCommand
{
    public string Id => CommandIds.Diplomacy;

    public Shortcut[] ActivationKeys { get; set; } = [new(Key.D, ctrl: true)];

    public CommandStatus Status { get; private set; }

    public bool Update()
    {
        // Greyed out rather than hidden before the first meeting, so the entry is
        // where it will be once there is somebody to talk to.
        Status = Met().Count > 0 ? CommandStatus.Normal : CommandStatus.Disabled;
        return Status != CommandStatus.Disabled;
    }

    private List<Civilization> Met() =>
        gameScreen.Game.AllCivilizations
            .Where(civ => civ != gameScreen.Player.Civilization && civ.Alive &&
                          civ.PlayerType != PlayerType.Barbarians &&
                          DiplomacyFunctions.HaveMet(gameScreen.Player.Civilization, civ))
            .OrderBy(civ => civ.TribeName)
            .ToList();

    public void Action()
    {
        var met = Met();
        if (met.Count == 0)
        {
            return;
        }

        if (met.Count == 1)
        {
            Parley(met[0]);
            return;
        }

        var listbox = new ListboxDefinition();
        listbox.Update(met.Select(Describe).ToList());

        gameScreen.ShowPopup("CHOOSECIV", (button, index, _, _) =>
        {
            if (button == Labels.Ok && index >= 0 && index < met.Count)
            {
                gameScreen.QueueAfterCurrentPopup(() => Parley(met[index]));
            }
        }, listBox: listbox);
    }

    /// <summary>
    /// A civilisation as it appears in the ministry's list: who they are, and how
    /// things stand between us.
    /// </summary>
    private string Describe(Civilization civ)
    {
        var us = gameScreen.Player.Civilization;

        // Civ II's Foreign Minister gives each civilisation one line carrying
        // three facts -- what they think of us, the treaty in force, and whether
        // we have an embassy -- behind the leader's name and title:
        //
        //   Consul Ishmael of the Babylonians (Worshipful, Alliance, No Embassy)
        //
        // This listed the tribe and the treaty and nothing else, so the two things
        // that decide whether a proposal will be accepted, and whether their
        // affairs can be seen at all, were both invisible from the one screen that
        // exists to show them.
        var embassy = DiplomacyFunctions.HasEmbassyWith(us, civ) ? "Embassy" : "No Embassy";
        return $"{civ.LeaderTitle} {civ.LeaderName} of the {civ.TribeName} " +
               $"({DiplomacyFunctions.AttitudeName(civ, us)}, " +
               $"{DiplomacyFunctions.StandingName(us, civ)}, {embassy})";
    }

    /// <summary>
    /// The leader on the other side of the table, carried through every step of
    /// the audience.
    /// </summary>
    /// <remarks>
    /// Civ II holds a parley in a throne room and keeps the other leader in front
    /// of you from the opening line to the last, so the whole exchange reads as a
    /// conversation with a person. Losing the portrait between steps would turn it
    /// back into a sequence of unrelated message boxes, which is what it was.
    /// </remarks>
    private DialogImageElements? _portrait;

    /// <summary>Shows one step of the audience, with the leader still present.</summary>
    private void Step(string dialog,
        Action<string, int, IList<bool>?, IDictionary<string, string>?> answered,
        IList<string>? replaceStrings = null, IList<string>? buttons = null,
        ListboxDefinition? listBox = null, IList<TextBoxDefinition>? textBoxes = null)
    {
        gameScreen.ShowPopup(dialog, handleButtonClick: answered, replaceStrings: replaceStrings,
            buttons: buttons, listBox: listBox,
            textBoxes: textBoxes is null ? null : textBoxes.ToList(),
            dialogImage: _portrait);
    }

    /// <summary>Goes back to the audience once the current dialog is answered.</summary>
    private void ReturnToAudience(Civilization other) =>
        gameScreen.QueueAfterCurrentPopup(() => Audience(other));

    /// <summary>
    /// How tall the leader's portrait is drawn, in logical pixels.
    /// </summary>
    /// <remarks>
    /// The art is a 1254-pixel square, which drawn at its own size makes a dialog
    /// larger than the screen -- the first attempt at this produced a window of
    /// stone with the question hanging off the top corner. Big enough to be a
    /// person you are talking to, small enough to leave room for what is being
    /// said.
    /// </remarks>
    private const float PortraitHeight = 300f;

    private void Parley(Civilization other)
    {
        _portrait = null;
        if (LeaderPortraits.For(other) is { } portrait)
        {
            // Measured with the same helper the image box itself uses to size the
            // control, so the scale asked for is the scale applied.
            var height = RaylibUtils.Images.GetImageHeight(portrait, gameScreen.Main.ActiveInterface);
            var scale = height > 0 ? PortraitHeight / height : 1f;
            _portrait = new DialogImageElements(portrait, scale);
        }

        Audience(other);
    }

    /// <summary>
    /// The audience itself: what may be raised, given who they are to us.
    /// </summary>
    /// <remarks>
    /// Nested, as Civ II's is. Its parley offers a handful of openings -- have a
    /// proposal to make, wish to offer you a gift, cancel this worthless alliance,
    /// consider this disclosure complete -- and each opens a list of its own, so
    /// the exchange has the shape of a conversation. This was one flat list of
    /// every action at once, which asked the player to pick a treaty and a gift
    /// from the same menu.
    ///
    /// The audience also stays open. Everything but taking leave comes back here,
    /// so several matters can be settled in one sitting rather than one per trip
    /// through the Foreign Ministry.
    /// </remarks>
    private void Audience(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var relation = DiplomacyFunctions.Between(us, other);
        var proposals = DiplomacyFunctions.AvailableProposals(us, other).ToList();

        var buttons = new List<string>();
        if (proposals.Any(IsTreaty))
        {
            buttons.Add(ProposeButton);
        }

        buttons.Add(GiftButton);

        if (relation.Alliance)
        {
            buttons.Add(BreakAllianceButton);
        }

        if (proposals.Contains(DiplomacyFunctions.Proposal.DeclareWar))
        {
            buttons.Add(DeclareWarButton);
        }

        buttons.Add(Farewell);

        Step("DIPLOMACYMENU", (button, _, _, _) =>
        {
            switch (button)
            {
                case ProposeButton:
                    gameScreen.QueueAfterCurrentPopup(() => ProposeMenu(other));
                    break;
                case GiftButton:
                    gameScreen.QueueAfterCurrentPopup(() => GiftMenu(other));
                    break;
                case BreakAllianceButton:
                case DeclareWarButton:
                    gameScreen.QueueAfterCurrentPopup(() => DeclareWar(other));
                    break;
            }
        }, replaceStrings: [other.Adjective, Standing(other)], buttons: buttons);
    }

    /// <summary>The treaties that can be put to them, and never mind.</summary>
    private void ProposeMenu(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var treaties = DiplomacyFunctions.AvailableProposals(us, other).Where(IsTreaty).ToList();
        if (treaties.Count == 0)
        {
            Audience(other);
            return;
        }

        var buttons = treaties.Select(ButtonFor).Append(NeverMind).ToList();

        Step("DIPLOMACYPROPOSE", (button, _, _, _) =>
        {
            var chosen = treaties.FirstOrDefault(proposal => ButtonFor(proposal) == button);
            if (button == NeverMind)
            {
                ReturnToAudience(other);
                return;
            }

            gameScreen.QueueAfterCurrentPopup(() => Propose(chosen, other));
        }, replaceStrings: [other.Adjective], buttons: buttons);
    }

    /// <summary>Gold or knowledge, and never mind.</summary>
    private void GiftMenu(Civilization other)
    {
        Step("DIPLOMACYGIFT", (button, _, _, _) =>
        {
            switch (button)
            {
                case GiftGoldButton:
                    gameScreen.QueueAfterCurrentPopup(() => OfferGold(other));
                    break;
                case GiftTechButton:
                    gameScreen.QueueAfterCurrentPopup(() => OfferTechnology(other));
                    break;
                default:
                    ReturnToAudience(other);
                    break;
            }
        }, replaceStrings: [other.Adjective],
            buttons: [GiftGoldButton, GiftTechButton, NeverMind]);
    }

    private static bool IsTreaty(DiplomacyFunctions.Proposal proposal) =>
        proposal is DiplomacyFunctions.Proposal.CeaseFire
            or DiplomacyFunctions.Proposal.Peace
            or DiplomacyFunctions.Proposal.Alliance;

    private const string ProposeButton = "We Have a Proposal";
    private const string GiftButton = "We Offer a Gift";
    private const string BreakAllianceButton = "Cancel This Alliance";
    private const string DeclareWarButton = "Declare War";
    private const string GiftGoldButton = "Gold";
    private const string GiftTechButton = "Knowledge";
    private const string NeverMind = "Never Mind";

    /// <summary>The one line of context the parley opens with.</summary>
    private string Standing(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        // Short deliberately. The dialog lays the leader's portrait out beside the
        // text, and a long line takes the whole width for itself and leaves the
        // portrait a column twenty pixels wide -- the image is drawn inside its
        // slot, so it does not overflow, it simply shrinks to a speck. The full
        // account of the relationship is in the Foreign Ministry's own list, which
        // is where the player has just come from.
        //
        // Civ II's players read both of these: what they think of you decides
        // whether they will deal, what your word is worth decides whether the deal
        // will hold. Both in Civ II's own vocabulary.
        return $"Their court is {DiplomacyFunctions.AttitudeName(other, us)}. " +
               $"Your word is {DiplomacyFunctions.ReputationName(us)}.";
    }

    private const string Farewell = "Farewell";

    private static string ButtonFor(DiplomacyFunctions.Proposal proposal) => proposal switch
    {
        DiplomacyFunctions.Proposal.CeaseFire => "Cease-Fire",
        DiplomacyFunctions.Proposal.Peace => "Peace Treaty",
        DiplomacyFunctions.Proposal.Alliance => "Alliance",
        DiplomacyFunctions.Proposal.DeclareWar => "Declare War",
        DiplomacyFunctions.Proposal.GiveGold => "Gift of Gold",
        _ => "Give Technology"
    };

    /// <summary>
    /// Puts a treaty to them and reports the answer. The answer is theirs to make:
    /// a computer civilisation weighs what it thinks of us against how the war is
    /// going, so the same offer can be refused one decade and taken the next.
    /// </summary>
    private void Propose(DiplomacyFunctions.Proposal proposal, Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var kind = proposal switch
        {
            DiplomacyFunctions.Proposal.CeaseFire => DiplomacyProposals.CeaseFire,
            DiplomacyFunctions.Proposal.Peace => DiplomacyProposals.Peace,
            _ => DiplomacyProposals.Alliance
        };

        var before = DiplomacyFunctions.Between(us, other).Summary;
        gameScreen.Game.Players[other.Id].ProposalReceived(us, new DiplomaticProposal { Kind = kind });
        var accepted = DiplomacyFunctions.Between(us, other).Summary != before;

        SessionLog.Record($"proposed {kind} to {other.TribeName}: {(accepted ? "accepted" : "refused")}");
        Step(accepted ? "PARLEYACCEPT2" : "PARLEYNOTHANKS", (_, _, _, _) => { });
        ReturnToAudience(other);
        gameScreen.StatusPanel.Update();
    }

    private void DeclareWar(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        Step("BREAKTREATY", (button, _, _, _) =>
        {
            if (button != Labels.Ok)
            {
                ReturnToAudience(other);
                return;
            }

            DiplomacyFunctions.DeclareWar(gameScreen.Game, us, other);
            SessionLog.Record($"declared war on {other.TribeName}");
            gameScreen.Game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.CeaseFire });

            // War ends the audience. There is nothing further to say across a
            // table that no longer exists.
            Step("WARDECLARED", (_, _, _, _) => { }, replaceStrings: [other.Adjective]);
        }, replaceStrings: [other.Adjective]);
    }

    private const string GiftAmount = "Gift";

    private void OfferGold(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        Step("MONEYGIFT", (button, _, _, textBoxes) =>
        {
            if (button != Labels.Ok || textBoxes == null ||
                !textBoxes.TryGetValue(GiftAmount, out var entered) ||
                !int.TryParse(entered, out var gold) || gold <= 0)
            {
                ReturnToAudience(other);
                return;
            }

            gold = Math.Min(gold, us.Money);
            if (gold <= 0)
            {
                ReturnToAudience(other);
                return;
            }

            us.Money -= gold;
            other.Money += gold;
            gameScreen.Game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.GiveGold, Gold = gold });

            SessionLog.Record($"gave {gold} gold to {other.TribeName}");
            gameScreen.StatusPanel.Update();
            Step("GIFTSENT", (_, _, _, _) => { }, replaceStrings: [other.Adjective]);
            ReturnToAudience(other);
        },
        replaceStrings: [other.Adjective],
        textBoxes: [new TextBoxDefinition
        {
            Index = 0, Name = GiftAmount, Description = "Gold:", InitialValue = "0",
            MinValue = 0, Width = 200
        }]);
    }

    private void OfferTechnology(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var game = gameScreen.Game;

        var giveable = game.Rules.Advances
            .Where(advance => AdvanceFunctions.HasTech(us, advance.Index) &&
                              !AdvanceFunctions.HasTech(other, advance.Index))
            .OrderBy(advance => advance.Name)
            .ToList();

        if (giveable.Count == 0)
        {
            Step("PARLEYNOTHANKS", (_, _, _, _) => { });
            ReturnToAudience(other);
            return;
        }

        var listbox = new ListboxDefinition();
        listbox.Update(giveable.Select(advance => advance.Name).ToList());

        Step("GIVETECH", (button, index, _, _) =>
        {
            if (button != Labels.Ok || index < 0 || index >= giveable.Count)
            {
                ReturnToAudience(other);
                return;
            }

            game.GiveAdvance(giveable[index].Index, other);
            game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.GiveTechnology, Advance = giveable[index].Index });

            SessionLog.Record($"gave {giveable[index].Name} to {other.TribeName}");
            Step("GIFTSENT", (_, _, _, _) => { }, replaceStrings: [other.Adjective]);
            ReturnToAudience(other);
        }, replaceStrings: [other.Adjective], listBox: listbox);
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Diplomacy";
}
