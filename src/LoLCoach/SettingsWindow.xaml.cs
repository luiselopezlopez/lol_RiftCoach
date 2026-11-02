using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
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
            EndpointInput.Text = settings.Endpoint;
            ModelInput.Text = settings.Deployment;
            RequestLimitInput.Text = settings.RequestLimit.ToString(CultureInfo.InvariantCulture);
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
        if (!int.TryParse(RequestLimitInput.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
        {
            ErrorText.Text = "RequestLimit debe ser un número entero entre 1 y 100000.";
            RequestLimitInput.Focus();
            return;
        }
        try
        {
            var key = DeleteKeyInput.IsChecked == true ? "" : string.IsNullOrWhiteSpace(ApiKeyInput.Password) ? null : ApiKeyInput.Password;
            viewModel.SaveConnectionSettings(EndpointInput.Text, ModelInput.Text, limit, key);
            DialogResult = true;
        }
        catch (Exception exception) when (IsSettingsError(exception))
        {
            ShowError(exception);
        }
    }

    private static bool IsSettingsError(Exception exception)
        => exception is ArgumentException or IOException or UnauthorizedAccessException or JsonException
            or CryptographicException or InvalidOperationException or NotSupportedException;

    private void ShowError(Exception exception) => ErrorText.Text = exception switch
    {
        ArgumentException or InvalidOperationException => exception.Message,
        JsonException => "El archivo de configuración tiene un formato inválido. Corrígelo antes de guardar.",
        CryptographicException => "No se pudo cifrar la clave. La configuración no se ha guardado.",
        _ => "No se pudo acceder al archivo de configuración. Comprueba la ruta y los permisos."
    };
}
