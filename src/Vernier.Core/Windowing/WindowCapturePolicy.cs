using Vernier.Core.Settings;

namespace Vernier.Core.Windowing;

public static class DisplayAffinity
{
    public const uint None = 0x00000000;               // WDA_NONE
    public const uint ExcludeFromCapture = 0x00000011; // WDA_EXCLUDEFROMCAPTURE
    /// <summary>First build that implements WDA_EXCLUDEFROMCAPTURE (Windows 10 version 2004).</summary>
    public const int MinimumBuild = 19041;
}

public enum PolicyStatus { NotRequested, Requested, Applied, Unsupported, Failed }

public sealed record WindowPolicyState(nint Hwnd, string Name, PolicyStatus Status, uint RequestedAffinity, uint? EffectiveAffinity, string Detail);

/// <summary>The thin slice of user32 the policy needs. The real implementation is P/Invoke; tests use a labelled fake.</summary>
public interface INativeWindowApi
{
    int OsBuild { get; }
    uint CurrentProcessId { get; }
    bool IsWindow(nint hwnd);
    bool IsTopLevel(nint hwnd);
    uint GetWindowProcessId(nint hwnd);
    bool SetWindowDisplayAffinity(nint hwnd, uint affinity, out int lastError);
    bool GetWindowDisplayAffinity(nint hwnd, out uint affinity, out int lastError);
}

public interface IWindowCapturePolicyService
{
    CapturePolicyMode Mode { get; }
    IReadOnlyList<WindowPolicyState> States { get; }
    /// <summary>One line for the status bar: what was asked for and what Windows reports.</summary>
    string Summary { get; }
    event Action? Changed;
    /// <summary>Call once the window has a handle (SourceInitialized) and again if the handle is recreated.</summary>
    void Register(nint hwnd, string name);
    void Unregister(nint hwnd);
    void SetMode(CapturePolicyMode mode);
    /// <summary>Re-applies to every registered window and re-reads the effective affinity.</summary>
    void Reapply();
}

/// <summary>
/// Requests SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE) for this process's own top-level
/// windows and reports what Windows says back. "Applied" means the OS accepted and reports the
/// affinity; it is not evidence that any particular capture tool honours it.
/// </summary>
public sealed class WindowCapturePolicyService(INativeWindowApi api) : IWindowCapturePolicyService
{
    private readonly Dictionary<nint, WindowPolicyState> _states = [];
    private readonly object _gate = new();

    public CapturePolicyMode Mode { get; private set; } = CapturePolicyMode.Standard;
    public event Action? Changed;

    public IReadOnlyList<WindowPolicyState> States
    {
        get { lock (_gate) return [.. _states.Values]; }
    }

    private bool WantsExclusion => Mode is CapturePolicyMode.RequestExclusion or CapturePolicyMode.ShareSafe;

    public void Register(nint hwnd, string name)
    {
        lock (_gate) _states[hwnd] = Apply(hwnd, name);
        Changed?.Invoke();
    }

    public void Unregister(nint hwnd)
    {
        lock (_gate) _states.Remove(hwnd);
        Changed?.Invoke();
    }

    public void SetMode(CapturePolicyMode mode)
    {
        Mode = mode;
        Reapply();
    }

    public void Reapply()
    {
        lock (_gate)
            foreach (var (hwnd, state) in _states.ToArray())
                _states[hwnd] = Apply(hwnd, state.Name);
        Changed?.Invoke();
    }

    public string Summary
    {
        get
        {
            var states = States;
            if (!WantsExclusion) return "Standard window: Vernier appears in screen captures like any other app.";
            if (states.Count == 0) return "Capture exclusion requested; no windows are open yet.";
            var applied = states.Count(s => s.Status == PolicyStatus.Applied);
            if (states.Any(s => s.Status == PolicyStatus.Unsupported)) return "Capture exclusion is not supported on this version of Windows.";
            return applied == states.Count
                ? $"Capture exclusion reported as applied by Windows for {applied} window(s). Capture tools may still differ."
                : $"Capture exclusion applied to {applied} of {states.Count} window(s); see Diagnostics.";
        }
    }

    private WindowPolicyState Apply(nint hwnd, string name)
    {
        var wanted = WantsExclusion ? DisplayAffinity.ExcludeFromCapture : DisplayAffinity.None;

        if (hwnd == 0 || !api.IsWindow(hwnd))
            return new(hwnd, name, PolicyStatus.Failed, wanted, null, "The window handle is not valid (the window may have been closed or recreated).");
        if (api.GetWindowProcessId(hwnd) != api.CurrentProcessId)
            return new(hwnd, name, PolicyStatus.Failed, wanted, null, "Refused: the window belongs to another process. Vernier only sets the policy on its own windows.");
        if (!api.IsTopLevel(hwnd))
            return new(hwnd, name, PolicyStatus.Failed, wanted, null, "Refused: display affinity applies to top-level windows only.");
        if (WantsExclusion && api.OsBuild < DisplayAffinity.MinimumBuild)
            return new(hwnd, name, PolicyStatus.Unsupported, wanted, null,
                $"WDA_EXCLUDEFROMCAPTURE needs Windows 10 version 2004 (build {DisplayAffinity.MinimumBuild}) or later; this is build {api.OsBuild}. Use Hide instead.");

        if (!api.SetWindowDisplayAffinity(hwnd, wanted, out var setError))
            return new(hwnd, name, PolicyStatus.Failed, wanted, ReadBack(hwnd),
                $"SetWindowDisplayAffinity failed (Win32 error {setError}). " + Hint(setError));

        if (!api.GetWindowDisplayAffinity(hwnd, out var effective, out var getError))
            return new(hwnd, name, WantsExclusion ? PolicyStatus.Requested : PolicyStatus.NotRequested, wanted, null,
                $"The request was accepted, but the effective affinity could not be read back (Win32 error {getError}).");

        if (effective != wanted)
            return new(hwnd, name, PolicyStatus.Failed, wanted, effective,
                $"Windows accepted the call but reports affinity 0x{effective:X2} instead of 0x{wanted:X2}.");

        return WantsExclusion
            ? new(hwnd, name, PolicyStatus.Applied, wanted, effective, "Windows reports WDA_EXCLUDEFROMCAPTURE (0x11) on this window.")
            : new(hwnd, name, PolicyStatus.NotRequested, wanted, effective, "Standard capture behaviour (WDA_NONE).");
    }

    private uint? ReadBack(nint hwnd) => api.GetWindowDisplayAffinity(hwnd, out var a, out _) ? a : null;

    private static string Hint(int error) => error switch
    {
        5 => "Access denied: the window must belong to this process and the desktop must be composited.",
        8 => "Not enough memory to apply the policy.",
        87 => "Invalid parameter: this Windows build may not accept WDA_EXCLUDEFROMCAPTURE for this window type.",
        1400 => "Invalid window handle.",
        _ => "",
    };
}
