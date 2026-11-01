using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using LoLCoach.Models;
using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class IntegrationBoundaryTests
{
    [Theory]
    [InlineData("http://test.openai.azure.com/openai/v1")]
    [InlineData("https://test.openai.azure.com.evil.example/openai/v1")]
    [InlineData("https://test.openai.azure.com:8443/openai/v1")]
    [InlineData("https://key@test.openai.azure.com/openai/v1")]
    [InlineData("https://test.openai.azure.com/openai/v1?key=x")]
    [InlineData("https://example.com/openai/v1")]
    public void RejectsUnsafeEndpoints(string endpoint)
        => Assert.Throws<ArgumentException>(() => SettingsService.ValidateEndpoint(endpoint));

    [Fact]
    public void NormalizesAzureV1Endpoint()
        => Assert.Equal("https://test.openai.azure.com/openai/v1/",
            SettingsService.ValidateEndpoint("https://test.openai.azure.com/openai/v1").AbsoluteUri);

    [Fact]
    public void EndpointModelAndEncryptedKeyAreStoredInLocalSettings()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        var service = new SettingsService(directory);
        try
        {
            service.Save(new());
            service.SaveKey("test-secret-not-a-real-key");
            Assert.Equal("test-secret-not-a-real-key", service.LoadKey());
            var json = System.IO.File.ReadAllText(service.LocalSettingsPath);
            Assert.DoesNotContain("test-secret", json);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(new AppSettings().Endpoint, document.RootElement.GetProperty("Endpoint").GetString());
            Assert.Equal(new AppSettings().Deployment, document.RootElement.GetProperty("Model").GetString());
            Assert.StartsWith("dpapi:", document.RootElement.GetProperty("ApiKey").GetString());
            service.Save(new AppSettings { Deployment = "another-deployment" });
            Assert.Equal("test-secret-not-a-real-key", service.LoadKey());
            Assert.Equal("another-deployment", service.Load().Deployment);
            service.DeleteKey();
            Assert.Null(service.LoadKey());
            Assert.False(System.IO.File.Exists(System.IO.Path.Combine(directory, "credential.bin")));
        }
        finally { service.Reset(); }
    }

    [Fact]
    public void LegacySettingsAndCredentialMigrateWithoutLosingKeyOrProfile()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(directory);
        var service = new SettingsService(directory);
        try
        {
            var previous = new AppSettings { Deployment = "legacy-model", Profile = new(PlayerRole.Top, "Garen", []) };
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "settings.json"), JsonSerializer.Serialize(previous));
            var encrypted = System.Security.Cryptography.ProtectedData.Protect(Encoding.UTF8.GetBytes("fake-legacy-secret"),
                null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, "credential.bin"), encrypted);
            Assert.Equal("legacy-model", service.Load().Deployment);
            Assert.Equal("Garen", service.Load().Profile!.Champion);
            Assert.Equal("fake-legacy-secret", service.LoadKey());
            Assert.DoesNotContain("fake-legacy-secret", System.IO.File.ReadAllText(service.LocalSettingsPath));
            Assert.False(System.IO.File.Exists(System.IO.Path.Combine(directory, "credential.bin")));
        }
        finally { service.Reset(); }
    }

    [Fact]
    public void ManuallyConfiguredLocalSettingsCanSupplyTheApiKey()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(directory);
        var service = new SettingsService(directory);
        try
        {
            System.IO.File.WriteAllText(service.LocalSettingsPath,
                """{"Endpoint":"https://test.openai.azure.com/openai/v1/","Model":"manual-model","ApiKey":"fake-manual-key"}""");
            Assert.Equal("manual-model", service.Load().Deployment);
            Assert.Equal("fake-manual-key", service.LoadKey());
        }
        finally { service.Reset(); }
    }

    [Fact]
    public void EmptyProfileStringDoesNotPreventLoadingModelOrKey()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        System.IO.Directory.CreateDirectory(directory);
        var service = new SettingsService(directory);
        try
        {
            System.IO.File.WriteAllText(service.LocalSettingsPath,
                """{"Endpoint":"https://test.openai.azure.com/openai/v1/","Model":"configured-model","ApiKey":"fake-test-key","RequestLimit":12,"Profile":""}""");
            Assert.Equal("configured-model", service.Load().Deployment);
            Assert.Equal("fake-test-key", service.LoadKey());
            Assert.Null(service.Load().Profile);
            service.Save(service.Load() with { Profile = new(PlayerRole.Top, "Garen", []) });
            Assert.Equal("Garen", service.Load().Profile!.Champion);
            Assert.Equal("fake-test-key", service.LoadKey());
        }
        finally { service.Reset(); }
    }

    [Fact]
    public void NormalizerOnlyExportsOwnAllowedFields()
    {
        using var active = JsonDocument.Parse("""{"riotId":"private","level":3,"currentGold":500,"championStats":{"currentHealth":450,"maxHealth":900},"enemies":["hidden"]}""");
        using var game = JsonDocument.Parse("""{"gameTime":120,"unexpected":"private"}""");
        var snapshot = GameSnapshot.Parse(active.RootElement, game.RootElement);
        var json = JsonSerializer.Serialize(snapshot);
        Assert.Equal(450, snapshot.Health);
        Assert.DoesNotContain("private", json);
        Assert.DoesNotContain("hidden", json);
        using var empty = JsonDocument.Parse("{}");
        Assert.Null(GameSnapshot.Parse(empty.RootElement, empty.RootElement).Health);
    }

    [Fact]
    public void RosterKeepsLocalChampionStatsWithoutExportingOtherPlayers()
    {
        using var active = JsonDocument.Parse("""{"riotId":"local#private","level":4}""");
        using var game = JsonDocument.Parse("{}");
        using var players = JsonDocument.Parse("""
            [{"riotId":"enemy#private","championName":"Ashe","team":"CHAOS","level":5,"isDead":true,"respawnTimer":12,"scores":{"kills":3,"deaths":2,"assists":1,"creepScore":30,"wardScore":1.5},"items":[{"displayName":"Boots","count":1}]},
             {"riotId":"local#private","championName":"Garen","team":"ORDER","level":4,"position":"TOP","scores":{"kills":2,"deaths":1,"assists":3,"creepScore":45}},
             {"riotId":"ally#private","championName":"Lux","team":"ORDER","level":4}]
            """);
        var snapshot = GameSnapshot.Parse(active.RootElement, game.RootElement, players.RootElement);
        Assert.Equal(new[] { "Tú", "Aliado", "Rival" }, snapshot.Roster.Select(player => player.Relation));
        Assert.True(snapshot.Roster[0].IsOwn);
        Assert.Equal("Garen", snapshot.Roster[0].Champion);
        Assert.Equal(45, snapshot.Roster[0].CreepScore);
        Assert.Null(snapshot.Roster[1].Kills);
        var enemy = snapshot.Roster[2];
        Assert.Equal(5, enemy.Level);
        Assert.Equal(3, enemy.Kills);
        Assert.Equal(1.5, enemy.WardScore);
        Assert.True(enemy.IsDead);
        Assert.Equal(12, enemy.RespawnTimer);
        Assert.Equal("Boots", Assert.Single(enemy.Items).Name);
        var exported = JsonSerializer.Serialize(snapshot);
        Assert.DoesNotContain("Roster", exported);
        Assert.DoesNotContain("Ashe", exported);
        Assert.DoesNotContain("private", exported);
        Assert.Empty(GameSnapshot.Parse(active.RootElement, game.RootElement).Roster);
        using var anonymous = JsonDocument.Parse("{}");
        Assert.All(GameSnapshot.Parse(anonymous.RootElement, game.RootElement, players.RootElement).Roster,
            player => { Assert.False(player.IsOwn); Assert.Equal("Sin equipo", player.Relation); });
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("<script>texto</script>", false)]
    [InlineData("Texto\ncon salto", false)]
    [InlineData("Ataca súbdito de cañón.", true)]
    [InlineData("Huye", true)]
    [InlineData("Ataca Torre", true)]
    [InlineData("Aléjate de Torre", true)]
    [InlineData("Sepárate de Torre", true)]
    [InlineData("Separate de torre", true)]
    [InlineData("Ataca Garen", true)]
    [InlineData("Ataca Garen con E+Q", true)]
    [InlineData("Ataca Garen con 3+Q+R", true)]
    [InlineData("Ataca Maestro Yi con Q", true)]
    [InlineData("Ataca Cho'Gath con E+Q", true)]
    [InlineData("Ataca Garen con clic derecho", true)]
    [InlineData("Ataca Garen con X+Q", false)]
    [InlineData("Ataca Garen con 8+Q", false)]
    [InlineData("Ataca Garen con Q+", false)]
    [InlineData("Ataca Garen con +Q", false)]
    [InlineData("Ataca Garen con Q++E", false)]
    [InlineData("Ataca Garen con E+Q\nCompra Botas", false)]
    [InlineData("Huye de Garen", true)]
    [InlineData("Huye de Cho'Gath", true)]
    [InlineData("Ataca Cañón", true)]
    [InlineData("Huye de", false)]
    [InlineData("Aléjate de", false)]
    [InlineData("Huye de <campeón>", false)]
    [InlineData("Aléjate de Torre\nCompra Botas", false)]
    [InlineData("Compra Hoja de Doran", true)]
    [InlineData("Ve a la torre superior", true)]
    [InlineData("Avanza", true)]
    [InlineData("Retrocede", true)]
    [InlineData("Espera", true)]
    [InlineData("Usa Q+W+E", true)]
    [InlineData("Mejora Q", true)]
    [InlineData("Mejora R", true)]
    [InlineData("Mejora D", false)]
    [InlineData("Farmea", false)]
    [InlineData("Apoya Top", true)]
    [InlineData("Apoya Mid", true)]
    [InlineData("Apoya Bot", true)]
    [InlineData("Apoya", false)]
    [InlineData("Apoya <línea>", false)]
    [InlineData("Apoya Bot y compra Botas", false)]
    [InlineData("Ve a Dragón", true)]
    [InlineData("Ataca Dragón", true)]
    [InlineData("Ve a Heraldo", true)]
    [InlineData("Ve a Azul", true)]
    [InlineData("Ataca Gromp", true)]
    [InlineData("Usa 3+Q", true)]
    [InlineData("Usa 7", true)]
    [InlineData("Usa 0", false)]
    [InlineData("Usa 8", false)]
    [InlineData("Usa A", false)]
    [InlineData("Pulsa B", true)]
    [InlineData("Escóndete", true)]
    [InlineData("Huye y compra un objeto", false)]
    [InlineData("No hay evidencia suficiente.", false)]
    [InlineData("Ataca <objetivo>", false)]
    public void AdviceMustBeBriefPlainText(string text, bool expected)
        => Assert.Equal(expected, AdviceValidator.IsValid(text));

    [Fact]
    public async Task StreamsImageAndStateThroughAzureV1Contract()
    {
        var handler = new StreamingHandler();
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler));
        var observation = TestObservations.Create(new(120, 3, 400, 900, 500), jpeg: [1, 2, 3]);
        observation = observation with { State = observation.State with
        {
            Roster = [new("Ashe", "Rival", false, 5, "BOTTOM", 2, 1, 3, 30, 0, false, 0, [new("Botas", 1)])]
        } };
        var parts = new List<string>();
        await foreach (var part in service.StreamAsync(observation, new(PlayerRole.Top, "Garen", []), [], default)) parts.Add(part);
        Assert.Equal("Avanza.", string.Concat(parts));
        Assert.Contains("data:image/jpeg;base64,AQID", handler.Body);
        Assert.Contains("\"stream\":true", handler.Body);
        using var requestBody = JsonDocument.Parse(handler.Body);
        var systemMessage = requestBody.RootElement.GetProperty("messages")[0];
        Assert.Equal("system", systemMessage.GetProperty("role").GetString());
        var systemPrompt = systemMessage.GetProperty("content").GetString()!;
        Assert.StartsWith("Eres un coach de League of Legends para un jugador principiante.", systemPrompt);
        Assert.Contains("# PASO 1: OBSERVA", systemPrompt);
        Assert.Contains("# PASO 2: DECIDE LA ORDEN (aplica la primera regla que se cumpla)", systemPrompt);
        Assert.Contains("Una línea, máximo 6 palabras, sin explicación.", systemPrompt);
        Assert.Contains("Mejora <Q/W/E/R>", systemPrompt);
        Assert.Contains("Nunca recomiendes comprar si la tienda no está abierta.", systemPrompt);
        Assert.Contains("# MODO REAPARICIÓN (muerto o en base)", systemPrompt);
        Assert.Contains("# CONSEJO TÁCTICO (fuera de base)", systemPrompt);
        Assert.Contains("# HABILIDADES Y OBJETOS ACTIVOS", systemPrompt);
        Assert.Contains("# MINIMAPA", systemPrompt);
        Assert.Contains("No cambies BUILD_OBJETIVO ni ORDEN_HABILIDADES.", systemPrompt);
        Assert.Contains("# SEGURIDAD", systemPrompt);
        Assert.Contains("# CATÁLOGO DE PROGRESIÓN", systemPrompt);
        Assert.Contains("Ataca Torre, Aléjate de Torre, Ataca Garen con E+Q, Huye de Garen", systemPrompt);
        Assert.Contains("Todo ataque a un campeón debe indicar el combo", systemPrompt);
        Assert.Contains("No confundas ORDEN_HABILIDADES", systemPrompt);
        Assert.Contains("súbditos aliados recibiendo sus ataques", systemPrompt);
        Assert.Contains("No ataques a un campeón protegido por esa torre.", systemPrompt);
        Assert.Contains("Evalúa el minimapa en CADA turno", systemPrompt);
        Assert.Contains("Apoya Bot, Apoya Mid, Ve a Dragón", systemPrompt);
        Assert.Contains("Nunca respondas Farmea", systemPrompt);
        Assert.Contains("El tiempo de partida por sí solo no prueba", systemPrompt);
        Assert.Contains("Un icono ambiguo o una zona en niebla no confirma disponibilidad.", systemPrompt);
        Assert.Contains("# IDENTIFICAR TORRES", systemPrompt);
        Assert.Contains("torre aliada tiene barra de vida azul y una torre enemiga barra de vida roja", systemPrompt);
        Assert.Contains("Nunca ordenes atacar una torre aliada.", systemPrompt);
        Assert.DoesNotContain("evita repetirlas literalmente", systemPrompt);
        Assert.Equal(2048, requestBody.RootElement.GetProperty("max_completion_tokens").GetInt32());
        var image = requestBody.RootElement.GetProperty("messages")[1].GetProperty("content")[1].GetProperty("image_url");
        Assert.Equal("high", image.GetProperty("detail").GetString());
        Assert.Contains("/openai/v1/chat/completions", handler.Url);
        using var context = JsonDocument.Parse(requestBody.RootElement.GetProperty("messages")[1].GetProperty("content")[0].GetProperty("text").GetString()!);
        Assert.Equal("Grebas de berserker", context.RootElement.GetProperty("BUILD_OBJETIVO")[0].GetProperty("Nombre").GetString());
        Assert.Equal("E", context.RootElement.GetProperty("ORDEN_HABILIDADES")[1].GetString());
        Assert.Equal("early", context.RootElement.GetProperty("fase_partida").GetProperty("Phase").GetString());
        Assert.Equal("Ashe", context.RootElement.GetProperty("campeones_enemigos")[0].GetProperty("Champion").GetString());
        Assert.Equal(5, context.RootElement.GetProperty("campeones_enemigos")[0].GetProperty("Level").GetInt32());
    }

    [Fact]
    public async Task InitialGuideUsesChampionAbilitiesAndEnemyContextWithoutPlayerIdentities()
    {
        var handler = new GuideHandler();
        var abilityCalls = 0;
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler),
            abilityReferenceFactory: (champion, patch, _) =>
            {
                abilityCalls++; Assert.Equal("Garen", champion); Assert.Equal("16.21.1", patch);
                return Task.FromResult(new ChampionAbilityReference(champion, patch,
                    [new("Pasiva", "Perseverancia", "Regenera vida"), new("Q", "Golpe decisivo", "Acelera y silencia"),
                     new("W", "Coraje", "Reduce daño"), new("E", "Juicio", "Daño giratorio"), new("R", "Justicia", "Remata") ]));
            });
        var snapshot = new GameSnapshot(120, 3, 500, 800, 600)
        {
            Roster = [new("Ashe", "Rival", false, 4, "BOTTOM", 1, 0, 0, 30, 0, false, 0, [new("Botas", 1)])]
        };
        var guide = await service.CreateGuideAsync(new(snapshot, TestObservations.Practice, 0), new(PlayerRole.Top, "Ashe", []), default);
        Assert.Equal("Garen: Q para acercarte. W para resistir.", guide);
        Assert.Equal(1, abilityCalls);
        using var request = JsonDocument.Parse(handler.Body);
        var messages = request.RootElement.GetProperty("messages");
        Assert.Contains("Máximo 180 palabras", messages[0].GetProperty("content").GetString());
        using var data = JsonDocument.Parse(messages[1].GetProperty("content").GetString()!);
        Assert.Equal("Garen", data.RootElement.GetProperty("champion").GetString());
        Assert.Equal(5, data.RootElement.GetProperty("ficha_habilidades").GetProperty("Abilities").GetArrayLength());
        Assert.Equal("Ashe", data.RootElement.GetProperty("campeones_enemigos")[0].GetProperty("Champion").GetString());
        Assert.DoesNotContain("riotId", handler.Body);
    }

    [Fact]
    public async Task AbilityReferenceLoadsDataDragonKeysAndUsesTheLocalCache()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "LoLCoach-abilities-" + Guid.NewGuid());
        var handler = new GuideHandler("""{"data":{"Garen":{"passive":{"name":"Perseverancia","description":"Regenera vida"},"spells":[{"name":"Golpe decisivo","description":"Silencia"},{"name":"Coraje","description":"Reduce daño"},{"name":"Juicio","description":"Gira"},{"name":"Justicia","description":"Remata"}]}}}""");
        try
        {
            using var catalog = new DataDragonService(new HttpClient(handler), directory);
            var first = await catalog.GetAbilityReferenceAsync("Garen", "16.21.1", default);
            Assert.Equal(new[] { "Pasiva", "Q", "W", "E", "R" }, first.Abilities.Select(ability => ability.Key));
            Assert.Equal("Golpe decisivo", first.Abilities[1].Name);
            var cached = await catalog.GetAbilityReferenceAsync("Garen", "16.21.1", default);
            Assert.Equal(first.Abilities, cached.Abilities);
            Assert.Equal(1, handler.Calls);
            Assert.Equal("/cdn/16.21.1/data/es_ES/champion/Garen.json", handler.Path);
            await Assert.ThrowsAsync<ArgumentException>(() => catalog.GetAbilityReferenceAsync("../private", "16.21.1", default));
        }
        finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData(170, true)]
    [InlineData(400, false)]
    public async Task InitialGuideToleratesSlightlyLongAnswersButRejectsRunawayOutput(int words, bool accepted)
    {
        var content = string.Join(' ', Enumerable.Repeat("palabra", words));
        var handler = new GuideHandler($$"""{"id":"guide-test","object":"chat.completion","created":1,"model":"test","choices":[{"index":0,"message":{"role":"assistant","content":"{{content}}"},"finish_reason":"stop"}]}""");
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler),
            abilityReferenceFactory: (champion, patch, _) => Task.FromResult(new ChampionAbilityReference(champion, patch, [])));
        var state = new PracticeState(new GameSnapshot(120, 3, 500, 800, 600), TestObservations.Practice, 0);
        var create = service.CreateGuideAsync(state, new(PlayerRole.Top, "Garen", []), default);
        if (accepted) Assert.Equal(content, await create);
        else await Assert.ThrowsAsync<ModelOutputException>(() => create);
    }

    [Theory]
    [InlineData("ORDER", "azul", "abajo a la izquierda")]
    [InlineData("CHAOS", "rojo", "arriba a la derecha")]
    public void MapSideComesFromTheLocalPlayerTeam(string team, string color, string ownBase)
    {
        using var active = JsonDocument.Parse("""{"riotId":"local#private"}""");
        using var game = JsonDocument.Parse("{}");
        using var players = JsonDocument.Parse($$"""[{"riotId":"local#private","team":"{{team}}"}]""");
        var snapshot = GameSnapshot.Parse(active.RootElement, game.RootElement, players.RootElement);
        var side = Assert.IsType<MapSide>(MatchTacticalContext.Side(snapshot));
        Assert.Equal(color, side.Equipo);
        Assert.Equal(ownBase, side.BasePropia);
        Assert.DoesNotContain(team, JsonSerializer.Serialize(snapshot));
        Assert.Null(MatchTacticalContext.Side(GameSnapshot.Parse(active.RootElement, game.RootElement)));
    }

    private sealed class GuideHandler(string? responseBody = null) : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string Path { get; private set; } = "";
        public int Calls { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(responseBody ?? """{"id":"guide-test","object":"chat.completion","created":1,"model":"test","choices":[{"index":0,"message":{"role":"assistant","content":"Garen: Q para acercarte. W para resistir."},"finish_reason":"stop"}]}""", Encoding.UTF8, "application/json") };
        }
    }

    [Theory]
    [InlineData(120, 3, "early")]
    [InlineData(839, 8, "early")]
    [InlineData(840, 8, "mid")]
    [InlineData(600, 9, "mid")]
    [InlineData(1680, 14, "late")]
    [InlineData(1680, 10, "mid")]
    [InlineData(2100, 8, "late")]
    public void PhaseUsesTimeAndChampionLevel(double seconds, int level, string expected)
    {
        var phase = MatchTacticalContext.EstimatePhase(new(seconds, level, 500, 800, 600));
        Assert.Equal(expected, phase.Phase);
        Assert.True(phase.IsEstimate);
        Assert.Equal(seconds / 60, phase.Minutes);
        Assert.Equal(level, phase.Level);
    }

    [Fact]
    public void TacticalContextKeepsOnlyEnemyChampionStatsAndHandlesMissingData()
    {
        var snapshot = new GameSnapshot(null, null, null, null, null)
        {
            Roster = [new("Garen", "Tú", true, 3, "TOP", 0, 0, 0, 20, 0, false, 0, []),
                new("Lux", "Aliado", false, 3, "MIDDLE", 0, 0, 0, 20, 0, false, 0, []),
                new("Ashe", "Rival", false, 5, "BOTTOM", 2, 1, 3, 30, 0, false, 0, [new("Botas", 1)])]
        };
        Assert.Equal("desconocida", MatchTacticalContext.EstimatePhase(snapshot).Phase);
        var enemy = Assert.Single(MatchTacticalContext.Enemies(snapshot));
        Assert.Equal("Ashe", enemy.Champion);
        Assert.Equal(5, enemy.Level);
        Assert.Equal("Botas", Assert.Single(enemy.Items));
        var json = JsonSerializer.Serialize(enemy);
        Assert.DoesNotContain("Position", json);
        Assert.DoesNotContain("riotId", json);
        Assert.DoesNotContain("Lux", json);
    }

    [Theory]
    [InlineData(1, 0, 0, 0, 0, "E")]
    [InlineData(3, 1, 0, 1, 0, "W")]
    [InlineData(4, 1, 1, 1, 0, "E")]
    [InlineData(6, 2, 1, 2, 0, "R")]
    [InlineData(7, 2, 1, 2, 1, "E")]
    [InlineData(11, 3, 1, 5, 1, "R")]
    [InlineData(16, 5, 3, 5, 2, "R")]
    [InlineData(18, 5, 5, 5, 3, null)]
    public void CatalogRecommendsOnlyLegalGarenSkillUpgrades(int level, int rankQ, int rankW, int rankE, int rankR, string? expected)
    {
        var snapshot = new GameSnapshot(120, level, 400, 900, 500)
        {
            Player = new PlayerGameDetails { AbilityLevels = new() { ["Q"] = rankQ, ["W"] = rankW, ["E"] = rankE, ["R"] = rankR } }
        };
        var plan = ChampionProgressionCatalog.Default.GetPlan("Garen", PlayerRole.Top, snapshot)!;
        Assert.Equal(expected, plan.SiguienteHabilidad);
        Assert.Equal(level - rankQ - rankW - rankE - rankR, plan.PuntosHabilidad);
    }

    [Fact]
    public void CatalogSelectsChampionRoleAndMapsOwnedActiveItemsWithoutInventingData()
    {
        var catalog = ChampionProgressionCatalog.Default;
        Assert.Equal(173, catalog.Champions.Length);
        Assert.Contains(catalog.Champions, champion => champion.Id == "Akali");
        using var active = JsonDocument.Parse("""{"riotId":"local","level":3,"abilities":{"Q":{"abilityLevel":1},"W":{"abilityLevel":0},"E":{"abilityLevel":1},"R":{"abilityLevel":0}}}""");
        using var players = JsonDocument.Parse("""[{"riotId":"local","position":"TOP","items":[{"displayName":"Owned boots","itemID":3006,"slot":0,"count":1},{"displayName":"Health potion","itemID":2003,"slot":3,"count":2},{"displayName":"Ward","itemID":3340,"slot":6,"count":1}]}]""");
        using var game = JsonDocument.Parse("{}");
        var snapshot = GameSnapshot.Parse(active.RootElement, game.RootElement, players.RootElement);
        Assert.Equal(3006, snapshot.Player!.Items[0].Id);
        Assert.Equal(3, snapshot.Player.Items[1].Slot);
        var plan = catalog.GetPlan("Garen", PlayerRole.Support, snapshot)!;
        Assert.Equal("top", plan.Role);
        Assert.Equal("16.21.1", plan.Version);
        Assert.Equal(6, plan.BUILD_OBJETIVO.Length);
        Assert.DoesNotContain(plan.ObjetosPendientes, item => item.Id == 3006);
        Assert.Equal(6631, plan.ObjetosPendientes[0].Id);
        Assert.Equal("5", plan.ObjetosActivos[0].Tecla);
        Assert.Equal("4", plan.ObjetosActivos[1].Tecla);
        Assert.Equal("curacion", plan.ObjetosActivos[0].Tipo);
        Assert.Equal("W", plan.SiguienteHabilidad);
        var empty = new GameSnapshot(null, null, null, null, null);
        Assert.Null(catalog.GetPlan("UnknownChampion", PlayerRole.Top, empty));
        Assert.Null(catalog.GetPlan("Garen", PlayerRole.Top, empty)!.PuntosHabilidad);
        Assert.Null(catalog.GetPlan("Garen", PlayerRole.Top, empty)!.SiguienteHabilidad);
        Assert.Equal("support", catalog.GetPlan("Lux", PlayerRole.Support, empty)!.Role);
        Assert.Equal("mid", catalog.GetPlan("Lux", PlayerRole.Mid, empty)!.Role);
        Assert.Equal("top", catalog.GetPlan("Garen", PlayerRole.Support, empty)!.Role);
        Assert.Equal("MasterYi", catalog.GetPlan("Maestro Yi", PlayerRole.Jungle, empty)!.Champion);
        var shiftedPosition = snapshot with { Player = snapshot.Player with { Position = "MIDDLE" } };
        Assert.Equal("support", catalog.GetPlan("Lux", PlayerRole.Mid, shiftedPosition, "support")!.Role);
        var special = catalog.GetPlan("Aphelios", PlayerRole.Adc, snapshot)!;
        Assert.False(special.ProgresionEstandar);
        Assert.Null(special.PuntosHabilidad);
        Assert.Null(special.SiguienteHabilidad);
    }

    [Fact]
    public async Task LiveCoachingIsBlockedBeforeNetworkRequest()
    {
        var handler = new StreamingHandler();
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var part in service.StreamAsync(new([], new(null, null, null, null, null), 0,
                ObservationKind.LiveGame), new(PlayerRole.Top, "Garen", []), [], default)) { }
        });
        Assert.Empty(handler.Body);
    }

    [Fact]
    public async Task VerifiedBotPracticeStreamsOwnStateAndImageWithoutPlayerIdentities()
    {
        using var active = JsonDocument.Parse("""{"riotId":"private#local","summonerName":"Private User","level":4,"currentGold":500,"championStats":{"currentHealth":450,"maxHealth":900,"resourceType":"MANA","resourceValue":200,"resourceMax":300,"attackDamage":75,"abilityPower":20,"armor":40,"magicResist":30,"attackSpeed":0.65,"abilityHaste":10,"moveSpeed":350},"abilities":{"Q":{"abilityLevel":2,"displayName":"Q spell","id":"private-ability-id"}},"fullRunes":{"keystone":{"displayName":"Electrocute"},"primaryRuneTree":{"displayName":"Precision"},"secondaryRuneTree":{"displayName":"Domination"},"generalRunes":[{"displayName":"Press the Attack"}]}}""");
        using var players = JsonDocument.Parse("""[{"riotId":"private#local","summonerName":"Private User","team":"ORDER","isBot":false,"championName":"Garen","position":"TOP","scores":{"kills":2,"deaths":1,"assists":3,"creepScore":45,"wardScore":1.5},"items":[{"displayName":"Doran's Blade","count":1,"itemID":1055}],"summonerSpells":{"summonerSpellOne":{"displayName":"Flash"},"summonerSpellTwo":{"displayName":"Teleport"}}},{"riotId":"private#bot","summonerName":"Hidden Bot Name","team":"CHAOS","isBot":true}]""");
        var verification = BotMatchVerifier.Verify(active.RootElement, players.RootElement);
        using var game = JsonDocument.Parse("""{"gameMode":"CLASSIC","mapName":"Map11","gameTime":120}""");
        var snapshot = GameSnapshot.Parse(active.RootElement, game.RootElement, players.RootElement);
        var handler = new StreamingHandler();
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler));
        var observation = new Observation([1, 2, 3], snapshot, 0, ObservationKind.BotPractice)
        { Practice = verification };
        var previousAdvice = Enumerable.Range(1, 32).Select(index => $"Ataca objetivo {index}.").ToArray();
        await foreach (var part in service.StreamAsync(observation, new(PlayerRole.Adc, "Ashe", []), previousAdvice, default)) { }
        using var request = JsonDocument.Parse(handler.Body);
        var messages = request.RootElement.GetProperty("messages");
        Assert.Equal(32, messages.GetArrayLength());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());
        Assert.Equal("Ataca objetivo 3.", messages[1].GetProperty("content").GetString());
        Assert.Equal("Ataca objetivo 32.", messages[30].GetProperty("content").GetString());
        var content = messages[31].GetProperty("content");
        using var context = JsonDocument.Parse(content[0].GetProperty("text").GetString()!);
        Assert.Equal("Garen", context.RootElement.GetProperty("champion").GetString());
        Assert.Equal("BotPractice", context.RootElement.GetProperty("source").GetString());
        var state = context.RootElement.GetProperty("state");
        Assert.Equal("CLASSIC", state.GetProperty("GameMode").GetString());
        Assert.Equal("Map11", state.GetProperty("MapName").GetString());
        var player = state.GetProperty("Player");
        Assert.Equal("TOP", player.GetProperty("Position").GetString());
        Assert.Equal(45, player.GetProperty("CreepScore").GetInt32());
        Assert.Equal(2, player.GetProperty("AbilityLevels").GetProperty("Q").GetInt32());
        Assert.Contains("Electrocute", player.GetProperty("Runes").EnumerateArray().Select(rune => rune.GetString()));
        Assert.Equal("Flash", player.GetProperty("SummonerSpells")[0].GetString());
        Assert.Equal("Doran's Blade", player.GetProperty("Items")[0].GetProperty("Name").GetString());
        Assert.DoesNotContain("private#", handler.Body);
        Assert.DoesNotContain("Private User", handler.Body);
        Assert.DoesNotContain("Hidden Bot Name", handler.Body);
        Assert.DoesNotContain("private-ability-id", handler.Body);
        Assert.DoesNotContain("riotId", handler.Body);
        Assert.Contains("data:image/jpeg;base64,AQID", handler.Body);
    }

    [Fact]
    public async Task UnverifiedBotClaimCannotReachAzureTransport()
    {
        var handler = new StreamingHandler();
        using var service = new FoundryCoachingService(new(), "fake-key", new HttpClient(handler));
        await Assert.ThrowsAsync<PracticeUnavailableException>(async () =>
        {
            await foreach (var part in service.StreamAsync(new([], new(null, null, null, null, null), 0,
                ObservationKind.BotPractice), new(PlayerRole.Top, "Garen", []), [], default)) { }
        });
        Assert.Empty(handler.Body);
    }

    private sealed class StreamingHandler : HttpMessageHandler
    {
        public string Body { get; private set; } = "";
        public string Url { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Url = request.RequestUri!.AbsoluteUri;
            var stream = """
                data: {"id":"test","object":"chat.completion.chunk","created":1,"model":"test","choices":[{"index":0,"delta":{"content":"Avanza"},"finish_reason":null}]}

                data: {"id":"test","object":"chat.completion.chunk","created":1,"model":"test","choices":[{"index":0,"delta":{"content":"."},"finish_reason":"stop"}]}

                data: [DONE]


                """;
            return new(HttpStatusCode.OK) { Content = new StringContent(stream, Encoding.UTF8, "text/event-stream") };
        }
    }
}