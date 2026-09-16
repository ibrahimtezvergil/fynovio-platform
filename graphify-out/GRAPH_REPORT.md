# Graph Report - fynovio-platform  (2026-09-16)

## Corpus Check
- 177 files · ~80,331 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1343 nodes · 2100 edges · 101 communities (93 shown, 8 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 106 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `798b6057`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- .Create
- TenantFieldDefinition
- Party
- OutboxMessage
- ExternalIdentity
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- Contracts.csproj
- CRM Module — Current-State Analysis
- .HandleAsync
- Migration
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
- IHasRowVersion
- IdempotencyRecord
- AccessDbContext
- OpportunityNeed
- .NewWaitingOpportunity
- Permission
- MembershipStatus
- .HandleAsync
- Role
- CRM Target Model — Phase 0 Delta Plan
- RoleAssignmentScopeType
- Design notes
- Access.Domain.Authorization
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- PartyRef
- IdempotencyRecord
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- EntityRef
- .CreateContext
- FixOpportunityAssignedPrincipalIndex
- IEntityTypeConfiguration
- PartyRelationship
- Party
- Access.Persistence.Configurations
- CrmDbContext
- Design notes
- MasterDataDbContext
- Contracts
- ModuleBoundaryTests
- MasterData.Persistence
- PartyConfiguration
- EntityVersion
- CRM.Persistence.Migrations
- .CreateAdminContext
- CRM.Persistence
- OpportunityStatus
- .BuildModel
- PrincipalRef
- PartyExternalIdentity
- EnableRowLevelSecurity
- .BuildTargetModel
- .HandleAsync
- .CreateAdminContext
- CRM.Persistence.Configurations
- .Create
- MasterData.Application
- InitialMasterDataSchema
- InitialAccessSchema
- ArgumentOutOfRangeException
- TenantFieldValueType
- MergePartyHandler.cs
- .Create
- EnableRowLevelSecurity
- CRM.Domain
- CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan
- PartyType
- PartyTests
- .SetTenantContextAsync
- TenantId
- .BuildTargetModel

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 70 edges
2. `Opportunity` - 43 edges
3. `CrmDbContext` - 28 edges
4. `OpportunityLine` - 27 edges
5. `MasterDataDbContext` - 26 edges
6. `PartyRelationship` - 24 edges
7. `OutboxMessage` - 23 edges
8. `OutboxMessage` - 23 edges
9. `Party` - 22 edges
10. `Party` - 21 edges

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

## Communities (101 total, 8 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.08
Nodes (24): List, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CancelDate, CancelReason, CreatedAt (+16 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.10
Nodes (18): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+10 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - ".Create"
Cohesion: 0.13
Nodes (11): TenantId, OpportunityMoneyTests, ArgumentException, Fact, OpportunityStateMachineTests, ArgumentException, Fact, InvalidOperationException (+3 more)

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.16
Nodes (13): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+5 more)

### Community 5 - "Party"
Cohesion: 0.10
Nodes (19): Party, CreatedAt, CreationSource, CustomFields, Email, Id, MergedIntoPartyId, Name (+11 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.10
Nodes (19): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+11 more)

### Community 7 - "ExternalIdentity"
Cohesion: 0.18
Nodes (10): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+2 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.13
Nodes (14): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+6 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.05
Nodes (42): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker, net10.0, Microsoft.NET.Sdk, net10.0, net10.0, EFCore.NamingConventions (10.0.1) (+34 more)

### Community 10 - "CRM Module — Current-State Analysis"
Cohesion: 0.07
Nodes (26): 10. Events and Integration, 11. Test Coverage, 12. Current Scope Summary, 13. Target-vs-Current Comparison, 14. Recommended Next Actions, 1. Executive Summary, 2. CRM File / Module Inventory, 3. Current Domain Model (+18 more)

### Community 11 - ".HandleAsync"
Cohesion: 0.19
Nodes (10): Guid, CreatePartyCommand, CancellationToken, Task, TimeSpan, CreatePartyHandler, IdempotencyKeyReusedException, Fact (+2 more)

### Community 12 - "Migration"
Cohesion: 0.25
Nodes (5): Migration, DateTimeOffset, Guid, MigrationBuilder, InitialCrmSchema

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
Cohesion: 0.16
Nodes (12): RoleAssignment, AccountId, Id, RoleId, RowVersion, ScopeId, ScopeType, TenantId (+4 more)

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
Cohesion: 0.17
Nodes (9): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+1 more)

### Community 40 - "IHasRowVersion"
Cohesion: 0.19
Nodes (9): DbContextEventData, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, RowVersionInterceptor, CancellationToken, DbContext (+1 more)

### Community 41 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (15): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+7 more)

### Community 42 - "AccessDbContext"
Cohesion: 0.10
Nodes (15): Access.Persistence, DbContext, IDesignTimeDbContextFactory, AccessConnectionString, AccessDbContext, Accounts, ExternalIdentities, Permissions (+7 more)

### Community 43 - "OpportunityNeed"
Cohesion: 0.22
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

### Community 44 - ".NewWaitingOpportunity"
Cohesion: 0.49
Nodes (3): OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 45 - "Permission"
Cohesion: 0.22
Nodes (6): Permission, Description, Id, Key, PermissionConfiguration, EntityTypeBuilder

### Community 46 - "MembershipStatus"
Cohesion: 0.31
Nodes (6): MembershipStatus, Active, Disabled, Invited, TenantMembershipConfiguration, EntityTypeBuilder

### Community 47 - ".HandleAsync"
Cohesion: 0.18
Nodes (10): Guid, MergePartyCommand, CancellationToken, Task, TimeSpan, MergePartyHandler, TenantId, Fact (+2 more)

### Community 48 - "Role"
Cohesion: 0.22
Nodes (8): Role, Id, IsSystem, Name, TenantId, TenantId, RoleConfiguration, EntityTypeBuilder

### Community 49 - "CRM Target Model — Phase 0 Delta Plan"
Cohesion: 0.13
Nodes (14): 0. Context and the decision this plan corrects, 10. Explicitly out of scope for this document, 11. Decisions recorded (2026-09-16, Party/MasterData reconciliation), 12. Revised dependency graph (2026-09-16, after §5.A/B/D/E), 1. Source-of-truth documents, 2. Current-state recap, 3. Decision matrix — current → target → migration strategy → compatibility risk, 4. Sales module re-introduction — concrete shape (+6 more)

### Community 50 - "RoleAssignmentScopeType"
Cohesion: 0.27
Nodes (6): RoleAssignmentScopeType, Network, OrganizationUnit, Tenant, RoleAssignmentConfiguration, EntityTypeBuilder

### Community 51 - "Design notes"
Cohesion: 0.17
Nodes (11): Account is the one deliberate exception to mandatory `tenant_id`, Concurrency token — added in Revision 2, was an omission not a decision, Design notes, Diagram, Identity + Access schema (PostgreSQL, `identity`/`access` schemas), Namespace layout, Revision 3, Not yet designed here, `PrincipalRef` mapping (+3 more)

### Community 52 - "Access.Domain.Authorization"
Cohesion: 0.20
Nodes (6): Access.Domain.Authorization, RolePermission, PermissionId, RoleId, RolePermissionConfiguration, EntityTypeBuilder

### Community 53 - "MasterData / Party Foundation — Phase 0.5 Execution Plan"
Cohesion: 0.14
Nodes (13): Kabul kriterleri (bu planın "bitti" demesi için), MasterData / Party Foundation — Phase 0.5 Execution Plan, Task 0: MasterData projesini iskeletten gerçek modüle çevir, Task 10: CI + dokümanları senkronla, Task 1: Contracts — PartyRef, PartyType, PartyDirectoryEntry, IPartyDirectory, IPartyIdentityResolver, Task 2: Domain — Party, PartyRelationship, PartyExternalIdentity + merge invariant tests, Task 3: Mimari sınır testi, Task 4: Outbox / Idempotency / Evidence (CRM'in şeklinin birebir tekrarı) (+5 more)

### Community 54 - "PartyRef"
Cohesion: 0.08
Nodes (27): CRM.Tests, TenantId, IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, IPartyIdentityResolver (+19 more)

### Community 55 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (15): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+7 more)

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+8 more)

### Community 58 - "EntityRef"
Cohesion: 0.25
Nodes (5): EntityRef, BoundedContext, EntityType, Id, TenantId

### Community 59 - ".CreateContext"
Cohesion: 0.31
Nodes (8): DbUpdateException, Fact, InvalidOperationException, PartyId, PostgresException, Task, TenantId, TenantIsolationTests

### Community 60 - "FixOpportunityAssignedPrincipalIndex"
Cohesion: 0.25
Nodes (5): MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

### Community 61 - "IEntityTypeConfiguration"
Cohesion: 0.25
Nodes (5): IEntityTypeConfiguration, EvidenceRecordConfiguration, EvidenceRecordConfiguration, IdempotencyRecordConfiguration, OutboxMessageConfiguration

### Community 62 - "PartyRelationship"
Cohesion: 0.12
Nodes (16): DateTimeOffset, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle, Metadata (+8 more)

### Community 63 - "Party"
Cohesion: 0.14
Nodes (12): DateTimeOffset, Party, CreatedAt, Email, Id, MergedIntoPartyId, Name, PartyType (+4 more)

### Community 64 - "Access.Persistence.Configurations"
Cohesion: 0.24
Nodes (4): Access.Persistence.Configurations, Access.Domain.Identity, AccountConfiguration, ExternalIdentityConfiguration

### Community 65 - "CrmDbContext"
Cohesion: 0.04
Nodes (39): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+31 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.17
Nodes (10): DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties, PartyExternalIdentities (+2 more)

### Community 68 - "Contracts"
Cohesion: 0.25
Nodes (5): MasterData.Persistence.Configurations, MasterData.Tests.Domain, Contracts, MasterData.Domain, PartyExternalIdentityConfiguration

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "MasterData.Persistence"
Cohesion: 0.32
Nodes (3): MasterData.Persistence, MasterData.Persistence.Migrations, MasterDataConnectionString

### Community 71 - "PartyConfiguration"
Cohesion: 0.53
Nodes (3): EntityTypeBuilder, PartyType, PartyConfiguration

### Community 72 - "EntityVersion"
Cohesion: 0.40
Nodes (3): EntityVersion, Entity, Version

### Community 73 - "CRM.Persistence.Migrations"
Cohesion: 0.38
Nodes (3): CRM.Persistence.Migrations, MigrationBuilder, FixCancelExpiryCheck

### Community 74 - ".CreateAdminContext"
Cohesion: 0.07
Nodes (35): CRM.Application, DbUpdateConcurrencyException, IAsyncLifetime, InvalidOperationException, CompletedPayload, CompleteOpportunityCommand, Guid, CompleteOpportunityHandler (+27 more)

### Community 75 - "CRM.Persistence"
Cohesion: 0.25
Nodes (4): CRM.Tests.Integration, CRM.Persistence, ICollectionFixture, PostgresCollection

### Community 76 - "OpportunityStatus"
Cohesion: 0.24
Nodes (7): OpportunityStatus, Canceled, Completed, Offered, Waiting, OpportunityConfiguration, EntityTypeBuilder

### Community 77 - ".BuildModel"
Cohesion: 0.33
Nodes (4): CrmDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 78 - "PrincipalRef"
Cohesion: 0.18
Nodes (6): MasterData.Tests, PrincipalRef, Issuer, Subject, TestData, Operator

### Community 79 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (11): DateTimeOffset, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId, Provider (+3 more)

### Community 80 - "EnableRowLevelSecurity"
Cohesion: 0.25
Nodes (5): MigrationBuilder, EnableRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder

### Community 81 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 82 - ".HandleAsync"
Cohesion: 0.20
Nodes (8): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, Fact, Task, ResolveOrCreatePartyTests

### Community 83 - ".CreateAdminContext"
Cohesion: 0.21
Nodes (6): MasterData.Tests.Integration, PostgresCollection, PostgreSqlContainer, Task, PostgresFixture, AdminConnectionString

### Community 84 - "CRM.Persistence.Configurations"
Cohesion: 0.15
Nodes (6): CRM.Persistence.Configurations, CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox, IdempotencyRecordConfiguration

### Community 85 - ".Create"
Cohesion: 0.33
Nodes (5): TenantId, ArgumentException, Fact, InvalidOperationException, PartyRelationshipTests

### Community 86 - "MasterData.Application"
Cohesion: 0.18
Nodes (5): MasterData.Application, CreatePartyResult, MergedPartyPayload, MergePartyResult, ResolveOrCreatePartyResult

### Community 87 - "InitialMasterDataSchema"
Cohesion: 0.20
Nodes (7): DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialMasterDataSchema

### Community 88 - "InitialAccessSchema"
Cohesion: 0.10
Nodes (14): Access.Persistence.Migrations, ModelSnapshot, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder, AccessDbContextModelSnapshot (+6 more)

### Community 89 - "ArgumentOutOfRangeException"
Cohesion: 0.23
Nodes (9): ArgumentOutOfRangeException, PartyRelationshipStatus, Active, Ended, PartyRelationshipType, BranchOf, WorksFor, EntityTypeBuilder (+1 more)

### Community 90 - "TenantFieldValueType"
Cohesion: 0.27
Nodes (7): TenantFieldValueType, Boolean, Date, Number, Text, TenantFieldDefinitionConfiguration, EntityTypeBuilder

### Community 91 - "MergePartyHandler.cs"
Cohesion: 0.31
Nodes (3): MasterData.Idempotency, MasterData.Outbox, MasterData.Evidence

### Community 92 - ".Create"
Cohesion: 0.36
Nodes (5): PartyType, TenantId, Fact, InvalidOperationException, PartyMergeTests

### Community 93 - "EnableRowLevelSecurity"
Cohesion: 0.25
Nodes (5): MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, EnableRowLevelSecurity

### Community 94 - "CRM.Domain"
Cohesion: 0.32
Nodes (3): CRM.Tests.Domain, CRM.Domain, OpportunityLineConfiguration

### Community 95 - "CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan"
Cohesion: 0.25
Nodes (7): Acceptance criteria, CRM Phase 1 — Lifecycle/Pipeline Foundation — Execution Plan, Task 0: Pre-flight design decisions — read and confirm before Task 3, Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost`, Task 2: Pipeline definition/version/stage tables (additive), Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover, Task 4: Docs, CI, memory sync

### Community 96 - "PartyType"
Cohesion: 0.33
Nodes (4): PartyType, Organization, Person, CreatedPartyPayload

### Community 97 - "PartyTests"
Cohesion: 0.53
Nodes (3): ArgumentException, Fact, PartyTests

### Community 98 - ".SetTenantContextAsync"
Cohesion: 0.40
Nodes (3): CancellationToken, Task, MasterDataDbContextTenantExtensions

### Community 100 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **517 isolated node(s):** `Task 0: Pre-flight design decisions — read and confirm before Task 3`, `Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost``, `Task 2: Pipeline definition/version/stage tables (additive)`, `Task 3: `Opportunity.PartyId` → `PartyRef` — MasterData cutover`, `Task 4: Docs, CI, memory sync` (+512 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 698 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `CustomerNeed`, `Party`, `.HandleAsync`, `IHasRowVersion`, `AccessDbContext`, `OpportunityNeed`, `.HandleAsync`, `Role`, `RoleAssignmentScopeType`, `Access.Domain.Authorization`, `PartyRef`, `EntityRef`, `IEntityTypeConfiguration`, `Access.Persistence.Configurations`, `CrmDbContext`, `EntityVersion`, `.CreateAdminContext`, `CRM.Persistence`, `OpportunityStatus`, `PrincipalRef`, `PartyExternalIdentity`, `.HandleAsync`, `.CreateAdminContext`, `CRM.Persistence.Configurations`, `MasterData.Application`, `ArgumentOutOfRangeException`, `MergePartyHandler.cs`, `CRM.Domain`, `PartyType`, `.SetTenantContextAsync`, `TenantId`?**
  _High betweenness centrality (0.212) - this node is a cross-community bridge._
- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `CustomerNeed`, `TenantFieldDefinition`, `.CreateAdminContext`, `AccessDbContext`, `OpportunityNeed`, `EvidenceRecord`, `CRM.Persistence.Configurations`, `IdempotencyRecord`?**
  _High betweenness centrality (0.076) - this node is a cross-community bridge._
- **Why does `MasterDataDbContext` connect `MasterDataDbContext` to `.SetTenantContextAsync`, `OutboxMessage`, `IdempotencyRecord`, `AccessDbContext`, `.HandleAsync`, `.CreateContext`, `.HandleAsync`, `PartyExternalIdentity`, `.HandleAsync`, `.CreateAdminContext`, `PartyRef`, `EvidenceRecord`, `MergePartyHandler.cs`, `PartyRelationship`, `Party`?**
  _High betweenness centrality (0.071) - this node is a cross-community bridge._
- **What connects `Task 0: Pre-flight design decisions — read and confirm before Task 3`, `Task 1: `OpportunityStatus` lifecycle rename — `Waiting/Offered/Completed/Canceled` → `Draft/Open/Won/Lost``, `Task 2: Pipeline definition/version/stage tables (additive)` to the rest of the system?**
  _517 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.07881773399014778 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `.Create` be split into smaller, more focused modules?**
  _Cohesion score 0.13012477718360071 - nodes in this community are weakly interconnected._