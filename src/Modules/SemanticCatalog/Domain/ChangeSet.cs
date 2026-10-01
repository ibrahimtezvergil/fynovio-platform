using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Contracts;

namespace SemanticCatalog.Domain;

/// <summary>`Draft → Validated → AwaitingApproval → Approved → Published → Activating → Active | ActivationFailed`, plus the
/// side exits `Rejected`, `Discarded` and `Superseded` (adr-semantic-catalog-changeset.md S-5; OD-4: nothing is usable
/// before `Active`).</summary>
public enum ChangeSetStatus
{
    Draft,
    Validated,
    AwaitingApproval,
    Approved,
    Published,
    Activating,
    Active,
    ActivationFailed,
    Rejected,
    Discarded,
    Superseded
}

/// <summary>Who authored the set. Only `Human` exists in v1; an AI assistant source arrives with phase 3 and will need a
/// separate approver (self-approval is a human-only shortcut).</summary>
public enum ChangeSetSource
{
    Human
}

public sealed record ChangeSetTransition(ChangeSetStatus From, ChangeSetStatus To);

/// <summary>A catalog-owned, versioned bundle of definition changes (OD-5). ACID applies only inside the catalog (OD-4):
/// publishing applies every item and bumps the tenant's catalog revision in one transaction. Metadata only (OD-7): a set
/// carries definitions and layouts, never record values or secrets.</summary>
public sealed class ChangeSet
{
    private static readonly IReadOnlyDictionary<ChangeSetStatus, ChangeSetStatus[]> Allowed = new Dictionary<ChangeSetStatus, ChangeSetStatus[]>
    {
        [ChangeSetStatus.Draft] = [ChangeSetStatus.Validated, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.Validated] = [ChangeSetStatus.AwaitingApproval, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.AwaitingApproval] = [ChangeSetStatus.Approved, ChangeSetStatus.Rejected, ChangeSetStatus.Discarded, ChangeSetStatus.Superseded],
        [ChangeSetStatus.Approved] = [ChangeSetStatus.Published, ChangeSetStatus.Superseded],
        // Published → Active directly is the zero-participant case: nothing has to acknowledge, so there is nothing to wait for.
        [ChangeSetStatus.Published] = [ChangeSetStatus.Activating, ChangeSetStatus.Active],
        [ChangeSetStatus.Activating] = [ChangeSetStatus.Active, ChangeSetStatus.ActivationFailed],
        [ChangeSetStatus.ActivationFailed] = [ChangeSetStatus.Activating],
        [ChangeSetStatus.Active] = [],
        [ChangeSetStatus.Rejected] = [],
        [ChangeSetStatus.Discarded] = [],
        [ChangeSetStatus.Superseded] = []
    };

    private readonly List<ChangeSetItem> _items = [];
    private readonly List<ChangeSetTransition> _transitions = [];

    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public ChangeSetStatus Status { get; private set; }
    public ChangeSetSource Source { get; private set; }
    /// <summary>The tenant catalog revision this set was authored against; publishing refuses to overwrite a newer one.</summary>
    public long BaseRevision { get; private set; }
    public long? PublishedRevision { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public string CreatedByIssuer { get; private set; } = null!;
    public string CreatedBySubject { get; private set; } = null!;
    public string? FailureReason { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ChangeSetItem> Items => _items;

    /// <summary>The moves made on this instance, in order — recorded as evidence by whoever drives the lifecycle.</summary>
    public IReadOnlyList<ChangeSetTransition> Transitions => _transitions;

    private ChangeSet() { }

    public static ChangeSet Draft(TenantId tenantId, PrincipalRef author, long baseRevision, IEnumerable<ChangeSetItem> items)
    {
        var list = items.ToList();
        if (list.Count == 0)
            throw new ArgumentException("A change set needs at least one item.", nameof(items));
        if (baseRevision < 0)
            throw new ArgumentOutOfRangeException(nameof(baseRevision));

        // One set must not touch the same definition twice: the items would be applied in order against stale reads.
        var targets = list.Select(item => item.TargetIdentity).ToList();
        if (targets.Distinct(StringComparer.Ordinal).Count() != targets.Count)
            throw new ArgumentException("A change set cannot contain two items for the same definition.", nameof(items));

        var now = DateTimeOffset.UtcNow;
        var set = new ChangeSet
        {
            TenantId = tenantId,
            Status = ChangeSetStatus.Draft,
            Source = ChangeSetSource.Human,
            BaseRevision = baseRevision,
            CreatedByIssuer = author.Issuer,
            CreatedBySubject = author.Subject,
            CreatedAt = now,
            UpdatedAt = now
        };
        for (var ordinal = 0; ordinal < list.Count; ordinal++)
            set._items.Add(list[ordinal].Attach(tenantId, ordinal));
        set.ContentHash = ComputeContentHash(set._items);
        return set;
    }

    public static bool CanMove(ChangeSetStatus from, ChangeSetStatus to) => Allowed[from].Contains(to);

    public void Validate() => MoveTo(ChangeSetStatus.Validated);
    public void SubmitForApproval() => MoveTo(ChangeSetStatus.AwaitingApproval);
    public void Approve() => MoveTo(ChangeSetStatus.Approved);
    public void Reject() => MoveTo(ChangeSetStatus.Rejected);
    public void Discard() => MoveTo(ChangeSetStatus.Discarded);
    public void Supersede() => MoveTo(ChangeSetStatus.Superseded);

    public void Publish(long newRevision)
    {
        MoveTo(ChangeSetStatus.Published);
        PublishedRevision = newRevision;
    }

    public void BeginActivation() => MoveTo(ChangeSetStatus.Activating);
    public void CompleteActivation() => MoveTo(ChangeSetStatus.Active);

    public void FailActivation(string reason)
    {
        MoveTo(ChangeSetStatus.ActivationFailed);
        FailureReason = reason.Length > 200 ? reason[..200] : reason;
    }

    private void MoveTo(ChangeSetStatus next)
    {
        if (!CanMove(Status, next))
            throw new InvalidOperationException($"A change set cannot move from {Status} to {next}.");

        _transitions.Add(new ChangeSetTransition(Status, next));
        Status = next;
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    /// <summary>SHA-256 over the canonical JSON of the items — independent of ids, authors, timestamps and base revision, so the
    /// same logical change always hashes the same ("is this the set that was approved?").</summary>
    public static string ComputeContentHash(IEnumerable<ChangeSetItem> items)
    {
        var canonical = new JsonArray(items.OrderBy(item => item.Ordinal).Select(item => (JsonNode?)new JsonObject
        {
            ["kind"] = item.TargetKind,
            ["operation"] = item.Operation.ToString(),
            ["targetId"] = item.TargetId,
            ["payload"] = JsonNode.Parse(item.PayloadJson)
        }).ToArray());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web))))).ToLowerInvariant();
    }
}
