using Contracts;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CRM.Activity;

/// <summary>Projects every published opportunity fact into <c>crm.opportunity_activity</c> (E-2 (a)). Replay-safe, so it
/// starts from the beginning. Writes only the projection and the inbox — no aggregate, no PDP-gated command (E-5).</summary>
public sealed class OpportunityActivityConsumer(CrmDbContext context) : IEventConsumer
{
    public const string ConsumerName = "crm.opportunity-activity";
    private const string Prefix = "enterprise.crmsales.opportunity.";
    private const string Suffix = ".v1";

    /// <summary>Every opportunity fact CRM publishes. Line add/cancel write evidence but no outbox row, so they are not
    /// facts and do not appear here.</summary>
    public static readonly IReadOnlySet<string> Kinds = new HashSet<string>
    {
        "created", "opened", "stage_changed", "moved_pipeline", "reassigned", "won", "lost", "archived", "restored", "custom_fields_changed"
    };

    public string Name => ConsumerName;
    public IReadOnlySet<string> EventTypes { get; } = Kinds.Select(kind => Prefix + kind + Suffix).ToHashSet();
    public ConsumerStartPolicy StartPolicy => ConsumerStartPolicy.FromBeginning;

    public async Task HandleAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        var kind = envelope.EventType[Prefix.Length..^Suffix.Length];

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SetTenantContextAsync(envelope.TenantId, cancellationToken);

        if (await context.ConsumedEvents.AnyAsync(e => e.Consumer == ConsumerName && e.EventId == envelope.EventId, cancellationToken))
            return;

        context.ConsumedEvents.Add(ConsumedEvent.Record(ConsumerName, envelope));
        context.OpportunityActivity.Add(OpportunityActivityEntry.From(envelope, kind));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Another worker applied it between the check and the insert (an expired lease reclaimed): already done.
            return;
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
