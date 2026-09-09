using RhyCiv.Engine;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GoodyHuts.Outcomes;
using Model.Core.Mapping;
using Model.Core.Production;
using Model.Core.Units;

namespace Model.Core.Player
{
    public interface IPlayer
    {
        Civilization Civilization { get; }
        Tile ActiveTile { get; set; }

        Unit? ActiveUnit { get; }
        
        List<Unit> WaitingList { get; }

        void CivilDisorder(City city);
        void OrderRestored(City city);
        void WeLoveTheKingStarted(City city);
        void WeLoveTheKingCanceled(City city);
        void CantMaintain(City city, Improvement cityImprovement);
        void SelectNewAdvance(List<Advance> researchPossibilities);
        
        void CantProduce(City city, IProductionOrder? newItem);
        
        void CityProductionComplete(City city);
        IInterfaceCommands Ui { get; }
        
        void NotifyImprovementEnabled(TerrainImprovement improvement, int level);
        void MapChanged(List<Tile> tiles);
        void WaitingAtEndOfTurn();
        void NotifyAdvanceResearched(int advance);

        /// <summary>
        /// The anarchy following a revolution is over and a government has to be
        /// chosen. The player is expected to answer by calling
        /// <c>GovernmentFunctions.AdoptGovernment</c>; until they do, the
        /// civilisation stays in anarchy and is asked again next turn.
        /// </summary>
        void ChooseGovernment(IList<int> availableGovernments);

        /// <summary>
        /// An advance has just opened a form of government the civilisation could
        /// not previously adopt. Civ II offers the revolution at this point rather
        /// than leaving the player to notice.
        /// </summary>
        void GovernmentAvailable(int government);
        void FoodShortage(City city);

        /// <summary>
        /// The city has a full food box but cannot grow without an Aqueduct or
        /// Sewer System.
        /// </summary>
        void CityGrowthHalted(City city);

        /// <summary>
        /// This player's civilization has lost its last city and unit.
        /// </summary>
        void CivilizationDestroyed();

        /// <summary>
        /// Every rival has been eliminated and this civilisation holds the world.
        /// </summary>
        void CivilizationVictorious();
        void CityDecrease(City city);
        void TurnStart(int turnNumber);

        /// <summary>
        /// Set current unit as active unit, and move it if the move parameter is true.
        ///  If the unit parameter is null, set ActiveUnit to null.
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="move"></param>
        /// <returns></returns>
        void SetUnitActive(Unit? unit, bool move);
        void UnitLost(Unit unit, Unit? killedBy);
        
        void UnitsLost(List<Unit> deadUnits, Unit? killedBy = null);
        
        /// <summary>
        /// Called to notify the player that a unit has moved.
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="tileTo"></param>
        /// <param name="tileFrom"></param>
        void UnitMoved(Unit unit, Tile tileTo, Tile tileFrom);

        /// <summary>
        /// Called when a combat happens between two units that we can see, not necessarily our units
        /// </summary>
        /// <param name="combatEventArgs"></param>
        void CombatHappened(CombatEventArgs combatEventArgs);
        
        /// <summary>
        /// Called when a move order can't be followed and has been canceled.
        /// </summary>
        /// <param name="unit"></param>
        /// <param name="blockedReason"></param>
        void MoveBlocked(Unit unit, BlockedReason blockedReason);

        /// <summary>
        /// Called when a unit triggers a goody hut on the tile it moved to.
        /// </summary>
        void GoodyHutTriggered(Unit unit, GoodyHutOutcomeResult outcome);

        /// <summary>
        /// Called when there is a tech theft from a conquered city
        /// </summary>
        /// <param name="techs">The options to steal</param>
        void SelectTechFromConquest(List<Advance> techs);

        /// <summary>
        /// Notifies the player that the specified city has been lost. This method is invoked when control of the city is transferred to another player
        ///
        /// The city will already have been removed from the player's city list and ownership transferred.
        ///  Called before SelectTechFromConquest and CityCaptured
        /// </summary>
        /// <param name="city">The city that is lost.</param>
        void CityLost(City city);

        /// <summary>
        /// Notifies the player that they have captured a city.
        ///  Ownership will already have been transferred.
        ///   Called after CityLost and SelectTechFromConquest
        /// </summary>
        /// <param name="city">The city that has been captured.</param>
        void CityCaptured(City city);

        /// <summary>
        /// A Diplomat has reached somebody else's unit or city and could act on it.
        /// The engine cannot decide what to do -- buying costs gold the player may
        /// want to keep -- so it hands the decision over.
        /// </summary>
        /// <param name="diplomat">The diplomat, still standing where it was.</param>
        /// <param name="target">The square it tried to move onto.</param>
        void DiplomatArrived(Unit diplomat, Tile target);
    }

    public interface IInterfaceCommands
    {
        void ShowDialog(string dialogKey);
        Tuple<string, int, List<bool>> ShowDialog(PopupBox popupBox, List<bool>? checkBoxOptionStates = null);
        void SavePopup(string key, PopupBox popup);
    }
}
