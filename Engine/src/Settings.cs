using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Model.Utils;

namespace RhyCiv.Engine
{
    public class Settings
    {
        private static string SettingsFilePath => Path.Combine(ApplicationDataFolder, SettingsFileName);

        /// <summary>
        /// The directory this build reads and writes. Before the project was
        /// deforked the same data lived under <see cref="LegacyDataFolderName"/>;
        /// <see cref="MigrateLegacyDataFolder"/> moves it across once.
        /// </summary>
        private const string DataFolderName = "rhYciv";

        /// <summary>Pre-defork name of <see cref="DataFolderName"/>.</summary>
        private const string LegacyDataFolderName = "AxxCiv";

        /// <summary>
        /// Where the player's settings live. Overridable so the tests can exercise
        /// saving without writing into the player's own configuration; nothing else
        /// reassigns it.
        /// </summary>
        internal static Func<string> DataFolder { get; set; } =
            () => Path.Combine(GetLocalAppDataFolder(), DataFolderName);

        private static string ApplicationDataFolder => DataFolder();

        private static string LegacyApplicationDataFolder =>
            Path.Combine(GetLocalAppDataFolder(), LegacyDataFolderName);

        /// <summary>
        /// Moves saves, logs and settings written by a pre-defork build into the
        /// current data directory, once, on first launch. It runs before anything
        /// reads the folder and is deliberately best-effort: a player whose old
        /// directory cannot be moved gets a fresh one rather than a failed start.
        /// The legacy directory is left in place so an older build still runs.
        /// </summary>
        public static void MigrateLegacyDataFolder()
        {
            try
            {
                if (Directory.Exists(ApplicationDataFolder)) return;
                if (!Directory.Exists(LegacyApplicationDataFolder)) return;

                CopyDirectory(LegacyApplicationDataFolder, ApplicationDataFolder);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(
                    $"Could not migrate the previous data directory '{LegacyApplicationDataFolder}': {e.Message}");
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: false);
            }

            foreach (var directory in Directory.EnumerateDirectories(source))
            {
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
            }
        }

        /// <summary>
        /// Writable per-user storage for standalone saves. This deliberately
        /// avoids writing beside the bundled ruleset, which is read-only in a
        /// Flatpak installation.
        /// </summary>
        public static string SaveGameFolder
        {
            get
            {
                var path = Path.Combine(ApplicationDataFolder, "Saves");
                Directory.CreateDirectory(path);
                return path;
            }
        }
        
        /// <summary>
        /// Writable per-user storage for crash reports, beside the saves and for the
        /// same reason: the bundled ruleset directory is read-only under Flatpak.
        /// </summary>
        public static string CrashLogFolder
        {
            get
            {
                var path = Path.Combine(ApplicationDataFolder, "Logs");
                Directory.CreateDirectory(path);
                return path;
            }
        }

        private const string SettingsFileName = "appsettings.json";

        /// <summary>Pre-defork name of the <see cref="GameDataPath"/> settings key.</summary>
        private const string LegacyGameDataPathKey = "Civ2Path";

        // Game settings from App.config
        public static string GameDataPath { get; private set; } = string.Empty;
        
        public static string[] SearchPaths { get; internal set; } = BuiltInSearchPaths;

        public static int TextureFilter { get; private set; }
        public static float Brightness { get; private set; } = 1f;
        public static float Saturation { get; private set; } = 1f;
        public static float Gamma { get; private set; } = 1f;

        /// <summary>
        /// Settings that change the game rather than the way it looks, and that are
        /// off until somebody asks for them.
        /// <para>
        /// Global warming is a rule the player opts into: pollution accumulating
        /// into a changed climate is faithful to Civ II but it is not something to
        /// have happen to somebody who did not ask for it. The cheat and editor
        /// menus are development tools, and a menu bar with them on it is not the
        /// one the game is meant to be played with.
        /// </para>
        /// </summary>
        public static bool GlobalWarmingEnabled { get; private set; }

        /// <inheritdoc cref="GlobalWarmingEnabled"/>
        public static bool CheatMenuEnabled { get; private set; }

        /// <inheritdoc cref="GlobalWarmingEnabled"/>
        public static bool EditorMenuEnabled { get; private set; }

        /// <summary>
        /// Records the advanced settings and writes them out. They persist between
        /// sessions: somebody who has turned the cheat menu on is not asked again
        /// next time they start the game.
        /// </summary>
        public static void SetAdvancedSettings(bool globalWarming, bool cheatMenu, bool editorMenu)
        {
            if (GlobalWarmingEnabled == globalWarming && CheatMenuEnabled == cheatMenu &&
                EditorMenuEnabled == editorMenu)
            {
                return;
            }

            GlobalWarmingEnabled = globalWarming;
            CheatMenuEnabled = cheatMenu;
            EditorMenuEnabled = editorMenu;
            Save();
        }

        public static bool LoadConfigSettings()
        {
            MigrateLegacyDataFolder();

            if (File.Exists(SettingsFilePath))
            {
                LoadSettings(SettingsFilePath);
                if (HasStandaloneData)
                {
                    SelectStandaloneRootIfNeeded();
                    return true;
                }
                if (!string.IsNullOrWhiteSpace(GameDataPath) && IsValidRoot(GameDataPath))
                {
                    return true;
                }
            }
            var alternativePath = Path.Combine(BasePath, SettingsFileName);

            LoadSettings(alternativePath);

            if (HasStandaloneData)
            {
                SelectStandaloneRootIfNeeded();
                return true;
            }

            return !string.IsNullOrWhiteSpace(GameDataPath) && IsValidRoot(GameDataPath);
        }

        private static void SelectStandaloneRootIfNeeded()
        {
            if (!string.IsNullOrWhiteSpace(GameDataPath) && IsValidRoot(GameDataPath)) return;

            GameDataPath = BuiltInSearchPaths.First(path =>
                FileUtilities.GetFile(path, RulesFile) != null && FileUtilities.GetFile(path, "game.txt") != null);
        }

        public static string BasePath => AppDomain.CurrentDomain.BaseDirectory;

        private static void LoadSettings(string? settingsFilePath)
        {
            if (!File.Exists(settingsFilePath)) return;

            var contents = File.ReadAllText(settingsFilePath, Encoding.UTF8);

            var settingsDoc = JsonDocument.Parse(contents);

            var root = settingsDoc.RootElement;

            // "Civ2Path" is what this key was called before the defork; settings
            // files written by an older build still use it.
            if (root.TryGetProperty(nameof(GameDataPath), out var gameDataPathElement) ||
                root.TryGetProperty(LegacyGameDataPathKey, out gameDataPathElement))
            {
                var gameDataPath = gameDataPathElement.GetString();
                if (IsValidRoot(gameDataPath))
                {
                    GameDataPath = gameDataPath!;
                }
            }

            if (root.TryGetProperty(nameof(RememberedChoices), out var choicesElement) &&
                choicesElement.ValueKind == JsonValueKind.Object)
            {
                RememberedChoices.Clear();
                foreach (var choice in choicesElement.EnumerateObject())
                {
                    if (choice.Value.TryGetInt32(out var value) && value >= 0)
                    {
                        RememberedChoices[choice.Name] = value;
                    }
                }
            }

            if (root.TryGetProperty(nameof(SearchPaths), out var searchPathsElement))
            {
                var searchPaths = BuiltInSearchPaths.Concat(searchPathsElement.EnumerateArray()
                        .Select(e => e.GetString()).Where(IsValidRoot).OfType<string>())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!string.IsNullOrWhiteSpace(GameDataPath))
                {
                    SearchPaths = !searchPaths.Contains(GameDataPath, StringComparer.OrdinalIgnoreCase)
                        ? BuiltInSearchPaths.Concat([GameDataPath]).Concat(searchPaths)
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                        : searchPaths;
                }
                else if(searchPaths.Length > 0)
                {
                    GameDataPath = searchPaths[0];
                    SearchPaths = searchPaths;
                }
                
            }else if (!string.IsNullOrWhiteSpace(GameDataPath))
            {
                SearchPaths = [..BuiltInSearchPaths, GameDataPath];
            }

            TextureFilter = root.TryGetProperty(nameof(TextureFilter), out var textureFilter) ? textureFilter.GetInt32() : 0;
            Brightness = ReadCorrection(root, nameof(Brightness), 1f, 0.5f, 1.5f);
            Saturation = ReadCorrection(root, nameof(Saturation), 1f, 0f, 2f);
            Gamma = ReadCorrection(root, nameof(Gamma), 1f, 0.5f, 2f);
            SeenTutorials.Clear();
            if (root.TryGetProperty(nameof(SeenTutorials), out var tutorials) &&
                tutorials.ValueKind == JsonValueKind.Array)
            {
                foreach (var tutorial in tutorials.EnumerateArray())
                {
                    var key = tutorial.GetString();
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        SeenTutorials.Add(key);
                    }
                }
            }

            GlobalWarmingEnabled = ReadFlag(root, nameof(GlobalWarmingEnabled));
            CheatMenuEnabled = ReadFlag(root, nameof(CheatMenuEnabled));
            EditorMenuEnabled = ReadFlag(root, nameof(EditorMenuEnabled));
        }

        /// <summary>An advanced setting, absent from the file until it is turned on.</summary>
        private static bool ReadFlag(JsonElement root, string property) =>
            root.TryGetProperty(property, out var element) &&
            element.ValueKind == JsonValueKind.True;

        private static float ReadCorrection(JsonElement root, string property, float fallback, float minimum, float maximum) =>
            root.TryGetProperty(property, out var element) && element.TryGetSingle(out var value)
                ? Math.Clamp(value, minimum, maximum)
                : fallback;

        public static void SetColorCorrection(float brightness, float saturation, float gamma)
        {
            Brightness = Math.Clamp(brightness, 0.5f, 1.5f);
            Saturation = Math.Clamp(saturation, 0f, 2f);
            Gamma = Math.Clamp(gamma, 0.5f, 2f);
            Save();
        }

        public static bool IsValidRoot(string? gameDataPath)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(gameDataPath) && Directory.Exists(gameDataPath) && FileUtilities.GetFile(gameDataPath, RulesFile) != null;
            }
            catch
            {
                return false;
            }
        }

        private const string RulesFile = "rules.txt";

        public static bool AddPath(string path)
        {
            if (!IsValidRoot(path))
            {
                var dir = Path.GetDirectoryName(path);
                if (dir is null || !IsValidRoot(dir)) return false;
                path = dir;
            }

            if (string.IsNullOrWhiteSpace(GameDataPath) || !IsValidRoot(GameDataPath))
            {
                GameDataPath = path;
                SearchPaths = [..BuiltInSearchPaths, path];
            }
            else
            {
                SearchPaths = SearchPaths.Append(path).ToArray();
            }
            Save();// This overwrites the appsettings.
            return true;
        }

        private static string[] BuiltInSearchPaths =>
        [
            Path.Combine(BasePath, "FOSSart", "Standalone"),
            Path.Combine(BasePath, "RaylibUI", "FOSSart", "Standalone"),
            Path.Combine(BasePath, "FOSSart"),
            Path.Combine(BasePath, "RaylibUI", "FOSSart"),
            BasePath
        ];

        private static bool HasStandaloneData => BuiltInSearchPaths.Any(path =>
            FileUtilities.GetFile(path, RulesFile) != null && FileUtilities.GetFile(path, "game.txt") != null);

        /// <summary>
        /// The answer given last time each of the new-game questions was asked,
        /// keyed by the dialog's name.
        /// <para>
        /// Starting a game asks a dozen questions and every one of them opened on
        /// its default, so a player who always wants the same thing -- raging hordes
        /// every game, say -- had to say so every game. The answers are remembered
        /// between sessions and the dialogs open on them.
        /// </para>
        /// </summary>
        private static readonly Dictionary<string, int> RememberedChoices = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The remembered answer to a new-game question, if there is one.</summary>
        public static int? NewGameChoice(string dialogName) =>
            RememberedChoices.TryGetValue(dialogName, out var choice) ? choice : null;

        /// <summary>
        /// Records an answer to a new-game question and writes it out. Saving here
        /// rather than at the end of the flow means a run of questions abandoned
        /// half way still remembers what was answered before it was.
        /// </summary>
        public static void RememberNewGameChoice(string dialogName, int choice)
        {
            if (string.IsNullOrWhiteSpace(dialogName) || choice < 0)
            {
                return;
            }

            if (RememberedChoices.TryGetValue(dialogName, out var existing) && existing == choice)
            {
                return;
            }

            RememberedChoices[dialogName] = choice;
            Save();
        }

        /// <summary>
        /// Tutorial advice the player has already been given.
        /// <para>
        /// Remembered between games as well as within one: somebody who has been
        /// told how the number pad works does not need telling again the next time
        /// they start a game, and being told twice is how a tutorial turns into a
        /// nuisance.
        /// </para>
        /// </summary>
        private static readonly HashSet<string> SeenTutorials = new(StringComparer.OrdinalIgnoreCase);

        public static bool HasSeenTutorial(string key) => SeenTutorials.Contains(key);

        public static void MarkTutorialSeen(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !SeenTutorials.Add(key))
            {
                return;
            }

            Save();
        }

        /// <summary>Puts the tutorial back, for somebody who wants it again.</summary>
        public static void ForgetTutorials()
        {
            if (SeenTutorials.Count == 0)
            {
                return;
            }

            SeenTutorials.Clear();
            Save();
        }

        public static void Save()
        {
            if (!Directory.Exists(ApplicationDataFolder))
            {
                Directory.CreateDirectory(ApplicationDataFolder);
            }
            // File.Create, not File.OpenWrite: OpenWrite does not truncate, so
            // writing a shorter settings file than the one already there left the
            // tail of the old one behind and the result would not parse.
            using var stream = File.Create(SettingsFilePath);
            using var writer = new Utf8JsonWriter(stream);
            writer.WriteStartObject();
            writer.WriteString(nameof(GameDataPath),GameDataPath);
            writer.WriteStartArray(nameof(SearchPaths));
            foreach (var searchPath in SearchPaths)
            {
                writer.WriteStringValue(searchPath);
            }
            writer.WriteEndArray();
            writer.WriteNumber(nameof(TextureFilter), TextureFilter);
            writer.WriteNumber(nameof(Brightness), Brightness);
            writer.WriteNumber(nameof(Saturation), Saturation);
            writer.WriteNumber(nameof(Gamma), Gamma);
            writer.WriteBoolean(nameof(GlobalWarmingEnabled), GlobalWarmingEnabled);
            writer.WriteBoolean(nameof(CheatMenuEnabled), CheatMenuEnabled);
            writer.WriteBoolean(nameof(EditorMenuEnabled), EditorMenuEnabled);
            writer.WriteStartArray(nameof(SeenTutorials));
            foreach (var tutorial in SeenTutorials)
            {
                writer.WriteStringValue(tutorial);
            }
            writer.WriteEndArray();
            writer.WriteStartObject(nameof(RememberedChoices));
            foreach (var choice in RememberedChoices)
            {
                writer.WriteNumber(choice.Key, choice.Value);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
        }
        
        private static string GetLocalAppDataFolder() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? BasePath;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                    ?? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? BasePath, ".local", "share");
            } 
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? BasePath, "Library", "Application Support");
            }
            throw new NotImplementedException("Unknown OS Platform");
        }
    }
}
