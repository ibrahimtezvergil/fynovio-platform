namespace CRM.Application;

/// <summary>Thrown both by the pre-check (expectedVersion already known stale at load
/// time) and by the DbUpdateConcurrencyException catch (staleness discovered only at
/// commit time, i.e. a genuine race) — same error to the caller either way, per
/// architecture plan §13's idempotency×concurrency matrix scenario D.</summary>
public sealed class OpportunityConcurrencyConflictException : InvalidOperationException
{
    public OpportunityConcurrencyConflictException(long opportunityId, long expectedVersion)
        : base($"Opportunity {opportunityId} was not at expected version {expectedVersion}.")
    {
    }
}
