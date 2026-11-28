using RhyCiv.Engine.Diagnostics;

namespace RhyCiv.Tests.Diagnostics;

/// <summary>
/// The session record exists to make crashes visible that no handler can catch —
/// a fault in a native library or the graphics driver kills the process outright.
/// Its two behaviours both have to hold: a session that ends properly must leave
/// nothing behind, and one that does not must leave enough to act on.
///
/// The first matters as much as the second. If a clean exit left a record, every
/// launch would report a crash, and players would learn to ignore the reports
/// that are real.
/// </summary>
[Collection("SessionLog")]
public class SessionLogTests : IDisposable
{
    private readonly string _directory =
        Directory.CreateTempSubdirectory("rhyciv-session-").FullName;

    public SessionLogTests() => SessionLog.LogFolder = () => _directory;

    public void Dispose()
    {
        SessionLog.End();
        SessionLog.LogFolder = null!;
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void ASessionThatEndsProperly_LeavesNothingBehind()
    {
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("did something");
        SessionLog.End();

        Assert.Empty(Directory.GetFiles(_directory));
    }

    [Fact]
    public void ASessionThatEndsProperly_IsNotReportedByTheNextOne()
    {
        SessionLog.Begin("0.0.0-test");
        SessionLog.End();

        Assert.Null(SessionLog.Begin("0.0.0-test"));
    }

    [Fact]
    public void ASessionThatCrashedWithAnException_IsNotAlsoReportedAsANativeFault()
    {
        // A managed crash writes its own report, naming the exception and the
        // stack, and then ends the session. It used to only do the first, so the
        // next launch found the record still open and wrote a *second* report
        // saying no managed exception had been recorded and the graphics driver
        // was the likely cause. One NullReferenceException became two crashes,
        // the second of them pointing at entirely the wrong thing -- and it was
        // read that way while a real crash was being chased.
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("something the game was doing");
        SessionLog.End();   // what the exception handler does after reporting

        Assert.Null(SessionLog.Begin("0.0.0-test"));
    }

    [Fact]
    public void ASessionThatNeverEnded_IsPromotedToACrashReport()
    {
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("opened the city window");
        // No End(): the process died where a native fault would have killed it.

        var report = SessionLog.Begin("0.0.0-test");

        Assert.NotNull(report);
        var contents = File.ReadAllText(report);
        Assert.Contains("ended without shutting down", contents);
        Assert.Contains("opened the city window", contents);
        Assert.Contains("0.0.0-test", contents);
    }

    [Fact]
    public void ACrashReport_IsOnlyProducedOnce()
    {
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("something");
        Assert.NotNull(SessionLog.Begin("0.0.0-test"));

        // The second launch after the crash is a normal one.
        SessionLog.End();
        Assert.Null(SessionLog.Begin("0.0.0-test"));
    }

    [Fact]
    public void RecentActivity_CarriesTheLastActionsIntoAManagedCrashReport()
    {
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("turn 12 begins");
        SessionLog.Record("command UNIT_ORDER_FORTIFY");

        var recent = SessionLog.RecentActivity();

        Assert.Contains("turn 12 begins", recent);
        Assert.Contains("UNIT_ORDER_FORTIFY", recent);
    }

    [Fact]
    public void ANoHandlerReport_CarriesTheOutputTailTheGpuAndTheCrashReport()
    {
        // The whole point of the no-handler report is that a driver fault with
        // no managed stack can still be acted on: it needs the game's last
        // actions, the runtime's last words, the GPU it crashed on, and the
        // faulting stack the .NET runtime itself wrote at the fault.
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("turn 49 played by Romans");
        // No End(): the process died where a driver fault would have killed it.

        File.WriteAllLines(
            Path.Combine(_directory, "session-output.previous.log"),
            Enumerable.Range(0, 200).Select(i => $"early filler {i}")
                .Concat(new[]
                {
                    "INFO: GL: OpenGL device information:",
                    "INFO:     > Vendor:   Intel",
                    "INFO:     > Renderer: Mesa Intel(R) UHD Graphics 630 (CFL GT2)",
                    "INFO:     > Version:  4.6 (Core Profile) Mesa 26.2.2 (git-3281a69a8b)",
                    "INFO: TEXTURE: [ID 685] Unloaded texture data from VRAM (GPU)",
                    "late filler 200",
                    "Segmentation fault (core dumped)",
                }));

        // The runtime's own crash report, in the shape createdump writes it
        // (everything a string, booleans included).
        var dumps = Directory.CreateDirectory(Path.Combine(_directory, "dumps")).FullName;
        File.WriteAllText(Path.Combine(dumps, "crash-1234.dmp"), "minidump bytes");
        File.WriteAllText(Path.Combine(dumps, "crash-1234.dmp.crashreport.json"), """
            {
              "payload": {
                "protocol_version": "1.0.0",
                "configuration": { "architecture": "amd64", "version": "9.0.19" },
                "process_name": "RaylibUI.dll",
                "threads": [
                  {
                    "is_managed": "false",
                    "crashed": "true",
                    "native_thread_id": "0x6fb6",
                    "ctx": { "IP": "0x7fb95d6a6efe", "SP": "0x7ffcd9c9f1d8", "BP": "0x0" },
                    "stack_frames": [
                      {
                        "is_managed": "false",
                        "native_address": "0x7fb95d6a6efe",
                        "native_image_offset": "0x9cefe",
                        "unmanaged_name": "cgl_get_proc_address",
                        "native_module": "libGL.so.1"
                      },
                      {
                        "is_managed": "true",
                        "method_name": "RaylibUI.Main.DrawFrame",
                        "filename": "RaylibUI.dll",
                        "native_image_offset": "0x2a1"
                      }
                    ]
                  }
                ]
              },
              "parameters": { "ExceptionType": "0x20000000" }
            }
            """);

        var report = SessionLog.Begin("0.0.0-test");

        Assert.NotNull(report);
        var contents = File.ReadAllText(report);
        Assert.Contains("turn 49 played by Romans", contents);          // recent activity
        Assert.Contains("Game version: 0.0.0-test", contents);          // the build that died
        Assert.Contains("Renderer: Mesa Intel(R) UHD Graphics 630", contents);
        Assert.Contains("Version:  4.6 (Core Profile) Mesa 26.2.2", contents);
        Assert.Contains("late filler 200", contents);                   // the tail reaches the fault
        Assert.Contains("Segmentation fault (core dumped)", contents);
        Assert.Contains("early filler 6", contents);                    // 200 lines back, no more
        Assert.DoesNotContain("early filler 4\n", contents);
        Assert.Contains("libGL.so.1", contents);                        // the faulting stack
        Assert.Contains("RaylibUI.Main.DrawFrame", contents);
        Assert.Contains("0x20000000", contents);                        // SIGSEGV
    }

    [Fact]
    public void ANoHandlerReport_ReportsMissingPiecesAsNotCaptured()
    {
        // Everything past the session record comes from files the launcher may
        // or may not have kept; their absence is stated, never faked, and must
        // not stop the report being written.
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("something");

        var report = SessionLog.Begin("0.0.0-test");

        Assert.NotNull(report);
        var contents = File.ReadAllText(report);
        Assert.Contains("not captured", contents);
        Assert.Contains("something", contents);
    }

    [Fact]
    public void ANoHandlerReport_DoesNotCrashOnACorruptCrashReport()
    {
        SessionLog.Begin("0.0.0-test");
        var dumps = Directory.CreateDirectory(Path.Combine(_directory, "dumps")).FullName;
        File.WriteAllText(Path.Combine(dumps, "crash-9.dmp"), "minidump bytes");
        File.WriteAllText(Path.Combine(dumps, "crash-9.dmp.crashreport.json"), "{ truncated");

        var report = SessionLog.Begin("0.0.0-test");

        Assert.NotNull(report);
        Assert.Contains("not captured", File.ReadAllText(report));
    }

    [Fact]
    public void CrashDumps_AreBoundedToTheNewestThree()
    {
        // A mini dump is tens of megabytes; only the newest few are worth the
        // disk, so older ones and their crash reports are deleted.
        var dumps = Directory.CreateDirectory(Path.Combine(_directory, "dumps")).FullName;
        var started = new DateTime(2026, 11, 28, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 5; i++)
        {
            var dump = Path.Combine(dumps, $"crash-{i}.dmp");
            File.WriteAllText(dump, "bytes");
            File.WriteAllText(dump + ".crashreport.json", "{}");
            File.SetLastWriteTimeUtc(dump, started.AddMinutes(i));
            File.SetLastWriteTimeUtc(dump + ".crashreport.json", started.AddMinutes(i));
        }
        SessionLog.Begin("0.0.0-test");
        SessionLog.Record("the crash after the crashes");

        var report = SessionLog.Begin("0.0.0-test");

        Assert.NotNull(report);
        Assert.Equal(3, Directory.GetFiles(dumps, "*.dmp").Length);
        Assert.Equal(3, Directory.GetFiles(dumps, "*.crashreport.json").Length);
        Assert.All(new[] { "crash-2", "crash-3", "crash-4" },
            name => Assert.True(File.Exists(Path.Combine(dumps, name + ".dmp")), name));
    }
}
