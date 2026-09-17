using System.Text.RegularExpressions;

namespace Contracts;

/// <summary>Stable business-action vocabulary key, owned by the module that names it
/// (e.g. `crm.opportunity.win`) and evaluated by Access. Immutable once published —
/// a rename is a deprecate-and-introduce, never an in-place edit (gap-closure §2).</summary>
public readonly partial record struct ActionKey
{
    public string Value { get; }

    public ActionKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Value is required.", nameof(value));
        if (!Format().IsMatch(value))
            throw new ArgumentException(
                "ActionKey must be at least three lowercase dot-separated segments (letters, digits, underscore), e.g. 'crm.opportunity.win' or 'access.role_assignment.grant'.",
                nameof(value));

        Value = value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*){2,}$")]
    private static partial Regex Format();
}
