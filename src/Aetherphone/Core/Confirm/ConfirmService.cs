using Aetherphone.Core.Localization;
using Aetherphone.Core.Net;

namespace Aetherphone.Core.Confirm;

internal enum ConfirmSectionKind
{
    Paragraph,
    Card,
    Labeled,
    Chip,
    Divider,
}

internal readonly struct ConfirmSection
{
    public readonly ConfirmSectionKind Kind;
    public readonly string Label;
    public readonly string Text;

    private ConfirmSection(ConfirmSectionKind kind, string label, string text)
    {
        Kind = kind;
        Label = label;
        Text = text;
    }

    public static ConfirmSection Paragraph(string text) => new(ConfirmSectionKind.Paragraph, string.Empty, text);

    public static ConfirmSection Card(string label, string text) => new(ConfirmSectionKind.Card, label, text);

    public static ConfirmSection Labeled(string label, string text) => new(ConfirmSectionKind.Labeled, label, text);

    public static ConfirmSection Chip(string label, string text) => new(ConfirmSectionKind.Chip, label, text);

    public static ConfirmSection Divider() => new(ConfirmSectionKind.Divider, string.Empty, string.Empty);
}

internal sealed class ConfirmRequest
{
    public string? Title;
    public required string Message;
    public required string ConfirmLabel;
    public required string CancelLabel;
    public string? BusyLabel;
    public string? FailedMessage;
    public ConfirmSection[]? Sections;
    public bool Danger = true;
    public bool Acknowledge;
    public Action<Action<bool>>? ConfirmAsync;
    public Action? Confirm;
    public Action? Cancel;
}

internal sealed class ConfirmService
{
    private readonly Queue<ConfirmRequest> queued = new();
    private readonly FailureSlot failure = new();

    public ConfirmRequest? Active { get; private set; }
    public volatile bool Busy;
    public string? Status { get; private set; }

    public void Ask(ConfirmRequest request)
    {
        if (Active is not null)
        {
            queued.Enqueue(request);
            return;
        }

        Active = request;
        Busy = false;
        Status = null;
        failure.Clear();
    }

    public void ReportFailure(AepFailure value)
    {
        failure.Set(value);
    }

    public void Alert(string? title, string message, string dismissLabel, Action? onDismiss = null)
    {
        Ask(new ConfirmRequest
        {
            Title = title,
            Message = message,
            ConfirmLabel = dismissLabel,
            CancelLabel = dismissLabel,
            Danger = false,
            Acknowledge = true,
            Confirm = onDismiss,
            Cancel = onDismiss,
        });
    }

    public void Alert(string? title, ConfirmSection[] sections, string message, string dismissLabel,
        Action? onDismiss = null)
    {
        Ask(new ConfirmRequest
        {
            Title = title,
            Message = message,
            Sections = sections,
            ConfirmLabel = dismissLabel,
            CancelLabel = dismissLabel,
            Danger = false,
            Acknowledge = true,
            Confirm = onDismiss,
            Cancel = onDismiss,
        });
    }

    public void Proceed()
    {
        if (Active is not { } request || Busy)
        {
            return;
        }

        if (request.ConfirmAsync is { } handler)
        {
            Busy = true;
            Status = null;
            handler(ok =>
            {
                Busy = false;
                if (ok)
                {
                    Advance();
                }
                else
                {
                    Status = ExplainFailure(request);
                }
            });
            return;
        }

        request.Confirm?.Invoke();
        Advance();
    }

    private string ExplainFailure(ConfirmRequest request)
    {
        if (failure.Failed)
        {
            return failure.Text();
        }

        return request.FailedMessage ?? Loc.T(L.Failure.Unknown, AepFailure.None.Reference());
    }

    public void CancelActive()
    {
        if (Busy || Active is not { } request)
        {
            return;
        }

        request.Cancel?.Invoke();
        Advance();
    }

    private void Advance()
    {
        Busy = false;
        Status = null;
        Active = queued.Count > 0 ? queued.Dequeue() : null;
    }
}
