using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Data;
using System.Windows.Markup;

namespace LoLCoach.Services;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["RIFT COACH"] = "RIFT COACH",
        ["ENTRENADOR DE LA GRIETA"] = "LEAGUE COACH",
        ["_Configuración"] = "_Settings",
        ["_Ventanas"] = "_Windows",
        ["Campeón"] = "Champion",
        ["Miniguía"] = "Quick guide",
        ["Minimapa"] = "Minimap",
        ["Entrenamiento"] = "Coaching",
        ["Campeones"] = "Champions",
        ["Estrategia del campeón"] = "Champion strategy",
        ["Arrastra para mover"] = "Drag to move",
        ["Cerrar ventana"] = "Close window",
        ["Redimensionar"] = "Resize",
        ["Sesión y partida"] = "Session and game",
        ["Recomendación de campeón"] = "Champion recommendation",
        ["Recomendación de campeón y posición"] = "Champion and role recommendation",
        ["Restablecer disposición"] = "Reset layout",
        ["Pausa la sesión para editar la configuración."] = "Pause the session to edit settings.",
        ["Campeón elegido"] = "Selected champion",
        ["CAMPEÓN ELEGIDO"] = "SELECTED CHAMPION",
        ["TU CAMPEÓN EN PARTIDA"] = "YOUR IN-GAME CHAMPION",
        ["NIVEL"] = "LEVEL",
        ["VIDA"] = "HEALTH",
        ["ORO"] = "GOLD",
        ["Barra de vida"] = "Health bar",
        ["Tus atributos"] = "Your stats",
        ["La miniguía aparecerá al detectar la partida."] = "The quick guide appears when a game is detected.",
        ["Historial de consejos"] = "Coaching history",
        ["Recap de partida"] = "Match recap",
        ["Los datos de cierre no confirman el resultado."] = "The closing data does not confirm the result.",
        ["Sesión, partida y depuración"] = "Session, game, and diagnostics",
        ["Iniciar sesión"] = "Start session",
        ["INICIAR"] = "START",
        ["Pausar y cancelar solicitudes"] = "Pause and cancel requests",
        ["Fin de partida"] = "End match",
        ["FIN PARTIDA"] = "END MATCH",
        ["Finalizar sesión y generar el recap disponible"] = "End the session and generate the available recap",
        ["Fin de partida indicado. Generando recap."] = "Match end indicated. Generating recap.",
        ["PAUSAR"] = "PAUSE",
        ["Sesión"] = "Session",
        ["Inicio automático al detectar partida"] = "Start automatically when a game is detected",
        ["Depuración: omitir verificación de bots"] = "Debug: skip bot verification",
        ["Puede permitir sesiones PvP. Úsalo solo para pruebas controladas."] = "This can allow PvP sessions. Use it only for controlled testing.",
        ["MODELO"] = "MODEL",
        ["Solicitudes de sesión: "] = "Session requests: ",
        ["Autoplay de voz en español"] = "Speak coaching aloud",
        ["Voz española"] = "Voice for the selected language",
        ["VOLUMEN"] = "VOLUME",
        ["Volumen"] = "Volume",
        ["Partida"] = "Game",
        ["Dato"] = "Field",
        ["Valor"] = "Value",
        ["Información recuperada de LoL"] = "League of Legends game data",
        ["Depuración"] = "Diagnostics",
        ["CONSULTAS A LAS API"] = "API REQUESTS",
        ["Limpiar registros"] = "Clear logs",
        ["Hora"] = "Time",
        ["Tiempo"] = "Time",
        ["Método"] = "Method",
        ["Ruta"] = "Path",
        ["Resultado"] = "Result",
        ["Consultas a la API de LoL"] = "League of Legends API requests",
        ["Consultas a la API de Azure OpenAI"] = "Azure OpenAI API requests",
        ["Captura"] = "Capture",
        ["Sin captura activa"] = "No active capture",
        ["Captura de pantalla enviada"] = "Screenshot sent",
        ["Recorte enviado sin pérdida, a resolución nativa."] = "Lossless crop sent at native resolution.",
        ["Sin minimapa enviado"] = "No minimap sent",
        ["Recorte del minimapa enviado"] = "Minimap crop sent",
        ["Marcador de todos los campeones"] = "All champions scoreboard",
        ["Nv. "] = "Lv. ",
        ["Tu punto de partida"] = "Your starting point",
        ["Pregunta anterior"] = "Previous question",
        ["ANTERIOR"] = "BACK",
        ["Respuestas del cuestionario"] = "Questionnaire answers",
        ["ELEGIR"] = "CHOOSE",
        ["USAR ESTE PERFIL"] = "USE THIS PROFILE",
        ["REPETIR CUESTIONARIO"] = "RETAKE QUESTIONNAIRE",
        ["Sin perfil"] = "No profile",
        ["Ver resultados"] = "See results",
        ["Siguiente"] = "Next",
        ["Otra opción"] = "Another option",
        ["Otra opción: "] = "Another option: ",
        ["Actualizado: "] = "Updated: ",
        ["Configuración"] = "Settings",
        ["Idioma"] = "Language",
        ["CONEXIÓN AL MODELO"] = "MODEL CONNECTION",
        ["_Model (nombre del despliegue)"] = "_Model (deployment name)",
        ["Deja la clave vacía para conservar la actual. Una clave nueva se guarda cifrada para tu usuario de Windows."] = "Leave the key blank to keep the current one. A new key is encrypted for your Windows account.",
        ["Borrar la clave guardada"] = "Delete the saved key",
        ["Los cambios se aplican al pulsar Iniciar."] = "Changes apply when you press Start.",
        ["PROMPT DE COACHING"] = "COACHING PROMPT",
        ["Edita el prompt que recibe el modelo. Cada cambio guardado conserva la versión anterior; los cambios se aplican al pulsar Iniciar."] = "Edit the prompt sent to the model. Each saved change keeps the previous version; changes apply when you press Start.",
        ["Prompt de coaching"] = "Coaching prompt",
        ["_Versiones anteriores"] = "_Previous versions",
        ["Restaurar versión"] = "Restore version",
        ["Restaurar versión del prompt"] = "Restore prompt version",
        ["Versiones anteriores del prompt"] = "Previous prompt versions",
        ["Guardar prompt"] = "Save prompt",
        ["Guardar"] = "Save",
        ["Cancelar"] = "Cancel",
        ["Español"] = "Spanish",
        ["Inglés"] = "English",
        ["Borrador"] = "Draft",
        ["Completo"] = "Complete",
        ["Texto completo"] = "Complete text",
        ["Sin consejo"] = "No coaching yet",
        ["Sin observaciones"] = "No observations",
        ["Sin medición"] = "Not measured",
        ["Preparando arranque"] = "Preparing to start",
        ["Comprobando el modelo"] = "Checking the model",
        ["Modelo disponible"] = "Model available",
        ["Esperando el inicio de la partida"] = "Waiting for a game to start",
        ["Partida detectada"] = "Game detected",
        ["Esperando una partida con rivales bot verificados"] = "Waiting for a game with verified bot opponents",
        ["Esperando respuesta de la API local"] = "Waiting for a response from the local API",
        ["Escena preparada"] = "Scene ready",
        ["Analizando"] = "Analyzing",
        ["Consejo disponible"] = "Coaching available",
        ["Respuesta descartada"] = "Response discarded",
        ["Respuesta caducada"] = "Response expired",
        ["Reintentando la API local"] = "Retrying the local API",
        ["Generando recap de la partida"] = "Generating match recap",
        ["Recap de partida disponible"] = "Match recap available",
        ["Recap de partida no disponible"] = "Match recap unavailable",
        ["Sin fase disponible"] = "Game phase unavailable",
        ["Sin datos de partida"] = "No game data",
        ["Tu campeón"] = "Your champion",
        ["Esperando datos de jugadores"] = "Waiting for player data",
        ["Pendiente"] = "Pending",
        ["Disponible"] = "Available",
        ["Comprobando"] = "Checking",
        ["Sin consultar"] = "Not checked",
        ["Sin datos"] = "No data",
        ["Detenido"] = "Stopped",
        ["Sin conexión activa"] = "No active connection",
        ["Partida disponible"] = "Game available",
        ["Bots verificados"] = "Verified bots",
        ["Nivel"] = "Level",
        ["Vida restante"] = "Health remaining",
        ["Oro disponible"] = "Available gold",
        ["Tiempo de partida"] = "Game time",
        ["Rivales"] = "Opponents",
        ["Súbditos (CS)"] = "Minions (CS)",
        ["Visión"] = "Vision",
        ["Daño de ataque"] = "Attack damage",
        ["Poder de habilidad"] = "Ability power",
        ["Armadura"] = "Armor",
        ["Resistencia mágica"] = "Magic resistance",
        ["Velocidad de ataque"] = "Attack speed",
        ["Aceleración"] = "Ability haste",
        ["Velocidad"] = "Movement speed",
        ["Habilidades"] = "Abilities",
        ["Hechizos"] = "Spells",
        ["Runas"] = "Runes",
        ["Recurso"] = "Resource",
        ["Retratos no disponibles; el resultado sigue siendo válido."] = "Portraits are unavailable; your result is still valid.",
        ["Resistencia y habilidades sencillas para practicar el duelo."] = "Durability and simple abilities for practicing lane trades.",
        ["Movilidad y herramientas para aprender rutas y objetivos."] = "Mobility and tools for learning jungle routes and objectives.",
        ["No se pudo guardar el ajuste de depuración."] = "Could not save the debug setting.",
        ["No se pudo guardar el perfil."] = "Could not save the profile.",
        ["Pausa la sesión antes de cambiar el prompt."] = "Pause the session before changing the prompt.",
        ["Prompt guardado. Pulsa Iniciar para aplicarlo."] = "Prompt saved. Press Start to apply it.",
        ["Versión del prompt restaurada. Pulsa Iniciar para aplicarla."] = "Prompt version restored. Press Start to apply it.",
        ["Pausa la sesión antes de cambiar la configuración."] = "Pause the session before changing settings.",
        ["Configuración guardada. Pulsa Iniciar para aplicarla."] = "Settings saved. Press Start to apply them.",
        ["No se pudo guardar la disposición de las ventanas."] = "Could not save the window layout.",
        ["No se pudo restablecer la disposición de las ventanas."] = "Could not reset the window layout.",
        ["Esperando la ventana del juego"] = "Waiting for the game window",
        ["Incompleto · no vigente"] = "Incomplete · outdated",
        ["La ApiKey cifrada de localsettings.json no puede leerse con este usuario de Windows."] = "The encrypted ApiKey in localsettings.json cannot be read by this Windows account.",
        ["Falta Model."] = "Model is missing.",
        ["Falta ApiKey."] = "ApiKey is missing.",
        ["Rival de línea: sin datos"] = "Lane opponent: no data",
        ["Rival de línea: varios candidatos"] = "Lane opponent: multiple candidates",
        ["Rival de línea: sin datos de posición"] = "Lane opponent: no role data",
        ["No hay una voz española compatible instalada."] = "No compatible voice for the selected language is installed.",
        ["Síntesis de voz no disponible."] = "Speech synthesis is unavailable.",
        ["Voz española disponible"] = "Voice for the selected language is available",
        ["No se encontró la versión del prompt seleccionada."] = "The selected prompt version was not found.",
        ["No se pudo cifrar la clave. La configuración no se ha guardado."] = "The key could not be encrypted. Settings were not saved.",
        ["No se pudo acceder al archivo de configuración. Comprueba la ruta y los permisos."] = "Could not access the settings file. Check the path and permissions.",
        ["Introduce el nombre del despliegue."] = "Enter the deployment name.",
        ["Usa un endpoint Azure OpenAI HTTPS con la ruta /openai/v1/."] = "Use an Azure OpenAI HTTPS endpoint with the /openai/v1/ path.",
        ["El prompt no puede estar vacío."] = "The prompt cannot be empty.",
        ["Introduce una clave válida."] = "Enter a valid key.",
        ["La versión del prompt seleccionada no es válida."] = "The selected prompt version is invalid.",
        ["La versión del prompt ya no existe."] = "The prompt version no longer exists.",
        ["El archivo de configuración tiene un formato inválido. Corrígelo antes de guardar."] = "The settings file has an invalid format. Fix it before saving.",
        ["No se pudo completar la operación. Revisa la conexión y localsettings.json."] = "The operation could not be completed. Check the connection and localsettings.json.",
        ["Operación detenida o tiempo de espera agotado."] = "Operation stopped or timed out.",
        ["Azure rechazó la credencial o sus permisos."] = "Azure rejected the credential or its permissions.",
        ["Revisa Endpoint y Model en localsettings.json."] = "Check Endpoint and Model in localsettings.json.",
        ["Azure limita las solicitudes. Sesión detenida; espera antes de reintentar."] = "Azure rate-limited requests. The session stopped; wait before trying again.",
        ["El modelo rechazó la solicitud. Comprueba visión y parámetros."] = "The model rejected the request. Check vision support and parameters.",
        ["Revisa Endpoint, Model y ApiKey en localsettings.json."] = "Check Endpoint, Model, and ApiKey in localsettings.json.",
        ["Conexión no disponible"] = "Connection unavailable",
        ["Correcto"] = "Success",
        ["Error HTTP"] = "HTTP error",
        ["Cancelado / tiempo agotado"] = "Canceled / timed out",
        ["El modelo agotó el límite de tokens antes de completar el consejo."] = "The model reached its token limit before finishing the coaching response.",
        ["Azure filtró la respuesta del modelo."] = "Azure filtered the model response.",
        ["El modelo terminó sin una respuesta completa."] = "The model stopped without a complete response.",
        ["El modelo no respondió a la pregunta inicial."] = "The model did not answer the initial question.",
        ["Partida detectada; verificación de bots omitida (depuración)"] = "Game detected; bot verification skipped (debug)",
        ["Preparando miniguía del campeón"] = "Preparing the champion quick guide",
        ["Miniguía disponible"] = "Quick guide available",
        ["Miniguía no disponible; continúa el entrenamiento"] = "Quick guide unavailable; coaching will continue",
        ["No se pudo preparar la miniguía del campeón."] = "Could not prepare the champion quick guide.",
        ["Sesión detenida"] = "Session stopped",
        ["La observación ha caducado antes del envío."] = "The observation expired before it could be sent.",
        ["Respuesta demasiado larga; sesión detenida."] = "Response too long; session stopped.",
        ["Catálogo no disponible; se mantienen las recomendaciones locales."] = "Catalog unavailable; local recommendations will be used.",
        ["Identificador de campeón o versión no válido."] = "Champion ID or version is invalid.",
        ["Estado propio incompleto; no se enviará esta observación."] = "Player state is incomplete; this observation will not be sent.",
        ["Selecciona la ventana del juego, no el cliente ni otra aplicación."] = "Select the game window, not the client or another app.",
        ["La captura del juego se ha detenido; no se enviará esta observación."] = "Game capture stopped; this observation will not be sent.",
        ["Captura y estado no están sincronizados; no se enviará la observación."] = "Capture and game state are out of sync; this observation will not be sent.",
        ["La captura está vacía o negra; comprueba el modo de ventana."] = "The capture is empty or black; check the window mode.",
        ["La ventana de League of Legends no está visible. Reanuda la captura."] = "The League of Legends window is not visible. Resume capture.",
        ["No ha llegado una captura nueva del juego; sesión detenida."] = "No new game capture arrived; session stopped.",
        ["Captura detenida; no se enviarán más imágenes."] = "Capture stopped; no more images will be sent.",
        ["Captura no disponible. Vuelve a seleccionar la ventana."] = "Capture is unavailable. Select the window again.",
        ["Dimensiones de captura no válidas."] = "Capture dimensions are invalid.",
        ["Este equipo no admite Windows Graphics Capture."] = "This device does not support Windows Graphics Capture.",
        ["Selecciona una ventana visible y no minimizada."] = "Select a visible, non-minimized window.",
        ["Ya hay una captura pendiente."] = "A capture is already pending.",
        ["Rift Coach isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or registered trademarks of Riot Games, Inc."] = "Rift Coach isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or registered trademarks of Riot Games, Inc.",
        ["Rift Coach no cuenta con el respaldo de Riot Games ni refleja las opiniones de Riot Games ni de ninguna persona implicada oficialmente en producir o gestionar sus propiedades. Riot Games y todas las propiedades asociadas son marcas comerciales o marcas registradas de Riot Games, Inc."] = "Rift Coach isn't endorsed by Riot Games and doesn't reflect the views or opinions of Riot Games or anyone officially involved in producing or managing Riot Games properties. Riot Games, and all associated properties are trademarks or registered trademarks of Riot Games, Inc.",
        ["Sin objetos"] = "No items",
        ["Sin posición"] = "No role",
        ["Sin equipo"] = "No team",
        ["Aliado"] = "Ally",
        ["Rival"] = "Opponent",
        ["Tú"] = "You",
        ["Jungla"] = "Jungle",
        ["Medio"] = "Mid",
        ["Tirador"] = "Bottom",
        ["Apoyo"] = "Support",
        ["Desconocido"] = "Unknown",
        ["Baja"] = "Low",
        ["Media"] = "Medium",
        ["Baja mecánica; jungla exige mapa"] = "Low mechanics; jungle requires map awareness",
        ["Baja mecánica; posición exigente"] = "Low mechanics; demanding positioning",
        ["¿A qué distancia prefieres jugar?"] = "What range do you prefer to play at?",
        ["Cerca y resistiendo golpes"] = "Up close, able to take hits",
        ["A distancia, buscando daño"] = "At range, looking for damage",
        ["Desde una posición de apoyo"] = "From a support position",
        ["¿Qué ritmo te atrae más?"] = "What pace appeals to you?",
        ["Un duelo de paciencia"] = "A patient duel",
        ["Moverme y buscar oportunidades"] = "Move around and look for opportunities",
        ["Acompañar y proteger"] = "Stay with teammates and protect them",
        ["¿Qué valoras al cometer un error?"] = "What matters to you after making a mistake?",
        ["Poder resistir y recuperarme"] = "Being able to survive and recover",
        ["Tener alcance para mantener distancia"] = "Having range to keep my distance",
        ["Contar con alguien a mi lado"] = "Having someone by my side",
        ["¿Cuánta dificultad mecánica buscas al empezar?"] = "How much mechanical difficulty do you want as a beginner?",
        ["Pocas habilidades y un plan sencillo"] = "Few abilities and a simple plan",
        ["Coordinar ataques y movimiento"] = "Coordinate attacks and movement",
        ["Aprender combinaciones poco a poco"] = "Learn ability combos step by step",
        ["¿Qué momento te resulta más satisfactorio?"] = "What feels most rewarding?",
        ["Ganar mi enfrentamiento"] = "Winning my lane matchup",
        ["Hacer daño constante al equipo rival"] = "Dealing steady damage to the enemy team",
        ["Salvar o facilitar la jugada de un aliado"] = "Saving an ally or enabling their play",
        ["¿Cómo prefieres repartir tu atención?"] = "How do you prefer to divide your attention?",
        ["En mi zona y mi rival"] = "On my lane and opponent",
        ["En varias zonas del mapa"] = "Across several areas of the map",
        ["En mis compañeros cercanos"] = "On nearby teammates",
        ["¿Te gustaría planear rutas y objetivos?"] = "Would you like to plan routes and objectives?",
        ["Sí, aunque necesite más aprendizaje"] = "Yes, even if it means more to learn",
        ["Prefiero aprender primero una línea"] = "I would rather learn one lane first",
        ["Prefiero acompañar las decisiones del equipo"] = "I would rather support the team's decisions",
        ["¿Qué tarea repetida te atrae más?"] = "Which repeated task appeals to you most?",
        ["Conseguir oro con los últimos golpes"] = "Earn gold with last hits",
        ["Gestionar campamentos y recorridos"] = "Manage jungle camps and routes",
        ["Cuidar la visión y ayudar al equipo"] = "Maintain vision and help the team",
        ["Un tanque con un propósito claro en las peleas de equipo."] = "A tank with a clear purpose in team fights.",
        ["Daño cercano con un ritmo pausado para aprender distancias."] = "Close-range damage at a steady pace that teaches spacing.",
        ["Control de masas y una función clara junto al equipo."] = "Crowd control and a clear role alongside the team.",
        ["Recuperación de vida para familiarizarte con campamentos."] = "Health recovery to help you learn jungle camps.",
        ["Nunu y Willump"] = "Nunu & Willump",
        ["Alcance y pocas habilidades para aprender recursos y oleadas."] = "Range and a small ability set for learning resources and waves.",
        ["Daño a distancia para practicar puntería y posición."] = "Ranged damage for practicing aim and positioning.",
        ["Un plan de línea claro para centrarte en el mapa."] = "A clear lane plan that lets you focus on the map.",
        ["Ataques a distancia para practicar movimiento y posición."] = "Ranged attacks for practicing movement and positioning.",
        ["Un patrón de daño accesible para aprender la línea inferior."] = "An accessible damage pattern for learning the bottom lane.",
        ["Oleadas y protección propia para practicar tiempos."] = "Wave control and self-protection for practicing timing.",
        ["Ayuda al equipo con habilidades sencillas; necesita buena posición."] = "Helps the team with simple abilities; needs good positioning.",
        ["Protección y curación para aprender a observar aliados."] = "Protection and healing for learning to watch your teammates.",
        ["Resistencia y control; requiere elegir bien cuándo comprometerse."] = "Durability and crowd control; choose carefully when to commit.",
        ["Selecciona una respuesta válida."] = "Select a valid answer.",
        ["Completa las ocho preguntas."] = "Answer all eight questions.",
        ["Catálogo de campeones vacío."] = "Champion catalog is empty.",
        ["Falta el catálogo de progresión de campeones."] = "Champion progression catalog is missing.",
        ["Falta la verificación de rivales bot para esta observación."] = "Bot opponent verification is missing for this observation.",
        ["No se puede confirmar una partida exclusivamente contra bots."] = "A match against bots only could not be confirmed.",
        ["Origen de observación no válido."] = "Observation source is invalid.",
        ["Práctica: solo rivales bot verificados. No implica aprobación de Riot; PvP bloqueado."] = "Practice: verified bot opponents only. This does not imply Riot endorsement; PvP is blocked.",
        ["Rift Coach"] = "Rift Coach"
    };

    private static readonly Regex LaneOpponent = new(@"\ARival de línea: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Phase = new(@"\AFase estimada: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Difficulty = new(@"\ADificultad: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Count = new(@"\A(\d+) campeones · K/D/A y súbditos \(CS\)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RosterUpdated = new(@"\A(\d+) campeones · K/D/A y súbditos \(CS\) · (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex LastUpdated = new(@"\AÚltima actualización: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex FullPhrase = new(@"\AFrase completa: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Payload = new(@"\AImágenes actuales \(sin historial\): (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Level = new(@"\ANv\. (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Vision = new(@"\AVisión: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Dead = new(@"\AMuerto \((.+)\)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Tactical = new(@"\ATáctica: (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Stat = new(@"\A(Nivel|Vida|Oro) (.+)\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Age = new(@"\A([\d.,]+) s desde la captura\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex QuizResult = new(@"\A([^:]+): (.+)\. Otra opción: ([^.]+)\.\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private string language = "es";

    public static LocalizationManager Instance { get; } = new();
    public static string[] SupportedLanguages { get; } = ["es", "en"];
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language
    {
        get => language;
        private set
        {
            if (language == value) return;
            language = value;
            OnPropertyChanged();
        }
    }

    public string this[string source] => Translate(source);

    public void SetLanguage(string? value)
    {
        var normalized = NormalizeLanguage(value);
        var languageChanged = Language != normalized;
        var culture = CultureInfo.GetCultureInfo(normalized == "en" ? "en-US" : "es-ES");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        if (languageChanged) Language = normalized;
        if (languageChanged) OnPropertyChanged("Item[]");
    }

    public static string NormalizeLanguage(string? value)
        => string.Equals(value, "en", StringComparison.OrdinalIgnoreCase) ? "en" : "es";

    public string Translate(string? source)
    {
        if (string.IsNullOrEmpty(source) || Language != "en") return source ?? "";
        if (English.TryGetValue(source, out var translated)) return translated;
        var rosterUpdate = RosterUpdated.Match(source);
        if (rosterUpdate.Success)
        {
            var updatedAt = rosterUpdate.Groups[2].Value;
            return $"{rosterUpdate.Groups[1].Value} champions · K/D/A and minions (CS) · {Translate(updatedAt)}";
        }
        if (source.Contains(" · ", StringComparison.Ordinal))
            return string.Join(" · ", source.Split(" · ").Select(Translate));
        if (TryPattern(LaneOpponent, source, out var suffix))
            return $"Lane opponent: {TranslateSuffix(suffix)}";
        if (TryPattern(Phase, source, out suffix))
        {
            var phase = suffix switch
            {
                "temprana" => "early game", "media" => "mid game", "tardía" => "late game", "desconocida" => "unknown", _ => suffix
            };
            return $"Estimated phase: {phase}";
        }
        if (TryPattern(Difficulty, source, out suffix)) return $"Difficulty: {Translate(suffix)}";
        if (TryPattern(Count, source, out suffix)) return $"{suffix} champions · K/D/A and minions (CS)";
        if (TryPattern(LastUpdated, source, out suffix)) return $"Last updated: {suffix}";
        if (TryPattern(FullPhrase, source, out suffix)) return $"Full response: {suffix}";
        if (TryPattern(Payload, source, out suffix)) return $"Current images (no history): {suffix}";
        if (TryPattern(Level, source, out suffix)) return $"Lv. {suffix}";
        if (TryPattern(Vision, source, out suffix)) return $"Vision: {suffix}";
        if (TryPattern(Dead, source, out suffix)) return $"Dead ({suffix})";
        if (TryPattern(Tactical, source, out suffix)) return $"Tactic: {suffix}";
        var stat = Stat.Match(source);
        if (stat.Success)
        {
            var label = stat.Groups[1].Value switch
            {
                "Nivel" => "Level", "Vida" => "Health", _ => "Gold"
            };
            return $"{label} {stat.Groups[2].Value}";
        }
        if (TryPattern(Age, source, out suffix)) return $"{suffix} s since screenshot";
        var quizResult = QuizResult.Match(source);
        if (quizResult.Success)
            return $"{Translate(quizResult.Groups[1].Value)}: {Translate(quizResult.Groups[2].Value)}. Another option: {Translate(quizResult.Groups[3].Value)}.";
        if (source.StartsWith("Actualizado: ", StringComparison.Ordinal)) return "Updated: " + source["Actualizado: ".Length..];
        if (source.StartsWith("Otra opción: ", StringComparison.Ordinal)) return "Another option: " + Translate(source["Otra opción: ".Length..]);
        if (source.StartsWith(" · ", StringComparison.Ordinal)) return source;
        return source;
    }

    private string TranslateSuffix(string suffix) => suffix switch
    {
        "sin datos" => "no data",
        "varios candidatos" => "multiple candidates",
        "sin datos de posición" => "no role data",
        _ => suffix
    };

    private static bool TryPattern(Regex pattern, string value, out string capture)
    {
        var match = pattern.Match(value);
        capture = match.Success ? match.Groups[match.Groups.Count - 1].Value : "";
        return match.Success;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new(propertyName));
}

[MarkupExtensionReturnType(typeof(object))]
public sealed class TranslateExtension(string text) : MarkupExtension
{
    [ConstructorArgument("text")]
    public string Text { get; set; } = text;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(LocalizationManager.Language))
        {
            Source = LocalizationManager.Instance,
            Converter = TranslateConverter.Instance,
            ConverterParameter = Text,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}

[MarkupExtensionReturnType(typeof(object))]
public sealed class LocalizedBindingExtension : MarkupExtension
{
    public LocalizedBindingExtension() { }
    public LocalizedBindingExtension(string path) => Path = path;

    [ConstructorArgument("path")]
    public string Path { get; set; } = "";
    public BindingMode Mode { get; set; } = BindingMode.OneWay;
    public bool UpperCase { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding
        {
            Converter = UpperCase ? TranslateMultiConverter.UpperCaseInstance : TranslateMultiConverter.Instance,
            Mode = Mode
        };
        binding.Bindings.Add(new Binding(Path) { Mode = Mode });
        binding.Bindings.Add(new Binding(nameof(LocalizationManager.Language))
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay
        });
        return binding.ProvideValue(serviceProvider);
    }
}

public sealed class TranslateConverter : IValueConverter
{
    public static TranslateConverter Instance { get; } = new();
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => LocalizationManager.Instance.Translate(parameter as string);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

public sealed class TranslateMultiConverter : IMultiValueConverter
{
    private readonly bool upperCase;

    public TranslateMultiConverter() { }
    private TranslateMultiConverter(bool upperCase) => this.upperCase = upperCase;
    public static TranslateMultiConverter Instance { get; } = new();
    public static TranslateMultiConverter UpperCaseInstance { get; } = new(true);
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var text = values.Length > 0 && values[0] is string value
            ? LocalizationManager.Instance.Translate(value) : values.FirstOrDefault()?.ToString() ?? "";
        return upperCase ? culture.TextInfo.ToUpper(text) : text;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => targetTypes.Select(_ => Binding.DoNothing).ToArray();
}
