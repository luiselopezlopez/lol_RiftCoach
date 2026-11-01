using System.Speech.Synthesis;

namespace LoLCoach.Services;

public interface ISpeechService : IDisposable
{
    string[] Voices { get; }
    string? InitializationError { get; }
    void Speak(string text, string? voice, int volume);
    Task WaitUntilFinishedAsync(CancellationToken cancellationToken);
    void Cancel();
}

public sealed class SpeechService : ISpeechService
{
    private SpeechSynthesizer? synthesizer;
    private string lastText = "";
    private Task speechCompletion = Task.CompletedTask;
    public string[] Voices { get; }
    public string? InitializationError { get; }

    public SpeechService()
    {
        try
        {
            synthesizer = new();
            Voices = synthesizer.GetInstalledVoices().Where(voice => voice.Enabled
                && voice.VoiceInfo.Culture.TwoLetterISOLanguageName == "es").Select(voice => voice.VoiceInfo.Name).ToArray();
            synthesizer.SetOutputToDefaultAudioDevice();
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            synthesizer?.Dispose(); synthesizer = null;
            Voices = [];
            InitializationError = "Síntesis de voz no disponible.";
        }
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