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
}
