using Access.Persistence;
using Contracts;

namespace Access.Application.Authentication;

/// <summary>What the current actor may do in the active tenant, for the UI to show or hide controls.
/// Advisory only: every action is authorised again, server-side, when it is actually attempted.</summary>
public sealed record Capabilities(bool CanInviteMembers);

/// <summary>Answers `GET /auth/me` capabilities through the same PDP the actions themselves use — there is
/// no second, UI-only permission model to drift.</summary>
public sealed class GetCapabilitiesHandler
{
    private readonly AccessDbContext _context;
    private readonly IAuthorizer _authorizer;

    public GetCapabilitiesHandler(AccessDbContext context, IAuthorizer authorizer)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    }

    public async Task<Capabilities> HandleAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        // The authorizer reads RLS-protected tables: the tenant GUC is transaction-local, so the
        // decision has to be taken inside its own explicit transaction.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(actor.TenantId, cancellationToken);

        var decision = await _authorizer.AuthorizeAsync(
            new AuthorizationRequest(
                actor,
                new ActionKey(CreateInvitationHandler.ActionKeyValue),
                new ResourceDescriptor("Identity.TenantMembership", null, null)),
            cancellationToken);

        await transaction.RollbackAsync(cancellationToken);
        return new Capabilities(decision.IsAllowed);
    }
}
