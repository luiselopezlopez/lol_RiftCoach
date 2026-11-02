using System.IO;
using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public sealed class CoachingPromptSettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "LoLCoach-prompts-" + Guid.NewGuid());
    private readonly SettingsService store;

    public CoachingPromptSettingsTests() => store = new SettingsService(directory);

    [Fact]
    public void FirstLoadCreatesStandalonePromptUsingTheBundledDefault()
    {
        var prompt = store.LoadPrompt();

        Assert.Equal(SettingsService.LoadDefaultCoachingPrompt(), prompt);
        Assert.Equal(prompt, File.ReadAllText(store.CoachingPromptPath));
        Assert.Equal(prompt, store.LoadForEditing().CoachingPrompt);
        Assert.Empty(store.LoadPromptVersions());
    }

    [Fact]
    public void EachChangedPromptCreatesARecoverableVersionAndRestoreIsVersioned()
    {
        var original = store.LoadPrompt();
        store.SavePrompt("prompt editado 1");
        var firstVersion = Assert.Single(store.LoadPromptVersions());
        Assert.Equal(original, File.ReadAllText(VersionPath(firstVersion)));

        store.SavePrompt("prompt editado 1");
        Assert.Single(store.LoadPromptVersions());

        store.SavePrompt("prompt editado 2");
        var previousEdit = store.LoadPromptVersions()
            .Single(version => File.ReadAllText(VersionPath(version)) == "prompt editado 1");
        store.RestorePromptVersion(previousEdit.Id);

        Assert.Equal("prompt editado 1", store.LoadPrompt());
        Assert.Contains(store.LoadPromptVersions(),
            version => File.ReadAllText(VersionPath(version)) == "prompt editado 2");
        Assert.Equal(3, store.LoadPromptVersions().Count);
    }

    [Fact]
    public void EmptyPromptsAndInvalidVersionIdsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => store.SavePrompt("  "));
        Assert.Throws<ArgumentException>(() => store.RestorePromptVersion("..\\localsettings"));
        Assert.Empty(store.LoadPromptVersions());
    }

    [Fact]
    public void PromptFileAccessErrorsAreNotSilentlyReplacedByTheDefault()
    {
        Directory.CreateDirectory(store.CoachingPromptPath);

        var exception = Assert.ThrowsAny<Exception>(() => store.Load());
        Assert.True(exception is IOException or UnauthorizedAccessException);
    }

    private string VersionPath(PromptVersion version)
        => Path.Combine(directory, "coaching-prompt-history", version.Id + ".txt");

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
