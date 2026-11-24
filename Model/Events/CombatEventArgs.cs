using System.Collections.Generic;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Units;
using Model.Core.Units;

namespace RhyCiv.Engine.Events
{
    public class CombatEventArgs : UnitEventArgs
    {
        public List<bool> CombatRoundsAttackerWins;

        public UnitInfo Defender;

        public CombatEventArgs(UnitEventType eventType, Unit attacker, Unit defender, List<bool> combatRoundsAttackerWins,
            List<int> attackerHitpoints, List<int> defenderHitpoints) : base(eventType, new [] { attacker.CurrentLocation, defender.CurrentLocation })
        {
            Attacker = new UnitInfo(attacker, attackerHitpoints);
            Defender = new UnitInfo(defender, defenderHitpoints);
            CombatRoundsAttackerWins = combatRoundsAttackerWins;
            Sound = attacker.AttackSound;
        }

        public UnitInfo Attacker { get; set; }
        public string Sound { get; }

        /// <summary>
        /// Which of the defender's city bonuses stood between it and this attack,
        /// or <see cref="RhyCiv.Engine.Units.DefenseBonusKind.None"/> when nothing
        /// but the unit's own strength did. Set by the combat resolver for the
        /// defender it chose, so the player can be told what held the attack back.
        /// </summary>
        public DefenseBonusKind DefenseBonus { get; set; } = DefenseBonusKind.None;
    }
}