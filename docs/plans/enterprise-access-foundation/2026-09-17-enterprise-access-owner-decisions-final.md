# Enterprise Access Foundation — Owner Decisions Final Closure

> **Kapsam:** Round 1–3 mimari incelemeleri tamamlandı ([[2026-09-17-enterprise-access-foundation-review.md]],
> [[2026-09-17-enterprise-access-foundation-review-round2.md]],
> [[2026-09-17-enterprise-access-foundation-review-round3-final.md]] — hiçbiri değiştirilmedi).
> Bu dosya, Round 3'ün açık bıraktığı iki owner kararını owner'ın verdiği nihai kararla
> kapatır ve repo'ya karşı doğrular. Kod/migration/implementation yok.

---

## 1. Owner Decisions Closed

### Decision A — System Catalog

```text
Platform Template
→ tenant-local instance (provisioning'de kopyalanır)

No continuous reconciliation in Phase 1.5.
```

Action Registry platform-owned kalır (değişmedi). Role ve PermissionSet **runtime
instance'ları** tenant-local'dır, `tenant_id NOT NULL`. Template'ler platform'un elinde
provisioning-seed olarak durur; provisioning anında tenant'a kopyalanır. Kopyalandıktan
sonra template değişse bile mevcut tenant satırları **otomatik değişmez** — reconciler,
silent propagation, "platform template değişince tenant konfigürasyonu değişir" mekanizması
Phase 1.5'te yok. Provenance için `origin`/`origin_key`/`origin_version` gibi metadata
kullanılabilir (`origin = system_template` semantiği), ama kolon adları bu turda freeze
edilmiyor.

### Decision B — Opportunity Owner

```text
Owner = AssignedPrincipal
AssignedPrincipal is mutable through explicit Reassign domain behavior.
No separate OwnerPrincipalRef.
```

`Opportunity.Owner` kavramsal olarak `Opportunity.AssignedPrincipal`'a eşittir. Bu alan
artık yalnızca `Create()`'te set edilen immutable bir değer değil, yaşam döngüsü boyunca
explicit bir domain operasyonuyla (`Reassign(newPrincipal)` veya repo naming convention'ına
uygun eşdeğeri) değiştirilebilir olmak zorunda. `created_by`/creator hiçbir zaman ownership
değildir, yalnızca tarihsel provenance'tır. Ayrı bir `OwnerPrincipalRef` **açılmayacak** —
bugün "assigned salesperson" ile "commercial owner" arasında ayrı iki business responsibility
olduğuna dair bir requirement yok; böyle bir ihtiyaç doğarsa bu ayrı bir architecture
decision'dır, bugün değil.

**Reassignment'ın taşıması gereken enterprise kontroller (implement edilmiyor, yalnızca
target behavior olarak doğrulanıyor):** authorization, concurrency (`RowVersion`), audit/
evidence, outbox/domain event (ör. `OpportunityReassigned` — kesin isim bu turda freeze
edilmiyor). Ownership değişikliği hiçbir zaman sessiz bir DB update olamaz.

**Query authorization etkisi:** `OwnedBy(P) = Opportunity.AssignedPrincipal == P`. Bu, hem
`Authorize(actor, crm.opportunity.read, opportunity)` hem
`opportunity ∈ ResolveAccessScope(actor, crm.opportunity.read)` tarafından **aynı** ownership
fact'i olarak kullanılır — single-resource authorization ve query authorization farklı owner
tanımına asla sahip olamaz (round 1 §10'daki eşdeğerlik invariant'ının doğrudan sonucu).

---

## 2. Repository Impact (yalnızca liste — implementasyon yok)

**Entities**
- `Role` (`src/Modules/Access/Domain/Authorization/Role.cs`): `TenantId?` → `TenantId`
  (nullable kalkacak); `IsSystem` alanının anlamı "bugünkü tenant_id=NULL sistem rolü"
  yerine "bu satır bir template'ten kopyalandı" olacak şekilde yeniden çerçevelenecek;
  provenance alanları (`origin`/`origin_key`/`origin_version` benzeri) eklenecek.
- Yeni `PermissionSet` entity'si (bugün repo'da hiç yok) — `TenantId` NOT NULL, provenance
  alanları aynı örüntüyle.
- `Opportunity` (`src/Modules/CRM/Domain/Opportunity.cs`): yeni bir `Reassign(PrincipalRef
  newPrincipal, ...)` public domain metodu — bugün `AssignedPrincipal` yalnız `Create()`
  içinde set ediliyor, başka hiçbir metotta değişmiyor.

**Enums**
- Decision A için yeni enum gerekmiyor (`IsSystem` bool zaten var, provenance string/enum
  olabilir ama bu turda seçilmiyor).
- Decision B için doğrudan bir enum yok; olası bir `OpportunityReassigned` fact/event tipi
  ileride CRM'in event vocabulary'sine eklenecek.

**Configurations**
- `RoleConfiguration.cs`: `tenant_id` dönüşümü nullable'dan NOT NULL'a geçecek;
  `(tenant_id, name)` / `(tenant_id, key)` uniqueness eklenecek (round 1'de zaten
  IMPLEMENT NOW'daydı).
- Yeni `PermissionSetConfiguration.cs`.
- `RolePermissionConfiguration.cs`: `Role → PermissionSet → Permission` üçlüsüne uygun
  olarak `RolePermissionSetConfiguration` + `PermissionSetItemConfiguration`'a
  dönüşecek (round 1 §8, tablo #2-#5).
- `Opportunity`'nin EF konfigürasyonunda yeni bir alan değişikliği gerekmiyor —
  `AssignedPrincipal` zaten mevcut kolonları kullanıyor, yalnızca domain tarafında
  mutasyon metodu eklenecek.

**Migrations**
- `roles.tenant_id`: `nullable: true` → `NOT NULL`. Bugün repo'da hiç veri yok (Host'a
  kayıtlı bile değil), bu yüzden veri taşıma riski **sıfır** — round 1'in migration
  riski listesindeki "system role satırlarının tenant'lara çoğaltılması" senaryosu
  bu repo için geçerli değil, boş tablodan başlanıyor.
- Yeni `permission_sets`, `permission_set_items`, `role_permission_sets` tabloları;
  mevcut `role_permissions` tablosunun sökümü.
- `Opportunity` tablosunda şema değişikliği gerekmiyor (kolon zaten var).

**Commands / Handlers**
- Yeni bir `ReassignOpportunity` command + handler (bugün repo'da yok) — Round 3'ün final
  pipeline'ına uygun sırayla: authorize → concurrency check (RowVersion) → domain mutasyon
  → evidence + outbox, tek `SaveChanges()` (mevcut `CompleteOpportunityHandler` kalıbı
  örnek alınabilir, ama bu turda yazılmıyor).

**Tests**
- `tests/Access.Tests` (bugün hiç yok) — `roles.tenant_id NOT NULL` invariant'ı, yeni
  `PermissionSet` domain testleri.
- `tests/CRM.Tests` — `Opportunity.Reassign()` için yeni domain testleri (mutasyon,
  concurrency, olası invariant: hangi status'larda reassign edilebilir).

**Contracts**
- `PrincipalRef` zaten mevcut, `Reassign`'in parametresi olarak kullanılabilir — yeni bir
  Contracts tipi gerekmiyor Decision B için.
- Decision A'nın `origin`/template kavramı için Contracts'ta yeni bir primitive
  gerekmiyor; bu Access'in kendi domain modelinde kalır.

---

## 3. Frozen Invariants

```text
roles.tenant_id NOT NULL
permission_sets.tenant_id NOT NULL

Action Registry platform-owned

Role / PermissionSet runtime instances tenant-local

No automatic template reconciler in Phase 1.5

Opportunity Owner = AssignedPrincipal

AssignedPrincipal must support explicit reassignment

created_by != owner

No OwnerPrincipalRef in Phase 1.5

OwnedBy uses AssignedPrincipal

Authorize() and ResolveAccessScope() use identical ownership semantics
```

---

## 4. Remaining Owner Decisions

```text
NONE
```

Repository doğrulaması bu iki kararı imkânsız kılan hiçbir architecture-breaking bulgu
ortaya çıkarmadı:

- `Role.TenantId` bugün `TenantId?` (nullable) — Decision A ile **çelişmiyor**, tam
  tersine bu değişikliğin gerekçesi. Veri boş olduğu için geçiş maliyeti yok.
- `Permission` bugün zaten `TenantId` taşımıyor (global/platform-owned) — Action Registry
  = platform-owned kararıyla **zaten uyumlu**, değişiklik gerekmiyor.
- `RoleAssignment.TenantId` bugün zaten `NOT NULL` — bu karardan etkilenmiyor.
- `AssignedPrincipal` bugün immutable, ama bu bir **çelişki değil**, yalnızca eksik bir
  domain yeteneği (Reassign metodu yok) — Decision B'nin repo impact listesinde zaten
  kayıtlı (§2).
- `IsSystem` bool alanı ve tek migration'daki "system role" yorumları Decision A ile
  çakışmıyor; anlamları yeniden çerçevelenecek ama alan adı/varlığı korunabilir.

Round 1–3'te kapanmış hiçbir karar bu turda yeniden açılmadı.

---

## 5. Final Architecture Gate

```text
ARCHITECTURE APPROVED
OWNER DECISIONS CLOSED
READY FOR PHASE 1.5 EXECUTION PLANNING
```
