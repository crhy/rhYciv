using RhyCiv.Tests.TestFiles;

namespace RhyCiv.Tests.IO;

/// <summary>
/// Where the wonders sit in the improvement table.
/// <para>
/// A Civ II save does not record wonders on its cities. It keeps a separate table
/// near the front saying which city holds each of the twenty-eight, in a fixed
/// order beginning with the Pyramids, and the reader turns a wonder's number in
/// that table into an improvement by adding it to the index of the first wonder.
/// Get that index wrong by one and every city in an imported game holds the wrong
/// wonder -- which is exactly what happened first time: Cardiff was given the
/// Oracle in place of its Great Library, and Kells Copernicus' Observatory in
/// place of Michelangelo's Chapel. Nothing failed, and nothing looked wrong until
/// the city screen was put beside Civ II's.
/// </para>
/// </summary>
public class WonderTableTests
{
    private const int FirstWonderIndex = 39;
    private const int WonderCount = 28;

    [Fact]
    public void TheFirstWonderInTheTableIsThePyramids()
    {
        var (_, _, rules) = CleanRoomGameFactory.CreateGame();

        Assert.Equal("Pyramids", rules.Improvements[FirstWonderIndex].Name);
        Assert.True(rules.Improvements[FirstWonderIndex].IsWonder);
    }

    [Fact]
    public void TwentyEightWondersFollowIt_AndNothingBeforeThemIsOne()
    {
        var (_, _, rules) = CleanRoomGameFactory.CreateGame();

        Assert.All(rules.Improvements.Take(FirstWonderIndex),
            improvement => Assert.False(improvement.IsWonder,
                $"{improvement.Name} is a wonder but sits before the wonder table starts"));

        var wonders = rules.Improvements.Skip(FirstWonderIndex).Take(WonderCount).ToList();
        Assert.Equal(WonderCount, wonders.Count);
        Assert.All(wonders, improvement => Assert.True(improvement.IsWonder,
            $"{improvement.Name} is inside the wonder table but is not a wonder"));
    }

    [Fact]
    public void AWonderKnownByItsPositionIsTheOneExpected()
    {
        // Spot checks at both ends and in the middle, in Civ II's own order. These
        // are the two that caught the off-by-one.
        var (_, _, rules) = CleanRoomGameFactory.CreateGame();

        Assert.Equal("Great Library", rules.Improvements[FirstWonderIndex + 4].Name);
        Assert.Equal("Michelangelo's Chapel", rules.Improvements[FirstWonderIndex + 10].Name);
        Assert.Equal("Cure for Cancer", rules.Improvements[FirstWonderIndex + 27].Name);
    }
}
