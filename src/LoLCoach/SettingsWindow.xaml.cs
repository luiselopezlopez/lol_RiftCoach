using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using LoLCoach.Services;
using LoLCoach.ViewModels;

namespace LoLCoach;

public partial class SettingsWindow : Window
{
    private readonly DashboardViewModel viewModel;

    public SettingsWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = viewModel;
        try
        {
            var settings = viewModel.LoadConnectionSettings();
            LanguageInput.SelectedValue = settings.Language;
            EndpointInput.Text = settings.Endpoint;
            ModelInput.Text = settings.Deployment;
            CoachingPromptInput.Text = viewModel.LoadCoachingPrompt();
            RefreshPromptVersions();
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ShowError(exception);
            SaveButton.IsEnabled = false;
        }
        Closed += (_, _) => ApiKeyInput.Clear();
    }

    private void OnDeleteKeyChanged(object sender, RoutedEventArgs args)
    {
        ApiKeyInput.IsEnabled = DeleteKeyInput.IsChecked != true;
        if (!ApiKeyInput.IsEnabled) ApiKeyInput.Clear();
    }

    private void OnSave(object sender, RoutedEventArgs args)
    {
        ErrorText.Text = "";
        try
        {
            var key = DeleteKeyInput.IsChecked == true ? "" : string.IsNullOrWhiteSpace(ApiKeyInput.Password) ? null : ApiKeyInput.Password;
            viewModel.SaveConnectionSettings(EndpointInput.Text, ModelInput.Text, key, LanguageInput.SelectedValue as string ?? "es");
            DialogResult = true;
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ShowError(exception);
        }
    }

    private void OnSavePrompt(object sender, RoutedEventArgs args)
    {
        ErrorText.Text = "";
        try
        {
            viewModel.SaveCoachingPrompt(CoachingPromptInput.Text);
            RefreshPromptVersions();
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ShowError(exception);
        }
    }

    private void OnRestorePromptVersion(object sender, RoutedEventArgs args)
    {
        ErrorText.Text = "";
        if (PromptVersionInput.SelectedItem is not PromptVersion version) return;
        try
        {
            viewModel.RestorePromptVersion(version.Id);
            CoachingPromptInput.Text = viewModel.LoadCoachingPrompt();
            RefreshPromptVersions();
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ShowError(exception);
        }
    }

    private void RefreshPromptVersions()
    {
        PromptVersionInput.ItemsSource = viewModel.LoadPromptVersions();
        PromptVersionInput.SelectedIndex = 0;
    }

    private static bool IsSettingsError(Exception exception)
        => exception is ArgumentException or IOException or UnauthorizedAccessException or JsonException
            or CryptographicException or InvalidOperationException or NotSupportedException;

    private void ShowError(Exception exception) => ErrorText.Text = LocalizationManager.Instance.Translate(exception switch
    {
        ArgumentException or InvalidOperationException => exception.Message,
        FileNotFoundException => "No se encontró la versión del prompt seleccionada.",
        JsonException => "El archivo de configuración tiene un formato inválido. Corrígelo antes de guardar.",
        CryptographicException => "No se pudo cifrar la clave. La configuración no se ha guardado.",
        _ => "No se pudo acceder al archivo de configuración. Comprueba la ruta y los permisos."
    });
}
