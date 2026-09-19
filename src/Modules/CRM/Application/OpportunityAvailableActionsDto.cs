namespace CRM.Application;

/// <summary>Plain booleans only — Phase 1.5 has no obligation/approval/masking outcome
/// to collapse (architecture plan §1/§13A). If Phase 1.5 ever gains one, this shape
/// must grow to a richer per-action outcome; flagged here so a future implementer
/// doesn't mistake this for a permanent design choice.</summary>
public sealed record OpportunityAvailableActionsDto(
    bool CanOpen,
    bool CanChangeStage,
    IReadOnlyList<long> AllowedTargetStageIds,
    bool CanWin,
    bool CanLose,
    bool CanReassign);
