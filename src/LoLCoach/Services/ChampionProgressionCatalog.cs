using System.Text.Json;
using System.Text.Json.Serialization;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed class ChampionProgressionCatalog
{
    private readonly CatalogData data;
    private static readonly Lazy<ChampionProgressionCatalog> shared = new(() =>
    {
        using var stream = typeof(ChampionProgressionCatalog).Assembly.GetManifestResourceStream("LoLCoach.lol_coach_data.json")
            ?? throw new InvalidOperationException("Falta el catálogo de progresión de campeones.");
        using var reader = new System.IO.StreamReader(stream);
        return new(reader.ReadToEnd());
    });

    public static ChampionProgressionCatalog Default => shared.Value;
    public ChampionChoice[] Champions { get; }

    public ChampionProgressionCatalog(string json)
    {
        data = JsonSerializer.Deserialize<CatalogData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new JsonException("Catálogo de campeones vacío.");
        Champions = data.Campeones.Select(entry => QuestionnaireService.Champions.FirstOrDefault(choice => choice.Id == entry.Key)
            ?? new ChampionChoice(entry.Key, entry.Value.Nombre, ParseRole(entry.Value.RolPrincipal),
                entry.Value.Titulo, $"{entry.Value.Dificultad}/10")).OrderBy(choice => choice.Name).ToArray();
    }

    public ProgressionPlan? GetPlan(string champion, PlayerRole role, GameSnapshot snapshot, string? chosenRole = null)
    {
        var match = data.Campeones.FirstOrDefault(entry => string.Equals(entry.Key, champion, StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Value.Nombre, champion, StringComparison.OrdinalIgnoreCase));
        if (match.Value is null) return null;
        var roleKey = chosenRole ?? (snapshot.Player?.Position?.ToUpperInvariant() switch
        {
            "TOP" => "top", "JUNGLE" => "jungla", "MIDDLE" => "mid", "BOTTOM" => "adc", "UTILITY" => "support",
            _ => RoleKey(role)
        });
        if (!match.Value.Roles.TryGetValue(roleKey, out var build))
        {
            roleKey = match.Value.RolPrincipal;
            if (!match.Value.Roles.TryGetValue(roleKey, out build)) return null;
        }
        var items = snapshot.Player?.Items ?? [];
        var pending = build.BuildObjetivo.Where(target => !items.Any(item => item.Id == target.Id
            || item.Id is null && string.Equals(item.Name, target.Nombre, StringComparison.OrdinalIgnoreCase))).ToArray();
        var activeItems = new List<ActiveInventoryItem>();
        foreach (var item in items)
            if (item.Id is { } id && item.Slot is >= 0 and <= 6
                && data.ObjetosActivos.TryGetValue(id.ToString(), out var activeItem)
                && data.TeclaPorSlot.TryGetValue(item.Slot.Value.ToString(), out var key))
                activeItems.Add(new(id, activeItem.Nombre, activeItem.Tipo, key));

        var levels = snapshot.Player?.AbilityLevels;
        var standard = match.Key is not ("Aphelios" or "Jayce" or "Elise" or "Nidalee" or "Karma" or "Udyr");
        int? points = standard && snapshot.Level is >= 1 and <= 18 && levels is not null
            && new[] { "Q", "W", "E", "R" }.All(key => levels.ContainsKey(key))
            ? Math.Max(0, snapshot.Level.Value - levels.Values.Sum()) : null;
        string? next = null;
        if (points is > 0 && levels is not null)
        {
            var level = snapshot.Level!.Value;
            bool CanUpgrade(string ability) => ability is "Q" or "W" or "E" or "R"
                && levels.TryGetValue(ability, out var rank)
                && rank < (ability == "R" ? level >= 16 ? 3 : level >= 11 ? 2 : level >= 6 ? 1 : 0
                    : Math.Min(5, (level + 1) / 2));
            var spent = levels.Values.Sum();
            var sequenceChoice = spent < build.SecuenciaNiveles.Length ? build.SecuenciaNiveles[spent] : null;
            if (level <= 3)
                next = build.SecuenciaNiveles.Concat(build.OrdenHabilidades)
                    .FirstOrDefault(ability => ability != "R" && levels.TryGetValue(ability, out var rank) && rank == 0 && CanUpgrade(ability));
            next ??= sequenceChoice is not null && CanUpgrade(sequenceChoice) ? sequenceChoice
                : build.OrdenHabilidades.FirstOrDefault(CanUpgrade);
        }
        return new(data.Version, match.Key, roleKey, build.BuildObjetivo, build.ObjetosIniciales,
            build.OrdenHabilidades, build.SecuenciaNiveles, pending, activeItems.ToArray(), points, next, standard);
    }

    private static string RoleKey(PlayerRole role) => role switch
    {
        PlayerRole.Top => "top", PlayerRole.Jungle => "jungla", PlayerRole.Mid => "mid", PlayerRole.Adc => "adc", _ => "support"
    };

    private static PlayerRole ParseRole(string role) => role switch
    {
        "top" => PlayerRole.Top, "jungla" => PlayerRole.Jungle, "mid" => PlayerRole.Mid, "adc" => PlayerRole.Adc, _ => PlayerRole.Support
    };

    private sealed record CatalogData
    {
        public string Version { get; init; } = "";
        public Dictionary<string, CatalogChampion> Campeones { get; init; } = [];
        [JsonPropertyName("objetos_activos")] public Dictionary<string, ActiveItem> ObjetosActivos { get; init; } = [];
        [JsonPropertyName("tecla_por_slot")] public Dictionary<string, string> TeclaPorSlot { get; init; } = [];
    }

    private sealed record CatalogChampion
    {
        public string Nombre { get; init; } = "";
        public string Titulo { get; init; } = "";
        public int Dificultad { get; init; }
        [JsonPropertyName("rol_principal")] public string RolPrincipal { get; init; } = "";
        public Dictionary<string, CatalogBuild> Roles { get; init; } = [];
    }

    private sealed record CatalogBuild
    {
        [JsonPropertyName("BUILD_OBJETIVO")] public BuildItem[] BuildObjetivo { get; init; } = [];
        [JsonPropertyName("objetos_iniciales")] public BuildItem[] ObjetosIniciales { get; init; } = [];
        [JsonPropertyName("ORDEN_HABILIDADES")] public string[] OrdenHabilidades { get; init; } = [];
        [JsonPropertyName("secuencia_niveles")] public string[] SecuenciaNiveles { get; init; } = [];
    }

    private sealed record ActiveItem
    {
        public string Nombre { get; init; } = "";
        public string Tipo { get; init; } = "";
    }
}

public sealed record BuildItem(int Id, string Nombre);
public sealed record ActiveInventoryItem(int Id, string Nombre, string Tipo, string Tecla);
public sealed record ProgressionPlan(string Version, string Champion, string Role,
    BuildItem[] BUILD_OBJETIVO, BuildItem[] ObjetosIniciales, string[] ORDEN_HABILIDADES,
    string[] SecuenciaNiveles, BuildItem[] ObjetosPendientes, ActiveInventoryItem[] ObjetosActivos,
    int? PuntosHabilidad, string? SiguienteHabilidad, bool ProgresionEstandar);