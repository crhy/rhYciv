using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.MapObjects;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GoodyHuts.Outcomes;
using Model.Core.Mapping;
using Model.Core.Player;
using Model.Core.Production;
using Model.Core.Units;

namespace RhyCiv.Tests.Mocks;

public class MockPlayer : IPlayer
{
    public MockPlayer(Civilization civ)
    {
        Civilization = civ;
    }

    public Civilization Civilization { get; }
    public Tile ActiveTile { get; set; }
    public Unit? ActiveUnit { get; set; }
    public List<Unit> WaitingList { get; } = new();

    public bool MoveBlockedCalled { get; private set; }
    public BlockedReason LastBlockedReason { get; private set; }

    public void CivilDisorder(City city)
    {
    }

    public void OrderRestored(City city)
    {
    }

    public void WeLoveTheKingStarted(City city)
    {
    }

    public void WeLoveTheKingCanceled(City city)
    {
    }

    public void CantMaintain(City city, Improvement cityImprovement)
    {
    }

    public void SelectNewAdvance(List<Advance> researchPossibilities)
    {
    }

    public void CantProduce(City city, IProductionOrder? newItem)
    {
    }

    public void CityProductionComplete(City city)
    {
    }

    public IInterfaceCommands Ui { get; set; }

    public void NotifyImprovementEnabled(TerrainImprovement improvement, int level)
    {
    }

    public void MapChanged(List<Tile> tiles)
    {
    }

    public virtual void WaitingAtEndOfTurn()
    {
    }

    public void NotifyAdvanceResearched(int advance)
    {
    }

    public void FoodShortage(City city)
    {
    }

    public void CityGrowthHalted(City city)
    {
    }

    public List<Tile> PollutedSquares { get; } = [];

    public int WarmedSquares { get; private set; }

    public void CityPolluted(City city, Tile square)
    {
        PollutedSquares.Add(square);
    }

    public List<(City City, Improvement Wonder)> WondersBegun { get; } = [];
    public List<(City City, Improvement Wonder)> WondersNearlyComplete { get; } = [];
    public List<(City City, Improvement Wonder)> WondersCompleted { get; } = [];
    public List<(City City, Improvement Wonder)> WondersLost { get; } = [];
    public List<(City City, Improvement Wonder)> WondersCaptured { get; } = [];

    public void WonderBegun(City city, Improvement wonder) => WondersBegun.Add((city, wonder));

    public void WonderNearlyComplete(City city, Improvement wonder) => WondersNearlyComplete.Add((city, wonder));

    public void WonderCompleted(City city, Improvement wonder) => WondersCompleted.Add((city, wonder));

    public void WonderLost(City ourCity, Improvement wonder, City builtIn) => WondersLost.Add((ourCity, wonder));

    public void WonderCaptured(City city, Improvement wonder) => WondersCaptured.Add((city, wonder));

    public List<(Unit Caravan, City City)> CaravansArrived { get; } = [];

    public void CaravanArrived(Unit caravan, City city) => CaravansArrived.Add((caravan, city));

    public List<Civilization> Met { get; } = [];
    public List<(Civilization From, DiplomaticProposal Proposal)> Proposals { get; } = [];

    public void ContactMade(Civilization other) => Met.Add(other);

    public List<Civilization> WarsDeclaredOnUs { get; } = [];

    public void WarDeclared(Civilization aggressor) => WarsDeclaredOnUs.Add(aggressor);

    public List<(Tile Target, Civilization Attacker)> NuclearStrikes { get; } = [];

    public void NuclearStrike(Tile target, Civilization attacker) => NuclearStrikes.Add((target, attacker));

    public virtual void ProposalReceived(Civilization from, DiplomaticProposal proposal) =>
        Proposals.Add((from, proposal));

    public void GlobalWarming(int squaresChanged)
    {
        WarmedSquares += squaresChanged;
    }

    public void CivilizationDestroyed()
    {
    }

    /// <summary>Uprisings this player was told about, newest last.</summary>
    public List<(Tile Where, bool FromTheSea)> Uprisings { get; } = [];

    public void BarbarianUprising(Tile where, bool fromTheSea)
    {
        Uprisings.Add((where, fromTheSea));
    }


    public void CivilizationVictorious()
    {
    }
    public void CityDecrease(City city)
    {
    }

    public IList<int> GovernmentsOffered { get; private set; } = new List<int>();

    public virtual void ChooseGovernment(IList<int> availableGovernments)
    {
        GovernmentsOffered = availableGovernments;
    }

    public virtual void GovernmentAvailable(int government)
    {
    }

    public virtual void TurnStart(int turnNumber)
    {
    }

    public virtual void SetUnitActive(Unit? unit, bool move)
    {
        ActiveUnit = unit;
    }

    public void UnitLost(Unit unit, Unit? killedBy)
    {
    }

    public void UnitsLost(List<Unit> deadUnits, Unit? killedBy)
    {
    }

    public void UnitMoved(Unit unit, Tile tileTo, Tile tileFrom)
    {
    }

    public void CombatHappened(CombatEventArgs combatEventArgs)
    {
    }

    public void MoveBlocked(Unit unit, BlockedReason blockedReason)
    {
        MoveBlockedCalled = true;
        LastBlockedReason = blockedReason;
    }

    public void GoodyHutTriggered(Unit unit, GoodyHutOutcomeResult outcome)
    {
    }

    public void SelectTechFromConquest(List<Advance> techs)
    {
    }

    public void CityLost(City city)
    {
    }

    public void CityCaptured(City city)
    {
    }

    public virtual void DiplomatArrived(Unit diplomat, Tile target)
    {
        DiplomatTargets.Add((diplomat, target));
    }

    /// <summary>Every square a diplomat was offered an action on.</summary>
    public List<(Unit Diplomat, Tile Target)> DiplomatTargets { get; } = new();
}
