using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoLCoach.Services;

public sealed record AppSettings
{
    public string Endpoint { get; init; } = "https://luisellmcontainer.openai.azure.com/openai/v1/";
    public string Deployment { get; init; } = "gpt-6-luna";
    public int RequestLimit { get; init; } = 12;
    public PlayerProfile? Profile { get; init; }
    public bool AllowUnverifiedBotOpponentsForDebugging { get; init; }
}

public sealed record PanelLayout(double Left, double Top, double Width, double Height, bool IsOpen, int ZIndex = 0)
{
    [JsonIgnore]
    public bool IsValid => double.IsFinite(Left) && double.IsFinite(Top) && double.IsFinite(Width) && double.IsFinite(Height)
        && Width > 0 && Height > 0;
}

public sealed class SettingsService
{
    private readonly string directory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true, PropertyNameCaseInsensitive = true,
        Converters = { new OptionalProfileConverter() }
    };
    private static readonly JsonSerializerOptions ProfileOptions = new() { PropertyNameCaseInsensitive = true };
    private const string ProtectedKeyPrefix = "dpapi:";
    private sealed record LocalSettings
    {
        public string Endpoint { get; init; } = new AppSettings().Endpoint;
        public string Model { get; init; } = new AppSettings().Deployment;
        public string? ApiKey { get; init; }
        public int RequestLimit { get; init; } = 12;
        public PlayerProfile? Profile { get; init; }
        public bool AllowUnverifiedBotOpponentsForDebugging { get; init; }
        public Dictionary<string, PanelLayout>? Layout { get; init; }
    }

    private sealed class OptionalProfileConverter : JsonConverter<PlayerProfile>
    {
        public override PlayerProfile? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String && string.IsNullOrWhiteSpace(reader.GetString())) return null;
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Profile debe ser un objeto, null o una cadena vacía.");
            return JsonSerializer.Deserialize<PlayerProfile>(ref reader, ProfileOptions);
        }

        public override void Write(Utf8JsonWriter writer, PlayerProfile value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, ProfileOptions);
    }

    public string LocalSettingsPath => Path.Combine(directory, "localsettings.json");

    public SettingsService(string? directory = null)
        => this.directory = directory ?? ResolveDirectory(AppContext.BaseDirectory, Environment.CurrentDirectory,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoLCoach"));

    public static string ResolveDirectory(string executableDirectory, string workingDirectory, string userDirectory)
    {
        if (File.Exists(Path.Combine(executableDirectory, "localsettings.json"))) return executableDirectory;
        if (File.Exists(Path.Combine(workingDirectory, "localsettings.json"))) return workingDirectory;
        for (var parent = new DirectoryInfo(executableDirectory).Parent; parent is not null; parent = parent.Parent)
            if (File.Exists(Path.Combine(parent.FullName, "localsettings.json"))) return parent.FullName;
        return userDirectory;
    }

    public static Uri ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Port != 443
            || !uri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || uri.AbsolutePath.TrimEnd('/') != "/openai/v1")
            throw new ArgumentException("Usa un endpoint Azure OpenAI HTTPS con la ruta /openai/v1/.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }

    public AppSettings Load()
    {
        try
        {
            return LoadForEditing();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public AppSettings LoadForEditing() => ToAppSettings(ReadLocalSettings());

    private static AppSettings ToAppSettings(LocalSettings local) => new()
    {
        Endpoint = local.Endpoint, Deployment = local.Model, RequestLimit = local.RequestLimit,
        Profile = local.Profile, AllowUnverifiedBotOpponentsForDebugging = local.AllowUnverifiedBotOpponentsForDebugging
    };

    public void Save(AppSettings settings)
    {
        ValidateSettings(settings);
        var local = ReadLocalSettings();
        WriteLocalSettings(local with { Endpoint = settings.Endpoint, Model = settings.Deployment,
            RequestLimit = settings.RequestLimit, Profile = settings.Profile,
            AllowUnverifiedBotOpponentsForDebugging = settings.AllowUnverifiedBotOpponentsForDebugging });
    }

    private static void ValidateSettings(AppSettings settings)
    {
        ValidateEndpoint(settings.Endpoint);
        if (string.IsNullOrWhiteSpace(settings.Deployment) || settings.Deployment.Length > 128)
            throw new ArgumentException("Introduce el nombre del despliegue.");
        if (settings.RequestLimit is < 1 or > 100000)
            throw new ArgumentException("El límite debe estar entre 1 y 100000 solicitudes.");
    }

    public AppSettings SaveConnectionSettings(string endpoint, string model, int requestLimit, string? apiKey)
    {
        var settings = new AppSettings { Endpoint = endpoint.Trim(), Deployment = model.Trim(), RequestLimit = requestLimit };
        ValidateSettings(settings);
        var local = ReadLocalSettings();
        var updated = local with
        {
            Endpoint = ValidateEndpoint(settings.Endpoint).AbsoluteUri, Model = settings.Deployment,
            RequestLimit = settings.RequestLimit,
            ApiKey = apiKey is null ? local.ApiKey : string.IsNullOrWhiteSpace(apiKey) ? null : ProtectKey(apiKey)
        };
        WriteLocalSettings(updated);
        return ToAppSettings(updated);
    }

    public IReadOnlyDictionary<string, PanelLayout> LoadLayout()
    {
        try
        {
            return (ReadLocalSettings().Layout ?? [])
                .Where(entry => entry.Value is { IsValid: true })
                .ToDictionary(entry => entry.Key, entry => entry.Value);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, PanelLayout>();
        }
    }

    public void SaveLayout(IReadOnlyDictionary<string, PanelLayout> layout)
        => WriteLocalSettings(ReadLocalSettings() with { Layout = layout.Where(entry => entry.Value.IsValid)
            .ToDictionary(entry => entry.Key, entry => entry.Value) });

    public void SaveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Introduce una clave válida.");
        var local = ReadLocalSettings();
        WriteLocalSettings(local with { ApiKey = ProtectKey(key) });
    }

    private static string ProtectKey(string key)
    {
        var plaintext = Encoding.UTF8.GetBytes(key.Trim());
        try
        {
            var encrypted = ProtectedData.Protect(plaintext, null, DataProtectionScope.CurrentUser);
            return ProtectedKeyPrefix + Convert.ToBase64String(encrypted);
        }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public string? LoadKey()
    {
        var key = ReadLocalSettings().ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (!key.StartsWith(ProtectedKeyPrefix, StringComparison.Ordinal)) return key.Trim();
        byte[] encrypted;
        try { encrypted = Convert.FromBase64String(key[ProtectedKeyPrefix.Length..]); }
        catch (FormatException exception) { throw new CryptographicException("Clave local cifrada no válida.", exception); }
        var plaintext = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(plaintext); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }

    public void DeleteKey()
    {
        WriteLocalSettings(ReadLocalSettings() with { ApiKey = null });
        var legacyPath = Path.Combine(directory, "credential.bin");
        if (File.Exists(legacyPath)) File.Delete(legacyPath);
    }

    private LocalSettings ReadLocalSettings()
    {
        if (File.Exists(LocalSettingsPath))
            return JsonSerializer.Deserialize<LocalSettings>(File.ReadAllText(LocalSettingsPath), JsonOptions)
                ?? throw new JsonException("localsettings.json no contiene una configuración válida.");
        var legacySettingsPath = Path.Combine(directory, "settings.json");
        var legacyKeyPath = Path.Combine(directory, "credential.bin");
        var previous = File.Exists(legacySettingsPath)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(legacySettingsPath), JsonOptions) ?? new()
            : new AppSettings();
        var local = new LocalSettings
        {
            Endpoint = previous.Endpoint, Model = previous.Deployment, RequestLimit = previous.RequestLimit, Profile = previous.Profile,
            ApiKey = File.Exists(legacyKeyPath) ? ProtectedKeyPrefix + Convert.ToBase64String(File.ReadAllBytes(legacyKeyPath)) : null
        };
        WriteLocalSettings(local);
        if (File.Exists(legacyKeyPath)) File.Delete(legacyKeyPath);
        if (File.Exists(legacySettingsPath)) File.Delete(legacySettingsPath);
        return local;
    }

    private void WriteLocalSettings(LocalSettings local)
    {
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, "localsettings.tmp");
        File.WriteAllText(temporary, JsonSerializer.Serialize(local, JsonOptions));
        File.Move(temporary, LocalSettingsPath, true);
    }

    public void Reset()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}