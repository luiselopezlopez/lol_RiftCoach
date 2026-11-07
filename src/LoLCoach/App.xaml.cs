using System.Windows;

namespace LoLCoach;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        var settings = new Services.SettingsService().Load();
        Services.LocalizationManager.Instance.SetLanguage(settings.Language);
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
