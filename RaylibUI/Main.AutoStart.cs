using System;
using System.Collections.Generic;
using System.Linq;
using RhyCiv.Engine.MapObjects;
using RhyCiv.UI.Classic;
using RhyCiv.Engine;
using RhyCiv.Engine.Advances;
using RhyCiv.Engine.NewGame;
using Model.Controls;
using CivInit = RhyCiv.UI.Classic.Rules.Initialization;

namespace RaylibUI
{
    public partial class Main
    {
        // Boot straight into a freshly generated game, skipping every new-game
        // dialog, when RHYCIV_AUTOSTART is set. Intended for UI review and
        // screenshot capture of live gameplay without a human at the menu.
        //   RHYCIV_AUTOSTART=1        enable
        //   RHYCIV_AUTOSTART_SEED=N   deterministic map/start (default: time-based)
        //   RHYCIV_AUTOSTART_CIVS=N   number of rival civilisations (default: max)
        //   RHYCIV_AUTOSTART_ZOOM=N   initial map zoom, -7..32 (default: -1)
        //   RHYCIV_AUTOSTART_REVEAL=1 reveal the whole map (terrain review)
        //   RHYCIV_AUTOSTART_LOAD=PATH open a saved game instead of generating one
        private bool TryAutoStartGame()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART")))
            {
                return false;
            }

            // A player's own save, opened without a human at the load dialog. Almost
            // every report that is worth reproducing comes with one, and playing back
            // to the same position by hand is usually not possible at all -- there is
            // no way to reach turn 162 of somebody else's game.
            var savePath = Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_LOAD");
            if (!string.IsNullOrWhiteSpace(savePath))
            {
                if (!File.Exists(savePath))
                {
                    Console.WriteLine($"autostart: no save at '{savePath}'");
                    return false;
                }

                Console.WriteLine($"autostart: loading {savePath}");
                var loaded = RhyCiv.Engine.SaveLoad.LoadGame.LoadFrom(savePath, this);

                // Loading ends on the "you are back in the year such and such"
                // dialog, which a player dismisses to reach the map. Answer it here
                // so the game comes up playing rather than waiting on a button.
                if (loaded is Model.InterfaceActions.MenuAction menu)
                {
                    loaded = ActiveInterface.ProcessDialog(menu.DialogElement.Name!,
                        new Model.Controls.DialogResult("Ok", 0));
                }

                if (loaded is not Model.InterfaceActions.StartGame loadedGame)
                {
                    Console.WriteLine($"autostart: loading '{savePath}' did not produce a game");
                    return false;
                }

                var loadedMap = loadedGame.Game.Maps[0];
                Console.WriteLine($"autostart: loaded turn {loadedGame.Game.TurnNumber}, " +
                                  $"{loadedMap.XDim}x{loadedMap.YDim} world, " +
                                  $"player '{loadedGame.Game.GetPlayerCiv.TribeName}' " +
                                  $"with {loadedGame.Game.GetPlayerCiv.Cities.Count} cities");
                StartGame(loadedGame.Game, loadedGame.ViewData);
                ReportGameState(loadedGame.Game);
                StartZoomSweep();
                StartWindowChurn();
                return true;
            }

            if (ActiveInterface is not ClassicInterface civ2)
            {
                Console.WriteLine("autostart: active interface is not the Civ2 interface; skipping");
                return false;
            }

            // RHYCIV_AUTOSTART=quick exercises the menu's Quick start entry rather
            // than this harness's own settings, so that path can be checked without
            // clicking through the menu.
            if (string.Equals(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART"), "quick",
                    StringComparison.OrdinalIgnoreCase))
            {
                var quickAction = civ2.StartInstantGame();
                if (quickAction is not Model.InterfaceActions.StartGame started)
                {
                    Console.WriteLine("autostart: quick start did not produce a game");
                    return false;
                }

                var quickGame = started.Game;
                Console.WriteLine(
                    $"autostart: quick start, {quickGame.Maps[0].XDim}x{quickGame.Maps[0].YDim} world, " +
                    $"{quickGame.AllCivilizations.Count} civs incl. barbarians, " +
                    $"player '{quickGame.GetPlayerCiv.TribeName}', difficulty {quickGame.DifficultyLevel}, " +
                    $"units {quickGame.GetPlayerCiv.Units.Count}");
                CivInit.ViewData = new Dictionary<string, string?>
                    { ["Zoom"] = Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_ZOOM") ?? "0" };
                StartGame(quickGame, CivInit.ViewData);

                var quickCityValue = Environment.GetEnvironmentVariable("RHYCIV_TEST_CITY");
                if (!string.IsNullOrWhiteSpace(quickCityValue)
                    && _activeScreen is RunGame.GameScreen quickScreen)
                {
                    RunCityFoundingHarness((Game)quickGame, quickScreen,
                        int.TryParse(quickCityValue, out var quickWanted) ? Math.Max(1, quickWanted) : 1);
                }

                return true;
            }

            CivInit.LoadGraphicsAssets(civ2);

            var config = CivInit.ConfigObject;
            config.Random = int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_SEED"), out var seed)
                ? new FastRandom(seed)
                : new FastRandom();
            config.QuickStart = true;
            // RHYCIV_AUTOSTART_SIZE=75x120 generates at a named size, so a world can
            // be measured against a Civ II save of the same dimensions.
            config.WorldSize = new[] { 50, 80 };
            if (Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_SIZE") is { } sizeWanted &&
                sizeWanted.Split('x') is { Length: 2 } parts &&
                int.TryParse(parts[0], out var wide) && int.TryParse(parts[1], out var high))
            {
                config.WorldSize = new[] { wide, high };
            }
            config.BarbarianActivity = 1;
            config.DifficultyLevel = 2;

            var maxCivs = civ2.PlayerColours.Length - 1;
            config.NumberOfCivs = int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_CIVS"), out var civs)
                ? Math.Clamp(civs + 1, 2, maxCivs)
                : maxCivs;

            config.PlayerCiv = CivInit.MakeCivilization(config, config.Rules.Leaders[0], true, 1);
            config.Gender = config.PlayerCiv.LeaderGender;

            CivInit.CompleteConfig();

            var maps = MapGenerator.GenerateMap(config).GetAwaiter().GetResult();
            var game = NewGameInitialisation.StartNewGame(config, maps, config.Civilizations,
                civ2.MainApp.ActiveRuleSet.Paths);
            CivInit.Start(game);

            var zoom = int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_ZOOM"), out var z) ? z : -1;
            CivInit.ViewData = new Dictionary<string, string?> { ["Zoom"] = zoom.ToString() };

            var reveal = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_AUTOSTART_REVEAL"));
            if (reveal)
            {
                foreach (var map in game.Maps)
                {
                    map.MapRevealed = true;
                }
            }

            Console.WriteLine(
                $"autostart: generated {config.WorldSize[0]}x{config.WorldSize[1]} world, " +
                $"{config.Civilizations.Count} civs, player '{game.GetPlayerCiv.TribeName}', zoom {zoom}");

            StartGame(game, CivInit.ViewData);

            // The report was only ever reached by the load path, so asking a freshly
            // generated world what it was made of printed nothing at all -- which is
            // exactly the comparison a generated world is wanted for.
            ReportGameState(game);

            if (reveal && _activeScreen is RunGame.GameScreen gameScreen)
            {
                gameScreen.TileCache.Clear();
                gameScreen.MapControl.ForceRedraw = true;
            }

            // RHYCIV_TEST_CITY=N founds N cities and opens the last one's city
            // screen, so crashes on that path can be reproduced without playing to
            // them. N defaults to 1. Each city after the first is founded from a
            // separate settler walked far enough away to clear the adjacency rule,
            // which is the path the second-city crash report describes.
            var testCityValue = Environment.GetEnvironmentVariable("RHYCIV_TEST_CITY");
            if (!string.IsNullOrWhiteSpace(testCityValue)
                && _activeScreen is RunGame.GameScreen cityScreen)
            {
                var wanted = int.TryParse(testCityValue, out var count) ? Math.Max(1, count) : 1;
                RunCityFoundingHarness(game, cityScreen, wanted);
            }

            // RHYCIV_TEST_DIPLOMACY=1 meets every civilisation and opens the
            // Foreign Ministry, so the parley and its portrait can be looked at
            // without playing until somebody walks into somebody.
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_DIPLOMACY"))
                && _activeScreen is RunGame.GameScreen diploScreen)
            {
                var mine = game.GetPlayerCiv;
                foreach (var other in game.AllCivilizations.Where(c =>
                             c != mine && c.PlayerType != Model.Core.PlayerType.Barbarians))
                {
                    // The contact flag directly, not MakeContact: that announces the
                    // meeting, and seven announcements queue in front of the screen
                    // this is here to look at.
                    RhyCiv.Engine.Diplomacy.DiplomacyFunctions.Between(mine, other).Contact = true;
                    RhyCiv.Engine.Diplomacy.DiplomacyFunctions.Between(other, mine).Contact = true;
                    Console.WriteLine($"diplomacy: met {other.TribeName} " +
                                      $"({RhyCiv.Engine.Diplomacy.DiplomacyFunctions.AttitudeName(other, mine)}) " +
                                      $"portrait={(RhyCiv.Engine.Diplomacy.LeaderPortraits.For(other)?.GetKey() ?? "none")}");
                }

                // RHYCIV_TEST_DIPLOMACY=<tribe> opens the audience with that tribe
                // directly, rather than the ministry's list, so the parley and its
                // portrait can be looked at.
                var wanted = Environment.GetEnvironmentVariable("RHYCIV_TEST_DIPLOMACY");
                if (!string.Equals(wanted, "1", StringComparison.Ordinal) &&
                    game.AllCivilizations.FirstOrDefault(c =>
                        c.TribeName.StartsWith(wanted!, StringComparison.OrdinalIgnoreCase)) is { } target)
                {
                    foreach (var other in game.AllCivilizations.Where(c => c != mine && c != target))
                    {
                        RhyCiv.Engine.Diplomacy.DiplomacyFunctions.Between(mine, other).Contact = false;
                    }
                }

                diploScreen.RunCommand(CommandIds.Diplomacy);
            }

            // RHYCIV_TEST_POPUP=NAME[,NAME...] pops the named GAME.TXT dialog(s)
            // right after start, with placeholder text, so prompt layout can be
            // reviewed without playing to the event that triggers it.
            var testPopups = Environment.GetEnvironmentVariable("RHYCIV_TEST_POPUP");
            if (!string.IsNullOrWhiteSpace(testPopups) && _activeScreen is RunGame.GameScreen screen)
            {
                var fillers = new List<string>
                {
                    game.GetPlayerCiv.TribeName, "Babylon", "the Wonder of the Ages",
                    "an aqueduct", "scholars", "the Hanging Gardens", "Marketplace",
                };
                foreach (var name in testPopups.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    screen.ShowPopup(name, replaceStrings: fillers,
                        replaceNumbers: new List<int> { 42, 120, 7 });
                }
            }

            return true;
        }

        /// <summary>
        /// Prints what this game works out for every city, when RHYCIV_REPORT is
        /// set, and quits.
        /// </summary>
        /// <remarks>
        /// For comparing against the original. Civilization II's own save files
        /// load here directly, so the same position can be opened in both games --
        /// and Civ II states its numbers on the city screen, where they can be read
        /// off and set beside these. A disagreement names the formula that is
        /// wrong, which is a great deal more use than an anecdote about a battle.
        ///
        /// Tab-separated, so it can be pasted into anything that takes a table.
        /// </remarks>
        private void ReportGameState(Model.Core.IGame game)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_REPORT")))
            {
                return;
            }

            var civ = game.GetPlayerCiv;
            Console.WriteLine($"# rhYciv report: turn {game.TurnNumber}, {game.Date.GameYearString(game.TurnNumber)}");
            Console.WriteLine($"# player {civ.TribeName}  gold {civ.Money}  science {civ.Science}  " +
                              $"tax {civ.TaxRate}%  sci-rate {civ.ScienceRate}%  government {civ.Government}  " +
                              $"difficulty {game.DifficultyLevel}");
            Console.WriteLine(string.Join("\t",
                "city", "owner", "size", "food", "eaten", "surplus", "stored", "box",
                "shields", "support", "waste", "production", "trade", "corruption", "tax", "science",
                "worked", "specialists", "tile-trade", "route-trade",
                "happy", "content", "unhappy", "disorder", "improvements"));

            foreach (var city in game.AllCities.OrderBy(c => c.OwnerId).ThenBy(c => c.Name))
            {
                city.CalculateOutput(city.Owner.Government, game);
                var happy = city.CalculateHappiness(game);
                Console.WriteLine(string.Join("\t",
                    city.Name, city.Owner.TribeName, city.Size,
                    city.FoodProduction, city.FoodConsumption, city.SurplusHunger,
                    city.FoodInStorage, (city.Size + 1) * game.Rules.Cosmic.RowsFoodBox,
                    city.TotalProduction, city.Support, city.Waste, city.Production,
                    city.Trade, city.Corruption, city.GetTax(), city.GetScience(),
                    city.WorkedTiles.Count, city.NoOfSpecialistsx4 / 4,
                    city.WorkedTiles.Sum(t => t.GetTrade(city.GetOrganizationLevel(game.Rules))),
                    RhyCiv.Engine.UnitActions.CaravanActions.TradeFromRoutes(game, city),
                    happy.HappyCitizens, happy.ContentCitizens, happy.UnhappyCitizens,
                    happy.IsInDisorder,
                    city.Improvements.Count == 0 ? "-" : string.Join("+", city.Improvements.Select(i => i.Name))));
            }

            // RHYCIV_REPORT_CITY=NAME also lists that city's worked squares, which
            // is what a disagreement about food or trade comes down to.
            var detail = Environment.GetEnvironmentVariable("RHYCIV_REPORT_CITY");
            if (!string.IsNullOrWhiteSpace(detail))
            {
                // A comma-separated list, because a city's arrangement can only be
                // judged against its neighbours': two cities may not work the same
                // square, and a square already taken is the usual reason a city is
                // working land that looks worse than what is lying next to it.
                var wanted = detail.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                               StringSplitOptions.TrimEntries);
                var claimed = new Dictionary<string, string>();
                foreach (var name in wanted)
                {
                    if (game.AllCities.FirstOrDefault(c =>
                            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) is not { } detailed)
                    {
                        Console.WriteLine($"# no city called {name}");
                        continue;
                    }

                    var org = detailed.GetOrganizationLevel(game.Rules);
                    var low = org == 0;
                    Console.WriteLine($"# worked squares of {detailed.Name} at " +
                                      $"({detailed.Location.X},{detailed.Location.Y}), size {detailed.Size}, " +
                                      $"{detailed.NoOfSpecialistsx4 / 4} specialists, " +
                                      $"{game.GetActiveCiv.Government}");
                    // Civ II lists a city's trade routes at the foot of its screen as
                    // "Cardiff Gems: +2", so the partner, the commodity and the
                    // arrows it pays are all comparable directly.
                    foreach (var route in detailed.TradeRoutes)
                    {
                        var partner = route.Destination >= 0 && route.Destination < game.AllCities.Count
                            ? game.AllCities[route.Destination].Name
                            : $"#{route.Destination}";
                        Console.WriteLine($"#   route to {partner}, commodity {route.Commodity}, " +
                                          $"worth {RhyCiv.Engine.UnitActions.CaravanActions.TradeFromRoutes(game, detailed)} " +
                                          $"in total for {detailed.TradeRoutes.Length} route(s)");
                    }

                    Console.WriteLine(string.Join("\t", "x", "y", "dx", "dy", "terrain", "special",
                        "food", "shields", "trade", "alsoWorkedBy"));
                    foreach (var square in detailed.WorkedTiles)
                    {
                        var key = $"{square.X},{square.Y}";
                        var clash = claimed.TryGetValue(key, out var other) ? other : "-";
                        claimed[key] = detailed.Name;
                        Console.WriteLine(string.Join("\t",
                            square.X, square.Y,
                            square.X - detailed.Location.X, square.Y - detailed.Location.Y,
                            square.Type, square.SpecialsName ?? "-",
                            square.GetFood(low), square.GetShields(low), square.GetTrade(org),
                            clash));
                    }
                }
            }

            // RHYCIV_REPORT_MAP=1 prints what the world is made of: how much of it
            // is land, and how that land is divided. Civ II's own saves load here,
            // so the same line printed for one of those and for a generated world
            // is a direct comparison of the two generators.
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_REPORT_MAP")))
            {
                foreach (var map in game.Maps)
                {
                    var tiles = map.Tile.Cast<Model.Core.Mapping.Tile>()
                        .Where(t => t != null).ToList();
                    var ocean = tiles.Count(t => t.Type == Model.Core.Mapping.TerrainType.Ocean);
                    var land = tiles.Count - ocean;
                    var rivers = tiles.Count(t => t.River);
                    Console.WriteLine($"map: {map.XDim}x{map.YDim} = {tiles.Count} tiles, " +
                                      $"land {land} ({100.0 * land / Math.Max(1, tiles.Count):0.0}%), " +
                                      $"ocean {ocean} ({100.0 * ocean / Math.Max(1, tiles.Count):0.0}%), " +
                                      $"river {rivers} ({100.0 * rivers / Math.Max(1, land):0.0}% of land)");
                    foreach (var group in tiles.Where(t => t.Type != Model.Core.Mapping.TerrainType.Ocean)
                                 .GroupBy(t => t.Type).OrderByDescending(g => g.Count()))
                    {
                        Console.WriteLine($"map:   {group.Key,-10} {group.Count(),6} " +
                                          $"({100.0 * group.Count() / Math.Max(1, land):0.0}% of land)");
                    }
                }
            }

            Console.Out.Flush();
            _shouldClose = true;
        }

        /// <summary>
        /// Opens and closes the city window over and over when RHYCIV_TEST_CHURN is
        /// set, so a leak of the textures a window paints for itself shows up in a
        /// minute instead of an afternoon.
        /// <para>
        /// This is how the hard crashes were finally pinned down: raylib reports
        /// every texture it loads and unloads, so a session's captured output can be
        /// counted, and a window that gives nothing back on the way out shows as a
        /// load with no matching unload.
        /// </para>
        /// </summary>
        private void StartWindowChurn()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_CHURN")))
            {
                return;
            }

            const string churn = "WINDOW_CHURN";
            var opened = 0;

            void Step()
            {
                if (_activeScreen is not RunGame.GameScreen screen ||
                    screen.Game.GetPlayerCiv.Cities.Count == 0)
                {
                    return;
                }

                var window = screen.ShowCityWindow(screen.Game.GetPlayerCiv.Cities[0]);
                screen.CloseDialog(window);
                opened++;
                Console.WriteLine($"window-churn: {opened}");
                Schedule(churn, TimeSpan.FromMilliseconds(250), Step);
            }

            Schedule(churn, TimeSpan.FromSeconds(3), Step);
        }

        /// <summary>
        /// Steps the zoom up one level every couple of seconds when
        /// RHYCIV_TEST_ZOOM_SWEEP is set, so a run of screenshots covers every zoom
        /// level in order.
        /// <para>
        /// Zoom faults are about what changes between one level and the next --
        /// a map that shifts sideways as it is zoomed cannot be seen in any single
        /// frame, only in the step from one to the one after. Starting the game at
        /// a fixed zoom shows neither.
        /// </para>
        /// </summary>
        private void StartZoomSweep()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_ZOOM_SWEEP")))
            {
                return;
            }

            const string sweep = "ZOOM_SWEEP";

            // The point the sweep holds still, in the map view's own coordinates.
            var ZoomSweepPointer = new System.Numerics.Vector2(
                int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_ZOOM_X"), out var sweepX) ? sweepX : 300,
                int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_ZOOM_Y"), out var sweepY) ? sweepY : 250);

            // From the far end, so the sweep crosses the point where the whole map
            // stops fitting across the screen -- which is where the view used to
            // jump sideways.
            var from = int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_ZOOM_FROM"), out var start0)
                ? start0
                : RunGame.GameScreen.MinimumZoom;
            if (_activeScreen is RunGame.GameScreen start)
            {
                start.TriggerMapEvent(new RhyCiv.Engine.Events.MapEventArgs(
                    RhyCiv.Engine.Enums.MapEventType.ZoomChange) { Zoom = from });
            }

            void Step()
            {
                if (_activeScreen is not RunGame.GameScreen screen)
                {
                    return;
                }

                var next = screen.Zoom + 1;
                if (next > RunGame.GameScreen.MaximumZoom)
                {
                    return;
                }

                // Through the same path the wheel uses, about a fixed point well
                // away from the middle: a zoom that holds the pointer still and one
                // that recentres look identical in the middle of the view and
                // nowhere else.
                var before = screen.MapControl.TileAtViewPosition(ZoomSweepPointer);
                screen.MapControl.ZoomAbout(ZoomSweepPointer, next);
                var after = screen.MapControl.TileAtViewPosition(ZoomSweepPointer);
                Console.WriteLine($"zoom-sweep: {next} under-pointer " +
                                  $"{(before == null ? "-" : $"{before.X},{before.Y}")} -> " +
                                  $"{(after == null ? "-" : $"{after.X},{after.Y}")}" +
                                  (before != null && after != null && (before.X != after.X || before.Y != after.Y)
                                      ? "  MOVED" : ""));
                Schedule(sweep, TimeSpan.FromSeconds(2), Step);
            }

            Schedule(sweep, TimeSpan.FromSeconds(2), Step);
        }

        /// <summary>
        /// Founds <paramref name="wanted"/> cities for the human player, reporting each
        /// step so an unhandled exception can be pinned to the city it happened on.
        /// </summary>
        private static void RunCityFoundingHarness(Game game, RunGame.GameScreen screen, int wanted)
        {
            var civ = game.GetPlayerCiv;
            Model.Core.Cities.City? last = null;

            for (var founded = 0; founded < wanted; founded++)
            {
                var settler = civ.Units.FirstOrDefault(u =>
                    !u.Dead && u.AiRole == Model.Constants.AiRoleType.Settle);
                if (settler == null)
                {
                    Console.WriteLine($"test-city: no settler left after {founded} cities");
                    break;
                }

                // Walk clear of every existing city: founding adjacent to one is
                // rejected, and the walk itself exercises the movement path.
                var attempts = 0;
                while (attempts++ < 40 && !CanFoundHere(settler.CurrentLocation, civ))
                {
                    var step = settler.CurrentLocation.Neighbours()
                        .FirstOrDefault(t => t.Type != Model.Core.Mapping.TerrainType.Ocean &&
                                             !t.Terrain.Impassable);
                    if (step == null)
                    {
                        break;
                    }

                    settler.X = step.X;
                    settler.Y = step.Y;
                    settler.CurrentLocation = step;
                }

                if (!CanFoundHere(settler.CurrentLocation, civ))
                {
                    Console.WriteLine($"test-city: could not find a legal site for city {founded + 1}");
                    break;
                }

                var name = RhyCiv.Engine.UnitActions.CityActions.GetCityName(civ, game);
                Console.WriteLine($"test-city: founding city {founded + 1} '{name}' at " +
                                  $"{settler.CurrentLocation.X},{settler.CurrentLocation.Y}");
                last = RhyCiv.Engine.UnitActions.CityActions.BuildCity(settler, game, name);
                Console.WriteLine($"test-city: founded '{last.Name}' size {last.Size}, production " +
                                  (last.ItemInProduction == null ? "<null>" : last.ItemInProduction.ToString()));

                // Everything the real order does after BuildCity.
                last.Location.SetVisible(civ.Id);
                last.Location.UpdatePlayer(civ.Id);
                Console.WriteLine($"test-city: city {founded + 1} tile published");
            }

            if (last != null)
            {
                // RHYCIV_TEST_CITY_SIZE grows the city before its window opens, so the
                // citizen row has something in it to inspect.
                if (int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_CITY_SIZE"), out var size)
                    && size > last.Size)
                {
                    while (last.Size < size)
                    {
                        last.Size++;
                        last.AutoAddDistributionWorkers(game.Rules);
                    }

                    last.CalculateOutput(last.Owner.Government, game);
                    Console.WriteLine($"test-city: grown to size {last.Size}");
                }

                if (int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_CITY_SHIELDS"), out var sh))
                {
                    last.ShieldsProgress = sh;
                    Console.WriteLine($"test-city: shields set to {last.ShieldsProgress}/{last.ItemInProduction.Cost}");
                }

                // RHYCIV_TEST_FORTIFY=1 fortifies the garrison, which is the state
                // the Units Present row is hardest to reach by hand and easiest to
                // get wrong: the fortification marker is drawn beside a sprite a
                // fraction of the size the map draws it at.
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_FORTIFY")))
                {
                    // Everything still alive is walked into the city first: founding
                    // spends the settler, so a city on the turn it is founded has an
                    // empty garrison and nothing to draw.
                    foreach (var garrison in civ.Units.Where(u => !u.Dead))
                    {
                        garrison.X = last.Location.X;
                        garrison.Y = last.Location.Y;
                        garrison.CurrentLocation = last.Location;
                        garrison.Order = (int)RhyCiv.Engine.Enums.OrderType.Fortified;
                    }
                    Console.WriteLine($"test-city: fortified {last.UnitsInCity.Count} units in the city");
                }

                Console.WriteLine($"test-city: units in city {last.UnitsInCity.Count} " +
                                  $"[{string.Join(", ", last.UnitsInCity.Select(u => $"{u.Name} dead={u.Dead} home={(u.HomeCity == null ? "NONE" : u.HomeCity.Name)}"))}]");
                Console.WriteLine($"test-city: supported {last.SupportedUnits.Count} " +
                                  $"[{string.Join(", ", last.SupportedUnits.Select(u => $"{u.Name} dead={u.Dead}"))}]");

                // RHYCIV_TEST_NO_UNITS=1 ends every unit's turn and asks for the
                // next one. That is the state reached after founding a city with
                // the last settler, and it is where ChooseNextUnit has nothing to
                // hand on to.
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_NO_UNITS")))
                {
                    foreach (var unit in civ.Units)
                    {
                        unit.MovePointsLost = unit.MaxMovePoints;
                    }
                    Console.WriteLine("test-city: all units spent; asking for the next unit");
                    game.ChooseNextUnit();
                    Console.WriteLine("test-city: ChooseNextUnit returned");
                }

                var openedWindow = screen.ShowCityWindow(last);
                Console.WriteLine("test-city: city window opened");

                // RHYCIV_TEST_PRODUCTION=1 opens the Change Production list on top,
                // optionally after granting the first N advances so the list carries
                // more than a settler and a warrior. The list is the hardest part of
                // the city screen to reach by hand and the easiest to get wrong.
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RHYCIV_TEST_PRODUCTION")))
                {
                    if (int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_ADVANCES"), out var advances))
                    {
                        for (var index = 0; index < Math.Min(advances, game.Rules.Advances.Length); index++)
                        {
                            game.GiveAdvance(index, civ);
                        }
                        Console.WriteLine($"test-city: granted {advances} advances");
                    }

                    openedWindow.ShowChangeProduction();
                    Console.WriteLine("test-city: change production dialog opened");
                }
            }

            // Founding the last settler's city leaves nobody awaiting orders, so the
            // real order's ChooseNextUnit call runs the first end of turn. That is
            // the step the second-city crash report actually reaches, and skipping
            // it is why this harness could not reproduce the report before.
            Console.WriteLine("test-city: choosing next unit (may end the turn)");
            game.ChooseNextUnit();
            Console.WriteLine($"test-city: next unit chosen, turn {game.TurnNumber}");

            // With the last settler spent nobody is awaiting orders, so the player's
            // next act is to end the turn. RHYCIV_TEST_TURNS=N runs that many.
            var turns = int.TryParse(Environment.GetEnvironmentVariable("RHYCIV_TEST_TURNS"), out var t)
                ? t
                : 0;
            for (var i = 0; i < turns; i++)
            {
                Console.WriteLine($"test-city: ending turn {game.TurnNumber}");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (game.ProcessEndOfTurn())
                {
                    game.ChoseNextCiv();
                }
                sw.Stop();

                // The turn number and whose turn it now is, because a turn that does
                // not come back round to the player is the failure this harness
                // exists to catch, and it is invisible from the turn number alone.
                Console.WriteLine($"test-city: turn is now {game.TurnNumber} in {sw.ElapsedMilliseconds} ms, " +
                                  $"active {game.GetActiveCiv.TribeName}" +
                                  (game.GetActiveCiv == game.GetPlayerCiv ? " (the player)" : " (NOT the player)"));
                foreach (var c in civ.Cities)
                {
                    Console.WriteLine($"test-city:   {c.Name} size {c.Size} shields {c.ShieldsProgress}" +
                                      $"/{c.ItemInProduction.Cost} producing {c.ItemInProduction.GetDescription()}" +
                                      $" (+{c.Production}/turn) disorder={c.CivilDisorder}");
                }

                Console.WriteLine($"test-city:   units {civ.Units.Count(u => !u.Dead)}");

                // What the world looks like, not just this civilisation: whether
                // anybody is improving their land, and how much pollution is
                // building up, are only visible from here.
                var squares = game.Maps.SelectMany(m => m.Tile.Cast<Model.Core.Mapping.Tile>())
                    .Where(t => t != null).ToList();
                Console.WriteLine($"test-city:   world: {squares.Count(t => t.Improvements.Count > 0)} improved, " +
                                  $"{RhyCiv.Engine.PollutionFunctions.PollutedSquares(game).Count} polluted");
            }
        }

        // RHYCIV_AUTOCLICK="First Button,Second Button" presses one named button on
        // whatever window is in front, once per timed screenshot. It is how the
        // later pages of a nested dialog -- the second and third steps of a parley,
        // say -- get captured headlessly: each shot records a page, and the click
        // that follows it moves on to the next one. A name that is not on screen is
        // reported and skipped, so a run does not silently photograph the same page
        // several times over.
        private Queue<string>? _autoClicks;

        private void AutoClickAfterScreenshot()
        {
            if (_autoClicks == null)
            {
                var wanted = Environment.GetEnvironmentVariable("RHYCIV_AUTOCLICK");
                _autoClicks = new Queue<string>(string.IsNullOrWhiteSpace(wanted)
                    ? []
                    : wanted.Split(',', StringSplitOptions.RemoveEmptyEntries |
                                        StringSplitOptions.TrimEntries));
            }

            if (_autoClicks.Count == 0 || _activeScreen is not BaseScreen screen)
            {
                return;
            }

            var wantedButton = _autoClicks.Dequeue();
            var dialog = screen.TopDialog;
            var button = dialog is null ? null : FindButton(dialog.Controls, wantedButton);
            if (button is null)
            {
                Console.WriteLine($"autoclick: no button '{wantedButton}' on screen");
                return;
            }

            Console.WriteLine($"autoclick: {wantedButton}");
            button.PerformClick();
        }

        private static Controls.Button? FindButton(IEnumerable<IControl>? controls, string text)
        {
            if (controls is null)
            {
                return null;
            }

            foreach (var control in controls)
            {
                if (control is Controls.Button button &&
                    string.Equals(button.Text, text, StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }

                if (FindButton(control.Controls, text) is { } nested)
                {
                    return nested;
                }
            }

            return null;
        }

        private static bool CanFoundHere(Model.Core.Mapping.Tile tile, Model.Core.Civilization civ)
        {
            if (tile.Type == Model.Core.Mapping.TerrainType.Ocean || tile.Terrain.Impassable ||
                tile.CityHere != null)
            {
                return false;
            }

            return !tile.Neighbours().Any(t => t.IsCityPresent);
        }

    }
}
