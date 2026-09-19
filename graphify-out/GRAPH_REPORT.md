# Graph Report - crm-phase2-opportunity-commands-api  (2026-09-19)

## Corpus Check
- 344 files · ~186,855 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2668 nodes · 5004 edges · 166 communities (156 shown, 9 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 524 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d6517184`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- OpportunityMoneyTests
- TenantFieldDefinition
- PipelineStage
- OutboxMessage
- IEntityTypeConfiguration
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- fynovio-platform.slnx
- CRM Module — Current-State Analysis
- CrmDbContext
- CRM.Persistence.Migrations
- http
- EvidenceRecord
- Worker
- .NET rehberi — Laravel'den gelenler için
- Organization/Class1.cs
- ModuleBoundaryTests
- TenantLifecycle/Class1.cs
- fynovio-platform
- Pilot Enforcement Implementation Plan
- RoleAssignment
- What You Must Do When Invoked
- .A_single_dispatch_pass_marks_every_pending_message_processed
- fynovio-platform — Engineering Contract
- fynovio-platform — Claude Code Notes
- graphify reference: extra exports and benchmark
- AI tooling — architecture record
- graphify reference: query, path, explain
- graphify reference: add a URL and watch a folder
- graphify reference: commit hook and native CLAUDE.md integration
- graphify reference: incremental update and cluster-only
- guard.py
- graphify reference: GitHub clone and cross-repo merge
- graphify reference: transcribe video and audio
- .claude/CLAUDE.md
- extraction-spec.md
- TenantMembership
- IdempotencyRecord
- OpportunityStateMachineTests
- .HandleAsync
- CRM.Application
- PHASE 2 — CRM Opportunity Commands & API — Planning Document
- .NewDraftOpportunity
- .ResolveAsync
- CRM Target Model — Phase 0 Delta Plan
- Enterprise Access Foundation — Phase 1.5 Execution Plan
- IdempotencyRecord
- Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review
- AddPipelineTables
- Design notes
- Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- .ResolveExternalIdentityAsync
- EntityVersion
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- .HandleAsync
- PostgresFixture
- FixOpportunityAssignedPrincipalIndex
- RenameOpportunityLifecycle
- PartyRelationship
- Party
- BackfillMasterDataParties
- OutboxMessage
- Design notes
- MasterDataDbContext
- MasterData.Domain
- ModuleBoundaryTests
- DropOpportunityPartyForeignKey
- .HandleAsync
- DropCrmParties
- FixCancelExpiryCheck
- PostgresFixture
- OutboxMessage
- .Create
- IdempotencyRecord
- .Create
- CRM.Tests.csproj
- Migration
- RemoveCrmPartyEntity
- 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)
- EvidenceRecord
- .Owner_relation_grant_is_consistent_across_both_contracts
- AuthorizationDecision
- ActionKey
- CRM.Tests.Integration
- MasterData.Persistence
- Enterprise Access Foundation — Owner Decisions Final Closure
- TenantAccessState
- PHASE 2 — CRM Opportunity Commands & API
- MasterData.Application
- PermissionSet
- .CreatePartyAsync
- Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)
- 6. Current Repo Deltas
- Contracts
- OpportunityStatus
- LegacyMigrationFixture
- 2. Repository Current State (doğrulanmış)
- 12. Phase 1.5 Real Scope
- 8. Target Data Model (Phase 1.5 minimum)
- MasterData.Tests
- PipelineDefinition
- 10. Query Authorization Strategy
- CRM Phase 2 — Opportunity Commands & API Implementation Plan
- .Create
- AccessActionCatalog.cs
- 17. Final Recommendation
- OpportunityEndpoints.cs
- .HandleAsync
- TenantId
- AccessDbContext
- InitialAccessSchema
- ExternalIdentity
- PipelineDefinitionVersion
- PipelineConstraintTests
- Access.Persistence.Configurations
- .Create
- .SeedAsync
- IAuthorizer
- .CreateAdminContext
- ModuleBoundaryTests
- .GivenGrantAsync
- Access.Tests.csproj
- Access.Tests.Domain
- AuthorizationRequest
- Access.Domain.Authorization
- PartyMergeTests
- PrincipalRef
- PartyExternalIdentity
- .OwnedBy_scope_returns_only_that_principals_opportunities
- .CreateAdminContext
- MasterData
- EnableRowLevelSecurityOnPipelineTables
- .HandleAsync
- CRM.Persistence
- EnableAccessRowLevelSecurity
- .SeedOpenOpportunityAsync
- PermissionSetItem
- ActionRegistryEntry
- .HandleAsync
- Access.Persistence.Migrations
- PartyRef
- CLAUDE PHASE 2 — EXECUTION PROMPT
- .SetTenantContextAsync
- ActorContext
- OpportunityPipelineFieldsTests
- RolePermissionSet
- GetOpportunityQuery
- InitialMasterDataSchema
- Access.csproj
- AddPipelineStageActiveAndEntryFlags
- .ResolveAsync
- Host.Tests.csproj
- .TryHandleAsync
- .BuildModel
- .BuildModel
- OpportunityDto
- CRM.csproj
- .BuildModel
- Access.Tests/TestData.cs
- .BuildTargetModel
- AccessConnectionString.cs

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 164 edges
2. `PrincipalRef` - 68 edges
3. `CRM.Application` - 65 edges
4. `CRM.Domain` - 54 edges
5. `Opportunity` - 54 edges
6. `AccessDbContext` - 47 edges
7. `CrmDbContext` - 46 edges
8. `CRM.Persistence` - 39 edges
9. `AuthorizationRequest` - 34 edges
10. `TenantId` - 33 edges

## Surprising Connections (you probably didn't know these)
- `StubScopeResolver` --references--> `AccessScope`  [EXTRACTED]
  tests/CRM.Tests/StubAuthorizer.cs → src/Contracts/AccessScope.cs
- `LegacyMigrationFixture` --references--> `TenantId`  [EXTRACTED]
  tests/CRM.Tests/Integration/LegacyMigrationFixture.cs → src/Contracts/ActorContext.cs
- `StubAuthorizer` --implements--> `IAuthorizer`  [EXTRACTED]
  tests/CRM.Tests/StubAuthorizer.cs → src/Contracts/IAuthorizer.cs
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/CRM.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `OpportunityEndpointsTests` --references--> `Program`  [EXTRACTED]
  tests/Host.Tests/OpportunityEndpointsTests.cs → src/Host/Program.cs

## Import Cycles
- None detected.

## Communities (166 total, 9 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.06
Nodes (32): CancellationToken, Task, TenantId, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CreatedAt (+24 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.07
Nodes (25): EntityRef, BoundedContext, EntityType, Id, TenantId, OpportunityLine, CancelReason, CreatedAt (+17 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.11
Nodes (17): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+9 more)

### Community 3 - "OpportunityMoneyTests"
Cohesion: 0.44
Nodes (3): OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (20): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+12 more)

### Community 5 - "PipelineStage"
Cohesion: 0.13
Nodes (13): PipelineStage, CreatedAt, Id, IsActive, IsEntry, Name, PipelineDefinitionVersionId, SortOrder (+5 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "IEntityTypeConfiguration"
Cohesion: 0.15
Nodes (11): IEntityTypeConfiguration, Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt (+3 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.11
Nodes (17): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+9 more)

### Community 9 - "fynovio-platform.slnx"
Cohesion: 0.20
Nodes (9): Microsoft.AspNetCore.Authentication.JwtBearer (10.0.4), Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0, net10.0, Microsoft.NET.Sdk, net10.0 (+1 more)

### Community 10 - "CRM Module — Current-State Analysis"
Cohesion: 0.07
Nodes (26): 10. Events and Integration, 11. Test Coverage, 12. Current Scope Summary, 13. Target-vs-Current Comparison, 14. Recommended Next Actions, 1. Executive Summary, 2. CRM File / Module Inventory, 3. Current Domain Model (+18 more)

### Community 11 - "CrmDbContext"
Cohesion: 0.12
Nodes (15): CrmDbContext, CustomerNeeds, EvidenceRecords, IdempotencyRecords, Opportunities, OpportunityLines, OpportunityNeeds, OutboxMessages (+7 more)

### Community 12 - "CRM.Persistence.Migrations"
Cohesion: 0.18
Nodes (8): CRM.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, InitialCrmSchema, DateTimeOffset, Guid, ModelBuilder

### Community 13 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+9 more)

### Community 15 - "Worker"
Cohesion: 0.25
Nodes (7): DOTNET_ENVIRONMENT, profiles, Worker, $schema, commandName, dotnetRunMessages, environmentVariables

### Community 16 - ".NET rehberi — Laravel'den gelenler için"
Cohesion: 0.13
Nodes (14): 1. Solution ve proje kavramı, 2. Proje tipleri — bu repoda kullanılan üç farklı "SDK", 3. `Program.cs` — composition root, yani "her şeyin bağlandığı yer", 4. Entity Framework Core — Eloquent'in .NET karşılığı, 5. Migration'lar: `dotnet ef` akışı, 6. Bağlantı dizesi (connection string) nereden geliyor — bugün kurduğumuz yapı, 7. Konfigürasyon katmanları — `.env` yerine ne var, 8. Uçtan uca akış — bugün ne oldu (+6 more)

### Community 18 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): MasterData.Tests.Architecture, Assembly, Fact, TestResult, ModuleBoundaryTests

### Community 20 - "fynovio-platform"
Cohesion: 0.20
Nodes (9): Architecture, Documentation for contributors and coding agents, fynovio-platform, Getting started, Installing PostgreSQL locally (macOS / Homebrew), Prerequisites, Repository layout, Runtime role (Row-Level Security) (+1 more)

### Community 21 - "Pilot Enforcement Implementation Plan"
Cohesion: 0.14
Nodes (13): Pilot Enforcement Implementation Plan, Sonraki plan (bu planın kapsamı dışında), Task 0: Bağlayıcı çekirdeği kararlaştır (checkpoint — kod yok), Task 10: Dokümanları kodla senkronla, Task 1: Test projesi ve domain durum makinesi testleri, Task 2: Mimari sınır testi (FF01), Task 3: Testcontainers altyapısı + iptal hatasını gösteren kırmızı test, Task 4: CHECK kısıtını düzelt (migration) (+5 more)

### Community 22 - "RoleAssignment"
Cohesion: 0.10
Nodes (19): RoleAssignment, AccountId, GrantedByAccountId, Id, PrincipalType, Reason, RoleId, RowVersion (+11 more)

### Community 23 - "What You Must Do When Invoked"
Cohesion: 0.08
Nodes (24): For /graphify add and --watch, For /graphify query, For the commit hook and native CLAUDE.md integration, For --update and --cluster-only, /graphify, Honesty Rules, Interpreter guard for subcommands, Part A - Structural extraction for code files (+16 more)

### Community 24 - ".A_single_dispatch_pass_marks_every_pending_message_processed"
Cohesion: 0.09
Nodes (21): BackgroundService, IServiceProvider, IServiceScope, IServiceScopeFactory, CancellationToken, ILogger, IServiceScopeFactory, Task (+13 more)

### Community 25 - "fynovio-platform — Engineering Contract"
Cohesion: 0.14
Nodes (13): Architecture Rules (binding — enforced by fitness functions, doc 12), Code Conventions, Commands, Cross-Agent / Multi-Agent Policy, Database Rules, Enforcement Scope (approved 2026-09-16), Formatting & Tooling, fynovio-platform — Engineering Contract (+5 more)

### Community 26 - "fynovio-platform — Claude Code Notes"
Cohesion: 0.20
Nodes (9): fynovio-platform — Claude Code Notes, graphify, Memory, Methodology routing, Output style, Plan checkbox tracking (`docs/plans/*.md`), Reasoning effort, Repository retrieval hierarchy (+1 more)

### Community 27 - "graphify reference: extra exports and benchmark"
Cohesion: 0.22
Nodes (8): graphify reference: extra exports and benchmark, Step 6b - Wiki (only if --wiki flag), Step 7 - Neo4j export (only if --neo4j or --neo4j-push flag), Step 7a - FalkorDB export (only if --falkordb or --falkordb-push flag), Step 7b - SVG export (only if --svg flag), Step 7c - GraphML export (only if --graphml flag), Step 7d - MCP server (only if --mcp flag), Step 8 - Token reduction benchmark (only if total_words > 5000)

### Community 28 - "AI tooling — architecture record"
Cohesion: 0.22
Nodes (8): AI tooling — architecture record, CORE — always active, Cross-agent, EXPERIMENTAL — deferred, not installed, Explicitly not introduced, LAZY / ON-DEMAND — present but not always active, OUTSIDE THE CODING STACK, REFERENCE ONLY — inspected, not adopted wholesale

### Community 29 - "graphify reference: query, path, explain"
Cohesion: 0.33
Nodes (5): For /graphify explain, For /graphify path, graphify reference: query, path, explain, Step 0 — Constrained query expansion (REQUIRED before traversal), Step 1 — Traversal

### Community 30 - "graphify reference: add a URL and watch a folder"
Cohesion: 0.50
Nodes (3): For /graphify add, For --watch, graphify reference: add a URL and watch a folder

### Community 31 - "graphify reference: commit hook and native CLAUDE.md integration"
Cohesion: 0.50
Nodes (3): For git commit hook, For native CLAUDE.md integration, graphify reference: commit hook and native CLAUDE.md integration

### Community 32 - "graphify reference: incremental update and cluster-only"
Cohesion: 0.50
Nodes (3): For --cluster-only, For --update (incremental re-extraction), graphify reference: incremental update and cluster-only

### Community 38 - "TenantMembership"
Cohesion: 0.06
Nodes (26): DbContextEventData, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, MembershipStatus, Active, Disabled (+18 more)

### Community 39 - "IdempotencyRecord"
Cohesion: 0.09
Nodes (19): MasterData.Idempotency, MasterData.Outbox, MasterData.Evidence, DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt (+11 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.19
Nodes (5): OpportunityStateMachineTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException

### Community 41 - ".HandleAsync"
Cohesion: 0.13
Nodes (14): PartyType, Organization, Person, CreatedPartyPayload, Guid, CreatePartyCommand, CancellationToken, Task (+6 more)

### Community 42 - "CRM.Application"
Cohesion: 0.04
Nodes (22): CRM.Application, AddedLinePayload, AddOpportunityLineResult, CanceledLinePayload, CancelOpportunityLineResult, ChangePipelineStageResult, CreatedPayload, CreateOpportunityResult (+14 more)

### Community 43 - "PHASE 2 — CRM Opportunity Commands & API — Planning Document"
Cohesion: 0.04
Nodes (46): 0. How to read this document, 10. CustomerNeed impact assessment, 11. Persistence / migration impact (Phase 2), 12. Legacy compatibility / migration impact, 12A. Event / outbox catalog (Phase 2 events), 13. Idempotency × concurrency behavior matrix, 14. Authorization evaluation sequence mapped to actual Phase 1.5 components, 15. Consistent error model (+38 more)

### Community 44 - ".NewDraftOpportunity"
Cohesion: 0.49
Nodes (3): OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 45 - ".ResolveAsync"
Cohesion: 0.21
Nodes (10): AccessScope, All, AnyOf, None, IReadOnlyList, OwnedBy, ScopeTerm, ActionKey (+2 more)

### Community 46 - "CRM Target Model — Phase 0 Delta Plan"
Cohesion: 0.13
Nodes (14): 0. Context and the decision this plan corrects, 10. Explicitly out of scope for this document, 11. Decisions recorded (2026-09-16, Party/MasterData reconciliation), 12. Revised dependency graph (2026-09-16, after §5.A/B/D/E), 1. Source-of-truth documents, 2. Current-state recap, 3. Decision matrix — current → target → migration strategy → compatibility risk, 4. Sales module re-introduction — concrete shape (+6 more)

### Community 47 - "Enterprise Access Foundation — Phase 1.5 Execution Plan"
Cohesion: 0.04
Nodes (42): Definition of Done, Enterprise Access Foundation — Phase 1.5 Execution Plan, Task 0: Pre-flight — confirm the five source documents, Task 10: Docs and CI sync, Task 1: Contracts — authorization primitives, Task 2: Access domain model rebuild, Task 3: Access persistence configurations + schema migration, Task 4: Access RLS + runtime role grants (+34 more)

### Community 48 - "IdempotencyRecord"
Cohesion: 0.13
Nodes (15): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+7 more)

### Community 49 - "Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review"
Cohesion: 0.15
Nodes (13): 11. Versioning / Audit / Evidence, 13. Freeze Decisions (owner onayı gerekli), 14. YAGNI / Overengineering Risks, 15. Migration Risks, 16. "How This Architecture Could Fail", 1. Executive Verdict, 3. Benchmark Corrections, 4. Architecture Decisions Review (+5 more)

### Community 50 - "AddPipelineTables"
Cohesion: 0.20
Nodes (6): DateTimeOffset, MigrationBuilder, AddPipelineTables, DateTimeOffset, Guid, ModelBuilder

### Community 51 - "Design notes"
Cohesion: 0.13
Nodes (14): Account is the one deliberate exception to mandatory `tenant_id`, Concurrency token vs. `TenantAccessState.Revision` — two different counters, never conflated, Design notes, Diagram, Identity + Access schema (PostgreSQL, `identity`/`access` schemas), Namespace layout, Revision 4, Not yet designed here, `PrincipalRef` mapping (+6 more)

### Community 52 - "Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure"
Cohesion: 0.17
Nodes (12): 10. Final Gate, 1. Final Verdict, 2. Closure Matrix, 3. Final Ownership Matrix, 4. Final Authorization Command Pipeline, 5. Final Query Authorization Invariant, 7. Owner Decisions Still Open, 8. Final Freeze List (+4 more)

### Community 53 - "MasterData / Party Foundation — Phase 0.5 Execution Plan"
Cohesion: 0.14
Nodes (13): Kabul kriterleri (bu planın "bitti" demesi için), MasterData / Party Foundation — Phase 0.5 Execution Plan, Task 0: MasterData projesini iskeletten gerçek modüle çevir, Task 10: CI + dokümanları senkronla, Task 1: Contracts — PartyRef, PartyType, PartyDirectoryEntry, IPartyDirectory, IPartyIdentityResolver, Task 2: Domain — Party, PartyRelationship, PartyExternalIdentity + merge invariant tests, Task 3: Mimari sınır testi, Task 4: Outbox / Idempotency / Evidence (CRM'in şeklinin birebir tekrarı) (+5 more)

### Community 54 - ".ResolveExternalIdentityAsync"
Cohesion: 0.29
Nodes (7): IPartyIdentityResolver, CancellationToken, Task, CancellationToken, PartyRef, Task, PartyIdentityResolver

### Community 55 - "EntityVersion"
Cohesion: 0.40
Nodes (3): EntityVersion, Entity, Version

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - ".HandleAsync"
Cohesion: 0.08
Nodes (26): InvalidOperationException, CancellationToken, Task, CancellationToken, DbUpdateException, Task, CancellationToken, DbUpdateException (+18 more)

### Community 59 - "PostgresFixture"
Cohesion: 0.21
Nodes (12): PartyId, PostgreSqlContainer, Task, PostgresFixture, AdminConnectionString, DbUpdateException, Fact, InvalidOperationException (+4 more)

### Community 60 - "FixOpportunityAssignedPrincipalIndex"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

### Community 61 - "RenameOpportunityLifecycle"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RenameOpportunityLifecycle, DateTimeOffset, Guid, ModelBuilder

### Community 62 - "PartyRelationship"
Cohesion: 0.07
Nodes (29): DateTimeOffset, TenantId, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle (+21 more)

### Community 63 - "Party"
Cohesion: 0.12
Nodes (15): DateTimeOffset, Party, CreatedAt, Email, Id, MergedIntoPartyId, Name, PartyType (+7 more)

### Community 64 - "BackfillMasterDataParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, BackfillMasterDataParties, DateTimeOffset, Guid, ModelBuilder

### Community 65 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.12
Nodes (13): DbContext, IDesignTimeDbContextFactory, CrmDbContextFactory, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords (+5 more)

### Community 68 - "MasterData.Domain"
Cohesion: 0.10
Nodes (9): MasterData.Persistence.Configurations, MasterData.Tests.Domain, MasterData.Tests.Integration, MasterData.Domain, ICollectionFixture, PartyExternalIdentityConfiguration, PostgresCollection, PostgresCollection (+1 more)

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - ".HandleAsync"
Cohesion: 0.16
Nodes (15): IEnumerable, CancellationToken, Task, BootstrapTenantAccessCommand, Guid, BootstrapTenantAccessHandler, CancellationToken, Task (+7 more)

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "FixCancelExpiryCheck"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - "PostgresFixture"
Cohesion: 0.11
Nodes (14): OpportunityConcurrencyTests, DbUpdateConcurrencyException, Fact, Task, PartyBackfillVerificationTests, Fact, Task, PipelineDefinitionPersistenceTests (+6 more)

### Community 75 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 76 - ".Create"
Cohesion: 0.22
Nodes (9): IClassFixture, RoleTests, ArgumentException, Fact, AccessConstraintTests, DbUpdateConcurrencyException, DbUpdateException, Fact (+1 more)

### Community 77 - "IdempotencyRecord"
Cohesion: 0.11
Nodes (16): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+8 more)

### Community 78 - ".Create"
Cohesion: 0.08
Nodes (26): GetPipelineStagesHandler, CancellationToken, IReadOnlyList, Task, PipelineStageDto, StageId, GetPipelineStagesHandlerTests, Fact (+18 more)

### Community 79 - "CRM.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 80 - "Migration"
Cohesion: 0.20
Nodes (6): Migration, MigrationBuilder, EnableRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder

### Community 81 - "RemoveCrmPartyEntity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RemoveCrmPartyEntity, DateTimeOffset, Guid, ModelBuilder

### Community 82 - "3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)"
Cohesion: 0.07
Nodes (28): Acceptance criteria, CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan, Task 0: Pre-flight design decisions — read and confirm before Task 3, Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`, Task 2: Pipeline definition/version/stage tables (additive), Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover, Task 4: Docs, CI, memory sync, 1. Domain katmanı (`tests/CRM.Tests/Domain/`) (+20 more)

### Community 83 - "EvidenceRecord"
Cohesion: 0.11
Nodes (17): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+9 more)

### Community 84 - ".Owner_relation_grant_is_consistent_across_both_contracts"
Cohesion: 0.29
Nodes (5): AuthorizeResolveAccessScopeEquivalenceTests, InlineData, OwnedBy, Task, Theory

### Community 85 - "AuthorizationDecision"
Cohesion: 0.17
Nodes (11): AuthorizationDecision, DecisionId, Effect, IsAllowed, ReasonCode, Revision, Guid, CancellationToken (+3 more)

### Community 86 - "ActionKey"
Cohesion: 0.17
Nodes (8): GeneratedRegex, Regex, ActionKey, Value, CancellationToken, Task, CancellationToken, Task

### Community 87 - "CRM.Tests.Integration"
Cohesion: 0.16
Nodes (3): CRM.Tests.Application, CRM.Tests.Integration, CRM.Tests

### Community 88 - "MasterData.Persistence"
Cohesion: 0.11
Nodes (11): MasterData.Persistence, MasterData.Persistence.Migrations, MasterDataConnectionString, CancellationToken, Task, MasterDataDbContextTenantExtensions, MigrationBuilder, DateTimeOffset (+3 more)

### Community 89 - "Enterprise Access Foundation — Owner Decisions Final Closure"
Cohesion: 0.25
Nodes (8): 1. Owner Decisions Closed, 2. Repository Impact (yalnızca liste — implementasyon yok), 3. Frozen Invariants, 4. Remaining Owner Decisions, 5. Final Architecture Gate, Decision A — System Catalog, Decision B — Opportunity Owner, Enterprise Access Foundation — Owner Decisions Final Closure

### Community 90 - "TenantAccessState"
Cohesion: 0.20
Nodes (7): TenantAccessState, Revision, RowVersion, TenantId, TenantId, TenantAccessStateConfiguration, EntityTypeBuilder

### Community 91 - "PHASE 2 — CRM Opportunity Commands & API"
Cohesion: 0.06
Nodes (33): 0. FIRST RULE — INSPECT BEFORE CHANGING, 0A. REFERENCE PRECEDENCE + CONFLICT HANDLING — BINDING, 10. WON / LOST SEMANTICS, 11. CUSTOMER NEED — DO NOT DESTROY OR IGNORE, 11A. UPDATE OPPORTUNITY CAPABILITY, 12. API DESIGN, 13. QUERY SIDE, 13A. AVAILABLE ACTIONS / AVAILABLE TRANSITIONS — BINDING (+25 more)

### Community 92 - "MasterData.Application"
Cohesion: 0.09
Nodes (16): MasterData.Application, CreatePartyResult, MergedPartyPayload, CancellationToken, Task, MergePartyResult, PartyNotFoundException, Guid (+8 more)

### Community 93 - "PermissionSet"
Cohesion: 0.11
Nodes (16): PermissionSet, Id, Items, Key, Name, Origin, TenantId, IReadOnlyCollection (+8 more)

### Community 94 - ".CreatePartyAsync"
Cohesion: 0.13
Nodes (21): AddOpportunityLineHandler, TimeSpan, CancelOpportunityLineHandler, TimeSpan, CreateOpportunityHandler, TimeSpan, AddOpportunityLineHandlerTests, Fact (+13 more)

### Community 95 - "Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)"
Cohesion: 0.29
Nodes (7): 1. Executive Verdict, 2. Round 1 Bulgularının PDF'e Yansıması, 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı, 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler), 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde), 6. Final Recommendation, Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

### Community 96 - "6. Current Repo Deltas"
Cohesion: 0.29
Nodes (7): 6.1 Identity — logical vs physical (çelişki değil, kayıt), 6.2 `roles.tenant_id` nullable → target NOT NULL, 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da, 6.4 `AssignedPrincipal` mutasyonsuz, 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz, 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı), 6. Current Repo Deltas

### Community 97 - "Contracts"
Cohesion: 0.13
Nodes (5): CRM.Tests.Domain, MasterData.Tests, CRM.Persistence.Configurations, Contracts, CRM.Domain

### Community 98 - "OpportunityStatus"
Cohesion: 0.24
Nodes (8): ListOpportunitiesRequest, OpportunityStatus, Draft, Lost, Open, Won, OpportunityConfiguration, EntityTypeBuilder

### Community 99 - "LegacyMigrationFixture"
Cohesion: 0.08
Nodes (22): Host.Tests, IAsyncLifetime, IMigrator, TenantId, Value, Program, LegacyDataMigrationTests, Fact (+14 more)

### Community 100 - "2. Repository Current State (doğrulanmış)"
Cohesion: 0.40
Nodes (5): 2.1 Access modülü — dosya düzeyi, 2.2 Contracts düzeyi, 2.3 Bağımlılık gerçekliği, 2.4 PDF ↔ repo çelişkileri, 2. Repository Current State (doğrulanmış)

### Community 102 - "12. Phase 1.5 Real Scope"
Cohesion: 0.50
Nodes (4): 12. Phase 1.5 Real Scope, DESIGN / FREEZE NOW, IMPLEMENT LATER, DO NOT BUILD YET, IMPLEMENT NOW

### Community 103 - "8. Target Data Model (Phase 1.5 minimum)"
Cohesion: 0.50
Nodes (4): 8.1 System vs tenant katalog: öneri (owner kararı), 8.2 Tablolar, 8.3 Özel soruların cevapları, 8. Target Data Model (Phase 1.5 minimum)

### Community 104 - "MasterData.Tests"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 105 - "PipelineDefinition"
Cohesion: 0.15
Nodes (13): PipelineDefinition, CreatedAt, Id, Name, TenantId, UpdatedAt, Versions, DateTimeOffset (+5 more)

### Community 106 - "10. Query Authorization Strategy"
Cohesion: 0.67
Nodes (3): 10.1 Seçenekler, 10.2 Öneri: residual AST + domain adapter, 10. Query Authorization Strategy

### Community 107 - "CRM Phase 2 — Opportunity Commands & API Implementation Plan"
Cohesion: 0.07
Nodes (28): CRM Phase 2 — Opportunity Commands & API Implementation Plan, File Structure, Scope note — one implementation sub-decision this plan makes, flagged rather than silently assumed, Self-review (performed before handing this plan over), Task 10: `AddOpportunityLineCommand` / `AddOpportunityLineHandler`, Task 11: `CancelOpportunityLineCommand` / `CancelOpportunityLineHandler`, Task 12: `LoseOpportunityCommand` / `LoseOpportunityHandler`, Task 13: `Opportunity.Reassign()` domain method (+20 more)

### Community 108 - ".Create"
Cohesion: 0.15
Nodes (16): OpenOpportunityHandler, TimeSpan, ReassignOpportunityCommand, Guid, ReassignOpportunityHandler, DbUpdateException, TimeSpan, OpenOpportunityHandlerTests (+8 more)

### Community 109 - "AccessActionCatalog.cs"
Cohesion: 0.67
Nodes (3): AccessActionCatalog, ActionRegistryDescriptor, IReadOnlyList

### Community 110 - "17. Final Recommendation"
Cohesion: 0.67
Nodes (3): 17. Final Recommendation, Kaynaklar, OLD → NEW

### Community 111 - "OpportunityEndpoints.cs"
Cohesion: 0.08
Nodes (20): Host.Authentication, Host.Endpoints, RequestDelegate, ActorContextMiddleware, HttpContext, HttpContextActorContextExtensions, JwtOptions, Audience (+12 more)

### Community 112 - ".HandleAsync"
Cohesion: 0.21
Nodes (17): Admin, AdminAssignmentId, GrantedPayload, Grantee, AuthorizationDeniedException, GrantRoleAssignmentCommand, GrantRoleAssignmentResult, Guid (+9 more)

### Community 113 - "TenantId"
Cohesion: 0.10
Nodes (18): IEndpointRouteBuilder, TenantId, AddOpportunityLineCommand, Guid, CancellationToken, DbUpdateException, Task, CancelOpportunityLineCommand (+10 more)

### Community 114 - "AccessDbContext"
Cohesion: 0.08
Nodes (20): AccessDbContext, Accounts, Actions, EvidenceRecords, ExternalIdentities, IdempotencyRecords, OutboxMessages, PermissionSetItems (+12 more)

### Community 115 - "InitialAccessSchema"
Cohesion: 0.22
Nodes (5): DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder

### Community 116 - "ExternalIdentity"
Cohesion: 0.15
Nodes (11): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+3 more)

### Community 117 - "PipelineDefinitionVersion"
Cohesion: 0.14
Nodes (13): PipelineDefinitionVersion, CreatedAt, Id, PipelineDefinitionId, Stages, TenantId, VersionNumber, DateTimeOffset (+5 more)

### Community 118 - "PipelineConstraintTests"
Cohesion: 0.28
Nodes (7): PipelineConstraintTests, DbUpdateException, DefinitionId, Fact, PostgresException, Task, VersionId

### Community 119 - "Access.Persistence.Configurations"
Cohesion: 0.10
Nodes (14): Access.Persistence.Configurations, PrincipalType, User, Role, Id, Key, Name, Origin (+6 more)

### Community 120 - ".Create"
Cohesion: 0.15
Nodes (15): Guid, MergePartyCommand, TimeSpan, MergePartyHandler, PartyType, TenantId, ArgumentException, Fact (+7 more)

### Community 121 - ".SeedAsync"
Cohesion: 0.22
Nodes (12): EntryStageId, InactiveStageId, OtherActiveStageId, ChangePipelineStageCommand, Guid, ChangePipelineStageHandler, TimeSpan, InvalidPipelineTransitionException (+4 more)

### Community 122 - "IAuthorizer"
Cohesion: 0.20
Nodes (12): EntryStage, OtherStage, IAuthorizer, GetOpportunityAvailableActionsHandler, GetOpportunityAvailableActionsQuery, Guid, DenyingAuthorizer, GetOpportunityAvailableActionsHandlerTests (+4 more)

### Community 123 - ".CreateAdminContext"
Cohesion: 0.29
Nodes (8): AccessRlsTests, DbUpdateException, Fact, Task, PostgresFixture, AdminConnectionString, PostgreSqlContainer, Task

### Community 124 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): Access.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 125 - ".GivenGrantAsync"
Cohesion: 0.22
Nodes (15): Authorizer, ScopeResolver, Action, Actor, IActionCatalog, AccessActionCatalogService, AccessAuthorizer, AccessScopeResolver (+7 more)

### Community 126 - "Access.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 127 - "Access.Tests.Domain"
Cohesion: 0.16
Nodes (7): Access.Tests.Domain, ActionKeyTests, ArgumentException, InlineData, Theory, TenantAccessStateTests, Fact

### Community 128 - "AuthorizationRequest"
Cohesion: 0.22
Nodes (11): All, None, AuthorizationRequest, Resource, ResourceDescriptor, Id, OwnerPrincipal, ResourceType (+3 more)

### Community 129 - "Access.Domain.Authorization"
Cohesion: 0.15
Nodes (10): Access.Evidence, Access.Application, Access.Tests.Application, Access.Outbox, Access.Persistence, Access.Idempotency, Access.Domain.Identity, Access.Domain.Authorization (+2 more)

### Community 130 - "PartyMergeTests"
Cohesion: 0.53
Nodes (3): Fact, InvalidOperationException, PartyMergeTests

### Community 131 - "PrincipalRef"
Cohesion: 0.15
Nodes (10): PrincipalRef, Issuer, Subject, HttpContext, Task, CancellationToken, Task, TenantId (+2 more)

### Community 132 - "PartyExternalIdentity"
Cohesion: 0.13
Nodes (12): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+4 more)

### Community 133 - ".OwnedBy_scope_returns_only_that_principals_opportunities"
Cohesion: 0.27
Nodes (10): IAccessScopeResolver, ListOpportunitiesHandler, ListOpportunitiesQuery, Guid, TenantId, ListOpportunitiesHandlerTests, Fact, Task (+2 more)

### Community 134 - ".CreateAdminContext"
Cohesion: 0.47
Nodes (6): WinOpportunityHandler, TimeSpan, WinOpportunityHandlerTests, Fact, Task, TenantId

### Community 135 - "MasterData"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 136 - "EnableRowLevelSecurityOnPipelineTables"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurityOnPipelineTables, DateTimeOffset, Guid, ModelBuilder

### Community 137 - ".HandleAsync"
Cohesion: 0.19
Nodes (11): Exception, RevokedPayload, IdempotencyKeyReusedException, RevokeRoleAssignmentCommand, RevokeRoleAssignmentResult, Guid, RevokedPayload, RevokeRoleAssignmentHandler (+3 more)

### Community 138 - "CRM.Persistence"
Cohesion: 0.12
Nodes (7): CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox, Worker, CRM.Persistence, CrmConnectionString

### Community 139 - "EnableAccessRowLevelSecurity"
Cohesion: 0.22
Nodes (7): Schema, MigrationBuilder, EnableAccessRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder, Table

### Community 140 - ".SeedOpenOpportunityAsync"
Cohesion: 0.29
Nodes (9): RowVersion, LoseOpportunityCommand, Guid, LoseOpportunityHandler, TimeSpan, LoseOpportunityHandlerTests, Fact, Task (+1 more)

### Community 141 - "PermissionSetItem"
Cohesion: 0.18
Nodes (9): PermissionSetItem, ActionKey, Id, PermissionSetId, Relation, TenantId, TenantId, PermissionSetItemConfiguration (+1 more)

### Community 142 - "ActionRegistryEntry"
Cohesion: 0.17
Nodes (8): ActionRegistryEntry, ActionKey, IsDeprecated, OwnerModule, ResourceType, RiskClass, ActionRegistryEntryConfiguration, EntityTypeBuilder

### Community 143 - ".HandleAsync"
Cohesion: 0.17
Nodes (9): AnyOf, IQueryable, CancellationToken, Task, CancellationToken, IReadOnlyList, OwnedBy, Task (+1 more)

### Community 144 - "Access.Persistence.Migrations"
Cohesion: 0.22
Nodes (5): Access.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, RebuildAccessAuthorizationModel

### Community 145 - "PartyRef"
Cohesion: 0.15
Nodes (14): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, PartyDirectoryEntry, PartyRef, PartyId (+6 more)

### Community 146 - "CLAUDE PHASE 2 — EXECUTION PROMPT"
Cohesion: 0.18
Nodes (10): CLAUDE PHASE 2 — EXECUTION PROMPT, Critical architecture constraints, Final console response, Mandatory references, Open-decision discipline, PHASE 2 READINESS, Repository hygiene, Required output (+2 more)

### Community 147 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CrmDbContextTenantExtensions, CancellationToken, Task

### Community 148 - "ActorContext"
Cohesion: 0.18
Nodes (8): ActorContext, CorrelationId, Principal, Guid, CancellationToken, Task, CancellationToken, Task

### Community 149 - "OpportunityPipelineFieldsTests"
Cohesion: 0.46
Nodes (3): OpportunityPipelineFieldsTests, ArgumentException, Fact

### Community 150 - "RolePermissionSet"
Cohesion: 0.22
Nodes (7): RolePermissionSet, PermissionSetId, RoleId, TenantId, TenantId, RolePermissionSetConfiguration, EntityTypeBuilder

### Community 151 - "GetOpportunityQuery"
Cohesion: 0.38
Nodes (7): GetOpportunityHandler, GetOpportunityQuery, Guid, GetOpportunityHandlerTests, Fact, Task, TenantId

### Community 152 - "InitialMasterDataSchema"
Cohesion: 0.20
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialMasterDataSchema

### Community 153 - "Access.csproj"
Cohesion: 0.20
Nodes (8): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Worker, net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0

### Community 154 - "AddPipelineStageActiveAndEntryFlags"
Cohesion: 0.22
Nodes (5): MigrationBuilder, AddPipelineStageActiveAndEntryFlags, DateTimeOffset, Guid, ModelBuilder

### Community 155 - ".ResolveAsync"
Cohesion: 0.25
Nodes (6): AuthorizationEffect, Allow, Deny, StubAuthorizer, CancellationToken, Task

### Community 156 - "Host.Tests.csproj"
Cohesion: 0.25
Nodes (7): Microsoft.AspNetCore.Mvc.Testing (10.0.4), net10.0, Microsoft.NET.Test.Sdk (17.14.1), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 157 - ".TryHandleAsync"
Cohesion: 0.29
Nodes (6): IExceptionHandler, CancellationToken, Exception, HttpContext, ValueTask, CrmProblemDetailsExceptionHandler

### Community 158 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, MasterDataDbContextModelSnapshot

### Community 159 - ".BuildModel"
Cohesion: 0.33
Nodes (4): AccessDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 160 - "OpportunityDto"
Cohesion: 0.47
Nodes (4): OpportunityDto, OpportunityLineDto, DateTimeOffset, IReadOnlyList

### Community 161 - "CRM.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 162 - ".BuildModel"
Cohesion: 0.33
Nodes (4): CrmDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 164 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **852 isolated node(s):** `Value`, `Principal`, `CorrelationId`, `Effect`, `ReasonCode` (+847 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1264 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **9 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `AuthorizationRequest`, `OpportunityLine`, `Access.Domain.Authorization`, `PrincipalRef`, `PartyExternalIdentity`, `.OwnedBy_scope_returns_only_that_principals_opportunities`, `.HandleAsync`, `CRM.Persistence`, `PermissionSetItem`, `PartyRef`, `.SetTenantContextAsync`, `ActorContext`, `RoleAssignment`, `RolePermissionSet`, `.ResolveAsync`, `Access.Tests/TestData.cs`, `TenantMembership`, `IdempotencyRecord`, `.HandleAsync`, `CRM.Application`, `.ResolveAsync`, `.ResolveExternalIdentityAsync`, `EntityVersion`, `EvidenceRecord`, `PartyRelationship`, `MasterData.Domain`, `OutboxMessage`, `IdempotencyRecord`, `EvidenceRecord`, `.Owner_relation_grant_is_consistent_across_both_contracts`, `AuthorizationDecision`, `ActionKey`, `CRM.Tests.Integration`, `MasterData.Persistence`, `TenantAccessState`, `MasterData.Application`, `PermissionSet`, `LegacyMigrationFixture`, `OpportunityEndpoints.cs`, `.HandleAsync`, `AccessDbContext`, `ExternalIdentity`, `Access.Persistence.Configurations`, `.Create`, `IAuthorizer`, `.GivenGrantAsync`, `Access.Tests.Domain`?**
  _High betweenness centrality (0.184) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Persistence` to `Access.Domain.Authorization`, `EnableRowLevelSecurityOnPipelineTables`, `CRM.Persistence.Migrations`, `.SetTenantContextAsync`, `AddPipelineStageActiveAndEntryFlags`, `.BuildModel`, `AddPipelineTables`, `FixOpportunityAssignedPrincipalIndex`, `RenameOpportunityLifecycle`, `BackfillMasterDataParties`, `MasterDataDbContext`, `ModuleBoundaryTests`, `DropOpportunityPartyForeignKey`, `DropCrmParties`, `FixCancelExpiryCheck`, `Migration`, `RemoveCrmPartyEntity`, `CRM.Tests.Integration`, `OpportunityEndpoints.cs`?**
  _High betweenness centrality (0.104) - this node is a cross-community bridge._
- **Why does `PrincipalRef` connect `PrincipalRef` to `AuthorizationRequest`, `Opportunity`, `.OwnedBy_scope_returns_only_that_principals_opportunities`, `.HandleAsync`, `.SeedOpenOpportunityAsync`, `ActorContext`, `GetOpportunityQuery`, `IdempotencyRecord`, `OpportunityStateMachineTests`, `.HandleAsync`, `.ResolveAsync`, `EvidenceRecord`, `.HandleAsync`, `.HandleAsync`, `IdempotencyRecord`, `EvidenceRecord`, `.Owner_relation_grant_is_consistent_across_both_contracts`, `.CreatePartyAsync`, `.Create`, `.HandleAsync`, `TenantId`, `ExternalIdentity`, `.Create`, `.SeedAsync`, `IAuthorizer`, `.GivenGrantAsync`?**
  _High betweenness centrality (0.068) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `PrincipalRef` (e.g. with `.InvokeAsync()` and `.MapOpportunityEndpoints()`) actually correct?**
  _`PrincipalRef` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Value`, `Principal`, `CorrelationId` to the rest of the system?**
  _852 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.05897435897435897 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.07459677419354839 - nodes in this community are weakly interconnected._