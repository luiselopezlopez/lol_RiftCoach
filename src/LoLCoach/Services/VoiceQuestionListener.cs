using System.Globalization;
using System.Speech.Recognition;
using System.Text.RegularExpressions;

namespace LoLCoach.Services;

public sealed class VoiceQuestionListener : IDisposable
{
    private static readonly Regex WakeWord = new(@"\A\s*[^\p{L}\p{N}]*lol(?=$|[^\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private readonly string language;
    private SpeechRecognitionEngine? recognizer;
    private bool disposed;

    public bool IsListening { get; private set; }
    public string? ErrorMessage { get; private set; }
    public event Action<string>? QuestionRecognized;
    public event Action<string>? RecognitionFailed;

    public VoiceQuestionListener(string language)
        => this.language = LocalizationManager.NormalizeLanguage(language);

    public bool Start()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsListening) return true;

        ErrorMessage = null;
        try
        {
            var cultureName = language == "en" ? "en-US" : "es-ES";
            recognizer = new SpeechRecognitionEngine(CultureInfo.GetCultureInfo(cultureName));
            recognizer.LoadGrammar(new DictationGrammar());
            recognizer.SpeechRecognized += OnSpeechRecognized;
            recognizer.RecognizeCompleted += OnRecognizeCompleted;
            recognizer.SetInputToDefaultAudioDevice();
            IsListening = true;
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
            or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception
            or PlatformNotSupportedException or NotSupportedException)
        {
            ErrorMessage = "No se pudo iniciar el reconocimiento de voz. Comprueba el idioma y el micrófono.";
            Stop();
            return false;
        }
    }

    public void Stop()
    {
        IsListening = false;
        var current = recognizer;
        recognizer = null;
        if (current is null) return;
        current.SpeechRecognized -= OnSpeechRecognized;
        current.RecognizeCompleted -= OnRecognizeCompleted;
        try { current.RecognizeAsyncCancel(); }
        catch (InvalidOperationException) { }
        current.Dispose();
    }

    private void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs args)
    {
        if (!IsListening || args.Result.Confidence < 0.55) return;
        var question = ExtractQuestion(args.Result.Text);
        if (question is not null) QuestionRecognized?.Invoke(question);
    }

    private void OnRecognizeCompleted(object? sender, RecognizeCompletedEventArgs args)
    {
        if (!IsListening) return;
        IsListening = false;
        ErrorMessage = args.Error is not null || args.InputStreamEnded
            ? "El micrófono interrumpió la escucha. Comprueba la entrada de audio y vuelve a activarla."
            : "El reconocimiento de voz se detuvo. Vuelve a activarlo.";
        RecognitionFailed?.Invoke(ErrorMessage);
    }

    internal static string? ExtractQuestion(string? recognizedText)
    {
        if (string.IsNullOrWhiteSpace(recognizedText)) return null;
        var text = recognizedText.Trim();
        var match = WakeWord.Match(text);
        if (!match.Success) return null;
        var question = text[match.Length..].TrimStart(' ', '\t', ':', ',', '.', ';', '!', '?', '¡', '¿', '-', '—', '"', '\'');
        if (question.Length == 0) return null;
        return question[..Math.Min(question.Length, 500)];
    }

    public void Dispose()
    {
        if (disposed) return;
        Stop();
        disposed = true;
    }
}
