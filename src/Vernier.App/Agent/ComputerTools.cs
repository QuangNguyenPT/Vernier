using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// The tutor's hands: one input step per call, in the window the question was asked about, after
/// the learner allows that exact step. The step is planned before approval (target resolved by
/// id or text, never by a guessed box; shown on screen), checked again against the live window
/// just before it runs, and followed by a verdict on whether the screen changed. Terminals,
/// administrator windows, password fields and Vernier itself are refused. Esc stops a step and
/// ends hands for the rest of the turn.
/// </summary>
public static class ComputerTools
{
    public static readonly string[] Names = ["computer_act"];
    public static IEnumerable<ITool> All() => [new ComputerAct()];

    private const string RevokedKey = "hands-revoked";

    // A keystroke in any of these runs a command.
    private static readonly HashSet<string> Terminals = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd", "powershell", "powershell_ise", "pwsh", "WindowsTerminal", "wt", "conhost", "OpenConsole", "wsl", "wslhost", "bash", "mintty",
        "alacritty", "wezterm-gui", "putty", "kitty", "Hyper", "Tabby", "ConEmu", "ConEmu64", "Cmder",
    };

    /// <summary>Why the tutor may not operate this window, or null when it may.</summary>
    public static string? Refusal(nint window)
    {
        if (window == 0 || !Win32.IsWindow(window)) return "There is no application window to act in: the question was not asked over an application.";
        Win32.GetWindowThreadProcessId(window, out var pid);
        if (pid == Environment.ProcessId) return "That is Vernier's own window; the tutor does not operate Vernier.";
        string name;
        try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException) { return "The application has closed."; }
        if (Terminals.Contains(name))
            return $"{name} is a terminal. Vernier never types or clicks in terminals or consoles, because a keystroke there runs a command. Give the learner the command and let them type it.";
        if (IsElevated(pid))
            return $"{name} runs as administrator (or its privileges cannot be read). Vernier does not operate administrator windows; give the learner the steps instead.";
        return null;
    }

    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000, TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(nint token, int cls, out int info, int length, out int returned);

    /// <summary>True when the process is elevated, or when Windows will not say (protected processes): both are refused.</summary>
    public static bool IsElevated(uint pid)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == 0) return true;
        try
        {
            if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return true;
            try { return !GetTokenInformation(token, TokenElevation, out var elevated, sizeof(int), out _) || elevated != 0; }
            finally { CloseHandle(token); }
        }
        finally { CloseHandle(process); }
    }

    /// <summary>One planned step: the input to send, where, in which window, and what the learner is shown.</summary>
    private sealed record Plan(IReadOnlyList<ComputerAction> Steps, nint Window, PxRect WindowBounds, PxRect? Target, string Summary)
    {
        public Guid? PreviewMark { get; set; }
    }

    private sealed class ComputerAct : ITool
    {
        public ToolDefinition Definition { get; } = new("computer_act",
            "Operates the learner's application for them, one step per call, only when they asked you to do it for them. " +
            "click, double_click or right_click a target; type text into the field that has focus; press keys such as ctrl+s; scroll at a target (amount: positive up, negative down). " +
            "Name the target by id or exact text from the current screen list; a guessed pixel box is refused. The learner approves every step before it runs. " +
            "It runs only in the window the question was asked about, never in terminals, administrator windows or password fields. " +
            "The result says whether the window changed; call look before the next step, because ids from before a step are stale.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["action"] = new JsonObject { ["type"] = "string", ["enum"] = new JsonArray("click", "double_click", "right_click", "type", "keys", "scroll") },
                    ["target"] = ScreenTools.TargetSchema(),
                    ["text"] = new JsonObject { ["type"] = "string", ["maxLength"] = ActionPolicy.MaxTextLength, ["description"] = "For type: the text, without Enter (press it as a separate keys step)." },
                    ["keys"] = new JsonObject { ["type"] = "string", ["maxLength"] = 40, ["description"] = "For keys: a chord such as ctrl+s, enter, alt+f." },
                    ["amount"] = new JsonObject { ["type"] = "integer", ["minimum"] = -20, ["maximum"] = 20, ["description"] = "For scroll: notches." },
                    ["why"] = new JsonObject { ["type"] = "string", ["maxLength"] = 120, ["description"] = "What this step does, in a few words; the learner reads it when approving." },
                },
                ["required"] = new JsonArray("action", "why"),
                ["additionalProperties"] = false,
            },
            ToolRisk.Act);

        private static string Key(JsonObject args) => "act:" + args.ToJsonString();

        public async Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            if (turn.State.ContainsKey(RevokedKey)) throw new ToolException(Revoked);
            var plan = Make(args, turn);
            turn.State[Key(args)] = plan;
            if (plan.Target is { } t)
            {
                // Show the learner exactly where, while they decide.
                var mark = new Annotation
                {
                    Kind = AnnotationKind.Point, Points = [new PxPoint(t.X, t.Y), new PxPoint(t.Right, t.Bottom)], Label = "here?",
                    Meaning = "proposed input step", Source = "action-preview", Lifetime = UiApprover.Timeout + TimeSpan.FromSeconds(5),
                };
                plan.PreviewMark = mark.Id;
                await AppServices.Ui.InvokeAsync(() => AppServices.Annotations.Add(mark)).Task.ConfigureAwait(false);
            }
            return plan.Summary;
        }

        public void Declined(JsonObject args, ToolTurn turn)
        {
            if (turn.State.TryRemove(Key(args), out var p) && p is Plan { PreviewMark: { } markId })
                AppServices.OnUi(() => AppServices.Annotations.Remove(markId));
        }

        private const string Revoked = "The learner pressed Esc, which ends the tutor's hands for this turn. Do not act again; tell them what is left to do.";

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            // Only the plan the learner saw runs; the executor never lets another call in between preview and here.
            var plan = turn.State.TryRemove(Key(args), out var p) && p is Plan planned ? planned : throw new ToolException("This step was not previewed, so it did not run.");
            if (plan.PreviewMark is { } markId) await AppServices.Ui.InvokeAsync(() => AppServices.Annotations.Remove(markId)).Task.ConfigureAwait(false);
            if (turn.State.ContainsKey(RevokedKey)) throw new ToolException(Revoked);

            // Re-review against the live window: approval took seconds and the screen may have moved on.
            if (Refusal(plan.Window) is { } refusal) throw new ToolException(refusal);
            if (Win32.WindowBounds(plan.Window) is not { } now || !Near(now, plan.WindowBounds))
                throw new ToolException("The window moved, resized or closed since the capture, so the step did not run. Call look and plan it again.");
            var frame = turn.State.TryGetValue(ScreenTools.FrameKey, out var f) ? f as CapturedFrame : null;
            if (plan.Target is { } target && frame is not null && await Task.Run(() => ScreenCaptureService.HasChanged(frame, target), ct).ConfigureAwait(false))
                throw new ToolException("That part of the screen changed after the capture, so the step did not run. Call look to see it as it is now.");
            foreach (var step in plan.Steps)
                if (step.Point is { } point && Covering(point, plan.Window) is { } cover) throw new ToolException(cover);

            using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
            // Esc must be able to stop the step wherever focus is, so it is held as a global hotkey while the step runs; with
            // no way to stop it, the step does not run. (A step that itself presses Esc is instant and would trip its own key.)
            var pressesEsc = plan.Steps.Any(s => s.Type == ActionType.Keys && ActionPolicy.Normalize(s.Keys).Split('+').Contains("esc"));
            var hotkeys = await AppServices.Ui.InvokeAsync(() => (System.Windows.Application.Current.MainWindow as Views.MainWindow)?.Hotkeys).Task.ConfigureAwait(false);
            var esc = hotkeys is null || pressesEsc ? -1 : await AppServices.Ui.InvokeAsync(() => hotkeys.Register("Esc (stop the tutor's hands)", 0, 0x1B, () =>
            {
                turn.State[RevokedKey] = true;
                stop.Cancel();
            })).Task.ConfigureAwait(false);
            if (esc < 0 && !pressesEsc) throw new ToolException("Vernier could not reserve Esc to stop the step (another application holds it), so the step did not run.");
            var driver = new SendInputDriver(plan.Window);
            var sent = 0;
            try
            {
                if (!Win32.BringToFront(plan.Window))
                    throw new ToolException("Windows would not bring the application to the front, so the step did not run. Ask the learner to click into it, then try again.");
                await Task.Delay(120, stop.Token).ConfigureAwait(false);
                foreach (var step in plan.Steps)
                {
                    await driver.ExecuteAsync(step, stop.Token).ConfigureAwait(false);
                    sent++;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new ToolException(sent == 0
                    ? "The learner pressed Esc and the step was stopped before anything was sent. Do not act again in this turn."
                    : "The learner pressed Esc after part of the step was sent. Do not act again in this turn; tell them what happened.");
            }
            catch (InvalidOperationException e)
            {
                throw new ToolException(e.Message + " The step did not complete.");
            }
            finally
            {
                if (esc >= 0) await AppServices.Ui.InvokeAsync(() => hotkeys!.Unregister(esc)).Task.ConfigureAwait(false);
            }
            AppServices.Log.Info($"Tutor input: {plan.Steps[^1].Type} step sent."); // never the text that was typed

            // Effect verdict: did anything change in the window since the capture the step was planned on?
            await Task.Delay(450, ct).ConfigureAwait(false);
            var changed = frame is not null && await Task.Run(() => ScreenCaptureService.HasChanged(frame, plan.WindowBounds), ct).ConfigureAwait(false);
            return ToolResult.Ok(changed
                ? "Done. The window changed after the step. Call look to see the result before the next step; earlier ids are no longer valid."
                : "Sent, but nothing visibly changed in the window. The step may not have taken effect (wrong target, a dialog, or the app ignores synthetic input). Call look before trying anything else.");
        }

        private static bool Near(PxRect a, PxRect b) => Math.Abs(a.X - b.X) < 2 && Math.Abs(a.Y - b.Y) < 2 && Math.Abs(a.Width - b.Width) < 2 && Math.Abs(a.Height - b.Height) < 2;

        // Vernier's glass and edge lights are click-through; anything else on top would take the click.
        private static string? Covering(PxPoint point, nint window)
        {
            var hit = Win32.GetAncestor(Win32.WindowFromPoint(new Win32.POINT { X = (int)point.X, Y = (int)point.Y }), Win32.GA_ROOT);
            if (hit == window || ((long)Win32.GetWindowLongPtr(hit, Win32.GWL_EXSTYLE) & Win32.WS_EX_TRANSPARENT) != 0) return null;
            Win32.GetWindowThreadProcessId(hit, out var pid);
            return pid == Environment.ProcessId
                ? "Vernier's ask bar is over that point, so the click would land on it. Tell the learner to move the bar, then call look and try again."
                : "Another window is over that point, so the step did not run.";
        }

        private static Plan Make(JsonObject args, ToolTurn turn)
        {
            var ask = turn.State.TryGetValue(ScreenTools.AskKey, out var a) && a is ScreenAsk s ? s : throw new ToolException("This turn did not start from the screen, so there is no application to act in.");
            var screen = ScreenTools.Screen(turn);
            if (Refusal(ask.Foreground) is { } refusal) throw new ToolException(refusal);
            var window = ask.Foreground;
            var bounds = Win32.WindowBounds(window) ?? throw new ToolException("The application window is minimised or closed.");
            var action = args["action"]!.GetValue<string>();
            var why = args["why"]!.GetValue<string>().Trim();

            PxRect? target = null;
            if (args["target"] is JsonObject t)
            {
                var resolved = ScreenTools.Resolve(screen, t);
                if (resolved.Confidence < 0.6)
                    throw new ToolException("That target is only an estimate from the screenshot; acting on a guess could hit the wrong thing. Name it by id or exact text (find_on_screen), or ask the learner to do this step.");
                target = resolved.Bounds;
            }
            else if (action is "click" or "double_click" or "right_click" or "scroll")
                throw new ToolException($"{action} needs a target.");

            var at = target is { } r ? screen.Geometry.PhysicalToImage(r.Center) : (PxPoint?)null;
            ProposedAction Proposal(string type) => new() { Type = type, X = at?.X, Y = at?.Y, Text = args["text"]?.GetValue<string>(), Keys = args["keys"]?.GetValue<string>(), Amount = Json.Int(args["amount"]), Why = why };
            ProposedAction[] proposals = action switch
            {
                "scroll" => [Proposal("move"), Proposal("scroll")],
                _ => [Proposal(action.Replace("_", ""))],
            };
            var decisions = ActionPolicy.Review(proposals, screen.Geometry, bounds);
            if (decisions.FirstOrDefault(d => !d.Allowed) is { } blocked) throw new ToolException(blocked.Reason);
            var steps = decisions.Select(d => d.Action).ToArray();
            var what = steps[^1].Type switch
            {
                ActionType.Type => $"Type \"{steps[^1].Text}\"",
                ActionType.Keys => $"Press {steps[^1].Keys}",
                ActionType.Scroll => $"Scroll {(steps[^1].Amount < 0 ? "down" : "up")} {Math.Abs(steps[^1].Amount)}",
                var kind => kind switch { ActionType.DoubleClick => "Double-click", ActionType.RightClick => "Right-click", _ => "Click" } + (target is null ? "" : " the marked spot"),
            };
            return new Plan(steps, window, bounds, target, $"{what} in {Win32.WindowTitle(window)}.\nWhy: {why}");
        }
    }
}
