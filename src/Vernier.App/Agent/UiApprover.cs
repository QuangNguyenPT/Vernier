using Vernier.App.Views;
using Vernier.Core.Tools;

namespace Vernier.App.Agent;

/// <summary>
/// Asks the learner before any Act or Consequential tool call runs, on a card that stays on top
/// without taking focus or blocking Vernier. No answer within a minute counts as no: the card
/// closes and the call does not run. Esc on the card stops the tutor's whole answer.
/// </summary>
public sealed class UiApprover : IToolApprover
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public async Task<ApprovalResolution> ApproveAsync(ApprovalRequest request, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        var asked = await AppServices.Ui.InvokeAsync(() => ConfirmDialog.AskAsync("Let the tutor do this?",
            $"{request.Summary}\n\nNothing runs unless you allow it. This closes by itself in {Timeout.TotalSeconds:0} seconds. Esc stops the tutor.",
            "Allow once", danger: request.Tool.Risk == ToolRisk.Consequential, timeout.Token)).Task.ConfigureAwait(false);
        var answer = await asked.ConfigureAwait(false);
        if (answer == true) return ApprovalResolution.AllowOnce;
        if (answer is null)
        {
            request.Turn.End(); // Esc: the tutor's tools end for this answer
            return ApprovalResolution.Cancelled;
        }
        return ct.IsCancellationRequested ? ApprovalResolution.Cancelled : timeout.IsCancellationRequested ? ApprovalResolution.Timeout : ApprovalResolution.Deny;
    }
}
