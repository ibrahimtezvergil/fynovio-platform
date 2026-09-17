# Graph Report - fynovio-platform  (2026-09-17)

## Corpus Check
- 261 files · ~129,971 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2049 nodes · 3357 edges · 154 communities (138 shown, 14 thin omitted)
- Extraction: 93% EXTRACTED · 7% INFERRED · 0% AMBIGUOUS · INFERRED: 220 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f6cdb89f`
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
- ExternalIdentity
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
- .ResolveAsync
- CRM Target Model — Phase 0 Delta Plan
- Enterprise Access Foundation — Phase 1.5 Execution Plan
- IdempotencyRecord
- Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review
- AddPipelineTables
- Design notes
- Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- .GetPartiesAsync
- RolePermissionSet
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- PostgresFixture
- .CreateAdminContext
- FixOpportunityAssignedPrincipalIndex
- RenameOpportunityLifecycle
- PartyRelationship
- Party
- BackfillMasterDataParties
- OutboxMessage
- Design notes
- MasterDataDbContext
- MasterData.Application
- ModuleBoundaryTests
- DropOpportunityPartyForeignKey
- .ResolveAsync
- DropCrmParties
- FixCancelExpiryCheck
- .Create
- OutboxMessage
- RoleAssignmentTests
- IdempotencyRecord
- PrincipalRef
- CRM.Tests.csproj
- EnableRowLevelSecurity
- RemoveCrmPartyEntity
- CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan
- EvidenceRecord
- Contracts.csproj
- AuthorizationDecision
- ActionKey
- Contracts
- EnableRowLevelSecurity
- Enterprise Access Foundation — Owner Decisions Final Closure
- TenantAccessState
- AccessRlsTests
- .HandleAsync
- PermissionSet
- .HandleAsync
- Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)
- 6. Current Repo Deltas
- EntityRef
- OpportunityStatus
- TenantId
- 2. Repository Current State (doğrulanmış)
- 12. Phase 1.5 Real Scope
- 8. Target Data Model (Phase 1.5 minimum)
- MasterData.Tests
- Role
- 10. Query Authorization Strategy
- IEntityTypeConfiguration
- ActionRegistryEntry
- .ResolveAccountIdAsync
- 17. Final Recommendation
- CRM.Persistence
- .HandleAsync
- PartyRelationshipConfiguration
- Account
- InitialAccessSchema
- .CreatePartyAsync
- PartyRef
- CrmDbContextFactory
- MasterData.Domain
- PartyExternalIdentity
- AccessConstraintTests
- PostgresCollection
- .SetTenantContextAsync
- ModuleBoundaryTests
- Migration
- Access.Tests.csproj
- .Rejects_malformed_keys
- .GivenGrantAsync
- Access.Domain.Authorization
- .Create
- Access.Persistence.Migrations
- .SetTenantContextAsync
- 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)
- Enterprise Access Foundation — Technical Gap Closure (pre-execution)
- MasterData
- .AuthorizeAsync
- .Create
- .BuildTargetModel
- EnableAccessRowLevelSecurity
- Enterprise Access Foundation — Test Senaryoları ve Beklenen Sonuçlar
- ActionKey
- ArgumentException
- ArgumentOutOfRangeException
- 1. Domain katmanı (`tests/Access.Tests/Domain/`)
- .BuildModel
- PartyType
- .BuildModel
- .BuildModel
- PartyConfiguration
- MasterDataDbContextFactory
- 1. Domain katmanı (`tests/CRM.Tests/Domain/`)
- 4. Integration katmanı — gerçek Postgres (`tests/Access.Tests/Integration/`)

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 117 edges
2. `Opportunity` - 47 edges
3. `AccessDbContext` - 41 edges
4. `CrmDbContext` - 32 edges
5. `MasterDataDbContext` - 28 edges
6. `OpportunityLine` - 27 edges
7. `PrincipalRef` - 27 edges
8. `Access.Domain.Authorization` - 25 edges
9. `PartyRelationship` - 24 edges
10. `CRM.Domain` - 24 edges

## Surprising Connections (you probably didn't know these)
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/CRM.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/MasterData.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `Opportunity` --implements--> `IHasRowVersion`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/IHasRowVersion.cs
- `Opportunity` --references--> `PrincipalRef`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/PrincipalRef.cs
- `Opportunity` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Domain/OpportunityLine.cs

## Import Cycles
- None detected.

## Communities (154 total, 14 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.07
Nodes (29): Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CreatedAt, Currency, CustomFields, EstimatedAmount (+21 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.10
Nodes (19): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+11 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.20
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - "OpportunityMoneyTests"
Cohesion: 0.44
Nodes (3): OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (20): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+12 more)

### Community 5 - "PipelineDefinitionVersion"
Cohesion: 0.05
Nodes (40): PipelineDefinition, CreatedAt, Id, Name, TenantId, UpdatedAt, Versions, DateTimeOffset (+32 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "ExternalIdentity"
Cohesion: 0.13
Nodes (12): Access.Domain.Identity, ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims (+4 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.12
Nodes (15): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+7 more)

### Community 9 - "fynovio-platform.slnx"
Cohesion: 0.13
Nodes (14): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker, net10.0, net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3) (+6 more)

### Community 10 - "CRM Module — Current-State Analysis"
Cohesion: 0.07
Nodes (26): 10. Events and Integration, 11. Test Coverage, 12. Current Scope Summary, 13. Target-vs-Current Comparison, 14. Recommended Next Actions, 1. Executive Summary, 2. CRM File / Module Inventory, 3. Current Domain Model (+18 more)

### Community 11 - "CrmDbContext"
Cohesion: 0.11
Nodes (17): CrmDbContext, CustomerNeeds, EvidenceRecords, IdempotencyRecords, Opportunities, OpportunityLines, OpportunityNeeds, OutboxMessages (+9 more)

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
Cohesion: 0.05
Nodes (29): DbContextEventData, IDesignTimeDbContextFactory, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, PrincipalType, User (+21 more)

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
Cohesion: 0.10
Nodes (17): MembershipStatus, Active, Disabled, Invited, TenantMembership, AccountId, DisabledAt, Id (+9 more)

### Community 39 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+8 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.30
Nodes (5): OpportunityStateMachineTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException

### Community 41 - ".HandleAsync"
Cohesion: 0.18
Nodes (11): Guid, CreatePartyCommand, CancellationToken, Task, TimeSpan, CreatePartyHandler, CreatePartyResult, IdempotencyKeyReusedException (+3 more)

### Community 42 - "AccessDbContext"
Cohesion: 0.12
Nodes (16): AccessDbContext, Accounts, Actions, EvidenceRecords, ExternalIdentities, IdempotencyRecords, OutboxMessages, PermissionSetItems (+8 more)

### Community 43 - "OpportunityNeed"
Cohesion: 0.28
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

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
Cohesion: 0.15
Nodes (13): Definition of Done, Enterprise Access Foundation — Phase 1.5 Execution Plan, Task 0: Pre-flight — confirm the five source documents, Task 10: Docs and CI sync, Task 1: Contracts — authorization primitives, Task 2: Access domain model rebuild, Task 3: Access persistence configurations + schema migration, Task 4: Access RLS + runtime role grants (+5 more)

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

### Community 54 - ".GetPartiesAsync"
Cohesion: 0.15
Nodes (14): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, PartyDirectoryEntry, CancellationToken, IReadOnlyCollection (+6 more)

### Community 55 - "RolePermissionSet"
Cohesion: 0.22
Nodes (7): RolePermissionSet, PermissionSetId, RoleId, TenantId, TenantId, RolePermissionSetConfiguration, EntityTypeBuilder

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - "PostgresFixture"
Cohesion: 0.26
Nodes (6): IAsyncLifetime, PostgresFixture, AdminConnectionString, AccessDbContext, PostgreSqlContainer, Task

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
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.18
Nodes (10): DbContext, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties (+2 more)

### Community 68 - "MasterData.Application"
Cohesion: 0.12
Nodes (8): MasterData.Idempotency, MasterData.Application, MasterData.Persistence, MasterData.Tests.Integration, MasterData.Outbox, MasterData.Evidence, MergedPartyPayload, MergePartyResult

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - ".ResolveAsync"
Cohesion: 0.22
Nodes (5): IAccessScopeResolver, CancellationToken, Task, IActionCatalog, AccessScopeResolver

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "FixCancelExpiryCheck"
Cohesion: 0.22
Nodes (5): MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - ".Create"
Cohesion: 0.07
Nodes (38): CRM.Application, InvalidOperationException, CompletedPayload, CompleteOpportunityCommand, Guid, CompleteOpportunityHandler, CancellationToken, Task (+30 more)

### Community 75 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 76 - "RoleAssignmentTests"
Cohesion: 0.25
Nodes (5): ArgumentException, ArgumentOutOfRangeException, RoleAssignmentTests, Fact, InvalidOperationException

### Community 77 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (15): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+7 more)

### Community 78 - "PrincipalRef"
Cohesion: 0.18
Nodes (6): MasterData.Tests, PrincipalRef, Issuer, Subject, TestData, Operator

### Community 79 - "CRM.Tests.csproj"
Cohesion: 0.13
Nodes (13): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1) (+5 more)

### Community 80 - "EnableRowLevelSecurity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder

### Community 81 - "RemoveCrmPartyEntity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RemoveCrmPartyEntity, DateTimeOffset, Guid, ModelBuilder

### Community 82 - "CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan"
Cohesion: 0.29
Nodes (7): Acceptance criteria, CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan, Task 0: Pre-flight design decisions — read and confirm before Task 3, Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`, Task 2: Pipeline definition/version/stage tables (additive), Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover, Task 4: Docs, CI, memory sync

### Community 83 - "EvidenceRecord"
Cohesion: 0.12
Nodes (16): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+8 more)

### Community 84 - "Contracts.csproj"
Cohesion: 0.25
Nodes (4): Access.Tests.Application, Access.Tests.Integration, net10.0, Microsoft.NET.Sdk

### Community 85 - "AuthorizationDecision"
Cohesion: 0.17
Nodes (10): AuthorizationDecision, DecisionId, Effect, IsAllowed, ReasonCode, Revision, Guid, AuthorizationEffect (+2 more)

### Community 86 - "ActionKey"
Cohesion: 0.17
Nodes (8): GeneratedRegex, Regex, ActionKey, Value, CancellationToken, Task, CancellationToken, Task

### Community 87 - "Contracts"
Cohesion: 0.13
Nodes (5): CRM.Tests.Domain, CRM.Persistence.Configurations, Contracts, CRM.Tests.Integration, CRM.Domain

### Community 88 - "EnableRowLevelSecurity"
Cohesion: 0.18
Nodes (6): MasterData.Persistence.Migrations, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, EnableRowLevelSecurity

### Community 89 - "Enterprise Access Foundation — Owner Decisions Final Closure"
Cohesion: 0.25
Nodes (8): 1. Owner Decisions Closed, 2. Repository Impact (yalnızca liste — implementasyon yok), 3. Frozen Invariants, 4. Remaining Owner Decisions, 5. Final Architecture Gate, Decision A — System Catalog, Decision B — Opportunity Owner, Enterprise Access Foundation — Owner Decisions Final Closure

### Community 90 - "TenantAccessState"
Cohesion: 0.20
Nodes (7): TenantAccessState, Revision, RowVersion, TenantId, TenantId, TenantAccessStateConfiguration, EntityTypeBuilder

### Community 91 - "AccessRlsTests"
Cohesion: 0.44
Nodes (4): AccessRlsTests, DbUpdateException, Fact, Task

### Community 92 - ".HandleAsync"
Cohesion: 0.20
Nodes (9): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, ResolveOrCreatePartyResult, Fact, Task (+1 more)

### Community 93 - "PermissionSet"
Cohesion: 0.08
Nodes (23): PermissionSet, Id, Items, Key, Name, Origin, TenantId, IReadOnlyCollection (+15 more)

### Community 94 - ".HandleAsync"
Cohesion: 0.26
Nodes (9): Guid, MergePartyCommand, CancellationToken, Task, TimeSpan, MergePartyHandler, Fact, Task (+1 more)

### Community 95 - "Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)"
Cohesion: 0.29
Nodes (7): 1. Executive Verdict, 2. Round 1 Bulgularının PDF'e Yansıması, 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı, 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler), 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde), 6. Final Recommendation, Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

### Community 96 - "6. Current Repo Deltas"
Cohesion: 0.29
Nodes (7): 6.1 Identity — logical vs physical (çelişki değil, kayıt), 6.2 `roles.tenant_id` nullable → target NOT NULL, 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da, 6.4 `AssignedPrincipal` mutasyonsuz, 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz, 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı), 6. Current Repo Deltas

### Community 97 - "EntityRef"
Cohesion: 0.14
Nodes (9): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version (+1 more)

### Community 98 - "OpportunityStatus"
Cohesion: 0.27
Nodes (7): OpportunityStatus, Draft, Lost, Open, Won, OpportunityConfiguration, EntityTypeBuilder

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

### Community 105 - "Role"
Cohesion: 0.14
Nodes (12): Role, Id, Key, Name, Origin, TenantId, TenantId, RoleConfiguration (+4 more)

### Community 106 - "10. Query Authorization Strategy"
Cohesion: 0.67
Nodes (3): 10.1 Seçenekler, 10.2 Öneri: residual AST + domain adapter, 10. Query Authorization Strategy

### Community 107 - "IEntityTypeConfiguration"
Cohesion: 0.24
Nodes (6): Access.Persistence.Configurations, IEntityTypeConfiguration, EvidenceRecordConfiguration, IdempotencyRecordConfiguration, PermissionSetItemConfiguration, RoleAssignmentConfiguration

### Community 108 - "ActionRegistryEntry"
Cohesion: 0.13
Nodes (9): ActionRegistryEntry, ActionKey, IsDeprecated, OwnerModule, ResourceType, RiskClass, ActionRegistryEntryConfiguration, EntityTypeBuilder (+1 more)

### Community 110 - "17. Final Recommendation"
Cohesion: 0.67
Nodes (3): 17. Final Recommendation, Kaynaklar, OLD → NEW

### Community 111 - "CRM.Persistence"
Cohesion: 0.18
Nodes (6): CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox, CRM.Persistence, CrmDbContextTenantExtensions

### Community 112 - ".HandleAsync"
Cohesion: 0.07
Nodes (53): Admin, AdminAssignmentId, AuthorizationDeniedException, Exception, GrantedPayload, Grantee, IClassFixture, IdempotencyKeyReusedException (+45 more)

### Community 113 - "PartyRelationshipConfiguration"
Cohesion: 0.22
Nodes (8): PartyRelationshipStatus, Active, Ended, PartyRelationshipType, BranchOf, WorksFor, EntityTypeBuilder, PartyRelationshipConfiguration

### Community 114 - "Account"
Cohesion: 0.17
Nodes (10): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+2 more)

### Community 115 - "InitialAccessSchema"
Cohesion: 0.22
Nodes (5): DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder

### Community 116 - ".CreatePartyAsync"
Cohesion: 0.24
Nodes (6): CRM.Tests, TestData, Seller, PartyRef, Task, TenantId

### Community 117 - "PartyRef"
Cohesion: 0.20
Nodes (10): IPartyIdentityResolver, CancellationToken, Task, PartyRef, PartyId, TenantId, CancellationToken, PartyRef (+2 more)

### Community 119 - "MasterData.Domain"
Cohesion: 0.23
Nodes (4): MasterData.Persistence.Configurations, MasterData.Tests.Domain, MasterData.Domain, PartyExternalIdentityConfiguration

### Community 120 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (12): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+4 more)

### Community 121 - "AccessConstraintTests"
Cohesion: 0.43
Nodes (5): DbUpdateConcurrencyException, AccessConstraintTests, DbUpdateException, Fact, Task

### Community 122 - "PostgresCollection"
Cohesion: 0.40
Nodes (3): ICollectionFixture, PostgresCollection, PostgresCollection

### Community 123 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): AccessDbContextTenantExtensions, CancellationToken, Task

### Community 124 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): Access.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 125 - "Migration"
Cohesion: 0.18
Nodes (8): Migration, DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialMasterDataSchema

### Community 126 - "Access.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 127 - ".Rejects_malformed_keys"
Cohesion: 0.38
Nodes (4): ActionKeyTests, ArgumentException, InlineData, Theory

### Community 128 - ".GivenGrantAsync"
Cohesion: 0.09
Nodes (37): AccessScopeResolver, Action, ActionKey, Actor, ActorContext, All, Authorizer, DateTimeOffset (+29 more)

### Community 129 - "Access.Domain.Authorization"
Cohesion: 0.13
Nodes (11): Access.Evidence, Access.Application, Access.Outbox, Access.Persistence, Access.Idempotency, Access.Domain.Authorization, Access.Tests.Domain, AccessActionCatalog (+3 more)

### Community 130 - ".Create"
Cohesion: 0.28
Nodes (6): Fact, InvalidOperationException, PartyMergeTests, ArgumentException, Fact, PartyTests

### Community 131 - "Access.Persistence.Migrations"
Cohesion: 0.22
Nodes (5): Access.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, RebuildAccessAuthorizationModel

### Community 132 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CancellationToken, Task, MasterDataDbContextTenantExtensions

### Community 133 - "3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)"
Cohesion: 0.20
Nodes (10): 2. Mimari sınır testleri (`tests/CRM.Tests/Architecture/`), 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`), `CompleteOpportunityHandlerTests`, CRM Phase 1 — Test Senaryoları ve Beklenen Sonuçlar, `ModuleBoundaryTests`, `OpportunityConcurrencyTests`, `OpportunityPersistenceTests`, `PartyBackfillVerificationTests` (+2 more)

### Community 134 - "Enterprise Access Foundation — Technical Gap Closure (pre-execution)"
Cohesion: 0.20
Nodes (10): 1. RoleAssignment scope — "reserve" değil, "kaldır", 2. ActionKey / Action Registry tablosu — text PK, 3. Action Registry senkronizasyonu — CI analyzer değil, runtime + idempotent seed, 4. Contracts tipleri — minimal, genişletilebilir yüzey, 5. Escalation guard — self-check, granular allowlist yok, 6. Bootstrap problemi — yeni bulgu, kapatılıyor, 7. PermissionSetItem `relation` — yalnız `owner`, 8. `PrincipalType` — Access-internal enum, Contracts'ta değil (+2 more)

### Community 135 - "MasterData"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 136 - ".AuthorizeAsync"
Cohesion: 0.33
Nodes (5): AccessAuthorizer, CancellationToken, Guid, Task, TenantId

### Community 137 - ".Create"
Cohesion: 0.33
Nodes (5): TenantId, ArgumentException, Fact, InvalidOperationException, PartyRelationshipTests

### Community 138 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 139 - "EnableAccessRowLevelSecurity"
Cohesion: 0.22
Nodes (7): Schema, MigrationBuilder, EnableAccessRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder, Table

### Community 140 - "Enterprise Access Foundation — Test Senaryoları ve Beklenen Sonuçlar"
Cohesion: 0.25
Nodes (8): 2. Mimari sınır testleri (`tests/Access.Tests/Architecture/`), 3. Application katmanı — in-memory (`tests/Access.Tests/Application/`), 5. Test Coverage Review'a yanıt (2026-09-17), `AccessAuthorizerTests`, `AuthorizeResolveAccessScopeEquivalenceTests`, Enterprise Access Foundation — Test Senaryoları ve Beklenen Sonuçlar, `ModuleBoundaryTests`, Özet — sınıf başına test sayısı

### Community 144 - "1. Domain katmanı (`tests/Access.Tests/Domain/`)"
Cohesion: 0.33
Nodes (6): 1. Domain katmanı (`tests/Access.Tests/Domain/`), `ActionKeyTests`, `PermissionSetTests`, `RoleAssignmentTests`, `RoleTests`, `TenantAccessStateTests`

### Community 145 - ".BuildModel"
Cohesion: 0.33
Nodes (5): ModelSnapshot, DateTimeOffset, Guid, ModelBuilder, MasterDataDbContextModelSnapshot

### Community 146 - "PartyType"
Cohesion: 0.33
Nodes (4): PartyType, Organization, Person, CreatedPartyPayload

### Community 147 - ".BuildModel"
Cohesion: 0.33
Nodes (4): AccessDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 148 - ".BuildModel"
Cohesion: 0.33
Nodes (4): CrmDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 149 - "PartyConfiguration"
Cohesion: 0.53
Nodes (3): EntityTypeBuilder, PartyType, PartyConfiguration

### Community 152 - "1. Domain katmanı (`tests/CRM.Tests/Domain/`)"
Cohesion: 0.40
Nodes (5): 1. Domain katmanı (`tests/CRM.Tests/Domain/`), `OpportunityMoneyTests`, `OpportunityRowVersionTests`, `OpportunityStateMachineTests`, `PipelineDefinitionVersionTests`

### Community 153 - "4. Integration katmanı — gerçek Postgres (`tests/Access.Tests/Integration/`)"
Cohesion: 0.40
Nodes (5): 4. Integration katmanı — gerçek Postgres (`tests/Access.Tests/Integration/`), `AccessConstraintTests` (yeni sınıf — C4, H1, H2), `AccessRlsTests` (FF03 — RLS, ayrıcalıksız runtime rolüyle, süper kullanıcıyla değil), `BootstrapTenantAccessHandlerTests`, `GrantRevokeRoleAssignmentHandlerTests`

## Knowledge Gaps
- **717 isolated node(s):** ``OpportunityStateMachineTests``, ``OpportunityMoneyTests``, ``OpportunityRowVersionTests``, ``PipelineDefinitionVersionTests``, ``ModuleBoundaryTests`` (+712 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1016 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **14 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `.GivenGrantAsync`, `Access.Domain.Authorization`, `TenantFieldDefinition`, `.SetTenantContextAsync`, `OutboxMessage`, `ExternalIdentity`, `.AuthorizeAsync`, `PartyType`, `RoleAssignment`, `TenantMembership`, `IdempotencyRecord`, `.HandleAsync`, `.ResolveAsync`, `.GetPartiesAsync`, `RolePermissionSet`, `EvidenceRecord`, `MasterData.Application`, `.ResolveAsync`, `.Create`, `OutboxMessage`, `PrincipalRef`, `AuthorizationDecision`, `ActionKey`, `TenantAccessState`, `.HandleAsync`, `PermissionSet`, `EntityRef`, `TenantId`, `Role`, `IEntityTypeConfiguration`, `CRM.Persistence`, `.HandleAsync`, `PartyRelationshipConfiguration`, `.CreatePartyAsync`, `PartyRef`, `MasterData.Domain`, `.SetTenantContextAsync`, `.Rejects_malformed_keys`?**
  _High betweenness centrality (0.245) - this node is a cross-community bridge._
- **Why does `AccessDbContext` connect `AccessDbContext` to `.GivenGrantAsync`, `MasterDataDbContext`, `TenantMembership`, `ExternalIdentity`, `.AuthorizeAsync`, `.ResolveAsync`, `Role`, `OutboxMessage`, `ActionRegistryEntry`, `IdempotencyRecord`, `.HandleAsync`, `Account`, `EvidenceRecord`, `RoleAssignment`, `RolePermissionSet`, `TenantAccessState`, `.SetTenantContextAsync`, `PermissionSet`?**
  _High betweenness centrality (0.086) - this node is a cross-community bridge._
- **Why does `MasterDataDbContext` connect `MasterDataDbContext` to `MasterData.Application`, `.SetTenantContextAsync`, `OutboxMessage`, `IdempotencyRecord`, `.HandleAsync`, `.Create`, `EvidenceRecord`, `.CreatePartyAsync`, `PartyRef`, `.GetPartiesAsync`, `MasterDataDbContextFactory`, `PartyExternalIdentity`, `PartyRelationship`, `.CreateAdminContext`, `.HandleAsync`, `.HandleAsync`, `Party`?**
  _High betweenness centrality (0.067) - this node is a cross-community bridge._
- **What connects ``OpportunityStateMachineTests``, ``OpportunityMoneyTests``, ``OpportunityRowVersionTests`` to the rest of the system?**
  _717 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.0659536541889483 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.09956709956709957 - nodes in this community are weakly interconnected._
- **Should `TenantFieldDefinition` be split into smaller, more focused modules?**
  _Cohesion score 0.11333333333333333 - nodes in this community are weakly interconnected._