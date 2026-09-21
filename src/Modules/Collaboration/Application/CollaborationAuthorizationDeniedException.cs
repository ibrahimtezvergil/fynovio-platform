namespace Collaboration.Application;

public sealed class CollaborationAuthorizationDeniedException(string actionKey) : UnauthorizedAccessException($"Authorization denied for {actionKey}.");
