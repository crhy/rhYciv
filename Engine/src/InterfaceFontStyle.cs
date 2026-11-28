namespace RhyCiv.Engine;

/// <summary>
/// The typeface the interface chrome -- the menu bar, dialogs, buttons and labels
/// -- is drawn with.
/// <para>
/// The bundled atlases are Liberation Sans and Liberation Serif. rhY's complaint
/// (crhy/rhYciv#66) was that the menu text was hard to read: a Times-like serif at
/// the small sizes the chrome uses reads as a smear of hairlines and stems. A
/// sans is clearer there, so it is the default. The serif is kept as an option
/// (and for the long-form Civilopedia body, where it reads better) so a player
/// who preferred the original look can switch back.
/// </para>
/// </summary>
public enum InterfaceFontStyle
{
    /// <summary>Liberation Sans: clearer at small UI sizes. The default.</summary>
    Sans = 0,

    /// <summary>Liberation Serif: the original look, for those who prefer it.</summary>
    Classic = 1,
}
