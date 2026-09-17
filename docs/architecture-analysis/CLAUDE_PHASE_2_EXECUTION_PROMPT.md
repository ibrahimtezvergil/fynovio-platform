# CLAUDE PHASE 2 — EXECUTION PROMPT

You are working inside the Enterprise Business Platform repository.

Your task is to perform the **Phase 2 — CRM Opportunity Commands & API planning pass** with enterprise architecture discipline.

## Mandatory references

Read these BEFORE making any implementation decision:

1. `PHASE_2_OPPORTUNITY_COMMANDS_API_BINDING_SPEC.md` — binding Phase 2 scope and execution contract.
2. `Enterprise_CRM_Target_Model_Binding_Implementation_Specification.pdf` — binding CRM target model.
3. `docs/architecture-analysis/CRM_CURRENT_STATE_ANALYSIS.md`, if present.
4. Phase 0.5 MasterData / Party implementation and its approved decision/report documents.
5. Phase 1 CRM Lifecycle / Pipeline implementation and its approved decision/report documents.
6. Phase 1.5 Enterprise Access / Authorization implementation and its approved decision/report documents.
7. Repository-local instructions such as `CLAUDE.md`, `AGENTS.md`, architecture ADRs and testing conventions.
8. Current CRM code, tests, migrations, persistence configuration and host/runtime configuration.

If a repo-local graph/index tool such as Graphify is already installed/configured by repository instructions, use it according to those instructions for navigation. **Do not install or reconfigure developer tooling as part of Phase 2.**

## Source-of-truth rule

- Actual repository state = source of truth for what is implemented.
- Approved binding architecture/decision documents = source of truth for intended behavior.
- The Phase 2 binding specification = source of truth for this phase's scope, safety constraints, analysis requirements and stop condition.

Do not trust an old plan over current code.
Do not trust an implementation shortcut over a binding architecture decision.

If code and a binding decision conflict, **report the conflict before proposing implementation**.

If a mandatory binding reference is missing, do not infer its contents. Mark the consequence as `BLOCKER` or `OPEN DECISION`. You may continue inspecting everything else that can be verified, but `Ready to implement` must be `NO` if the missing evidence prevents safe implementation.

## Critical architecture constraints

You MUST preserve all constraints in the Phase 2 binding specification, including but not limited to:

- Lifecycle Status is NOT Pipeline Stage.
- Pipeline Stage is tenant-configurable; it is not a hard-coded enum.
- Pipeline stage changes MUST NOT silently become canonical `Won` / `Lost` transitions unless an already-binding decision explicitly says so.
- CRM Opportunity is NOT Quote; future Sales owns Quote/Order semantics.
- Phase 1.5 authorization/policy infrastructure MUST be consumed, not duplicated.
- Authorization is not assumed to be a single boolean gate.
- Where established, preserve coarse capability checks, tenant-safe loads, record-level policy, territory/org/team/ownership scope, Field-Level Security, policy obligations and approval requirements.
- Policy evaluation may depend on both current aggregate state and the proposed mutation.
- Field-level WRITE checks apply to fields actually being changed; field-level READ applies to server-side projections.
- Sensitive fields must not be sent to the frontend merely to be hidden visually.
- `RequireApproval` or other obligations must preserve the Phase 1.5 execution contract; do not invent approval behavior.
- Explicitly determine initial/default pipeline-stage semantics; do not assume sort-order-first.
- Preserve historical Opportunity references to retired/inactive stages according to the established model.
- Analyze post-create Opportunity editing; do not introduce unrestricted generic PATCH semantics.
- Respect bounded-context ownership for CRM, MasterData/Party, Pipeline, Security and future Sales data.
- Idempotency and optimistic concurrency must be designed together.
- Exact idempotent replay must return the original result without re-running the domain command, incrementing version, or duplicating Evidence/Outbox.
- Analyze concurrent requests using the same idempotency key using existing infrastructure; do not invent ad-hoc locks.
- Frontend must not reconstruct backend lifecycle, transition or authorization logic. Analyze authoritative available-actions / allowed-transitions projection where appropriate.
- Available-action representation must not collapse `approval required` or richer policy outcomes into misleading booleans if Phase 1.5 supports richer semantics.
- PostgreSQL RLS, unprivileged runtime role, tenant-safe relationships, optimistic concurrency, Evidence, Outbox and transactional idempotency must remain intact.

## Work mode — INSPECT FIRST, PLAN ONLY

Do **not** implement production code yet.

Inspect the repository in a focused order:

1. repo instructions / architecture index
2. Phase 0.5, Phase 1 and Phase 1.5 decision/report documents
3. current Opportunity aggregate/domain model
4. pipeline model and transition rules
5. authorization/policy/FLS/approval contracts
6. persistence, migrations, RLS and runtime DB role
7. idempotency, Evidence and Outbox infrastructure
8. API conventions, ProblemDetails/error conventions and concurrency conventions
9. CRM tests and Testcontainers integration setup
10. any existing query/projection conventions

For every material planning conclusion, include concrete evidence:

- repository file path
- relevant class/type/module/function
- migration/table/policy/test where applicable
- binding document/decision reference where applicable

Do not produce a generic architecture essay detached from the repository.

## Required output

Create exactly this planning document:

`docs/architecture-analysis/PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md`

It must cover every required item from the binding spec, including the expanded requirements for:

- verified repository state
- dependencies inherited from Phase 0.5 / Phase 1 / Phase 1.5
- Opportunity aggregate assessment
- lifecycle vs pipeline mapping
- initial/default pipeline-stage behavior
- inactive/retired stage behavior
- required commands
- Opportunity update/mutability model
- mutable-field bounded-context ownership classification
- minimum required queries
- available actions / allowed transitions strategy
- proposed HTTP API surface
- authorization evaluation mapped to actual Phase 1.5 components
- field-level READ/WRITE security behavior
- policy obligations / approval integration contract
- idempotency × concurrency matrix, including exact replay and concurrent duplicates
- Evidence and Outbox behavior
- persistence/migration impact
- legacy compatibility/migration impact
- CustomerNeed impact
- consistent error model
- test plan across domain/application/integration/API
- exact files expected to change
- exact files expected to be added
- explicit non-goals
- risks, blockers and OPEN DECISIONS
- recommended implementation sequence

## Open-decision discipline

Do NOT silently decide product/domain semantics that are not already binding.

For each unresolved item, write:

`OPEN DECISION — <name>`

and include:

- issue
- verified current state
- options
- architectural impact
- migration impact
- recommendation
- why implementation must wait for approval

A recommendation is not approval.

## Repository hygiene

Before approval:

- DO NOT modify production code.
- DO NOT create migrations.
- DO NOT implement endpoints.
- DO NOT refactor unrelated code.
- DO NOT install packages/tools.
- DO NOT update lockfiles.
- DO NOT perform repository-wide formatting or auto-fixes that modify production files.
- DO NOT redesign Phase 0.5, Phase 1 or Phase 1.5 unless a concrete contradiction blocks Phase 2.

The intended repository change is only:

`docs/architecture-analysis/PHASE_2_OPPORTUNITY_COMMANDS_API_PLAN.md`

unless an existing repository rule mandates another documentation-only artifact.

## Final console response

After writing the plan, STOP.

Print only a concise review summary using this shape:

### PHASE 2 READINESS

- Existing foundation:
- Verified Phase 1.5 authorization/FLS/policy integration:
- Required commands:
- Required queries:
- Initial pipeline-stage semantics:
- Update/mutability model:
- Idempotency/concurrency contract:
- Migration impact:
- Critical risks/blockers:
- Open decisions:
- Proposed implementation order:
- Ready to implement: YES / NO

Then wait for explicit approval.

Do not continue into implementation, even if the plan says `Ready to implement: YES`.
