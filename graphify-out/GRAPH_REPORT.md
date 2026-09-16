# Graph Report - fynovio-platform  (2026-09-16)

## Corpus Check
- 195 files · ~88,749 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1492 nodes · 2398 edges · 110 communities (97 shown, 13 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 121 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `7e8fd4bf`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- OpportunityMoneyTests
- TenantFieldDefinition
- PipelineDefinition
- OutboxMessage
- ExternalIdentity
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
- Account
- OpportunityStateMachineTests
- IdempotencyRecord
- AccessDbContext
- OpportunityNeed
- OpportunityRowVersionTests
- Access.Domain.Authorization
- PipelineDefinitionVersion
- PipelineStage
- Role
- CRM Target Model — Phase 0 Delta Plan
- AddPipelineTables
- Design notes
- RolePermission
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- .GetPartiesAsync
- IdempotencyRecord
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- PartyRef
- PostgresFixture
- Migration
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
- PartyConfiguration
- DropCrmParties
- CRM.Persistence
- .Create
- .CreateAdminContext
- OpportunityStatus
- .BuildModel
- PrincipalRef
- PartyExternalIdentity
- EnableRowLevelSecurity
- RemoveCrmPartyEntity
- .Create
- Contracts
- CRM.Persistence.Configurations
- .Create
- .CreatePartyAsync
- CrmDbContextFactory
- InitialMasterDataSchema
- ArgumentOutOfRangeException
- IHasRowVersion
- .HandleAsync
- .HandleAsync
- .Create
- CRM.Domain
- CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan
- InitialAccessSchema
- EntityRef
- MembershipStatus
- TenantId
- EnableRowLevelSecurity
- InvalidOperationException
- .BuildModel
- AccessDbContextModelSnapshot.cs
- .SetTenantContextAsync
- Access.Domain.Identity
- .BuildTargetModel
- DateTimeOffset
- TenantId
- EntityTypeBuilder

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 77 edges
2. `Opportunity` - 59 edges
3. `CrmDbContext` - 34 edges
4. `MasterDataDbContext` - 28 edges
5. `OpportunityLine` - 27 edges
6. `PartyRelationship` - 24 edges
7. `CRM.Domain` - 24 edges
8. `OutboxMessage` - 23 edges
9. `OutboxMessage` - 23 edges
10. `OpportunityStateMachineTests` - 22 edges

## Surprising Connections (you probably didn't know these)
- `TestData` --references--> `PrincipalRef`  [EXTRACTED]
  tests/CRM.Tests/TestData.cs → src/Contracts/PrincipalRef.cs
- `PrincipalRef` --references--> `TestData`  [EXTRACTED]
  src/Contracts/PrincipalRef.cs → tests/MasterData.Tests/TestData.cs
- `Opportunity` --implements--> `IHasRowVersion`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/IHasRowVersion.cs
- `Opportunity` --references--> `PrincipalRef`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Contracts/PrincipalRef.cs
- `Opportunity` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Domain/OpportunityLine.cs

## Import Cycles
- None detected.

## Communities (110 total, 13 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.06
Nodes (35): List, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CancelDate, CancelReason, CreatedAt (+27 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.10
Nodes (17): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, ProductRef (+9 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - "OpportunityMoneyTests"
Cohesion: 0.35
Nodes (3): OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (20): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+12 more)

### Community 5 - "PipelineDefinition"
Cohesion: 0.12
Nodes (13): PipelineDefinition, CreatedAt, Id, Name, TenantId, UpdatedAt, Versions, DateTimeOffset (+5 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "ExternalIdentity"
Cohesion: 0.17
Nodes (11): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+3 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.12
Nodes (15): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+7 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.05
Nodes (42): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker, net10.0, Microsoft.NET.Sdk, net10.0, net10.0, EFCore.NamingConventions (10.0.1) (+34 more)

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
Cohesion: 0.14
Nodes (15): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+7 more)

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
Cohesion: 0.11
Nodes (18): RoleAssignment, AccountId, Id, RoleId, RowVersion, ScopeId, ScopeType, TenantId (+10 more)

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
Cohesion: 0.14
Nodes (11): TenantMembership, AccountId, DisabledAt, Id, InvitedAt, JoinedAt, RowVersion, Status (+3 more)

### Community 39 - "Account"
Cohesion: 0.15
Nodes (10): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+2 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.22
Nodes (4): OpportunityStateMachineTests, ArgumentException, Fact, InvalidOperationException

### Community 41 - "IdempotencyRecord"
Cohesion: 0.07
Nodes (31): PartyType, Organization, Person, CreatedPartyPayload, Guid, CreatePartyCommand, CancellationToken, Task (+23 more)

### Community 42 - "AccessDbContext"
Cohesion: 0.10
Nodes (15): Access.Persistence, DbContext, IDesignTimeDbContextFactory, AccessConnectionString, AccessDbContext, Accounts, ExternalIdentities, Permissions (+7 more)

### Community 43 - "OpportunityNeed"
Cohesion: 0.22
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

### Community 44 - "OpportunityRowVersionTests"
Cohesion: 0.53
Nodes (3): OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 45 - "Access.Domain.Authorization"
Cohesion: 0.15
Nodes (9): Access.Persistence.Configurations, Access.Domain.Authorization, Permission, Description, Id, Key, PermissionConfiguration, EntityTypeBuilder (+1 more)

### Community 46 - "PipelineDefinitionVersion"
Cohesion: 0.16
Nodes (11): PipelineDefinitionVersion, CreatedAt, Id, PipelineDefinitionId, Stages, TenantId, VersionNumber, DateTimeOffset (+3 more)

### Community 47 - "PipelineStage"
Cohesion: 0.16
Nodes (10): PipelineStage, CreatedAt, Id, Name, PipelineDefinitionVersionId, SortOrder, TenantId, DateTimeOffset (+2 more)

### Community 48 - "Role"
Cohesion: 0.25
Nodes (7): Role, Id, IsSystem, Name, TenantId, TenantId, EntityTypeBuilder

### Community 49 - "CRM Target Model — Phase 0 Delta Plan"
Cohesion: 0.13
Nodes (14): 0. Context and the decision this plan corrects, 10. Explicitly out of scope for this document, 11. Decisions recorded (2026-09-16, Party/MasterData reconciliation), 12. Revised dependency graph (2026-09-16, after §5.A/B/D/E), 1. Source-of-truth documents, 2. Current-state recap, 3. Decision matrix — current → target → migration strategy → compatibility risk, 4. Sales module re-introduction — concrete shape (+6 more)

### Community 50 - "AddPipelineTables"
Cohesion: 0.20
Nodes (6): DateTimeOffset, MigrationBuilder, AddPipelineTables, DateTimeOffset, Guid, ModelBuilder

### Community 51 - "Design notes"
Cohesion: 0.17
Nodes (11): Account is the one deliberate exception to mandatory `tenant_id`, Concurrency token — added in Revision 2, was an omission not a decision, Design notes, Diagram, Identity + Access schema (PostgreSQL, `identity`/`access` schemas), Namespace layout, Revision 3, Not yet designed here, `PrincipalRef` mapping (+3 more)

### Community 52 - "RolePermission"
Cohesion: 0.25
Nodes (5): RolePermission, PermissionId, RoleId, RolePermissionConfiguration, EntityTypeBuilder

### Community 53 - "MasterData / Party Foundation — Phase 0.5 Execution Plan"
Cohesion: 0.14
Nodes (13): Kabul kriterleri (bu planın "bitti" demesi için), MasterData / Party Foundation — Phase 0.5 Execution Plan, Task 0: MasterData projesini iskeletten gerçek modüle çevir, Task 10: CI + dokümanları senkronla, Task 1: Contracts — PartyRef, PartyType, PartyDirectoryEntry, IPartyDirectory, IPartyIdentityResolver, Task 2: Domain — Party, PartyRelationship, PartyExternalIdentity + merge invariant tests, Task 3: Mimari sınır testi, Task 4: Outbox / Idempotency / Evidence (CRM'in şeklinin birebir tekrarı) (+5 more)

### Community 54 - ".GetPartiesAsync"
Cohesion: 0.19
Nodes (11): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, PartyDirectoryEntry, CancellationToken, IReadOnlyCollection (+3 more)

### Community 55 - "IdempotencyRecord"
Cohesion: 0.14
Nodes (14): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+6 more)

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - "PartyRef"
Cohesion: 0.20
Nodes (11): TenantId, IPartyIdentityResolver, CancellationToken, Task, PartyRef, PartyId, TenantId, CancellationToken (+3 more)

### Community 59 - "PostgresFixture"
Cohesion: 0.14
Nodes (17): IAsyncLifetime, ICollectionFixture, PartyId, PostgresCollection, PostgresCollection, PostgreSqlContainer, Task, PostgresFixture (+9 more)

### Community 60 - "Migration"
Cohesion: 0.20
Nodes (6): Migration, MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

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
Cohesion: 0.11
Nodes (14): MasterDataConnectionString, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties (+6 more)

### Community 68 - "MasterData.Domain"
Cohesion: 0.12
Nodes (6): MasterData.Idempotency, MasterData.Persistence.Configurations, MasterData.Tests.Domain, MasterData.Outbox, MasterData.Domain, MasterData.Evidence

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - "PartyConfiguration"
Cohesion: 0.53
Nodes (3): EntityTypeBuilder, PartyType, PartyConfiguration

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "CRM.Persistence"
Cohesion: 0.15
Nodes (7): CRM.Tests.Integration, CRM.Persistence, MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - ".Create"
Cohesion: 0.09
Nodes (30): DbUpdateConcurrencyException, CompleteOpportunityHandler, TimeSpan, OpportunityId, CompleteOpportunityHandlerTests, Fact, InvalidOperationException, Task (+22 more)

### Community 75 - ".CreateAdminContext"
Cohesion: 0.16
Nodes (14): MergedPartyPayload, Guid, MergePartyCommand, CancellationToken, Task, TimeSpan, MergePartyHandler, MergePartyResult (+6 more)

### Community 76 - "OpportunityStatus"
Cohesion: 0.16
Nodes (11): OpportunityStatus, Canceled, Completed, Draft, Lost, Offered, Open, Waiting (+3 more)

### Community 77 - ".BuildModel"
Cohesion: 0.29
Nodes (5): ModelSnapshot, CrmDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 78 - "PrincipalRef"
Cohesion: 0.18
Nodes (6): MasterData.Tests, PrincipalRef, Issuer, Subject, TestData, Operator

### Community 79 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (13): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+5 more)

### Community 80 - "EnableRowLevelSecurity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder

### Community 81 - "RemoveCrmPartyEntity"
Cohesion: 0.22
Nodes (5): MigrationBuilder, RemoveCrmPartyEntity, DateTimeOffset, Guid, ModelBuilder

### Community 82 - ".Create"
Cohesion: 0.50
Nodes (3): PipelineDefinitionVersionTests, Fact, InvalidOperationException

### Community 83 - "Contracts"
Cohesion: 0.21
Nodes (5): MasterData.Application, MasterData.Persistence, Contracts, MasterData.Tests.Integration, CRM.Tests

### Community 84 - "CRM.Persistence.Configurations"
Cohesion: 0.18
Nodes (5): CRM.Persistence.Configurations, CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox

### Community 85 - ".Create"
Cohesion: 0.33
Nodes (5): TenantId, ArgumentException, Fact, InvalidOperationException, PartyRelationshipTests

### Community 86 - ".CreatePartyAsync"
Cohesion: 0.32
Nodes (5): TestData, Seller, PartyRef, Task, TenantId

### Community 88 - "InitialMasterDataSchema"
Cohesion: 0.22
Nodes (5): MasterData.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, InitialMasterDataSchema

### Community 89 - "ArgumentOutOfRangeException"
Cohesion: 0.24
Nodes (9): ArgumentOutOfRangeException, PartyRelationshipStatus, Active, Ended, PartyRelationshipType, BranchOf, WorksFor, EntityTypeBuilder (+1 more)

### Community 90 - "IHasRowVersion"
Cohesion: 0.19
Nodes (9): DbContextEventData, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, RowVersionInterceptor, CancellationToken, DbContext (+1 more)

### Community 91 - ".HandleAsync"
Cohesion: 0.19
Nodes (7): CRM.Application, CompletedPayload, CompleteOpportunityCommand, Guid, CancellationToken, Task, CompleteOpportunityResult

### Community 92 - ".HandleAsync"
Cohesion: 0.22
Nodes (9): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, ResolveOrCreatePartyResult, Fact, Task (+1 more)

### Community 93 - ".Create"
Cohesion: 0.28
Nodes (6): Fact, InvalidOperationException, PartyMergeTests, ArgumentException, Fact, PartyTests

### Community 94 - "CRM.Domain"
Cohesion: 0.11
Nodes (12): CRM.Tests.Domain, CRM.Domain, IEntityTypeConfiguration, EvidenceRecordConfiguration, EntityTypeBuilder, IdempotencyRecordConfiguration, EntityTypeBuilder, OpportunityLineConfiguration (+4 more)

### Community 95 - "CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan"
Cohesion: 0.25
Nodes (7): Acceptance criteria, CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan, Task 0: Pre-flight design decisions — read and confirm before Task 3, Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`, Task 2: Pipeline definition/version/stage tables (additive), Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover, Task 4: Docs, CI, memory sync

### Community 96 - "InitialAccessSchema"
Cohesion: 0.22
Nodes (6): Access.Persistence.Migrations, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder

### Community 97 - "EntityRef"
Cohesion: 0.18
Nodes (7): EntityRef, BoundedContext, EntityType, Id, EntityVersion, Entity, Version

### Community 98 - "MembershipStatus"
Cohesion: 0.31
Nodes (6): MembershipStatus, Active, Disabled, Invited, TenantMembershipConfiguration, EntityTypeBuilder

### Community 100 - "EnableRowLevelSecurity"
Cohesion: 0.29
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, EnableRowLevelSecurity

### Community 101 - "InvalidOperationException"
Cohesion: 0.29
Nodes (4): InvalidOperationException, IdempotencyKeyReusedException, OpportunityNotFoundException, PartyNotFoundException

### Community 102 - ".BuildModel"
Cohesion: 0.33
Nodes (4): DateTimeOffset, Guid, ModelBuilder, MasterDataDbContextModelSnapshot

### Community 103 - "AccessDbContextModelSnapshot.cs"
Cohesion: 0.40
Nodes (3): AccessDbContextModelSnapshot, DateTimeOffset, ModelBuilder

### Community 104 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CrmDbContextTenantExtensions, CancellationToken, Task

### Community 106 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **538 isolated node(s):** `Task 0: Pre-flight design decisions — read and confirm before Task 3`, `Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost``, `Task 2: Pipeline definition/version/stage tables (additive)`, `Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover`, `Task 4: Docs, CI, memory sync` (+533 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 752 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `OpportunityLine`, `CustomerNeed`, `TenantFieldDefinition`, `PipelineDefinition`, `RoleAssignment`, `IdempotencyRecord`, `AccessDbContext`, `OpportunityNeed`, `Access.Domain.Authorization`, `PipelineDefinitionVersion`, `PipelineStage`, `.GetPartiesAsync`, `PartyRef`, `MasterData.Domain`, `CRM.Persistence`, `OpportunityStatus`, `PrincipalRef`, `CRM.Persistence.Configurations`, `IHasRowVersion`, `.HandleAsync`, `CRM.Domain`, `EntityRef`, `TenantId`, `.SetTenantContextAsync`, `Access.Domain.Identity`?**
  _High betweenness centrality (0.216) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Persistence` to `BackfillMasterDataParties`, `ModuleBoundaryTests`, `DropOpportunityPartyForeignKey`, `DropCrmParties`, `.SetTenantContextAsync`, `CRM.Persistence.Migrations`, `.BuildModel`, `EnableRowLevelSecurity`, `RemoveCrmPartyEntity`, `AddPipelineTables`, `Contracts`, `CRM.Persistence.Configurations`, `CrmDbContextFactory`, `Migration`, `RenameOpportunityLifecycle`?**
  _High betweenness centrality (0.120) - this node is a cross-community bridge._
- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `CustomerNeed`, `OutboxMessage`, `TenantFieldDefinition`, `PipelineDefinition`, `.SetTenantContextAsync`, `AccessDbContext`, `OpportunityNeed`, `.Create`, `EvidenceRecord`, `PipelineDefinitionVersion`, `PipelineStage`, `CRM.Persistence.Configurations`, `CrmDbContextFactory`, `IdempotencyRecord`?**
  _High betweenness centrality (0.104) - this node is a cross-community bridge._
- **What connects `Task 0: Pre-flight design decisions — read and confirm before Task 3`, `Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost``, `Task 2: Pipeline definition/version/stage tables (additive)` to the rest of the system?**
  _538 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.05537098560354374 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.1038961038961039 - nodes in this community are weakly interconnected._
- **Should `TenantFieldDefinition` be split into smaller, more focused modules?**
  _Cohesion score 0.11333333333333333 - nodes in this community are weakly interconnected._