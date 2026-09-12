using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using RhyCiv.Engine;
using RhyCiv.Engine.Diagnostics;
using System.Reflection;

namespace RaylibUI
{
    class Program
    {
        static int Main(string[] args)
        {
            // A hard crash used to leave nothing behind but a window that vanished,
            // which is why the reports we get name the action and not the line. Write
            // the stack trace somewhere the player can find it and say where it went.
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Report(e.ExceptionObject as Exception, "unhandled exception");
            TaskScheduler.UnobservedTaskException += (_, e) =>
                Report(e.Exception, "unobserved task exception");

            // A record of what the game is doing, flushed as it goes. The handlers
            // above cover managed exceptions; a fault in a native library or the
            // graphics driver kills the process without running any of them, and
            // leaves nothing to report. A session record left behind at the next
            // launch is how those crashes become visible at all.
            SessionLog.Begin(Version);

            // A signalled shutdown -- logging out, or `kill` -- is an orderly end,
            // not a crash. Without this every such exit would leave a session record
            // behind and be reported as one, which would quickly teach players to
            // ignore the reports that matter.
            using var term = PosixSignalRegistration.Create(PosixSignal.SIGTERM, _ => SessionLog.End());
            using var quit = PosixSignalRegistration.Create(PosixSignal.SIGINT, _ => SessionLog.End());

            try
            {
                var main = new Main();

                main.RunLoop();
                SessionLog.End();
                return 0;
            }
            catch (Exception ex)
            {
                Report(ex, "crash");

                // The session has been accounted for -- there is a report naming
                // the exception and the stack. Leaving the record behind made the
                // *next* launch find it and write a second report saying "no
                // managed exception was recorded, so this was most likely a fault
                // in a native library or the graphics driver", which was untrue and
                // actively misleading: one NullReferenceException turned into two
                // crash reports, the second of them blaming the graphics driver.
                SessionLog.End();
                return 1;
            }
        }

        private static string Version =>
            typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion
            ?? typeof(Program).Assembly.GetName().Version?.ToString()
            ?? "unknown version";

        private static void Report(Exception? exception, string kind)
        {
            if (exception == null)
            {
                return;
            }

            var report = new StringBuilder()
                .AppendLine($"rhYciv {Version} {kind} at {DateTime.Now:yyyy-MM-dd HH:mm:ss}")
                .AppendLine(exception.ToString())
                .AppendLine()
                .AppendLine("What the game was doing:")
                .AppendLine(SessionLog.RecentActivity())
                .ToString();

            // Always to stderr, so a terminal run shows it even if the file cannot
            // be written.
            Console.Error.WriteLine(report);

            try
            {
                var path = Path.Combine(Settings.CrashLogFolder,
                    $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.WriteAllText(path, report);
                Console.Error.WriteLine($"rhYciv: crash report written to {path}");
            }
            catch (Exception writeFailure)
            {
                Console.Error.WriteLine($"rhYciv: could not write a crash report: {writeFailure.Message}");
            }
        }
    }
}
