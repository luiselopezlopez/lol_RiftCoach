using System.Speech.Synthesis;

namespace LoLCoach.Services;

public interface ISpeechService : IDisposable
{
    string[] Voices { get; }
    string? InitializationError { get; }
    void SetLanguage(string language) { }
    void Speak(string text, string? voice, int volume);
    Task WaitUntilFinishedAsync(CancellationToken cancellationToken);
    void Cancel();
}

public sealed class SpeechService : ISpeechService
{
    private SpeechSynthesizer? synthesizer;
    private (string Name, string Language)[] voices = [];
    private string language = "es";
    private string lastText = "";
    private Task speechCompletion = Task.CompletedTask;
    public string[] Voices => voices.Where(voice => voice.Language == language).Select(voice => voice.Name).ToArray();
    public string? InitializationError { get; }

    public SpeechService()
    {
        try
        {
            synthesizer = new();
            voices = synthesizer.GetInstalledVoices().Where(voice => voice.Enabled)
                .Select(voice => (voice.VoiceInfo.Name, voice.VoiceInfo.Culture.TwoLetterISOLanguageName)).ToArray();
            synthesizer.SetOutputToDefaultAudioDevice();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            synthesizer?.Dispose(); synthesizer = null;
            voices = [];
            InitializationError = "Síntesis de voz no disponible.";
        }
    }

    public void SetLanguage(string value)
    {
        language = LocalizationManager.NormalizeLanguage(value);
        lastText = "";
    }

    public void Speak(string text, string? voice, int volume)
    {
        if (synthesizer is null || voice is null || !Voices.Contains(voice) || text == lastText) return;
        synthesizer.SpeakAsyncCancelAll();
        synthesizer.SelectVoice(voice);
        synthesizer.Volume = Math.Clamp(volume, 0, 100);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Prompt? prompt = null;
        EventHandler<SpeakCompletedEventArgs>? handler = null;
        handler = (_, args) =>
        {
            if (!ReferenceEquals(args.Prompt, prompt)) return;
            synthesizer.SpeakCompleted -= handler;
            completion.TrySetResult();
        };
        synthesizer.SpeakCompleted += handler;
        try
        {
            prompt = synthesizer.SpeakAsync(text);
            speechCompletion = completion.Task;
            lastText = text;
        }
        catch
        {
            synthesizer.SpeakCompleted -= handler;
            completion.TrySetResult();
            throw;
        }
    }

    public Task WaitUntilFinishedAsync(CancellationToken cancellationToken)
        => speechCompletion.WaitAsync(cancellationToken);

    public void Cancel()
    {
        synthesizer?.SpeakAsyncCancelAll();
        speechCompletion = Task.CompletedTask;
        lastText = "";
    }
    public void Dispose() { Cancel(); synthesizer?.Dispose(); }
}
