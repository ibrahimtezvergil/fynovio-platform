# Graph Report - fynovio-platform  (2026-09-14)

## Corpus Check
- 70 files · ~23,382 words
- Verdict: corpus is large enough that graph structure adds value.

## Summary
- 383 nodes · 511 edges · 23 communities (16 shown, 6 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 2 edges (avg confidence: 0.88)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `195325d9`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Opportunity
- OpportunityLine
- Contracts
- Party
- TenantFieldDefinition
- OutboxMessage
- CRM.Persistence
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
- `CrmDbContext` --references--> `TenantFieldDefinition`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Customization/TenantFieldDefinition.cs
- `CrmDbContext` --references--> `CustomerNeed`  [EXTRACTED]
  src/Modules/CRM/Persistence/CrmDbContext.cs → src/Modules/CRM/Domain/CustomerNeed.cs
- `Opportunity` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Domain/OpportunityLine.cs
- `Opportunity` --implements--> `IHasRowVersion`  [EXTRACTED]
  src/Modules/CRM/Domain/Opportunity.cs → src/Modules/CRM/Persistence/IHasRowVersion.cs
- `OpportunityLineConfiguration` --references--> `OpportunityLine`  [EXTRACTED]
  src/Modules/CRM/Persistence/Configurations/OpportunityLineConfiguration.cs → src/Modules/CRM/Domain/OpportunityLine.cs

## Import Cycles
- None detected.

## Communities (23 total, 6 thin omitted)

### Community 0 - "Opportunity"
Cohesion: 0.05
Nodes (35): IReadOnlyCollection, List, PrincipalRef, Issuer, Subject, Opportunity, AssignedPrincipal, AssignedPrincipalIssuer (+27 more)

### Community 1 - "OpportunityLine"
Cohesion: 0.07
Nodes (26): EntityRef, BoundedContext, EntityType, Id, TenantId, EntityVersion, Entity, Version (+18 more)

### Community 2 - "Contracts"
Cohesion: 0.06
Nodes (29): CRM.Persistence.Configurations, Contracts, CRM.Idempotency, CRM.Evidence, CRM.Outbox, CRM.Domain, IEntityTypeConfiguration, TenantId (+21 more)

### Community 3 - "Party"
Cohesion: 0.10
Nodes (19): Party, CreatedAt, CreationSource, CustomFields, Email, Id, MergedIntoPartyId, Name (+11 more)

### Community 4 - "TenantFieldDefinition"
Cohesion: 0.11
Nodes (21): CRM.Customization, TenantFieldAggregateType, Opportunity, Party, TenantFieldDefinition, AggregateType, CreatedAt, FieldName (+13 more)

### Community 5 - "OutboxMessage"
Cohesion: 0.10
Nodes (19): OutboxMessage, AggregateId, AggregateType, AggregateVersion, CausationId, CorrelationId, EventId, EventType (+11 more)

### Community 6 - "CRM.Persistence"
Cohesion: 0.10
Nodes (15): CRM.Persistence, CRM.Persistence.Migrations, Migration, MigrationBuilder, ModelSnapshot, DateTimeOffset, Guid, InitialCrmSchema (+7 more)

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
Cohesion: 0.16
Nodes (9): CancellationToken, DbContextEventData, InterceptionResult, SaveChangesInterceptor, IHasRowVersion, RowVersion, RowVersionInterceptor, DbContext (+1 more)

### Community 13 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 14 - "Worker"
Cohesion: 0.22
Nodes (6): BackgroundService, Worker, ILogger, CancellationToken, Worker, Task

### Community 15 - "Worker"
Cohesion: 0.25
Nodes (7): DOTNET_ENVIRONMENT, profiles, Worker, $schema, commandName, dotnetRunMessages, environmentVariables

## Knowledge Gaps
- **170 isolated node(s):** `Diagram`, `What changed in this revision, and why`, `Revision 3 (2026-09-14): command idempotency and evidence atomicity`, `Open architectural note: `Party` lives in the CRM schema as a pilot exception`, `Tenant isolation ([17](.) §3.1)` (+165 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 227 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **6 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CrmDbContext` connect `CrmDbContext` to `OpportunityLine`, `Contracts`, `TenantFieldDefinition`, `OutboxMessage`, `EvidenceRecord`, `IdempotencyRecord`?**
  _High betweenness centrality (0.258) - this node is a cross-community bridge._
- **Why does `Opportunity` connect `Opportunity` to `OpportunityLine`, `Contracts`, `RowVersionInterceptor`?**
  _High betweenness centrality (0.178) - this node is a cross-community bridge._
- **Why does `OpportunityLine` connect `OpportunityLine` to `Opportunity`, `Contracts`, `CrmDbContext`?**
  _High betweenness centrality (0.146) - this node is a cross-community bridge._
- **What connects `Diagram`, `What changed in this revision, and why`, `Revision 3 (2026-09-14): command idempotency and evidence atomicity` to the rest of the system?**
  _170 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Opportunity` be split into smaller, more focused modules?**
  _Cohesion score 0.05217391304347826 - nodes in this community are weakly interconnected._
- **Should `OpportunityLine` be split into smaller, more focused modules?**
  _Cohesion score 0.06818181818181818 - nodes in this community are weakly interconnected._
- **Should `Contracts` be split into smaller, more focused modules?**
  _Cohesion score 0.06285714285714286 - nodes in this community are weakly interconnected._