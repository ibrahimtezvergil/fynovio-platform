# Graph Report - fynovio-platform  (2026-09-14)

## Corpus Check
- 70 files · ~23,382 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 479 nodes · 593 edges · 38 communities (26 shown, 11 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `6bf61351`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- Contracts
- Party
- TenantFieldDefinition
- OutboxMessage
- InitialCrmSchema
- CrmDbContext
- CRM+Sales pilot schema (PostgreSQL, `crm` schema)
- Contracts.csproj
- EvidenceRecord
- IdempotencyRecord
- RowVersionInterceptor
- http
- Worker
- Worker
- Access/Class1.cs
- Organization/Class1.cs
- MasterData/Class1.cs
- TenantLifecycle/Class1.cs
- Sales/Class1.cs
- CancellationToken
- What You Must Do When Invoked
- CustomerNeed
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

## God Nodes (most connected - your core abstractions)
1. `Opportunity` - 38 edges
2. `OpportunityLine` - 26 edges
3. `CrmDbContext` - 24 edges
4. `OutboxMessage` - 23 edges
5. `Contracts` - 22 edges
6. `Party` - 21 edges
7. `EvidenceRecord` - 19 edges
8. `IdempotencyRecord` - 17 edges
9. `TenantFieldDefinition` - 16 edges
10. `CustomerNeed` - 13 edges

## Surprising Connections (you probably didn't know these)
- `Opportunity` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Domain/OpportunityLine.cs
- `CrmDbContext` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Domain/OpportunityLine.cs
- `CrmDbContext` --references--> `EvidenceRecord`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Evidence/EvidenceRecord.cs
- `CrmDbContext` --references--> `IdempotencyRecord`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Idempotency/IdempotencyRecord.cs
- `CrmDbContext` --references--> `CustomerNeed`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Domain/CustomerNeed.cs

## Import Cycles
- None detected.

## Communities (38 total, 11 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.05
Nodes (37): IReadOnlyCollection, List, PrincipalRef, Issuer, Subject, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer (+29 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.06
Nodes (28): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version (+20 more)

### Community 2 - "Contracts"
Cohesion: 0.12
Nodes (9): CRM.Persistence.Configurations, Contracts, CRM.Idempotency, CRM.Evidence, CRM.Outbox, CRM.Persistence, CRM.Domain, TenantId (+1 more)

### Community 3 - "Party"
Cohesion: 0.10
Nodes (19): Party, CreatedAt, CreationSource, CustomFields, Email, Id, MergedIntoPartyId, Name (+11 more)

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (21): CRM.Customization, TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName (+13 more)

### Community 5 - "OutboxMessage"
Cohesion: 0.10
Nodes (20): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+12 more)

### Community 6 - "InitialCrmSchema"
Cohesion: 0.10
Nodes (14): CRM.Persistence.Migrations, Migration, MigrationBuilder, ModelSnapshot, DateTimeOffset, Guid, InitialCrmSchema, DateTimeOffset (+6 more)

### Community 7 - "CrmDbContext"
Cohesion: 0.11
Nodes (17): DbContext, DbSet, IDesignTimeDbContextFactory, CrmDbContext, CustomerNeeds, EvidenceRecords, IdempotencyRecords, Opportunities (+9 more)

### Community 8 - "CRM+Sales pilot schema (PostgreSQL, `crm` schema)"
Cohesion: 0.14
Nodes (13): Atomic durable intent ([14](.) decision #4), Contracts primitives already added (`src/Contracts/`), CRM+Sales pilot schema (PostgreSQL, `crm` schema), Cross-module references carry no FK ([07](.) §3, `EntityRef` in `Contracts`), Design notes carried over from revision 1 (still accurate), Diagram, Money: one rounding rule ([17](.) §3.4), No soft delete ([17](.) §3.5) (+5 more)

### Community 9 - "Contracts.csproj"
Cohesion: 0.22
Nodes (10): EFCore.NamingConventions (10.0.1), Microsoft.EntityFrameworkCore.Design (10.0.0), Microsoft.Extensions.Hosting (10.0.12), Npgsql.EntityFrameworkCore.PostgreSQL (10.0.3), Microsoft.NET.Sdk, Microsoft.NET.Sdk.Web, Microsoft.NET.Sdk.Worker, net10.0 (+2 more)

### Community 10 - "EvidenceRecord"
Cohesion: 0.12
Nodes (17): EvidenceRecord, Action, AggregateId, AggregateType, AggregateVersion, CorrelationId, Detail, Id (+9 more)

### Community 11 - "IdempotencyRecord"
Cohesion: 0.12
Nodes (16): IdempotencyRecord, CreatedAt, ExpiresAt, IdempotencyKey, Operation, PrincipalIssuer, PrincipalSubject, RequestHash (+8 more)

### Community 12 - "RowVersionInterceptor"
Cohesion: 0.25
Nodes (7): CancellationToken, DbContextEventData, InterceptionResult, SaveChangesInterceptor, RowVersionInterceptor, DbContext, ValueTask

### Community 13 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "Worker"
Cohesion: 0.22
Nodes (6): BackgroundService, Worker, ILogger, CancellationToken, Worker, Task

### Community 15 - "Worker"
Cohesion: 0.25
Nodes (7): DOTNET_ENVIRONMENT, profiles, Worker, $schema, commandName, dotnetRunMessages, environmentVariables

### Community 23 - "What You Must Do When Invoked"
Cohesion: 0.08
Nodes (24): For /graphify add and --watch, For /graphify query, For the commit hook and native CLAUDE.md integration, For --update and --cluster-only, /graphify, Honesty Rules, Interpreter guard for subcommands, Part A - Structural extraction for code files (+16 more)

### Community 24 - "CustomerNeed"
Cohesion: 0.11
Nodes (18): IEntityTypeConfiguration, CustomerNeed, AveragePrice, CreatedAt, Id, Name, TenantId, DateTimeOffset (+10 more)

### Community 25 - "fynovio-platform — Engineering Contract"
Cohesion: 0.15
Nodes (12): Architecture Rules (binding — enforced by fitness functions, doc 12), Code Conventions, Commands, Cross-Agent / Multi-Agent Policy, Database Rules, Formatting & Tooling, fynovio-platform — Engineering Contract, Generated / Derived Artifacts — don't hand-edit (+4 more)

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

## Knowledge Gaps
- **237 isolated node(s):** `graphify`, `Usage`, `What graphify is for`, `Step 0 - GitHub repos and multi-path merge (only if a URL or several paths)`, `Step 1 - Ensure graphify is installed` (+232 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 309 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **11 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `Contracts`, `TenantFieldDefinition`, `OutboxMessage`, `EvidenceRecord`, `IdempotencyRecord`, `CustomerNeed`?**
  _High betweenness centrality (0.165) - this node is a cross-community bridge._
- **Why does `Opportunity` connect `Opportunity` to `CustomerNeed`, `OpportunityLine`, `Contracts`?**
  _High betweenness centrality (0.114) - this node is a cross-community bridge._
- **Why does `OpportunityLine` connect `OpportunityLine` to `Opportunity`, `Contracts`, `CrmDbContext`?**
  _High betweenness centrality (0.093) - this node is a cross-community bridge._
- **What connects `graphify`, `Usage`, `What graphify is for` to the rest of the system?**
  _237 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.04846938775510204 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.06349206349206349 - nodes in this community are weakly interconnected._
- **Should `Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.12183908045977011 - nodes in this community are weakly interconnected._