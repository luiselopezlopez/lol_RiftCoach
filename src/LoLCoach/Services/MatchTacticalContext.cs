using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed record MatchPhase(string Phase, double? Minutes, int? Level, bool IsEstimate = true);
public sealed record MapSide(string Equipo, string BasePropia, string BaseEnemiga);
public sealed record EnemyTactics(string Champion, int? Level, int? Kills, int? Deaths, int? Assists,
    bool IsDead, string[] Items);

public static class MatchTacticalContext
{
    public static MatchPhase EstimatePhase(GameSnapshot snapshot)
    {
        var minutes = snapshot.GameTime is >= 0 ? snapshot.GameTime / 60 : null;
        var level = snapshot.Level is >= 1 and <= 18 ? snapshot.Level : null;
        var phase = minutes is null && level is null ? "desconocida"
            : minutes >= 35 || minutes >= 28 && level >= 14 ? "late"
            : minutes >= 14 || level >= 9 ? "mid" : "early";
        return new(phase, minutes, level);
    }

    public static MapSide? Side(GameSnapshot snapshot) => snapshot.OwnTeam switch
    {
        "ORDER" => new("azul", "abajo a la izquierda", "arriba a la derecha"),
        "CHAOS" => new("rojo", "arriba a la derecha", "abajo a la izquierda"),
        _ => null
    };

    public static EnemyTactics[] Enemies(GameSnapshot snapshot) => snapshot.Roster
        .Where(player => player.Relation == "Rival")
        .Select(player => new EnemyTactics(player.Champion, player.Level, player.Kills, player.Deaths, player.Assists,
            player.IsDead, player.Items.Select(item => item.Name).ToArray())).ToArray();
}