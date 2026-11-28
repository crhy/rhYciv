using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RhyCiv.Engine.Diagnostics;

    /// <summary>
    /// A running record of what the game was doing, written to disk as it happens.
    /// </summary>
    /// <remarks>
    /// The crash handler in Program catches managed exceptions, and that is most of
    /// them. It cannot catch the rest: a fault inside raylib or the graphics driver,
    /// or a stack overflow, kills the process without .NET running any handler at
    /// all. Those crashes leave nothing behind, and a report that says only "it
    /// crashed while I was playing" cannot be acted on.
    ///
    /// So this does not try to catch anything. It writes a line per notable action,
    /// flushed immediately, and holds the file open for the length of the session.
    /// A clean exit deletes it. A file still present at the next launch therefore
    /// means the previous session died without shutting down, and it is promoted to
    /// a crash report carrying everything the dead process left: its recent
    /// activity, the tail of its output, the GPU it was on, and the dump and JSON
    /// crash report the runtime writes at the fault when the launcher enables them
    /// (DOTNET_DbgEnableMiniDump, DOTNET_EnableCrashReport).
    ///
    /// Everything here is best-effort. Diagnostics must never be the reason a player
    /// cannot start the game, so every operation swallows its own failures.
    /// </remarks>
public static class SessionLog
{
    private const string ActiveFileName = "session-in-progress.log";

    /// <summary>
    /// Anything the process wrote to its own output streams during the session
    /// that crashed, kept aside by the launcher before this one overwrote it.
    /// </summary>
    /// <remarks>
    /// The record below says what the game was doing; this says what the runtime
    /// said on the way down, which is the half that names a stack overflow or a
    /// driver abort -- and carries the GPU banner a native fault is triaged
    /// against. Only the packaged launcher captures it, so the report quotes its
    /// tail when it is there and says it was not captured when it is not.
    /// </remarks>
    private const string PreviousOutputFileName = "session-output.previous.log";

    /// <summary>Lines kept in memory for the crash report; the file keeps them all.</summary>
    private const int RecentLines = 40;

    /// <summary>
    /// How much of the dead session's output the report quotes. The fault is at
    /// the end of it, and a game session can print megabytes getting there.
    /// </summary>
    private const int OutputTailLines = 200;

    /// <summary>
    /// Crash dumps kept on disk. A mini dump is tens of megabytes, and the
    /// newest few cover anything still worth acting on.
    /// </summary>
    private const int DumpFilesKept = 3;

    /// <summary>Frames of the faulting thread quoted into the report.</summary>
    private const int MaxReportedFrames = 60;

    /// <summary>
    /// Where the runtime's crash reporter puts its dumps -- the launcher sets
    /// DOTNET_DbgMiniDumpName under the Logs folder, in here.
    /// </summary>
    private const string DumpFolderName = "dumps";

    private static string DumpFolder => Path.Combine(LogFolder(), DumpFolderName);

    private static readonly object Gate = new();
    private static readonly Queue<string> Recent = new();
    private static StreamWriter? _writer;

    /// <summary>
    /// Where the record is kept. Overridable so the tests can exercise this
    /// without writing into the player's own log folder; nothing else reassigns it.
    /// </summary>
    internal static Func<string> LogFolder { get; set; } = () => Settings.CrashLogFolder;

    private static string ActivePath => Path.Combine(LogFolder(), ActiveFileName);

    /// <summary>
    /// Opens the session record, and returns the previous session's record if it
    /// was left behind — which means that session crashed without a handler
    /// running. The caller decides how to report it.
    /// </summary>
    public static string? Begin(string version)
    {
        string? previous = null;
        lock (Gate)
        {
            try
            {
                // Release any record this process is already holding. Beginning a
                // session while one is open is not something the game does -- a
                // real previous session is a dead process, and a dead process holds
                // no handle -- but on Windows a file open without FileShare.Delete
                // cannot be renamed or removed, so promoting it would throw rather
                // than produce the crash report. Closing first makes the two cases
                // behave alike.
                _writer?.Dispose();
                _writer = null;

                previous = PromotePreviousSession(version);

                // FileShare.Delete so the next launch can always take this file over,
                // whatever state this process is left in.
                _writer = new StreamWriter(
                    new FileStream(ActivePath, FileMode.Create, FileAccess.Write,
                        FileShare.Read | FileShare.Delete))
                {
                    AutoFlush = true,
                };

                Write($"rhYciv {version}");
                Write($"{RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
                Write($".NET {RuntimeInformation.FrameworkDescription}");
                Write($"flatpak={(Environment.GetEnvironmentVariable("FLATPAK_ID") is { } id ? id : "no")}");
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"rhYciv: session log unavailable: {e.Message}");
                _writer = null;
            }
        }

        return previous;
    }

    /// <summary>Records one notable action. Cheap enough to call from game code.</summary>
    public static void Record(string message)
    {
        lock (Gate)
        {
            Write(message);
        }
    }

    /// <summary>
    /// Marks the session as having ended properly, so the next launch does not
    /// report it as a crash.
    /// </summary>
    public static void End()
    {
        lock (Gate)
        {
            try
            {
                _writer?.Dispose();
                _writer = null;
                if (File.Exists(ActivePath))
                {
                    File.Delete(ActivePath);
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"rhYciv: could not close the session log: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The last few recorded actions, for inclusion in a managed crash report.
    /// </summary>
    public static string RecentActivity()
    {
        lock (Gate)
        {
            return Recent.Count == 0
                ? "(nothing recorded)"
                : string.Join(Environment.NewLine, Recent);
        }
    }

    private static void Write(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff}  {message}";

        Recent.Enqueue(line);
        while (Recent.Count > RecentLines)
        {
            Recent.Dequeue();
        }

        try
        {
            _writer?.WriteLine(line);
        }
        catch
        {
            // A full or read-only disk must not stop play.
        }
    }

    /// <summary>
    /// Renames a session record left by a previous run into a crash report, and
    /// returns what it held.
    /// </summary>
    private static string? PromotePreviousSession(string version)
    {
        if (!File.Exists(ActivePath))
        {
            return null;
        }

        var contents = File.ReadAllText(ActivePath);
        var report = Path.Combine(LogFolder(),
            $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-no-handler.log");

        var header = new StringBuilder()
            .AppendLine("rhYciv: the previous session ended without shutting down.")
            .AppendLine()
            .AppendLine("No managed exception was recorded, so this was most likely a fault in a")
            .AppendLine("native library or the graphics driver, which terminates the process before")
            .AppendLine("any handler can run. What follows is everything the crashed process left")
            .AppendLine("behind: what it was doing, what it and the runtime printed, the GPU it was")
            .AppendLine("talking to, and the crash report the runtime wrote at the fault if the")
            .AppendLine("launcher had its crash reporter turned on.")
            .AppendLine()
            .ToString();

        var previousOutput = ReadPreviousOutput();

        var body = new StringBuilder()
            .Append(header)
            .AppendLine($"Game version: {VersionOfTheCrashedSession(contents)}")
            .AppendLine($"This report was written by: rhYciv {version}")
            .AppendLine()
            .AppendLine("What the game was doing, to the moment it died (the session record,")
            .AppendLine("which is the crashed session's RecentActivity carried to its last line):")
            .AppendLine()
            .Append(contents)
            .Append(OutputSection(previousOutput))
            .Append(GpuSection(previousOutput))
            .Append(CrashReportSection())
            .ToString();

        File.WriteAllText(report, body);
        File.Delete(ActivePath);
        TrimOldCrashDumps();
        Console.Error.WriteLine($"rhYciv: previous session did not exit cleanly; wrote {report}");
        return report;
    }

    /// <summary>
    /// The version the crashed session was running, from the banner the session
    /// record starts with; a player's crash is only actionable against a build.
    /// </summary>
    private static string VersionOfTheCrashedSession(string contents)
    {
        var banner = contents.Split('\n').FirstOrDefault();
        var marker = banner?.IndexOf("rhYciv ", StringComparison.Ordinal) ?? -1;
        if (banner == null || marker < 0)
        {
            return "not captured";
        }

        var rest = banner[(marker + "rhYciv ".Length)..].Trim();
        return rest.Length == 0 ? "not captured" : rest;
    }

    /// <summary>
    /// Reads the crashed session's own output, kept aside by the launcher, once,
    /// so both the tail and the GPU lines can come from it.
    /// </summary>
    private static string[]? ReadPreviousOutput()
    {
        try
        {
            var path = Path.Combine(LogFolder(), PreviousOutputFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            var lines = File.ReadAllLines(path);
            return lines.Length == 0 ? null : lines;
        }
        catch
        {
            // A report that cannot read one section is still worth having; the
            // section itself says it could not be captured.
            return null;
        }
    }

    /// <summary>
    /// The end of the crashed session's stdout/stderr -- the last thing it said
    /// before the fault, including anything the runtime or a native library
    /// printed on the way down.
    /// </summary>
    private static string OutputSection(string[]? previousOutput)
    {
        if (previousOutput == null)
        {
            return Section("What the game and the runtime printed",
                $"not captured: no {PreviousOutputFileName} was kept; the launcher keeps it, " +
                "so a crash outside the launcher has no output to fold in");
        }

        var tail = previousOutput.Length <= OutputTailLines
            ? previousOutput
            : previousOutput[^OutputTailLines..];

        var section = new StringBuilder()
            .AppendLine()
            .AppendLine($"What the game and the runtime printed (last {tail.Length} of " +
                        $"{previousOutput.Length} lines):")
            .AppendLine();
        foreach (var line in tail)
        {
            section.AppendLine(line);
        }

        return section.ToString();
    }

    /// <summary>
    /// The GPU the crashed session was driving -- renderer string and driver
    /// version, straight from the raylib banner it printed at start-up. Native
    /// faults live or die on this, so a report without it cannot be triaged.
    /// </summary>
    private static string GpuSection(string[]? previousOutput)
    {
        if (previousOutput == null)
        {
            return Section("GPU", "not captured: the crashed session's output was not kept");
        }

        var found = new List<string>();
        for (var i = 0; i < previousOutput.Length; i++)
        {
            // raylib prints the block below "GL: OpenGL device information:";
            // an older layout also lines starting with "GL:" carry it.
            if (previousOutput[i].Contains("OpenGL device information", StringComparison.OrdinalIgnoreCase))
            {
                for (var j = i + 1;
                     j < previousOutput.Length && previousOutput[j].TrimStart().StartsWith(">");
                     j++)
                {
                    found.Add(previousOutput[j].Trim());
                }

                break;
            }
        }

        if (found.Count == 0)
        {
            foreach (var line in previousOutput)
            {
                if (line.Contains("Renderer:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("OpenGL version", StringComparison.OrdinalIgnoreCase))
                {
                    found.Add(line.Trim());
                }
            }
        }

        if (found.Count == 0)
        {
            return Section("GPU", "not captured: the crashed session printed no GL device information");
        }

        return new StringBuilder()
            .AppendLine()
            .AppendLine("GPU:")
            .AppendLine()
            .AppendLine(string.Join(Environment.NewLine, found))
            .ToString();
    }

    /// <summary>
    /// What the .NET runtime wrote at the fault itself: the mini dump's JSON
    /// crash report, whose faulting thread carries the native and managed stack
    /// that names a driver fault the way a managed handler never could.
    /// </summary>
    private static string CrashReportSection()
    {
        try
        {
            if (!Directory.Exists(DumpFolder))
            {
                return Section("Native crash report",
                    "not captured: no dump folder; the launcher sets DOTNET_DbgEnableMiniDump=1 " +
                    "and DOTNET_EnableCrashReport=1, so without them a native fault leaves no stack");
            }

            var reports = Directory.GetFiles(DumpFolder, "*.crashreport.json")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToArray();

            if (reports.Length == 0)
            {
                var dumps = Directory.GetFiles(DumpFolder, "*.dmp");
                return Section("Native crash report",
                    dumps.Length == 0
                        ? "not captured: no crash dump; the previous build's launcher may predate " +
                          "the runtime's crash reporter (DOTNET_DbgEnableMiniDump, DOTNET_EnableCrashReport)"
                        : $"not captured: {dumps.Length} dump file(s) but no crash report JSON; " +
                          "DOTNET_EnableCrashReport was not set at the crash");
            }

            return ParseCrashReport(reports[0]);
        }
        catch
        {
            return Section("Native crash report", "not captured: the crash report could not be read");
        }
    }

    /// <summary>
    /// The faulting thread's stack out of the runtime's crash report JSON, in
    /// the shape createdump writes it: a crashed thread object whose
    /// stack_frames carry either a managed method or a native module.
    /// </summary>
    private static string ParseCrashReport(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));

        var payload = document.RootElement.TryGetProperty("payload", out var p)
            ? p
            : document.RootElement;

        var section = new StringBuilder()
            .AppendLine()
            .AppendLine($"Native crash report ({Path.GetFileName(path)}):");

        if (document.RootElement.TryGetProperty("parameters", out var parameters) &&
            parameters.TryGetProperty("ExceptionType", out var exceptionType))
        {
            section.AppendLine($"fault: {exceptionType.GetString()}");
        }

        if (payload.TryGetProperty("threads", out var threads) &&
            threads.ValueKind == JsonValueKind.Array)
        {
            JsonElement? faulting = null;
            foreach (var thread in threads.EnumerateArray())
            {
                if (IsTrue(thread, "crashed"))
                {
                    faulting = thread;
                    break;
                }
            }

            faulting ??= threads.EnumerateArray().FirstOrDefault();

            if (faulting.Value.ValueKind == JsonValueKind.Object)
            {
                if (faulting.Value.TryGetProperty("managed_exception_type", out var managed))
                {
                    section.AppendLine($"exception: {managed.GetString()}");
                }

                if (faulting.Value.TryGetProperty("native_thread_id", out var threadId))
                {
                    section.AppendLine($"faulting thread: {threadId.GetString()}");
                }

                section.AppendLine("stack:");
                if (faulting.Value.TryGetProperty("stack_frames", out var frames) &&
                    frames.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var frame in frames.EnumerateArray())
                    {
                        if (index >= MaxReportedFrames)
                        {
                            section.AppendLine($"  ... {frames.GetArrayLength() - index} frames not shown");
                            break;
                        }

                        section.AppendLine($"  #{index++} {FormatFrame(frame)}");
                    }
                }
                else
                {
                    section.AppendLine("  (no stack frames in the crash report)");
                }
            }
        }

        return section.ToString();
    }

    private static string FormatFrame(JsonElement frame)
    {
        var managed = IsTrue(frame, "is_managed");
        var name = GetString(frame, "method_name")
            ?? GetString(frame, "unmanaged_name")
            ?? "(no symbol)";
        var module = GetString(frame, "filename") ?? GetString(frame, "native_module");
        var offset = GetString(frame, "native_image_offset");

        return module == null
            ? $"{name} [{(managed ? "managed" : "native")}]"
            : $"{name} [{module}{(offset == null ? "" : "+" + offset)}]";
    }

    private static bool IsTrue(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return false;
        }

        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && value.GetString() == "true");
    }

    private static string? GetString(JsonElement element, string property)
    {
        return element.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string Section(string title, string what)
    {
        return new StringBuilder()
            .AppendLine()
            .AppendLine($"{title}:")
            .AppendLine()
            .AppendLine(what)
            .ToString();
    }

    /// <summary>
    /// Dumps are only useful until the report is read, and a mini dump is still
    /// tens of megabytes, so the newest few are kept and the rest deleted. The
    /// crash report JSONs that belong to deleted dumps go with them.
    /// </summary>
    private static void TrimOldCrashDumps()
    {
        try
        {
            if (!Directory.Exists(DumpFolder))
            {
                return;
            }

            foreach (var pattern in new[] { "*.dmp", "*.crashreport.json" })
            {
                var files = Directory.GetFiles(DumpFolder, pattern)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .Skip(DumpFilesKept)
                    .ToList();
                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    {
                        // Old dumps are clutter, not a reason to fail a session.
                    }
                }
            }
        }
        catch
        {
            // Same: pruning is housekeeping.
        }
    }
}
