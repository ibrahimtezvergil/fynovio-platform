# Graph Report - fynovio-platform  (2026-09-17)

## Corpus Check
- 269 files · ~135,832 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2129 nodes · 3711 edges · 150 communities (138 shown, 11 thin omitted)
- Extraction: 92% EXTRACTED · 8% INFERRED · 0% AMBIGUOUS · INFERRED: 288 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `d3d9e086`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- .Create
- TenantFieldDefinition
- PipelineDefinitionVersion
- OutboxMessage
- Account
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
- AccessDbContext
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
- RolePermissionSet
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- .HandleAsync
- .CreateAdminContext
- FixOpportunityAssignedPrincipalIndex
- RenameOpportunityLifecycle
- PartyRelationship
- Party
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
- .Grant
- IdempotencyRecord
- TenantId
- CRM.Tests.csproj
- EnableRowLevelSecurity
- RemoveCrmPartyEntity
- 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)
- EvidenceRecord
- .Owner_relation_grant_is_consistent_across_both_contracts
- AuthorizationDecision
- ActionKey
- CRM.Domain
- InitialMasterDataSchema
- Enterprise Access Foundation — Owner Decisions Final Closure
- TenantAccessState
- .CreateAdminContext
- .HandleAsync
- PermissionSet
- .HandleAsync
- Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)
- 6. Current Repo Deltas
- .CreateContext
- OpportunityStatus
- LegacyMigrationFixture
- 2. Repository Current State (doğrulanmış)
- 12. Phase 1.5 Real Scope
- 8. Target Data Model (Phase 1.5 minimum)
- MasterData.Tests
- PipelineDefinition
- 10. Query Authorization Strategy
- PermissionSetItem
- ActionRegistryEntry
- .SeedOpenOpportunityAsync
- 17. Final Recommendation
- PipelineStage
- .HandleAsync
- .Create
- .Create
- Migration
- ExternalIdentity
- PartyRelationshipConfiguration
- CrmDbContextFactory
- IEntityTypeConfiguration
- PartyExternalIdentity
- .Create
- PostgresCollection
- .SetTenantContextAsync
- ModuleBoundaryTests
- .GivenGrantAsync
- Access.Tests.csproj
- .Rejects_malformed_keys
- PrincipalRef
- Access.Persistence
- .Create
- Role
- .SetTenantContextAsync
- Access.Domain.Authorization
- CRM.csproj
- MasterData
- EnableRowLevelSecurityOnPipelineTables
- .Create
- CompleteOpportunityHandler.cs
- EnableAccessRowLevelSecurity
- AccessDbContextFactory
- Access.csproj
- PartyConfiguration
- MasterDataDbContextFactory
- RoleTests
- .ResolveExternalIdentityAsync_follows_the_merge_chain
- .Configure
- .SetTenantContextAsync
- Worker.csproj
- OpportunityPipelineFieldsTests

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 124 edges
2. `Opportunity` - 48 edges
3. `AccessDbContext` - 46 edges
4. `PrincipalRef` - 45 edges
5. `CrmDbContext` - 33 edges
6. `Access.Domain.Authorization` - 31 edges
7. `CRM.Domain` - 29 edges
8. `MasterDataDbContext` - 29 edges
9. `OpportunityLine` - 27 edges
10. `PartyRelationship` - 24 edges

## Surprising Connections (you probably didn't know these)
- `LegacyMigrationFixture` --references--> `TenantId`  [EXTRACTED]
  tests/CRM.Tests/Integration/LegacyMigrationFixture.cs → src/Contracts/ActorContext.cs
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/CRM.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/MasterData.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `AuthorizationRequest` --references--> `ActionKey`  [EXTRACTED]
  src/Contracts/AuthorizationRequest.cs → src/Contracts/ActionKey.cs
- `ActorContext` --references--> `PrincipalRef`  [EXTRACTED]
  src/Contracts/ActorContext.cs → src/Contracts/PrincipalRef.cs

## Import Cycles
- None detected.

## Communities (150 total, 11 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.07
Nodes (27): Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CreatedAt, Currency, CustomFields, EstimatedAmount (+19 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.05
Nodes (32): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version (+24 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - ".Create"
Cohesion: 0.31
Nodes (5): PartyRef, TenantId, OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (21): CRM.Customization, TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName (+13 more)

### Community 5 - "PipelineDefinitionVersion"
Cohesion: 0.16
Nodes (11): PipelineDefinitionVersion, CreatedAt, Id, PipelineDefinitionId, Stages, TenantId, VersionNumber, DateTimeOffset (+3 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "Account"
Cohesion: 0.15
Nodes (10): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+2 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.12
Nodes (16): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+8 more)

### Community 9 - "fynovio-platform.slnx"
Cohesion: 0.22
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
Cohesion: 0.12
Nodes (16): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+8 more)

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
Cohesion: 0.13
Nodes (13): RoleAssignment, AccountId, GrantedByAccountId, Id, PrincipalType, Reason, RoleId, RowVersion (+5 more)

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
Cohesion: 0.06
Nodes (26): DbContextEventData, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, MembershipStatus, Active, Disabled (+18 more)

### Community 39 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+8 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.22
Nodes (5): OpportunityStateMachineTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException

### Community 41 - ".HandleAsync"
Cohesion: 0.14
Nodes (15): PartyType, Organization, Person, CreatedPartyPayload, Guid, CreatePartyCommand, CancellationToken, Task (+7 more)

### Community 42 - "AccessDbContext"
Cohesion: 0.12
Nodes (16): AccessDbContext, Accounts, Actions, EvidenceRecords, ExternalIdentities, IdempotencyRecords, OutboxMessages, PermissionSetItems (+8 more)

### Community 43 - "OpportunityNeed"
Cohesion: 0.25
Nodes (6): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, EntityTypeBuilder

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

### Community 54 - "PartyRef"
Cohesion: 0.10
Nodes (21): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, IPartyIdentityResolver, CancellationToken, Task (+13 more)

### Community 55 - "RolePermissionSet"
Cohesion: 0.22
Nodes (7): RolePermissionSet, PermissionSetId, RoleId, TenantId, TenantId, RolePermissionSetConfiguration, EntityTypeBuilder

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - ".HandleAsync"
Cohesion: 0.13
Nodes (11): CRM.Application, InvalidOperationException, CompletedPayload, CompleteOpportunityCommand, Guid, CancellationToken, Task, CompleteOpportunityResult (+3 more)

### Community 59 - ".CreateAdminContext"
Cohesion: 0.21
Nodes (12): PartyId, PostgreSqlContainer, Task, PostgresFixture, AdminConnectionString, DbUpdateException, Fact, InvalidOperationException (+4 more)

### Community 60 - "FixOpportunityAssignedPrincipalIndex"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

### Community 61 - "RenameOpportunityLifecycle"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RenameOpportunityLifecycle, DateTimeOffset, Guid, ModelBuilder

### Community 62 - "PartyRelationship"
Cohesion: 0.12
Nodes (16): DateTimeOffset, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle, Metadata (+8 more)

### Community 63 - "Party"
Cohesion: 0.12
Nodes (14): DateTimeOffset, PartyType, TenantId, Party, CreatedAt, Email, Id, MergedIntoPartyId (+6 more)

### Community 64 - "BackfillMasterDataParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, BackfillMasterDataParties, DateTimeOffset, Guid, ModelBuilder

### Community 65 - "OutboxMessage"
Cohesion: 0.10
Nodes (19): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+11 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.18
Nodes (10): DbContext, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties (+2 more)

### Community 68 - "Contracts"
Cohesion: 0.09
Nodes (11): MasterData.Idempotency, MasterData.Persistence.Configurations, MasterData.Application, MasterData.Tests.Domain, MasterData.Persistence, Contracts, MasterData.Tests.Integration, MasterData.Outbox (+3 more)

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - ".HandleAsync"
Cohesion: 0.22
Nodes (11): CancellationToken, Task, BootstrapTenantAccessCommand, Guid, BootstrapTenantAccessHandler, CancellationToken, Task, BootstrapTenantAccessHandlerTests (+3 more)

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "FixCancelExpiryCheck"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - "PostgresFixture"
Cohesion: 0.13
Nodes (13): DbUpdateConcurrencyException, Fact, Task, OpportunityPersistenceTests, Fact, Task, PartyBackfillVerificationTests, Fact (+5 more)

### Community 75 - "OutboxMessage"
Cohesion: 0.09
Nodes (19): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+11 more)

### Community 76 - ".Grant"
Cohesion: 0.27
Nodes (6): TenantId, RoleAssignmentTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException

### Community 77 - "IdempotencyRecord"
Cohesion: 0.13
Nodes (14): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+6 more)

### Community 78 - "TenantId"
Cohesion: 0.17
Nodes (12): MasterData.Tests, TenantId, StageId, PipelineRlsTests, DbUpdateException, DefinitionId, Fact, PostgresException (+4 more)

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
Cohesion: 0.13
Nodes (15): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+7 more)

### Community 84 - ".Owner_relation_grant_is_consistent_across_both_contracts"
Cohesion: 0.25
Nodes (6): IClassFixture, OwnedBy, AuthorizeResolveAccessScopeEquivalenceTests, InlineData, Task, Theory

### Community 85 - "AuthorizationDecision"
Cohesion: 0.11
Nodes (16): AuthorizationDecision, DecisionId, Effect, IsAllowed, ReasonCode, Revision, Guid, AuthorizationEffect (+8 more)

### Community 86 - "ActionKey"
Cohesion: 0.17
Nodes (8): GeneratedRegex, Regex, ActionKey, Value, CancellationToken, Task, CancellationToken, Task

### Community 87 - "CRM.Domain"
Cohesion: 0.18
Nodes (6): CRM.Tests.Domain, CRM.Tests.Integration, CRM.Persistence, CRM.Domain, OpportunityConcurrencyTests, PipelineDefinitionPersistenceTests

### Community 88 - "InitialMasterDataSchema"
Cohesion: 0.05
Nodes (26): MasterData.Persistence.Migrations, ModelSnapshot, AccessDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder, CrmDbContextModelSnapshot, DateTimeOffset (+18 more)

### Community 89 - "Enterprise Access Foundation — Owner Decisions Final Closure"
Cohesion: 0.25
Nodes (8): 1. Owner Decisions Closed, 2. Repository Impact (yalnızca liste — implementasyon yok), 3. Frozen Invariants, 4. Remaining Owner Decisions, 5. Final Architecture Gate, Decision A — System Catalog, Decision B — Opportunity Owner, Enterprise Access Foundation — Owner Decisions Final Closure

### Community 90 - "TenantAccessState"
Cohesion: 0.20
Nodes (7): TenantAccessState, Revision, RowVersion, TenantId, TenantId, TenantAccessStateConfiguration, EntityTypeBuilder

### Community 91 - ".CreateAdminContext"
Cohesion: 0.35
Nodes (7): PipelineConstraintTests, DbUpdateException, DefinitionId, Fact, PostgresException, Task, VersionId

### Community 92 - ".HandleAsync"
Cohesion: 0.22
Nodes (9): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, ResolveOrCreatePartyResult, Fact, Task (+1 more)

### Community 93 - "PermissionSet"
Cohesion: 0.15
Nodes (11): PermissionSet, Id, Items, Key, Name, Origin, TenantId, IReadOnlyCollection (+3 more)

### Community 94 - ".HandleAsync"
Cohesion: 0.18
Nodes (11): MergedPartyPayload, Guid, MergePartyCommand, CancellationToken, Task, TimeSpan, MergePartyHandler, MergePartyResult (+3 more)

### Community 95 - "Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)"
Cohesion: 0.29
Nodes (7): 1. Executive Verdict, 2. Round 1 Bulgularının PDF'e Yansıması, 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı, 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler), 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde), 6. Final Recommendation, Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

### Community 96 - "6. Current Repo Deltas"
Cohesion: 0.29
Nodes (7): 6.1 Identity — logical vs physical (çelişki değil, kayıt), 6.2 `roles.tenant_id` nullable → target NOT NULL, 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da, 6.4 `AssignedPrincipal` mutasyonsuz, 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz, 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı), 6. Current Repo Deltas

### Community 97 - ".CreateContext"
Cohesion: 0.35
Nodes (7): TenantIsolationTests, DbUpdateException, Fact, InvalidOperationException, PostgresException, Task, TenantId

### Community 98 - "OpportunityStatus"
Cohesion: 0.21
Nodes (7): OpportunityStatus, Draft, Lost, Open, Won, OpportunityConfiguration, EntityTypeBuilder

### Community 99 - "LegacyMigrationFixture"
Cohesion: 0.14
Nodes (14): IAsyncLifetime, IMigrator, TenantId, Value, LegacyDataMigrationTests, Fact, InlineData, Task (+6 more)

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
Cohesion: 0.12
Nodes (13): PipelineDefinition, CreatedAt, Id, Name, TenantId, UpdatedAt, Versions, DateTimeOffset (+5 more)

### Community 106 - "10. Query Authorization Strategy"
Cohesion: 0.67
Nodes (3): 10.1 Seçenekler, 10.2 Öneri: residual AST + domain adapter, 10. Query Authorization Strategy

### Community 107 - "PermissionSetItem"
Cohesion: 0.20
Nodes (8): PermissionSetItem, ActionKey, Id, PermissionSetId, Relation, TenantId, TenantId, EntityTypeBuilder

### Community 108 - "ActionRegistryEntry"
Cohesion: 0.17
Nodes (8): ActionRegistryEntry, ActionKey, IsDeprecated, OwnerModule, ResourceType, RiskClass, ActionRegistryEntryConfiguration, EntityTypeBuilder

### Community 109 - ".SeedOpenOpportunityAsync"
Cohesion: 0.40
Nodes (7): CompleteOpportunityHandler, TimeSpan, CompleteOpportunityHandlerTests, Fact, InvalidOperationException, Task, TenantId

### Community 110 - "17. Final Recommendation"
Cohesion: 0.67
Nodes (3): 17. Final Recommendation, Kaynaklar, OLD → NEW

### Community 111 - "PipelineStage"
Cohesion: 0.16
Nodes (10): PipelineStage, CreatedAt, Id, Name, PipelineDefinitionVersionId, SortOrder, TenantId, DateTimeOffset (+2 more)

### Community 112 - ".HandleAsync"
Cohesion: 0.09
Nodes (39): Admin, AdminAssignmentId, Exception, GrantedPayload, Grantee, RevokedPayload, IAuthorizer, CancellationToken (+31 more)

### Community 113 - ".Create"
Cohesion: 0.33
Nodes (4): PermissionSetTests, ArgumentException, Fact, InvalidOperationException

### Community 114 - ".Create"
Cohesion: 0.31
Nodes (5): PipelineDefinitionVersionTests, Fact, InvalidOperationException, Fact, Task

### Community 115 - "Migration"
Cohesion: 0.10
Nodes (14): Access.Persistence.Migrations, Migration, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder, DateTimeOffset (+6 more)

### Community 116 - "ExternalIdentity"
Cohesion: 0.17
Nodes (11): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+3 more)

### Community 117 - "PartyRelationshipConfiguration"
Cohesion: 0.23
Nodes (8): PartyRelationshipStatus, Active, Ended, PartyRelationshipType, BranchOf, WorksFor, EntityTypeBuilder, PartyRelationshipConfiguration

### Community 119 - "IEntityTypeConfiguration"
Cohesion: 0.08
Nodes (17): CRM.Persistence.Configurations, Access.Persistence.Configurations, IEntityTypeConfiguration, IdempotencyRecordConfiguration, EntityTypeBuilder, PermissionSetConfiguration, PermissionSetItemConfiguration, RoleAssignmentConfiguration (+9 more)

### Community 120 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (13): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+5 more)

### Community 121 - ".Create"
Cohesion: 0.42
Nodes (5): AccessConstraintTests, DbUpdateConcurrencyException, DbUpdateException, Fact, Task

### Community 122 - "PostgresCollection"
Cohesion: 0.40
Nodes (3): ICollectionFixture, PostgresCollection, PostgresCollection

### Community 124 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): Access.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 125 - ".GivenGrantAsync"
Cohesion: 0.23
Nodes (15): Authorizer, ScopeResolver, Action, Actor, IActionCatalog, AccessActionCatalogService, AccessAuthorizer, AccessScopeResolver (+7 more)

### Community 126 - "Access.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 127 - ".Rejects_malformed_keys"
Cohesion: 0.47
Nodes (4): ActionKeyTests, ArgumentException, InlineData, Theory

### Community 128 - "PrincipalRef"
Cohesion: 0.18
Nodes (14): All, None, AuthorizationRequest, Resource, PrincipalRef, Issuer, Subject, ResourceDescriptor (+6 more)

### Community 129 - "Access.Persistence"
Cohesion: 0.09
Nodes (14): Access.Evidence, Access.Application, Access.Tests.Application, Access.Outbox, Access.Persistence, Access.Idempotency, AccessActionCatalog, ActionRegistryDescriptor (+6 more)

### Community 130 - ".Create"
Cohesion: 0.28
Nodes (6): Fact, InvalidOperationException, PartyMergeTests, ArgumentException, Fact, PartyTests

### Community 131 - "Role"
Cohesion: 0.20
Nodes (8): Role, Id, Key, Name, Origin, TenantId, TenantId, EntityTypeBuilder

### Community 132 - ".SetTenantContextAsync"
Cohesion: 0.50
Nodes (3): CancellationToken, Task, MasterDataDbContextTenantExtensions

### Community 133 - "Access.Domain.Authorization"
Cohesion: 0.17
Nodes (6): Access.Domain.Identity, Access.Domain.Authorization, Access.Tests.Integration, Access.Tests.Domain, TenantAccessStateTests, Fact

### Community 134 - "CRM.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 135 - "MasterData"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 136 - "EnableRowLevelSecurityOnPipelineTables"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurityOnPipelineTables, DateTimeOffset, Guid, ModelBuilder

### Community 137 - ".Create"
Cohesion: 0.33
Nodes (5): TenantId, ArgumentException, Fact, InvalidOperationException, PartyRelationshipTests

### Community 138 - "CompleteOpportunityHandler.cs"
Cohesion: 0.32
Nodes (3): CRM.Idempotency, CRM.Evidence, CRM.Outbox

### Community 139 - "EnableAccessRowLevelSecurity"
Cohesion: 0.18
Nodes (7): Schema, MigrationBuilder, EnableAccessRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder, Table

### Community 140 - "AccessDbContextFactory"
Cohesion: 0.29
Nodes (3): IDesignTimeDbContextFactory, AccessConnectionString, AccessDbContextFactory

### Community 141 - "Access.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 142 - "PartyConfiguration"
Cohesion: 0.53
Nodes (3): EntityTypeBuilder, PartyType, PartyConfiguration

### Community 144 - "RoleTests"
Cohesion: 0.47
Nodes (3): RoleTests, ArgumentException, Fact

### Community 145 - ".ResolveExternalIdentityAsync_follows_the_merge_chain"
Cohesion: 0.60
Nodes (3): Fact, Task, PartyDirectoryTests

### Community 146 - ".Configure"
Cohesion: 0.40
Nodes (3): PrincipalType, User, EntityTypeBuilder

### Community 147 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CrmDbContextTenantExtensions, CancellationToken, Task

### Community 148 - "Worker.csproj"
Cohesion: 0.50
Nodes (3): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Worker, net10.0

## Knowledge Gaps
- **724 isolated node(s):** `Value`, `Principal`, `CorrelationId`, `Effect`, `ReasonCode` (+719 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1034 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `PrincipalRef`, `OpportunityLine`, `Access.Persistence`, `Role`, `TenantFieldDefinition`, `Access.Domain.Authorization`, `CustomerNeed`, `PipelineDefinitionVersion`, `CompleteOpportunityHandler.cs`, `.SetTenantContextAsync`, `RoleAssignment`, `TenantMembership`, `.HandleAsync`, `OpportunityNeed`, `ActorContext`, `PartyRef`, `RolePermissionSet`, `.HandleAsync`, `OutboxMessage`, `IdempotencyRecord`, `TenantId`, `EvidenceRecord`, `.Owner_relation_grant_is_consistent_across_both_contracts`, `AuthorizationDecision`, `ActionKey`, `CRM.Domain`, `TenantAccessState`, `PermissionSet`, `OpportunityStatus`, `LegacyMigrationFixture`, `PipelineDefinition`, `PermissionSetItem`, `PipelineStage`, `.HandleAsync`, `.Create`, `IEntityTypeConfiguration`?**
  _High betweenness centrality (0.228) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Domain` to `BackfillMasterDataParties`, `Contracts`, `ModuleBoundaryTests`, `DropOpportunityPartyForeignKey`, `DropCrmParties`, `FixCancelExpiryCheck`, `CompleteOpportunityHandler.cs`, `EnableRowLevelSecurityOnPipelineTables`, `CRM.Persistence.Migrations`, `EnableRowLevelSecurity`, `RemoveCrmPartyEntity`, `AddPipelineTables`, `.SetTenantContextAsync`, `CrmDbContextFactory`, `InitialMasterDataSchema`, `FixOpportunityAssignedPrincipalIndex`, `RenameOpportunityLifecycle`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **Why does `AccessDbContext` connect `AccessDbContext` to `Role`, `Access.Domain.Authorization`, `Account`, `AccessDbContextFactory`, `RoleAssignment`, `TenantMembership`, `RolePermissionSet`, `MasterDataDbContext`, `.HandleAsync`, `OutboxMessage`, `IdempotencyRecord`, `EvidenceRecord`, `TenantAccessState`, `PermissionSet`, `PermissionSetItem`, `ActionRegistryEntry`, `.HandleAsync`, `ExternalIdentity`, `.SetTenantContextAsync`, `.GivenGrantAsync`?**
  _High betweenness centrality (0.090) - this node is a cross-community bridge._
- **Are the 14 inferred relationships involving `PrincipalRef` (e.g. with `.HandleAsync()` and `.Additive_grants_across_two_roles_compose_without_replacement()`) actually correct?**
  _`PrincipalRef` has 14 INFERRED edges - model-reasoned connections that need verification._
- **What connects `Value`, `Principal`, `CorrelationId` to the rest of the system?**
  _724 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.07056451612903226 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.051515151515151514 - nodes in this community are weakly interconnected._