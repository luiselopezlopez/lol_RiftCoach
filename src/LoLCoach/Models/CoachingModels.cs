using System.Text.Json;
using System.Text.RegularExpressions;

namespace LoLCoach.Models;

public sealed record GameSnapshot(double? GameTime, int? Level, double? Health, double? MaxHealth, double? Gold)
{
    public string? GameMode { get; init; }
    public string? MapName { get; init; }
    public PlayerGameDetails? Player { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyList<RosterPlayer> Roster { get; init; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string? OwnTeam { get; init; }

    public static GameSnapshot Parse(JsonElement activePlayer, JsonElement gameStats, JsonElement players = default)
    {
        var stats = activePlayer.TryGetProperty("championStats", out var value) ? value : default;
        static double? Number(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.Number && item.TryGetDouble(out var number)
            && double.IsFinite(number) ? number : null;
        static string? Text(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null;
        var level = Number(activePlayer, "level");
        var snapshot = new GameSnapshot(Number(gameStats, "gameTime"), level is >= 1 and <= 30 ? (int)level : null,
            Number(stats, "currentHealth"), Number(stats, "maxHealth"), Number(activePlayer, "currentGold"))
        {
            GameMode = Text(gameStats, "gameMode"),
            MapName = Text(gameStats, "mapName"),
            Player = ParsePlayerDetails(activePlayer, stats, players, Number, Text),
            Roster = ParseRoster(activePlayer, players, Number, Text),
            OwnTeam = ParseOwnTeam(activePlayer, players, Text)
        };
        return snapshot;
    }

    private static string? ParseOwnTeam(JsonElement activePlayer, JsonElement players, Func<JsonElement, string, string?> text)
    {
        var riotId = text(activePlayer, "riotId");
        if (players.ValueKind != JsonValueKind.Array || string.IsNullOrEmpty(riotId)) return null;
        var team = text(players.EnumerateArray().FirstOrDefault(player => text(player, "riotId") == riotId), "team");
        return team is "ORDER" or "CHAOS" ? team : null;
    }

    private static IReadOnlyList<RosterPlayer> ParseRoster(JsonElement activePlayer, JsonElement players,
        Func<JsonElement, string, double?> number, Func<JsonElement, string, string?> text)
    {
        if (players.ValueKind != JsonValueKind.Array) return [];
        var riotId = text(activePlayer, "riotId");
        var ownPlayer = players.EnumerateArray().FirstOrDefault(player =>
            !string.IsNullOrEmpty(riotId) && text(player, "riotId") == riotId);
        var ownTeam = text(ownPlayer, "team");
        var roster = new List<RosterPlayer>();
        foreach (var player in players.EnumerateArray())
        {
            if (player.ValueKind != JsonValueKind.Object) continue;
            var isOwn = !string.IsNullOrEmpty(riotId) && text(player, "riotId") == riotId;
            var team = text(player, "team");
            var relation = isOwn ? "Tú" : ownTeam is null || team is null ? "Sin equipo" : team == ownTeam ? "Aliado" : "Rival";
            var level = number(player, "level");
            var items = new List<ItemSnapshot>();
            if (player.TryGetProperty("items", out var itemData) && itemData.ValueKind == JsonValueKind.Array)
                foreach (var item in itemData.EnumerateArray())
                    if (text(item, "displayName") is { Length: > 0 } name)
                        items.Add(new(name, (int)Math.Clamp(number(item, "count") ?? 1, 1, 99)));
            roster.Add(new(text(player, "championName") ?? "Desconocido", relation, isOwn,
                level is >= 1 and <= 30 ? (int)level : null, text(player, "position"),
                Score(player, "kills"), Score(player, "deaths"), Score(player, "assists"),
                Score(player, "creepScore"), ScoreNumber(player, "wardScore", number),
                player.TryGetProperty("isDead", out var dead) && dead.ValueKind == JsonValueKind.True,
                number(player, "respawnTimer"), items));
        }
        return roster.OrderBy(player => player.IsOwn ? 0 : player.Relation == "Aliado" ? 1 : 2).ToArray();
    }

    private static PlayerGameDetails? ParsePlayerDetails(JsonElement activePlayer, JsonElement stats, JsonElement players,
        Func<JsonElement, string, double?> number, Func<JsonElement, string, string?> text)
    {
        var riotId = text(activePlayer, "riotId");
        var ownPlayer = players.ValueKind == JsonValueKind.Array
            ? players.EnumerateArray().FirstOrDefault(player => text(player, "riotId") == riotId)
            : default;
        var hasOwnPlayer = ownPlayer.ValueKind == JsonValueKind.Object;
        var abilities = new Dictionary<string, int>();
        if (activePlayer.TryGetProperty("abilities", out var abilityData) && abilityData.ValueKind == JsonValueKind.Object)
        {
            foreach (var ability in abilityData.EnumerateObject())
                if (ability.Name is "Q" or "W" or "E" or "R" && ability.Value.TryGetProperty("abilityLevel", out var abilityLevel)
                    && abilityLevel.TryGetInt32(out var parsedLevel) && parsedLevel is >= 0 and <= 5)
                    abilities[ability.Name] = parsedLevel;
        }

        var runes = new List<string>();
        if (activePlayer.TryGetProperty("fullRunes", out var fullRunes))
        {
            AddRuneName(fullRunes, "keystone", runes, text);
            AddRuneName(fullRunes, "primaryRuneTree", runes, text);
            AddRuneName(fullRunes, "secondaryRuneTree", runes, text);
            if (fullRunes.TryGetProperty("generalRunes", out var generalRunes) && generalRunes.ValueKind == JsonValueKind.Array)
                foreach (var rune in generalRunes.EnumerateArray())
                    if (text(rune, "displayName") is { Length: > 0 } name && !runes.Contains(name)) runes.Add(name);
        }

        var items = new List<ItemSnapshot>();
        if (hasOwnPlayer && ownPlayer.TryGetProperty("items", out var itemData) && itemData.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in itemData.EnumerateArray())
                if (text(item, "displayName") is { Length: > 0 } name)
                {
                    var count = item.TryGetProperty("count", out var countData) && countData.TryGetInt32(out var parsedCount)
                        ? Math.Clamp(parsedCount, 1, 99) : 1;
                    items.Add(new(name, count)
                    {
                        Id = number(item, "itemID") is >= 1 and <= int.MaxValue ? (int)number(item, "itemID")!.Value : null,
                        Slot = number(item, "slot") is >= 0 and <= 6 ? (int)number(item, "slot")!.Value : null
                    });
                }
        }

        var combatStats = new Dictionary<string, double>();
        foreach (var property in new[] { "attackDamage", "abilityPower", "armor", "magicResist", "attackSpeed", "abilityHaste", "moveSpeed" })
            if (number(stats, property) is { } stat) combatStats[property] = stat;

        return new PlayerGameDetails
        {
            Position = hasOwnPlayer ? text(ownPlayer, "position") : null,
            Kills = hasOwnPlayer ? Score(ownPlayer, "kills") : null,
            Deaths = hasOwnPlayer ? Score(ownPlayer, "deaths") : null,
            Assists = hasOwnPlayer ? Score(ownPlayer, "assists") : null,
            CreepScore = hasOwnPlayer ? Score(ownPlayer, "creepScore") : null,
            WardScore = hasOwnPlayer ? ScoreNumber(ownPlayer, "wardScore", number) : null,
            ResourceType = text(stats, "resourceType"),
            Resource = number(stats, "resourceValue"),
            MaxResource = number(stats, "resourceMax"),
            CombatStats = combatStats,
            AbilityLevels = abilities,
            Runes = runes,
            SummonerSpells = hasOwnPlayer ? ReadSummonerSpells(ownPlayer, text) : [],
            Items = items
        };
    }

    private static void AddRuneName(JsonElement runes, string property, ICollection<string> names,
        Func<JsonElement, string, string?> text)
    {
        if (runes.TryGetProperty(property, out var rune) && text(rune, "displayName") is { Length: > 0 } name
            && !names.Contains(name)) names.Add(name);
    }

    private static int? Score(JsonElement player, string property)
    {
        if (player.ValueKind != JsonValueKind.Object || !player.TryGetProperty("scores", out var scores)
            || scores.ValueKind != JsonValueKind.Object || !scores.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed) || parsed < 0) return null;
        return parsed;
    }

    private static double? ScoreNumber(JsonElement player, string property, Func<JsonElement, string, double?> number)
        => player.TryGetProperty("scores", out var scores) ? number(scores, property) : null;

    private static List<string> ReadSummonerSpells(JsonElement player, Func<JsonElement, string, string?> text)
    {
        var names = new List<string>();
        if (player.TryGetProperty("summonerSpells", out var spells))
            foreach (var property in new[] { "summonerSpellOne", "summonerSpellTwo" })
                if (spells.TryGetProperty(property, out var spell) && text(spell, "displayName") is { Length: > 0 } name)
                    names.Add(name);
        return names;
    }
}

public sealed record PlayerGameDetails
{
    public string? Position { get; init; }
    public int? Kills { get; init; }
    public int? Deaths { get; init; }
    public int? Assists { get; init; }
    public int? CreepScore { get; init; }
    public double? WardScore { get; init; }
    public string? ResourceType { get; init; }
    public double? Resource { get; init; }
    public double? MaxResource { get; init; }
    public Dictionary<string, double> CombatStats { get; init; } = [];
    public Dictionary<string, int> AbilityLevels { get; init; } = [];
    public List<string> Runes { get; init; } = [];
    public List<string> SummonerSpells { get; init; } = [];
    public List<ItemSnapshot> Items { get; init; } = [];
}

public sealed record ItemSnapshot(string Name, int Count)
{
    public int? Id { get; init; }
    public int? Slot { get; init; }
}

public sealed record RosterPlayer(string Champion, string Relation, bool IsOwn, int? Level, string? Position,
    int? Kills, int? Deaths, int? Assists, int? CreepScore, double? WardScore,
    bool IsDead, double? RespawnTimer, IReadOnlyList<ItemSnapshot> Items);

public enum ObservationKind { LiveGame, BotPractice }
public sealed record Observation(byte[] Jpeg, GameSnapshot State, long CapturedAt, ObservationKind Kind)
{
    public VerifiedBotPractice? Practice { get; init; }
    public byte[]? MinimapPng { get; init; }
}

public sealed class VerifiedBotPractice
{
    public string Champion { get; }
    public bool BotVerificationBypassed { get; }
    internal VerifiedBotPractice(string champion, bool botVerificationBypassed = false)
        => (Champion, BotVerificationBypassed) = (champion, botVerificationBypassed);
}

public sealed class PracticeUnavailableException(string message) : InvalidOperationException(message);

public static class BotMatchVerifier
{
    public static VerifiedBotPractice Verify(JsonElement activePlayer, JsonElement players,
        bool allowUnverifiedBotOpponentsForDebugging = false)
    {
        static string? Text(JsonElement element, string property) => element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        static bool? IsBot(JsonElement player) => player.TryGetProperty("isBot", out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
        static PracticeUnavailableException Unavailable() => new("No se puede confirmar una partida exclusivamente contra bots.");

        var localId = Text(activePlayer, "riotId");
        if (string.IsNullOrWhiteSpace(localId) || players.ValueKind != JsonValueKind.Array) throw Unavailable();
        var roster = players.EnumerateArray().ToArray();
        if (roster.Length is < 2 or > 10 || roster.Any(player => player.ValueKind != JsonValueKind.Object
            || Text(player, "team") is not ("ORDER" or "CHAOS"))) throw Unavailable();
        var matches = roster.Where(player => Text(player, "riotId") == localId).ToArray();
        if (matches.Length != 1 || IsBot(matches[0]) != false) throw Unavailable();
        var team = Text(matches[0], "team");
        var opponents = roster.Where(player => Text(player, "team") != team).ToArray();
        if (opponents.Length == 0
            || (!allowUnverifiedBotOpponentsForDebugging
                && (roster.Any(player => IsBot(player) is null) || opponents.Any(player => IsBot(player) != true))))
            throw Unavailable();
        var champion = Text(matches[0], "championName");
        if (string.IsNullOrWhiteSpace(champion)) throw Unavailable();
        return new(champion, allowUnverifiedBotOpponentsForDebugging);
    }
}
public sealed record AdviceUpdate(string Text, bool IsFinal, TimeSpan Age);

public static class ProductPolicy
{
    public const bool LiveCoachingEnabled = false;
    public const string LiveRestriction = "Práctica: solo rivales bot verificados. No implica aprobación de Riot; PvP bloqueado.";

    public static void EnsureAllowed(ObservationKind kind)
    {
        if (kind == ObservationKind.LiveGame && !LiveCoachingEnabled)
            throw new InvalidOperationException(LiveRestriction);
        if (!Enum.IsDefined(kind)) throw new InvalidOperationException("Origen de observación no válido.");
    }

    public static void EnsureAllowed(Observation observation)
    {
        EnsureAllowed(observation.Kind);
        if (observation.Kind == ObservationKind.BotPractice && observation.Practice is null)
            throw new PracticeUnavailableException("Falta la verificación de rivales bot para esta observación.");
    }
}

public interface IObservationSource
{
    Task<Observation> CaptureAsync(CancellationToken cancellationToken);
}

public interface ICoachingClient
{
    IAsyncEnumerable<string> StreamAsync(Observation observation, Services.PlayerProfile profile,
    IReadOnlyList<string> recentAdvice, CancellationToken cancellationToken);
}

public static class AdviceValidator
{
    private static readonly Regex InstructionPattern = new(
        @"\A(?:Ataca\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*\s+con\s+(?:[QWERDF1-7](?:\+[QWERDF1-7]){0,7}|clic derecho)|Ataca\s+(?!.*\scon\s)[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Apoya\s+(?:Top|Mid|Bot)|Congela(?:\s+oleada)?|Empuja\s+(?:lento|r[aá]pido)|(?:Presiona|Farmea)\s+(?:Top|Mid|Bot)|Huye(?:\s+de\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*)?|(?:Aléjate|Alejate|Sepárate|Separate)\s+de\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Compra\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Ve\s+a\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Avanza|Retrocede|Espera|Mejora\s+[QWER]|Usa\s+[QWERDF1-7](?:\s*(?:\+|,)?\s*[QWERDF1-7]){0,7}|Pulsa\s+B|Escóndete)[.!]?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex EnglishInstructionPattern = new(
        @"\A(?:Attack\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*\s+with\s+(?:[QWERDF1-7](?:\+[QWERDF1-7]){0,7}|right click)|Attack\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Help\s+(?:Top|Mid|Bot)|Freeze(?:\s+the)?\s+wave|Slow push|Fast push|(?:Pressure|Farm)\s+(?:Top|Mid|Bot)|Retreat(?:\s+(?:from\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|to base))?|Stay away from\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Buy\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Go to\s+[\p{L}\p{N}][\p{L}\p{N} '’.-]*|Return to base|Recall(?: to base)?|Advance|Back up|Wait|Upgrade\s+[QWER]|Use\s+[QWERDF1-7](?:\s*(?:\+|,)?\s*[QWERDF1-7]){0,7}|Press\s+B|Hide)[.!]?\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex TacticalPattern = new(@"\A(?:Táctica|Tactic): (?<text>[^\r\n]{3,220})\z",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsValid(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 360) return false;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        if (normalized.Any(character => char.IsControl(character) && character != '\n')) return false;
        var lines = normalized.Split('\n');
        if (lines.Length is < 1 or > 2
            || !(InstructionPattern.IsMatch(lines[0]) || EnglishInstructionPattern.IsMatch(lines[0]))) return false;
        if (lines.Length == 1) return true;
        var tactical = TacticalPattern.Match(lines[1]);
        return tactical.Success
            && tactical.Groups["text"].Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length <= 15;
    }
}

public interface IModelClient : ICoachingClient
{
    Task<string> ProbeAsync(CancellationToken cancellationToken);
}

public interface IChampionGuideClient
{
    Task<string> CreateGuideAsync(Services.PracticeState state, Services.PlayerProfile profile, CancellationToken cancellationToken);
}
