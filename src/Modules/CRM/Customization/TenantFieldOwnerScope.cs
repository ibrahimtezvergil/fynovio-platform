namespace CRM.Customization;

/// <summary>Doc 15 §6: every tier-1 record carries its owner scope. `Network` is added together with the
/// Organization-module resolver that evaluates it, not reserved empty today.</summary>
public enum TenantFieldOwnerScope
{
    Tenant
}
