using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed record PracticeState(GameSnapshot Snapshot, VerifiedBotPractice Verification, long ReadAt);

public interface IPracticeStateReader
{
    Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken);
}

public sealed class LiveClientDataService : IPracticeStateReader, IDisposable
{
    private readonly HttpClient client;
    private readonly X509Certificate2 root;
    private readonly bool allowUnverifiedBotOpponentsForDebugging;

    private LiveClientDataService(X509Certificate2 root, Action<ApiDiagnostic>? diagnostics,
        bool allowUnverifiedBotOpponentsForDebugging)
    {
        this.root = root;
        this.allowUnverifiedBotOpponentsForDebugging = allowUnverifiedBotOpponentsForDebugging;
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ServerCertificateCustomValidationCallback = (request, certificate, _, errors) =>
        {
            if (request.RequestUri is not { Host: "127.0.0.1", Port: 2999, Scheme: "https" } || certificate is null
                || (errors & (SslPolicyErrors.RemoteCertificateNameMismatch | SslPolicyErrors.RemoteCertificateNotAvailable)) != 0)
                return false;
            using var chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.Add(root);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return chain.Build(certificate);
        };
        client = new HttpClient(diagnostics is null ? handler : new ApiDiagnosticsHandler("LoL", diagnostics, handler))
        { BaseAddress = new Uri("https://127.0.0.1:2999/liveclientdata/"), Timeout = TimeSpan.FromSeconds(2) };
    }

    public static async Task<LiveClientDataService> CreateAsync(CancellationToken cancellationToken,
        Action<ApiDiagnostic>? diagnostics = null, bool allowUnverifiedBotOpponentsForDebugging = false)
    {
        using var download = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var pem = await download.GetStringAsync("https://static.developer.riotgames.com/docs/lol/riotgames.pem", cancellationToken);
        return new(X509Certificate2.CreateFromPem(pem), diagnostics, allowUnverifiedBotOpponentsForDebugging);
    }

    public async Task<GameSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var activeTask = client.GetStringAsync("activeplayer", cancellationToken);
        var statsTask = client.GetStringAsync("gamestats", cancellationToken);
        await Task.WhenAll(activeTask, statsTask);
        using var active = JsonDocument.Parse(await activeTask);
        using var stats = JsonDocument.Parse(await statsTask);
        return GameSnapshot.Parse(active.RootElement, stats.RootElement);
    }

    public async Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
    {
        var started = TimeProvider.System.GetTimestamp();
        var activeTask = client.GetStringAsync("activeplayer", cancellationToken);
        var statsTask = client.GetStringAsync("gamestats", cancellationToken);
        var playersTask = client.GetStringAsync("playerlist", cancellationToken);
        await Task.WhenAll(activeTask, statsTask, playersTask);
        using var active = JsonDocument.Parse(await activeTask);
        using var stats = JsonDocument.Parse(await statsTask);
        using var players = JsonDocument.Parse(await playersTask);
        var verification = BotMatchVerifier.Verify(active.RootElement, players.RootElement,
            allowUnverifiedBotOpponentsForDebugging);
        var snapshot = GameSnapshot.Parse(active.RootElement, stats.RootElement, players.RootElement);
        if (snapshot.GameTime is not >= 0 || snapshot.Health is null || snapshot.MaxHealth is not > 0)
            throw new PracticeUnavailableException("Estado propio incompleto; no se enviará esta observación.");
        return new(snapshot, verification, started);
    }

    public void Dispose() { client.Dispose(); root.Dispose(); }
}