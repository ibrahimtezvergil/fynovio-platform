namespace Collaboration.Application;

/// <summary>A link could not be resolved for this actor. Unknown type, missing target, another tenant's target and an
/// unauthorized target are all this one failure — the message never says which (no existence oracle).</summary>
public sealed class CalendarLinkTargetUnavailableException()
    : Exception("The link target is unavailable.");
