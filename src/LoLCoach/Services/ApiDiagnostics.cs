using System.Diagnostics;
using System.Net.Http;

namespace LoLCoach.Services;

public sealed record ApiDiagnostic(DateTimeOffset Time, string Api, string Method, string Path,
    int? StatusCode, double Milliseconds, string Result)
{
    public string TimeLabel => Time.ToLocalTime().ToString("HH:mm:ss");
    public string StatusLabel => StatusCode?.ToString() ?? "-";
    public string DurationLabel => $"{Milliseconds:0} ms";
}

public sealed class ApiDiagnosticsHandler(string api, Action<ApiDiagnostic> report, HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var time = DateTimeOffset.UtcNow;
        var path = request.RequestUri?.AbsolutePath ?? "/";
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            report(new(time, api, request.Method.Method, path, (int)response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, response.IsSuccessStatusCode ? "Correcto" : "Error HTTP"));
            return response;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            report(new(time, api, request.Method.Method, path, (exception as HttpRequestException)?.StatusCode is { } status ? (int)status : null,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                exception is OperationCanceledException ? "Cancelado / tiempo agotado" : "Conexión no disponible"));
            throw;
        }
    }
}