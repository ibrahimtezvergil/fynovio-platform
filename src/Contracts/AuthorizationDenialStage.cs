namespace Contracts;

/// <summary>Which evaluation stage produced a `Deny` — meaningless on an `Allow` decision
/// (stays `None`). Lets a consuming PEP externally collapse a `Record`-stage denial into
/// the same "not found" shape as a genuinely missing resource (tenant non-leak rule)
/// while a `Coarse`-stage denial stays a visible `403` — without CRM (or any other
/// consuming module) re-deriving the distinction itself from resource/owner comparisons.
/// See docs/architecture-analysis/2026-09-19-crm-phase2-authorization-delta-and-entry-stage-resolution.md.</summary>
public enum AuthorizationDenialStage
{
    /// <summary>Not a denial (`Effect == Allow`), or a denial whose stage a caller
    /// hasn't distinguished (e.g. a test double) — never asserted to mean "record-level."</summary>
    None,

    /// <summary>The action isn't registered, the principal isn't recognized, or the
    /// actor holds zero grants for this action at all — no resource-specific fact was
    /// ever consulted.</summary>
    Coarse,

    /// <summary>The actor holds at least one grant for this action, but none of them
    /// cover this specific resource (e.g. an owner-relation grant whose owner doesn't
    /// match).</summary>
    Record
}
