namespace Model.Controls;
using Model.Input;

public class DropdownMenuContents
{
    /// <summary>
    /// Which menu this is, as the interface names it -- "GAME", "ORDERS", "CHEAT".
    /// The title is what the player reads and can be translated or renamed; this
    /// stays put, so it is what code should test against when it wants to know
    /// whether a particular menu should be on the bar at all.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    
    public Key HotKey { get; set; }
    
    public required IList<MenuCommand> Commands { get; init; }

    public required int[] SeparatorRows { get; init; }
}
