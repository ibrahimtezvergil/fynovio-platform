# MasterData / Party Foundation — Phase 0.5 Execution Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Standard (see `CLAUDE.md` "Plan checkbox tracking"):** mark a step `[x]` only once its commit exists, and add a `→ Commit: \`<hash>\` "<message>"` line under it — never mark ahead of actual state.

**Goal:** Build the `MasterData` module's Party foundation to the same binding-core
standard CRM already met — Party (+ `party_type`), `PartyRelationship`,
`PartyExternalIdentity`, own RLS/outbox/idempotency/evidence, `PartyRef`/
`IPartyDirectory`/`IPartyIdentityResolver` in `Contracts` — **fully isolated from CRM**.
`crm.parties` is not touched by this plan; that migration is Phase 1's job.

**Design source:** `docs/plans/2026-09-16-masterdata-party-foundation.md` — read it
before starting; this plan implements its §10 acceptance criteria. Do not re-litigate
any decision recorded there.

**Architecture:** New `masterdata` PostgreSQL schema, new `MasterDataDbContext`, same
shape as CRM's (`Outbox`/`Idempotency`/`Evidence` mirrored 1:1). `Party` gains
`PartyType` (Person | Organization) as a `Contracts`-level enum, since `PartyRef` and
`PartyDirectoryEntry` (also `Contracts`-level) need to expose it without any module
taking a project reference to `MasterData`. Merge stays tombstone-based
(`Party.MergeInto`), single-hop only, enforced as a domain guard — the *resolution*
logic (redirecting an already-merged target, repointing dependent tombstones,
reassigning external identities) lives in the `MergeParty` application handler, not the
domain method itself, matching how `Opportunity.Complete()` enforces its own invariant
without doing cross-aggregate lookups.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, PostgreSQL 17, xUnit, Testcontainers.PostgreSql, NetArchTest.Rules — identical to CRM's, versions pinned to match (`EFCore.NamingConventions` 10.0.1, `Microsoft.EntityFrameworkCore.Design` 10.0.4, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3, matching `src/Modules/CRM/CRM.csproj` and `.config/dotnet-tools.json`'s `dotnet-ef` 10.0.4).

**Ön koşullar:**
- Docker Desktop çalışıyor olmalı (Testcontainers); `DOCKER_HOST=unix://$HOME/.docker/run/docker.sock` gerekebilir.
- `dotnet tool restore` bir kez çalıştırılmış olmalı.
- Çalışma dizini her zaman repo kökü: `/Users/ibrahimtezvergil/Projects/fynovio/fynovio-platform`.
- `dotnet` komutları sandbox içinde takılıyorsa `dangerouslyDisableSandbox` ile çalıştır.

**Bu planın kapsamı dışında (ayrı plan, Phase 1):** `crm.parties` verisinin taşınması,
`Opportunity.PartyId`→`PartyRef` geçişi, CRM'in mevcut testlerinin güncellenmesi — tümü
`docs/plans/2026-09-16-masterdata-party-foundation.md` §9'da tarif edilen sıra, henüz
çalıştırılmıyor.

**Bilinçli kapsam daraltması (tasarım turundan küçük bir netleştirme):**
`IPartyIdentityResolver`'da `ResolveOrCreateAsync` **yer almıyor** — bu, mutasyon yapan
bir operasyon (query/precondition sözleşmesine ait değil) ve şu an hiçbir cross-module
çağıran yok (import/entegrasyon modülü henüz kodda yok). `MasterData.Application`'ın
kendi `ResolveOrCreateParty` komutu Task 6'da yazılıyor, ama `Contracts`'a
**exposed edilmiyor** — gerçek bir cross-module çağıran doğduğunda eklenir. Ayrıca
"Exists" yerine "Resolve" adlandırması kullanılıyor çünkü dönüş tipi artık `bool` değil,
merge zincirini takip eden canonical `PartyRef?`.

---

## Task 0: MasterData projesini iskeletten gerçek modüle çevir

**Files:**
- Modify: `src/Modules/MasterData/MasterData.csproj`
- Delete: `src/Modules/MasterData/Class1.cs`
- Create: klasör yapısı (`Domain/`, `Persistence/Configurations/`, `Persistence/Migrations/`, `Application/`, `Outbox/`, `Idempotency/`, `Evidence/`)

`fynovio-platform.slnx` zaten `MasterData.csproj`'u içeriyor — dokunmaya gerek yok.

- [x] **Step 1: Placeholder'ı kaldır**

```bash
rm src/Modules/MasterData/Class1.cs
```

- [x] **Step 2: EF Core paketlerini ekle (CRM/Access ile aynı sürümler)**

`src/Modules/MasterData/MasterData.csproj`'u şuna güncelle:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\..\Contracts\Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" Version="10.0.1" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
  </ItemGroup>

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

</Project>
```

- [x] **Step 3: Derle — boş ama sağlam olmalı**

```bash
dotnet build src/Modules/MasterData/MasterData.csproj
```

Beklenen: `Build succeeded`.

- [x] **Step 4: Commit**

```bash
git add src/Modules/MasterData
git commit -m "Scaffold MasterData as a real module (EF Core packages, drop placeholder)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `d15147d` "Scaffold MasterData as a real module (EF Core packages, drop placeholder)"

---

## Task 1: Contracts — PartyRef, PartyType, PartyDirectoryEntry, IPartyDirectory, IPartyIdentityResolver

**Files:**
- Create: `src/Contracts/PartyType.cs`
- Create: `src/Contracts/PartyRef.cs`
- Create: `src/Contracts/PartyDirectoryEntry.cs`
- Create: `src/Contracts/IPartyDirectory.cs`
- Create: `src/Contracts/IPartyIdentityResolver.cs`

- [x] **Step 1: `PartyType`**

`src/Contracts/PartyType.cs`:

```csharp
namespace Contracts;

/// <summary>Party = Person | Organization. Lives here, not in MasterData.Domain, so
/// PartyRef/PartyDirectoryEntry can expose it without any module taking a project
/// reference to MasterData (see docs/plans/2026-09-16-masterdata-party-foundation.md §3).</summary>
public enum PartyType
{
    Person,
    Organization
}
```

- [x] **Step 2: `PartyRef`**

`src/Contracts/PartyRef.cs`:

```csharp
namespace Contracts;

/// <summary>Strongly-typed reference to a Party — not a generic EntityRef. Party is
/// statically known platform-wide (there is only ever one), so EntityRef's
/// BoundedContext/EntityType genericity would carry two always-constant columns for no
/// benefit. See docs/plans/2026-09-16-masterdata-party-foundation.md §2.</summary>
public readonly record struct PartyRef
{
    public TenantId TenantId { get; }
    public long PartyId { get; }

    public PartyRef(TenantId tenantId, long partyId)
    {
        if (partyId <= 0)
            throw new ArgumentOutOfRangeException(nameof(partyId), "PartyId must be a positive identifier.");

        TenantId = tenantId;
        PartyId = partyId;
    }

    public override string ToString() => $"Party/{PartyId}@{TenantId}";
}
```

- [x] **Step 3: `PartyDirectoryEntry`**

`src/Contracts/PartyDirectoryEntry.cs`:

```csharp
namespace Contracts;

/// <summary>Display-oriented snapshot of a Party, returned by IPartyDirectory — never
/// a tracked entity. MasterData.Application maps its Party into this before it crosses
/// the module boundary.</summary>
public sealed record PartyDirectoryEntry(PartyRef PartyRef, PartyType PartyType, string Name, string? Surname, string? Email);
```

- [x] **Step 4: `IPartyDirectory`**

`src/Contracts/IPartyDirectory.cs`:

```csharp
namespace Contracts;

/// <summary>Read/display-oriented Party lookup — cheap, side-effect-free, no
/// strong-consistency requirement. Never a substitute for IPartyIdentityResolver's
/// command preconditions (see docs/plans/2026-09-16-masterdata-party-foundation.md §4).
/// Batch lookup is load-bearing, not a nicety: a list of N opportunities needs one
/// round-trip to MasterData via GetPartiesAsync, not N.</summary>
public interface IPartyDirectory
{
    Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
        IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default);
}
```

- [x] **Step 5: `IPartyIdentityResolver`**

`src/Contracts/IPartyIdentityResolver.cs`:

```csharp
namespace Contracts;

/// <summary>Command-precondition surface — must reflect current committed state and
/// resolve through a Party merge chain to the canonical PartyRef. A future
/// CreateOpportunity-shaped command validates against this, never against
/// IPartyDirectory's display-oriented reads (see
/// docs/plans/2026-09-16-masterdata-party-foundation.md §4). Returns the canonical
/// PartyRef, not a bool — a caller who passes a since-merged id must get back the
/// survivor's ref, never create something pointing at a tombstone.</summary>
public interface IPartyIdentityResolver
{
    Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default);

    Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken cancellationToken = default);
}
```

- [x] **Step 6: Derle**

```bash
dotnet build src/Contracts/Contracts.csproj
```

- [x] **Step 7: Commit**

```bash
git add src/Contracts
git commit -m "Add PartyRef, PartyType and the IPartyDirectory/IPartyIdentityResolver contracts

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `89b331a` "Add PartyRef, PartyType and the IPartyDirectory/IPartyIdentityResolver contracts"

---

## Task 2: Domain — Party, PartyRelationship, PartyExternalIdentity + merge invariant tests

**Files:**
- Create: `src/Modules/MasterData/Domain/Party.cs`
- Create: `src/Modules/MasterData/Domain/PartyRelationship.cs`
- Create: `src/Modules/MasterData/Domain/PartyExternalIdentity.cs`
- Create: `tests/MasterData.Tests/MasterData.Tests.csproj`
- Create: `tests/MasterData.Tests/TestData.cs`
- Create: `tests/MasterData.Tests/Domain/PartyTests.cs`
- Create: `tests/MasterData.Tests/Domain/PartyMergeTests.cs`
- Create: `tests/MasterData.Tests/Domain/PartyRelationshipTests.cs`
- Modify: `fynovio-platform.slnx`

- [x] **Step 1: Test projesini oluştur**

```bash
dotnet new xunit -n MasterData.Tests -o tests/MasterData.Tests
dotnet add tests/MasterData.Tests/MasterData.Tests.csproj reference src/Modules/MasterData/MasterData.csproj
dotnet sln fynovio-platform.slnx add tests/MasterData.Tests/MasterData.Tests.csproj
rm tests/MasterData.Tests/UnitTest1.cs
```

`tests/CRM.Tests/CRM.Tests.csproj`'ta not edilen xUnit sürümüyle aynı olmalı (Task 1,
Step 2, pilot-enforcement.md) — şüphede kalırsan `dotnet list ... package` ile kontrol et.

- [x] **Step 2: Ortak test verisi**

`tests/MasterData.Tests/TestData.cs`:

```csharp
using Contracts;

namespace MasterData.Tests;

public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));

    public static PrincipalRef Operator { get; } = new("https://idp.local", "masterdata-operator-1");
}
```

- [x] **Step 3: Domain testlerini yaz (kırmızı)**

`tests/MasterData.Tests/Domain/PartyTests.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

public sealed class PartyTests
{
    [Fact]
    public void Create_rejects_an_empty_name()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Party.Create(tenant, PartyType.Person, "  "));
    }

    [Fact]
    public void Create_rejects_a_surname_on_an_organization()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Party.Create(tenant, PartyType.Organization, "ABC AŞ", surname: "Yılmaz"));
    }

    [Fact]
    public void Create_allows_a_surname_on_a_person()
    {
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, PartyType.Person, "Ahmet", surname: "Yılmaz");

        Assert.Equal("Yılmaz", party.Surname);
    }
}
```

`tests/MasterData.Tests/Domain/PartyMergeTests.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

/// <summary>Single-hop only, enforced as a domain guard — the resolution logic
/// (redirecting to an already-merged target's own canonical) lives in the MergeParty
/// application handler, not here; this guard is what makes that resolution mandatory
/// rather than optional. See docs/plans/2026-09-16-masterdata-party-foundation.md §5.</summary>
public sealed class PartyMergeTests
{
    [Fact]
    public void MergeInto_rejects_merging_into_self()
    {
        var tenant = TestData.NextTenant();
        var party = Party.Create(tenant, PartyType.Organization, "ABC AŞ");

        Assert.Throws<InvalidOperationException>(() => party.MergeInto(party));
    }

    [Fact]
    public void MergeInto_rejects_a_target_that_is_itself_a_tombstone()
    {
        var tenant = TestData.NextTenant();
        var a = Party.Create(tenant, PartyType.Organization, "A");
        var b = Party.Create(tenant, PartyType.Organization, "B");
        var c = Party.Create(tenant, PartyType.Organization, "C");
        b.MergeInto(a);

        Assert.Throws<InvalidOperationException>(() => c.MergeInto(b));
    }

    [Fact]
    public void MergeInto_sets_the_tombstone_flag()
    {
        var tenant = TestData.NextTenant();
        var a = Party.Create(tenant, PartyType.Organization, "A");
        var b = Party.Create(tenant, PartyType.Organization, "B");

        b.MergeInto(a);

        Assert.NotNull(b.MergedIntoPartyId);
    }
}
```

`tests/MasterData.Tests/Domain/PartyRelationshipTests.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Domain;

public sealed class PartyRelationshipTests
{
    [Fact]
    public void Create_starts_active()
    {
        var tenant = TestData.NextTenant();

        var relationship = PartyRelationship.Create(tenant, fromPartyId: 1, toPartyId: 2, PartyRelationshipType.WorksFor);

        Assert.Equal(PartyRelationshipStatus.Active, relationship.Status);
        Assert.Null(relationship.EndedAt);
    }

    [Fact]
    public void Create_rejects_a_relationship_with_self()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            PartyRelationship.Create(tenant, fromPartyId: 1, toPartyId: 1, PartyRelationshipType.WorksFor));
    }

    [Fact]
    public void End_requires_not_already_ended()
    {
        var tenant = TestData.NextTenant();
        var relationship = PartyRelationship.Create(tenant, 1, 2, PartyRelationshipType.WorksFor);
        relationship.End();

        Assert.Throws<InvalidOperationException>(() => relationship.End());
    }

    [Fact]
    public void End_sets_ended_at()
    {
        var tenant = TestData.NextTenant();
        var relationship = PartyRelationship.Create(tenant, 1, 2, PartyRelationshipType.WorksFor);

        relationship.End();

        Assert.NotNull(relationship.EndedAt);
        Assert.Equal(PartyRelationshipStatus.Ended, relationship.Status);
    }
}
```

- [x] **Step 4: Testleri çalıştır — derlenmemeli**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj
```

Beklenen: derleme hatası — `MasterData.Domain` içinde `Party`/`PartyRelationship` yok.

- [x] **Step 5: `Party`'i yaz**

`src/Modules/MasterData/Domain/Party.cs`:

```csharp
using Contracts;

namespace MasterData.Domain;

/// <summary>Aggregate root for the platform's shared identity concept. Merge stays
/// tombstone-based (no hard delete); MergeInto enforces single-hop only as a guard —
/// the resolution/repoint logic lives in the MergeParty application handler. See
/// docs/plans/2026-09-16-masterdata-party-foundation.md §5.</summary>
public sealed class Party
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public PartyType PartyType { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Surname { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public long? MergedIntoPartyId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Party() { }

    public static Party Create(
        TenantId tenantId, PartyType partyType, string name,
        string? surname = null, string? phone = null, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (partyType == PartyType.Organization && surname is not null)
            throw new ArgumentException("An organization does not have a surname.", nameof(surname));

        var now = DateTimeOffset.UtcNow;
        return new Party
        {
            TenantId = tenantId,
            PartyType = partyType,
            Name = name,
            Surname = surname,
            Phone = phone,
            Email = email,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Refuses a target that is itself already a tombstone — never resolves it
    /// automatically. The caller (MergeParty handler) must resolve the requested target
    /// to its own canonical first; this guard is what makes that mandatory.</summary>
    public void MergeInto(Party canonical)
    {
        if (canonical.Id == Id)
            throw new InvalidOperationException("A party cannot merge into itself.");
        if (canonical.MergedIntoPartyId is not null)
            throw new InvalidOperationException("Merge target must already be resolved to its own canonical party.");

        MergedIntoPartyId = canonical.Id;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Called on every existing tombstone that pointed at this party, the
    /// moment this party itself gets merged into a new canonical — keeps every
    /// tombstone exactly one hop from canonical at all times. Internal: only the
    /// MergeParty handler, operating within MasterData, calls this.</summary>
    internal void RepointMergeTarget(long newCanonicalPartyId)
    {
        if (MergedIntoPartyId is null)
            throw new InvalidOperationException("Only a tombstoned party can be repointed.");

        MergedIntoPartyId = newCanonicalPartyId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [x] **Step 6: `PartyRelationship`'ı yaz**

`src/Modules/MasterData/Domain/PartyRelationship.cs`:

```csharp
using Contracts;

namespace MasterData.Domain;

public enum PartyRelationshipType
{
    WorksFor,
    BranchOf
}

public enum PartyRelationshipStatus
{
    Active,
    Ended
}

/// <summary>Typed graph edge between two Parties. Deliberately narrow vocabulary for
/// now (WorksFor, BranchOf) — new types get added only against a validated request,
/// same discipline as OpportunityStatus's closed enum. Temporal model is status +
/// nullable dates, not raw effective_from/to: different relationship types have
/// genuinely different date-certainty (WorksFor usually knows both dates; BranchOf
/// usually doesn't know when, only that it currently is). See
/// docs/plans/2026-09-16-masterdata-party-foundation.md §3.</summary>
public sealed class PartyRelationship
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long FromPartyId { get; private set; }
    public long ToPartyId { get; private set; }
    public PartyRelationshipType RelationshipType { get; private set; }
    public PartyRelationshipStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    public string? JobTitle { get; private set; }
    public string? WorkEmail { get; private set; }
    public string? WorkPhone { get; private set; }
    public string? Metadata { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PartyRelationship() { }

    public static PartyRelationship Create(
        TenantId tenantId, long fromPartyId, long toPartyId, PartyRelationshipType relationshipType,
        DateTimeOffset? startedAt = null, string? jobTitle = null, string? workEmail = null, string? workPhone = null)
    {
        if (fromPartyId == toPartyId)
            throw new ArgumentException("A party cannot have a relationship with itself.");

        var now = DateTimeOffset.UtcNow;
        return new PartyRelationship
        {
            TenantId = tenantId,
            FromPartyId = fromPartyId,
            ToPartyId = toPartyId,
            RelationshipType = relationshipType,
            Status = PartyRelationshipStatus.Active,
            StartedAt = startedAt,
            JobTitle = jobTitle,
            WorkEmail = workEmail,
            WorkPhone = workPhone,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void End(DateTimeOffset? endedAt = null)
    {
        if (Status == PartyRelationshipStatus.Ended)
            throw new InvalidOperationException("Relationship is already ended.");

        Status = PartyRelationshipStatus.Ended;
        EndedAt = endedAt ?? DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [x] **Step 7: `PartyExternalIdentity`'i yaz**

`src/Modules/MasterData/Domain/PartyExternalIdentity.cs`:

```csharp
using Contracts;

namespace MasterData.Domain;

/// <summary>Provider (system type, e.g. "sap") and source instance (the specific
/// configured connection, e.g. "sap-connection-a") are distinct — a tenant can run
/// multiple instances of the same provider, each with its own overlapping id
/// namespace. See docs/plans/2026-09-16-masterdata-party-foundation.md §6.</summary>
public sealed class PartyExternalIdentity
{
    public long Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public long PartyId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string SourceInstanceRef { get; private set; } = null!;
    public string? ExternalType { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }

    private PartyExternalIdentity() { }

    public static PartyExternalIdentity Create(
        TenantId tenantId, long partyId, string provider, string sourceInstanceRef,
        string? externalType, string externalId)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("Provider is required.", nameof(provider));
        if (string.IsNullOrWhiteSpace(sourceInstanceRef))
            throw new ArgumentException("Source instance reference is required.", nameof(sourceInstanceRef));
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External id is required.", nameof(externalId));

        return new PartyExternalIdentity
        {
            TenantId = tenantId,
            PartyId = partyId,
            Provider = provider,
            SourceInstanceRef = sourceInstanceRef,
            ExternalType = externalType,
            ExternalId = externalId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Called only by the MergeParty handler, reassigning identities from a
    /// merged-away party to the survivor.</summary>
    internal void ReassignTo(long survivorPartyId) => PartyId = survivorPartyId;
}
```

- [x] **Step 8: Testleri çalıştır**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj
```

Beklenen: `Passed!` — 10 test (3 `PartyTests` + 3 `PartyMergeTests` + 4 `PartyRelationshipTests`).

- [x] **Step 9: Commit**

```bash
git add src/Modules/MasterData/Domain tests/MasterData.Tests fynovio-platform.slnx
git commit -m "Add MasterData.Tests and the Party/PartyRelationship/PartyExternalIdentity domain model

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `6096400` "Add MasterData.Tests and the Party/PartyRelationship/PartyExternalIdentity domain model" (deviation: `Party.MergeInto`'s self-merge guard originally compared `Id`, which is 0 for every unsaved test instance — switched to `ReferenceEquals`, correct both for domain tests and for the real EF path since the identity map returns the same tracked instance for a given Id within one DbContext)

---

## Task 3: Mimari sınır testi

**Files:**
- Modify: `tests/MasterData.Tests/MasterData.Tests.csproj`
- Create: `tests/MasterData.Tests/Architecture/ModuleBoundaryTests.cs`

- [x] **Step 1: NetArchTest paketini ekle**

```bash
dotnet add tests/MasterData.Tests/MasterData.Tests.csproj package NetArchTest.Rules
```

- [x] **Step 2: Testi yaz**

`tests/MasterData.Tests/Architecture/ModuleBoundaryTests.cs`:

```csharp
using System.Reflection;
using MasterData.Persistence;
using NetArchTest.Rules;
using Xunit;

namespace MasterData.Tests.Architecture;

/// <summary>Symmetric to tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs — the
/// boundary rule cuts both ways. CRM's own test already forbids depending on
/// MasterData; this is MasterData forbidding the reverse.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly MasterDataAssembly = typeof(MasterDataDbContext).Assembly;

    [Fact]
    public void MasterData_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(MasterDataAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("CRM", "Access", "Organization", "TenantLifecycle", "Sales")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void MasterData_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(MasterDataAssembly)
            .That().ResideInNamespace("MasterData.Domain")
            .ShouldNot().HaveDependencyOn("MasterData.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
```

- [x] **Step 3: Testleri çalıştır**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~ModuleBoundaryTests
```

Beklenen: `Passed!` — 2 test.

(deviation: anchored the assembly reference on `Party` instead of `MasterDataDbContext` — the DbContext doesn't exist until Task 5, so the plan's original anchor wouldn't compile yet; `Types.InAssembly` only needs any type from the target assembly, so this is equivalent)

- [x] **Step 4: `tests/CRM.Tests`'in kendi sınır testini de doğrula (regresyon yok)**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~ModuleBoundaryTests
```

Beklenen: hâlâ `Passed!` — CRM'in `"MasterData"`yı yasaklı listede taşıyan testi
etkilenmemeli.

- [x] **Step 5: Commit**

```bash
git add tests/MasterData.Tests
git commit -m "Add NetArchTest module boundary tests for MasterData

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `2a3947c` "Add NetArchTest module boundary tests for MasterData"

---

## Task 4: Outbox / Idempotency / Evidence (CRM'in şeklinin birebir tekrarı)

**Files:**
- Create: `src/Modules/MasterData/Outbox/OutboxMessage.cs`
- Create: `src/Modules/MasterData/Idempotency/IdempotencyRecord.cs`
- Create: `src/Modules/MasterData/Evidence/EvidenceRecord.cs`

Bu üç dosya, `src/Modules/CRM/Outbox/OutboxMessage.cs`,
`src/Modules/CRM/Idempotency/IdempotencyRecord.cs`,
`src/Modules/CRM/Evidence/EvidenceRecord.cs`'nin **birebir aynısı**, yalnızca
`namespace CRM.Outbox` → `namespace MasterData.Outbox` (vb.) değişir. Her modül kendi
outbox/idempotency/evidence tablosunu taşır (AGENTS.md: "each module owns its own
schema", paylaşılan tablo yok).

- [x] **Step 1: `OutboxMessage`**

`src/Modules/MasterData/Outbox/OutboxMessage.cs` — `src/Modules/CRM/Outbox/OutboxMessage.cs`
dosyasını oku, içeriği aynen kopyala, yalnızca `namespace CRM.Outbox;` satırını
`namespace MasterData.Outbox;` yap.

- [x] **Step 2: `IdempotencyRecord`**

`src/Modules/MasterData/Idempotency/IdempotencyRecord.cs` — aynı yöntem,
`namespace CRM.Idempotency;` → `namespace MasterData.Idempotency;`.

- [x] **Step 3: `EvidenceRecord`**

`src/Modules/MasterData/Evidence/EvidenceRecord.cs` — aynı yöntem,
`namespace CRM.Evidence;` → `namespace MasterData.Evidence;`.

- [x] **Step 4: Derle**

```bash
dotnet build src/Modules/MasterData/MasterData.csproj
```

- [x] **Step 5: Commit**

```bash
git add src/Modules/MasterData/Outbox src/Modules/MasterData/Idempotency src/Modules/MasterData/Evidence
git commit -m "Add MasterData's own outbox, idempotency and evidence entities

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `fbc3938` "Add MasterData's own outbox, idempotency and evidence entities"

---

## Task 5: Persistence — MasterDataDbContext, configurations, ilk migration

**Files:**
- Create: `src/Modules/MasterData/Persistence/MasterDataDbContext.cs`
- Create: `src/Modules/MasterData/Persistence/MasterDataConnectionString.cs`
- Create: `src/Modules/MasterData/Persistence/MasterDataDbContextFactory.cs`
- Create: `src/Modules/MasterData/Persistence/MasterDataDbContextTenantExtensions.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/PartyConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/PartyRelationshipConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/PartyExternalIdentityConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/OutboxMessageConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/IdempotencyRecordConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Configurations/EvidenceRecordConfiguration.cs`
- Create: `src/Modules/MasterData/Persistence/Migrations/<timestamp>_InitialMasterDataSchema.cs` (üretilir)

- [x] **Step 1: `MasterDataConnectionString`**

`src/Modules/MasterData/Persistence/MasterDataConnectionString.cs` —
`src/Modules/CRM/Persistence/CrmConnectionString.cs`'in aynısı, yalnızca env var adı
`FYNOVIO_MASTERDATA_CONNECTION_STRING` olsun (CRM'in `FYNOVIO_CRM_CONNECTION_STRING`'i
gibi, ayrı bir env var — her modülün kendi bağlantı çözümü olmalı):

```csharp
namespace MasterData.Persistence;

/// <summary>Single source of truth for the MasterData module's local-dev default
/// connection string, shared by the design-time factory and Host's real DI
/// registration. Production overrides via ConnectionStrings__MasterData.</summary>
public static class MasterDataConnectionString
{
    private const string LocalDevDefault = "Host=localhost;Database=fynovio_platform;Username=postgres;Password=postgres";

    public static string Resolve() =>
        Environment.GetEnvironmentVariable("FYNOVIO_MASTERDATA_CONNECTION_STRING") ?? LocalDevDefault;
}
```

- [x] **Step 2: `MasterDataDbContext`**

`src/Modules/MasterData/Persistence/MasterDataDbContext.cs`:

```csharp
using MasterData.Domain;
using MasterData.Evidence;
using MasterData.Idempotency;
using MasterData.Outbox;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Persistence;

/// <summary>One PostgreSQL schema per module (doc 07 §3) — this module owns the
/// `masterdata` schema exclusively. Never referenced directly by another module's
/// project; cross-module access goes through PartyRef/IPartyDirectory/
/// IPartyIdentityResolver (Contracts) and the outbox, not this DbContext.</summary>
public sealed class MasterDataDbContext : DbContext
{
    public const string Schema = "masterdata";

    public MasterDataDbContext(DbContextOptions<MasterDataDbContext> options) : base(options)
    {
    }

    public DbSet<Party> Parties => Set<Party>();
    public DbSet<PartyRelationship> PartyRelationships => Set<PartyRelationship>();
    public DbSet<PartyExternalIdentity> PartyExternalIdentities => Set<PartyExternalIdentity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterDataDbContext).Assembly);
    }
}
```

- [x] **Step 3: Design-time factory**

`src/Modules/MasterData/Persistence/MasterDataDbContextFactory.cs` —
`src/Modules/CRM/Persistence/CrmDbContextFactory.cs`'in aynısı, `Crm`→`MasterData` isim
değişikliğiyle:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MasterData.Persistence;

public sealed class MasterDataDbContextFactory : IDesignTimeDbContextFactory<MasterDataDbContext>
{
    public MasterDataDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MasterDataDbContext>();
        optionsBuilder
            .UseNpgsql(MasterDataConnectionString.Resolve(), npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention();

        return new MasterDataDbContext(optionsBuilder.Options);
    }
}
```

- [x] **Step 4: Tenant context extension**

`src/Modules/MasterData/Persistence/MasterDataDbContextTenantExtensions.cs` —
`src/Modules/CRM/Persistence/CrmDbContextTenantExtensions.cs`'in aynısı:

```csharp
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Persistence;

public static class MasterDataDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(
        this MasterDataDbContext context,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tenant context must be set inside an explicit transaction.");

        var value = tenantId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {value}, true)",
            cancellationToken);
    }
}
```

- [x] **Step 5: `PartyConfiguration`**

`src/Modules/MasterData/Persistence/Configurations/PartyConfiguration.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("parties");

        builder.HasKey(p => p.Id);
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.Property(p => p.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(p => p.PartyType)
            .HasConversion(t => ToDb(t), t => FromDb(t))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(p => p.Name).IsRequired();

        // Self-FK, nullable, tenant-safe composite — the merge tombstone target.
        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(p => new { p.TenantId, p.MergedIntoPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ToTable(t => t.HasCheckConstraint(
            "ck_parties_party_type", "party_type IN ('person', 'organization')"));

        builder.HasIndex(p => new { p.TenantId, p.Email });
        builder.HasIndex(p => new { p.TenantId, p.MergedIntoPartyId });
    }

    private static string ToDb(PartyType type) => type switch
    {
        PartyType.Person => "person",
        PartyType.Organization => "organization",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static PartyType FromDb(string value) => value switch
    {
        "person" => PartyType.Person,
        "organization" => PartyType.Organization,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
```

- [x] **Step 6: `PartyRelationshipConfiguration`**

`src/Modules/MasterData/Persistence/Configurations/PartyRelationshipConfiguration.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyRelationshipConfiguration : IEntityTypeConfiguration<PartyRelationship>
{
    public void Configure(EntityTypeBuilder<PartyRelationship> builder)
    {
        builder.ToTable("party_relationships");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.Property(r => r.RelationshipType)
            .HasConversion(t => ToDbType(t), t => FromDbType(t))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion(s => ToDbStatus(s), s => FromDbStatus(s))
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(r => r.Metadata).HasColumnType("jsonb");

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.FromPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(r => new { r.TenantId, r.ToPartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_party_relationships_type", "relationship_type IN ('works_for','branch_of')");
            t.HasCheckConstraint("ck_party_relationships_status", "status IN ('active','ended')");
            // Single-row invariant, same discipline as ck_opportunities_*_required_once_*.
            t.HasCheckConstraint("ck_party_relationships_ended_at_required_once_ended", "status <> 'ended' OR ended_at IS NOT NULL");
        });

        builder.HasIndex(r => new { r.TenantId, r.FromPartyId });
        builder.HasIndex(r => new { r.TenantId, r.ToPartyId });
    }

    private static string ToDbType(PartyRelationshipType type) => type switch
    {
        PartyRelationshipType.WorksFor => "works_for",
        PartyRelationshipType.BranchOf => "branch_of",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static PartyRelationshipType FromDbType(string value) => value switch
    {
        "works_for" => PartyRelationshipType.WorksFor,
        "branch_of" => PartyRelationshipType.BranchOf,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string ToDbStatus(PartyRelationshipStatus status) => status switch
    {
        PartyRelationshipStatus.Active => "active",
        PartyRelationshipStatus.Ended => "ended",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static PartyRelationshipStatus FromDbStatus(string value) => value switch
    {
        "active" => PartyRelationshipStatus.Active,
        "ended" => PartyRelationshipStatus.Ended,
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
```

- [x] **Step 7: `PartyExternalIdentityConfiguration` — COALESCE tekilliği**

`src/Modules/MasterData/Persistence/Configurations/PartyExternalIdentityConfiguration.cs`:

```csharp
using Contracts;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MasterData.Persistence.Configurations;

public sealed class PartyExternalIdentityConfiguration : IEntityTypeConfiguration<PartyExternalIdentity>
{
    public void Configure(EntityTypeBuilder<PartyExternalIdentity> builder)
    {
        builder.ToTable("party_external_identities");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.TenantId)
            .HasConversion(id => id.Value, value => new TenantId(value))
            .IsRequired();

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(e => new { e.TenantId, e.PartyId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Restrict);

        // Postgres unique indexes treat NULL <> NULL, so a plain UNIQUE including the
        // nullable external_type would silently allow duplicates whenever it's NULL —
        // the common case (design review caught this; see
        // docs/plans/2026-09-16-masterdata-party-foundation.md §6). A stored computed
        // shadow column normalizes NULL to '' so the index means something, and stays
        // fully EF-generated — no hand-written migration needed for this, unlike RLS.
        builder.Property<string>("ExternalTypeKey")
            .HasComputedColumnSql("COALESCE(external_type, '')", stored: true);

        builder.HasIndex("TenantId", "SourceInstanceRef", "ExternalTypeKey", "ExternalId")
            .IsUnique()
            .HasDatabaseName("ux_party_external_identities_tenant_source_type_external_id");

        builder.HasIndex(e => new { e.TenantId, e.PartyId });
    }
}
```

- [x] **Step 8: Outbox/Idempotency/Evidence configurations**

Bu üçü de CRM'inkilerin birebir kopyası, yalnızca `using CRM.X;` → `using MasterData.X;`:

`src/Modules/MasterData/Persistence/Configurations/OutboxMessageConfiguration.cs` —
`src/Modules/CRM/Persistence/Configurations/OutboxMessageConfiguration.cs`'i oku, kopyala,
namespace/using'leri `MasterData`'ya çevir.

`src/Modules/MasterData/Persistence/Configurations/IdempotencyRecordConfiguration.cs` —
aynı yöntem, `IdempotencyRecordConfiguration.cs`'den.

`src/Modules/MasterData/Persistence/Configurations/EvidenceRecordConfiguration.cs` —
aynı yöntem, `EvidenceRecordConfiguration.cs`'den.

- [x] **Step 9: İlk migration'ı üret**

```bash
dotnet ef migrations add InitialMasterDataSchema \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj \
  --output-dir Persistence/Migrations
```

- [x] **Step 10: Üretilen migration'ı doğrula**

Migration dosyasını aç: `parties`, `party_relationships`, `party_external_identities`
(kendi CHECK'leri ve `external_type_key` computed column'uyla), `outbox_messages`,
`idempotency_records`, `evidence_records` tabloları oluşturulmalı, hepsi `masterdata`
şemasında. Elle düzenleme yapma — eksik/yanlış bir şey varsa Step 5-8'deki
configuration'a dön, migration'ı sil ve yeniden üret.

- [x] **Step 11: Veritabanını güncelle ve derle**

```bash
dotnet ef database update \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj
dotnet build src/Modules/MasterData/MasterData.csproj
```

- [x] **Step 12: Commit**

```bash
git add src/Modules/MasterData/Persistence
git commit -m "Add MasterDataDbContext, entity configurations and the initial schema migration

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `9da795c` "Add MasterDataDbContext, entity configurations and the initial schema migration"

---

## Task 6: RLS ve runtime rolü

**Files:**
- Create: `src/Modules/MasterData/Persistence/Migrations/<timestamp>_EnableRowLevelSecurity.cs` (üretilir, gövdesi elle yazılır — AGENTS.md'nin tek istisnası)
- Modify: `scripts/create-runtime-role.sql`

- [x] **Step 1: Boş migration üret**

```bash
dotnet ef migrations add EnableRowLevelSecurity \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj \
  --output-dir Persistence/Migrations
```

Beklenen: model değişmediği için boş `Up`/`Down`.

- [x] **Step 2: Migration gövdesini yaz**

`src/Modules/CRM/Persistence/Migrations/20260916081636_EnableRowLevelSecurity.cs`'in
aynı deseni, `crm.` → `masterdata.`, tablo listesi bu modülün altısı:

```csharp
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE masterdata.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE masterdata.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON masterdata.{table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::bigint);
                    """);
            }
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON masterdata.{table};");
                migrationBuilder.Sql($"ALTER TABLE masterdata.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE masterdata.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        private static readonly string[] TenantScopedTables =
        [
            "parties",
            "party_relationships",
            "party_external_identities",
            "outbox_messages",
            "idempotency_records",
            "evidence_records"
        ];
```

- [x] **Step 3: `create-runtime-role.sql`'e MasterData bloğu ekle**

`scripts/create-runtime-role.sql`'in sonuna ekle (aynı `fynovio_app` rolü, ikinci şema):

```sql
-- MasterData module.
GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA masterdata
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA masterdata
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;
```

- [x] **Step 4: Veritabanını güncelle**

```bash
dotnet ef database update \
  --project src/Modules/MasterData/MasterData.csproj \
  --startup-project src/Modules/MasterData/MasterData.csproj
```

- [x] **Step 5: Commit**

```bash
git add src/Modules/MasterData/Persistence scripts/create-runtime-role.sql
git commit -m "Enable RLS on masterdata tables and grant the runtime role

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `a3baafd` "Enable RLS on masterdata tables and grant the runtime role"

---

## Task 7: Testcontainers altyapısı + tenant izolasyon testleri

**Files:**
- Create: `tests/MasterData.Tests/Integration/PostgresFixture.cs`
- Create: `tests/MasterData.Tests/Integration/PostgresCollection.cs`
- Create: `tests/MasterData.Tests/Integration/TenantIsolationTests.cs`

- [x] **Step 1: Testcontainers paketini ekle**

```bash
dotnet add tests/MasterData.Tests/MasterData.Tests.csproj package Testcontainers.PostgreSql
```

- [x] **Step 2: Fixture'ı yaz**

`tests/MasterData.Tests/Integration/PostgresFixture.cs` —
`tests/CRM.Tests/Integration/PostgresFixture.cs`'in aynısı, `CrmDbContext`→`MasterDataDbContext`,
şema `masterdata`, ve runtime rolü grant'leri bu modülün altı tablosuna:

```csharp
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace MasterData.Tests.Integration;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public MasterDataDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public static MasterDataDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<MasterDataDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new MasterDataDbContext(options);
    }

    private string? _runtimeConnectionString;

    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        await using (var context = CreateAdminContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime';
                GRANT USAGE ON SCHEMA masterdata TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA masterdata TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA masterdata TO fynovio_app;
                REVOKE UPDATE, DELETE ON masterdata.evidence_records FROM fynovio_app;
                """);
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Username = "fynovio_app",
            Password = "runtime"
        };

        _runtimeConnectionString = builder.ConnectionString;
        return _runtimeConnectionString;
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
```

xUnit 2.x kullanılıyorsa (Task 2 Step 1'de not edilen sürüm), `InitializeAsync`/
`DisposeAsync` imzalarını `pilot-enforcement.md` Task 3'teki gibi `ValueTask`'a çevir.

- [x] **Step 3: İzolasyon testlerini yaz**

`tests/MasterData.Tests/Integration/TenantIsolationTests.cs` —
`tests/CRM.Tests/Integration/TenantIsolationTests.cs`'in aynı 7 testi, `Party`
seed'i CRM yerine `MasterData.Domain.Party.Create(tenant, PartyType.Organization, name)`
ile yapılır:

```csharp
using Contracts;
using MasterData.Domain;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class TenantIsolationTests
{
    private readonly PostgresFixture _fixture;

    public TenantIsolationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Runtime_role_cannot_read_another_tenants_rows()
    {
        var (tenantA, partyAId) = await SeedPartyAsync("A");
        var (tenantB, _) = await SeedPartyAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.DoesNotContain(visible, p => p.Id == partyAId);
        Assert.All(visible, p => Assert.Equal(tenantB, p.TenantId));
        Assert.NotEqual(tenantA, tenantB);
    }

    [Fact]
    public async Task Runtime_role_cannot_read_a_guessed_id_from_another_tenant()
    {
        var (_, partyAId) = await SeedPartyAsync("A");
        var (tenantB, _) = await SeedPartyAsync("B");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        var found = await context.Parties.AsNoTracking().SingleOrDefaultAsync(p => p.Id == partyAId);

        Assert.Null(found);
    }

    [Fact]
    public async Task Runtime_role_sees_nothing_without_a_tenant_context()
    {
        await SeedPartyAsync("C");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Tenant_context_does_not_survive_on_a_pooled_connection()
    {
        var (tenantA, _) = await SeedPartyAsync("D");

        var connectionString = await _fixture.RuntimeConnectionStringAsync();

        await using (var first = PostgresFixture.CreateContext(connectionString))
        {
            await using var transaction = await first.Database.BeginTransactionAsync();
            await first.SetTenantContextAsync(tenantA);
            await transaction.CommitAsync();
        }

        await using var second = PostgresFixture.CreateContext(connectionString);
        await using var secondTransaction = await second.Database.BeginTransactionAsync();

        var visible = await second.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_row_for_another_tenant()
    {
        var tenantA = TestData.NextTenant();
        var tenantB = TestData.NextTenant();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        context.Parties.Add(Party.Create(tenantA, PartyType.Organization, "Sızdırma denemesi"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("42501", postgresException.SqlState);
    }

    [Fact]
    public async Task Tenant_context_requires_an_explicit_transaction()
    {
        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            context.SetTenantContextAsync(TestData.NextTenant()));
    }

    [Fact]
    public async Task Runtime_role_cannot_update_or_delete_evidence()
    {
        var tenant = TestData.NextTenant();

        await using (var admin = _fixture.CreateAdminContext())
        {
            admin.EvidenceRecords.Add(MasterData.Evidence.EvidenceRecord.Create(
                tenant, nameof(Party), 1, 1, TestData.Operator, "Party.Create", "{}", Guid.NewGuid()));
            await admin.SaveChangesAsync();
        }

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenant);

        var record = await context.EvidenceRecords.SingleAsync();
        context.EvidenceRecords.Remove(record);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("42501", postgresException.SqlState);
    }

    private async Task<(TenantId TenantId, long PartyId)> SeedPartyAsync(string name)
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var party = Party.Create(tenant, PartyType.Organization, name);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return (tenant, party.Id);
    }
}
```

- [x] **Step 4: Testleri çalıştır**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~TenantIsolationTests
```

Beklenen: `Passed!` — 7 test.

- [x] **Step 5: Commit**

```bash
git add tests/MasterData.Tests
git commit -m "Add Testcontainers fixture and tenant isolation tests for MasterData

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `4762089` "Add Testcontainers fixture and tenant isolation tests for MasterData" (deviation: `PostgresFixture`/`PostgresCollection` written to match CRM's actual current files rather than the plan's template verbatim — `PostgreSqlBuilder("postgres:17-alpine")` constructor form, `NOSUPERUSER NOBYPASSRLS` on the runtime role, and `PostgresCollection` split into its own file; all 7 tests passed unmodified)

---

## Task 8: Application — CreateParty, MergeParty, ResolveOrCreateParty

**Files:**
- Create: `src/Modules/MasterData/Application/CreatePartyCommand.cs`
- Create: `src/Modules/MasterData/Application/CreatePartyResult.cs`
- Create: `src/Modules/MasterData/Application/CreatePartyHandler.cs`
- Create: `src/Modules/MasterData/Application/MergePartyCommand.cs`
- Create: `src/Modules/MasterData/Application/MergePartyResult.cs`
- Create: `src/Modules/MasterData/Application/MergePartyHandler.cs`
- Create: `src/Modules/MasterData/Application/ResolveOrCreatePartyCommand.cs`
- Create: `src/Modules/MasterData/Application/ResolveOrCreatePartyResult.cs`
- Create: `src/Modules/MasterData/Application/ResolveOrCreatePartyHandler.cs`
- Create: `src/Modules/MasterData/Application/PartyNotFoundException.cs`
- Create: `src/Modules/MasterData/Application/IdempotencyKeyReusedException.cs`
- Create: `src/Modules/MasterData/Application/*Payload.cs` (internal records, bkz. CRM'in `CompletedPayload.cs`)
- Create: `tests/MasterData.Tests/Integration/CreatePartyHandlerTests.cs`
- Create: `tests/MasterData.Tests/Integration/MergePartyHandlerTests.cs`
- Create: `tests/MasterData.Tests/Integration/ResolveOrCreatePartyTests.cs`

**Idempotency notu:** `CreateParty`/`MergeParty` standart, çağıranın verdiği
`IdempotencyKey` desenini kullanır (CRM'in `CompleteOpportunityHandler`'ının aynısı) —
iki kez çağrılırsa gerçekten yeni bir Party/merge üretebilirler, korumasız değiller.
`ResolveOrCreateParty` **ayrı bir `IdempotencyRecord` yazmaz** — `PartyExternalIdentity`nin
kendi `(tenant_id, source_instance_ref, external_type, external_id)` tekilliği zaten
doğal bir idempotency sağlıyor: aynı external referansla iki kez çağrı, ilkinde
oluşturur, ikincisinde bulur — çift Party üretmez. Bu, doc 20 kural 4'ün ("her
state-changing command idempotent") *niyetini* karşılıyor, mekanizmasını değil — kayıt
olarak burada bırakıyoruz, mekanik olarak zorlamıyoruz.

- [x] **Step 1: `CreateParty` — komut, sonuç, exception'lar**

`src/Modules/MasterData/Application/CreatePartyCommand.cs`:

```csharp
using Contracts;

namespace MasterData.Application;

public sealed record CreatePartyCommand(
    TenantId TenantId,
    PartyType PartyType,
    string Name,
    string? Surname,
    string? Phone,
    string? Email,
    string IdempotencyKey,
    Guid CorrelationId);
```

`src/Modules/MasterData/Application/CreatePartyResult.cs`:

```csharp
namespace MasterData.Application;

public sealed record CreatePartyResult(long PartyId, bool Replayed);
```

`src/Modules/MasterData/Application/IdempotencyKeyReusedException.cs`:

```csharp
namespace MasterData.Application;

public sealed class IdempotencyKeyReusedException : InvalidOperationException
{
    public IdempotencyKeyReusedException(string operation, string idempotencyKey)
        : base($"Idempotency key '{idempotencyKey}' was already used for a different {operation} request.")
    {
    }
}
```

`src/Modules/MasterData/Application/PartyNotFoundException.cs`:

```csharp
namespace MasterData.Application;

public sealed class PartyNotFoundException : InvalidOperationException
{
    public PartyNotFoundException(long partyId)
        : base($"Party {partyId} was not found.")
    {
    }
}
```

- [x] **Step 2: `CreatePartyHandler`**

`src/Modules/MasterData/Application/CreatePartyHandler.cs` — `CompleteOpportunityHandler`'ın
aynı iskeleti (transaction → tenant context → idempotency lookup → iş → outbox → tek
`SaveChanges`), evidence yazmıyor (rutin bir yaratma işlemi, doc 20'nin risk katalogunda
değil — `MergeParty` farklı, Step 4'te evidence yazıyor):

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterData.Domain;
using MasterData.Outbox;
using MasterData.Idempotency;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class CreatePartyHandler
{
    private const string Operation = "CreateParty";
    private const string EventType = "enterprise.masterdata.party.created.v1";
    private const string EventSource = "/enterprise/master-data";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly MasterDataDbContext _context;

    public CreatePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<CreatePartyResult> HandleAsync(CreatePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var requestHash = HashRequest(command);

        var existing = await _context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.TenantId == command.TenantId
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<CreatedPartyPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new CreatePartyResult(stored.PartyId, Replayed: true);
        }

        var party = Party.Create(command.TenantId, command.PartyType, command.Name, command.Surname, command.Phone, command.Email);
        _context.Parties.Add(party);
        await _context.SaveChangesAsync(cancellationToken); // party.Id needs a round-trip before the outbox payload can reference it

        var payload = new CreatedPartyPayload(party.Id, party.PartyType);
        var payloadJson = JsonSerializer.Serialize(payload);

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: party.Id,
            aggregateVersion: 1,
            eventType: EventType,
            source: EventSource,
            subject: $"parties/{party.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: payloadJson));

        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            new Contracts.PrincipalRef("system", Operation), // TODO Task 9: replace with the real caller principal once a caller exists
            Operation,
            command.IdempotencyKey,
            requestHash,
            SucceededStatus,
            payloadJson,
            IdempotencyRetention));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreatePartyResult(party.Id, Replayed: false);
    }

    private static string HashRequest(CreatePartyCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.PartyType}|{command.Name}|{command.Surname}|{command.Phone}|{command.Email}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
```

`src/Modules/MasterData/Application/CreatedPartyPayload.cs`:

```csharp
using Contracts;

namespace MasterData.Application;

internal sealed record CreatedPartyPayload(long PartyId, PartyType PartyType);
```

**Not (deviation, plana yaz):** `IdempotencyRecord.Create` bir `PrincipalRef` istiyor,
ama `CreatePartyCommand`'da henüz gerçek bir çağıran principal'ı yok (Host'a hiçbir
komut bağlanmadı, tıpkı bugünkü `CompleteOpportunity`'nin de HTTP'siz olması gibi).
Geçici olarak `("system", Operation)` kullanıldı — bir HTTP yüzeyi eklendiğinde gerçek
principal'a değiştirilmeli. Bunu TODO olarak bırak, sessizce "doğru" gibi işaretleme.

- [x] **Step 3: `CreatePartyHandlerTests` — kırmızı → yeşil**

`tests/MasterData.Tests/Integration/CreatePartyHandlerTests.cs` —
`CompleteOpportunityHandlerTests`'in ilk üç testinin aynı deseni:

```csharp
using MasterData.Application;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class CreatePartyHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CreatePartyHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Creating_writes_state_and_outbox_together()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "ABC AŞ", null, null, null, "key-1", Guid.NewGuid());

        long partyId;
        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new CreatePartyHandler(context).HandleAsync(command);
            Assert.False(result.Replayed);
            partyId = result.PartyId;
        }

        await using var verification = _fixture.CreateAdminContext();
        var party = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == partyId);
        var outbox = await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == partyId).ToListAsync();

        Assert.Equal("ABC AŞ", party.Name);
        Assert.Single(outbox);
        Assert.Equal("enterprise.masterdata.party.created.v1", outbox[0].EventType);
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_stored_response()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "XYZ AŞ", null, null, null, "key-2", Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
            await new CreatePartyHandler(first).HandleAsync(command);

        await using (var second = _fixture.CreateAdminContext())
        {
            var result = await new CreatePartyHandler(second).HandleAsync(command);
            Assert.True(result.Replayed);
        }

        await using var verification = _fixture.CreateAdminContext();
        var count = await verification.Parties.CountAsync(p => p.TenantId == tenant);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Reusing_a_key_for_a_different_request_is_rejected()
    {
        var tenant = TestData.NextTenant();
        var command = new CreatePartyCommand(tenant, PartyType.Organization, "A", null, null, null, "key-3", Guid.NewGuid());
        var differentCommand = command with { Name = "B" };

        await using (var first = _fixture.CreateAdminContext())
            await new CreatePartyHandler(first).HandleAsync(command);

        await using var second = _fixture.CreateAdminContext();
        await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() =>
            new CreatePartyHandler(second).HandleAsync(differentCommand));
    }
}
```

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~CreatePartyHandlerTests
```

Beklenen: `Passed!` — 3 test.

- [x] **Step 4: `MergeParty` — komut, sonuç**

`src/Modules/MasterData/Application/MergePartyCommand.cs`:

```csharp
using Contracts;

namespace MasterData.Application;

public sealed record MergePartyCommand(
    TenantId TenantId,
    long SourcePartyId,
    long TargetPartyId,
    PrincipalRef Principal,
    string IdempotencyKey,
    Guid CorrelationId);
```

`src/Modules/MasterData/Application/MergePartyResult.cs`:

```csharp
namespace MasterData.Application;

public sealed record MergePartyResult(long SourcePartyId, long CanonicalPartyId, bool Replayed);
```

`src/Modules/MasterData/Application/MergedPartyPayload.cs`:

```csharp
namespace MasterData.Application;

internal sealed record MergedPartyPayload(long SourcePartyId, long CanonicalPartyId);
```

- [x] **Step 5: `MergePartyHandler` — tek-hop çözümleme, tombstone repoint, external identity taşıma**

`src/Modules/MasterData/Application/MergePartyHandler.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MasterData.Domain;
using MasterData.Evidence;
using MasterData.Idempotency;
using MasterData.Outbox;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

/// <summary>Single-hop resolution + tombstone repointing + external-identity
/// reassignment, all inside MasterData's own schema/transaction — see
/// docs/plans/2026-09-16-masterdata-party-foundation.md §5. Evidence is written here
/// (unlike CreateParty) because a merge is hard to undo and audit-worthy, the same risk
/// category doc 20 already treats money/authorization/cancellation transitions with.</summary>
public sealed class MergePartyHandler
{
    private const string Operation = "MergeParty";
    private const string EventType = "enterprise.masterdata.party.merged.v1";
    private const string EventSource = "/enterprise/master-data";
    private const int SucceededStatus = 200;
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly MasterDataDbContext _context;

    public MergePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<MergePartyResult> HandleAsync(MergePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var requestHash = HashRequest(command);

        var existing = await _context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.TenantId == command.TenantId
                    && record.PrincipalIssuer == command.Principal.Issuer
                    && record.PrincipalSubject == command.Principal.Subject
                    && record.Operation == Operation
                    && record.IdempotencyKey == command.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                throw new IdempotencyKeyReusedException(Operation, command.IdempotencyKey);

            await transaction.CommitAsync(cancellationToken);
            var stored = JsonSerializer.Deserialize<MergedPartyPayload>(existing.ResponsePayload)
                ?? throw new InvalidOperationException("Stored idempotency response is empty.");
            return new MergePartyResult(stored.SourcePartyId, stored.CanonicalPartyId, Replayed: true);
        }

        var source = await _context.Parties.SingleOrDefaultAsync(p => p.Id == command.SourcePartyId, cancellationToken)
            ?? throw new PartyNotFoundException(command.SourcePartyId);
        var requestedTarget = await _context.Parties.SingleOrDefaultAsync(p => p.Id == command.TargetPartyId, cancellationToken)
            ?? throw new PartyNotFoundException(command.TargetPartyId);

        // Single-hop invariant: resolve the requested target to its own canonical first
        // — Party.MergeInto refuses a target that is itself a tombstone, so this
        // resolution has to happen here, before calling it.
        var canonical = requestedTarget.MergedIntoPartyId is { } alreadyCanonicalId
            ? await _context.Parties.SingleAsync(p => p.Id == alreadyCanonicalId, cancellationToken)
            : requestedTarget;

        // Repoint every existing tombstone that pointed at `source` — source may itself
        // already be canonical for other, previously merged parties.
        var dependentTombstones = await _context.Parties
            .Where(p => p.MergedIntoPartyId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var tombstone in dependentTombstones)
            tombstone.RepointMergeTarget(canonical.Id);

        // Reassign external identities to the survivor — same-schema, same-transaction.
        var externalIdentities = await _context.PartyExternalIdentities
            .Where(e => e.PartyId == source.Id)
            .ToListAsync(cancellationToken);
        foreach (var identity in externalIdentities)
            identity.ReassignTo(canonical.Id);

        source.MergeInto(canonical);

        var payload = new MergedPartyPayload(source.Id, canonical.Id);
        var payloadJson = JsonSerializer.Serialize(payload);

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: source.Id,
            aggregateVersion: 1,
            eventType: EventType,
            source: EventSource,
            subject: $"parties/{source.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: payloadJson));

        _context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: source.Id,
            aggregateVersion: 1,
            principal: command.Principal,
            action: "Party.Merge",
            detail: payloadJson,
            correlationId: command.CorrelationId));

        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            command.Principal,
            Operation,
            command.IdempotencyKey,
            requestHash,
            SucceededStatus,
            payloadJson,
            IdempotencyRetention));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MergePartyResult(source.Id, canonical.Id, Replayed: false);
    }

    private static string HashRequest(MergePartyCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{Operation}|{command.TenantId.Value}|{command.Principal}|{command.SourcePartyId}|{command.TargetPartyId}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
```

- [x] **Step 6: `MergePartyHandlerTests` — merge zinciri çözümleme dahil**

`tests/MasterData.Tests/Integration/MergePartyHandlerTests.cs`:

```csharp
using MasterData.Application;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class MergePartyHandlerTests
{
    private readonly PostgresFixture _fixture;

    public MergePartyHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Merging_writes_state_outbox_and_evidence_together()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B (duplicate)");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id;
            bId = b.Id;
        }

        var command = new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-1", Guid.NewGuid());
        await using (var context = _fixture.CreateAdminContext())
        {
            var result = await new MergePartyHandler(context).HandleAsync(command);
            Assert.Equal(aId, result.CanonicalPartyId);
        }

        await using var verification = _fixture.CreateAdminContext();
        var b = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == bId);
        var outbox = await verification.OutboxMessages.AsNoTracking().Where(m => m.AggregateId == bId).ToListAsync();
        var evidence = await verification.EvidenceRecords.AsNoTracking().Where(e => e.AggregateId == bId).ToListAsync();

        Assert.Equal(aId, b.MergedIntoPartyId);
        Assert.Single(outbox);
        Assert.Single(evidence);
    }

    [Fact]
    public async Task Merging_into_an_already_merged_target_resolves_to_its_canonical()
    {
        var tenant = TestData.NextTenant();
        long aId, bId, cId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            var c = Party.Create(tenant, PartyType.Organization, "C");
            seed.Parties.AddRange(a, b, c);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id; cId = c.Id;
        }

        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-2a", Guid.NewGuid()));

        // C merges "into B" — B is itself already a tombstone; must resolve to A.
        MergePartyResult result;
        await using (var context = _fixture.CreateAdminContext())
            result = await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, cId, bId, TestData.Operator, "merge-2b", Guid.NewGuid()));

        Assert.Equal(aId, result.CanonicalPartyId);

        await using var verification = _fixture.CreateAdminContext();
        var c = await verification.Parties.AsNoTracking().SingleAsync(p => p.Id == cId);
        Assert.Equal(aId, c.MergedIntoPartyId);
    }

    [Fact]
    public async Task External_identities_move_to_the_survivor_at_merge_time()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B (duplicate)");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;

            seed.PartyExternalIdentities.Add(PartyExternalIdentity.Create(tenant, bId, "sap", "sap-connection-a", null, "1001"));
            await seed.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-3", Guid.NewGuid()));

        await using var verification = _fixture.CreateAdminContext();
        var identity = await verification.PartyExternalIdentities.AsNoTracking().SingleAsync(e => e.ExternalId == "1001");
        Assert.Equal(aId, identity.PartyId);
    }
}
```

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~MergePartyHandlerTests
```

Beklenen: `Passed!` — 3 test.

- [x] **Step 7: `ResolveOrCreateParty`**

`src/Modules/MasterData/Application/ResolveOrCreatePartyCommand.cs`:

```csharp
using Contracts;

namespace MasterData.Application;

public sealed record ResolveOrCreatePartyCommand(
    TenantId TenantId,
    string Provider,
    string SourceInstanceRef,
    string? ExternalType,
    string ExternalId,
    PartyType PartyType,
    string Name,
    Guid CorrelationId);
```

`src/Modules/MasterData/Application/ResolveOrCreatePartyResult.cs`:

```csharp
namespace MasterData.Application;

public sealed record ResolveOrCreatePartyResult(long PartyId, bool Created);
```

`src/Modules/MasterData/Application/ResolveOrCreatePartyHandler.cs`:

```csharp
using MasterData.Domain;
using MasterData.Outbox;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

/// <summary>No separate IdempotencyRecord — PartyExternalIdentity's own
/// (tenant_id, source_instance_ref, external_type, external_id) uniqueness already
/// makes this naturally idempotent: calling it twice with the same external reference
/// resolves to the same party both times. See this plan's Task 8 intro note.</summary>
public sealed class ResolveOrCreatePartyHandler
{
    private const string EventType = "enterprise.masterdata.party.created.v1";
    private const string EventSource = "/enterprise/master-data";

    private readonly MasterDataDbContext _context;

    public ResolveOrCreatePartyHandler(MasterDataDbContext context) => _context = context;

    public async Task<ResolveOrCreatePartyResult> HandleAsync(ResolveOrCreatePartyCommand command, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var externalType = command.ExternalType ?? string.Empty; // matches the computed column's COALESCE(external_type, '')
        var existing = await _context.PartyExternalIdentities
            .SingleOrDefaultAsync(
                e => e.TenantId == command.TenantId
                    && e.SourceInstanceRef == command.SourceInstanceRef
                    && (e.ExternalType ?? string.Empty) == externalType
                    && e.ExternalId == command.ExternalId,
                cancellationToken);

        if (existing is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ResolveOrCreatePartyResult(existing.PartyId, Created: false);
        }

        var party = Party.Create(command.TenantId, command.PartyType, command.Name);
        _context.Parties.Add(party);
        await _context.SaveChangesAsync(cancellationToken);

        _context.PartyExternalIdentities.Add(PartyExternalIdentity.Create(
            command.TenantId, party.Id, command.Provider, command.SourceInstanceRef, command.ExternalType, command.ExternalId));

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Party),
            aggregateId: party.Id,
            aggregateVersion: 1,
            eventType: EventType,
            source: EventSource,
            subject: $"parties/{party.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: $"{{\"partyId\":{party.Id}}}"));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResolveOrCreatePartyResult(party.Id, Created: true);
    }
}
```

- [x] **Step 8: `ResolveOrCreatePartyTests` — doğal idempotency**

`tests/MasterData.Tests/Integration/ResolveOrCreatePartyTests.cs`:

```csharp
using MasterData.Application;
using MasterData.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class ResolveOrCreatePartyTests
{
    private readonly PostgresFixture _fixture;

    public ResolveOrCreatePartyTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task First_call_creates_second_call_resolves()
    {
        var tenant = TestData.NextTenant();
        var command = new ResolveOrCreatePartyCommand(
            tenant, "sap", "sap-connection-a", null, "1001", PartyType.Organization, "ABC AŞ", Guid.NewGuid());

        long partyId;
        await using (var first = _fixture.CreateAdminContext())
        {
            var result = await new ResolveOrCreatePartyHandler(first).HandleAsync(command);
            Assert.True(result.Created);
            partyId = result.PartyId;
        }

        await using (var second = _fixture.CreateAdminContext())
        {
            var result = await new ResolveOrCreatePartyHandler(second).HandleAsync(command);
            Assert.False(result.Created);
            Assert.Equal(partyId, result.PartyId);
        }

        await using var verification = _fixture.CreateAdminContext();
        var count = await verification.Parties.CountAsync(p => p.TenantId == tenant);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Two_connections_of_the_same_provider_do_not_collide()
    {
        var tenant = TestData.NextTenant();
        var connectionA = new ResolveOrCreatePartyCommand(tenant, "sap", "sap-connection-a", null, "1001", PartyType.Organization, "ABC AŞ", Guid.NewGuid());
        var connectionB = new ResolveOrCreatePartyCommand(tenant, "sap", "sap-connection-b", null, "1001", PartyType.Organization, "DEF AŞ", Guid.NewGuid());

        long partyIdA, partyIdB;
        await using (var context = _fixture.CreateAdminContext())
            partyIdA = (await new ResolveOrCreatePartyHandler(context).HandleAsync(connectionA)).PartyId;
        await using (var context = _fixture.CreateAdminContext())
            partyIdB = (await new ResolveOrCreatePartyHandler(context).HandleAsync(connectionB)).PartyId;

        Assert.NotEqual(partyIdA, partyIdB);
    }
}
```

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~ResolveOrCreatePartyTests
```

Beklenen: `Passed!` — 2 test. İkinci test, Task 5 Step 7'deki COALESCE tekilliğinin
gerçekten çalıştığını (iki farklı `source_instance_ref`, aynı `external_id` — çakışma
yok) doğruluyor.

- [x] **Step 9: Tüm paketi çalıştır**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj
```

Beklenen: `Passed!` — bu ana kadarki tüm testler (Domain 10 + Architecture 2 +
TenantIsolation 7 + CreateParty 3 + MergeParty 3 + ResolveOrCreate 2 = 27).

- [x] **Step 10: Commit**

```bash
git add src/Modules/MasterData/Application tests/MasterData.Tests
git commit -m "Add CreateParty, MergeParty and ResolveOrCreateParty commands with atomic outbox/idempotency/evidence

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `976d3ce` "Add CreateParty, MergeParty and ResolveOrCreateParty commands with atomic outbox/idempotency/evidence" (deviations: (1) test files need `using Contracts;` for `PartyType`, not `using MasterData.Domain;` as the plan's snippets implied; (2) `MergePartyHandlerTests` had two local variables named `b`/`c` redeclared in a sibling scope — CS0136 — renamed to `mergedB`/`mergedC`; (3) `External_identities_move_to_the_survivor_at_merge_time`'s verification query didn't filter by tenant, so it broke under the full suite once `ResolveOrCreatePartyTests` also used external id "1001" on a different tenant against the same RLS-bypassing admin connection — added `e.TenantId == tenant` to the query. Full package: 27/27 passed.)

---

## Task 9: `IPartyDirectory` / `IPartyIdentityResolver` implementasyonu + Host kaydı

**Files:**
- Create: `src/Modules/MasterData/Application/PartyDirectory.cs`
- Create: `src/Modules/MasterData/Application/PartyIdentityResolver.cs`
- Modify: `src/Host/Program.cs`
- Create: `tests/MasterData.Tests/Integration/PartyDirectoryTests.cs`

- [x] **Step 1: `PartyDirectory` — merge zincirini takip eder**

`src/Modules/MasterData/Application/PartyDirectory.cs`:

```csharp
using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyDirectory : IPartyDirectory
{
    private readonly MasterDataDbContext _context;

    public PartyDirectory(MasterDataDbContext context) => _context = context;

    public async Task<PartyDirectoryEntry?> GetPartyAsync(PartyRef partyRef, CancellationToken cancellationToken = default)
    {
        var entries = await GetPartiesAsync([partyRef], cancellationToken);
        return entries.TryGetValue(partyRef, out var entry) ? entry : null;
    }

    public async Task<IReadOnlyDictionary<PartyRef, PartyDirectoryEntry>> GetPartiesAsync(
        IReadOnlyCollection<PartyRef> partyRefs, CancellationToken cancellationToken = default)
    {
        if (partyRefs.Count == 0)
            return new Dictionary<PartyRef, PartyDirectoryEntry>();

        var tenantId = partyRefs.First().TenantId;
        if (partyRefs.Any(r => !r.TenantId.Equals(tenantId)))
            throw new ArgumentException("All PartyRefs in a batch lookup must share the same tenant.", nameof(partyRefs));

        var ids = partyRefs.Select(r => r.PartyId).ToHashSet();
        var parties = await _context.Parties
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        // Resolve any tombstones found to their canonical in one extra round-trip.
        var canonicalIds = parties
            .Where(p => p.MergedIntoPartyId is not null)
            .Select(p => p.MergedIntoPartyId!.Value)
            .Distinct()
            .ToList();

        var canonicals = canonicalIds.Count == 0
            ? []
            : await _context.Parties.AsNoTracking()
                .Where(p => p.TenantId == tenantId && canonicalIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

        var result = new Dictionary<PartyRef, PartyDirectoryEntry>();
        foreach (var requested in partyRefs)
        {
            var party = parties.SingleOrDefault(p => p.Id == requested.PartyId);
            if (party is null) continue;

            var resolved = party.MergedIntoPartyId is { } canonicalId
                ? canonicals.SingleOrDefault(p => p.Id == canonicalId)
                : party;
            if (resolved is null) continue;

            result[requested] = new PartyDirectoryEntry(requested, resolved.PartyType, resolved.Name, resolved.Surname, resolved.Email);
        }

        return result;
    }
}
```

- [x] **Step 2: `PartyIdentityResolver`**

`src/Modules/MasterData/Application/PartyIdentityResolver.cs`:

```csharp
using Contracts;
using MasterData.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MasterData.Application;

public sealed class PartyIdentityResolver : IPartyIdentityResolver
{
    private readonly MasterDataDbContext _context;

    public PartyIdentityResolver(MasterDataDbContext context) => _context = context;

    public async Task<PartyRef?> ResolveAsync(PartyRef partyRef, CancellationToken cancellationToken = default)
    {
        var party = await _context.Parties.AsNoTracking()
            .SingleOrDefaultAsync(p => p.TenantId == partyRef.TenantId && p.Id == partyRef.PartyId, cancellationToken);
        if (party is null) return null;

        if (party.MergedIntoPartyId is { } canonicalId)
            return new PartyRef(partyRef.TenantId, canonicalId);

        return partyRef;
    }

    public async Task<PartyRef?> ResolveExternalIdentityAsync(
        TenantId tenantId, string provider, string sourceInstanceRef,
        string? externalType, string externalId, CancellationToken cancellationToken = default)
    {
        var normalizedType = externalType ?? string.Empty;
        var identity = await _context.PartyExternalIdentities.AsNoTracking()
            .SingleOrDefaultAsync(
                e => e.TenantId == tenantId && e.SourceInstanceRef == sourceInstanceRef
                    && (e.ExternalType ?? string.Empty) == normalizedType && e.ExternalId == externalId,
                cancellationToken);
        if (identity is null) return null;

        return await ResolveAsync(new PartyRef(tenantId, identity.PartyId), cancellationToken);
    }
}
```

- [x] **Step 3: `Host`'a kaydet**

`src/Host/Program.cs`'e ekle (CRM'in yanına, ikinci bir `AddDbContext` + DI kaydı):

```csharp
using Contracts;
using MasterData.Application;
using MasterData.Persistence;

builder.Services.AddDbContext<MasterDataDbContext>(options => options
    .UseNpgsql(
        builder.Configuration.GetConnectionString("MasterData") ?? MasterDataConnectionString.Resolve(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", MasterDataDbContext.Schema))
    .UseSnakeCaseNamingConvention());

builder.Services.AddScoped<IPartyDirectory, PartyDirectory>();
builder.Services.AddScoped<IPartyIdentityResolver, PartyIdentityResolver>();
```

Mevcut `app.MapGet("/health/db", ...)`'i değiştirme — istersen ayrı bir
`/health/masterdata-db` ekleyebilirsin ama bu plan kapsamında zorunlu değil.

- [x] **Step 4: Derle**

```bash
dotnet build src/Host/Host.csproj
```

- [x] **Step 5: `IPartyDirectory`/`IPartyIdentityResolver` entegrasyon testleri**

`tests/MasterData.Tests/Integration/PartyDirectoryTests.cs`:

```csharp
using Contracts;
using MasterData.Application;
using MasterData.Domain;
using Xunit;

namespace MasterData.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class PartyDirectoryTests
{
    private readonly PostgresFixture _fixture;

    public PartyDirectoryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetParty_resolves_a_merged_party_to_its_survivor()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B (duplicate)");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;
        }
        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-4", Guid.NewGuid()));

        await using var directoryContext = _fixture.CreateAdminContext();
        var directory = new PartyDirectory(directoryContext);

        var entry = await directory.GetPartyAsync(new PartyRef(tenant, bId));

        Assert.NotNull(entry);
        Assert.Equal("A", entry!.Name);
    }

    [Fact]
    public async Task GetParties_batch_lookup_resolves_all_requested_refs_in_one_call()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;
        }

        await using var directoryContext = _fixture.CreateAdminContext();
        var directory = new PartyDirectory(directoryContext);

        var result = await directory.GetPartiesAsync([new PartyRef(tenant, aId), new PartyRef(tenant, bId)]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ResolveExternalIdentityAsync_follows_the_merge_chain()
    {
        var tenant = TestData.NextTenant();
        long aId, bId;
        await using (var seed = _fixture.CreateAdminContext())
        {
            var a = Party.Create(tenant, PartyType.Organization, "A");
            var b = Party.Create(tenant, PartyType.Organization, "B");
            seed.Parties.AddRange(a, b);
            await seed.SaveChangesAsync();
            aId = a.Id; bId = b.Id;
            seed.PartyExternalIdentities.Add(PartyExternalIdentity.Create(tenant, bId, "sap", "sap-connection-a", null, "1001"));
            await seed.SaveChangesAsync();
        }
        await using (var context = _fixture.CreateAdminContext())
            await new MergePartyHandler(context).HandleAsync(new MergePartyCommand(tenant, bId, aId, TestData.Operator, "merge-5", Guid.NewGuid()));

        await using var resolverContext = _fixture.CreateAdminContext();
        var resolver = new PartyIdentityResolver(resolverContext);

        var resolved = await resolver.ResolveExternalIdentityAsync(tenant, "sap", "sap-connection-a", null, "1001");

        Assert.Equal(aId, resolved!.Value.PartyId);
    }
}
```

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj --filter FullyQualifiedName~PartyDirectoryTests
```

Beklenen: `Passed!` — 3 test.

- [x] **Step 6: Tüm MasterData paketini çalıştır**

```bash
dotnet test tests/MasterData.Tests/MasterData.Tests.csproj
```

Beklenen: `Passed!` — 30 test (Task 8'deki 27 + bu task'ın 3'ü).

- [x] **Step 7: Commit**

```bash
git add src/Modules/MasterData/Application src/Host tests/MasterData.Tests
git commit -m "Implement IPartyDirectory/IPartyIdentityResolver and wire them into Host

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `f2fec34` "Implement IPartyDirectory/IPartyIdentityResolver and wire them into Host" — full package 30/30 (27 from Task 8 + 3 here)

---

## Task 10: CI + dokümanları senkronla

**Files:**
- Modify: `.github/workflows/ci.yml` (gerekirse — muhtemelen değişiklik gerekmez, zaten `dotnet test` tüm çözümü çalıştırıyor olmalı)
- Modify: `README.md`
- Modify: `AGENTS.md`
- Modify: `docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` (§6 Phase 0.5 satırının Status'unu ✅ yap)
- Modify: `docs/plans/2026-09-16-masterdata-party-foundation.md` (Progress notunu güncelle)
- Modify: `graphify-out/` (post-commit hook otomatik günceller, elle dokunma)

- [x] **Step 1: CI'ı doğrula**

```bash
cat .github/workflows/ci.yml
```

`dotnet test` komutunun tüm `.slnx`'i (yeni `MasterData.Tests` dahil) kapsadığını
doğrula — muhtemelen zaten kapsıyor (çözüm seviyesinde çalışıyor), değişiklik
gerekmeyebilir. Gerekiyorsa güncelle.

- [x] **Step 2: `dotnet format` + tam build**

```bash
dotnet format --verify-no-changes
dotnet build -c Release
dotnet test
```

Beklenen: hepsi yeşil, CRM'in 41 testi + MasterData'nın 30 testi = 71 test.

(not: `dotnet test` çözüm seviyesinde iki Testcontainers paketini paralel başlattığında bu makinede ara sıra Docker kaynak çakışması görüldü — her paket ayrı ayrı çalıştırıldığında 41/41 ve 30/30, tutarlı yeşil. Kod regresyonu değil, yerel Docker eşzamanlılık sınırı.)

- [x] **Step 3: `README.md`'yi güncelle**

Repository layout bölümüne `MasterData/` girdisini ekle (şu an "Placeholder" diyor,
gerçek içeriği yansıt); "Status" satırına MasterData'yı ekle; runtime rolü bölümüne
`masterdata` şeması grant'lerinin de uygulanması gerektiğini not et.

- [x] **Step 4: `AGENTS.md`'nin Status bölümünü güncelle**

`## Status` altına MasterData'nın eklendiğini, `party_type`/`PartyRelationship`/
`PartyExternalIdentity`'nin tarihini, ve toplam test sayısını yansıt.

- [x] **Step 5: Plan dosyalarındaki durum işaretlerini güncelle**

`docs/plans/2026-09-16-crm-target-model-phase0-delta-plan.md` §6'daki Phase 0.5
satırının Status hücresini `🟡` → `✅ Done` yap. `docs/plans/2026-09-16-masterdata-party-foundation.md`'nin
üstündeki "Progress" notunu güncelle.

- [x] **Step 6: Graph'ı güncelle ve commit**

```bash
graphify update .
git add README.md AGENTS.md docs/plans .github graphify-out
git commit -m "Sync docs, plan status and CI with the completed MasterData/Party foundation

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```
→ Commit: `9c4a00d` "Sync docs, plan status and CI with the completed MasterData/Party foundation"

(not: `.github/workflows/ci.yml` değişiklik gerektirmedi — `dotnet test` zaten çözüm seviyesinde çalışıyor, `MasterData.Tests` otomatik kapsanıyor.)

---

## Kabul kriterleri (bu planın "bitti" demesi için)

**Durum: TAMAMLANDI (2026-09-16).** 11 görev, 11 commit, `tests/MasterData.Tests` 30/30,
`tests/CRM.Tests` 41/41 (regresyon yok), `dotnet format --verify-no-changes` temiz.

Tümü `docs/plans/2026-09-16-masterdata-party-foundation.md` §10'un birebir aynısı:

- `Contracts`: `PartyRef`, `PartyType`, `PartyDirectoryEntry`, `IPartyDirectory`, `IPartyIdentityResolver` eklendi.
- `MasterData.Domain`: `Party` (+`PartyType`), `PartyRelationship`, `PartyExternalIdentity`, merge tek-hop invariant'ı, testleri.
- `MasterData.Application`: `CreateParty`, `MergeParty`, `ResolveOrCreateParty` — doc 20'nin 7 kurallık çekirdeğinin tamamı (RLS, tenant-safe FK, state+outbox aynı `SaveChanges`, idempotency, geri alınabilir migration'lar, CHECK'ler+testleri, desteklenen runtime).
- `MasterDataDbContext` + design-time factory + `InitialMasterDataSchema` + `EnableRowLevelSecurity` migration'ları.
- `scripts/create-runtime-role.sql`'e `masterdata` grant bloğu.
- `tests/MasterData.Tests`: ~30 test (Domain, Architecture, Integration — RLS izolasyonu, merge zinciri çözümleme, `IPartyDirectory`/`IPartyIdentityResolver` davranışı dahil).
- CI yeşil, `dotnet format --verify-no-changes` temiz.
- **Bu planın kapsamı dışında kalan, dokunulmayan:** `crm.parties`, `Opportunity.PartyId`, CRM'in mevcut testleri — hepsi Phase 1'in işi.
