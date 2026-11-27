using System.Text.Json;

namespace RhyCiv.Tests.IO;

/// <summary>
/// #106: fresh installs drew civilopedia icons blurred because the shipped
/// Engine/appsettings.json asked for Trilinear texture filtering (2). The file
/// now ships Point/nearest (0), matching what the runtime config already had.
/// A future default of anything else must fail the suite, so pin the shipped
/// value against the checkout, not a copy under bin/.
/// </summary>
public class AppSettingsTextureFilterTests
{
    [Fact]
    public void ShippedAppSettingsFilterTexturesWithNearest()
    {
        var appSettings = Path.Combine(FindRepositoryRoot(), "Engine", "appsettings.json");
        Assert.True(File.Exists(appSettings), $"Expected the shipped Engine/appsettings.json at {appSettings}");

        using var document = JsonDocument.Parse(File.ReadAllText(appSettings).TrimStart('\uFEFF'));

        Assert.True(document.RootElement.TryGetProperty("TextureFilter", out var filter),
            "Engine/appsettings.json must state TextureFilter, or fresh installs fall back to a blurred default.");
        Assert.Equal(0, filter.GetInt32());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "rhYciv.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root");
    }
}
