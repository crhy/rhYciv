using System.Numerics;
using RhyCiv.Engine;
using RhyCiv.Engine.IO;
using RhyCiv.Engine.MapObjects;
using Model;
using Model.Core.Mapping;
using Model.Interface;
using RaylibUI.Initialization;
using Raylib_CSharp;
using Raylib_CSharp.Windowing;
using Raylib_CSharp.Interact;
using Raylib_CSharp.Audio;
using Raylib_CSharp.Rendering;
using Raylib_CSharp.Colors;
using Raylib_CSharp.Images;
using Raylib_CSharp.Shaders;
using Raylib_CSharp.Textures;
using Raylib_CSharp.Transformations;

namespace RaylibUI
{
    public partial class Main : IMain
    {

        private IScreen _activeScreen = null!;
        private bool _shouldClose;


        private Sound Soundman = null!;
        private IUserInterface _activeInterface = null!;
        private RenderTexture2D _colorTarget;
        private string? _colorCorrectionMessage;
        private double _colorCorrectionMessageUntil;

        public Main()
        {
            var hasCivDir = Settings.LoadConfigSettings();

            //========= RAYLIB WINDOW SETTINGS
            // Multisampling is off unless RHYCIV_MSAA=1 asks for it.
            //
            // Every hard crash this game has been reported with -- the process gone,
            // no managed exception, nothing on stderr, which is a fault below .NET --
            // happened with 4x multisampling on, because until recently that was the
            // only way it ran. The one long session played without it ended cleanly,
            // and the next session with it back on crashed inside seven minutes.
            //
            // That is two sessions, which proves nothing on its own. It is acted on
            // because the trade is so one-sided: this is a game of 2D sprites blitted
            // axis-aligned, where multisampling has almost nothing to antialias and
            // costs a little fill rate for it. Giving it up is close to free, and the
            // crashes are not. The switch remains so the question can go on being
            // asked from either side.
            var wantMsaa = Environment.GetEnvironmentVariable("RHYCIV_MSAA") is "1" or "true";
            var flags = ConfigFlags.VSyncHint | ConfigFlags.ResizableWindow;
            if (wantMsaa)
            {
                flags |= ConfigFlags.Msaa4XHint;
            }

            Raylib.SetConfigFlags(flags);
            Window.Init(1600, 900, "rhYciv");

            DisplayScale.Update();
            var appIcon = Image.Load(AssetPaths.Resolve("FOSSart/rhyciv-app-icon.png"));
            Window.SetIcon(appIcon);
            appIcon.Unload();
            //Raylib.SetTargetFPS(60);
            AudioDevice.Init();

            Input.SetExitKey(KeyboardKey.F12);

            Shaders.Load();
            Helpers.LoadFonts();

            if (hasCivDir)
            {
                _activeScreen = SetupMainScreen();
                TryAutoStartGame();
            }
            else
            {
                _activeScreen = new GameFileLocatorScreen(this,() =>
                {
                    hasCivDir = true;
                    _activeScreen = SetupMainScreen();
                }, ShutdownApp);
            }

            //============ LOAD SOUNDS

            //prep this for a loop( should split that function out between loops and non loops)

        }

        private const float pulseTime = 30;

        private List<TimedEvent> _events = new List<TimedEvent>();

        // Frame-time reporting, on when RHYCIV_FRAME_LOG is set to a number of
        // seconds. A game that stops answering the keyboard is nearly always a game
        // whose frames have grown long enough to swallow a keypress between polls,
        // and that is very hard to judge by eye, so the loop can be asked to say how
        // long its frames are actually taking.
        private readonly double _frameLogInterval =
            double.TryParse(Environment.GetEnvironmentVariable("RHYCIV_FRAME_LOG"), out var seconds) && seconds > 0
                ? seconds
                : 0;
        private double _nextFrameReport;
        private int _framesSinceReport;
        private double _frameTimeSinceReport;
        private double _worstFrameSinceReport;

        public void RunLoop()
        {
            var counter = pulseTime;
            var pulse = false;

            while (!Window.ShouldClose() && !_shouldClose)
            {
                DisplayScale.Update();
                var frameTime = Time.GetFrameTime();
                ReportFrameTime(frameTime);

                if (Input.IsKeyPressed(KeyboardKey.F11))
                {
                    Window.ToggleBorderless();
                    DisplayScale.Update();
                }

                for (int i = 0; i < _events.Count; i++)
                {
                    _events[i].Remaining -= frameTime;
                    if (_events[i].Remaining < 0)
                    {
                        // Taken off the list before it runs, not after. Schedule
                        // replaces an entry of the same name in place, so an action
                        // that asks to be run again -- the natural way to write a
                        // repeating job -- wrote its next run into the very slot
                        // that was about to be removed, and never ran a second time.
                        var due = _events[i];
                        _events.RemoveAt(i);
                        i--;
                        due.Action();
                    }
                }

                HandleColorCorrectionKeys();
                var screenWidth = Window.GetScreenWidth();
                var screenHeight = Window.GetScreenHeight();
                if (ColorCorrectionActive())
                {
                    EnsureColorTarget(screenWidth, screenHeight);
                    Graphics.BeginTextureMode(_colorTarget);
                    DrawScene(pulse);
                    Graphics.EndTextureMode();

                    Graphics.BeginDrawing();
                    Graphics.ClearBackground(Color.Black);
                    Graphics.BeginShaderMode(Shaders.ColorCorrection);
                    Graphics.DrawTextureRec(_colorTarget.Texture,
                        new Rectangle(0, 0, screenWidth, -screenHeight), Vector2.Zero, Color.White);
                    Graphics.EndShaderMode();
                    DrawColorCorrectionMessage();
                    Graphics.EndDrawing();
                }
                else
                {
                    Graphics.BeginDrawing();
                    DrawScene(pulse);
                    DrawColorCorrectionMessage();
                    Graphics.EndDrawing();
                }

                CaptureScreenshotIfRequested();

                if (counter++ >= 30)
                {
                    pulse = !pulse;
                    counter = 0;
                }
            }

            ShutdownApp();
        }

        private void ReportFrameTime(float frameTime)
        {
            if (_frameLogInterval <= 0)
            {
                return;
            }

            _framesSinceReport++;
            _frameTimeSinceReport += frameTime;
            _worstFrameSinceReport = Math.Max(_worstFrameSinceReport, frameTime);

            var now = Time.GetTime();
            if (_nextFrameReport == 0)
            {
                _nextFrameReport = now + _frameLogInterval;
                return;
            }

            if (now < _nextFrameReport)
            {
                return;
            }

            var average = _frameTimeSinceReport / Math.Max(1, _framesSinceReport);
            Console.WriteLine(
                $"frames: {_framesSinceReport} in {_frameTimeSinceReport:0.00}s " +
                $"(avg {average * 1000:0.0} ms, worst {_worstFrameSinceReport * 1000:0.0} ms)");

            _nextFrameReport = now + _frameLogInterval;
            _framesSinceReport = 0;
            _frameTimeSinceReport = 0;
            _worstFrameSinceReport = 0;
        }

        // Press F10 to write a PNG of the current frame. Handy for bug reports and
        // for capturing the UI without an external screen-grabber; the directory
        // comes from RHYCIV_SHOT_DIR when set, otherwise the working directory.
        // When RHYCIV_SHOT_INTERVAL (seconds) is also set, a frame is written on
        // that cadence with no keypress - a capture aid for headless UI review.
        private int _screenshotCounter;
        private double _nextAutoShot = -1;

        private void CaptureScreenshotIfRequested()
        {
            var byKey = Input.IsKeyPressed(KeyboardKey.F10);

            var byTimer = false;
            if (double.TryParse(Environment.GetEnvironmentVariable("RHYCIV_SHOT_INTERVAL"),
                    out var interval) && interval > 0)
            {
                var now = Time.GetTime();
                if (_nextAutoShot < 0) _nextAutoShot = now + interval;
                if (now >= _nextAutoShot)
                {
                    byTimer = true;
                    _nextAutoShot = now + interval;
                }
            }

            if (!byKey && !byTimer)
            {
                return;
            }

            var dir = Environment.GetEnvironmentVariable("RHYCIV_SHOT_DIR");
            dir = string.IsNullOrWhiteSpace(dir) ? Environment.CurrentDirectory : dir;
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"rhyciv-{DateTime.Now:yyyyMMdd-HHmmss}-{_screenshotCounter++:00}.png");

            var frame = Image.LoadFromScreen();
            frame.Export(path);
            frame.Unload();
            Console.WriteLine($"screenshot: {path}");

            if (byTimer)
            {
                AutoClickAfterScreenshot();
            }
        }

        private void DrawScene(bool pulse)
        {
            Graphics.BeginMode2D(DisplayScale.Camera);
            _activeScreen.Draw(pulse);
            Graphics.EndMode2D();
        }

        private static bool ColorCorrectionActive() =>
            Math.Abs(Settings.Brightness - 1f) > 0.001f ||
            Math.Abs(Settings.Saturation - 1f) > 0.001f ||
            Math.Abs(Settings.Gamma - 1f) > 0.001f;

        private void EnsureColorTarget(int width, int height)
        {
            if (_colorTarget.IsValid() &&
                _colorTarget.Texture.Width == width && _colorTarget.Texture.Height == height)
            {
                return;
            }

            if (_colorTarget.IsValid())
            {
                _colorTarget.Unload();
            }
            _colorTarget = RenderTexture2D.Load(width, height);
        }

        private void HandleColorCorrectionKeys()
        {
            var modifiers = (Input.IsKeyDown(KeyboardKey.LeftControl) || Input.IsKeyDown(KeyboardKey.RightControl)) &&
                            (Input.IsKeyDown(KeyboardKey.LeftAlt) || Input.IsKeyDown(KeyboardKey.RightAlt));
            if (!modifiers)
            {
                return;
            }

            var brightness = Settings.Brightness;
            var saturation = Settings.Saturation;
            var gamma = Settings.Gamma;
            if (Input.IsKeyPressed(KeyboardKey.Up)) brightness += 0.05f;
            else if (Input.IsKeyPressed(KeyboardKey.Down)) brightness -= 0.05f;
            else if (Input.IsKeyPressed(KeyboardKey.Right)) saturation += 0.1f;
            else if (Input.IsKeyPressed(KeyboardKey.Left)) saturation -= 0.1f;
            else if (Input.IsKeyPressed(KeyboardKey.PageUp)) gamma += 0.05f;
            else if (Input.IsKeyPressed(KeyboardKey.PageDown)) gamma -= 0.05f;
            else if (Input.IsKeyPressed(KeyboardKey.Home)) brightness = saturation = gamma = 1f;
            else return;

            Settings.SetColorCorrection(brightness, saturation, gamma);
            Shaders.SetColorCorrection(Settings.Brightness, Settings.Saturation, Settings.Gamma);
            _colorCorrectionMessage =
                $"Brightness {Settings.Brightness:0.00}  Saturation {Settings.Saturation:0.00}  Gamma {Settings.Gamma:0.00}";
            _colorCorrectionMessageUntil = Time.GetTime() + 2.5;
        }

        private void DrawColorCorrectionMessage()
        {
            if (_colorCorrectionMessage == null || Time.GetTime() > _colorCorrectionMessageUntil)
            {
                return;
            }

            Graphics.BeginMode2D(DisplayScale.Camera);
            const int fontSize = 18;
            var textSize = TextRendering.Measure(Fonts.Arial, _colorCorrectionMessage, fontSize, 0f);
            var width = (int)MathF.Ceiling(textSize.X) + 20;
            var height = (int)MathF.Ceiling(textSize.Y) + 10;
            Graphics.DrawRectangle(8, 8, width, height, new Color(0, 0, 0, 210));
            TextRendering.Draw(Fonts.Arial, _colorCorrectionMessage, new Vector2(18, 13),
                fontSize, 0f, Color.White);
            Graphics.EndMode2D();
        }

        private MainMenu SetupMainScreen()
        {
            //Helpers.LoadFonts();
            Interfaces = Helpers.LoadInterfaces(this);
            AllRuleSets =  Interfaces.SelectMany((userInterface, idx) =>
                {
                    userInterface.InterfaceIndex = idx;
                    var sets = userInterface.FindRuleSets(Settings.SearchPaths);

                    foreach (var ruleset in sets)
                    {
                        ruleset.InterfaceIndex = idx;
                    }
                    return sets;
                })
                .ToArray();
            ActiveInterface = Helpers.GetInterface(Settings.GameDataPath, Interfaces, AllRuleSets);
            return new MainMenu(this,() => _shouldClose= true, StartGame, Soundman);
        }



        public IUserInterface ActiveInterface
        {
            get => _activeInterface;
            private set
            {
                if(value == _activeInterface) return;

                _activeInterface = value;

                ActiveRuleSet ??= AllRuleSets.First(r => r.InterfaceIndex == _activeInterface.InterfaceIndex);

                _activeInterface.Initialize();
                TextureCache.Clear();
                Labels.UpdateLabels(ActiveRuleSet);
                ImageUtils.SetLook(_activeInterface);
                Soundman?.Dispose();
                Soundman = new Sound(_activeInterface.Title);
                _activeScreen?.InterfaceChanged(Soundman);
            }
        }

        public IList<IUserInterface> Interfaces { get; set; } = [];

        void ShutdownApp()
        {
            if (_colorTarget.IsValid())
            {
                _colorTarget.Unload();
            }
            Shaders.Unload();
            Soundman?.Dispose();
            Window.Close();
            AudioDevice.Close();
        }

        /// <summary>
        /// Closes the game after the current frame.
        /// </summary>
        /// <remarks>
        /// The only way out used to be the main menu's own exit, so "Quit" from
        /// inside a game put the player at the menu and left them to quit a second
        /// time. Civ II's Quit leaves the game, and being asked which you meant is
        /// better than being given only the one nobody asked for.
        /// </remarks>
        public void Close()
        {
            _shouldClose = true;
        }

        public void ReloadMain()
        {
            ActiveRuleSet = AllRuleSets.First(r => r.InterfaceIndex == _activeInterface.InterfaceIndex);
            TextureCache.Clear();
            ImageUtils.SetLook(_activeInterface);
            _activeScreen = new MainMenu(this,() => _shouldClose= true, StartGame, Soundman);
        }

        public void Schedule(string eventName, TimeSpan delay, Action action)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Name == eventName)
                {
                    _events[i] = new TimedEvent(name: eventName, remaining: (float)delay.TotalSeconds, action: action);
                    return;
                }
            }
            _events.Add(new TimedEvent(name: eventName, remaining: (float)delay.TotalSeconds, action: action));
        }

        public void ClearSchedule(string eventName)
        {
            _events.RemoveAll(e => e.Name == eventName);
        }
    }

    internal class TimedEvent(string name, float remaining, Action action)
    {
        public float Remaining { get; set; } = remaining;
        public string Name { get; set; } = name;
        public Action Action { get; set; } = action;
    }
}
