using Host.Email;

namespace Host.Endpoints;

/// <summary>Development-only helpers. <see cref="MapDevEndpoints"/> is only called in the Development
/// environment, so these routes do not exist anywhere else (404).</summary>
public static class DevEndpoints
{
    public static void MapDevEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/dev");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            invocation.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(invocation);
        });

        // Every message the app "sent", newest first, with the link and token — lets a developer or an
        // E2E run finish an invitation / password-reset flow without a mail provider.
        group.MapGet("/mailbox", (DevMailbox mailbox, string? to) => Results.Ok(mailbox.List(to))).AllowAnonymous();

        group.MapDelete("/mailbox", (DevMailbox mailbox) =>
        {
            mailbox.Clear();
            return Results.NoContent();
        }).AllowAnonymous();
    }
}
