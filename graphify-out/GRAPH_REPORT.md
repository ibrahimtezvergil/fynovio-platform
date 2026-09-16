# Graph Report - fynovio-platform  (2026-09-16)

## Corpus Check
- 119 files · ~44,211 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 716 nodes · 1031 edges · 60 communities (38 shown, 22 thin omitted)
- Extraction: 97% EXTRACTED · 3% INFERRED · 0% AMBIGUOUS · INFERRED: 31 edges (avg confidence: 0.82)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `933d89a4`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- Contracts
- Party
- TenantFieldDefinition
- OutboxMessage
- .HandleAsync
- DateTimeOffset
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- Contracts.csproj
- CustomerNeed
- Worker
- CRM.Persistence
- http
- EvidenceRecord
- Worker
- .NET rehberi — Laravel'den gelenler için
- Organization/Class1.cs
- MasterData/Class1.cs
- TenantLifecycle/Class1.cs
- fynovio-platform
- Pilot Enforcement Implementation Plan
- CancellationToken
- What You Must Do When Invoked
- OpportunityStateMachineTests
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
- CrmDbContext
- EntityTypeBuilder
- CrmDbContext
- .NextTenant
- Task
- EntityRef
- .ProductRef
- OpportunityStatus
- TenantId
- TenantId
- DbContext
- ModuleBoundaryTests
- ArgumentException
- Design notes
- Opportunity
- OpportunityStatus
- EntityRef
- OpportunityNeed
- TestData
- .Touch
- CRM.Domain
- TenantId

## God Nodes (most connected - your core abstractions)
1. `Opportunity` - 42 edges
2. `OpportunityLine` - 24 edges
3. `OutboxMessage` - 23 edges
4. `CrmDbContext` - 22 edges
5. `Contracts` - 22 edges
6. `Party` - 20 edges
7. `EvidenceRecord` - 19 edges
8. `IdempotencyRecord` - 17 edges
9. `TenantFieldDefinition` - 16 edges
10. `CRM.Domain` - 16 edges

## Surprising Connections (you probably didn't know these)
- `OpportunityConfiguration` --references--> `Opportunity`  [EXTRACTED]
  src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs → src/Modules/CRM/Domain/Opportunity.cs
- `OpportunityLine` --references--> `TenantId`  [EXTRACTED]
  src/Modules/CRM/Domain/OpportunityLine.cs → src/Modules/CRM/Domain/Opportunity.cs
- `CrmDbContext` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Domain/OpportunityLine.cs
- `EvidenceRecordConfiguration` --references--> `EvidenceRecord`  [EXTRACTED]
  src/Modules/CRM/Persistence/Configurations/EvidenceRecordConfiguration.cs → src/Modules/CRM/Evidence/EvidenceRecord.cs
- `CrmDbContext` --references--> `EvidenceRecord`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Evidence/EvidenceRecord.cs

## Import Cycles
- None detected.

## Communities (60 total, 22 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.08
Nodes (23): IHasRowVersion, IReadOnlyCollection, List, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer, AssignedPrincipalSubject, CancelDate (+15 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.10
Nodes (19): OpportunityLine, CancelReason, CreatedAt, Id, IsCanceled, IsOptional, LineTotal, OpportunityId (+11 more)

### Community 2 - "Contracts"
Cohesion: 0.18
Nodes (9): CRM.Persistence.Configurations, Contracts, CRM.Idempotency, CRM.Customization, CRM.Evidence, CRM.Outbox, IEntityTypeConfiguration, EvidenceRecordConfiguration (+1 more)

### Community 3 - "Party"
Cohesion: 0.10
Nodes (19): Party, CreatedAt, CreationSource, CustomFields, Email, Id, MergedIntoPartyId, Name (+11 more)

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (20): TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName, FieldType (+12 more)

### Community 5 - "OutboxMessage"
Cohesion: 0.10
Nodes (19): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+11 more)

### Community 6 - ".HandleAsync"
Cohesion: 0.10
Nodes (15): CancellationToken, CRM.Application, Guid, InvalidOperationException, ModelBuilder, CompletedPayload, CompleteOpportunityCommand, TenantId (+7 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.13
Nodes (14): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+6 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.10
Nodes (28): net10.0, coverlet.collector (6.0.4), Microsoft.Extensions.Hosting (10.0.12), Microsoft.NET.Test.Sdk (17.14.1), NetArchTest.Rules (1.3.2), Testcontainers.PostgreSql (4.15.0), xunit (2.9.3), xunit.runner.visualstudio (3.1.4) (+20 more)

### Community 10 - "CustomerNeed"
Cohesion: 0.16
Nodes (10): CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset, TenantId (+2 more)

### Community 11 - "Worker"
Cohesion: 0.25
Nodes (5): BackgroundService, Worker, ILogger, CancellationToken, Worker

### Community 12 - "CRM.Persistence"
Cohesion: 0.05
Nodes (24): CRM.Persistence, CRM.Persistence.Migrations, IDesignTimeDbContextFactory, Migration, MigrationBuilder, ModelSnapshot, CrmConnectionString, CrmDbContextFactory (+16 more)

### Community 13 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "EvidenceRecord"
Cohesion: 0.05
Nodes (35): PrincipalRef, Issuer, Subject, EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion (+27 more)

### Community 15 - "Worker"
Cohesion: 0.25
Nodes (7): DOTNET_ENVIRONMENT, profiles, Worker, $schema, commandName, dotnetRunMessages, environmentVariables

### Community 16 - ".NET rehberi — Laravel'den gelenler için"
Cohesion: 0.13
Nodes (14): 1. Solution ve proje kavramı, 2. Proje tipleri — bu repoda kullanılan üç farklı "SDK", 3. `Program.cs` — composition root, yani "her şeyin bağlandığı yer", 4. Entity Framework Core — Eloquent'in .NET karşılığı, 5. Migration'lar: `dotnet ef` akışı, 6. Bağlantı dizesi (connection string) nereden geliyor — bugün kurduğumuz yapı, 7. Konfigürasyon katmanları — `.env` yerine ne var, 8. Uçtan uca akış — bugün ne oldu (+6 more)

### Community 20 - "fynovio-platform"
Cohesion: 0.20
Nodes (9): Architecture, Documentation for contributors and coding agents, fynovio-platform, Getting started, Installing PostgreSQL locally (macOS / Homebrew), Prerequisites, Repository layout, Runtime role (Row-Level Security) (+1 more)

### Community 21 - "Pilot Enforcement Implementation Plan"
Cohesion: 0.14
Nodes (13): Pilot Enforcement Implementation Plan, Sonraki plan (bu planın kapsamı dışında), Task 0: Bağlayıcı çekirdeği kararlaştır (checkpoint — kod yok), Task 10: Dokümanları kodla senkronla, Task 1: Test projesi ve domain durum makinesi testleri, Task 2: Mimari sınır testi (FF01), Task 3: Testcontainers altyapısı + iptal hatasını gösteren kırmızı test, Task 4: CHECK kısıtını düzelt (migration) (+5 more)

### Community 23 - "What You Must Do When Invoked"
Cohesion: 0.08
Nodes (24): For /graphify add and --watch, For /graphify query, For the commit hook and native CLAUDE.md integration, For --update and --cluster-only, /graphify, Honesty Rules, Interpreter guard for subcommands, Part A - Structural extraction for code files (+16 more)

### Community 24 - "OpportunityStateMachineTests"
Cohesion: 0.33
Nodes (4): OpportunityStateMachineTests, ArgumentException, Fact, InvalidOperationException

### Community 25 - "fynovio-platform — Engineering Contract"
Cohesion: 0.14
Nodes (13): Architecture Rules (binding — enforced by fitness functions, doc 12), Code Conventions, Commands, Cross-Agent / Multi-Agent Policy, Database Rules, Enforcement Scope (approved 2026-09-16), Formatting & Tooling, fynovio-platform — Engineering Contract (+5 more)

### Community 26 - "fynovio-platform — Claude Code Notes"
Cohesion: 0.22
Nodes (8): fynovio-platform — Claude Code Notes, graphify, Memory, Methodology routing, Output style, Reasoning effort, Repository retrieval hierarchy, Safety

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

### Community 40 - "CrmDbContext"
Cohesion: 0.08
Nodes (23): Access.Persistence, DbContext, DbContextEventData, DbSet, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion (+15 more)

### Community 41 - ".NextTenant"
Cohesion: 0.08
Nodes (32): CrmDbContext, CRM.Tests.Integration, DbUpdateConcurrencyException, DbUpdateException, Fact, IAsyncLifetime, ICollectionFixture, OpportunityId (+24 more)

### Community 44 - ".ProductRef"
Cohesion: 0.23
Nodes (7): TenantId, OpportunityMoneyTests, ArgumentException, Fact, OpportunityRowVersionTests, Fact, InvalidOperationException

### Community 49 - "ModuleBoundaryTests"
Cohesion: 0.32
Nodes (4): Assembly, CRM.Tests.Architecture, TestResult, ModuleBoundaryTests

### Community 51 - "Design notes"
Cohesion: 0.17
Nodes (11): Account is the one deliberate exception to mandatory `tenant_id`, Concurrency token — added in Revision 2, was an omission not a decision, Design notes, Diagram, Identity + Access schema (PostgreSQL, `identity`/`access` schemas), Namespace layout, Revision 3, Not yet designed here, `PrincipalRef` mapping (+3 more)

### Community 53 - "OpportunityStatus"
Cohesion: 0.21
Nodes (8): EntityTypeBuilder, Party, OpportunityStatus, Canceled, Completed, Offered, Waiting, OpportunityConfiguration

### Community 54 - "EntityRef"
Cohesion: 0.17
Nodes (8): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version

### Community 55 - "OpportunityNeed"
Cohesion: 0.22
Nodes (7): OpportunityNeed, CustomerNeedId, OpportunityId, TenantId, TenantId, OpportunityNeedConfiguration, EntityTypeBuilder

### Community 56 - "TestData"
Cohesion: 0.20
Nodes (7): ArgumentOutOfRangeException, CRM.Tests, PrincipalRef, TenantId, EntityRef, TestData, Seller

### Community 57 - ".Touch"
Cohesion: 0.22
Nodes (3): DateTimeOffset, EntityRef, OpportunityLine

## Knowledge Gaps
- **292 isolated node(s):** `Stack`, `Code Conventions`, `Architecture Rules (binding — enforced by fitness functions, doc 12)`, `Database Rules`, `Enforcement Scope (approved 2026-09-16)` (+287 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 399 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **22 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `Contracts`, `TenantFieldDefinition`, `OutboxMessage`, `CustomerNeed`, `EvidenceRecord`, `OpportunityNeed`?**
  _High betweenness centrality (0.139) - this node is a cross-community bridge._
- **Why does `Opportunity` connect `Opportunity` to `.ProductRef`, `OpportunityStatus`, `OpportunityNeed`, `TestData`, `.Touch`, `OpportunityStateMachineTests`?**
  _High betweenness centrality (0.121) - this node is a cross-community bridge._
- **Why does `CRM.Domain` connect `CRM.Domain` to `OpportunityLine`, `Contracts`, `Party`, `.NextTenant`, `CustomerNeed`, `OpportunityStatus`, `OpportunityNeed`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **What connects `Stack`, `Code Conventions`, `Architecture Rules (binding — enforced by fitness functions, doc 12)` to the rest of the system?**
  _292 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.08333333333333333 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.1 - nodes in this community are weakly interconnected._
- **Should `Party` be split into smaller, more focused modules?**
  _Cohesion score 0.10153846153846154 - nodes in this community are weakly interconnected._