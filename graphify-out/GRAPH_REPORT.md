# Graph Report - fynovio-platform  (2026-09-16)

## Corpus Check
- 176 files · ~74,159 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 1334 nodes · 2092 edges · 88 communities (80 shown, 8 thin omitted)
- Extraction: 95% EXTRACTED · 5% INFERRED · 0% AMBIGUOUS · INFERRED: 106 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f2fec349`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- CustomerNeed
- Party
- TenantFieldDefinition
- OutboxMessage
- OutboxMessage
- PrincipalRef
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- Contracts.csproj
- CRM Module — Current-State Analysis
- Contracts
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
- RowVersionInterceptor
- IdempotencyRecord
- AccessDbContext
- OpportunityNeed
- .NewWaitingOpportunity
- Permission
- MembershipStatus
- Access.Persistence
- Role
- CRM Target Model — Phase 0 Delta Plan
- RoleAssignmentScopeType
- Design notes
- RolePermission
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- .Create
- IdempotencyRecord
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- EvidenceRecord
- EntityRef
- .CreateAdminContext
- FixOpportunityAssignedPrincipalIndex
- IEntityTypeConfiguration
- PartyRelationship
- Party
- Access.Domain.Identity
- CrmDbContext
- Design notes
- MasterDataDbContext
- Access.Domain.Authorization
- ModuleBoundaryTests
- MasterDataDbContextFactory
- PartyConfiguration
- EntityVersion
- FixCancelExpiryCheck
- .CreateAdminContext
- CRM.Persistence
- OpportunityStatus
- .BuildModel
- TenantId
- PartyExternalIdentity
- CRM.Persistence.Migrations
- .BuildTargetModel
- IHasRowVersion
- CRM.Domain
- .Create
- InitialAccessSchema
- ArgumentOutOfRangeException
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
- `EntityVersion` --references--> `EntityRef`  [EXTRACTED]
  src/Contracts/EntityVersion.cs → src/Contracts/EntityRef.cs
- `OpportunityLine` --references--> `EntityRef`  [EXTRACTED]
  src/Modules/CRM/Domain/OpportunityLine.cs → src/Contracts/EntityRef.cs
- `PartyRef` --references--> `TenantId`  [EXTRACTED]
  src/Contracts/PartyRef.cs → src/Contracts/EntityRef.cs

## Import Cycles
- None detected.

## Communities (88 total, 8 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.08
Nodes (24): List, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CancelDate, CancelReason, CreatedAt (+16 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.09
Nodes (19): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+11 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - "Party"
Cohesion: 0.05
Nodes (34): DbUpdateConcurrencyException, TenantId, Party, CreatedAt, CreationSource, CustomFields, Email, Id (+26 more)

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (21): CRM.Customization, TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName (+13 more)

### Community 5 - "OutboxMessage"
Cohesion: 0.11
Nodes (18): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+10 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "PrincipalRef"
Cohesion: 0.14
Nodes (12): PrincipalRef, Issuer, Subject, ExternalIdentity, AccountId, Id, Issuer, LinkedAt (+4 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.13
Nodes (14): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+6 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.05
Nodes (42): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker, net10.0, Microsoft.NET.Sdk, net10.0, net10.0, EFCore.NamingConventions (10.0.1) (+34 more)

### Community 10 - "CRM Module — Current-State Analysis"
Cohesion: 0.07
Nodes (26): 10. Events and Integration, 11. Test Coverage, 12. Current Scope Summary, 13. Target-vs-Current Comparison, 14. Recommended Next Actions, 1. Executive Summary, 2. CRM File / Module Inventory, 3. Current Domain Model (+18 more)

### Community 11 - "Contracts"
Cohesion: 0.06
Nodes (28): MasterData.Idempotency, MasterData.Persistence.Configurations, MasterData.Application, MasterData.Tests.Domain, MasterData.Persistence, Contracts, MasterData.Tests.Integration, MasterData.Outbox (+20 more)

### Community 12 - "Migration"
Cohesion: 0.25
Nodes (5): Migration, DateTimeOffset, Guid, MigrationBuilder, InitialCrmSchema

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
Cohesion: 0.15
Nodes (10): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+2 more)

### Community 40 - "RowVersionInterceptor"
Cohesion: 0.29
Nodes (7): DbContextEventData, InterceptionResult, SaveChangesInterceptor, RowVersionInterceptor, CancellationToken, DbContext, ValueTask

### Community 41 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+8 more)

### Community 42 - "AccessDbContext"
Cohesion: 0.18
Nodes (10): AccessDbContext, Accounts, ExternalIdentities, Permissions, RoleAssignments, RolePermissions, Roles, TenantMemberships (+2 more)

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
Cohesion: 0.27
Nodes (6): MembershipStatus, Active, Disabled, Invited, TenantMembershipConfiguration, EntityTypeBuilder

### Community 47 - "Access.Persistence"
Cohesion: 0.25
Nodes (3): Access.Persistence, AccessConnectionString, AccessDbContextFactory

### Community 48 - "Role"
Cohesion: 0.25
Nodes (7): Role, Id, IsSystem, Name, TenantId, TenantId, EntityTypeBuilder

### Community 49 - "CRM Target Model — Phase 0 Delta Plan"
Cohesion: 0.14
Nodes (13): 0. Context and the decision this plan corrects, 10. Explicitly out of scope for this document, 11. Decisions recorded (2026-09-16, Party/MasterData reconciliation), 1. Source-of-truth documents, 2. Current-state recap, 3. Decision matrix — current → target → migration strategy → compatibility risk, 4. Sales module re-introduction — concrete shape, 5. Assumptions requiring an explicit owner decision — STOP list (+5 more)

### Community 50 - "RoleAssignmentScopeType"
Cohesion: 0.31
Nodes (6): RoleAssignmentScopeType, Network, OrganizationUnit, Tenant, RoleAssignmentConfiguration, EntityTypeBuilder

### Community 51 - "Design notes"
Cohesion: 0.17
Nodes (11): Account is the one deliberate exception to mandatory `tenant_id`, Concurrency token — added in Revision 2, was an omission not a decision, Design notes, Diagram, Identity + Access schema (PostgreSQL, `identity`/`access` schemas), Namespace layout, Revision 3, Not yet designed here, `PrincipalRef` mapping (+3 more)

### Community 52 - "RolePermission"
Cohesion: 0.25
Nodes (5): RolePermission, PermissionId, RoleId, RolePermissionConfiguration, EntityTypeBuilder

### Community 53 - "MasterData / Party Foundation — Phase 0.5 Execution Plan"
Cohesion: 0.14
Nodes (13): Kabul kriterleri (bu planın "bitti" demesi için), MasterData / Party Foundation — Phase 0.5 Execution Plan, Task 0: MasterData projesini iskeletten gerçek modüle çevir, Task 10: CI + dokümanları senkronla, Task 1: Contracts — PartyRef, PartyType, PartyDirectoryEntry, IPartyDirectory, IPartyIdentityResolver, Task 2: Domain — Party, PartyRelationship, PartyExternalIdentity + merge invariant tests, Task 3: Mimari sınır testi, Task 4: Outbox / Idempotency / Evidence (CRM'in şeklinin birebir tekrarı) (+5 more)

### Community 54 - ".Create"
Cohesion: 0.05
Nodes (40): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, IPartyIdentityResolver, CancellationToken, Task (+32 more)

### Community 55 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (15): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+7 more)

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 58 - "EntityRef"
Cohesion: 0.25
Nodes (5): EntityRef, BoundedContext, EntityType, Id, TenantId

### Community 59 - ".CreateAdminContext"
Cohesion: 0.11
Nodes (21): Guid, ResolveOrCreatePartyCommand, CancellationToken, Task, ResolveOrCreatePartyHandler, ResolveOrCreatePartyResult, PostgreSqlContainer, Task (+13 more)

### Community 60 - "FixOpportunityAssignedPrincipalIndex"
Cohesion: 0.25
Nodes (5): MigrationBuilder, FixOpportunityAssignedPrincipalIndex, DateTimeOffset, Guid, ModelBuilder

### Community 61 - "IEntityTypeConfiguration"
Cohesion: 0.18
Nodes (7): CRM.Persistence.Configurations, IEntityTypeConfiguration, EvidenceRecordConfiguration, EntityTypeBuilder, IdempotencyRecordConfiguration, OutboxMessageConfiguration, EntityTypeBuilder

### Community 62 - "PartyRelationship"
Cohesion: 0.12
Nodes (16): DateTimeOffset, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle, Metadata (+8 more)

### Community 63 - "Party"
Cohesion: 0.12
Nodes (14): DateTimeOffset, PartyType, TenantId, Party, CreatedAt, Email, Id, MergedIntoPartyId (+6 more)

### Community 64 - "Access.Domain.Identity"
Cohesion: 0.25
Nodes (3): Access.Domain.Identity, ExternalIdentityConfiguration, EntityTypeBuilder

### Community 65 - "CrmDbContext"
Cohesion: 0.10
Nodes (16): CrmConnectionString, CrmDbContext, CustomerNeeds, EvidenceRecords, IdempotencyRecords, Opportunities, OpportunityLines, OpportunityNeeds (+8 more)

### Community 66 - "Design notes"
Cohesion: 0.25
Nodes (7): Design notes, Diagram, Not an extension of Tenant Lifecycle or Organization, Not yet designed here, Owns zero business data, `tenant_id` is the primary key, not a surrogate `id`, Tenant network schema (module owner not yet assigned)

### Community 67 - "MasterDataDbContext"
Cohesion: 0.18
Nodes (10): DbContext, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties (+2 more)

### Community 68 - "Access.Domain.Authorization"
Cohesion: 0.33
Nodes (3): Access.Persistence.Configurations, Access.Domain.Authorization, RoleConfiguration

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.43
Nodes (4): ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "MasterDataDbContextFactory"
Cohesion: 0.29
Nodes (3): IDesignTimeDbContextFactory, MasterDataConnectionString, MasterDataDbContextFactory

### Community 71 - "PartyConfiguration"
Cohesion: 0.53
Nodes (3): EntityTypeBuilder, PartyType, PartyConfiguration

### Community 72 - "EntityVersion"
Cohesion: 0.40
Nodes (3): EntityVersion, Entity, Version

### Community 73 - "FixCancelExpiryCheck"
Cohesion: 0.25
Nodes (5): MigrationBuilder, FixCancelExpiryCheck, DateTimeOffset, Guid, ModelBuilder

### Community 74 - ".CreateAdminContext"
Cohesion: 0.08
Nodes (31): CRM.Application, IAsyncLifetime, InvalidOperationException, CompletedPayload, CompleteOpportunityCommand, Guid, CompleteOpportunityHandler, CancellationToken (+23 more)

### Community 75 - "CRM.Persistence"
Cohesion: 0.20
Nodes (6): CRM.Tests.Integration, CRM.Persistence, CRM.Tests.Architecture, ICollectionFixture, PostgresCollection, PostgresCollection

### Community 76 - "OpportunityStatus"
Cohesion: 0.21
Nodes (7): OpportunityStatus, Canceled, Completed, Offered, Waiting, OpportunityConfiguration, EntityTypeBuilder

### Community 77 - ".BuildModel"
Cohesion: 0.33
Nodes (4): CrmDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder

### Community 78 - "TenantId"
Cohesion: 0.11
Nodes (12): MasterData.Tests, CRM.Tests, TenantId, CrmDbContextTenantExtensions, CancellationToken, Task, CancellationToken, Task (+4 more)

### Community 79 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (13): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+5 more)

### Community 80 - "CRM.Persistence.Migrations"
Cohesion: 0.28
Nodes (3): CRM.Persistence.Migrations, MigrationBuilder, EnableRowLevelSecurity

### Community 81 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

### Community 84 - "CRM.Domain"
Cohesion: 0.20
Nodes (5): CRM.Tests.Domain, CRM.Idempotency, CRM.Evidence, CRM.Outbox, CRM.Domain

### Community 85 - ".Create"
Cohesion: 0.33
Nodes (5): TenantId, ArgumentException, Fact, InvalidOperationException, PartyRelationshipTests

### Community 88 - "InitialAccessSchema"
Cohesion: 0.05
Nodes (27): MasterData.Persistence.Migrations, Access.Persistence.Migrations, ModelSnapshot, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder (+19 more)

### Community 89 - "ArgumentOutOfRangeException"
Cohesion: 0.24
Nodes (9): ArgumentOutOfRangeException, PartyRelationshipStatus, Active, Ended, PartyRelationshipType, BranchOf, WorksFor, EntityTypeBuilder (+1 more)

### Community 90 - ".BuildTargetModel"
Cohesion: 0.50
Nodes (3): DateTimeOffset, Guid, ModelBuilder

## Knowledge Gaps
- **510 isolated node(s):** `net10.0`, `Microsoft.NET.Sdk`, `BoundedContext`, `EntityType`, `Id` (+505 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 690 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **8 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `OpportunityLine`, `CustomerNeed`, `Party`, `TenantFieldDefinition`, `OutboxMessage`, `PrincipalRef`, `IdempotencyRecord`, `OpportunityNeed`, `MembershipStatus`, `Access.Persistence`, `.Create`, `EvidenceRecord`, `EntityRef`, `.CreateAdminContext`, `IEntityTypeConfiguration`, `Access.Domain.Identity`, `Access.Domain.Authorization`, `EntityVersion`, `.CreateAdminContext`, `CRM.Persistence`, `OpportunityStatus`, `TenantId`, `IHasRowVersion`, `CRM.Domain`?**
  _High betweenness centrality (0.199) - this node is a cross-community bridge._
- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `CustomerNeed`, `MasterDataDbContext`, `TenantFieldDefinition`, `OutboxMessage`, `.CreateAdminContext`, `OpportunityNeed`, `EvidenceRecord`, `TenantId`, `CRM.Domain`, `IdempotencyRecord`?**
  _High betweenness centrality (0.078) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Persistence` to `CrmDbContext`, `FixCancelExpiryCheck`, `Contracts`, `Migration`, `.BuildModel`, `TenantId`, `CRM.Persistence.Migrations`, `CRM.Domain`, `FixOpportunityAssignedPrincipalIndex`?**
  _High betweenness centrality (0.078) - this node is a cross-community bridge._
- **What connects `net10.0`, `Microsoft.NET.Sdk`, `BoundedContext` to the rest of the system?**
  _510 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.07881773399014778 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.09486166007905138 - nodes in this community are weakly interconnected._
- **Should `Party` be split into smaller, more focused modules?**
  _Cohesion score 0.05407925407925408 - nodes in this community are weakly interconnected._