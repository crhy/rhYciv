using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Units;
using Model.Core.Units;

namespace RhyCiv.Engine.Events
{
    public class MovementBlockedEventArgs(IUnit subjectUnit, BlockedReason reason, Unit? causedBy = null)
        : UnitEventArgs(UnitEventType.MovementBlocked, [subjectUnit.CurrentLocation])
    {
        public BlockedReason Reason { get; set; } = reason;

        /// <summary>
        /// The enemy unit responsible for the refusal, when one is known; for
        /// example the unit whose zone of control blocked the move.
        /// </summary>
        public Unit? CausedBy { get; set; } = causedBy;
    }
}