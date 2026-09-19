# Graph Report - fynovio-platform  (2026-09-20)

## Corpus Check
- 354 files · ~205,337 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 2854 nodes · 5574 edges · 177 communities (164 shown, 12 thin omitted)
- Extraction: 89% EXTRACTED · 11% INFERRED · 0% AMBIGUOUS · INFERRED: 634 edges (avg confidence: 0.83)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `4f70d1fb`
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
- .EvaluateAsync
- CRM Target Model — Phase 0 Delta Plan
- Enterprise Access Foundation — Phase 1.5 Execution Plan
- IdempotencyRecord
- Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review
- AddPipelineTables
- Design notes
- Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure
- MasterData / Party Foundation — Phase 0.5 Execution Plan
- .ResolveExternalIdentityAsync
- EntityRef
- MasterData / Party Foundation — Design Reference (Phase 0.5)
- 4. Entegrasyon testleri (`tests/CRM.Tests/Integration/`)
- OpportunityAuthorizationDeniedException
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
- EvidenceRecord
- DropCrmParties
- OpportunityEndpointsTests
- PostgresFixture
- OutboxMessage
- .Create
- IdempotencyRecord
- TenantId
- CRM.Tests.csproj
- Migration
- RemoveCrmPartyEntity
- 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)
- EvidenceRecord
- ARCHITECTURE DELTA — Phase 1.5 Authorization Contract
- AccessAuthorizer
- ActionKey
- CRM.Domain
- InitialMasterDataSchema
- Enterprise Access Foundation — Owner Decisions Final Closure
- TenantAccessState
- PHASE 2 — CRM Opportunity Commands & API
- MasterData.Application
- PermissionSet
- .CreatePartyAsync
- Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)
- 6. Current Repo Deltas
- PipelineDefinition
- OpportunityStatus
- LegacyMigrationFixture
- 2. Repository Current State (doğrulanmış)
- 12. Phase 1.5 Real Scope
- 8. Target Data Model (Phase 1.5 minimum)
- MasterData.Tests
- .GrantAsync
- 10. Query Authorization Strategy
- CRM Phase 2 — Opportunity Commands & API Implementation Plan
- .Create
- CRM Phase 2 — Test Gap Audit
- 17. Final Recommendation
- OpportunityEndpoints.cs
- .CreateAdminContext
- .MapOpportunityEndpoints
- AccessDbContext
- Access.Persistence.Migrations
- ExternalIdentity
- .HandleAsync
- .CreateAdminContext
- IEntityTypeConfiguration
- .HandleAsync
- .SeedAsync
- IAuthorizer
- .Create
- ModuleBoundaryTests
- .GivenGrantAsync
- Access.Tests.csproj
- Access.Tests.Domain
- PrincipalRef
- Access.Domain.Authorization
- PHASE 2 — CRM Opportunity Commands & API — Closure Report
- .Create
- PartyExternalIdentity
- .OwnedBy_scope_returns_only_that_principals_opportunities
- WinOpportunityHandler
- MasterData
- EnableRowLevelSecurityOnPipelineTables
- .HandleAsync
- CRM.Persistence
- EnableAccessRowLevelSecurity
- .SeedOpenOpportunityAsync
- CancelOpportunityLineCommand
- ActionRegistryEntry
- ActorContext
- PipelineDefinitionVersion
- PartyRef
- CLAUDE PHASE 2 — EXECUTION PROMPT
- PrincipalResolver
- .AuthorizeAsync
- OpportunityPipelineFieldsTests
- PHASE 1.5 RUNTIME RLS PDP DELTA
- .SeedAsync
- RowVersionInterceptor
- Access.csproj
- AddPipelineStageActiveAndEntryFlags
- AuthorizationDenialStage
- Host.Tests.csproj
- .HandleAsync
- .BuildModel
- AuthorizationDecision
- OpportunityDto
- CRM.csproj
- MembershipStatus
- ICollectionFixture
- OpportunityNeed
- CrmActionCatalog.cs
- .Create
- CreateOpportunityHandler
- RolePermissionSet
- .Configure
- .Owner_relation_grant_is_consistent_across_both_contracts
- .ApplyAnyOf
- AuthorizationEffect
- IHasRowVersion
- ActorContextMiddleware.cs
- AccessConnectionString.cs
- CrmConnectionString.cs

## God Nodes (most connected - your core abstractions)
1. `Contracts` - 172 edges
2. `PrincipalRef` - 80 edges
3. `CRM.Application` - 68 edges
4. `CRM.Domain` - 57 edges
5. `Opportunity` - 54 edges
6. `AccessDbContext` - 52 edges
7. `CrmDbContext` - 46 edges
8. `AuthorizationRequest` - 39 edges
9. `CRM.Persistence` - 39 edges
10. `ActorContext` - 38 edges

## Surprising Connections (you probably didn't know these)
- `OpportunityEndpointsTests` --references--> `Program`  [EXTRACTED]
  tests/Host.Tests/OpportunityEndpointsTests.cs → src/Host/Program.cs
- `StubScopeResolver` --references--> `AccessScope`  [EXTRACTED]
  tests/CRM.Tests/StubAuthorizer.cs → src/Contracts/AccessScope.cs
- `LegacyMigrationFixture` --references--> `TenantId`  [EXTRACTED]
  tests/CRM.Tests/Integration/LegacyMigrationFixture.cs → src/Contracts/ActorContext.cs
- `StubAuthorizer` --references--> `AuthorizationEffect`  [EXTRACTED]
  tests/CRM.Tests/StubAuthorizer.cs → src/Contracts/AuthorizationEffect.cs
- `StubAuthorizer` --implements--> `IAuthorizer`  [EXTRACTED]
  tests/CRM.Tests/StubAuthorizer.cs → src/Contracts/IAuthorizer.cs

## Import Cycles
- None detected.

## Communities (177 total, 12 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.07
Nodes (27): Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CreatedAt, Currency, CustomFields, EstimatedAmount (+19 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.09
Nodes (20): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+12 more)

### Community 2 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 3 - "OpportunityMoneyTests"
Cohesion: 0.44
Nodes (3): OpportunityMoneyTests, ArgumentException, Fact

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (20): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+12 more)

### Community 5 - "PipelineStage"
Cohesion: 0.12
Nodes (13): PipelineStage, CreatedAt, Id, IsActive, IsEntry, Name, PipelineDefinitionVersionId, SortOrder (+5 more)

### Community 6 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): DateTimeOffset, Guid, TenantId, OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId (+12 more)

### Community 7 - "Account"
Cohesion: 0.15
Nodes (10): Account, CreatedAt, DisplayName, Email, Id, Locale, UpdatedAt, DateTimeOffset (+2 more)

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
Cohesion: 0.11
Nodes (17): IDesignTimeDbContextFactory, CrmDbContext, CustomerNeeds, EvidenceRecords, IdempotencyRecords, Opportunities, OpportunityLines, OpportunityNeeds (+9 more)

### Community 12 - "CRM.Persistence.Migrations"
Cohesion: 0.10
Nodes (13): CRM.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, InitialCrmSchema, DateTimeOffset, Guid, ModelBuilder (+5 more)

### Community 13 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "EvidenceRecord"
Cohesion: 0.11
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
Cohesion: 0.12
Nodes (14): RoleAssignment, AccountId, GrantedByAccountId, Id, PrincipalType, Reason, RoleId, RowVersion (+6 more)

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
Cohesion: 0.14
Nodes (11): TenantMembership, AccountId, DisabledAt, Id, InvitedAt, JoinedAt, RowVersion, Status (+3 more)

### Community 39 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): DateTimeOffset, TenantId, TimeSpan, IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation (+8 more)

### Community 40 - "OpportunityStateMachineTests"
Cohesion: 0.19
Nodes (5): OpportunityStateMachineTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException

### Community 41 - ".HandleAsync"
Cohesion: 0.16
Nodes (11): Guid, CreatePartyCommand, CancellationToken, Task, TimeSpan, CreatePartyHandler, CreatePartyResult, IdempotencyKeyReusedException (+3 more)

### Community 42 - "CRM.Application"
Cohesion: 0.04
Nodes (21): CRM.Application, AddedLinePayload, AddOpportunityLineResult, CanceledLinePayload, CancelOpportunityLineResult, ChangePipelineStageResult, CreatedPayload, CreateOpportunityResult (+13 more)

### Community 43 - "PHASE 2 — CRM Opportunity Commands & API — Planning Document"
Cohesion: 0.04
Nodes (46): 0. How to read this document, 10. CustomerNeed impact assessment, 11. Persistence / migration impact (Phase 2), 12. Legacy compatibility / migration impact, 12A. Event / outbox catalog (Phase 2 events), 13. Idempotency × concurrency behavior matrix, 14. Authorization evaluation sequence mapped to actual Phase 1.5 components, 15. Consistent error model (+38 more)

### Community 44 - ".NewDraftOpportunity"
Cohesion: 0.49
Nodes (3): OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 45 - ".EvaluateAsync"
Cohesion: 0.22
Nodes (10): AccessScope, All, AnyOf, None, IReadOnlyList, OwnedBy, ScopeTerm, ActionKey (+2 more)

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

### Community 54 - ".ResolveExternalIdentityAsync"
Cohesion: 0.29
Nodes (7): IPartyIdentityResolver, CancellationToken, Task, CancellationToken, PartyRef, Task, PartyIdentityResolver

### Community 55 - "EntityRef"
Cohesion: 0.17
Nodes (8): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version

### Community 56 - "MasterData / Party Foundation — Design Reference (Phase 0.5)"
Cohesion: 0.15
Nodes (12): 10. Phase 0.5 acceptance criteria, 11. Explicitly deferred (unchanged from prior review, restated for this document's completeness), 1. Why Party moves out of CRM, 2. PartyRef — a strongly-typed reference, not generic EntityRef, 3. Party = Person | Organization; Contact is a role, not an entity, 4. IPartyDirectory vs. IPartyIdentityResolver — read/display is not command/identity, 5. Party merge / canonical identity, 6. PartyExternalIdentity — provider vs. source instance (+4 more)

### Community 57 - "4. Entegrasyon testleri (`tests/CRM.Tests/Integration/`)"
Cohesion: 0.06
Nodes (34): 1. Domain katmanı (`tests/CRM.Tests/Domain/`), 2. Uygulama katmanı — komut handler'ları (`tests/CRM.Tests/Application/`), 3. Uygulama katmanı — sorgu handler'ları (`tests/CRM.Tests/Application/`), 4. Entegrasyon testleri (`tests/CRM.Tests/Integration/`), 5. Mimari sınır testi (`tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`), 6. HTTP / Host katmanı (`tests/Host.Tests/OpportunityEndpointsTests.cs`), `AddOpportunityLineHandlerTests.cs`, `CancelOpportunityLineHandlerTests.cs` (+26 more)

### Community 58 - "OpportunityAuthorizationDeniedException"
Cohesion: 0.12
Nodes (21): InvalidOperationException, CancellationToken, Task, CancellationToken, DbUpdateException, Task, IdempotencyKeyReusedException, CancellationToken (+13 more)

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
Cohesion: 0.07
Nodes (29): DateTimeOffset, TenantId, PartyRelationship, CreatedAt, EndedAt, FromPartyId, Id, JobTitle (+21 more)

### Community 63 - "Party"
Cohesion: 0.08
Nodes (23): DateTimeOffset, PartyType, TenantId, Party, CreatedAt, Email, Id, MergedIntoPartyId (+15 more)

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
Cohesion: 0.15
Nodes (11): DbContext, DbSet, ModelBuilder, MasterDataDbContext, EvidenceRecords, IdempotencyRecords, OutboxMessages, Parties (+3 more)

### Community 68 - "Contracts"
Cohesion: 0.08
Nodes (13): MasterData.Idempotency, MasterData.Persistence.Configurations, MasterData.Tests.Domain, MasterData.Persistence, Contracts, MasterData.Tests.Integration, MasterData.Outbox, MasterData.Domain (+5 more)

### Community 69 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): CRM.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 70 - "DropOpportunityPartyForeignKey"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropOpportunityPartyForeignKey, DateTimeOffset, Guid, ModelBuilder

### Community 71 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): DateTimeOffset, Guid, TenantId, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+9 more)

### Community 72 - "DropCrmParties"
Cohesion: 0.22
Nodes (5): MigrationBuilder, DropCrmParties, DateTimeOffset, Guid, ModelBuilder

### Community 73 - "OpportunityEndpointsTests"
Cohesion: 0.18
Nodes (11): AccountId, Host.Tests, HttpClient, JsonElement, Subject, JwtTestTokenFactory, Fact, PostgreSqlContainer (+3 more)

### Community 74 - "PostgresFixture"
Cohesion: 0.14
Nodes (10): PartyBackfillVerificationTests, Fact, Task, PipelineDefinitionPersistenceTests, Fact, Task, PostgresFixture, AdminConnectionString (+2 more)

### Community 75 - "OutboxMessage"
Cohesion: 0.09
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 76 - ".Create"
Cohesion: 0.22
Nodes (7): GetPipelineStagesHandler, GetPipelineStagesHandlerTests, Fact, Task, PipelineDefinitionVersionTests, Fact, InvalidOperationException

### Community 77 - "IdempotencyRecord"
Cohesion: 0.11
Nodes (16): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+8 more)

### Community 78 - "TenantId"
Cohesion: 0.08
Nodes (28): MasterData.Tests, Access.Tests, TenantId, CancellationToken, Task, CrmDbContextTenantExtensions, CancellationToken, Task (+20 more)

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
Cohesion: 0.13
Nodes (15): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+7 more)

### Community 84 - "ARCHITECTURE DELTA — Phase 1.5 Authorization Contract"
Cohesion: 0.12
Nodes (16): ARCHITECTURE DELTA — Phase 1.5 Authorization Contract, Binding HTTP semantics (restated, now binding), Concrete shape, CRM Phase 2 — Owner Decisions: Authorization Delta & Pipeline Entry-Stage Resolution, CRM-side wiring — refined 2026-09-19 to preserve internal diagnostic semantics, Disposition of `PipelineDefinitionVersion.AddStage`'s current `_stages.Count == 0 => IsEntry` default, Disposition of the test-gap audit, Existing-Opportunity pinning (restated, now binding — was already true by construction, now explicit) (+8 more)

### Community 85 - "AccessAuthorizer"
Cohesion: 0.40
Nodes (5): AccessAuthorizer, CancellationToken, Guid, Task, TenantId

### Community 86 - "ActionKey"
Cohesion: 0.17
Nodes (8): GeneratedRegex, Regex, ActionKey, Value, CancellationToken, Task, CancellationToken, Task

### Community 87 - "CRM.Domain"
Cohesion: 0.18
Nodes (5): CRM.Tests.Domain, CRM.Tests.Application, CRM.Tests.Integration, CRM.Domain, CRM.Tests

### Community 88 - "InitialMasterDataSchema"
Cohesion: 0.10
Nodes (13): MasterData.Persistence.Migrations, DateTimeOffset, Guid, MigrationBuilder, DateTimeOffset, Guid, ModelBuilder, InitialMasterDataSchema (+5 more)

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
Cohesion: 0.10
Nodes (14): MasterData.Application, PartyType, Organization, Person, CreatedPartyPayload, Guid, ResolveOrCreatePartyCommand, CancellationToken (+6 more)

### Community 93 - "PermissionSet"
Cohesion: 0.08
Nodes (21): PermissionSet, Id, Items, Key, Name, Origin, TenantId, IReadOnlyCollection (+13 more)

### Community 94 - ".CreatePartyAsync"
Cohesion: 0.15
Nodes (21): OpenOpportunityCommand, DateTimeOffset, Guid, OpenOpportunityHandler, TimeSpan, PipelineConfigurationInvalidException, AddOpportunityLineHandlerTests, Fact (+13 more)

### Community 95 - "Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)"
Cohesion: 0.29
Nodes (7): 1. Executive Verdict, 2. Round 1 Bulgularının PDF'e Yansıması, 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı, 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler), 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde), 6. Final Recommendation, Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

### Community 96 - "6. Current Repo Deltas"
Cohesion: 0.29
Nodes (7): 6.1 Identity — logical vs physical (çelişki değil, kayıt), 6.2 `roles.tenant_id` nullable → target NOT NULL, 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da, 6.4 `AssignedPrincipal` mutasyonsuz, 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz, 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı), 6. Current Repo Deltas

### Community 97 - "PipelineDefinition"
Cohesion: 0.10
Nodes (16): CRM.Persistence.Configurations, PipelineDefinition, CreatedAt, Id, Name, TenantId, UpdatedAt, Versions (+8 more)

### Community 98 - "OpportunityStatus"
Cohesion: 0.19
Nodes (8): ListOpportunitiesRequest, OpportunityStatus, Draft, Lost, Open, Won, OpportunityConfiguration, EntityTypeBuilder

### Community 99 - "LegacyMigrationFixture"
Cohesion: 0.13
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

### Community 105 - ".GrantAsync"
Cohesion: 0.36
Nodes (7): WinOpportunityCommand, Guid, OpportunityAuthorizationTests, Fact, OpportunityId, Task, TenantId

### Community 106 - "10. Query Authorization Strategy"
Cohesion: 0.67
Nodes (3): 10.1 Seçenekler, 10.2 Öneri: residual AST + domain adapter, 10. Query Authorization Strategy

### Community 107 - "CRM Phase 2 — Opportunity Commands & API Implementation Plan"
Cohesion: 0.07
Nodes (29): CRM Phase 2 — Opportunity Commands & API Implementation Plan, File Structure, Phase 2 complete, Scope note — one implementation sub-decision this plan makes, flagged rather than silently assumed, Self-review (performed before handing this plan over), Task 10: `AddOpportunityLineCommand` / `AddOpportunityLineHandler`, Task 11: `CancelOpportunityLineCommand` / `CancelOpportunityLineHandler`, Task 12: `LoseOpportunityCommand` / `LoseOpportunityHandler` (+21 more)

### Community 108 - ".Create"
Cohesion: 0.19
Nodes (12): ReassignOpportunityHandler, TimeSpan, ReassignOpportunityHandlerTests, Fact, Task, OpportunityConcurrencyTests, DbUpdateConcurrencyException, Fact (+4 more)

### Community 109 - "CRM Phase 2 — Test Gap Audit"
Cohesion: 0.12
Nodes (15): 0. Decision provenance — the three named behaviors, 1. Real, project-acknowledged gap: `OpportunityAuthorizationTests.cs` was never built, 2. Real, untested code path: unprivileged runtime role through the application/handler pipeline, 3. HTTP/Host end-to-end coverage, 4. Cross-tenant non-leak at HTTP level, 5. Record-level authorization scopes, 6. Field-level security, 7. Policy decisions / obligations / approval (+7 more)

### Community 110 - "17. Final Recommendation"
Cohesion: 0.67
Nodes (3): 17. Final Recommendation, Kaynaklar, OLD → NEW

### Community 111 - "OpportunityEndpoints.cs"
Cohesion: 0.08
Nodes (19): Host.Authentication, Host.Endpoints, HttpContext, HttpContextActorContextExtensions, JwtOptions, Audience, Issuer, SigningKey (+11 more)

### Community 112 - ".CreateAdminContext"
Cohesion: 0.07
Nodes (50): Admin, AdminAssignmentId, Exception, GrantedPayload, Grantee, IClassFixture, IEnumerable, RevokedPayload (+42 more)

### Community 113 - ".MapOpportunityEndpoints"
Cohesion: 0.15
Nodes (9): IEndpointRouteBuilder, AddOpportunityLineCommand, Guid, CancellationToken, Task, GetOpportunityQuery, Guid, ReassignOpportunityCommand (+1 more)

### Community 114 - "AccessDbContext"
Cohesion: 0.11
Nodes (17): AccessDbContext, Accounts, Actions, EvidenceRecords, ExternalIdentities, IdempotencyRecords, OutboxMessages, PermissionSetItems (+9 more)

### Community 115 - "Access.Persistence.Migrations"
Cohesion: 0.10
Nodes (13): Access.Persistence.Migrations, DateTimeOffset, MigrationBuilder, InitialAccessSchema, DateTimeOffset, ModelBuilder, DateTimeOffset, Guid (+5 more)

### Community 116 - "ExternalIdentity"
Cohesion: 0.15
Nodes (11): ExternalIdentity, AccountId, Id, Issuer, LinkedAt, Principal, RawClaims, Subject (+3 more)

### Community 117 - ".HandleAsync"
Cohesion: 0.25
Nodes (5): CreateOpportunityCommand, Guid, CancellationToken, DbUpdateException, Task

### Community 118 - ".CreateAdminContext"
Cohesion: 0.32
Nodes (7): PipelineConstraintTests, DbUpdateException, DefinitionId, Fact, PostgresException, Task, VersionId

### Community 119 - "IEntityTypeConfiguration"
Cohesion: 0.10
Nodes (16): Access.Persistence.Configurations, IEntityTypeConfiguration, Role, Id, Key, Name, Origin, TenantId (+8 more)

### Community 120 - ".HandleAsync"
Cohesion: 0.13
Nodes (15): MergedPartyPayload, Guid, MergePartyCommand, CancellationToken, Task, TimeSpan, MergePartyHandler, MergePartyResult (+7 more)

### Community 121 - ".SeedAsync"
Cohesion: 0.21
Nodes (13): EntryStageId, InactiveStageId, OtherActiveStageId, ChangePipelineStageCommand, Guid, ChangePipelineStageHandler, TimeSpan, InvalidPipelineTransitionException (+5 more)

### Community 122 - "IAuthorizer"
Cohesion: 0.21
Nodes (13): EntryStage, OtherStage, IAuthorizer, GetOpportunityAvailableActionsHandler, GetOpportunityAvailableActionsQuery, Guid, DenyingAuthorizer, GetOpportunityAvailableActionsHandlerTests (+5 more)

### Community 123 - ".Create"
Cohesion: 0.12
Nodes (15): CancellationToken, Task, RoleAssignmentTests, ArgumentException, ArgumentOutOfRangeException, Fact, InvalidOperationException, RoleTests (+7 more)

### Community 124 - "ModuleBoundaryTests"
Cohesion: 0.31
Nodes (5): Access.Tests.Architecture, ModuleBoundaryTests, Assembly, Fact, TestResult

### Community 125 - ".GivenGrantAsync"
Cohesion: 0.27
Nodes (13): Authorizer, ScopeResolver, Action, Actor, IActionCatalog, AccessActionCatalogService, AccessScopeResolver, AccessTestFixture (+5 more)

### Community 126 - "Access.Tests.csproj"
Cohesion: 0.22
Nodes (8): net10.0, coverlet.collector (6.0.4), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 127 - "Access.Tests.Domain"
Cohesion: 0.16
Nodes (7): Access.Tests.Domain, ActionKeyTests, ArgumentException, InlineData, Theory, TenantAccessStateTests, Fact

### Community 128 - "PrincipalRef"
Cohesion: 0.22
Nodes (14): All, AuthorizationRequest, Resource, PrincipalRef, Issuer, Subject, ResourceDescriptor, Id (+6 more)

### Community 129 - "Access.Domain.Authorization"
Cohesion: 0.15
Nodes (10): Access.Evidence, Access.Application, Access.Tests.Application, Access.Outbox, Access.Persistence, Access.Idempotency, Access.Domain.Identity, Access.Domain.Authorization (+2 more)

### Community 130 - "PHASE 2 — CRM Opportunity Commands & API — Closure Report"
Cohesion: 0.12
Nodes (15): 1. Phase 2 scope completed, 2.1 `AuthorizationDenialStage` delta, 2.2 Phase 1.5 runtime-RLS PDP delta, 2.3 Explicit `IsEntry` / invalid pipeline configuration resolution, 2. Architecture Deltas, 3. Production defects discovered and fixed, 4. Final security properties, 5. Final test results (+7 more)

### Community 131 - ".Create"
Cohesion: 0.34
Nodes (7): PdpRuntimeRoleTests, AnyOf, Fact, None, OwnedBy, Task, TenantId

### Community 132 - "PartyExternalIdentity"
Cohesion: 0.14
Nodes (13): DateTimeOffset, TenantId, PartyExternalIdentity, CreatedAt, ExternalId, ExternalType, Id, PartyId (+5 more)

### Community 133 - ".OwnedBy_scope_returns_only_that_principals_opportunities"
Cohesion: 0.35
Nodes (7): IAccessScopeResolver, ListOpportunitiesHandler, ListOpportunitiesHandlerTests, Fact, Task, TenantId, StubScopeResolver

### Community 134 - "WinOpportunityHandler"
Cohesion: 0.30
Nodes (8): WinOpportunityHandler, DbUpdateException, TimeSpan, WinOpportunityHandlerTests, Fact, OpportunityId, Task, TenantId

### Community 135 - "MasterData"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 136 - "EnableRowLevelSecurityOnPipelineTables"
Cohesion: 0.22
Nodes (5): MigrationBuilder, EnableRowLevelSecurityOnPipelineTables, DateTimeOffset, Guid, ModelBuilder

### Community 137 - ".HandleAsync"
Cohesion: 0.28
Nodes (6): CancellationToken, DbUpdateException, Task, TenantId, PipelineDefinitionVersionId, PipelineStageId

### Community 138 - "CRM.Persistence"
Cohesion: 0.10
Nodes (12): CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox, Worker, CRM.Persistence, AddOpportunityLineHandler, DbUpdateException (+4 more)

### Community 139 - "EnableAccessRowLevelSecurity"
Cohesion: 0.18
Nodes (7): Schema, MigrationBuilder, EnableAccessRowLevelSecurity, DateTimeOffset, Guid, ModelBuilder, Table

### Community 140 - ".SeedOpenOpportunityAsync"
Cohesion: 0.20
Nodes (11): RowVersion, LoseOpportunityCommand, Guid, LoseOpportunityHandler, DbUpdateException, TimeSpan, LoseOpportunityHandlerTests, Fact (+3 more)

### Community 142 - "ActionRegistryEntry"
Cohesion: 0.17
Nodes (8): ActionRegistryEntry, ActionKey, IsDeprecated, OwnerModule, ResourceType, RiskClass, ActionRegistryEntryConfiguration, EntityTypeBuilder

### Community 143 - "ActorContext"
Cohesion: 0.12
Nodes (14): ActorContext, CorrelationId, Principal, Guid, CancellationToken, Task, HttpContext, Task (+6 more)

### Community 144 - "PipelineDefinitionVersion"
Cohesion: 0.15
Nodes (11): PipelineDefinitionVersion, CreatedAt, Id, PipelineDefinitionId, Stages, TenantId, VersionNumber, DateTimeOffset (+3 more)

### Community 145 - "PartyRef"
Cohesion: 0.15
Nodes (14): IPartyDirectory, CancellationToken, IReadOnlyCollection, IReadOnlyDictionary, Task, PartyDirectoryEntry, PartyRef, PartyId (+6 more)

### Community 146 - "CLAUDE PHASE 2 — EXECUTION PROMPT"
Cohesion: 0.18
Nodes (10): CLAUDE PHASE 2 — EXECUTION PROMPT, Critical architecture constraints, Final console response, Mandatory references, Open-decision discipline, PHASE 2 READINESS, Repository hygiene, Required output (+2 more)

### Community 147 - "PrincipalResolver"
Cohesion: 0.28
Nodes (7): PrincipalResolver, CancellationToken, Task, TenantId, PrincipalResolverTests, Fact, Task

### Community 148 - ".AuthorizeAsync"
Cohesion: 0.14
Nodes (12): CancellationToken, Task, CancellationToken, Task, CancellationToken, Task, CancellationToken, IReadOnlyList (+4 more)

### Community 149 - "OpportunityPipelineFieldsTests"
Cohesion: 0.46
Nodes (3): OpportunityPipelineFieldsTests, ArgumentException, Fact

### Community 150 - "PHASE 1.5 RUNTIME RLS PDP DELTA"
Cohesion: 0.20
Nodes (9): Binding principle, Chosen implementation, PHASE 1.5 RUNTIME RLS PDP DELTA, Problem, Remaining architectural risk, Results, Security properties, Tests (+1 more)

### Community 151 - ".SeedAsync"
Cohesion: 0.35
Nodes (6): GetOpportunityHandler, GetOpportunityHandlerTests, Fact, OpportunityId, Task, TenantId

### Community 152 - "RowVersionInterceptor"
Cohesion: 0.25
Nodes (7): DbContextEventData, InterceptionResult, SaveChangesInterceptor, RowVersionInterceptor, CancellationToken, DbContext, ValueTask

### Community 153 - "Access.csproj"
Cohesion: 0.33
Nodes (5): net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk

### Community 154 - "AddPipelineStageActiveAndEntryFlags"
Cohesion: 0.22
Nodes (5): MigrationBuilder, AddPipelineStageActiveAndEntryFlags, DateTimeOffset, Guid, ModelBuilder

### Community 155 - "AuthorizationDenialStage"
Cohesion: 0.20
Nodes (7): AuthorizationDenialStage, Coarse, None, Record, StubAuthorizer, CancellationToken, Task

### Community 156 - "Host.Tests.csproj"
Cohesion: 0.25
Nodes (7): Microsoft.AspNetCore.Mvc.Testing (10.0.4), net10.0, Microsoft.NET.Test.Sdk (17.14.1), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4), Microsoft.NET.Sdk

### Community 157 - ".HandleAsync"
Cohesion: 0.18
Nodes (13): IExceptionHandler, CancellationToken, Exception, HttpContext, ValueTask, CrmProblemDetailsExceptionHandler, Status, Exception (+5 more)

### Community 158 - ".BuildModel"
Cohesion: 0.11
Nodes (13): ModelSnapshot, AccessDbContextModelSnapshot, DateTimeOffset, Guid, ModelBuilder, CrmDbContextModelSnapshot, DateTimeOffset, Guid (+5 more)

### Community 159 - "AuthorizationDecision"
Cohesion: 0.22
Nodes (8): AuthorizationDecision, DecisionId, DenialStage, Effect, IsAllowed, ReasonCode, Revision, Guid

### Community 160 - "OpportunityDto"
Cohesion: 0.47
Nodes (4): OpportunityDto, OpportunityLineDto, DateTimeOffset, IReadOnlyList

### Community 161 - "CRM.csproj"
Cohesion: 0.20
Nodes (8): Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Sdk.Worker, net10.0, EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.4), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, net10.0

### Community 162 - "MembershipStatus"
Cohesion: 0.27
Nodes (6): MembershipStatus, Active, Disabled, Invited, TenantMembershipConfiguration, EntityTypeBuilder

### Community 163 - "ICollectionFixture"
Cohesion: 0.29
Nodes (4): ICollectionFixture, PostgresCollection, PostgresCollection, PostgresCollection

### Community 164 - "OpportunityNeed"
Cohesion: 0.24
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

### Community 165 - "CrmActionCatalog.cs"
Cohesion: 0.67
Nodes (3): CrmActionCatalog, CrmActionDescriptor, IReadOnlyList

### Community 166 - ".Create"
Cohesion: 0.33
Nodes (4): PermissionSetTests, ArgumentException, Fact, InvalidOperationException

### Community 167 - "CreateOpportunityHandler"
Cohesion: 0.46
Nodes (5): CreateOpportunityHandler, TimeSpan, CreateOpportunityHandlerTests, Fact, Task

### Community 168 - "RolePermissionSet"
Cohesion: 0.33
Nodes (5): RolePermissionSet, PermissionSetId, RoleId, TenantId, TenantId

### Community 169 - ".Configure"
Cohesion: 0.40
Nodes (3): PrincipalType, User, EntityTypeBuilder

### Community 170 - ".Owner_relation_grant_is_consistent_across_both_contracts"
Cohesion: 0.40
Nodes (4): InlineData, OwnedBy, Task, Theory

### Community 171 - ".ApplyAnyOf"
Cohesion: 0.50
Nodes (3): IQueryable, AnyOf, OwnedBy

### Community 172 - "AuthorizationEffect"
Cohesion: 0.50
Nodes (3): AuthorizationEffect, Allow, Deny

## Knowledge Gaps
- **936 isolated node(s):** `None`, `Value`, `Principal`, `CorrelationId`, `Effect` (+931 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 1364 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **12 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Contracts` connect `Contracts` to `Access.Domain.Authorization`, `CustomerNeed`, `OpportunityLine`, `TenantFieldDefinition`, `.OwnedBy_scope_returns_only_that_principals_opportunities`, `PipelineStage`, `CRM.Persistence`, `.SeedOpenOpportunityAsync`, `CancelOpportunityLineCommand`, `EvidenceRecord`, `ActorContext`, `PipelineDefinitionVersion`, `PartyRef`, `RoleAssignment`, `.SeedAsync`, `RowVersionInterceptor`, `AuthorizationDenialStage`, `AuthorizationDecision`, `MembershipStatus`, `OpportunityNeed`, `.Create`, `RolePermissionSet`, `.HandleAsync`, `CRM.Application`, `AuthorizationEffect`, `.EvaluateAsync`, `IHasRowVersion`, `ActorContextMiddleware.cs`, `IdempotencyRecord`, `.ResolveExternalIdentityAsync`, `EntityRef`, `OutboxMessage`, `OutboxMessage`, `.Create`, `IdempotencyRecord`, `TenantId`, `EvidenceRecord`, `ActionKey`, `CRM.Domain`, `TenantAccessState`, `MasterData.Application`, `PermissionSet`, `PipelineDefinition`, `OpportunityStatus`, `LegacyMigrationFixture`, `OpportunityEndpoints.cs`, `.CreateAdminContext`, `.MapOpportunityEndpoints`, `ExternalIdentity`, `.HandleAsync`, `IEntityTypeConfiguration`, `IAuthorizer`, `Access.Tests.Domain`?**
  _High betweenness centrality (0.183) - this node is a cross-community bridge._
- **Why does `CRM.Persistence` connect `CRM.Persistence` to `Access.Domain.Authorization`, `EnableRowLevelSecurityOnPipelineTables`, `CrmDbContext`, `CRM.Persistence.Migrations`, `.SeedAsync`, `AddPipelineStageActiveAndEntryFlags`, `.BuildModel`, `CrmConnectionString.cs`, `AddPipelineTables`, `FixOpportunityAssignedPrincipalIndex`, `RenameOpportunityLifecycle`, `BackfillMasterDataParties`, `Contracts`, `ModuleBoundaryTests`, `DropOpportunityPartyForeignKey`, `DropCrmParties`, `.Create`, `TenantId`, `Migration`, `RemoveCrmPartyEntity`, `LegacyMigrationFixture`, `OpportunityEndpoints.cs`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `CustomerNeed`, `TenantFieldDefinition`, `.OwnedBy_scope_returns_only_that_principals_opportunities`, `WinOpportunityHandler`, `PipelineStage`, `CRM.Persistence`, `.SeedOpenOpportunityAsync`, `EvidenceRecord`, `PipelineDefinitionVersion`, `.SeedAsync`, `.A_single_dispatch_pass_marks_every_pending_message_processed`, `OpportunityNeed`, `CreateOpportunityHandler`, `IdempotencyRecord`, `OutboxMessage`, `MasterDataDbContext`, `OpportunityEndpointsTests`, `.Create`, `TenantId`, `.CreatePartyAsync`, `PipelineDefinition`, `LegacyMigrationFixture`, `.Create`, `.CreateAdminContext`, `.SeedAsync`, `IAuthorizer`?**
  _High betweenness centrality (0.061) - this node is a cross-community bridge._
- **Are the 25 inferred relationships involving `PrincipalRef` (e.g. with `.InvokeAsync()` and `.MapOpportunityEndpoints()`) actually correct?**
  _`PrincipalRef` has 25 INFERRED edges - model-reasoned connections that need verification._
- **What connects `None`, `Value`, `Principal` to the rest of the system?**
  _936 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.06951871657754011 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.08547008547008547 - nodes in this community are weakly interconnected._