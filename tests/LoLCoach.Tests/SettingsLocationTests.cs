using System.IO;
using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class SettingsLocationTests
{
    [Fact]
    public void ProjectLocalSettingsTakePrecedenceOverUserFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var executable = Path.Combine(root, "executable");
        var project = Path.Combine(root, "project");
        var user = Path.Combine(root, "user");
        Directory.CreateDirectory(project);
        Directory.CreateDirectory(user);
        try
        {
            File.WriteAllText(Path.Combine(project, "localsettings.json"), "{}");
            File.WriteAllText(Path.Combine(user, "localsettings.json"), "{}");
            Assert.Equal(project, SettingsService.ResolveDirectory(executable, project, user));
            Directory.CreateDirectory(executable);
            File.WriteAllText(Path.Combine(executable, "localsettings.json"), "{}");
            Assert.Equal(executable, SettingsService.ResolveDirectory(executable, project, user));
            File.Delete(Path.Combine(executable, "localsettings.json"));
            File.Delete(Path.Combine(project, "localsettings.json"));
            File.WriteAllText(Path.Combine(root, "localsettings.json"), "{}");
            Assert.Equal(root, SettingsService.ResolveDirectory(executable, project, user));
            File.Delete(Path.Combine(root, "localsettings.json"));
            Assert.Equal(user, SettingsService.ResolveDirectory(executable, project, user));
        }
        finally { Directory.Delete(root, true); }
    }
}