# Graph Report - fynovio-platform  (2026-09-18)

## Corpus Check
- 273 files · ~170,079 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2268 nodes · 3843 edges · 148 communities (125 shown, 22 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 294 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d66d681a`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- OpportunityMoneyTests
- TenantFieldDefinition
- PipelineDefinitionVersion
- OutboxMessage
- IEntityTypeConfiguration
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- Contracts.csproj
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
- Worker
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
- MasterData.Persistence
- OpportunityNeed
- .NewDraftOpportunity
- ActorContext
- CRM Target Model — Phase 0 Delta Plan
- Enterprise Access Foundation — Phase 1.5 Execution Plan
- IdempotencyRecord
- Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review
- AddPipelineTables
- Design notes
- Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- PartyRef
- EntityRef
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- GrantRoleAssignmentHandler.cs
- .CreateAdminContext
- FixOpportunityAssignedPrincipalIndex
- RenameOpportunityLifecycle
- PartyRelationship
- PartyExternalIdentity
- BackfillMasterDataParties
- OutboxMessage
- Design notes
- MasterDataDbContext
- Contracts
- ModuleBoundaryTests
- DropOpportunityPartyForeignKey
- .HandleAsync
- DropCrmParties
- FixCancelExpiryCheck
- PostgresFixture
- OutboxMessage
- PHASE 2 — CRM Opportunity Commands & API — Planning Document
- IdempotencyRecord
- .Create
- CRM.Tests.csproj
- EnableRowLevelSecurity
- RemoveCrmPartyEntity
- 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)
- EvidenceRecord
- .Owner_relation_grant_is_consistent_across_both_contracts
- AccessAuthorizer
- ActionKey
- CRM.Domain
- InitialMasterDataSchema
- Enterprise Access Foundation — Owner Decisions Final Closure
- TenantAccessState
- PHASE 2 — CRM Opportunity Commands & API
- .HandleAsync
- PermissionSet
- CRM Phase 2 — Opportunity Commands & API Implementation Plan
- Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)
- 6. Current Repo Deltas
- CRM.Persistence.Configurations
- OpportunityStatus
- LegacyMigrationFixture
- 2. Repository Current State (doğrulanmış)
- 12. Phase 1.5 Real Scope
- 8. Target Data Model (Phase 1.5 minimum)
- MasterData.Tests
- PipelineDefinition
- 10. Query Authorization Strategy
- .Initialize
- TenantId
- AccessActionCatalog.cs
- 17. Final Recommendation
- TenantId
- AccessDbContext
- .Create
- TenantId
- EnableAccessRowLevelSecurity
- ExternalIdentity
- TenantId
- MigrationBuilder
- Access.Domain.Authorization
- MasterData.Application
- Guid
- Migration
- ModelBuilder
- ModuleBoundaryTests
- .GivenGrantAsync
- Access.Tests.csproj
- .Rejects_malformed_keys
- PrincipalRef
- Access.Persistence
- Party
- ArgumentException
- ArgumentOutOfRangeException
- Access.Domain.Identity
- InvalidOperationException
- MasterData
- EnableRowLevelSecurityOnPipelineTables
- InlineData
- CRM.Persistence
- CLAUDE PHASE 2 — EXECUTION PROMPT
- Theory
- PostgreSqlContainer
- PipelineStage
- Access.csproj
- CRM.csproj
- Worker.csproj
- AuthorizationEffect
- .SetTenantContextAsync

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 120 edges
2. `Opportunity` - 46 edges
3. `AccessDbContext` - 46 edges
4. `PrincipalRef` - 45 edges
5. `PHASE 2 — CRM Opportunity Commands & API` - 33 edges
6. `CrmDbContext` - 32 edges
7. `Access.Domain.Authorization` - 31 edges
8. `CRM.Domain` - 29 edges
9. `CRM Phase 2 — Opportunity Commands & API Implementation Plan` - 28 edges
10. `MasterDataDbContext` - 28 edges

## Surprising Connections (you probably didn't know these)
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/MasterData.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/CRM.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `Opportunity` --implements--> `IHasRowVersion`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/IHasRowVersion.cs
- `Opportunity` --references--> `PrincipalRef`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/PrincipalRef.cs
- `Opportunity` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Domain/OpportunityLine.cs

## Import Cycles
- None detected.

## Communities (148 total, 22 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.07
Nodes (27): Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CreatedAt, Currency, CustomFields, EstimatedAmount (+19 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.09
Nodes (19): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+11 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - "OpportunityMoneyTests"
Cohesion: 0.44
Nodes (3): OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (21): CRM.Customization, TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName (+13 more)

### Community 5 - "PipelineDefinitionVersion"
Cohesion: 0.16
Nodes (12): ArgumentOutOfRangeException, TenantId, PipelineDefinitionVersion, CreatedAt, Id, PipelineDefinitionId, Stages, TenantId (+4 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "IEntityTypeConfiguration"
Cohesion: 0.15
Nodes (11): IEntityTypeConfiguration, Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt (+3 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.12
Nodes (16): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+8 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.17
Nodes (8): Microsoft.NET.Sdk.Web, net10.0, Microsoft.NET.Sdk, net10.0, net10.0, Microsoft.NET.Sdk, net10.0, Microsoft.NET.Sdk

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
Cohesion: 0.11
Nodes (18): CRM.Evidence, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail (+10 more)

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
Cohesion: 0.08
Nodes (23): PrincipalType, User, RoleAssignment, AccountId, GrantedByAccountId, Id, PrincipalType, Reason (+15 more)

### Community 23 - "What You Must Do When Invoked"
Cohesion: 0.08
Nodes (24): For /graphify add and --watch, For /graphify query, For the commit hook and native CLAUDE.md integration, For --update and --cluster-only, /graphify, Honesty Rules, Interpreter guard for subcommands, Part A - Structural extraction for code files (+16 more)

### Community 24 - "Worker"
Cohesion: 0.22
Nodes (6): BackgroundService, Worker, ILogger, CancellationToken, Task, Worker

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
Cohesion: 0.05
Nodes (29): DbContextEventData, IDesignTimeDbContextFactory, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, MembershipStatus, Active (+21 more)

### Community 39 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+8 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.08
Nodes (24): ArgumentException, InvalidOperationException, Opportunity, OpportunityStateMachineTests, Fact, PipelineDefinitionVersionTests, Fact, InvalidOperationException (+16 more)

### Community 41 - ".HandleAsync"
Cohesion: 0.14
Nodes (13): PartyType, Organization, Person, CreatedPartyPayload, Guid, CreatePartyCommand, CancellationToken, Task (+5 more)

### Community 42 - "MasterData.Persistence"
Cohesion: 0.14
Nodes (8): MasterData.Idempotency, MasterData.Persistence, MasterData.Tests.Integration, MasterData.Outbox, MasterData.Evidence, ICollectionFixture, PostgresCollection, PostgresCollection

### Community 43 - "OpportunityNeed"
Cohesion: 0.22
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

### Community 44 - ".NewDraftOpportunity"
Cohesion: 0.49
Nodes (3): OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 45 - "ActorContext"
Cohesion: 0.12
Nodes (17): AccessScope, All, AnyOf, None, IReadOnlyList, ActorContext, CorrelationId, Principal (+9 more)

### Community 46 - "CRM Target Model — Phase 0 Delta Plan"
Cohesion: 0.13
Nodes (14): 0. Context and the decision this plan corrects, 10. Explicitly out of scope for this document, 11. Decisions recorded (2026-09-16, Party/MasterData reconciliation), 12. Revised dependency graph (2026-09-16, after §5.A/B/D/E), 1. Source-of-truth documents, 2. Current-state recap, 3. Decision matrix — current → target → migration strategy → compatibility risk, 4. Sales module re-introduction — concrete shape (+6 more)

### Community 47 - "Enterprise Access Foundation — Phase 1.5 Execution Plan"
Cohesion: 0.04
Nodes (42): Definition of Done, Enterprise Access Foundation — Phase 1.5 Execution Plan, Task 0: Pre-flight — confirm the five source documents, Task 10: Docs and CI sync, Task 1: Contracts — authorization primitives, Task 2: Access domain model rebuild, Task 3: Access persistence configurations + schema migration, Task 4: Access RLS + runtime role grants (+34 more)

### Community 48 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+8 more)

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

### Community 54 - "PartyRef"
Cohesion: 0.09
Nodes (24): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, IPartyIdentityResolver, CancellationToken, Task (+16 more)

### Community 55 - "EntityRef"
Cohesion: 0.14
Nodes (9): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version (+1 more)

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - "GrantRoleAssignmentHandler.cs"
Cohesion: 0.21
Nodes (3): Access.Evidence, Access.Outbox, Access.Idempotency

### Community 59 - ".CreateAdminContext"
Cohesion: 0.20
Nodes (13): IAsyncLifetime, PartyId, PostgreSqlContainer, Task, PostgresFixture, AdminConnectionString, DbUpdateException, Fact (+5 more)

### Community 60 - "FixOpportunityAssignedPrincipalIndex"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

### Community 61 - "RenameOpportunityLifecycle"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RenameOpportunityLifecycle, DateTimeOffset, Guid, ModelBuilder

### Community 62 - "PartyRelationship"
Cohesion: 0.07
Nodes (29): DateTimeOffset, TenantId, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle (+21 more)

### Community 63 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (12): DateTimeOffset, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId, Provider (+4 more)

### Community 64 - "BackfillMasterDataParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, BackfillMasterDataParties, DateTimeOffset, Guid, ModelBuilder

### Community 65 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.10
Nodes (15): DbContext, MasterDataConnectionString, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages (+7 more)

### Community 68 - "Contracts"
Cohesion: 0.18
Nodes (5): MasterData.Persistence.Configurations, MasterData.Tests.Domain, Contracts, MasterData.Domain, CRM.Tests

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - ".HandleAsync"
Cohesion: 0.28
Nodes (9): BootstrapTenantAccessCommand, Guid, BootstrapTenantAccessHandler, CancellationToken, Task, BootstrapTenantAccessHandlerTests, Fact, InvalidOperationException (+1 more)

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "FixCancelExpiryCheck"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - "PostgresFixture"
Cohesion: 0.07
Nodes (38): CRM.Application, CompletedPayload, CompleteOpportunityCommand, Guid, CompleteOpportunityHandler, CancellationToken, Task, TimeSpan (+30 more)

### Community 75 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 76 - "PHASE 2 — CRM Opportunity Commands & API — Planning Document"
Cohesion: 0.04
Nodes (46): 0. How to read this document, 10. CustomerNeed impact assessment, 11. Persistence / migration impact (Phase 2), 12. Legacy compatibility / migration impact, 12A. Event / outbox catalog (Phase 2 events), 13. Idempotency × concurrency behavior matrix, 14. Authorization evaluation sequence mapped to actual Phase 1.5 components, 15. Consistent error model (+38 more)

### Community 77 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+8 more)

### Community 78 - ".Create"
Cohesion: 0.07
Nodes (35): Role, Id, Key, Name, Origin, TenantId, TenantId, RoleConfiguration (+27 more)

### Community 79 - "CRM.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 80 - "EnableRowLevelSecurity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder

### Community 81 - "RemoveCrmPartyEntity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RemoveCrmPartyEntity, DateTimeOffset, Guid, ModelBuilder

### Community 82 - "3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)"
Cohesion: 0.07
Nodes (28): Acceptance criteria, CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan, Task 0: Pre-flight design decisions — read and confirm before Task 3, Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`, Task 2: Pipeline definition/version/stage tables (additive), Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover, Task 4: Docs, CI, memory sync, 1. Domain katmanı (`tests/CRM.Tests/Domain/`) (+20 more)

### Community 83 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+9 more)

### Community 84 - ".Owner_relation_grant_is_consistent_across_both_contracts"
Cohesion: 0.29
Nodes (6): IClassFixture, OwnedBy, AuthorizeResolveAccessScopeEquivalenceTests, InlineData, Task, Theory

### Community 85 - "AccessAuthorizer"
Cohesion: 0.14
Nodes (14): AuthorizationDecision, DecisionId, Effect, IsAllowed, ReasonCode, Revision, Guid, AccessAuthorizer (+6 more)

### Community 86 - "ActionKey"
Cohesion: 0.17
Nodes (8): GeneratedRegex, Regex, ActionKey, Value, CancellationToken, Task, CancellationToken, Task

### Community 87 - "CRM.Domain"
Cohesion: 0.14
Nodes (8): CRM.Tests.Domain, CRM.Tests.Integration, CRM.Domain, OpportunityPipelineFieldsTests, Fact, OpportunityConcurrencyTests, PipelineDefinitionPersistenceTests, PostgresFixture

### Community 88 - "InitialMasterDataSchema"
Cohesion: 0.05
Nodes (26): MasterData.Persistence.Migrations, ModelSnapshot, AccessDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder, CrmDbContextModelSnapshot, DateTimeOffset (+18 more)

### Community 89 - "Enterprise Access Foundation — Owner Decisions Final Closure"
Cohesion: 0.25
Nodes (8): 1. Owner Decisions Closed, 2. Repository Impact (yalnızca liste — implementasyon yok), 3. Frozen Invariants, 4. Remaining Owner Decisions, 5. Final Architecture Gate, Decision A — System Catalog, Decision B — Opportunity Owner, Enterprise Access Foundation — Owner Decisions Final Closure

### Community 90 - "TenantAccessState"
Cohesion: 0.20
Nodes (7): TenantAccessState, Revision, RowVersion, TenantId, TenantId, TenantAccessStateConfiguration, EntityTypeBuilder

### Community 91 - "PHASE 2 — CRM Opportunity Commands & API"
Cohesion: 0.06
Nodes (33): 0. FIRST RULE — INSPECT BEFORE CHANGING, 0A. REFERENCE PRECEDENCE + CONFLICT HANDLING — BINDING, 10. WON / LOST SEMANTICS, 11. CUSTOMER NEED — DO NOT DESTROY OR IGNORE, 11A. UPDATE OPPORTUNITY CAPABILITY, 12. API DESIGN, 13. QUERY SIDE, 13A. AVAILABLE ACTIONS / AVAILABLE TRANSITIONS — BINDING (+25 more)

### Community 92 - ".HandleAsync"
Cohesion: 0.17
Nodes (9): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, ResolveOrCreatePartyResult, Fact, Task (+1 more)

### Community 93 - "PermissionSet"
Cohesion: 0.05
Nodes (36): ActionRegistryEntry, ActionKey, IsDeprecated, OwnerModule, ResourceType, RiskClass, PermissionSet, Id (+28 more)

### Community 94 - "CRM Phase 2 — Opportunity Commands & API Implementation Plan"
Cohesion: 0.07
Nodes (28): CRM Phase 2 — Opportunity Commands & API Implementation Plan, File Structure, Scope note — one implementation sub-decision this plan makes, flagged rather than silently assumed, Self-review (performed before handing this plan over), Task 10: `AddOpportunityLineCommand` / `AddOpportunityLineHandler`, Task 11: `CancelOpportunityLineCommand` / `CancelOpportunityLineHandler`, Task 12: `LoseOpportunityCommand` / `LoseOpportunityHandler`, Task 13: `Opportunity.Reassign()` domain method (+20 more)

### Community 95 - "Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)"
Cohesion: 0.29
Nodes (7): 1. Executive Verdict, 2. Round 1 Bulgularının PDF'e Yansıması, 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı, 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler), 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde), 6. Final Recommendation, Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

### Community 96 - "6. Current Repo Deltas"
Cohesion: 0.29
Nodes (7): 6.1 Identity — logical vs physical (çelişki değil, kayıt), 6.2 `roles.tenant_id` nullable → target NOT NULL, 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da, 6.4 `AssignedPrincipal` mutasyonsuz, 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz, 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı), 6. Current Repo Deltas

### Community 97 - "CRM.Persistence.Configurations"
Cohesion: 0.29
Nodes (3): CRM.Persistence.Configurations, PipelineDefinitionVersionConfiguration, EntityTypeBuilder

### Community 98 - "OpportunityStatus"
Cohesion: 0.21
Nodes (7): OpportunityStatus, Draft, Lost, Open, Won, OpportunityConfiguration, EntityTypeBuilder

### Community 99 - "LegacyMigrationFixture"
Cohesion: 0.14
Nodes (15): CrmDbContext, IMigrator, InlineData, MasterDataDbContext, OpportunityStatus, PostgreSqlContainer, LegacyDataMigrationTests, Fact (+7 more)

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
Cohesion: 0.18
Nodes (11): PipelineDefinition, CreatedAt, Id, Name, UpdatedAt, Versions, DateTimeOffset, IReadOnlyCollection (+3 more)

### Community 106 - "10. Query Authorization Strategy"
Cohesion: 0.67
Nodes (3): 10.1 Seçenekler, 10.2 Öneri: residual AST + domain adapter, 10. Query Authorization Strategy

### Community 109 - "AccessActionCatalog.cs"
Cohesion: 0.67
Nodes (3): AccessActionCatalog, ActionRegistryDescriptor, IReadOnlyList

### Community 110 - "17. Final Recommendation"
Cohesion: 0.67
Nodes (3): 17. Final Recommendation, Kaynaklar, OLD → NEW

### Community 112 - "AccessDbContext"
Cohesion: 0.06
Nodes (57): Admin, AdminAssignmentId, MasterData.Tests, Exception, GrantedPayload, Grantee, RevokedPayload, TenantId (+49 more)

### Community 113 - ".Create"
Cohesion: 0.39
Nodes (4): PermissionSetTests, ArgumentException, Fact, InvalidOperationException

### Community 115 - "EnableAccessRowLevelSecurity"
Cohesion: 0.10
Nodes (13): Access.Persistence.Migrations, Schema, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder, MigrationBuilder (+5 more)

### Community 116 - "ExternalIdentity"
Cohesion: 0.18
Nodes (11): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+3 more)

### Community 119 - "Access.Domain.Authorization"
Cohesion: 0.16
Nodes (3): Access.Persistence.Configurations, Access.Domain.Authorization, Access.Tests.Domain

### Community 120 - "MasterData.Application"
Cohesion: 0.10
Nodes (16): MasterData.Application, CreatePartyResult, IdempotencyKeyReusedException, MergedPartyPayload, Guid, MergePartyCommand, CancellationToken, Task (+8 more)

### Community 122 - "Migration"
Cohesion: 0.18
Nodes (8): Migration, DateTimeOffset, Guid, MigrationBuilder, RebuildAccessAuthorizationModel, DateTimeOffset, Guid, ModelBuilder

### Community 124 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): Access.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 125 - ".GivenGrantAsync"
Cohesion: 0.24
Nodes (14): Authorizer, ScopeResolver, Action, Actor, IActionCatalog, AccessActionCatalogService, AccessScopeResolver, PrincipalResolver (+6 more)

### Community 126 - "Access.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 127 - ".Rejects_malformed_keys"
Cohesion: 0.38
Nodes (4): ActionKeyTests, ArgumentException, InlineData, Theory

### Community 128 - "PrincipalRef"
Cohesion: 0.22
Nodes (14): All, None, AuthorizationRequest, Resource, PrincipalRef, Issuer, Subject, ResourceDescriptor (+6 more)

### Community 129 - "Access.Persistence"
Cohesion: 0.20
Nodes (3): Access.Application, Access.Tests.Application, Access.Persistence

### Community 130 - "Party"
Cohesion: 0.08
Nodes (23): DateTimeOffset, PartyType, TenantId, Party, CreatedAt, Email, Id, MergedIntoPartyId (+15 more)

### Community 135 - "MasterData"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 136 - "EnableRowLevelSecurityOnPipelineTables"
Cohesion: 0.22
Nodes (5): Guid, MigrationBuilder, ModelBuilder, EnableRowLevelSecurityOnPipelineTables, DateTimeOffset

### Community 138 - "CRM.Persistence"
Cohesion: 0.18
Nodes (5): CRM.Idempotency, CRM.Outbox, CRM.Persistence, CrmConnectionString, CrmDbContextFactory

### Community 139 - "CLAUDE PHASE 2 — EXECUTION PROMPT"
Cohesion: 0.18
Nodes (10): CLAUDE PHASE 2 — EXECUTION PROMPT, Critical architecture constraints, Final console response, Mandatory references, Open-decision discipline, PHASE 2 READINESS, Repository hygiene, Required output (+2 more)

### Community 142 - "PipelineStage"
Cohesion: 0.20
Nodes (10): PipelineStage, CreatedAt, Id, Name, PipelineDefinitionVersionId, SortOrder, TenantId, DateTimeOffset (+2 more)

### Community 143 - "Access.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 144 - "CRM.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 145 - "Worker.csproj"
Cohesion: 0.50
Nodes (3): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Worker, net10.0

### Community 146 - "AuthorizationEffect"
Cohesion: 0.50
Nodes (3): AuthorizationEffect, Allow, Deny

### Community 147 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CrmDbContextTenantExtensions, CancellationToken, Task

## Knowledge Gaps
- **832 isolated node(s):** `Mandatory references`, `Source-of-truth rule`, `Critical architecture constraints`, `Work mode — INSPECT FIRST, PLAN ONLY`, `Required output` (+827 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1164 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `Access.Persistence`, `CustomerNeed`, `OpportunityLine`, `TenantFieldDefinition`, `Access.Domain.Identity`, `OutboxMessage`, `CRM.Persistence`, `EvidenceRecord`, `AuthorizationEffect`, `.SetTenantContextAsync`, `TenantMembership`, `IdempotencyRecord`, `.HandleAsync`, `MasterData.Persistence`, `OpportunityNeed`, `ActorContext`, `IdempotencyRecord`, `PartyRef`, `EntityRef`, `EvidenceRecord`, `GrantRoleAssignmentHandler.cs`, `PartyRelationship`, `PartyExternalIdentity`, `OutboxMessage`, `MasterDataDbContext`, `PostgresFixture`, `AccessAuthorizer`, `ActionKey`, `CRM.Domain`, `TenantAccessState`, `.HandleAsync`, `CRM.Persistence.Configurations`, `OpportunityStatus`, `TenantId`, `AccessDbContext`, `Access.Domain.Authorization`, `MasterData.Application`, `.Rejects_malformed_keys`?**
  _High betweenness centrality (0.225) - this node is a cross-community bridge._
- **Why does `AccessDbContext` connect `AccessDbContext` to `MasterDataDbContext`, `Access.Domain.Identity`, `TenantMembership`, `.HandleAsync`, `IEntityTypeConfiguration`, `OutboxMessage`, `IdempotencyRecord`, `.Create`, `EvidenceRecord`, `PermissionSet`, `AccessAuthorizer`, `RoleAssignment`, `ExternalIdentity`, `TenantAccessState`, `.GivenGrantAsync`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Persistence` to `BackfillMasterDataParties`, `Access.Persistence`, `ModuleBoundaryTests`, `DropOpportunityPartyForeignKey`, `DropCrmParties`, `FixCancelExpiryCheck`, `CRM.Persistence.Migrations`, `EnableRowLevelSecurity`, `RemoveCrmPartyEntity`, `AddPipelineTables`, `.SetTenantContextAsync`, `CRM.Domain`, `InitialMasterDataSchema`, `FixOpportunityAssignedPrincipalIndex`, `RenameOpportunityLifecycle`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **Are the 14 inferred relationships involving `PrincipalRef` (e.g. with `.HandleAsync()` and `.Additive_grants_across_two_roles_compose_without_replacement()`) actually correct?**
  _`PrincipalRef` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Mandatory references`, `Source-of-truth rule`, `Critical architecture constraints` to the rest of the system?**
  _832 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.07056451612903226 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.09057971014492754 - nodes in this community are weakly interconnected._