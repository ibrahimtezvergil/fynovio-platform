# Pilot Enforcement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** CRM pilot modülünü, mimari dokümanların bağlayıcı çekirdeğini gerçekten *çalışır* biçimde uygulayan bir hale getirmek: testler, RLS ile tenant izolasyonu, tek transaction'da state+outbox+evidence yazan ilk komut ve bilinen iki domain hatasının düzeltilmesi.

**Architecture:** Mevcut modüler monolit korunur. Test projesi `tests/CRM.Tests` altında tek bir xUnit projesi olarak açılır (domain unit testleri + Testcontainers ile gerçek PostgreSQL entegrasyon testleri + NetArchTest mimari testleri). Aggregate sınırı sıkılaştırılır: `OpportunityLine` yalnızca `Opportunity` üzerinden değiştirilir ve `row_version` artışı interceptor yerine domain metotlarına taşınır. Tenant izolasyonu, migration içine yazılan ham SQL ile RLS politikaları + transaction-local `app.tenant_id` ayarı + ayrı yetkisiz runtime rolü şeklinde uygulanır. İlk gerçek komut (`CompleteOpportunity`) idempotency, outbox ve evidence kayıtlarını tek `SaveChanges()` içinde yazar.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, PostgreSQL 17, xUnit, Testcontainers.PostgreSql, NetArchTest.Rules, GitHub Actions.

**Ön koşullar:**
- Docker Desktop çalışıyor olmalı (Testcontainers).
- `dotnet tool restore` bir kez çalıştırılmış olmalı.
- Çalışma dizini her zaman repo kökü: `/Users/ibrahimtezvergil/Projects/fynovio/fynovio-platform`.

**Bu planın kapsamı dışında (ayrı plan):** Access modülü düzeltmeleri (rol tenant güvenliği, `row_version DEFAULT 1`, grant/revoke evidence'ı), outbox dispatcher (Worker), HTTP endpoint'leri ve React tarafı.

---

## Task 0: Bağlayıcı çekirdeği kararlaştır (checkpoint — kod yok)

Bu görev bir karar görevidir. Sonraki görevlerin hangi kuralları zorunlu kabul edeceğini sabitler.

**Files:**
- Modify: `AGENTS.md`

- [ ] **Step 1: Kararı platform sahibine sun**

Aşağıdaki iki maddeyi onaya sun, onay gelmeden Task 1'e geçme:

1. **Bağlayıcı çekirdek (bugün CI'da zorlanır):** her tabloda `tenant_id` + tenant-safe composite FK; tenant-scoped tablolarda RLS + yetkisiz runtime rolü; state+outbox aynı transaction; mutasyon komutlarında idempotency; reversible + expand/contract migration; şema seviyesinde CHECK/enum/para kuralı; desteklenen runtime + bağımlılık taraması.
2. **Tetiklemeli kurallar (yeteneği gelince açılır):** evidence yalnızca riskli komutlarda (para taşıyan geçiş, yetki değişimi, iptal); evidence tamper-proofing, modül başına DB rolü, delegation/SoD/decision epoch, doc 12'nin kalan gate'leri.

- [ ] **Step 2: Kararı `AGENTS.md`'ye yaz**

`AGENTS.md` içindeki "Testing / Definition of Done" bölümünün hemen üstüne şu bölümü ekle (onaylanan metinle):

```markdown
## Enforcement Scope (approved 2026-09-16)

**Binding core — enforced in CI from now on:**
1. Every tenant-scoped table carries `tenant_id` and every FK is the tenant-safe composite form.
2. RLS (`ENABLE` + `FORCE`) on every tenant-scoped table, exercised by an integration test running as the unprivileged runtime role.
3. Business state and its outbox row commit in the same `SaveChanges()`.
4. Every state-changing command is idempotent (tenant + principal + operation + key).
5. Migrations are reversible, expand/contract-shaped, and generated — never hand-edited except for the RLS exception below.
6. Single-row invariants are DB `CHECK` constraints and each one has a test.
7. Supported runtime (.NET LTS) and a dependency-vulnerability check in CI.

**Trigger-based — not required yet:**
Evidence records are required only for risk-catalogued commands (money-carrying transitions, authorization changes, cancellation). Evidence tamper-proofing, per-module DB roles, delegation/SoD/decision epochs and the remaining fitness functions in doc 12 stay NOT APPLICABLE until the capability they guard is built (doc 12 §1 permits a visible not-applicable state, never a fake pass).

**Named exception to the generated-migration rule:** PostgreSQL RLS has no EF Core model representation, so RLS policies live in an otherwise-empty generated migration whose `Up`/`Down` bodies are written by hand with `migrationBuilder.Sql(...)`. This is the only permitted hand-written migration content.
```

- [ ] **Step 3: Commit**

```bash
git add AGENTS.md
git commit -m "Record binding enforcement scope for the pilot

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Araştırma reposuna doküman 20 yazılsın mı diye sor**

`~/Projects/fynovio/enterprise ve B2B mimari araştırma/docs/architecture-analysis/` altına `20_PILOT_ENFORCEMENT_SCOPE.md` eklemek ayrı bir yetki gerektirir (o repoda yeni numaralı dosya yalnızca platform sahibinin açık talimatıyla açılır). Sor; hayır ise bu adım kapanır, `AGENTS.md` tek kayıt olarak kalır.

---

## Task 1: Test projesi ve domain durum makinesi testleri

**Files:**
- Create: `tests/CRM.Tests/CRM.Tests.csproj` (şablondan)
- Create: `tests/CRM.Tests/TestData.cs`
- Create: `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`
- Modify: `fynovio-platform.slnx`

- [ ] **Step 1: Test projesini oluştur ve referansları ekle**

```bash
cd /Users/ibrahimtezvergil/Projects/fynovio/fynovio-platform
dotnet new xunit -n CRM.Tests -o tests/CRM.Tests
dotnet add tests/CRM.Tests/CRM.Tests.csproj reference src/Modules/CRM/CRM.csproj
dotnet sln fynovio-platform.slnx add tests/CRM.Tests/CRM.Tests.csproj
dotnet build tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Build succeeded`. Şablonun ürettiği `UnitTest1.cs` dosyasını sil:

```bash
rm tests/CRM.Tests/UnitTest1.cs
```

- [ ] **Step 2: xUnit sürümünü not et**

```bash
dotnet list tests/CRM.Tests/CRM.Tests.csproj package
```

Çıktıdaki `xunit` sürümünü not et. Bu planın `IAsyncLifetime` kullanan kısımları (Task 3) iki sürüm için de kod veriyor; oradaki doğru varyantı seç.

- [ ] **Step 3: Ortak test verisi yardımcısını yaz**

`tests/CRM.Tests/TestData.cs`:

```csharp
using Contracts;

namespace CRM.Tests;

/// <summary>Her test kendi tenant'ıyla çalışır; testler arasında veri sızmasın diye
/// tenant id'leri artan bir sayaçtan gelir (AGENTS.md: testlerde sabit id yok).</summary>
public static class TestData
{
    private static long _nextTenantId = 1000;

    public static TenantId NextTenant() => new(Interlocked.Increment(ref _nextTenantId));

    public static PrincipalRef Seller { get; } = new("https://idp.local", "seller-1");

    public static EntityRef ProductRef(TenantId tenantId, long productId = 1) =>
        new(tenantId, "masterdata", "product", productId);
}
```

- [ ] **Step 4: Durum makinesi testlerini yaz**

`tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`:

```csharp
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

public sealed class OpportunityStateMachineTests
{
    private static Opportunity NewWaitingOpportunity()
    {
        var tenant = TestData.NextTenant();
        return Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRY", estimatedAmount: 1000m);
    }

    [Fact]
    public void Create_starts_in_waiting()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Equal(OpportunityStatus.Waiting, opportunity.Status);
        Assert.Null(opportunity.ExpiryDate);
    }

    [Fact]
    public void Create_rejects_a_currency_that_is_not_three_letters()
    {
        var tenant = TestData.NextTenant();

        Assert.Throws<ArgumentException>(() =>
            Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRYX", 1000m));
    }

    [Fact]
    public void Offer_requires_a_future_expiry_date()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            opportunity.Offer(DateTimeOffset.UtcNow.AddDays(-1)));
    }

    [Fact]
    public void Offer_moves_to_offered_and_stamps_offer_date()
    {
        var opportunity = NewWaitingOpportunity();

        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Equal(OpportunityStatus.Offered, opportunity.Status);
        Assert.NotNull(opportunity.OfferDate);
        Assert.NotNull(opportunity.ExpiryDate);
    }

    [Fact]
    public void Complete_is_rejected_while_waiting()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<InvalidOperationException>(() => opportunity.Complete(1000m));
    }

    [Fact]
    public void Complete_is_rejected_without_an_active_required_line()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 100m, isOptional: true);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() => opportunity.Complete(100m));
    }

    [Fact]
    public void Complete_succeeds_with_one_active_required_line()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 2, unitPrice: 50m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        opportunity.Complete(100m);

        Assert.Equal(OpportunityStatus.Completed, opportunity.Status);
        Assert.NotNull(opportunity.SaleDate);
    }

    [Fact]
    public void Cancel_requires_a_reason()
    {
        var opportunity = NewWaitingOpportunity();

        Assert.Throws<ArgumentException>(() => opportunity.Cancel("  "));
    }

    [Fact]
    public void Cancel_from_waiting_is_allowed()
    {
        var opportunity = NewWaitingOpportunity();

        opportunity.Cancel("müşteri vazgeçti");

        Assert.Equal(OpportunityStatus.Canceled, opportunity.Status);
        Assert.NotNull(opportunity.CancelDate);
    }

    [Fact]
    public void AddLine_is_rejected_after_offer()
    {
        var opportunity = NewWaitingOpportunity();
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));

        Assert.Throws<InvalidOperationException>(() =>
            opportunity.AddLine(TestData.ProductRef(opportunity.TenantId), quantity: 1, unitPrice: 10m));
    }
}
```

- [ ] **Step 5: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Passed!` — 10 test. Hepsi mevcut davranışı kilitler; bu adımda kırmızı test olmamalı.

- [ ] **Step 6: Commit**

```bash
git add tests/CRM.Tests fynovio-platform.slnx
git commit -m "Add CRM test project and Opportunity state machine tests

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Mimari sınır testi (FF01)

**Files:**
- Modify: `tests/CRM.Tests/CRM.Tests.csproj`
- Create: `tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`

- [ ] **Step 1: NetArchTest paketini ekle**

```bash
dotnet add tests/CRM.Tests/CRM.Tests.csproj package NetArchTest.Rules
```

- [ ] **Step 2: Testi yaz**

`tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`:

```csharp
using System.Reflection;
using CRM.Persistence;
using NetArchTest.Rules;
using Xunit;

namespace CRM.Tests.Architecture;

/// <summary>FF01 (doc 12): bir modül yalnızca Contracts'a bağımlı olabilir; modülün
/// Domain namespace'i kendi Persistence katmanına bağımlı olamaz.</summary>
public sealed class ModuleBoundaryTests
{
    private static readonly Assembly CrmAssembly = typeof(CrmDbContext).Assembly;

    [Fact]
    public void Crm_does_not_depend_on_another_module()
    {
        var result = Types.InAssembly(CrmAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("Access", "MasterData", "Organization", "TenantLifecycle")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    [Fact]
    public void Crm_domain_does_not_depend_on_persistence()
    {
        var result = Types.InAssembly(CrmAssembly)
            .That().ResideInNamespace("CRM.Domain")
            .ShouldNot().HaveDependencyOn("CRM.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, Describe(result));
    }

    private static string Describe(TestResult result) =>
        result.FailingTypeNames is null ? "OK" : string.Join(", ", result.FailingTypeNames);
}
```

- [ ] **Step 3: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~ModuleBoundaryTests
```

Beklenen: `Passed!` — 2 test. (Bu kural bugün zaten sağlanıyor; test onu kalıcı hale getiriyor.)

- [ ] **Step 4: Commit**

```bash
git add tests/CRM.Tests
git commit -m "Add NetArchTest module boundary tests (FF01)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Testcontainers altyapısı + iptal hatasını gösteren kırmızı test

**Files:**
- Modify: `tests/CRM.Tests/CRM.Tests.csproj`
- Create: `tests/CRM.Tests/Integration/PostgresFixture.cs`
- Create: `tests/CRM.Tests/Integration/OpportunityPersistenceTests.cs`

- [ ] **Step 1: Testcontainers paketini ekle**

```bash
dotnet add tests/CRM.Tests/CRM.Tests.csproj package Testcontainers.PostgreSql
```

- [ ] **Step 2: Fixture'ı yaz**

`tests/CRM.Tests/Integration/PostgresFixture.cs`:

```csharp
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>Gerçek PostgreSQL üzerinde migration'ları uygular (AGENTS.md: paylaşılan dev
/// veritabanı yok, Testcontainers). Konteynerin `postgres` kullanıcısı superuser'dır ve
/// RLS'i bypass eder — yetkisiz runtime rolüyle yapılan izolasyon testi Task 7'de.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("fynovio_platform_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public string AdminConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public CrmDbContext CreateAdminContext() => CreateContext(AdminConnectionString);

    public static CrmDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CrmDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new CrmDbContext(options);
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
```

**xUnit 2.x kullanıyorsan** (Task 1 Step 2'de not ettiğin sürüm `2.*` ise) `InitializeAsync`/`DisposeAsync` imzalarını şu şekilde değiştir:

```csharp
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateAdminContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
```

- [ ] **Step 3: Kırmızı testi yaz**

`tests/CRM.Tests/Integration/OpportunityPersistenceTests.cs`:

```csharp
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

[Collection(nameof(PostgresCollection))]
public sealed class OpportunityPersistenceTests
{
    private readonly PostgresFixture _fixture;

    public OpportunityPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canceling_a_waiting_opportunity_persists()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 1000m);
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        opportunity.Cancel("müşteri vazgeçti");
        await context.SaveChangesAsync();

        var reloaded = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Canceled, reloaded.Status);
    }

    [Fact]
    public async Task Canceling_an_offered_opportunity_persists()
    {
        await using var context = _fixture.CreateAdminContext();
        var tenant = TestData.NextTenant();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 1000m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        context.Opportunities.Add(opportunity);
        await context.SaveChangesAsync();

        opportunity.Cancel("bütçe onaylanmadı");
        await context.SaveChangesAsync();

        var reloaded = await context.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunity.Id);
        Assert.Equal(OpportunityStatus.Canceled, reloaded.Status);
    }
}
```

- [ ] **Step 4: Testleri çalıştır — ilki kırmızı olmalı**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~OpportunityPersistenceTests
```

Beklenen: `Canceling_a_waiting_opportunity_persists` FAIL. Hata metni:
`Npgsql.PostgresException : 23514: new row for relation "opportunities" violates check constraint "ck_opportunities_expiry_required_once_offered"`.
İkinci test (offered → canceled) PASS etmeli; bu, düzeltmenin mevcut doğru davranışı bozmadığını gösterecek.

- [ ] **Step 5: Commit (kırmızı testle birlikte)**

```bash
git add tests/CRM.Tests
git commit -m "Add Testcontainers fixture and a failing test for cancel-from-waiting

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: CHECK kısıtını düzelt (migration)

**Files:**
- Modify: `src/Modules/CRM/Persistence/Configurations/OpportunityConfiguration.cs:54`
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_FixCancelExpiryCheck.cs` (üretilir)
- Modify: `src/Modules/CRM/Persistence/Migrations/CrmDbContextModelSnapshot.cs` (üretilir)

- [ ] **Step 1: Kısıt ifadesini düzelt**

`OpportunityConfiguration.cs` içindeki şu satırı:

```csharp
            t.HasCheckConstraint("ck_opportunities_expiry_required_once_offered", "status = 'waiting' OR expiry_date IS NOT NULL");
```

şununla değiştir:

```csharp
            // expiry_date yalnızca offered/completed durumlarında zorunlu. Eski ifade
            // (status = 'waiting' OR ...) waiting → canceled geçişini de kapsıyordu ve
            // teklif verilmemiş bir opportunity'nin iptalini imkansız kılıyordu.
            t.HasCheckConstraint(
                "ck_opportunities_expiry_required_once_offered",
                "status NOT IN ('offered','completed') OR expiry_date IS NOT NULL");
```

- [ ] **Step 2: Migration üret**

```bash
dotnet ef migrations add FixCancelExpiryCheck \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

- [ ] **Step 3: Üretilen migration'ı doğrula**

Yeni `*_FixCancelExpiryCheck.cs` dosyasını aç. `Up` içinde `DropCheckConstraint` + `AddCheckConstraint`, `Down` içinde tersi olmalı. Bu operasyonlar yoksa (EF kısıt değişikliğini fark etmediyse) **dur ve haber ver** — elle migration yazmak AGENTS.md'ye aykırı, Task 0'daki tek istisna RLS'tir.

- [ ] **Step 4: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~OpportunityPersistenceTests
```

Beklenen: `Passed!` — 2 test.

- [ ] **Step 5: Commit**

```bash
git add src/Modules/CRM/Persistence
git commit -m "Fix expiry_date check so a waiting opportunity can be canceled

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Satır iptalini aggregate root'a taşı ve row_version'ı domain'e al

**Files:**
- Modify: `src/Modules/CRM/Domain/Opportunity.cs`
- Modify: `src/Modules/CRM/Domain/OpportunityLine.cs:55-62`
- Delete: `src/Modules/CRM/Persistence/RowVersionInterceptor.cs`
- Modify: `src/Modules/CRM/Persistence/CrmDbContextFactory.cs:16`
- Modify: `src/Host/Program.cs`
- Create: `tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs`

- [ ] **Step 1: Eşzamanlılık testini yaz (kırmızı)**

`tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs`:

```csharp
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>"En az bir aktif zorunlu satır" kuralı satırlar arası bir invariant'tır ve
/// tek satırlık CHECK ile ifade edilemez (docs/schema/crm-sales-schema.md). Bu yüzden
/// satır değişikliği aggregate root'un versiyonunu artırmalı, aksi halde iki eşzamanlı
/// işlem kuralı birlikte delebilir.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class OpportunityConcurrencyTests
{
    private readonly PostgresFixture _fixture;

    public OpportunityConcurrencyTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canceling_the_last_required_line_conflicts_with_completing()
    {
        long opportunityId;
        var tenant = TestData.NextTenant();

        await using (var seed = _fixture.CreateAdminContext())
        {
            var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
            seed.Parties.Add(party);
            await seed.SaveChangesAsync();

            var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 1000m);
            opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 100m);
            opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
            seed.Opportunities.Add(opportunity);
            await seed.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        await using var contextA = _fixture.CreateAdminContext();
        await using var contextB = _fixture.CreateAdminContext();

        var fromA = await contextA.Opportunities.Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunityId);
        var fromB = await contextB.Opportunities.Include(o => o.Lines)
            .SingleAsync(o => o.Id == opportunityId);

        fromB.CancelLine(fromB.Lines.Single(), "stokta yok");
        await contextB.SaveChangesAsync();

        fromA.Complete(100m);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextA.SaveChangesAsync());
    }
}
```

- [ ] **Step 2: Testi çalıştır — derlenmemeli**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~OpportunityConcurrencyTests
```

Beklenen: derleme hatası `'Opportunity' does not contain a definition for 'CancelLine'`.

- [ ] **Step 3: `OpportunityLine.Cancel`'ı internal yap**

`src/Modules/CRM/Domain/OpportunityLine.cs` içinde:

```csharp
    /// <summary>Yalnızca aggregate root üzerinden çağrılır — bkz. Opportunity.CancelLine.</summary>
    internal void Cancel(string cancelReason)
    {
        if (string.IsNullOrWhiteSpace(cancelReason))
            throw new ArgumentException("Cancel reason is required.", nameof(cancelReason));

        IsCanceled = true;
        CancelReason = cancelReason;
    }
```

- [ ] **Step 4: `Opportunity`'yi güncelle**

`src/Modules/CRM/Domain/Opportunity.cs` içinde, sınıfın sonuna `Touch` ekle ve tüm mutasyon metotlarında `UpdatedAt = DateTimeOffset.UtcNow;` satırlarını `Touch();` ile değiştir:

```csharp
    /// <summary>Tek versiyon artış noktası. Interceptor yerine burada artırılıyor: outbox ve
    /// evidence kayıtları aynı transaction içinde `RowVersion`'ı okuyor, interceptor
    /// SaveChanges sırasında artırdığı için bir eski değer yazılıyordu.</summary>
    private void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
        RowVersion++;
    }

    public void CancelLine(OpportunityLine line, string cancelReason)
    {
        if (!_lines.Contains(line))
            throw new InvalidOperationException("Line does not belong to this opportunity.");
        if (Status is OpportunityStatus.Completed or OpportunityStatus.Canceled)
            throw new InvalidOperationException($"Cannot cancel a line on an opportunity in status {Status}.");

        line.Cancel(cancelReason);
        Touch();
    }
```

`Create` içindeki `CreatedAt = now, UpdatedAt = now` ataması olduğu gibi kalır (yeni kayıt `RowVersion = 1` ile başlar). `AddLine`, `Offer`, `Complete`, `Cancel` metotlarındaki `UpdatedAt = DateTimeOffset.UtcNow;` satırları `Touch();` olur.

- [ ] **Step 5: CRM interceptor'ını kaldır**

```bash
rm src/Modules/CRM/Persistence/RowVersionInterceptor.cs
```

`src/Modules/CRM/Persistence/CrmDbContextFactory.cs` içindeki `.AddInterceptors(new RowVersionInterceptor());` satırını sil. `src/Host/Program.cs` içindeki aynı satırı sil. (Access modülü kendi interceptor'ını korur — orada domain metotları henüz versiyon artırmıyor.)

- [ ] **Step 6: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Passed!` — tüm testler, yeni eşzamanlılık testi dahil.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "Move line cancellation and row_version increment into the Opportunity aggregate

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: Para kuralını uygula (satır toplamı ve tek yuvarlama noktası)

**Karar gerekiyor (Step 0):** opsiyonel satırlar toplama dahil mi? Bu plan "hayır, opsiyonel satırlar toplama girmez" varsayımıyla yazıldı (opsiyonel satır henüz seçilmemiş alternatif demektir). Platform sahibi aksini söylerse Step 3'teki filtreyi değiştir ve testi güncelle.

**Files:**
- Modify: `src/Modules/CRM/Domain/OpportunityLine.cs`
- Modify: `src/Modules/CRM/Domain/Opportunity.cs`
- Modify: `tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs`
- Create: `tests/CRM.Tests/Domain/OpportunityMoneyTests.cs`

- [ ] **Step 1: Para testlerini yaz (kırmızı)**

`tests/CRM.Tests/Domain/OpportunityMoneyTests.cs`:

```csharp
using CRM.Domain;
using Xunit;

namespace CRM.Tests.Domain;

/// <summary>docs/schema/crm-sales-schema.md "Money: one rounding rule": girilen değerler
/// 2dp, hesaplanan değerler 4dp; hesaplanan toplam tam olarak bir kez, `completed`
/// geçişinde 2dp'ye yuvarlanır.</summary>
public sealed class OpportunityMoneyTests
{
    private static Opportunity OfferedWithLines(params (int Quantity, decimal UnitPrice, bool IsOptional)[] lines)
    {
        var tenant = TestData.NextTenant();
        var opportunity = Opportunity.Create(tenant, partyId: 1, TestData.Seller, "TRY", 0m);
        foreach (var line in lines)
        {
            opportunity.AddLine(TestData.ProductRef(tenant), line.Quantity, line.UnitPrice, line.IsOptional);
        }

        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        return opportunity;
    }

    [Fact]
    public void AddLine_computes_line_total_at_four_decimals()
    {
        var opportunity = OfferedWithLines((3, 33.335m, false));

        Assert.Equal(100.005m, opportunity.Lines.Single().LineTotal);
    }

    [Fact]
    public void Complete_derives_the_total_from_active_required_lines()
    {
        var opportunity = OfferedWithLines((2, 50m, false), (1, 25m, false));

        opportunity.Complete();

        Assert.Equal(125.00m, opportunity.TotalAmount);
    }

    [Fact]
    public void Complete_rounds_the_computed_total_to_two_decimals_once()
    {
        var opportunity = OfferedWithLines((3, 33.335m, false));

        opportunity.Complete();

        Assert.Equal(100.01m, opportunity.TotalAmount);
    }

    [Fact]
    public void Complete_excludes_optional_lines_from_the_total()
    {
        var opportunity = OfferedWithLines((1, 100m, false), (1, 40m, true));

        opportunity.Complete();

        Assert.Equal(100.00m, opportunity.TotalAmount);
    }

    [Fact]
    public void Complete_excludes_canceled_lines_from_the_total()
    {
        var opportunity = OfferedWithLines((1, 100m, false), (1, 40m, false));
        opportunity.CancelLine(opportunity.Lines.Last(), "stokta yok");

        opportunity.Complete();

        Assert.Equal(100.00m, opportunity.TotalAmount);
    }
}
```

- [ ] **Step 2: Testleri çalıştır — derlenmemeli**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~OpportunityMoneyTests
```

Beklenen: derleme hatası — `Complete()` parametresiz aşırı yüklemesi yok.

- [ ] **Step 3: `OpportunityLine.Create` satır toplamını hesaplasın**

`src/Modules/CRM/Domain/OpportunityLine.cs` içinde, `return new OpportunityLine { ... }` bloğuna `LineTotal` ekle:

```csharp
        return new OpportunityLine
        {
            TenantId = tenantId,
            ProductRefBoundedContext = productRef.BoundedContext,
            ProductRefEntityType = productRef.EntityType,
            ProductRefId = productRef.Id,
            Quantity = quantity,
            UnitPrice = unitPrice,
            // Hesaplanan değer: 4dp'de tutulur, yuvarlama yalnızca Opportunity.Complete()'te.
            LineTotal = decimal.Round(quantity * unitPrice, 4, MidpointRounding.AwayFromZero),
            IsOptional = isOptional,
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow
        };
```

- [ ] **Step 4: `Opportunity.Complete()`'i parametresiz yap**

`src/Modules/CRM/Domain/Opportunity.cs` içindeki `Complete` metodunu şununla değiştir:

```csharp
    /// <summary>Enforces the one cross-row invariant CHECK cannot express: at least one
    /// active (non-canceled), required (non-optional) line must exist. Toplam satırlardan
    /// türetilir ve tek yuvarlama noktası burasıdır (17 §3.4).</summary>
    public void Complete()
    {
        if (Status != OpportunityStatus.Offered)
            throw new InvalidOperationException($"Cannot complete an opportunity in status {Status}. It must be offered first.");

        var billableLines = _lines.Where(line => !line.IsOptional && !line.IsCanceled).ToList();
        if (billableLines.Count == 0)
            throw new InvalidOperationException("Cannot complete an opportunity without at least one active required line.");

        var computedTotal = billableLines.Sum(line => line.LineTotal ?? 0m);

        Status = OpportunityStatus.Completed;
        // Single declared rounding point (17 §3.4): 4dp computed total rounds to the
        // currency's 2dp minor unit exactly once, here.
        TotalAmount = decimal.Round(computedTotal, 2, MidpointRounding.AwayFromZero);
        SaleDate = DateTimeOffset.UtcNow;
        Touch();
    }
```

- [ ] **Step 5: Eski testleri yeni imzaya uyarla**

`tests/CRM.Tests/Domain/OpportunityStateMachineTests.cs` içinde üç çağrıyı güncelle:
- `Assert.Throws<InvalidOperationException>(() => opportunity.Complete(1000m));` → `... => opportunity.Complete());`
- `Assert.Throws<InvalidOperationException>(() => opportunity.Complete(100m));` → `... => opportunity.Complete());`
- `opportunity.Complete(100m);` → `opportunity.Complete();`

`tests/CRM.Tests/Integration/OpportunityConcurrencyTests.cs` içinde `fromA.Complete(100m);` → `fromA.Complete();`

- [ ] **Step 6: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Passed!` — tüm testler.

- [ ] **Step 7: Commit**

```bash
git add src tests
git commit -m "Derive opportunity total from lines with a single rounding point

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: RLS, yetkisiz runtime rolü ve tenant context (FF03)

**Files:**
- Create: `src/Modules/CRM/Persistence/CrmDbContextTenantExtensions.cs`
- Create: `src/Modules/CRM/Persistence/Migrations/<timestamp>_EnableRowLevelSecurity.cs` (üretilir, gövdesi elle yazılır — Task 0'daki istisna)
- Create: `scripts/create-runtime-role.sql`
- Create: `tests/CRM.Tests/Integration/TenantIsolationTests.cs`

- [ ] **Step 1: Tenant context yardımcısını yaz**

`src/Modules/CRM/Persistence/CrmDbContextTenantExtensions.cs`:

```csharp
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace CRM.Persistence;

/// <summary>RLS politikalarının okuduğu `app.tenant_id` ayarını transaction'a bağlı olarak
/// yazar (doc 07 §3: pooled bağlantıda tenant context transaction-local olmalı,
/// set_config'in üçüncü parametresi `true`). Transaction yoksa ayar bağlantıda kalır ve
/// havuzdan o bağlantıyı alan bir sonraki isteğe sızar — bu yüzden zorunlu kontrol var.</summary>
public static class CrmDbContextTenantExtensions
{
    public static async Task SetTenantContextAsync(
        this CrmDbContext context,
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        if (context.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Tenant context must be set inside an explicit transaction.");

        var value = tenantId.Value.ToString();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.tenant_id', {value}, true)",
            cancellationToken);
    }
}
```

- [ ] **Step 2: Boş migration üret**

```bash
dotnet ef migrations add EnableRowLevelSecurity \
  --project src/Modules/CRM/CRM.csproj \
  --startup-project src/Modules/CRM/CRM.csproj \
  --output-dir Persistence/Migrations
```

Beklenen: `Up`/`Down` gövdeleri boş bir migration (model değişmedi).

- [ ] **Step 3: Migration gövdesini yaz**

Üretilen `*_EnableRowLevelSecurity.cs` dosyasının `Up`/`Down` metotlarını şununla doldur:

```csharp
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RLS'in EF Core model karşılığı yok; AGENTS.md'de adı konmuş tek elle yazılan
            // migration istisnası (Enforcement Scope, 2026-09-16).
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"ALTER TABLE crm.{table} ENABLE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"""
                    CREATE POLICY tenant_isolation ON crm.{table}
                        USING (tenant_id = current_setting('app.tenant_id', true)::bigint)
                        WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::bigint);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON crm.{table};");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} NO FORCE ROW LEVEL SECURITY;");
                migrationBuilder.Sql($"ALTER TABLE crm.{table} DISABLE ROW LEVEL SECURITY;");
            }
        }

        private static readonly string[] TenantScopedTables =
        [
            "parties",
            "customer_needs",
            "opportunities",
            "opportunity_lines",
            "opportunity_needs",
            "tenant_field_definitions",
            "outbox_messages",
            "idempotency_records",
            "evidence_records"
        ];
```

`current_setting('app.tenant_id', true)` ayar yoksa `NULL` döner; karşılaştırma `NULL` olunca hiçbir satır görünmez — yani varsayılan davranış "deny".

- [ ] **Step 4: Runtime rolü scriptini yaz**

`scripts/create-runtime-role.sql`:

```sql
-- Uygulamanın çalışma zamanı rolü. Migration'ları çalıştıran rolden ayrıdır (doc 07 §3):
-- superuser ve tablo sahibi RLS'i bypass eder, bu yüzden uygulama asla onlarla bağlanmaz.
-- Parolayı çalıştırmadan önce değiştir.
CREATE ROLE fynovio_app LOGIN PASSWORD 'change-me';

GRANT CONNECT ON DATABASE fynovio_platform TO fynovio_app;
GRANT USAGE ON SCHEMA crm TO fynovio_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA crm
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO fynovio_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA crm
    GRANT USAGE, SELECT ON SEQUENCES TO fynovio_app;

-- evidence_records append-only (crm-sales-schema.md revizyon 3, madde 10).
REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;
```

- [ ] **Step 5: İzolasyon testini yaz**

`tests/CRM.Tests/Integration/TenantIsolationTests.cs`:

```csharp
using Contracts;
using CRM.Domain;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>FF03 (doc 12): izolasyon, migration'ları çalıştıran superuser ile değil,
/// yetkisiz runtime rolüyle test edilir — superuser RLS'i her koşulda bypass eder.</summary>
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
    public async Task Runtime_role_sees_nothing_without_a_tenant_context()
    {
        await SeedPartyAsync("C");

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();

        var visible = await context.Parties.AsNoTracking().ToListAsync();

        Assert.Empty(visible);
    }

    [Fact]
    public async Task Runtime_role_cannot_write_a_row_for_another_tenant()
    {
        var (tenantA, _) = await SeedPartyAsync("D");
        var tenantB = TestData.NextTenant();

        await using var context = PostgresFixture.CreateContext(await _fixture.RuntimeConnectionStringAsync());
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.SetTenantContextAsync(tenantB);

        context.Parties.Add(Party.Create(tenantA, "Sızdırma denemesi", PartyCreationSource.Manual));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal("42501", postgresException.SqlState);
    }

    private async Task<(TenantId TenantId, long PartyId)> SeedPartyAsync(string name)
    {
        var tenant = TestData.NextTenant();
        await using var context = _fixture.CreateAdminContext();
        var party = Party.Create(tenant, name, PartyCreationSource.Manual);
        context.Parties.Add(party);
        await context.SaveChangesAsync();
        return (tenant, party.Id);
    }
}
```

- [ ] **Step 6: Fixture'a runtime rolü ekle**

`tests/CRM.Tests/Integration/PostgresFixture.cs` içine ekle:

```csharp
    private string? _runtimeConnectionString;

    /// <summary>Migration'ları uygulayan superuser'dan ayrı, RLS'e tabi rol (FF03).</summary>
    public async Task<string> RuntimeConnectionStringAsync()
    {
        if (_runtimeConnectionString is not null)
            return _runtimeConnectionString;

        await using (var context = CreateAdminContext())
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE ROLE fynovio_app LOGIN PASSWORD 'runtime';
                GRANT USAGE ON SCHEMA crm TO fynovio_app;
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA crm TO fynovio_app;
                GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA crm TO fynovio_app;
                REVOKE UPDATE, DELETE ON crm.evidence_records FROM fynovio_app;
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
```

- [ ] **Step 7: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Passed!` — izolasyon testleri dahil hepsi. Eğer `Runtime_role_cannot_write_a_row_for_another_tenant` beklenen `42501` yerine başka bir SqlState verirse, gerçek değeri hata metninden al ve testi ona göre düzelt (RLS `WITH CHECK` ihlali PostgreSQL 17'de `42501 insufficient_privilege` verir).

- [ ] **Step 8: Commit**

```bash
git add src scripts tests
git commit -m "Enable RLS on crm tables with a transaction-local tenant context and isolation tests

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: İlk komut — CompleteOpportunity (state + outbox + evidence + idempotency)

**Files:**
- Create: `src/Modules/CRM/Application/CompleteOpportunityCommand.cs`
- Create: `src/Modules/CRM/Application/CompleteOpportunityHandler.cs`
- Create: `tests/CRM.Tests/Integration/CompleteOpportunityHandlerTests.cs`

- [ ] **Step 1: Testleri yaz (kırmızı)**

`tests/CRM.Tests/Integration/CompleteOpportunityHandlerTests.cs`:

```csharp
using CRM.Application;
using CRM.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CRM.Tests.Integration;

/// <summary>FF07 + FF08 (doc 12) ve doc 17 §6 madde 1: state, outbox ve evidence tek
/// transaction'da commit olur; aynı idempotency anahtarıyla tekrar gelen komut tek iş
/// etkisi yaratır.</summary>
[Collection(nameof(PostgresCollection))]
public sealed class CompleteOpportunityHandlerTests
{
    private readonly PostgresFixture _fixture;

    public CompleteOpportunityHandlerTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Completing_writes_state_outbox_and_evidence_together()
    {
        var (tenant, opportunityId) = await SeedOfferedOpportunityAsync();
        var command = new CompleteOpportunityCommand(
            tenant, opportunityId, TestData.Seller, IdempotencyKey: "key-1", CorrelationId: Guid.NewGuid());

        await using (var context = _fixture.CreateAdminContext())
        {
            var handler = new CompleteOpportunityHandler(context);
            var result = await handler.HandleAsync(command);
            Assert.False(result.Replayed);
        }

        await using var verification = _fixture.CreateAdminContext();
        var opportunity = await verification.Opportunities.AsNoTracking()
            .SingleAsync(o => o.Id == opportunityId);
        var outbox = await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync();
        var evidence = await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId).ToListAsync();

        Assert.Equal(OpportunityStatus.Completed, opportunity.Status);
        Assert.Single(outbox);
        Assert.Single(evidence);
        Assert.Equal("enterprise.crmsales.opportunity.completed.v1", outbox[0].EventType);
        Assert.Equal(opportunity.RowVersion, outbox[0].AggregateVersion);
        Assert.Equal(opportunity.RowVersion, evidence[0].AggregateVersion);
        Assert.Equal(command.CorrelationId, outbox[0].CorrelationId);
    }

    [Fact]
    public async Task Retrying_with_the_same_key_replays_the_stored_response()
    {
        var (tenant, opportunityId) = await SeedOfferedOpportunityAsync();
        var command = new CompleteOpportunityCommand(
            tenant, opportunityId, TestData.Seller, IdempotencyKey: "key-2", CorrelationId: Guid.NewGuid());

        await using (var first = _fixture.CreateAdminContext())
        {
            await new CompleteOpportunityHandler(first).HandleAsync(command);
        }

        await using (var second = _fixture.CreateAdminContext())
        {
            var result = await new CompleteOpportunityHandler(second).HandleAsync(command);
            Assert.True(result.Replayed);
        }

        await using var verification = _fixture.CreateAdminContext();
        var outbox = await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync();

        Assert.Single(outbox);
    }

    [Fact]
    public async Task A_rejected_command_leaves_no_outbox_or_evidence_row()
    {
        var tenant = TestData.NextTenant();
        long opportunityId;

        await using (var seed = _fixture.CreateAdminContext())
        {
            var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
            seed.Parties.Add(party);
            await seed.SaveChangesAsync();

            // waiting durumunda: Complete() reddedecek.
            var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 100m);
            opportunity.AddLine(TestData.ProductRef(tenant), 1, 100m);
            seed.Opportunities.Add(opportunity);
            await seed.SaveChangesAsync();
            opportunityId = opportunity.Id;
        }

        var command = new CompleteOpportunityCommand(
            tenant, opportunityId, TestData.Seller, IdempotencyKey: "key-3", CorrelationId: Guid.NewGuid());

        await using (var context = _fixture.CreateAdminContext())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => new CompleteOpportunityHandler(context).HandleAsync(command));
        }

        await using var verification = _fixture.CreateAdminContext();
        Assert.Empty(await verification.OutboxMessages.AsNoTracking()
            .Where(m => m.AggregateId == opportunityId).ToListAsync());
        Assert.Empty(await verification.EvidenceRecords.AsNoTracking()
            .Where(e => e.AggregateId == opportunityId).ToListAsync());
        Assert.Empty(await verification.IdempotencyRecords.AsNoTracking()
            .Where(r => r.IdempotencyKey == "key-3").ToListAsync());
    }

    private async Task<(Contracts.TenantId TenantId, long OpportunityId)> SeedOfferedOpportunityAsync()
    {
        var tenant = TestData.NextTenant();
        await using var seed = _fixture.CreateAdminContext();

        var party = Party.Create(tenant, "Acme", PartyCreationSource.Manual);
        seed.Parties.Add(party);
        await seed.SaveChangesAsync();

        var opportunity = Opportunity.Create(tenant, party.Id, TestData.Seller, "TRY", 100m);
        opportunity.AddLine(TestData.ProductRef(tenant), quantity: 1, unitPrice: 100m);
        opportunity.Offer(DateTimeOffset.UtcNow.AddDays(7));
        seed.Opportunities.Add(opportunity);
        await seed.SaveChangesAsync();

        return (tenant, opportunity.Id);
    }
}
```

- [ ] **Step 2: Testleri çalıştır — derlenmemeli**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj --filter FullyQualifiedName~CompleteOpportunityHandlerTests
```

Beklenen: derleme hatası — `CRM.Application` namespace'i yok.

- [ ] **Step 3: Komut ve sonucu yaz**

`src/Modules/CRM/Application/CompleteOpportunityCommand.cs`:

```csharp
using Contracts;

namespace CRM.Application;

/// <summary>Modülün ilk public komutu (doc 08: modülün dış yüzeyi command/query/event'tir).
/// IdempotencyKey çağıran tarafından üretilir; CorrelationId çağrı zincirini bağlar (doc 04).</summary>
public sealed record CompleteOpportunityCommand(
    TenantId TenantId,
    long OpportunityId,
    PrincipalRef Principal,
    string IdempotencyKey,
    Guid CorrelationId);

public sealed record CompleteOpportunityResult(long OpportunityId, decimal TotalAmount, bool Replayed);
```

- [ ] **Step 4: Handler'ı yaz**

`src/Modules/CRM/Application/CompleteOpportunityHandler.cs`:

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CRM.Domain;
using CRM.Evidence;
using CRM.Idempotency;
using CRM.Outbox;
using CRM.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CRM.Application;

/// <summary>Doc 17 §6 madde 1 ("state + outbox + evidence commit together") ve doc 04'ün
/// IdempotencyKey primitive'i tek bir SaveChanges() çağrısında karşılanır. Tenant context
/// aynı transaction içinde set edilir, böylece RLS politikaları devrede olur.</summary>
public sealed class CompleteOpportunityHandler
{
    private const string Operation = "CompleteOpportunity";
    private const string EventType = "enterprise.crmsales.opportunity.completed.v1";
    private const string EventSource = "/enterprise/crm-sales";
    private static readonly TimeSpan IdempotencyRetention = TimeSpan.FromDays(7);

    private readonly CrmDbContext _context;

    public CompleteOpportunityHandler(CrmDbContext context) => _context = context;

    public async Task<CompleteOpportunityResult> HandleAsync(
        CompleteOpportunityCommand command,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        await _context.SetTenantContextAsync(command.TenantId, cancellationToken);

        var requestHash = Hash(command);

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
                throw new InvalidOperationException("Idempotency key reused with a different request payload.");

            await transaction.CommitAsync(cancellationToken);
            var replayed = JsonSerializer.Deserialize<CompletedPayload>(existing.ResponsePayload)!;
            return new CompleteOpportunityResult(replayed.OpportunityId, replayed.TotalAmount, Replayed: true);
        }

        var opportunity = await _context.Opportunities
            .Include(o => o.Lines)
            .SingleAsync(o => o.Id == command.OpportunityId, cancellationToken);

        opportunity.Complete();

        var payload = new CompletedPayload(opportunity.Id, opportunity.TotalAmount ?? 0m, opportunity.Currency);
        var payloadJson = JsonSerializer.Serialize(payload);

        _context.OutboxMessages.Add(OutboxMessage.Create(
            command.TenantId,
            aggregateType: nameof(Opportunity),
            aggregateId: opportunity.Id,
            aggregateVersion: opportunity.RowVersion,
            eventType: EventType,
            source: EventSource,
            subject: $"opportunities/{opportunity.Id}",
            correlationId: command.CorrelationId,
            causationId: null,
            payload: payloadJson));

        _context.EvidenceRecords.Add(EvidenceRecord.Create(
            command.TenantId,
            aggregateType: nameof(Opportunity),
            aggregateId: opportunity.Id,
            aggregateVersion: opportunity.RowVersion,
            principal: command.Principal,
            action: "Opportunity.Complete",
            detail: payloadJson,
            correlationId: command.CorrelationId));

        _context.IdempotencyRecords.Add(IdempotencyRecord.Create(
            command.TenantId,
            command.Principal,
            Operation,
            command.IdempotencyKey,
            requestHash,
            responseStatus: 200,
            responsePayload: payloadJson,
            retention: IdempotencyRetention));

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CompleteOpportunityResult(opportunity.Id, payload.TotalAmount, Replayed: false);
    }

    private static string Hash(CompleteOpportunityCommand command)
    {
        var canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"{command.TenantId.Value}|{command.OpportunityId}|{command.Principal}|{Operation}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record CompletedPayload(long OpportunityId, decimal TotalAmount, string Currency);
}
```

- [ ] **Step 5: Testleri çalıştır**

```bash
dotnet test tests/CRM.Tests/CRM.Tests.csproj
```

Beklenen: `Passed!` — tüm testler.

- [ ] **Step 6: Commit**

```bash
git add src tests
git commit -m "Add CompleteOpportunity command writing state, outbox, evidence and idempotency atomically

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: CI (FF36 dahil)

**Files:**
- Create: `.github/workflows/ci.yml`

- [ ] **Step 1: Workflow'u yaz**

`.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore --configuration Release

      # FF36: bilinen zafiyetli bağımlılık varsa build kırılır.
      - name: Vulnerable package check
        run: |
          dotnet list package --vulnerable --include-transitive 2>&1 | tee vulnerable.log
          if grep -q "has the following vulnerable packages" vulnerable.log; then
            echo "Vulnerable packages found"
            exit 1
          fi

      # Testcontainers ubuntu-latest üzerindeki Docker daemon'ı kullanır.
      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal
```

- [ ] **Step 2: Zafiyet kontrolünü yerelde çalıştır**

```bash
dotnet list package --vulnerable --include-transitive
```

Beklenen: `System.Security.Cryptography.Xml` 9.0.0 listelenir (EF Core Design üzerinden geliyor). CI'ı yeşile çekmek için `Microsoft.EntityFrameworkCore.Design` paketini 10.0.4'e yükselt ve `.config/dotnet-tools.json` içindeki `dotnet-ef` sürümünü de 10.0.4 yap:

```bash
dotnet add src/Modules/CRM/CRM.csproj package Microsoft.EntityFrameworkCore.Design --version 10.0.4
dotnet add src/Modules/Access/Access.csproj package Microsoft.EntityFrameworkCore.Design --version 10.0.4
dotnet tool uninstall dotnet-ef --local
dotnet tool install dotnet-ef --local --version 10.0.4
dotnet list package --vulnerable --include-transitive
```

Beklenen son çıktı: zafiyetli paket listelenmemeli. Hâlâ listeleniyorsa sürümü bulunan en güncel 10.x ile dene (`dotnet package search Microsoft.EntityFrameworkCore.Design`) ve sonucu raporla.

- [ ] **Step 3: Tüm testleri çalıştır**

```bash
dotnet build && dotnet test
```

Beklenen: `Passed!`, 0 uyarı hedeflenir (NU1903 uyarıları kalkmış olmalı).

- [ ] **Step 4: Commit**

```bash
git add .github .config src
git commit -m "Add CI workflow with vulnerable-package gate and bump EF tooling

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: Dokümanları kodla senkronla

**Files:**
- Modify: `docs/schema/crm-sales-schema.md`
- Modify: `docs/schema/identity-access-schema.md`
- Modify: `AGENTS.md`
- Modify: `README.md`

- [ ] **Step 1: CRM şema dokümanını güncelle**

`docs/schema/crm-sales-schema.md` başlığındaki "This is a drawing/design document; no EF Core code or migration has been written yet." cümlesini sil ve yerine şu bölümü dosyanın sonuna ekle:

```markdown
## Revision 4 (2026-09-16): implementation-side corrections

1. **`ck_opportunities_expiry_required_once_offered` düzeltildi.** Eski ifade
   (`status = 'waiting' OR expiry_date IS NOT NULL`) waiting → canceled geçişini de
   kapsıyor ve teklif verilmemiş bir opportunity'nin iptalini imkansız kılıyordu. Yeni
   ifade: `status NOT IN ('offered','completed') OR expiry_date IS NOT NULL`. Migration:
   `FixCancelExpiryCheck`.
2. **Satır iptali aggregate root üzerinden.** `OpportunityLine.Cancel` artık `internal`;
   çağrı `Opportunity.CancelLine(line, reason)` üzerinden yapılır ve root'un
   `row_version`'ını artırır. Aksi halde "en az bir aktif zorunlu satır" invariant'ı iki
   eşzamanlı işlem tarafından birlikte delinebiliyordu.
3. **`row_version` artışı interceptor'dan domain'e taşındı.** `CRM.Persistence.RowVersionInterceptor`
   kaldırıldı; `Opportunity` kendi versiyonunu mutasyon metotlarında artırıyor. Sebep:
   interceptor `SaveChanges` sırasında artırdığı için aynı transaction'da yazılan
   `outbox_messages.aggregate_version` bir eski değeri taşıyordu.
4. **Para kuralı uygulandı.** `opportunity_lines.line_total` satır eklenirken 4dp olarak
   hesaplanıyor; `opportunities.total_amount` `Complete()` içinde aktif zorunlu satırlardan
   türetilip tam olarak bir kez 2dp'ye yuvarlanıyor. Opsiyonel ve iptal edilmiş satırlar
   toplama girmez.
5. **RLS uygulandı.** `EnableRowLevelSecurity` migration'ı, tenant taşıyan dokuz tabloda
   `ENABLE`/`FORCE ROW LEVEL SECURITY` ve `tenant_isolation` politikası kuruyor; politika
   `current_setting('app.tenant_id', true)` okuyor, uygulama bunu transaction-local olarak
   yazıyor (`CrmDbContextTenantExtensions.SetTenantContextAsync`). Çalışma zamanı rolü
   `scripts/create-runtime-role.sql` ile ayrıca oluşturulur; izolasyon testleri bu rolle
   çalışır (FF03).
6. **İlk komut yazıldı.** `CRM.Application.CompleteOpportunityHandler` state + outbox +
   evidence + idempotency kaydını tek `SaveChanges()` içinde yazıyor (doc 17 §6 madde 1).
```

- [ ] **Step 2: Identity/Access şema dokümanının başlığını düzelt**

`docs/schema/identity-access-schema.md` başındaki "Revision 3 — ... no migration yet (needs `dotnet ef migrations add`, not hand-written per AGENTS.md)." cümlesini şununla değiştir:

```markdown
Revision 3 — EF Core entities, `AccessDbContext` and configurations written
(`src/Modules/Access/`); `InitialAccessSchema` migration generated and applied
(2026-09-14). RLS, `row_version DEFAULT 1` and the `role_assignments → roles` tenant-safety
question are still open — tracked in the Access follow-up plan.
```

- [ ] **Step 3: AGENTS.md'nin Status ve Testing bölümlerini güncelle**

`AGENTS.md` içindeki "## Status" bölümünün metnini şununla değiştir:

```markdown
## Status
`Contracts` primitives, the CRM+Sales pilot module (entities, `CrmDbContext`, migrations,
RLS, the `CompleteOpportunity` command) and the Identity+Access schema are implemented as of
2026-09-16. Design docs: `docs/schema/crm-sales-schema.md`, `docs/schema/identity-access-schema.md`.
Runtime baseline is .NET 10 LTS (the explicit supported-runtime decision doc 07 §1 asked for;
.NET 8's support ended 2026-11-10). Tests live in `tests/CRM.Tests` and run in CI
(`.github/workflows/ci.yml`). Access-side follow-ups are tracked in `docs/plans/`.
```

"## Testing / Definition of Done" bölümündeki "(not yet created — pending first module test)" ifadesini sil.

- [ ] **Step 4: README'ye test ve runtime rolü adımlarını ekle**

`README.md` içindeki yerel kurulum bölümünün sonuna ekle:

```markdown
### Testler

```bash
dotnet test            # Docker çalışıyor olmalı (Testcontainers ile gerçek PostgreSQL)
```

### Çalışma zamanı rolü (RLS)

Migration'lar superuser ile çalışır, uygulama çalışmaz. Yerel veritabanında bir kez:

```bash
psql -d fynovio_platform -f scripts/create-runtime-role.sql
```

Sonra uygulamayı bu rolle bağla:

```bash
export FYNOVIO_CRM_CONNECTION_STRING="Host=localhost;Database=fynovio_platform;Username=fynovio_app;Password=<parola>"
```
```

- [ ] **Step 5: Graph'ı güncelle ve commit**

```bash
graphify update .
git add docs README.md AGENTS.md graphify-out
git commit -m "Sync schema docs, AGENTS.md and README with the implemented enforcement work

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Sonraki plan (bu planın kapsamı dışında)

1. **Access modülü:** `role_assignments → roles` tenant güvenliği (karar gerekiyor: `role_tenant_id` + composite FK önerilen seçenek), `(tenant_id, name)` unique index, sistem rolü CHECK'i, `row_version DEFAULT 1`, `RoleAssignment.Revoke` tarih kontrolü, grant/revoke için evidence, Access'in Host'a bağlanması, `tests/Access.Tests`.
2. **Outbox dispatcher:** Worker içinde `processed_at IS NULL` satırlarını işleyen döngü, FF09 için tekrar/çift teslim testleri.
3. **HTTP yüzeyi:** `CompleteOpportunity` için endpoint, `Idempotency-Key` header'ı, BFF cookie oturumu (doc 19 §3).
4. **On-prem paketleme:** çok aşamalı Dockerfile, Compose bundle, sürüm atlama politikası, yükseltme scripti (doc 16 §5, §7).
