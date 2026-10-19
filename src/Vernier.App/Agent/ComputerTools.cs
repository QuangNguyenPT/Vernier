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
    public static readonly string[] Names = ["computer_act", "write_to_app"];
    public static IEnumerable<ITool> All() => [new ComputerAct(), new WriteToApp()];

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

    /// <summary>
    /// Puts the tutor's written work into the application in front, the way that application is meant to be written to:
    /// Word (text after the cursor), PowerPoint (a slide after the current one), Excel (one cell) through Office's own object
    /// model, and any other editor by pasting at the cursor. One call, one effect, previewed exactly and run only after the
    /// learner allows it. Small models pass just the text: Vernier works out which application, where, and how.
    /// </summary>
    private sealed class WriteToApp : ITool
    {
        public const int MaxText = 4000, MaxBullets = 8;

        public ToolDefinition Definition { get; } = new("write_to_app",
            "Writes your worked text into the application the learner has in front, only when they ask you to put it there. Vernier picks how from the window: " +
            "Word inserts text after the cursor (title = an optional heading); PowerPoint adds a slide after the current one (title = slide title, text = one bullet per line); " +
            "Excel sets one cell (cell such as B2, default the selected cell; text = the value or formula; a cell that is not empty needs overwrite: true); " +
            "other editors (Notepad, Google Docs in the browser, OneNote…) get the text pasted at the cursor. Blender, terminals, administrator windows and password fields are refused. " +
            "The learner sees exactly what will be written and allows it first. Simplest call: {\"text\":\"Step 1: …\",\"why\":\"your worked answer\"}.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["text"] = new JsonObject { ["type"] = "string", ["maxLength"] = MaxText, ["description"] = "What to write: plain text, symbols written out (θ, ², →), no TeX." },
                    ["title"] = new JsonObject { ["type"] = "string", ["maxLength"] = 120, ["description"] = "Optional: a heading (Word) or the slide title (PowerPoint)." },
                    ["cell"] = new JsonObject { ["type"] = "string", ["maxLength"] = 12, ["description"] = "Optional, Excel: the cell, e.g. B2." },
                    ["overwrite"] = new JsonObject { ["type"] = "boolean", ["description"] = "Optional, Excel: replace a cell that is not empty." },
                    ["why"] = new JsonObject { ["type"] = "string", ["maxLength"] = 120, ["description"] = "What this is, in a few words; the learner reads it when approving." },
                },
                ["required"] = new JsonArray("text", "why"),
                ["additionalProperties"] = false,
            },
            ToolRisk.Act);

        private sealed record Plan(nint Window, Office.App App, string Process, string Text, string? Title, string? Cell, bool Overwrite)
        {
            public Office.CellTarget? Target { get; init; }
        }

        private static string Key(JsonObject args) => "write:" + args.ToJsonString();

        public async Task<string?> PreviewAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            if (turn.State.ContainsKey(RevokedKey)) throw new ToolException("The learner pressed Esc, which ends the tutor's hands for this turn. Do not write again.");
            var ask = turn.State.TryGetValue(ScreenTools.AskKey, out var a) && a is ScreenAsk s ? s : throw new ToolException("This turn did not start from the screen, so there is no application to write into.");
            if (Refusal(ask.Foreground) is { } refusal) throw new ToolException(refusal);
            Win32.GetWindowThreadProcessId(ask.Foreground, out var pid);
            string process;
            try { using var p = Process.GetProcessById((int)pid); process = p.ProcessName; }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException) { throw new ToolException("The application has closed."); }
            if (process.Equals("blender", StringComparison.OrdinalIgnoreCase))
                throw new ToolException("Vernier does not drive Blender (it has no safe way in, and running code there is not allowed). Mark the controls with overlay_marks and give the learner the steps.");
            var text = Clean(args["text"]?.GetValue<string>() ?? "");
            if (text.Length == 0) throw new ToolException("text is empty: give the words to write, e.g. {\"text\":\"Fmax = 13.7 N\",\"why\":\"the answer to (b)(i)\"}.");
            var plan = new Plan(ask.Foreground, Office.Of(process), process, text, args["title"]?.GetValue<string>() is { Length: > 0 } t ? Clean(t) : null,
                args["cell"]?.GetValue<string>(), args["overwrite"]?.GetValue<bool>() == true);
            string where;
            try
            {
                if (plan.App == Office.App.None) where = $"{Win32.WindowTitle(plan.Window)}, pasted at the cursor";
                else
                {
                    var described = await Office.DescribeAsync(plan.Window, plan.App, plan.Cell).ConfigureAwait(false);
                    where = described.Where;
                    plan = plan with { Target = described.Cell }; // the write goes to this cell and no other
                }
            }
            catch (OfficeException e) { throw new ToolException(e.Message); }
            if (plan.App == Office.App.Excel && Office.FormulaProblem(text) is { } bad) throw new ToolException(bad);
            turn.State[Key(args)] = plan;
            var what = plan.App switch
            {
                Office.App.PowerPoint => $"Title: {plan.Title ?? Lines(text)[0]}\n" + string.Join("\n", Bullets(plan).Select(b => "• " + b)),
                Office.App.Excel => (plan.Overwrite ? "Replace with: " : "Value: ") + text,
                _ => (plan.Title is { } h ? h + "\n" : "") + text,
            };
            return $"Write into {where}:\n{what}\nWhy: {args["why"]?.GetValue<string>()?.Trim()}";
        }

        public void Declined(JsonObject args, ToolTurn turn) => turn.State.TryRemove(Key(args), out _);

        public async Task<ToolResult> ExecuteAsync(JsonObject args, ToolTurn turn, CancellationToken ct)
        {
            var plan = turn.State.TryRemove(Key(args), out var p) && p is Plan planned ? planned : throw new ToolException("This write was not previewed, so it did not run.");
            if (turn.State.ContainsKey(RevokedKey)) throw new ToolException("The learner pressed Esc, which ends the tutor's hands for this turn. Nothing was written.");
            if (Refusal(plan.Window) is { } refusal) throw new ToolException(refusal);
            try
            {
                var done = plan.App switch
                {
                    Office.App.Word => await Office.WordInsertAsync(plan.Window, plan.Title, plan.Text).ConfigureAwait(false),
                    Office.App.PowerPoint => await Office.AddSlideAsync(plan.Window, plan.Title ?? Lines(plan.Text)[0], Bullets(plan)).ConfigureAwait(false),
                    Office.App.Excel => await Office.SetCellAsync(plan.Window, plan.Target!, plan.Text, plan.Overwrite).ConfigureAwait(false),
                    _ => await PasteAsync(plan, turn, ct).ConfigureAwait(false),
                };
                AppServices.Log.Info($"Tutor wrote into {plan.Process} ({plan.App})."); // never the text
                return ToolResult.Ok(done);
            }
            catch (OfficeException e) { throw new ToolException(e.Message); }
        }

        /// <summary>How long the pasted text stays on the clipboard: browser and Electron editors read it a moment after Ctrl+V.</summary>
        private static readonly TimeSpan PasteHold = TimeSpan.FromMilliseconds(1200);

        // Any other editor: the text goes on the clipboard, Ctrl+V into the field that has focus, and the learner's clipboard is put
        // back exactly (every format it held). If it holds something that cannot be copied back, nothing is pasted.
        private static async Task<string> PasteAsync(Plan plan, ToolTurn turn, CancellationToken ct)
        {
            var saved = await Clip(() =>
            {
                var keep = new System.Windows.DataObject();
                if (System.Windows.Clipboard.GetDataObject() is { } current)
                    foreach (var format in current.GetFormats(false))
                    {
                        object? data;
                        try { data = current.GetData(format, false); }
                        catch (Exception e) when (e is COMException or OutOfMemoryException or InvalidOperationException) { data = null; }
                        if (data is null) throw new ToolException("The learner's clipboard holds something Vernier could not put back afterwards, so nothing was pasted. Ask them to copy something else (or nothing), or give them the text to paste.");
                        keep.SetData(format, data);
                    }
                return keep;
            }).ConfigureAwait(false);
            if (!Win32.BringToFront(plan.Window)) throw new ToolException("Windows would not bring the application to the front, so nothing was pasted. Ask the learner to click where the text should go, then try again.");
            await Task.Delay(150, ct).ConfigureAwait(false);
            var text = (plan.Title is { } h ? h + "\n" : "") + plan.Text;
            await Clip(() =>
            {
                var paste = new System.Windows.DataObject(System.Windows.DataFormats.UnicodeText, text);
                paste.SetData("ExcludeClipboardContentFromMonitorProcessing", new System.IO.MemoryStream([1, 0, 0, 0])); // not into clipboard history
                System.Windows.Clipboard.SetDataObject(paste, true);
                return true;
            }).ConfigureAwait(false);
            try
            {
                if (turn.State.ContainsKey(RevokedKey)) throw new ToolException("The learner pressed Esc, which ends the tutor's hands for this turn. Nothing was pasted.");
                if (SendInputDriver.FocusIsPassword()) throw new ToolException("The field that has focus is a password field (or could not be checked); Vernier never writes into those.");
                await new SendInputDriver(plan.Window).ExecuteAsync(new ComputerAction(ActionType.Keys, null, null, "ctrl+v", 0, "paste"), ct).ConfigureAwait(false);
                await Task.Delay(PasteHold, CancellationToken.None).ConfigureAwait(false);
            }
            catch (InvalidOperationException e) { throw new ToolException(e.Message + " Nothing was pasted."); }
            finally
            {
                await Clip(() => { System.Windows.Clipboard.SetDataObject(saved, true); return true; }).ConfigureAwait(false);
            }
            return $"Pasted {plan.Text.Length} characters at the cursor in {Win32.WindowTitle(plan.Window)}. Call look to check it landed where you meant.";
        }

        // The clipboard is shared with every other program: opening it can fail for a moment, so try a few times before giving up.
        private static async Task<T> Clip<T>(Func<T> work)
        {
            for (var attempt = 0; ; attempt++)
            {
                try { return await AppServices.Ui.InvokeAsync(work).Task.ConfigureAwait(false); }
                catch (COMException) when (attempt < 5) { await Task.Delay(60).ConfigureAwait(false); }
                catch (COMException) { throw new ToolException("Another program is holding the clipboard, so nothing was pasted. Try again in a moment."); }
            }
        }

        private static string Clean(string s) => new(TexPlain.Clean(s.Trim()).Where(c => c is '\n' or '\t' || !char.IsControl(c)).ToArray());
        private static string[] Lines(string s) => s.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) is { Length: > 0 } l ? l : [s];
        private static IReadOnlyList<string> Bullets(Plan p) => [.. (p.Title is null ? Lines(p.Text).Skip(1) : Lines(p.Text)).Take(MaxBullets)];
    }
}
