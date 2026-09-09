using RhyCiv.Engine;
using RhyCiv.Engine.UnitActions;
using RhyCiv.Engine.SaveLoad;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.Diagnostics;
using RhyCiv.Engine.Enums;
using RhyCiv.Engine.Events;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using Model.Controls;
using Model.Controls.Civilopedia;
using Model.Core;
using Model.Core.Advances;
using Model.Core.Cities;
using Model.Core.GoodyHuts.Outcomes;
using Model.Core.Mapping;
using Model.Core.Units;
using Model.Events;
using Model.Core.Player;
using Model.Core.Production;
using Model.Images;
using RaylibUI.RunGame.GameControls.Civilopedia;

namespace RaylibUI.RunGame;

public class LocalPlayer : IPlayer
{
    private readonly GameScreen _gameScreen;

    public LocalPlayer(GameScreen gameScreen, Civilization civilization)
    {
        _gameScreen = gameScreen;
        Civilization = civilization;
    }

    public Civilization Civilization { get; }

    public Tile ActiveTile { get; set; }

    private Unit? _activeUnit;

    public Unit? ActiveUnit
    {
        get { return _activeUnit; }
        set
        {
            if (value == null)
            {
                _activeUnit = null;
            }
            else if (value is { TurnEnded: false, Dead: false } && value.Owner == Civilization)
            {
                if (value.CurrentLocation != null) ActiveTile = value.CurrentLocation;
                _activeUnit = value;
            }
            else
            {
#if DEBUG
                //     throw new NotSupportedException("Tried to set ended unit to active");
#endif
            }
        }
    }

    public void CivilDisorder(City city)
    {
        _gameScreen.ShowCityDialog("DISORDER", city);
    }

    public void OrderRestored(City city)
    {
        _gameScreen.ShowCityDialog("RESTORED", city);
    }

    public void WeLoveTheKingStarted(City city)
    {
        _gameScreen.ShowCityDialog("WELOVEKING", city);
    }

    public void WeLoveTheKingCanceled(City city)
    {
        _gameScreen.ShowCityDialog("WEDONTLOVEKING", city);
    }

    public void CantMaintain(City city, Improvement cityImprovement)
    {
        _gameScreen.ShowCityDialog("INHOCK", city, [city.Name, cityImprovement.Name],
            [cityImprovement.Cost]);
    }

    /// <summary>
    /// Whether the research chooser is on screen and still waiting for an answer.
    /// <para>
    /// The chooser is a dialog: showing it returns at once and the answer arrives
    /// later. The engine asks whenever the civilisation has nothing being
    /// researched, which is still true until the player replies -- so asking again
    /// before then would stack a second identical chooser behind the first, and the
    /// player would have to pick the same technology twice.
    /// </para>
    /// <para>
    /// It is set only when a chooser actually went up, and cleared only when an
    /// advance has been chosen. It deliberately survives the start of a turn: the
    /// engine asks for research from <c>TurnBeginning</c>, which runs *before* the
    /// player is told the turn has started, so clearing this on turn start -- which
    /// it used to do, as a backstop against a question that never got answered --
    /// cancelled the guard on the same turn it was set, every turn, and left it
    /// doing nothing at all.
    /// </para>
    /// </summary>
    private bool _awaitingResearchChoice;

    public void SelectNewAdvance(List<Advance> researchPossibilities)
    {
        if (_awaitingResearchChoice)
        {
            return;
        }

        // Only wait for an answer if there is going to be one. A ruleset whose text
        // has no RESEARCH dialog shows nothing, and setting the flag then would lock
        // the question out for the rest of the game.
        _awaitingResearchChoice = ShowResearchChoice(researchPossibilities, 0);
    }

    /// <summary>
    /// The name of the button that narrows the research list to the advances
    /// leading towards the civilisation's technology goal. It is added to the
    /// dialog here rather than in the game's text so that it is present whatever
    /// text the ruleset supplies.
    /// </summary>
    private const string GoalButton = "Goal";

    /// <summary>
    /// The research chooser. Info opens the Civilopedia page for whichever advance
    /// is highlighted, and puts the chooser back underneath, so the pedia closes
    /// onto the question again. Goal leads to the technology goal, and to the
    /// shorter list of advances that lead towards it.
    /// </summary>
    /// <returns>Whether the chooser was put up.</returns>
    private bool ShowResearchChoice(List<Advance> researchPossibilities, int preselected)
    {
        var activeInterface = _gameScreen.Main.ActiveInterface;
        return _gameScreen.ShowPopup("RESEARCH", (s, i, arg3, arg4) =>
            {
                if (researchPossibilities.Count == 0)
                {
                    _awaitingResearchChoice = false;
                    return;
                }

                var selectedIndex = Math.Clamp(i, 0, researchPossibilities.Count - 1);

                if (s == "Info")
                {
                    // Pushed first so it sits under the pedia page pushed next. The
                    // question is still outstanding, so the flag stays set.
                    ShowResearchChoice(researchPossibilities, selectedIndex);
                    NotifyAdvanceResearched(researchPossibilities[selectedIndex].Index);
                    return;
                }

                if (s == GoalButton)
                {
                    // Still the same outstanding question, asked a different way.
                    ShowGoalRoute(researchPossibilities);
                    return;
                }

                _awaitingResearchChoice = false;
                StartResearching(researchPossibilities[selectedIndex]);
            }, replaceStrings: [activeInterface.GetScientistName(Civilization.Epoch)],
            listBox: ResearchListbox(researchPossibilities, preselected),
            buttons: ResearchButtons());
    }

    /// <summary>
    /// The chooser's buttons, in the order they are read: what to work towards,
    /// then what a highlighted advance is, then the decision. The confirming button
    /// is last because that is where the eye finishes, and Goal is first because it
    /// is the question you ask before you answer this one.
    /// <para>
    /// Whatever else the ruleset's text defines for this dialog is kept, so a
    /// translated Info button survives; only the order is imposed, and Goal is added
    /// because no ruleset knows about it.
    /// </para>
    /// </summary>
    private List<string> ResearchButtons()
    {
        var defined = _gameScreen.Main.ActiveInterface.GetDialog("RESEARCH")?.Button ?? [];
        var buttons = new List<string> { GoalButton };
        buttons.AddRange(defined.Where(b => b != GoalButton && b != Labels.Ok));
        buttons.Add(Labels.Ok);
        return buttons;
    }

    /// <summary>
    /// Commits the civilisation to an advance, and makes sure it is actually being
    /// paid for: a civilisation that has had its science rate at nothing would
    /// otherwise agree to research something and never make any progress on it.
    /// </summary>
    private void StartResearching(Advance advance)
    {
        Civilization.ReseachingAdvance = advance.Index;
        if (Civilization.ScienceRate <= 0)
        {
            Civilization.ScienceRate = 60;
            Civilization.TaxRate = Math.Min(Civilization.TaxRate, 40);
        }
    }

    private ListboxDefinition ResearchListbox(IList<Advance> advances, int preselected)
    {
        var activeInterface = _gameScreen.Main.ActiveInterface;
        return new ListboxDefinition
        {
            VerticalScrollbar = advances.Count > 10,
            ImageShift = false,
            Rows = Math.Min(10, Math.Max(1, advances.Count)),
            Looks = new ListboxLooks
            {
                Font = activeInterface.Look.DefaultFont,
                FontSize = 20,
                TextColorFront = Raylib_CSharp.Colors.Color.Black,
                TextColorShadow = Raylib_CSharp.Colors.Color.Blank,
                TextShadowOffset = System.Numerics.Vector2.Zero,
                SelectedTextFont = activeInterface.Look.DefaultFont,
                SelectedTextBackgroundColor = new Raylib_CSharp.Colors.Color(107, 107, 107, 255),
                SelectedTextColorFront = Raylib_CSharp.Colors.Color.White,
                SelectedTextColorShadow = Raylib_CSharp.Colors.Color.Black
            },
            Groups = advances.Select(a => new ListboxGroup
            {
                Elements = [new() { Icon = GetClassicAdvanceIcon(a), Width = 2 * 36 + 2 },
                            new() { Text = a.Name, VerticalAlignment = VerticalAlignment.Center } ],
                Height = 36
            }).ToList(),
            SelectedId = Math.Clamp(preselected, 0, Math.Max(0, advances.Count - 1))
        };
    }

    /// <summary>
    /// The Goal button. Shows the advances on the list that lead towards the
    /// civilisation's technology goal, so a player working towards something
    /// several advances away does not have to hold the tech tree in their head
    /// every time the scientists ask.
    /// <para>
    /// Without a goal there is nothing to narrow the list by, so the goal is asked
    /// for first.
    /// </para>
    /// </summary>
    private void ShowGoalRoute(List<Advance> researchPossibilities)
    {
        if (Civilization.ResearchGoal < 0)
        {
            ShowGoalChooser(researchPossibilities);
            return;
        }

        var goal = GoalAdvance();
        if (goal == null)
        {
            // The goal names an advance these rules do not have, or one that has
            // since been learned. Either way there is nothing to steer by.
            Civilization.ResearchGoal = -1;
            ShowGoalChooser(researchPossibilities);
            return;
        }

        var steps = AdvanceFunctions.StepsToward(_gameScreen.Game, Civilization, goal.Index,
            researchPossibilities);

        if (steps.Count == 0)
        {
            ShowNothingLeadsToGoal(researchPossibilities, goal);
            return;
        }

        var elements = new DialogElements
        {
            Name = "RESEARCHGOALROUTE_DYNAMIC",
            Title = $"Towards {goal.Name}",
            Width = 400,
            Button = [Labels.Ok, GoalButton, Labels.Cancel],
            Text = [steps.Count == 1
                ? $"One advance you can begin now leads towards {goal.Name}."
                : $"These advances lead towards {goal.Name}."],
            LineStyles = [TextStyles.Left],
            Listbox = ResearchListbox(steps, 0)
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, elements, (button, selected, _, _) =>
        {
            _gameScreen.CloseDialog(dialog);

            if (button == GoalButton)
            {
                ShowGoalChooser(researchPossibilities);
                return;
            }

            if (button != Labels.Ok)
            {
                // Back to the full list; the question has still not been answered.
                ShowResearchChoice(researchPossibilities, 0);
                return;
            }

            var chosen = steps[Math.Clamp(selected, 0, steps.Count - 1)];
            _awaitingResearchChoice = false;
            StartResearching(chosen);
        });
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    /// <summary>
    /// Says so, plainly, when the goal is real but out of reach for now -- every
    /// advance that leads to it needs something the civilisation has not learned.
    /// Silently showing an empty list would read as a broken dialog.
    /// </summary>
    private void ShowNothingLeadsToGoal(List<Advance> researchPossibilities, Advance goal)
    {
        var elements = new DialogElements
        {
            Name = "RESEARCHGOALBLOCKED_DYNAMIC",
            Title = "Research Goal",
            // A remark, not a proclamation: kept to its own width so it wraps into
            // a few short lines rather than being stretched across the screen.
            Compact = true,
            Width = 260,
            Button = [Labels.Ok, GoalButton],
            Text =
            [
                $"There is nothing that can be researched towards {goal.Name} at this time.",
                "Choose from the full list, or set a different goal."
            ],
            LineStyles = [TextStyles.Left, TextStyles.Left]
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, elements, (button, _, _, _) =>
        {
            _gameScreen.CloseDialog(dialog);
            if (button == GoalButton)
            {
                ShowGoalChooser(researchPossibilities);
            }
            else
            {
                ShowResearchChoice(researchPossibilities, 0);
            }
        });
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    /// <summary>
    /// Picks the advance to work towards. Everything the civilisation does not have
    /// and is not barred from is offered, however far off, because that is the point
    /// of a goal; the first entry gives it up again.
    /// </summary>
    private void ShowGoalChooser(List<Advance> researchPossibilities)
    {
        var goals = AdvanceFunctions.PossibleResearchGoals(_gameScreen.Game, Civilization);
        if (goals.Count == 0)
        {
            ShowResearchChoice(researchPossibilities, 0);
            return;
        }

        var current = goals.FindIndex(a => a.Index == Civilization.ResearchGoal);
        var elements = new DialogElements
        {
            Name = "RESEARCHGOAL_DYNAMIC",
            Title = "Set Research Goal",
            Width = 400,
            Button = [Labels.Ok, Labels.Cancel],
            Text = ["What shall we work towards?"],
            LineStyles = [TextStyles.Left],
            Listbox = new ListboxDefinition
            {
                VerticalScrollbar = true,
                ImageShift = false,
                Rows = 10,
                Looks = GoalListLooks(),
                Groups = GoalEntries(goals),
                SelectedId = current >= 0 ? current + 1 : 0
            }
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, elements, (button, selected, _, _) =>
        {
            _gameScreen.CloseDialog(dialog);

            if (button != Labels.Ok)
            {
                ShowResearchChoice(researchPossibilities, 0);
                return;
            }

            // Entry zero is "no goal"; everything after it is an advance.
            Civilization.ResearchGoal = selected <= 0 || selected > goals.Count
                ? -1
                : goals[selected - 1].Index;

            if (Civilization.ResearchGoal < 0)
            {
                ShowResearchChoice(researchPossibilities, 0);
            }
            else
            {
                ShowGoalRoute(researchPossibilities);
            }
        });
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    private List<ListboxGroup> GoalEntries(IList<Advance> goals)
    {
        var entries = new List<ListboxGroup>
        {
            new()
            {
                Elements = [new() { Text = "(no goal)", VerticalAlignment = VerticalAlignment.Center }],
                Height = 36
            }
        };

        entries.AddRange(goals.Select(a => new ListboxGroup
        {
            Elements =
            [
                new() { Icon = GetClassicAdvanceIcon(a), Width = 2 * 36 + 2 },
                new() { Text = a.Name, VerticalAlignment = VerticalAlignment.Center }
            ],
            Height = 36
        }));

        return entries;
    }

    private ListboxLooks GoalListLooks()
    {
        var activeInterface = _gameScreen.Main.ActiveInterface;
        return new ListboxLooks
        {
            Font = activeInterface.Look.DefaultFont,
            FontSize = 20,
            TextColorFront = Raylib_CSharp.Colors.Color.Black,
            TextColorShadow = Raylib_CSharp.Colors.Color.Blank,
            TextShadowOffset = System.Numerics.Vector2.Zero,
            SelectedTextFont = activeInterface.Look.DefaultFont,
            SelectedTextBackgroundColor = new Raylib_CSharp.Colors.Color(107, 107, 107, 255),
            SelectedTextColorFront = Raylib_CSharp.Colors.Color.White,
            SelectedTextColorShadow = Raylib_CSharp.Colors.Color.Black
        };
    }

    private Advance? GoalAdvance()
    {
        var advances = _gameScreen.Game.Rules.Advances;
        var goal = Civilization.ResearchGoal;
        if (goal < 0 || goal >= advances.Length || AdvanceFunctions.HasTech(Civilization, goal))
        {
            return null;
        }

        return advances[goal];
    }

    /// <summary>
    /// The badge shown beside an advance in the research list, saying what kind of
    /// advance it is. This used to read a 36x20 patch of the ICONS sheet, which the
    /// standalone set fills with plain coloured rectangles, so every advance was a
    /// flat swatch (#48).
    /// </summary>
    private static readonly string[] AdvanceCategoryArt =
        ["academic", "applied", "military", "social"];

    private static IImageSource GetClassicAdvanceIcon(Advance advance)
    {
        var category = advance.KnowledgeCategory;
        if (category < 0 || category >= AdvanceCategoryArt.Length)
        {
            category = 1;
        }

        return new BitmapStorage(
            System.IO.Path.Combine("Icons", "AdvanceCategories", $"{AdvanceCategoryArt[category]}.png"));
    }

    public void CantProduce(City city, IProductionOrder? newItem)
    {
        _gameScreen.ShowCityDialog("BADBUILD", city);
    }

    public void CityProductionComplete(City city)
    {
        _gameScreen.ShowCityDialog("BUILT", city);
    }

    public IInterfaceCommands Ui { get; }
    public List<Unit> WaitingList { get; } = new();

    /// <summary>
    /// Announces a terrain improvement that an advance has just made available.
    /// <para>
    /// This went through <see cref="Ui"/>, which nothing ever assigns -- no type in
    /// the solution implements <see cref="IInterfaceCommands"/> -- so researching an
    /// advance that enables an improvement with a message threw a
    /// NullReferenceException and took the game down. It now uses the same popup
    /// path as the rest of this class.
    /// </para>
    /// </summary>
    public void NotifyImprovementEnabled(TerrainImprovement improvement, int level)
    {
        if (level < 0 || level >= improvement.Levels.Count)
        {
            return;
        }

        var dialogKey = improvement.Levels[level].EnabledMessage;
        if (!string.IsNullOrWhiteSpace(dialogKey))
        {
            _gameScreen.ShowPopup(dialogKey);
        }
    }

    public void MapChanged(List<Tile> tiles)
    {
        // var t = tiles.SelectMany(t => t.Map.DirectNeighbours(t));

        var allTiles = tiles
            .Concat(tiles.SelectMany(t => t.Map.DirectNeighbours(t).Where(n => n.IsVisible(_gameScreen.VisibleCivId))))
            .Distinct();
        foreach (var tile in allTiles)
        {
            _gameScreen.TileCache.Redraw(tile, _gameScreen.VisibleCivId);
        }

        _gameScreen.ForceRedraw();
    }

    /// <summary>
    /// Every unit has moved and the engine is waiting for the turn to be ended.
    /// <para>
    /// Civ II says so, in the side panel, and takes Enter for it. Nothing said so
    /// here: the interface simply dropped into viewing-pieces mode, which looks the
    /// same as choosing to look around mid-turn, so there was no way to tell that
    /// the game was waiting rather than that a unit had been missed.
    /// </para>
    /// </summary>
    public bool IsWaitingAtEndOfTurn { get; private set; }

    public void WaitingAtEndOfTurn()
    {
        IsWaitingAtEndOfTurn = true;
        _gameScreen.ActiveMode = _gameScreen.ViewPiece;
    }

    public void NotifyAdvanceResearched(int advance)
    {
        var rules = _gameScreen.Game.Rules;
        if (advance < 0 || advance >= rules.Advances.Length)
        {
            return;
        }

        var discoveredAdvance = rules.Advances[advance];
        var sortedAdvances = rules.Advances.Take(89).OrderBy(a => a.Name).ToList();
        var civilopediaIndex = sortedAdvances.IndexOf(discoveredAdvance);
        if (civilopediaIndex < 0)
        {
            civilopediaIndex = Math.Clamp(advance, 0, Math.Max(0, sortedAdvances.Count - 1));
        }

        _gameScreen.ShowDialog(new CivilopediaWindow(_gameScreen,
            new CivilopediaEntry(CivilopediaInfoType.Advances, CivilopediaWindowType.Info, civilopediaIndex)), stack: true);
    }

    public void FoodShortage(City city)
    {
        _gameScreen.ShowCityDialog("FOODSHORTAGE", city);
    }

    public void CityGrowthHalted(City city)
    {
        _gameScreen.ShowCityDialog("FURTHERGROWTH", city);
    }

    public void CivilizationDestroyed()
    {
        _gameScreen.ShowPopup("CIVDESTROYED");
    }

    public void CivilizationVictorious()
    {
        _gameScreen.ShowPopup("CONQUEST",
            dialogImage: new DialogImageElements(
                [_gameScreen.Main.ActiveInterface.PicSources["victoryConquest"][0]]));
    }

    public void CityDecrease(City city)
    {
        _gameScreen.ShowCityDialog("DECREASE", city);
    }

    /// <summary>
    /// How many autosaves are kept before the oldest is written over. Enough to
    /// step back past a turn that went wrong, few enough not to fill a disk.
    /// </summary>
    private const int AutosaveSlots = 3;

    public void TurnStart(int turnNumber)
    {
        IsWaitingAtEndOfTurn = false;
        _lastBlocked = (null, BlockedReason.NotBlocked);
        Autosave(turnNumber);
        _gameScreen.TurnStarting(turnNumber);
    }

    /// <summary>
    /// Writes the game at the start of the player's turn, if they have asked for it.
    /// <para>
    /// "Autosave each turn" has been a checkbox in Game Options for as long as the
    /// options dialog has existed, and nothing has ever read it. Given that a
    /// session can still end in a fault no handler can catch, an autosave that does
    /// not happen is the worst of the settings to have got wrong.
    /// </para>
    /// <para>
    /// The turn is saved as it begins, before the player has moved anything, so the
    /// newest autosave is always a position that can be picked up cleanly. Failing
    /// to write one must never end a session, so everything here is best-effort:
    /// the player gets a line in the record and carries on playing.
    /// </para>
    /// </summary>
    private void Autosave(int turnNumber)
    {
        var game = _gameScreen.Game;
        if (!game.Options.AutosaveEachTurn || _gameScreen.Main.ActiveRuleSet is not { } ruleset)
        {
            return;
        }

        try
        {
            var slot = Math.Abs(turnNumber) % AutosaveSlots + 1;
            var path = Path.Combine(Settings.SaveGameFolder, $"autosave{slot}.sav");
            var viewData = new Dictionary<string, string> { { "Zoom", _gameScreen.Zoom.ToString() } };

            // AtomicFile so a failure part way through cannot destroy the autosave
            // from three turns ago that it is writing over.
            AtomicFile.Write(path,
                stream => new GameSerializer().Write(stream, game, ruleset, viewData));
            SessionLog.Record($"autosaved turn {turnNumber} to {Path.GetFileName(path)}");
        }
        catch (Exception error)
        {
            SessionLog.Record($"autosave failed: {error.Message}");
            Console.Error.WriteLine($"rhYciv: could not autosave: {error.Message}");
        }
    }

    public void SetUnitActive(Unit? unit, bool move)
    {
        if (unit != null)
        {
            IsWaitingAtEndOfTurn = false;
        }

        ActiveUnit = unit;

        if (_gameScreen.Game.GetActiveCiv != Civilization)
        {
            return;
        }

        // ActiveUnit refuses a unit that has ended its turn, and used to leave the
        // previous one in place. Switching to Moving anyway left the map in
        // unit-moving mode pointing at a unit that was not there, so nothing
        // responded to the keyboard and no unit could be picked. Fall back to the
        // view piece instead, which the player can always move.
        if (unit != null && !ReferenceEquals(_activeUnit, unit))
        {
            // The engine offered a unit that cannot be given orders. It should not
            // any more, but if it ever does again the game must not be left with
            // nothing selected and no idea that it is waiting: that is the state
            // where a unit appears to blink without responding to anything and
            // Enter looks like it has done nothing. Treat it as having nothing left
            // to move, which is what it amounts to.
            _activeUnit = null;
            if (unit.CurrentLocation != null) ActiveTile = unit.CurrentLocation;
            IsWaitingAtEndOfTurn = true;
            _gameScreen.ActiveMode = _gameScreen.ViewPiece;
            return;
        }

        // No unit to move. The view piece is the only sensible mode here, and the
        // only safe one: MovingPieces.Activate asks the game for the next unit
        // whenever there is no active one, and this is reached from ChooseNextUnit
        // when nothing is awaiting orders. Entering Moving mode from here therefore
        // called back into ChooseNextUnit and recursed until the stack overflowed,
        // which .NET cannot catch -- the process died with no crash report at all.
        if (_activeUnit == null)
        {
            _gameScreen.ActiveMode = _gameScreen.ViewPiece;
            return;
        }

        _gameScreen.ActiveMode = _gameScreen.Moving;
    }

    public void UnitLost(Unit unit, Unit? killedBy)
    {
        UnitsLost([unit], killedBy);
    }

    /// <summary>
    /// Units belonging to this player have died — in combat, or with the city that
    /// supported them when it was captured.
    /// <para>
    /// Both of these were unimplemented. The engine took the units off the map and
    /// nothing told the interface, so the tiles they had stood on were never
    /// repainted and they appeared to survive whatever had killed them. Losing a
    /// city to the barbarians left its supported units apparently still standing.
    /// </para>
    /// </summary>
    public void UnitsLost(List<Unit> deadUnits, Unit? killedBy)
    {
        if (deadUnits.Count == 0)
        {
            return;
        }

        SessionLog.Record($"lost {deadUnits.Count} unit(s)" +
                          (killedBy == null ? "" : $" to {killedBy.Name}"));

        // Nothing should still be selected if it has just died: the map would go on
        // blinking a unit that is no longer there, and the side panel would describe
        // it as though it could still be given orders.
        if (ActiveUnit is { } active && deadUnits.Contains(active))
        {
            SetUnitActive(null, false);
        }

        // Unit.Dead takes a unit off its tile but asks for no redraw, so the tile
        // keeps the last frame it was drawn with.
        var tiles = deadUnits
            .Select(unit => unit.CurrentLocationOrNull)
            .OfType<Tile>()
            .Distinct()
            .ToList();

        if (tiles.Count > 0)
        {
            MapChanged(tiles);
        }
        else
        {
            _gameScreen.ForceRedraw();
        }
    }

    public void UnitMoved(Unit unit, Tile tileTo, Tile tileFrom)
    {
        OnUnitEvent?.Invoke(this, new MovementEventArgs(unit, tileFrom, tileTo));
    }

    public void CombatHappened(CombatEventArgs combatEventArgs)
    {
        OnUnitEvent?.Invoke(this, combatEventArgs);
    }

    private (Unit? Unit, BlockedReason Reason) _lastBlocked;

    public void MoveBlocked(Unit unit, BlockedReason blockedReason)
    {
        OnUnitEvent?.Invoke(this, new MovementBlockedEventArgs(unit, blockedReason));
        ReportBlockedMove(unit, blockedReason);
    }

    /// <summary>
    /// Say why a move was refused. The engine has always raised this, but nothing
    /// listened, so a unit that could not move simply did nothing and looked stuck.
    /// Repeats are suppressed - a unit bumping the same zone of control every turn
    /// should not stack up popups.
    /// </summary>
    private void ReportBlockedMove(Unit unit, BlockedReason blockedReason)
    {
        var message = blockedReason switch
        {
            BlockedReason.Zoc =>
                $"Your {unit.Name} cannot move directly between two squares that are both " +
                "next to an enemy unit. Move to a square you already occupy, or attack.",
            BlockedReason.ZeroAttackStrength =>
                $"Your {unit.Name} has no attack strength and cannot move onto an enemy unit.",
            BlockedReason.CannotAttackAirUnits =>
                $"Your {unit.Name} cannot attack air units.",
            BlockedReason.NotAmphibious =>
                $"Your {unit.Name} cannot make an amphibious assault from aboard ship.",
            BlockedReason.EdgeOfMap =>
                $"Your {unit.Name} has reached the edge of the world.",
            _ => null
        };

        if (message == null || (_lastBlocked.Unit == unit && _lastBlocked.Reason == blockedReason))
        {
            return;
        }

        _lastBlocked = (unit, blockedReason);

        var elements = new DialogElements
        {
            Name = "MOVEBLOCKED_DYNAMIC",
            Title = "Blocked",
            Width = 420,
            Button = [Labels.Ok],
            Text = [message],
            LineStyles = [TextStyles.Left]
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, elements, (_, _, _, _) => _gameScreen.CloseDialog(dialog));
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    public event EventHandler<UnitEventArgs> OnUnitEvent;

    public void GoodyHutTriggered(Unit unit, GoodyHutOutcomeResult outcome)
    {
        var args = new GoodyHutOutcomeEventArgs(unit, outcome);
        OnUnitEvent?.Invoke(this, args);

        _gameScreen.ForceRedraw();
        ShowGoodyHutResult(outcome);
    }

    private void ShowGoodyHutResult(GoodyHutOutcomeResult outcome)
    {
        var title = outcome.OutcomeType switch
        {
            "Gold" => "Village Gold",
            "Scrolls" => "Ancient Scrolls",
            "Nomads" => "Wandering Nomads",
            "AdvancedTribe" => "Advanced Tribe",
            "Barbarians" => "Barbarian Horde",
            "AbandonedVillage" => "Abandoned Village",
            "Mercenaries" => "Mercenaries",
            _ => "Village"
        };

        var message = outcome.Message;
        if (outcome.AdvanceIndex is { } advanceIndex &&
            advanceIndex >= 0 && advanceIndex < _gameScreen.Game.Rules.Advances.Length)
        {
            message += $" You have gained { _gameScreen.Game.Rules.Advances[advanceIndex].Name }.";
        }

        var dialogElements = new DialogElements
        {
            Name = "GOODYHUT_DYNAMIC",
            Title = title,
            Width = 420,
            Button = [Labels.Ok],
            Text = [message],
            LineStyles = [TextStyles.Left]
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, dialogElements, (_, _, _, _) => _gameScreen.CloseDialog(dialog));
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    public void SelectTechFromConquest(List<Advance> techs)
    {
        var advance = _gameScreen.Game.Random.ChooseFrom(techs);
        _gameScreen.Game.GiveAdvance(advance.Index, Civilization);
        
        //TODO: Show popup
    }

    /// <summary>
    /// A city of ours has been taken. Also unimplemented, so losing a city said
    /// nothing whatever -- it left your flag on the map and the city simply stopped
    /// being yours. It is the same news as a capture, seen from the other end.
    /// </summary>
    public void CityLost(City city)
    {
        SessionLog.Record($"lost {city.Name} (size {city.Size})");
        _gameScreen.ShowPopup("CITYLOST", replaceStrings: [city.Name]);
    }

    /// <summary>
    /// A Diplomat has reached somebody else's unit or city. Offer what can be done
    /// with it, and how much it costs.
    /// <para>
    /// A Diplomat has no attack strength, so before this the engine simply refused
    /// the move and said so. The unit could be built and walked across the map and
    /// then did nothing at all.
    /// </para>
    /// </summary>
    public void DiplomatArrived(Unit diplomat, Tile target)
    {
        var city = DiplomatActions.EnemyCityAt(diplomat, target);
        var unit = DiplomatActions.BribableUnitAt(diplomat, target);

        SessionLog.Record($"diplomat at {target.X},{target.Y} " +
                          $"(city={city?.Name ?? "none"}, unit={unit?.Name ?? "none"})");

        var choices = new List<(string Label, Action Take)>();
        if (city != null)
        {
            choices.Add(($"Investigate {city.Name}", () => OfferToInvestigate(diplomat, city)));

            if (DiplomatActions.StealableAdvances(_gameScreen.Game, diplomat, city).Count > 0)
            {
                choices.Add(($"Steal a secret from {city.Name}", () => OfferToSteal(diplomat, city)));
            }

            // Offered even where it cannot be done, so the refusal can say why:
            // a capital cannot be bought, and that is worth learning once.
            choices.Add(($"Incite a revolt in {city.Name}", () => OfferToIncite(diplomat, city)));
        }

        if (unit != null)
        {
            choices.Add(($"Bribe the {unit.Name}", () => OfferToBribe(diplomat, unit)));
        }

        switch (choices.Count)
        {
            case 0:
                // Something is here, but not something that can be dealt with: a
                // stack keeping an eye on each other, or a garrison that has to be
                // taken with the city.
                _gameScreen.ShowPopup("CANNOTBRIBE");
                return;
            case 1:
                choices[0].Take();
                return;
            default:
                ShowDiplomatChoices(choices);
                return;
        }
    }

    /// <summary>
    /// What this agent can do here. Built from what is actually on the square rather
    /// than read from the game's text, because the list changes with the square --
    /// and because the dialog it used to ask for was never in the text at all, so
    /// arriving at a defended city put up nothing whatever and the Diplomat stood
    /// there having achieved nothing.
    /// </summary>
    private void ShowDiplomatChoices(List<(string Label, Action Take)> choices)
    {
        var elements = new DialogElements
        {
            Name = "DIPLOMATACTION_DYNAMIC",
            Title = "Your Agent Reports",
            Compact = true,
            Width = 260,
            Button = [Labels.Ok, Labels.Cancel],
            Text = ["What are your orders?"],
            LineStyles = [TextStyles.Left],
            Options = new OptionsDefinition { Texts = choices.Select(c => c.Label).ToList() }
        };

        CivDialog? dialog = null;
        dialog = new CivDialog(_gameScreen.Main, elements, (button, selected, _, _) =>
        {
            _gameScreen.CloseDialog(dialog);
            if (button == Labels.Ok && selected >= 0 && selected < choices.Count)
            {
                choices[selected].Take();
            }
        });
        _gameScreen.ShowDialog(dialog, stack: true);
    }

    /// <summary>
    /// Looking inside somebody else's city. A Spy comes home having spent a move; a
    /// Diplomat does not come home, so that is asked about first.
    /// </summary>
    private void OfferToInvestigate(Unit agent, City city)
    {
        if (DiplomatActions.IsSpy(agent))
        {
            Investigate(agent, city);
            return;
        }

        _gameScreen.ShowPopup("INVESTIGATECITY", handleButtonClick: (button, _, _, _) =>
        {
            if (button == Labels.Ok)
            {
                Investigate(agent, city);
            }
        }, replaceStrings: [city.Name]);
    }

    /// <summary>
    /// Taking an advance out of somebody else's city. A city can only be robbed
    /// once, so the refusal has to be able to say that plainly; a Diplomat is spent
    /// on it and a Spy is not, as with a report.
    /// </summary>
    private void OfferToSteal(Unit agent, City city)
    {
        if (!DiplomatActions.CanStealFrom(city))
        {
            _gameScreen.ShowPopup("ALREADYSTOLEN", replaceStrings: [city.Name]);
            return;
        }

        if (DiplomatActions.IsSpy(agent))
        {
            Steal(agent, city);
            return;
        }

        _gameScreen.ShowPopup("STEALTECH", handleButtonClick: (button, _, _, _) =>
        {
            if (button == Labels.Ok)
            {
                Steal(agent, city);
            }
        }, replaceStrings: [city.Name]);
    }

    private void Steal(Unit agent, City city)
    {
        var available = DiplomatActions.StealableAdvances(_gameScreen.Game, agent, city);
        if (available.Count == 0)
        {
            return;
        }

        // Which secret is not the agent's to choose: he takes what he can reach.
        var taken = _gameScreen.Game.Random.ChooseFrom(available);
        if (!DiplomatActions.StealTechnology(_gameScreen.Game, agent, city, taken.Index))
        {
            return;
        }

        SessionLog.Record($"stole {taken.Name} from {city.Name}");
        _gameScreen.ForceRedraw();
        _gameScreen.ShowPopup("STOLENTECH", replaceStrings: [taken.Name, city.Name]);

        if (!agent.AwaitingOrders)
        {
            _gameScreen.Game.ChooseNextUnit();
        }
    }

    private void Investigate(Unit agent, City city)
    {
        if (!DiplomatActions.InvestigateCity(_gameScreen.Game, agent, city))
        {
            return;
        }

        SessionLog.Record($"investigated {city.Name} (size {city.Size})");
        _gameScreen.ForceRedraw();

        // The city as its owner sees it, and nothing in it can be touched. What the
        // player came for is the garrison, which the Units Present box lists.
        _gameScreen.ShowCityWindow(city, viewOnly: true);

        if (!agent.AwaitingOrders)
        {
            _gameScreen.Game.ChooseNextUnit();
        }
    }

    private void OfferToBribe(Unit diplomat, Unit target)
    {
        var cost = DiplomatActions.BribeCost(_gameScreen.Game, target);
        var treasury = diplomat.Owner.Money;
        if (cost > treasury)
        {
            _gameScreen.ShowPopup("NODIPLOMATGOLD", replaceNumbers: [cost, treasury]);
            return;
        }

        _gameScreen.ShowPopup("BRIBEUNIT", handleButtonClick: (button, _, _, _) =>
        {
            if (button != Labels.Ok || diplomat.Dead || target.Dead)
            {
                return;
            }

            if (DiplomatActions.BribeUnit(_gameScreen.Game, diplomat, target))
            {
                _gameScreen.ForceRedraw();
                _gameScreen.Game.ChooseNextUnit();
            }
        }, replaceNumbers: [cost, treasury], replaceStrings: [target.Name]);
    }

    private void OfferToIncite(Unit diplomat, City city)
    {
        if (!DiplomatActions.CanIncite(city))
        {
            _gameScreen.ShowPopup("CANNOTINCITE", replaceStrings: [city.Name]);
            return;
        }

        var cost = DiplomatActions.InciteCost(_gameScreen.Game, city);
        var treasury = diplomat.Owner.Money;
        if (cost > treasury)
        {
            _gameScreen.ShowPopup("NODIPLOMATGOLD", replaceNumbers: [cost, treasury]);
            return;
        }

        _gameScreen.ShowPopup("INCITEREVOLT", handleButtonClick: (button, _, _, _) =>
        {
            if (button != Labels.Ok || diplomat.Dead)
            {
                return;
            }

            if (DiplomatActions.InciteRevolt(_gameScreen.Game, diplomat, city))
            {
                _gameScreen.ForceRedraw();
                _gameScreen.Game.ChooseNextUnit();
            }
        }, replaceNumbers: [cost, treasury],
           replaceStrings: [city.Name, city.Owner.TribeName]);
    }

    /// <summary>
    /// A city has changed hands and is now ours.
    /// <para>
    /// Nothing was said and nothing was shown: the city simply appeared under your
    /// flag on the map, and whether you had taken it or it had merely revolted was
    /// left for you to work out. Civ II names the city and then puts you straight
    /// into it, because the first thing anybody does with a captured city is decide
    /// what it builds and whether its buildings survived.
    /// </para>
    /// </summary>
    public void CityCaptured(City city)
    {
        SessionLog.Record($"captured {city.Name} (size {city.Size})");
        _gameScreen.ShowPopup("CITYCAPTURE",
            handleButtonClick: (_, _, _, _) => _gameScreen.ShowCityWindow(city),
            replaceStrings: [city.Name]);
    }
}
