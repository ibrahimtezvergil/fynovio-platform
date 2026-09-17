# CRM Phase 1 — Test Senaryoları ve Beklenen Sonuçlar

**Proje:** `tests/CRM.Tests/`
**Kapsam:** Phase 1 — Lifecycle/Pipeline foundation (bkz. [execution plan](2026-09-16-crm-phase1-lifecycle-pipeline-execution-plan.md)): `Opportunity` yaşam döngüsü (Draft/Open/Won/Lost), satır bazlı para hesabı, pipeline tanım/versiyon/aşama ağacı, `Opportunity.PartyRef` ile MasterData'ya referans, RLS ile tenant izolasyonu, `CompleteOpportunity` komutunun state+outbox+evidence+idempotency atomikliği.
**Toplam:** 46 test.
**Doğrulama:** `dotnet test tests/CRM.Tests/CRM.Tests.csproj` ile 2026-09-17 tarihinde teyit edildi — **46/46 PASS**; solution genelinde (Access+CRM+MasterData) **134/134 PASS** (bkz. [Enterprise Access Foundation test planı](../enterprise-access-foundation/2026-09-17-enterprise-access-foundation-test-plan.md) §"Özet").

---

## 1. Domain katmanı (`tests/CRM.Tests/Domain/`)

Saf domain mantığı, DB'siz, hızlı çalışır.

### `OpportunityStateMachineTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Create_starts_in_draft` | Yeni `Opportunity.Create(...)` çağrısı | `Status == Draft`, `ExpiryDate == null` |
| `Create_rejects_a_currency_that_is_not_three_letters` | `"TRYX"` gibi 3 harften uzun bir para birimi | `ArgumentException` |
| `Open_requires_a_future_expiry_date` | Geçmiş tarihli `expiryDate` ile `Open()` | `ArgumentOutOfRangeException` |
| `Open_moves_to_open_and_stamps_opened_date` | Gelecek tarihli `expiryDate` ile `Open()` | `Status == Open`, `OpenedDate` ve `ExpiryDate` dolu |
| `Win_is_rejected_while_draft` | Hâlâ `Draft` durumdayken `Win()` | `InvalidOperationException` |
| `Win_is_rejected_without_an_active_required_line` | Yalnızca opsiyonel bir satır varken `Open()` + `Win()` | `InvalidOperationException` — en az bir aktif zorunlu satır şartı |
| `Win_succeeds_with_one_active_required_line` | Bir zorunlu aktif satır ile `Open()` + `Win()` | `Status == Won`, `WonDate` dolu |
| `Lose_requires_a_reason` | Boş/boşluk `reason` ile `Lose()` | `ArgumentException` |
| `Lose_from_draft_is_allowed` | `Draft` durumdan doğrudan `Lose("...")` | `Status == Lost`, `LostDate` dolu |
| `AddLine_is_rejected_after_open` | `Open()` sonrası `AddLine(...)` | `InvalidOperationException` — satırlar yalnızca Draft'ta eklenebilir |

### `OpportunityMoneyTests`

docs/schema/crm-sales-schema.md "Money: one rounding rule" invariant'ı: girilen değerler 2 hane, hesaplanan değerler 4 hane; toplam satırlardan türetilir ve `Won` geçişinde tam olarak bir kez 2 haneye yuvarlanır.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `AddLine_computes_the_line_total` | `quantity: 3, unitPrice: 33.33` | `line.LineTotal == 99.99` |
| `AddLine_rejects_a_unit_price_with_more_than_two_decimals` | `unitPrice: 33.335` (3 ondalık) | `ArgumentException` |
| `Create_rejects_an_estimated_amount_with_more_than_two_decimals` | `estimatedAmount: 10.001` | `ArgumentException` |
| `Win_derives_the_total_from_active_required_lines` | İki zorunlu satır (`2×50`, `1×25.50`) ile `Open()` + `Win()` | `TotalAmount == 125.50` |
| `Win_excludes_optional_lines_from_the_total` | Bir zorunlu + bir opsiyonel satır ile `Win()` | `TotalAmount == 100.00` — opsiyonel satır toplama girmiyor |
| `Win_excludes_canceled_lines_from_the_total` | Bir aktif + bir iptal edilmiş satır ile `Win()` | `TotalAmount == 100.00` — iptal edilen satır toplama girmiyor |

### `OpportunityRowVersionTests`

`row_version` artışı domain metotlarında olur; aynı transaction'da yazılan outbox/evidence kayıtları güncel versiyonu okuyabilsin diye.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `A_new_opportunity_starts_at_version_one` | Yeni `Opportunity` | `RowVersion == 1` |
| `Every_mutation_increments_the_version_once` | Sırasıyla `AddLine()` → `Open()` → `Win()` | `RowVersion` sırasıyla `2` → `3` → `4` |
| `Canceling_a_line_increments_the_root_version` | Bir satır `CancelLine(...)` ile iptal edilir | `RowVersion` bir artar, `line.IsCanceled == true` |
| `Canceling_a_line_of_another_opportunity_is_rejected` | Başka bir `Opportunity`'nin satırı iptal edilmeye çalışılır | `InvalidOperationException` |
| `Canceling_a_line_after_completion_is_rejected` | `Win()` sonrası bir satır iptal edilmeye çalışılır | `InvalidOperationException` |
| `A_rejected_mutation_does_not_change_the_version` | Reddedilen bir `Win()` çağrısı (Draft'tayken) | `RowVersion` değişmeden kalır |

### `PipelineDefinitionVersionTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `AddVersion_rejects_a_duplicate_version_number` | Aynı `versionNumber` bir tanıma iki kez eklenir | `InvalidOperationException` |
| `AddStage_rejects_a_duplicate_sort_order_within_one_version` | Aynı versiyon içinde aynı `sortOrder` ile iki aşama eklenir | `InvalidOperationException` |
| `Two_versions_can_reuse_the_same_stage_names` | İki farklı versiyon aynı aşama adını (`"Lead"`) kullanır | İkisi de kabul edilir; her versiyonun `Stages` listesinde 1 kayıt |
| `Two_versions_can_reuse_the_same_sort_orders` | İki farklı versiyon aynı `sortOrder` değerlerini (`1, 2`) kullanır | İkisi de kabul edilir; her versiyonda 2'şer aşama |

---

## 2. Mimari sınır testleri (`tests/CRM.Tests/Architecture/`)

NetArchTest ile reflection-tabanlı statik analiz; `Access.Tests`/`MasterData.Tests`'teki `ModuleBoundaryTests` ile simetrik (FF01, doc 12).

### `ModuleBoundaryTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Crm_does_not_depend_on_another_module` | `CRM` assembly'sinin `Access`, `MasterData`, `Organization`, `TenantLifecycle` namespace'lerine bağımlılığı taranır | Bağımlılık **yok** (`result.IsSuccessful == true`) |
| `Crm_domain_does_not_depend_on_persistence` | `CRM.Domain` namespace'indeki tipler `CRM.Persistence`'a bağımlı mı taranır | Bağımlılık **yok** — domain katmanı persistence'tan izole |

---

## 3. Integration katmanı — gerçek Postgres (`tests/CRM.Tests/Integration/`)

`PostgresFixture` (Testcontainers.PostgreSql) ile gerçek container ayağa kalkar; hem `CrmDbContext` hem `MasterDataDbContext` migration'ları sırayla uygulanır (MasterData önce — `crm.opportunities` → `masterdata.parties` FK zincirine bağlı `PartyRef` yüzünden), RLS politikaları gerçek Postgres motorunda test edilir.

### `CompleteOpportunityHandlerTests`

FF07 + FF08 (doc 12): state, outbox, evidence ve idempotency kaydı tek transaction'da commit olur; aynı anahtarla tekrar gelen komut tek iş etkisi yaratır.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Completing_writes_state_outbox_and_evidence_together` | Açık (Open) bir opportunity üzerinde `CompleteOpportunityHandler` çağrılır | `Status == Won`; tam olarak **1** outbox mesajı + **1** evidence kaydı; `EventType`, `Source`, `Subject`, `AggregateVersion`, `CorrelationId`, `Action`, `PrincipalSubject` alanları doğru |
| `Retrying_with_the_same_key_replays_the_stored_response` | Aynı idempotency key ile komut ikinci kez gönderilir | `Replayed == true`, aynı sonuç döner, yeni outbox/evidence satırı **oluşmaz** |
| `Reusing_a_key_for_a_different_request_is_rejected` | Aynı idempotency key, farklı bir opportunity için kullanılır | `IdempotencyKeyReusedException`; ikinci opportunity'nin durumu değişmeden kalır |
| `A_rejected_command_leaves_no_outbox_evidence_or_idempotency_row` | Hâlâ Draft'taki (zorunlu satırı olan ama `Open()` edilmemiş) bir opportunity'de `Win()` reddedilir | `InvalidOperationException`; outbox, evidence ve idempotency kayıtlarının **hiçbiri** yazılmaz |
| `Completing_works_under_the_runtime_role` | Komut, ayrıcalıksız runtime rolü bağlantısıyla çalıştırılır | Başarıyla tamamlanır — superuser gerekmiyor |
| `A_command_for_another_tenants_opportunity_is_not_found_under_the_runtime_role` | Runtime rolü, yanlış tenant context'iyle başka bir tenant'ın opportunity'sini tamamlamaya çalışır | `OpportunityNotFoundException`; opportunity durumu değişmeden kalır — RLS satırı görünmez kılıyor |

### `OpportunityConcurrencyTests`

"En az bir aktif zorunlu satır" kuralı satırlar arası bir invariant'tır ve tek satırlık bir CHECK ile ifade edilemez (docs/schema/crm-sales-schema.md). Bu yüzden satır değişikliği aggregate root'un `row_version`'ını artırmalı — aksi halde iki eşzamanlı işlem kuralı birlikte delebilir.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Canceling_the_last_required_line_conflicts_with_completing` | Aynı opportunity iki ayrı context'te yüklenir; biri son zorunlu satırı iptal edip kaydeder, diğeri (artık bayat olan) veriyle `Win()` çağırıp kaydetmeye çalışır | İkinci `SaveChangesAsync()` → `DbUpdateConcurrencyException` |

### `OpportunityPersistenceTests`

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Losing_a_draft_opportunity_persists` | Draft bir opportunity `Lose(...)` edilip kaydedilir, yeniden okunur | `Status == Lost` |
| `Losing_an_open_opportunity_persists` | Open bir opportunity `Lose(...)` edilip kaydedilir, yeniden okunur | `Status == Lost` |
| `Line_total_and_won_total_are_persisted` | Bir satır eklenip `Open()` + `Win()` yapılır, kaydedilir, yeniden okunur | `TotalAmount` ve `line.LineTotal` doğru yuvarlanmış haliyle (`99.99`) DB'den geri geliyor |

### `PartyBackfillVerificationTests`

Not: bu sınıf başlangıçta `crm.parties` vs `masterdata.parties` satır/kolon karşılaştırması için tasarlanmıştı; `crm.parties` `DropCrmParties` migration'ıyla kalıcı olarak kaldırıldığı ve Testcontainers her testte migration geçmişinin tamamını (drop dahil) tepeden uyguladığı için o karşılaştırma artık test edilemez hale geldi. Kalan tek test, `PostgresFixture`'ın çift-DbContext migration bağlantısının ve masterdata şema izinlerinin gerçekten çalıştığını kanıtlıyor.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `A_party_seeded_through_masterdata_is_readable_back` | `TestData.CreatePartyAsync` ile MasterData şemasına bir `Party` seed edilir, `MasterDataDbContext` üzerinden geri okunur | `TenantId`, `Name`, `PartyType == Organization` doğru şekilde eşleşiyor |

### `TenantIsolationTests` (FF03 — RLS, ayrıcalıksız runtime rolüyle, süper kullanıcıyla değil)

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Runtime_role_cannot_read_another_tenants_rows` | Tenant A ve tenant B için birer opportunity eklenir; runtime rol tenant B context'iyle `Opportunities` sorgular | Sonuç kümesi yalnızca tenant B'nin opportunity'sini içerir |
| `Runtime_role_cannot_read_a_guessed_id_from_another_tenant` | Tenant B context'indeyken tenant A'nın opportunity id'si tahmin edilip doğrudan sorgulanır | `null` döner |
| `Runtime_role_sees_nothing_without_a_tenant_context` | `app.tenant_id` hiç set edilmeden sorgu yapılır | Sonuç **boş** (fail-closed) |
| `Tenant_context_does_not_survive_on_a_pooled_connection` | Tek bağlantılı bir pool'da tenant context set edilip commit edilir; aynı pool'dan yeni bir context açılır | Yeni context'te hiçbir satır görünmüyor — tenant context bağlantı havuzunda sızmıyor |
| `Runtime_role_cannot_write_a_row_for_another_tenant` | Tenant B context'i altında tenant A'ya ait bir opportunity eklenmeye çalışılır | `DbUpdateException` — iç `PostgresException.SqlState == InsufficientPrivilege` |
| `Tenant_context_requires_an_explicit_transaction` | Açık bir transaction olmadan `SetTenantContextAsync(...)` çağrılır | `InvalidOperationException` |
| `Runtime_role_cannot_update_or_delete_evidence` | Runtime rolüyle `crm.evidence_records` tablosuna ham SQL `DELETE` denenir | `PostgresException`, `SqlState == InsufficientPrivilege` |

---

## Özet — sınıf başına test sayısı

| Sınıf | Dosya | Test sayısı |
|---|---|---|
| `OpportunityStateMachineTests` | `Domain/OpportunityStateMachineTests.cs` | 10 |
| `OpportunityMoneyTests` | `Domain/OpportunityMoneyTests.cs` | 6 |
| `OpportunityRowVersionTests` | `Domain/OpportunityRowVersionTests.cs` | 6 |
| `PipelineDefinitionVersionTests` | `Domain/PipelineDefinitionVersionTests.cs` | 4 |
| `ModuleBoundaryTests` | `Architecture/ModuleBoundaryTests.cs` | 2 |
| `CompleteOpportunityHandlerTests` | `Integration/CompleteOpportunityHandlerTests.cs` | 6 |
| `OpportunityConcurrencyTests` | `Integration/OpportunityConcurrencyTests.cs` | 1 |
| `OpportunityPersistenceTests` | `Integration/OpportunityPersistenceTests.cs` | 3 |
| `PartyBackfillVerificationTests` | `Integration/PartyBackfillVerificationTests.cs` | 1 |
| `TenantIsolationTests` | `Integration/TenantIsolationTests.cs` | 7 |
| **Toplam** | | **46** |

Tüm testler `dotnet test tests/CRM.Tests/CRM.Tests.csproj` ile çalıştırıldığında **46/46 PASS** verir. Solution genelinde `dotnet test fynovio-platform.slnx` ile **134/134 PASS** (Access 58 + CRM 46 + MasterData 30), regresyon yok. Son doğrulama: 2026-09-17.
