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
        var relation = DiplomacyFunctions.Between(us, civ);
        var standing = relation switch
        {
            { Alliance: true } => "allied",
            { Peace: true } => "at peace",
            { CeaseFire: true } => "cease-fire",
            { War: true } => "at war",
            _ => "no treaty"
        };

        return $"{civ.TribeName} ({standing})";
    }

    private void Parley(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var proposals = DiplomacyFunctions.AvailableProposals(us, other).ToList();
        var buttons = proposals.Select(ButtonFor).Append(Farewell).ToList();

        gameScreen.ShowPopup("DIPLOMACYMENU", (button, _, _, _) =>
        {
            var chosen = proposals.FirstOrDefault(proposal => ButtonFor(proposal) == button);
            if (button == Farewell)
            {
                return;
            }

            gameScreen.QueueAfterCurrentPopup(() => Act(chosen, other));
        }, replaceStrings: [other.Adjective, Standing(other)], buttons: buttons);
    }

    /// <summary>The one line of context the parley opens with.</summary>
    private string Standing(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        var attitude = DiplomacyFunctions.Attitude(other, us);
        var opinion = attitude switch
        {
            >= 80 => "worshipful",
            >= 65 => "friendly",
            >= 45 => "cordial",
            >= 25 => "uneasy",
            _ => "hostile"
        };

        // Civ II's players learn to read both of these numbers: what they think of
        // you decides whether they will deal, and what your word is worth decides
        // whether the deal will hold.
        return $"{Describe(other)}; their court is {opinion}, " +
               $"and your own reputation is {DiplomacyFunctions.ReputationName(us)}.";
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

    private void Act(DiplomacyFunctions.Proposal proposal, Civilization other)
    {
        switch (proposal)
        {
            case DiplomacyFunctions.Proposal.DeclareWar:
                DeclareWar(other);
                break;
            case DiplomacyFunctions.Proposal.GiveGold:
                OfferGold(other);
                break;
            case DiplomacyFunctions.Proposal.GiveTechnology:
                OfferTechnology(other);
                break;
            default:
                Propose(proposal, other);
                break;
        }
    }

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
        gameScreen.ShowPopup(accepted ? "PARLEYACCEPT" : "PARLEYNOTHANKS");
        gameScreen.StatusPanel.Update();
    }

    private void DeclareWar(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        gameScreen.ShowPopup("BREAKTREATY", (button, _, _, _) =>
        {
            if (button != Labels.Ok)
            {
                return;
            }

            DiplomacyFunctions.DeclareWar(gameScreen.Game, us, other);
            SessionLog.Record($"declared war on {other.TribeName}");
            gameScreen.Game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.CeaseFire });
            gameScreen.ShowPopup("WARDECLARED", replaceStrings: [other.Adjective]);
        }, replaceStrings: [other.Adjective]);
    }

    private const string GiftAmount = "Gift";

    private void OfferGold(Civilization other)
    {
        var us = gameScreen.Player.Civilization;
        gameScreen.ShowPopup("MONEYGIFT", (button, _, _, textBoxes) =>
        {
            if (button != Labels.Ok || textBoxes == null ||
                !textBoxes.TryGetValue(GiftAmount, out var entered) ||
                !int.TryParse(entered, out var gold) || gold <= 0)
            {
                return;
            }

            gold = Math.Min(gold, us.Money);
            if (gold <= 0)
            {
                return;
            }

            us.Money -= gold;
            other.Money += gold;
            gameScreen.Game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.GiveGold, Gold = gold });

            SessionLog.Record($"gave {gold} gold to {other.TribeName}");
            gameScreen.StatusPanel.Update();
            gameScreen.ShowPopup("GIFTSENT", replaceStrings: [other.Adjective]);
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
            gameScreen.ShowPopup("PARLEYNOTHANKS");
            return;
        }

        var listbox = new ListboxDefinition();
        listbox.Update(giveable.Select(advance => advance.Name).ToList());

        gameScreen.ShowPopup("GIVETECH", (button, index, _, _) =>
        {
            if (button != Labels.Ok || index < 0 || index >= giveable.Count)
            {
                return;
            }

            game.GiveAdvance(giveable[index].Index, other);
            game.Players[other.Id].ProposalReceived(us,
                new DiplomaticProposal { Kind = DiplomacyProposals.GiveTechnology, Advance = giveable[index].Index });

            SessionLog.Record($"gave {giveable[index].Name} to {other.TribeName}");
            gameScreen.ShowPopup("GIFTSENT", replaceStrings: [other.Adjective]);
        }, replaceStrings: [other.Adjective], listBox: listbox);
    }

    public bool Checked => false;
    public MenuCommand? Command { get; set; }
    public string ErrorDialog => string.Empty;
    public DialogImageElements? ErrorImage => null;
    public string? Name => "Diplomacy";
}
