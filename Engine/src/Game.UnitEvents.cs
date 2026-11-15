using RhyCiv.Engine.Events;

namespace RhyCiv.Engine
{
    public partial class Game
    {
        /// <summary>
        /// Raises a unit event on behalf of a caller outside the game itself.
        /// <para>
        /// A field-like event can only be invoked from the class that declares it,
        /// so anything else that has to raise one goes through here. The turn cycle
        /// in <c>Game.ActionsUnits</c> keeps raising its own activations directly;
        /// this exists for the scenario scripting API, which has to wake one
        /// particular unit rather than the next unit the turn cycle would pick.
        /// </para>
        /// <para>
        /// Not visible to scripts: it is the engine's own notification path, and a
        /// scenario that could raise arbitrary unit events would be able to invent
        /// activations the game never made.
        /// </para>
        /// </summary>
        /// <param name="args">The unit event to deliver.</param>
        internal void RaiseUnitEvent(UnitEventArgs args)
        {
            OnUnitEvent?.Invoke(this, args);
        }
    }
}
