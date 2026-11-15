namespace RhyCiv.Engine.Units;

/// <summary>
/// Which of Civ II's city defences turned an attack back.
/// <para>
/// The arithmetic for all four has been in the engine for a long time; what was
/// missing was the player ever hearing about it. A stack that survived an attack
/// because a SAM battery or a coastal fortress was under it looked, from the
/// map, exactly like a stack that survived on its own strength -- and the rule
/// the Civilopedia states went unlearned. Named here rather than in the engine
/// because the combat event that carries it is raised for every civ that can
/// see the fight, and that event lives with the model.
/// </para>
/// </summary>
public enum DefenseBonusKind
{
    /// <summary>No city defence applied to this attack.</summary>
    None,

    /// <summary>A fighter based in the city launches to meet an air attack.</summary>
    FighterScramble,

    /// <summary>The city's SAM batteries answer a conventional air attack.</summary>
    SamBattery,

    /// <summary>The city's SDI defence answers an attack carrying a nuclear warhead.</summary>
    MissileDefence,

    /// <summary>The city's coastal guns answer an attack arriving by sea.</summary>
    CoastalFortress
}
