namespace Access.Application;

public sealed class AuthorizationDeniedException(string action, string reasonCode)
    : Exception($"Action '{action}' was denied: {reasonCode}");
