using System.Runtime.InteropServices;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text;
using System.Windows.Automation;
using System.Windows.Interop;
using Vernier.Core.Agents;
using Vernier.Core.Geometry;

namespace Vernier.App.Platform;

/// <summary>
/// Sends approved input with SendInput. It refuses when the target window is not in front, and
/// never types into a password field. It cannot reach elevated windows (Vernier runs unelevated).
/// </summary>
public sealed class SendInputDriver(nint targetWindow) : IInputDriver
{
    private static readonly Dictionary<string, ushort> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = 0x11, ["shift"] = 0x10, ["alt"] = 0x12, ["enter"] = 0x0D, ["return"] = 0x0D, ["tab"] = 0x09, ["esc"] = 0x1B,
        ["space"] = 0x20, ["backspace"] = 0x08, ["delete"] = 0x2E, ["del"] = 0x2E, ["home"] = 0x24, ["end"] = 0x23,
        ["pageup"] = 0x21, ["pagedown"] = 0x22, ["left"] = 0x25, ["up"] = 0x26, ["right"] = 0x27, ["down"] = 0x28, ["insert"] = 0x2D,
        ["numpad0"] = 0x60, ["numpad1"] = 0x61, ["numpad2"] = 0x62, ["numpad3"] = 0x63, ["numpad4"] = 0x64,
        ["numpad5"] = 0x65, ["numpad6"] = 0x66, ["numpad7"] = 0x67, ["numpad8"] = 0x68, ["numpad9"] = 0x69,
        ["."] = 0xBE, [","] = 0xBC, ["-"] = 0xBD, ["="] = 0xBB, ["/"] = 0xBF, ["["] = 0xDB, ["]"] = 0xDD, [";"] = 0xBA, ["'"] = 0xDE, ["`"] = 0xC0,
    };

    public async Task ExecuteAsync(ComputerAction action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (action.Type == ActionType.Wait)
        {
            await Task.Delay(action.Amount, ct);
            return;
        }
        if (!Win32.IsWindow(targetWindow)) throw new InvalidOperationException("The target window was closed.");
        if (Win32.GetAncestor(Win32.GetForegroundWindow(), Win32.GA_ROOT) != targetWindow)
            throw new InvalidOperationException("Stopped: the target window is no longer in front, so input would have gone somewhere else.");

        switch (action.Type)
        {
            case ActionType.Move: Mouse(action.Point!.Value); break;
            case ActionType.Click: Mouse(action.Point!.Value, Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP); break;
            case ActionType.RightClick: Mouse(action.Point!.Value, Win32.MOUSEEVENTF_RIGHTDOWN, Win32.MOUSEEVENTF_RIGHTUP); break;
            case ActionType.DoubleClick:
                Mouse(action.Point!.Value, Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP);
                await Task.Delay(60, ct);
                Mouse(action.Point!.Value, Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP);
                break;
            case ActionType.Scroll:
                Send(new Win32.INPUT { type = 0, u = { mi = new Win32.MOUSEINPUT { dwFlags = Win32.MOUSEEVENTF_WHEEL, mouseData = unchecked((uint)(action.Amount * 120)) } } });
                break;
            case ActionType.Type:
                if (FocusIsPassword()) throw new InvalidOperationException("Stopped: the focused field is a password field (or could not be checked). Vernier never types into those.");
                if (FocusIsTerminal()) throw new InvalidOperationException("Stopped: the focus is in a terminal pane, where typed text runs as a command.");
                foreach (var c in action.Text ?? "")
                {
                    Send(Key(0, c, Win32.KEYEVENTF_UNICODE), Key(0, c, Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP));
                    await Task.Delay(8, ct);
                }
                break;
            case ActionType.Keys:
                if (FocusIsTerminal()) throw new InvalidOperationException("Stopped: the focus is in a terminal pane, where keys run commands.");
                var codes = ParseChord(action.Keys ?? "");
                Send([.. codes.Select(k => Key(k, '\0', 0)), .. codes.Reverse().Select(k => Key(k, '\0', Win32.KEYEVENTF_KEYUP))]);
                break;
        }
        await Task.Delay(180, CancellationToken.None); // the input is sent; let the application react before the next step
    }

    public static ushort[] ParseChord(string chord)
    {
        var result = new List<ushort>();
        foreach (var part in ActionPolicy.Normalize(chord).Split('+', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Keys.TryGetValue(part, out var vk)) result.Add(vk);
            else if (part.Length == 1 && char.IsLetterOrDigit(part[0])) result.Add(char.ToUpperInvariant(part[0]));
            else if (part.Length is 2 or 3 && part[0] == 'f' && int.TryParse(part[1..], out var n) && n is >= 1 and <= 12) result.Add((ushort)(0x70 + n - 1));
            else throw new InvalidOperationException($"Unknown key \"{part}\" in \"{chord}\".");
        }
        if (result.Count == 0) throw new InvalidOperationException("No keys to press.");
        return [.. result];
    }

    /// <summary>Physical pixel to the 0..65535 absolute coordinate SendInput uses across the virtual desktop.</summary>
    public static (int X, int Y) ToAbsolute(PxPoint physical, PxRect desktop) => (
        (int)Math.Round((physical.X - desktop.X) * 65535.0 / Math.Max(1, desktop.Width - 1)),
        (int)Math.Round((physical.Y - desktop.Y) * 65535.0 / Math.Max(1, desktop.Height - 1)));

    private static void Mouse(PxPoint p, params uint[] buttonFlags)
    {
        var (x, y) = ToAbsolute(p, Win32.VirtualDesktop());
        const uint move = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK;
        var inputs = new List<Win32.INPUT> { new() { type = 0, u = { mi = new Win32.MOUSEINPUT { dx = x, dy = y, dwFlags = move } } } };
        foreach (var f in buttonFlags)
            inputs.Add(new Win32.INPUT { type = 0, u = { mi = new Win32.MOUSEINPUT { dx = x, dy = y, dwFlags = f | Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK } } });
        Send([.. inputs]);
    }

    private static Win32.INPUT Key(ushort vk, char unicode, uint flags) =>
        new() { type = 1, u = { ki = new Win32.KEYBDINPUT { wVk = vk, wScan = unicode, dwFlags = flags } } };

    private static void Send(params Win32.INPUT[] inputs)
    {
        if (Win32.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Win32.INPUT>()) != inputs.Length)
            throw new InvalidOperationException("Windows blocked the input (the target may be running as administrator).");
    }

    // Fails closed: when the focused field cannot be inspected, it is treated as a password field and nothing is typed.
    private static bool FocusIsPassword()
    {
        try { return AutomationElement.FocusedElement is not { } focus || focus.Current.IsPassword; }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or COMException) { return true; }
    }

    // A terminal pane inside an editor (VS Code, Visual Studio, JetBrains) is a terminal: keys typed there run commands.
    private static bool FocusIsTerminal()
    {
        try
        {
            if (AutomationElement.FocusedElement is not { } focus) return true;
            var c = focus.Current;
            return c.ClassName.Contains("xterm", StringComparison.OrdinalIgnoreCase) || c.ClassName.Contains("terminal", StringComparison.OrdinalIgnoreCase)
                || c.AutomationId.Contains("terminal", StringComparison.OrdinalIgnoreCase) || c.Name.StartsWith("Terminal", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception e) when (e is ElementNotAvailableException or InvalidOperationException or COMException) { return true; }
    }
}

/// <summary>Global hotkeys via RegisterHotKey on one window. A hotkey another app already owns is reported, not stolen.</summary>
public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = [];
    private int _nextId = 0x5600;

    public List<string> Unavailable { get; } = [];

    public HotkeyManager(nint hwnd)
    {
        _source = HwndSource.FromHwnd(hwnd) ?? throw new InvalidOperationException("Window has no message source.");
        _source.AddHook(Hook);
    }

    public int Register(string label, uint modifiers, uint vk, Action handler)
    {
        var id = _nextId++;
        if (!Win32.RegisterHotKey(_source.Handle, id, modifiers | Win32.MOD_NOREPEAT, vk))
        {
            Unavailable.Add(label);
            return -1;
        }
        _handlers[id] = handler;
        return id;
    }

    public void Unregister(int id)
    {
        if (_handlers.Remove(id)) Win32.UnregisterHotKey(_source.Handle, id);
    }

    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32.WM_HOTKEY && _handlers.TryGetValue((int)wParam, out var handler))
        {
            handler();
            handled = true;
        }
        return 0;
    }

    public void Dispose()
    {
        foreach (var id in _handlers.Keys.ToArray()) Unregister(id);
        _source.RemoveHook(Hook);
    }
}

public interface ISpeechOutput
{
    bool IsSpeaking { get; }
    void Speak(string text, double speed);
    void Pause();
    void Resume();
    void Stop();
}

public interface ISpeechInput
{
    bool IsListening { get; }
    /// <summary>Starts recognising from the default microphone. Throws with an actionable message if it cannot.</summary>
    void Start();
    /// <summary>Stops and returns what was heard.</summary>
    Task<string> StopAsync();
    void Cancel();
}

/// <summary>Windows SAPI text-to-speech. Local, no network, interruptible.</summary>
public sealed class SapiSpeechOutput : ISpeechOutput, IDisposable
{
    private readonly SpeechSynthesizer _synth = new();

    public SapiSpeechOutput() => _synth.SetOutputToDefaultAudioDevice();

    public bool IsSpeaking => _synth.State != SynthesizerState.Ready;

    public void Speak(string text, double speed)
    {
        Stop();
        if (string.IsNullOrWhiteSpace(text)) return;
        // SAPI rate is -10..10; map 0.5x..2.0x onto it around 1.0x = 0.
        _synth.Rate = Math.Clamp((int)Math.Round(Math.Log2(speed) * 8), -10, 10);
        _synth.SpeakAsync(text);
    }

    public void Pause() { if (_synth.State == SynthesizerState.Speaking) _synth.Pause(); }
    public void Resume() { if (_synth.State == SynthesizerState.Paused) _synth.Resume(); }

    public void Stop()
    {
        if (_synth.State == SynthesizerState.Paused) _synth.Resume();
        _synth.SpeakAsyncCancelAll();
    }

    public void Dispose() => _synth.Dispose();
}

/// <summary>
/// Windows SAPI dictation, used push-to-talk. Audio goes from the microphone straight into the
/// recogniser in memory; nothing is written to disk and nothing leaves the machine.
/// </summary>
public sealed class SapiSpeechInput : ISpeechInput, IDisposable
{
    // ponytail: SAPI desktop dictation is the no-dependency local option; accuracy is modest, especially for technical terms.
    // Swap in a provider STT behind ISpeechInput when a key for one is configured.
    private SpeechRecognitionEngine? _engine;
    private readonly StringBuilder _heard = new();
    private TaskCompletionSource<bool>? _completed;

    public bool IsListening { get; private set; }

    public void Start()
    {
        if (IsListening) return;
        try
        {
            _engine ??= Create();
            _heard.Clear();
            _completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _engine.RecognizeAsync(RecognizeMode.Multiple);
            IsListening = true;
        }
        catch (Exception e) when (e is InvalidOperationException or PlatformNotSupportedException or COMException or ArgumentException)
        {
            _engine?.Dispose();
            _engine = null;
            throw new InvalidOperationException(
                "Voice input is unavailable: no microphone or no Windows speech recogniser was found. " +
                "Check Settings > Privacy > Microphone and Settings > Time & language > Speech. Typing works the same. (" + e.Message + ")", e);
        }
    }

    public async Task<string> StopAsync()
    {
        if (!IsListening || _engine is null) return "";
        IsListening = false;
        _engine.RecognizeAsyncStop();
        if (_completed is not null) await Task.WhenAny(_completed.Task, Task.Delay(3000));
        return _heard.ToString().Trim();
    }

    public void Cancel()
    {
        if (!IsListening || _engine is null) return;
        IsListening = false;
        _engine.RecognizeAsyncCancel();
        _heard.Clear();
    }

    private SpeechRecognitionEngine Create()
    {
        var engine = new SpeechRecognitionEngine();
        engine.LoadGrammar(new DictationGrammar());
        engine.SetInputToDefaultAudioDevice();
        engine.SpeechRecognized += (_, e) => { if (e.Result.Confidence > 0.3) _heard.Append(e.Result.Text).Append(' '); };
        engine.RecognizeCompleted += (_, _) => _completed?.TrySetResult(true);
        return engine;
    }

    public void Dispose() => _engine?.Dispose();
}
