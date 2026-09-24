namespace CRM.Domain;

public enum OpportunityCreationMode
{
    Form,
    Wizard
}

public enum AssignmentMode
{
    Manual,
    DefaultPrincipal,
    Team,
    Territory
}

public enum AssignmentPolicy
{
    AnyAssignablePrincipal,
    ManagerOnly,
    ManualOnly
}

public enum ConfigurationStatus
{
    Active,
    Inactive,
    Archived
}
