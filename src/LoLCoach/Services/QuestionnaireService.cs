namespace LoLCoach.Services;

public enum PlayerRole { Top, Jungle, Mid, Adc, Support }
public sealed record QuizOption(string Text, int[] Weights);
public sealed record QuizQuestion(string Text, QuizOption[] Options);
public sealed record ChampionChoice(string Id, string Name, PlayerRole Role, string Reason, string Difficulty);
public sealed record QuizResult(PlayerRole[] Roles, ChampionChoice[] Champions, string[] Reasons);
public sealed record PlayerProfile(PlayerRole Role, string Champion, int[] Answers);

public static class QuestionnaireService
{
    public static string RoleName(PlayerRole role) => role switch
    {
        PlayerRole.Top => "Top", PlayerRole.Jungle => "Jungla", PlayerRole.Mid => "Medio",
        PlayerRole.Adc => "Tirador", _ => "Apoyo"
    };

    public static readonly QuizQuestion[] Questions =
    [
        new("¿A qué distancia prefieres jugar?", [
            new("Cerca y resistiendo golpes", [4, 2, 0, 0, 1]),
            new("A distancia, buscando daño", [0, 0, 3, 4, 1]),
            new("Desde una posición de apoyo", [0, 1, 1, 0, 4])]),
        new("¿Qué ritmo te atrae más?", [
            new("Un duelo de paciencia", [4, 0, 1, 2, 0]),
            new("Moverme y buscar oportunidades", [0, 4, 3, 0, 1]),
            new("Acompañar y proteger", [0, 1, 0, 2, 4])]),
        new("¿Qué valoras al cometer un error?", [
            new("Poder resistir y recuperarme", [4, 3, 0, 0, 1]),
            new("Tener alcance para mantener distancia", [0, 0, 3, 4, 1]),
            new("Contar con alguien a mi lado", [0, 0, 0, 3, 4])]),
        new("¿Cuánta dificultad mecánica buscas al empezar?", [
            new("Pocas habilidades y un plan sencillo", [4, 1, 2, 1, 3]),
            new("Coordinar ataques y movimiento", [1, 2, 2, 4, 0]),
            new("Aprender combinaciones poco a poco", [1, 2, 4, 1, 1])]),
        new("¿Qué momento te resulta más satisfactorio?", [
            new("Ganar mi enfrentamiento", [4, 1, 3, 1, 0]),
            new("Hacer daño constante al equipo rival", [1, 1, 2, 4, 0]),
            new("Salvar o facilitar la jugada de un aliado", [0, 2, 1, 0, 4])]),
        new("¿Cómo prefieres repartir tu atención?", [
            new("En mi zona y mi rival", [4, 0, 2, 2, 0]),
            new("En varias zonas del mapa", [0, 4, 3, 0, 2]),
            new("En mis compañeros cercanos", [0, 1, 0, 2, 4])]),
        new("¿Te gustaría planear rutas y objetivos?", [
            new("Sí, aunque necesite más aprendizaje", [0, 5, 2, 0, 1]),
            new("Prefiero aprender primero una línea", [4, 0, 3, 3, 0]),
            new("Prefiero acompañar las decisiones del equipo", [0, 1, 0, 1, 4])]),
        new("¿Qué tarea repetida te atrae más?", [
            new("Conseguir oro con los últimos golpes", [3, 0, 2, 4, 0]),
            new("Gestionar campamentos y recorridos", [0, 5, 0, 0, 0]),
            new("Cuidar la visión y ayudar al equipo", [0, 1, 0, 0, 5])])
    ];

    public static readonly ChampionChoice[] Champions =
    [
        new("Garen", "Garen", PlayerRole.Top, "Resistencia y habilidades sencillas para practicar el duelo.", "Baja"),
        new("Malphite", "Malphite", PlayerRole.Top, "Un tanque con un propósito claro en las peleas de equipo.", "Baja"),
        new("Mordekaiser", "Mordekaiser", PlayerRole.Top, "Daño cercano con un ritmo pausado para aprender distancias.", "Media"),
        new("Amumu", "Amumu", PlayerRole.Jungle, "Control de masas y una función clara junto al equipo.", "Baja mecánica; jungla exige mapa"),
        new("Warwick", "Warwick", PlayerRole.Jungle, "Recuperación de vida para familiarizarte con campamentos.", "Media"),
        new("Nunu", "Nunu y Willump", PlayerRole.Jungle, "Movilidad y herramientas para aprender rutas y objetivos.", "Media"),
        new("Annie", "Annie", PlayerRole.Mid, "Alcance y pocas habilidades para aprender recursos y oleadas.", "Baja"),
        new("Lux", "Lux", PlayerRole.Mid, "Daño a distancia para practicar puntería y posición.", "Media"),
        new("Malzahar", "Malzahar", PlayerRole.Mid, "Un plan de línea claro para centrarte en el mapa.", "Media"),
        new("Ashe", "Ashe", PlayerRole.Adc, "Ataques a distancia para practicar movimiento y posición.", "Baja mecánica; posición exigente"),
        new("MissFortune", "Miss Fortune", PlayerRole.Adc, "Un patrón de daño accesible para aprender la línea inferior.", "Baja"),
        new("Sivir", "Sivir", PlayerRole.Adc, "Oleadas y protección propia para practicar tiempos.", "Media"),
        new("Sona", "Sona", PlayerRole.Support, "Ayuda al equipo con habilidades sencillas; necesita buena posición.", "Baja"),
        new("Soraka", "Soraka", PlayerRole.Support, "Protección y curación para aprender a observar aliados.", "Baja mecánica; posición exigente"),
        new("Leona", "Leona", PlayerRole.Support, "Resistencia y control; requiere elegir bien cuándo comprometerse.", "Media")
    ];

    public static QuizResult Evaluate(IReadOnlyList<int> answers)
    {
        if (answers.Count != Questions.Length)
            throw new ArgumentException("Completa las ocho preguntas.", nameof(answers));
        var scores = new int[5];
        var evidence = new List<(int Weight, string Text)>[5];
        for (var roleIndex = 0; roleIndex < 5; roleIndex++) evidence[roleIndex] = [];
        for (var questionIndex = 0; questionIndex < Questions.Length; questionIndex++)
        {
            var answer = answers[questionIndex];
            if (answer < 0 || answer >= Questions[questionIndex].Options.Length)
                throw new ArgumentException("Selecciona una respuesta válida.", nameof(answers));
            var option = Questions[questionIndex].Options[answer];
            for (var roleIndex = 0; roleIndex < 5; roleIndex++)
            {
                scores[roleIndex] += option.Weights[roleIndex];
                evidence[roleIndex].Add((option.Weights[roleIndex], option.Text));
            }
        }
        var roles = Enum.GetValues<PlayerRole>().OrderByDescending(role => scores[(int)role]).ThenBy(role => role).Take(2).ToArray();
        var preferredDifficulty = answers[3] == 0 ? "Baja" : "Media";
        var primary = Champions.Where(champion => champion.Role == roles[0])
            .OrderByDescending(champion => champion.Difficulty.StartsWith(preferredDifficulty, StringComparison.Ordinal)).Take(2);
        var secondary = Champions.Where(champion => champion.Role == roles[1]).Take(1);
        var reasons = roles.Select(role => string.Join(" · ", evidence[(int)role]
            .OrderByDescending(item => item.Weight).Take(2).Select(item => item.Text))).ToArray();
        return new(roles, primary.Concat(secondary).ToArray(), reasons);
    }
}
