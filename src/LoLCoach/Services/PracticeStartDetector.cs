using System.Net;
using System.Net.Http;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed class PracticeStartDetector(IPracticeStateReader reader, TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public async Task<PracticeState> WaitAsync(IProgress<string> progress, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider ?? TimeProvider.System);
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var state = await reader.ReadPracticeAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return state;
            }
            catch (PracticeUnavailableException)
            {
                progress.Report("Esperando una partida con rivales bot verificados");
            }
            catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.ServiceUnavailable || exception.HttpRequestError == HttpRequestError.ConnectionError)
            {
                progress.Report("Esperando el inicio de la partida");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                progress.Report("Esperando respuesta de la API local");
            }
        } while (await timer.WaitForNextTickAsync(cancellationToken));
        throw new OperationCanceledException(cancellationToken);
    }
}