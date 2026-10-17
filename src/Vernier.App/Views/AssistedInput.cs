using System.Globalization;
using System.Text;
using System.Windows;
using Vernier.App.Platform;
using Vernier.Core.Agents;
using Vernier.Core.Annotations;
using Vernier.Core.Geometry;

namespace Vernier.App.Views;

/// <summary>Shows the steps the tutor proposed, on screen and as a list, and runs them only after the learner approves.</summary>
public static class AssistedInput
{
    public static async Task ReviewAndRunAsync(Window? owner)
    {
        var session = AppServices.Session;
        var plan = session.PendingActions;
        var target = session.ActionWindow;
        if (plan.Count == 0 || target == 0) return;

        // Number the click points on screen so the plan can be checked against the real window.
        var previews = plan.Select((d, i) => (d, i)).Where(x => x.d.Allowed && x.d.Action.Point is not null).Select(x => new Annotation
        {
            Kind = AnnotationKind.Circle,
            Points = [new PxPoint(x.d.Action.Point!.Value.X - 10, x.d.Action.Point.Value.Y - 10), new PxPoint(x.d.Action.Point.Value.X + 10, x.d.Action.Point.Value.Y + 10)],
            Label = (x.i + 1).ToString(CultureInfo.InvariantCulture), Meaning = "proposed input step", Source = "action-preview", Lifetime = TimeSpan.FromMinutes(2),
        }).ToArray();
        AppServices.Annotations.Add(previews);

        var sb = new StringBuilder($"Target window: {Win32.WindowTitle(target)}\n\n");
        for (var i = 0; i < plan.Count; i++)
        {
            var d = plan[i];
            sb.Append(i + 1).Append(". ").Append(d.Action.Describe());
            if (!string.IsNullOrWhiteSpace(d.Action.Why)) sb.Append("  — ").Append(d.Action.Why);
            if (!d.Allowed) sb.Append("\n   BLOCKED: ").Append(d.Reason);
            sb.Append('\n');
        }
        sb.Append("\nSteps run one at a time and stop at the first blocked step, if the window loses focus, or when you press Esc. " +
                  "Positions are the model's estimates; check the numbered circles on screen first.");
        var approved = ConfirmDialog.Ask(owner, "Run these steps in the target window?", sb.ToString(), "Run steps", mono: true);
        foreach (var p in previews) AppServices.Annotations.Remove(p.Id);
        if (!approved) return;

        using var cts = new CancellationTokenSource();
        var escId = -1;
        var hotkeys = (Application.Current.MainWindow as MainWindow)?.Hotkeys;
        if (hotkeys is not null) escId = hotkeys.Register("Esc (stop steps)", 0, 0x1B, cts.Cancel);
        // The ask bar floats above everything; a click meant for the application must not land on it.
        var bar = owner as AskBar;
        bar?.Hide();
        try
        {
            Win32.SetForegroundWindow(target);
            await Task.Delay(350);
            var (done, why) = await new ActionExecutor(new SendInputDriver(target)).RunAsync(plan, _ => Task.FromResult(true), cts.Token);
            AppServices.Log.Info($"Assisted input: {done} of {plan.Count} step(s) executed. {why}");
            bar?.Show();
            var result = why is null ? $"All {done} step(s) were sent." : $"{done} of {plan.Count} step(s) were sent. {why}";
            if (owner is null) MessageBox.Show(result, "Vernier", MessageBoxButton.OK, MessageBoxImage.Information);
            else MessageBox.Show(owner, result, "Vernier", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally
        {
            if (escId >= 0) hotkeys!.Unregister(escId);
            session.ClearPendingActions();
        }
    }
}
