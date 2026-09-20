using Host.Endpoints;

namespace Host.Authentication;

public static class CsrfEndpointExtensions
{
    /// <summary>Applies <see cref="CsrfOriginGuard"/> to the endpoint: custom header + Origin/Referer allow-list
    /// + Fetch metadata. Any failure is a uniform 403 `csrf_rejected`.</summary>
    public static TBuilder RequireCsrfProtection<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(async (invocation, next) =>
        {
            var options = invocation.HttpContext.RequestServices.GetRequiredService<AuthenticationHostOptions>();
            return CsrfOriginGuard.ValidateRequest(invocation.HttpContext, options.AllowedOrigins)
                ? await next(invocation)
                : AuthProblems.CsrfRejected();
        });
}
