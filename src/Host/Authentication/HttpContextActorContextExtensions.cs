using Contracts;

namespace Host.Authentication;

public static class HttpContextActorContextExtensions
{
    public static ActorContext GetActorContext(this HttpContext context) =>
        context.Items["ActorContext"] as ActorContext?
            ?? throw new InvalidOperationException(
                "No ActorContext for this request — the request was not authenticated, or ActorContextMiddleware has not run yet.");
}
