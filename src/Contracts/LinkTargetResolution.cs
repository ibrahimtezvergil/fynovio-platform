namespace Contracts;

/// <summary>What an actor may learn about a record another module owns, when it is referenced from a link.
/// `Unavailable` is deliberately a single, payload-free case: a target that does not exist, belongs to another
/// tenant, is denied by authorization, is merged away without a survivor, or could not be read right now must be
/// indistinguishable to the caller — a link must never become an existence oracle. Closed shape: an adapter that
/// meets an unrecognized future case must treat it as `Unavailable` (fail-closed).</summary>
public abstract record LinkTargetResolution
{
    private LinkTargetResolution() { }

    /// <summary>The actor is authorized to see the target. `Label` is a short display string, `Subtitle` optional
    /// secondary text; both were computed for THIS actor at read time and must never be stored.</summary>
    public sealed record Accessible : LinkTargetResolution
    {
        public string Label { get; }
        public string? Subtitle { get; }

        public Accessible(string label, string? subtitle = null)
        {
            if (string.IsNullOrWhiteSpace(label))
                throw new ArgumentException("Label is required.", nameof(label));

            Label = label;
            Subtitle = string.IsNullOrWhiteSpace(subtitle) ? null : subtitle;
        }
    }

    /// <summary>Nothing may be shown or navigated to. Any two instances are equal.</summary>
    public sealed record Unavailable : LinkTargetResolution
    {
        public static Unavailable Instance { get; } = new();
    }
}
