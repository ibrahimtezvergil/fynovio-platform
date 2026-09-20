using System.Collections.Concurrent;

namespace Host.Email;

public sealed record DevMailboxEntry(Guid Id, DateTimeOffset At, string To, string TemplateId, string Subject, string Link, string Token);

/// <summary>Development-only in-memory record of every message the app "sent", with its link and token,
/// so a developer or an E2E run can complete invitation/reset flows without a mail provider.
/// Only registered — and only exposed at `/dev/mailbox` — in the Development environment.</summary>
public sealed class DevMailbox(TimeProvider timeProvider)
{
    private const int Capacity = 200;

    private readonly ConcurrentQueue<DevMailboxEntry> _entries = new();

    public void Record(RenderedEmail email)
    {
        _entries.Enqueue(new DevMailboxEntry(Guid.NewGuid(), timeProvider.GetUtcNow(), email.To, email.TemplateId, email.Subject, email.Link, email.Token));
        while (_entries.Count > Capacity && _entries.TryDequeue(out _)) { }
    }

    /// <summary>Newest first; optionally only the messages for one recipient.</summary>
    public IReadOnlyList<DevMailboxEntry> List(string? to = null) => _entries
        .Where(e => string.IsNullOrEmpty(to) || string.Equals(e.To, to.Trim(), StringComparison.OrdinalIgnoreCase))
        .Reverse()
        .ToList();

    public void Clear() => _entries.Clear();
}
