# Enterprise Access Foundation — Test Senaryoları ve Beklenen Sonuçlar

**Proje:** `tests/Access.Tests/`
**Kapsam:** Phase 1.5 — Enterprise Access Foundation (bkz. [execution plan](2026-09-17-enterprise-access-foundation-execution-plan.md), [gap closure](2026-09-17-enterprise-access-foundation-gap-closure.md))
**Toplam:** 58 test — orijinal 33 + [Test Coverage Review](Enterprise_Access_Phase_1_5_Test_Coverage_Review.pdf)'un kritik+ikincil paketinden 25 yeni test (bkz. §5).
**Doğrulama:** `dotnet test tests/Access.Tests/Access.Tests.csproj` ile 2026-09-17 tarihinde teyit edildi — **58/58 PASS**; solution genelinde (Access+CRM+MasterData) **134/134 PASS**, regresyon yok.

---

## 1. Domain katmanı (`tests/Access.Tests/Domain/`)

Saf domain mantığı, DB'siz, hızlı çalışır.

### `ActionKeyTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Accepts_well_formed_keys("crm.opportunity.win")` | 3+ segmentli, küçük harf, nokta ayraçlı geçerli anahtar | `ActionKey` oluşur, `.Value` girilen string'e eşit |
| `Accepts_well_formed_keys("access.role_assignment.grant")` | Segment içinde alt çizgi (`_`) barındıran geçerli anahtar | Kabul edilir (regex düzeltmesi: `[a-z0-9_]`) |
| `Accepts_well_formed_keys("sales.quote.approve")` | Farklı bir modül namespace'i | Kabul edilir |
| `Rejects_malformed_keys("")` | Boş string | `ArgumentException` fırlatılır |
| `Rejects_malformed_keys("crm")` | Tek segment (2 nokta şartı sağlanmıyor) | `ArgumentException` |
| `Rejects_malformed_keys("crm.opportunity")` | İki segment, minimum 3 segment kuralına aykırı | `ArgumentException` |
| `Rejects_malformed_keys("CRM.Opportunity.Win")` | Büyük harf içeriyor | `ArgumentException` |
| `Rejects_malformed_keys("crm..win")` | Ardışık nokta / boş segment | `ArgumentException` |

### `PermissionSetTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Grant_rejects_duplicate_action_key` | Aynı `ActionKey` iki kez grant edilmeye çalışılır | İkinci çağrıda `InvalidOperationException` |
| `Grant_rejects_unsupported_relation` | `relation: "team_member"` gibi tanımsız bir relation ile grant | `ArgumentException` (yalnızca `null` veya `"owner"` geçerli) |
| `Grant_with_null_relation_is_allowed` | `relation` parametresi verilmeden grant | `item.Relation == null`, `set.Items` tek elemanlı |

### `RoleTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Create_rejects_invalid_origin` | `origin: "bogus"` ile `Role.Create` | `ArgumentException` (yalnızca `tenant`/`system_template` geçerli) |
| `Create_sets_tenant_id_not_null` | Normal `Role.Create` çağrısı | `role.TenantId` verilen `TenantId`'ye eşit (nullable değil) |
| `Create_defaults_to_tenant_origin` | `origin` parametresi verilmez | `role.Origin == Role.OriginTenant` |

### `RoleAssignmentTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Revoke_twice_throws` | Aynı assignment iki kez revoke edilir | İkinci çağrıda `InvalidOperationException` |
| `IsActiveAt_false_after_revocation` | `ValidFrom`'dan sonra grant, `ValidFrom+1h`'de revoke | `IsActiveAt(ValidFrom+30dk) == true`, `IsActiveAt(ValidFrom+2h) == false` |
| `Grant_rejects_invalid_source` | `source: "bogus"` ile `Grant` | `ArgumentException` (yalnızca `manual`/`bootstrap` geçerli) |
| `Revoke_before_valid_from_throws` | `ValidFrom`'dan önceki bir zamanda revoke denemesi | `ArgumentOutOfRangeException` |
| `Legacy_network_scope_fields_do_not_exist` **(H6, yeni)** | Reflection ile `RoleAssignment` tipinin property listesi taranır | `ScopeType`/`ScopeId` **yok** — cross-tenant `Network` kapsamı gap-closure §1'de kalıcı olarak kaldırıldı, geri gelmesine karşı regresyon kilidi |

### `TenantAccessStateTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `BumpRevision_increments_monotonically` | `BumpRevision()` iki kez çağrılır | `Revision == 2` |
| `Initialize_starts_at_zero` | Yeni `TenantAccessState.Initialize(tenantId)` | `Revision == 0` |

---

## 2. Mimari sınır testleri (`tests/Access.Tests/Architecture/`)

NetArchTest ile derleme-zamanı değil, reflection-tabanlı statik analiz; CRM.Tests'teki `ModuleBoundaryTests` ile simetrik (FF01).

### `ModuleBoundaryTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Access_does_not_depend_on_another_module` | `Access` assembly'sinin `CRM`, `MasterData`, `Organization`, `TenantLifecycle` namespace'lerine bağımlılığı taranır | Bağımlılık **yok** (`result.IsSuccessful == true`) — modüller yalnızca `Contracts` üzerinden konuşur |
| `Access_domain_does_not_depend_on_persistence` | `Access.Domain` namespace'indeki tipler `Access.Persistence`'a bağımlı mı taranır | Bağımlılık **yok** — domain katmanı persistence'tan izole |

---

## 3. Application katmanı — in-memory (`tests/Access.Tests/Application/`)

`AccessTestFixture` (gerçek Postgres, ama migration'lı tek context, transaction rollback yok — her test kendi unique tenant/principal üretir) üzerinden çalışır.

### `AccessAuthorizerTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Unregistered_action_is_denied` | Action registry'de hiç kayıtlı olmayan bir `ActionKey` ile authorize | `decision.IsAllowed == false`, `ReasonCode == "action_not_registered"` |
| `Unrecognized_principal_is_denied` | Owner-relation grant'i olan bir action'a, tamamen tanınmayan bir principal ile istek | `IsAllowed == false`, `ReasonCode == "principal_not_recognized"` |
| `Owner_relation_grant_denies_a_non_owned_resource` | `relation="owner"` grant'i var ama kaynak başka bir principal'a ait | `IsAllowed == false`, `ReasonCode == "no_matching_grant"` |
| `Unrestricted_grant_allows_regardless_of_resource_owner` | `relation=null` (tenant-scope) grant, kaynak başkasına ait olsa bile | `IsAllowed == true`, `ReasonCode == "tenant_scope_grant"` |
| `ResolveAccessScope_returns_None_when_no_effective_grant` **(C1, yeni)** | Kayıtlı bir principal + kayıtlı bir action, ama hiç grant yok | `AccessScope.None` döner |
| `ResolveAccessScope_returns_All_for_unrestricted_grant` **(C2, yeni)** | `relation=null` grant, scope-resolver tarafında | `AccessScope.All` döner (yalnızca `Authorize` tarafı değil, query-side de doğrulanıyor) |
| `Cross_tenant_actor_context_is_denied_despite_valid_grant_in_another_tenant` **(C3, yeni)** | Tenant A'da geçerli owner-relation grant'i olan principal, `ActorContext.TenantId` tenant B'ye değiştirilerek istek atar | `IsAllowed == false`, `ReasonCode == "no_matching_grant"` — `AccessAuthorizer`'daki `a.TenantId == request.Actor.TenantId` filtresinin yük taşıdığını kanıtlar (`ResourceDescriptor`'da tenant alanı yok — bkz. §5 not) |
| `Future_dated_assignment_does_not_authorize` **(C6, yeni)** | `ValidFrom` gelecekte olan bir `RoleAssignment` | `IsAllowed == false`, `ReasonCode == "no_matching_grant"` |
| `Revoked_assignment_does_not_authorize` **(C6, yeni)** | `ValidTo` geçmişte olan (revoke edilmiş) bir `RoleAssignment` | `IsAllowed == false`, `ReasonCode == "no_matching_grant"` |
| `Recognized_principal_with_no_grant_is_denied_with_no_matching_grant` **(H3, yeni)** | Tanınan principal, kayıtlı action, sıfır grant | `ReasonCode == "no_matching_grant"` — `Unrecognized_principal_is_denied`'daki `principal_not_recognized`'dan ayrıştırılıyor |
| `Additive_grants_across_two_roles_compose_without_replacement` **(H4, yeni)** | Aynı action'ı iki farklı rol üzerinden granting: biri `owner` ilişkili, diğeri kısıtsız (`null`) | `IsAllowed == true`, `ReasonCode == "tenant_scope_grant"` — grant'lar birleşik (additive) değerlendiriliyor, ilk eşleşen rol sonucu belirlemiyor |
| `Create_action_with_type_only_resource_is_allowed_by_unrestricted_grant` **(H7, yeni)** | `ResourceDescriptor(resourceType, Id: null, OwnerPrincipal: null)` — CREATE senaryosu | `IsAllowed == true`, `ReasonCode == "tenant_scope_grant"` |

### `AuthorizeResolveAccessScopeEquivalenceTests`

Round 3 §5'te kilitlenen değişmez kuralı doğrular: **`Authorize(actor,action,row) == Allow` ⟺ `row ∈ ResolveAccessScope(actor,action,resourceType)`**. Henüz CRM/domain adaptörü yok (Phase 2); sentetik `Test.Resource` ile Access tarafının kendi içinde tutarlı olduğu kanıtlanıyor.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Owner_relation_grant_is_consistent_across_both_contracts(actorOwnsResource: true)` | Owner-relation grant'i olan actor, kendi sahip olduğu kaynağa istek atar | `AuthorizeAsync` → Allow **ve** `ResolveAsync` dönen `AccessScope`, bu kaynağı (owner eşleşmesiyle) kapsıyor — ikisi birbiriyle tutarlı |
| `Owner_relation_grant_is_consistent_across_both_contracts(actorOwnsResource: false)` | Aynı actor, başkasına ait kaynağa istek atar | `AuthorizeAsync` → Deny **ve** scope bu kaynağı kapsamıyor — yine tutarlı |

---

## 4. Integration katmanı — gerçek Postgres (`tests/Access.Tests/Integration/`)

`PostgresFixture` (Testcontainers.PostgreSql) ile gerçek container ayağa kalkar, migration'lar uygulanır, RLS politikaları gerçek Postgres motorunda test edilir.

### `AccessRlsTests` (FF03 — RLS, ayrıcalıksız runtime rolüyle, süper kullanıcıyla değil)

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Runtime_role_cannot_see_another_tenants_roles` | Admin context ile tenant A ve tenant B için birer `Role` eklenir; runtime (kısıtlı) rol ile `app.tenant_id=A` set edilip `Roles` sorgulanır | Sonuç kümesi `role_a`'yı içerir, `role_b`'yi **içermez** — RLS izolasyonu çalışıyor |
| `Runtime_role_without_tenant_context_sees_nothing` | `Role` eklenir ama runtime bağlantısında `app.tenant_id` hiç set edilmez | Sorgu sonucu **boş** — `NULLIF(current_setting(...,true),'')::bigint` hiçbir satırla eşleşmiyor (fail-closed) |
| `Runtime_role_cannot_insert_tenant_access_state_for_another_tenant` **(C5 INSERT, yeni)** | `app.tenant_id=A` iken `tenant_access_state`'e `tenant_id=B` satırı eklenmeye çalışılır | `DbUpdateException` — `WITH CHECK` reddediyor |
| `Runtime_role_cannot_update_another_tenants_tenant_access_state` **(C5 UPDATE, yeni)** | `app.tenant_id=A` iken tenant B'nin `tenant_access_state` satırına ham SQL `UPDATE` | **0 satır etkilenir, exception yok** — `USING` satırı görünmez kılıyor; admin context'te değer değişmemiş olarak doğrulanır |
| `Runtime_role_cannot_delete_another_tenants_role_assignment` **(C5 DELETE, yeni)** | `app.tenant_id=A` iken tenant B'ye ait bir `role_assignments` satırına ham SQL `DELETE` | **0 satır etkilenir**, satır admin context'ten hâlâ okunabilir |

### `BootstrapTenantAccessHandlerTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Bootstrap_creates_tenant_administrator_role_and_assignment` | Action registry seed edilir, hesap+external identity oluşturulur, `BootstrapTenantAccessHandler` çağrılır | `TenantAccessState.Revision == 0`; oluşan `RoleAssignment.Source == "bootstrap"`; tam olarak **1** evidence kaydı, **1** outbox mesajı yazılmış |
| `Bootstrap_twice_for_the_same_tenant_throws` | Aynı tenant için bootstrap iki kez çağrılır | İkinci çağrıda `InvalidOperationException` (tenant zaten bootstrap edilmiş — "bootstrap problem" tekilliği korunuyor) |
| `Bootstrap_role_and_permission_set_are_tenant_owned_system_template_instances` **(H8, yeni)** | Bootstrap sonrası oluşan `Role`/`PermissionSet` incelenir | `TenantId` set edilmiş (global değil), `Origin == OriginSystemTemplate` — round 4 Decision A'nın (template → tenant-local instance) fiilen uygulandığını kanıtlar |

### `GrantRevokeRoleAssignmentHandlerTests`

5 aşamalı uçtan uca senaryo (round 3 §9'daki kritik sıralamayı kanıtlıyor) + review'un istediği gibi **bağımsız, tek-iddialı** testlerle desteklendi (§11: erken bir hata artık sonraki güvenlik iddialarının hiç çalışmamasına yol açmıyor):

| Aşama | Senaryo | Beklenen sonuç |
|---|---|---|
| 1. Grant | Admin, `grantee`'ye `tenant_administrator` rolünü verir | `result.Replayed == false`, yeni bir `RoleAssignmentId` döner |
| 2. Replay (aynı idempotency key, aynı istek) | Aynı komut aynı idempotency key ile tekrar gönderilir | `result.Replayed == true`, **aynı** `RoleAssignmentId` döner, yeni satır **oluşmaz** (DB'de tek kayıt doğrulanır) |
| 3. Idempotency key reuse (farklı istek) | Aynı idempotency key, farklı bir grantee (`thirdAccount`) ile kullanılır | `IdempotencyKeyReusedException` fırlatılır |
| 4. Revoke | Admin kendi `RoleAssignment`'ını revoke eder | `result.Replayed == false` |
| 5. Yetkisiz yeniden grant denemesi | Artık yetkisi kalmayan admin, `thirdAccount`'a grant vermeye çalışır | `AuthorizationDeniedException` fırlatılır, mesaj `"no_matching_grant"` içerir — **authorize adımı idempotency lookup'tan önce çalıştığı için** istek idempotency katmanına hiç ulaşmadan reddediliyor |

| Test (bağımsız) | Senaryo | Beklenen sonuç |
|---|---|---|
| `Grant_increments_tenant_access_revision_exactly_once` **(C7, yeni)** | Yeni grant öncesi/sonrası `TenantAccessState.Revision` | Tam olarak **+1** |
| `Revoke_increments_tenant_access_revision_exactly_once` **(C7, yeni)** | Revoke öncesi/sonrası `Revision` | Tam olarak **+1** |
| `Replay_of_grant_does_not_increment_tenant_access_revision` **(C8, yeni)** | Aynı idempotency key ile replay | `Revision` **değişmez** |
| `Denied_grant_does_not_increment_tenant_access_revision` **(C8, yeni)** | Hiç grant'i olmayan bir principal grant vermeye çalışır | `AuthorizationDeniedException`, `Revision` **değişmez** |
| `Previously_successful_request_cannot_replay_after_authority_revocation` **(C9, yeni)** | Admin başarıyla grant verir (key K) → kendi yetkisi revoke edilir → **aynı** istek + **aynı** key K tekrar gönderilir | `AuthorizationDeniedException` (`no_matching_grant`) — eski başarı **döndürülmüyor**; authorize her zaman idempotency lookup'tan önce çalıştığı için cache'e hiç ulaşılmıyor |
| `Grant_emits_exactly_one_evidence_and_outbox_record` **(C10, yeni)** | Grant sonrası evidence/outbox sayımı | İkisi de tam **1** |
| `Revoke_emits_exactly_one_evidence_and_outbox_record` **(C10, yeni)** | Revoke sonrası evidence/outbox sayımı | İkisi de tam **1** |
| `Replay_does_not_emit_additional_evidence_or_outbox_records` **(C10, yeni)** | Replay sonrası evidence/outbox sayımı | Hâlâ **1** — ikinci bir çift yazılmamış |
| `Idempotency_key_reuse_across_different_tenants_does_not_collide` **(H9, yeni)** | Aynı idempotency key string'i iki farklı tenant'ın adminleri tarafından kullanılır | Her ikisi de bağımsız başarıyla sonuçlanır (`Replayed == false`, farklı `RoleAssignmentId`) — lookup tenant+principal+operation+hash'e göre kapsamlı, çıplak key stringine göre değil |

### `AccessConstraintTests` (yeni sınıf — C4, H1, H2)

Gerçek Postgres constraint'lerini, domain katmanının kendisinin değil DB'nin uyguladığını kanıtlar. Admin (superuser) bağlantısı kasıtlı: FK/unique-index/concurrency-token kontrolleri RLS'in aksine superuser'da da devrede kalır.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `RoleAssignment_insert_with_cross_tenant_role_reference_violates_composite_fk` **(C4)** | `RoleAssignment.Grant(tenantA, ..., roleId: tenantB'nin rolü, ...)` ile kaydedilmeye çalışılır | `DbUpdateException` — `(tenant_id, role_id) → roles(tenant_id, id)` composite FK reddediyor |
| `Role_key_is_unique_per_tenant_but_reusable_across_tenants` **(H1)** | Aynı `Key` tenant A'da iki kez, sonra tenant B'de bir kez oluşturulur | Tenant A'daki ikinci deneme `DbUpdateException`; tenant B'deki deneme **başarılı** |
| `Concurrent_revoke_of_the_same_assignment_raises_optimistic_concurrency_conflict` **(H2)** | Aynı `RoleAssignment` iki context'te yüklenir, ikisi de `Revoke()` çağırır, ilk `SaveChanges` kazanır | İkinci `SaveChanges` → `DbUpdateConcurrencyException` (stale `row_version`) |

---

## Özet — sınıf başına test sayısı

| Sınıf | Dosya | Test sayısı (case dahil) |
|---|---|---|
| `ActionKeyTests` | `Domain/ActionKeyTests.cs` | 8 |
| `PermissionSetTests` | `Domain/PermissionSetTests.cs` | 3 |
| `RoleTests` | `Domain/RoleTests.cs` | 3 |
| `RoleAssignmentTests` | `Domain/RoleAssignmentTests.cs` | 5 |
| `TenantAccessStateTests` | `Domain/TenantAccessStateTests.cs` | 2 |
| `ModuleBoundaryTests` | `Architecture/ModuleBoundaryTests.cs` | 2 |
| `AccessAuthorizerTests` | `Application/AccessAuthorizerTests.cs` | 12 |
| `AuthorizeResolveAccessScopeEquivalenceTests` | `Application/AuthorizeResolveAccessScopeEquivalenceTests.cs` | 2 |
| `AccessRlsTests` | `Integration/AccessRlsTests.cs` | 5 |
| `BootstrapTenantAccessHandlerTests` | `Integration/BootstrapTenantAccessHandlerTests.cs` | 3 |
| `GrantRevokeRoleAssignmentHandlerTests` | `Integration/GrantRevokeRoleAssignmentHandlerTests.cs` | 10 |
| `AccessConstraintTests` | `Integration/AccessConstraintTests.cs` | 3 |
| **Toplam** | | **58** |

Tüm testler `dotnet test tests/Access.Tests/Access.Tests.csproj` ile çalıştırıldığında **58/58 PASS** verir; `dotnet test fynovio-platform.slnx` ile solution genelinde **134/134 PASS** (Access 58 + CRM 46 + MasterData 30), regresyon yok. Son doğrulama: 2026-09-17.

---

## 5. Test Coverage Review'a yanıt (2026-09-17)

Owner'ın sağladığı [`Enterprise_Access_Phase_1_5_Test_Coverage_Review.pdf`](Enterprise_Access_Phase_1_5_Test_Coverage_Review.pdf), yukarıdaki 33 testi frozen mimari invariant'larına karşı denetleyip 10 kritik (C1-C10) + 9 ikincil (H1-H9) eksik önerdi. Kod tabanı incelendi: **hiçbir gerçek kod hatası bulunmadı** — `AccessAuthorizer`/`AccessScopeResolver`/`GrantRoleAssignmentHandler`/`RevokeRoleAssignmentHandler` zaten doğru davranıyordu (ör. revision bump'ı yalnızca gerçek mutasyondan sonra, authorize her zaman idempotency lookup'tan önce). Eksik olan, bu doğru davranışı **kanıtlayan testlerdi**. İstisna: iki test fixture'ı (`AccessTestFixture`, `PostgresFixture`) `RowVersionInterceptor`'ı Host'un aksine hiç kaydetmiyordu — bu gerçek bir fixture açığıydı, düzeltildi (H2'yi mümkün kılmak için).

**Eklenen:** C1-C10'un tamamı, H1-H4 ve H6-H9 — toplam 25 yeni test (yukarıdaki tablolarda **(yeni)** işaretli).

**Review'da istenen ama literal haliyle uygulanmayan 3 madde** (frozen mimari kararlarla çelişiyor, dondurulmuş tasarım bilerek korundu):

| ID | Review'un istediği | Neden literal uygulanmadı | Ne yapıldı |
|---|---|---|---|
| C3 | `ResourceDescriptor`'a bir "resource tenant" alanı ekleyip actor tenant'ıyla karşılaştırmak | `ResourceDescriptor = { ResourceType, Id?, OwnerPrincipal? }` — round 3'ün "minimal Contracts surface" dondurma kararı; tenant alanı yok | `Cross_tenant_actor_context_is_denied_despite_valid_grant_in_another_tenant` testi eklendi: `AccessAuthorizer.cs:30`'daki `a.TenantId == request.Actor.TenantId` filtresinin gerçekten yük taşıdığını, farklı bir `ActorContext.TenantId` ile aynı grant'in artık eşleşmediğini kanıtlıyor |
| H9 | Idempotency key'i "tenant + principal/**acting-for** + action + fingerprint" ile bağlamak | `ActorContext` kasıtlı olarak `ActingFor`/`ImpersonatedBy` içermiyor (DESIGN/FREEZE) | `Idempotency_key_reuse_across_different_tenants_does_not_collide` testi eklendi — kapsam tenant + principal + operation + request-hash'e daraltıldı (handler'ın gerçekten filtrelediği alanlar) |
| H5 | CRM/Sales/MasterData'nın Access implementasyonuna/persistence'ına referans vermediğini test etmek | **Zaten var** — `CRM.Tests/Architecture/ModuleBoundaryTests.cs:19` ve `MasterData.Tests/Architecture/ModuleBoundaryTests.cs:23`, `"Access"`'i yasaklı bağımlılık listesinde tutuyor | Yeni test yazılmadı, mevcut testler doğrulandı |

H6 ise neredeyse totolojik: `ScopeType`/`ScopeId` deprecate değil, tamamen kaldırıldı — çalışma zamanında test edilecek bir davranış yok. Yine de tek satırlık bir reflection testi (`Legacy_network_scope_fields_do_not_exist`) eklendi, ki bu şekil geleceğe kilitlensin.
