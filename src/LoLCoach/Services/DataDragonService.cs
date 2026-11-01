using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LoLCoach.Services;

public sealed class DataDragonService(HttpClient? httpClient = null, string? cachePath = null) : IDisposable
{
    private readonly HttpClient client = httpClient ?? new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly string cacheDirectory = cachePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LoLCoach", "catalog");
    private string? version;

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, "catalog.json");
        try
        {
            using var versions = JsonDocument.Parse(await client.GetStringAsync("https://ddragon.leagueoflegends.com/api/versions.json", cancellationToken));
            version = versions.RootElement[0].GetString() ?? throw new JsonException();
            var json = await client.GetStringAsync($"https://ddragon.leagueoflegends.com/cdn/{Uri.EscapeDataString(version)}/data/es_ES/champion.json", cancellationToken);
            using var catalog = JsonDocument.Parse(json);
            if (!catalog.RootElement.TryGetProperty("data", out _)) throw new JsonException();
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path)) throw new InvalidOperationException("Catálogo no disponible; se mantienen las recomendaciones locales.");
        }
        using var cached = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken));
        version = cached.RootElement.GetProperty("version").GetString();
    }

    public async Task<ImageSource?> GetPortraitAsync(string championId, CancellationToken cancellationToken)
    {
        if (version is null) return null;
        var filename = $"{version}-{championId}.png";
        var path = Path.Combine(cacheDirectory, filename);
        byte[] bytes;
        if (File.Exists(path)) bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        else
        {
            bytes = await client.GetByteArrayAsync($"https://ddragon.leagueoflegends.com/cdn/{Uri.EscapeDataString(version)}/img/champion/{Uri.EscapeDataString(championId)}.png", cancellationToken);
            await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        }
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var image = decoder.Frames[0];
        image.Freeze();
        return image;
    }

    public void Dispose() => client.Dispose();

    public async Task<ChampionAbilityReference> GetAbilityReferenceAsync(string championId, string patch, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(championId) || championId.Any(character => !char.IsAsciiLetterOrDigit(character))
            || string.IsNullOrEmpty(patch) || patch.Any(character => !char.IsAsciiDigit(character) && character != '.'))
            throw new ArgumentException("Identificador de campeón o versión no válido.");
        Directory.CreateDirectory(cacheDirectory);
        var path = Path.Combine(cacheDirectory, $"{patch}-{championId}-abilities.json");
        var json = File.Exists(path) ? await File.ReadAllTextAsync(path, cancellationToken)
            : await client.GetStringAsync($"https://ddragon.leagueoflegends.com/cdn/{patch}/data/es_ES/champion/{championId}.json", cancellationToken);
        using var document = JsonDocument.Parse(json);
        var champion = document.RootElement.GetProperty("data").GetProperty(championId);
        var passive = champion.GetProperty("passive");
        var abilities = new List<AbilityReference> { new("Pasiva", passive.GetProperty("name").GetString()!, passive.GetProperty("description").GetString()!) };
        var spells = champion.GetProperty("spells");
        if (spells.GetArrayLength() != 4) throw new JsonException("Ficha de habilidades incompleta.");
        var keys = new[] { "Q", "W", "E", "R" };
        for (var index = 0; index < keys.Length; index++)
            abilities.Add(new(keys[index], spells[index].GetProperty("name").GetString()!, spells[index].GetProperty("description").GetString()!));
        if (!File.Exists(path)) await File.WriteAllTextAsync(path, json, cancellationToken);
        return new(championId, patch, abilities.ToArray());
    }
}

public sealed record AbilityReference(string Key, string Name, string Description);
public sealed record ChampionAbilityReference(string Champion, string Version, AbilityReference[] Abilities);