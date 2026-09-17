namespace Contracts;

public readonly record struct AuthorizationRequest
{
    public ActorContext Actor { get; }
    public ActionKey Action { get; }
    public ResourceDescriptor Resource { get; }

    public AuthorizationRequest(ActorContext actor, ActionKey action, ResourceDescriptor resource)
    {
        Actor = actor;
        Action = action;
        Resource = resource;
    }
}
