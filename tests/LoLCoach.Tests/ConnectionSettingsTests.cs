using System.IO;
using System.Text.Json;
using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class ConnectionSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LoLCoach-settings-" + Guid.NewGuid());
    private readonly SettingsService store;

    public ConnectionSettingsTests() => store = new SettingsService(directory);

    [Fact]
    public void SavesConnectionFieldsAndPreservesOtherSettings()
    {
        store.Save(new AppSettings { Profile = new(PlayerRole.Top, "Garen", []), AllowUnverifiedBotOpponentsForDebugging = true });
        var layout = new PanelLayout(10, 20, 300, 400, false);
        store.SaveLayout(new Dictionary<string, PanelLayout> { ["training"] = layout });

        var saved = store.SaveConnectionSettings(" https://test.openai.azure.com/openai/v1 ", " new-model ", "fake-new-secret");

        var reloaded = new SettingsService(directory);
        Assert.Equal("https://test.openai.azure.com/openai/v1/", saved.Endpoint);
        Assert.Equal(saved.Endpoint, reloaded.Load().Endpoint);
        Assert.Equal("new-model", reloaded.Load().Deployment);
        Assert.Equal("Garen", reloaded.Load().Profile!.Champion);
        Assert.True(reloaded.Load().AllowUnverifiedBotOpponentsForDebugging);
        Assert.Equal(layout, reloaded.LoadLayout()["training"]);
        Assert.Equal("fake-new-secret", reloaded.LoadKey());
        var json = File.ReadAllText(store.LocalSettingsPath);
        Assert.DoesNotContain("fake-new-secret", json);
        using var document = JsonDocument.Parse(json);
        Assert.StartsWith("dpapi:", document.RootElement.GetProperty("ApiKey").GetString());
    }

    [Fact]
    public void NullKeyPreservesStoredKeyAndEmptyKeyDeletesIt()
    {
        store.SaveKey("fake-original-secret");
        var originalKey = JsonDocument.Parse(File.ReadAllText(store.LocalSettingsPath)).RootElement.GetProperty("ApiKey").GetString();
        store.SaveConnectionSettings(new AppSettings().Endpoint, "changed", null);
        using var document = JsonDocument.Parse(File.ReadAllText(store.LocalSettingsPath));
        Assert.Equal(originalKey, document.RootElement.GetProperty("ApiKey").GetString());
        Assert.Equal("fake-original-secret", store.LoadKey());
        store.SaveConnectionSettings(new AppSettings().Endpoint, "changed", "");
        Assert.Null(store.LoadKey());
    }

    [Theory]
    [InlineData("http://test.openai.azure.com/openai/v1/", "model")]
    [InlineData("https://test.openai.azure.com/openai/v1/", "")]
    public void InvalidInputDoesNotChangeAnySavedField(string endpoint, string model)
    {
        store.SaveKey("fake-original-secret");
        var original = File.ReadAllText(store.LocalSettingsPath);
        Assert.Throws<ArgumentException>(() => store.SaveConnectionSettings(endpoint, model, "fake-replacement"));
        Assert.Equal(original, File.ReadAllText(store.LocalSettingsPath));
    }

    [Fact]
    public void MalformedConfigurationIsReportedAndNotOverwritten()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(store.LocalSettingsPath, "{invalid");
        Assert.Throws<JsonException>(() => store.LoadForEditing());
        Assert.Throws<JsonException>(() => store.SaveConnectionSettings(new AppSettings().Endpoint, "model", null));
        Assert.Equal("{invalid", File.ReadAllText(store.LocalSettingsPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
