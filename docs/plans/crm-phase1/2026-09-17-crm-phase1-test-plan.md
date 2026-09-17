# CRM Phase 1 — Test Senaryoları ve Beklenen Sonuçlar

**Proje:** `tests/CRM.Tests/`
**Kapsam:** Phase 1 — Lifecycle/Pipeline foundation (bkz. [execution plan](2026-09-16-crm-phase1-lifecycle-pipeline-execution-plan.md)): `Opportunity` yaşam döngüsü (Draft/Open/Won/Lost), satır bazlı para hesabı, pipeline tanım/versiyon/aşama ağacı, `Opportunity.PartyRef` ile MasterData'ya referans, RLS ile tenant izolasyonu, `CompleteOpportunity` komutunun state+outbox+evidence+idempotency atomikliği.
**Toplam:** 79 test — orijinal 46 + [Test Coverage Verification Report](CRM_Phase1_Test_Coverage_Verification_Report.pdf)'un P0/P1 bulgularından 33 yeni test (bkz. §5).
**Doğrulama:** `dotnet test tests/CRM.Tests/CRM.Tests.csproj` ile 2026-09-17 tarihinde teyit edildi — **79/79 PASS**; solution genelinde (Access+CRM+MasterData) **167/167 PASS**, regresyon yok.

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

Aşağıdakiler §5'te anlatılan test-coverage review'un F-09 bulgusuyla eklendi — Draft/Open/Won/Lost × komut geçiş matrisinin daha önce test edilmemiş terminal-state/re-entrancy hücreleri:

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `AddLine_is_rejected_after_won` | `Won` durumundayken `AddLine(...)` | `InvalidOperationException` |
| `AddLine_is_rejected_after_lost` | `Lost` durumundayken `AddLine(...)` | `InvalidOperationException` |
| `Open_is_rejected_when_already_open` | Zaten `Open` durumundayken tekrar `Open()` | `InvalidOperationException` |
| `Open_is_rejected_when_won` | `Won` durumundayken `Open()` | `InvalidOperationException` |
| `Open_is_rejected_when_lost` | `Lost` durumundayken `Open()` | `InvalidOperationException` |
| `Win_is_rejected_when_already_won` | Zaten `Won` durumundayken tekrar `Win()` | `InvalidOperationException` |
| `Win_is_rejected_when_lost` | `Lost` durumundayken `Win()` | `InvalidOperationException` |
| `Lose_is_rejected_when_already_won` | `Won` durumundayken `Lose(...)` | `InvalidOperationException` |
| `Lose_is_rejected_when_already_lost` | Zaten `Lost` durumundayken tekrar `Lose(...)` | `InvalidOperationException` |
| `CancelLine_after_lost_is_rejected` | `Lost` durumundayken bir satır `CancelLine(...)` edilmeye çalışılır | `InvalidOperationException` |

### `OpportunityPipelineFieldsTests` (yeni, F-04)

`Opportunity.PipelineDefinitionVersionId`/`PipelineStageId` alanlarının Stage-belongs-to-Version tutarlılık açığının şu an **kurulamaz** olduğunu kanıtlayan regresyon kilidi — bir gün bu değişirse (bir komut bu alanlara yazmaya başlarsa) test kırılır, ki bu gerçek tutarlılık kontrolünün eklenmesi gerektiğinin tetikleyicisidir (bkz. delta plan F-08 karar notu).

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `PipelineDefinitionVersionId_and_PipelineStageId_have_no_public_setter` | Reflection ile iki property'nin `SetMethod`'u incelenir | İkisi de `public` değil |
| `No_public_command_assigns_a_pipeline_stage_or_version` | Reflection ile `Opportunity`'nin public metotları taranır | Adında "Stage" veya "Pipeline" geçen public bir metot **yok** |

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

### `PipelineRlsTests` (yeni, F-02 — gerçek bir güvenlik açığı buldu ve kapattı)

`pipeline_definitions`/`pipeline_definition_versions`/`pipeline_stages`, `AddPipelineTables` migration'ı `EnableRowLevelSecurity`'den *sonra* çalıştığı için RLS'siz kalmıştı — `fynovio_app` bu üç tabloda tenant filtresi olmadan okuyup yazabiliyordu. `EnableRowLevelSecurityOnPipelineTables` migration'ıyla kapatıldı.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Every_table_in_the_crm_schema_has_row_level_security_enabled_forced_and_policied` | `pg_class`/`pg_policies` katalogları üzerinden `crm` şemasındaki her tablo taranır (`__ef_migrations_history` hariç) | Her tablo `ENABLE` + `FORCE ROW LEVEL SECURITY` ve bir policy'ye sahip — sadece bu üç tablo için değil, gelecekteki herhangi bir tablo için de geçerli bir fitness function |
| `Runtime_role_cannot_read_another_tenants_pipeline_definition` | Tenant A ve B için birer `PipelineDefinition`; runtime rol tenant B context'iyle sorgular | Sonuç yalnızca tenant B'nin kaydını içerir |
| `Runtime_role_cannot_write_a_pipeline_definition_for_another_tenant` | Tenant B context'i altında tenant A'ya ait bir `PipelineDefinition` eklenmeye çalışılır | `DbUpdateException` — `InsufficientPrivilege` |
| `Runtime_role_cannot_read_another_tenants_pipeline_definition_version` | Aynı senaryo `PipelineDefinitionVersion` için | Sonuç yalnızca tenant B'nin kaydını içerir |
| `Runtime_role_cannot_read_another_tenants_pipeline_stage` | Aynı senaryo `PipelineStage` için | Sonuç yalnızca tenant B'nin kaydını içerir |

### `PipelineDefinitionPersistenceTests` (yeni, F-01 — gerçek bir veri bütünlüğü açığı buldu ve kapattı)

`PipelineDefinition.AddVersion`/`PipelineDefinitionVersion.AddStage` çocuğun parent FK'ını hiç set etmiyordu (`PipelineDefinitionId`/`PipelineDefinitionVersionId` her zaman `0`) — persist edilseydi FK constraint'e çarpardı. Mevcut testler saf in-memory olduğu için hiç yakalanmamıştı. Domain metotları düzeltildi (parent önce kaydedilmeli, `Id` sonra çocuğa aktarılır).

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Definition_version_and_stage_persist_and_reload_with_correct_relationships` | Definition → Version → Stage tam persistence round-trip (her seviye kendi `DbSet`'ine eklenir, kaydedilir, sonra bir sonraki seviye kurulur) | Yeniden yüklendiğinde `TenantId`, `PipelineDefinitionId`, `PipelineDefinitionVersionId`, `VersionNumber`, `Name`, `SortOrder` hepsi doğru |

### `PipelineConstraintTests` (yeni, F-03 + F-05)

DB'nin kendisinin — domain guard'ın değil — tenant-safe referential integrity'yi ve dört unique index'i uyguladığını kanıtlıyor. Admin (superuser) bağlantısı kasıtlı: FK/unique-index kontrolleri RLS'in aksine superuser'da da devrede kalır.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Version_referencing_a_foreign_tenants_definition_violates_composite_fk` | Ham SQL ile tenant B'ye ait, tenant A'nın definition'ına işaret eden bir version eklenir (public API bu uyumsuzluğu asla kuramaz) | `PostgresException`, `SqlState == ForeignKeyViolation` |
| `Stage_referencing_a_foreign_tenants_version_violates_composite_fk` | Aynı senaryo stage→version için | `PostgresException`, `SqlState == ForeignKeyViolation` |
| `Duplicate_pipeline_name_in_the_same_tenant_is_rejected_at_the_db` | Aynı tenant'ta aynı isimle iki `PipelineDefinition` | İkinci `SaveChangesAsync()` → `DbUpdateException` |
| `Same_pipeline_name_in_different_tenants_is_allowed` | İki farklı tenant'ta aynı isim | İkisi de başarılı |
| `Duplicate_version_number_on_the_same_definition_is_rejected_at_the_db` | Aynı definition, ikinci bir context'ten yeniden yüklenip (in-memory `_versions` boş, domain guard göremiyor) aynı `versionNumber` ile `AddVersion` | İkinci `SaveChangesAsync()` → `DbUpdateException` — DB unique index'in domain guard'dan bağımsız gerçek bir koruma olduğunu kanıtlıyor |
| `Duplicate_stage_name_on_the_same_version_is_rejected_at_the_db` | Aynı desenle aynı isimde iki stage | `DbUpdateException` |
| `Duplicate_sort_order_on_the_same_version_is_rejected_at_the_db` | Aynı desenle aynı `sortOrder`'da iki stage | `DbUpdateException` |

### `LegacyDataMigrationTests` (yeni, F-06 + F-07 — ayrı bir fixture/container)

Mevcut testler her zaman taze bir Testcontainers veritabanını doğrudan en son migration'a taşıyordu — bu yalnızca son şemanın ulaşılabilir olduğunu kanıtlıyordu, migration zincirinin **var olan (N-1) satırları** doğru yükselttiğini değil. `LegacyMigrationFixture`, CRM'i `RenameOpportunityLifecycle`'dan önceki son migration'a taşır, eski vocabulary'de (dört legacy status, eski kolon adları) bir `crm.parties` satırı ve dört `crm.opportunities` satırı seed eder, sonra en son migration'a kadar devam eder.

| Test | Senaryo | Beklenen sonuç |
|---|---|---|
| `Legacy_status_remaps_to_the_new_vocabulary` (4 case) | `waiting/offered/completed/canceled` statülü satırlar migrate edilir | Sırasıyla `Draft/Open/Won/Lost`'a eşleniyor |
| `Legacy_completed_opportunitys_sale_date_survives_as_won_date` | `sale_date` dolu bir `completed` satırı | Migration sonrası `WonDate` dolu |
| `Legacy_canceled_opportunitys_reason_and_date_survive_as_lost_fields` | `cancel_reason`/`cancel_date` dolu bir `canceled` satırı | Migration sonrası `LostReason`/`LostDate` doğru |
| `Legacy_opportunitys_party_id_survives_the_rename_to_party_ref_party_id` | Legacy `party_id` bir `crm.parties` satırına işaret ediyor | `party_ref_party_id` aynı id'yi (artık `masterdata.parties`'te) taşıyor |
| `Legacy_crm_party_backfills_into_masterdata_with_identity_and_tenant_preserved` | Gerçek bir `crm.parties` satırı `BackfillMasterDataParties` ile taşınıyor | `masterdata.parties`'te `tenant_id`, `name`, `surname`, `phone`, `email`, `party_type == Organization` hepsi korunmuş |

---

## Özet — sınıf başına test sayısı

| Sınıf | Dosya | Test sayısı |
|---|---|---|
| `OpportunityStateMachineTests` | `Domain/OpportunityStateMachineTests.cs` | 20 |
| `OpportunityMoneyTests` | `Domain/OpportunityMoneyTests.cs` | 6 |
| `OpportunityRowVersionTests` | `Domain/OpportunityRowVersionTests.cs` | 6 |
| `PipelineDefinitionVersionTests` | `Domain/PipelineDefinitionVersionTests.cs` | 4 |
| `OpportunityPipelineFieldsTests` | `Domain/OpportunityPipelineFieldsTests.cs` | 2 |
| `ModuleBoundaryTests` | `Architecture/ModuleBoundaryTests.cs` | 2 |
| `CompleteOpportunityHandlerTests` | `Integration/CompleteOpportunityHandlerTests.cs` | 6 |
| `OpportunityConcurrencyTests` | `Integration/OpportunityConcurrencyTests.cs` | 1 |
| `OpportunityPersistenceTests` | `Integration/OpportunityPersistenceTests.cs` | 3 |
| `PartyBackfillVerificationTests` | `Integration/PartyBackfillVerificationTests.cs` | 1 |
| `TenantIsolationTests` | `Integration/TenantIsolationTests.cs` | 7 |
| `PipelineRlsTests` | `Integration/PipelineRlsTests.cs` | 5 |
| `PipelineDefinitionPersistenceTests` | `Integration/PipelineDefinitionPersistenceTests.cs` | 1 |
| `PipelineConstraintTests` | `Integration/PipelineConstraintTests.cs` | 7 |
| `LegacyDataMigrationTests` | `Integration/LegacyDataMigrationTests.cs` | 8 |
| **Toplam** | | **79** |

Tüm testler `dotnet test tests/CRM.Tests/CRM.Tests.csproj` ile çalıştırıldığında **79/79 PASS** verir. Solution genelinde `dotnet test fynovio-platform.slnx` ile **167/167 PASS** (Access 58 + CRM 79 + MasterData 30), regresyon yok. Son doğrulama: 2026-09-17.

---

## 5. Test Coverage Verification Report'a yanıt (2026-09-17)

[`CRM_Phase1_Test_Coverage_Verification_Report.pdf`](CRM_Phase1_Test_Coverage_Verification_Report.pdf), yukarıdaki 46 testi binding spec + tamamlanmış Phase 1 execution plan kaydına karşı denetleyip 10 bulgu (F-01..F-10) önerdi. Rapor kendi sınırını açıkça belirtiyordu: bulgular yalnızca binding spec + execution plan'a dayanıyordu, gerçek koda karşı değil — "Each P0 item should therefore be verified against the actual test/source tree before remediation." Her bulgu gerçek kod okunarak doğrulandı:

**İki bulgu, sadece eksik test değil, gerçek implementasyon hatası çıktı:**

| ID | Bulgu | Doğrulama sonucu | Yapılan |
|---|---|---|---|
| F-02 (P0) | Pipeline tabloları için explicit non-superuser RLS testi yok | **Gerçek güvenlik açığı**: `AddPipelineTables`, `EnableRowLevelSecurity`'den *sonra* çalıştığı için üç tablo da hiç RLS almamıştı — `fynovio_app` tenant filtresi olmadan okuyup yazabiliyordu | `EnableRowLevelSecurityOnPipelineTables` migration'ı + katalog tabanlı fitness-function testi + per-table cross-tenant testleri |
| F-01 (P0) | Pipeline hierarchy persistence round-trip testi yok | **Gerçek veri bütünlüğü hatası**: `AddVersion`/`AddStage` parent FK'ı hiç set etmiyordu (`PipelineDefinitionId`/`PipelineDefinitionVersionId` her zaman `0`) — persist edilseydi FK constraint'e çarpardı | Domain metotları düzeltildi (parent önce kaydedilmeli) + round-trip testi |

**Diğer bulgular gerçek koddu, sadece eksik testti — hepsi kapatıldı:**

| ID | Bulgu | Yapılan |
|---|---|---|
| F-03 (P0) | Cross-tenant composite FK negatif testleri yok | `PipelineConstraintTests`: version→definition, stage→version |
| F-05 (P1) | DB unique constraint negatif testleri yok | `PipelineConstraintTests`: pipeline adı, version numarası, stage adı, sort order — dördü de domain guard'dan bağımsız DB-seviyesi testlerle |
| F-06 (P1) | Legacy lifecycle migration upgrade testi yok | `LegacyMigrationFixture` + `LegacyDataMigrationTests`: N-1'den seed, migrate, dört legacy status'ün doğru eşlendiği kanıtlandı |
| F-07 (P1) | Party backfill gerçek legacy satırlarla test edilmiyor | Aynı fixture: gerçek bir `crm.parties` satırı backfill'den geçiriliyor |
| F-09 (P1) | Lifecycle test suite tam bir transition matrix'e karşı map'lenmemiş | `OpportunityStateMachineTests`'e terminal-state/re-entrancy hücreleri eklendi |
| F-10 (P2) | Plan'daki completion/CI kanıtı normalize edilmeli | README.md/AGENTS.md/delta plan/execution plan'daki branch-era ("not yet merged", "not yet run", eski migration/test sayıları) ifadeler güncellendi |

**Kod değişikliği gerektirmeyen, karar kaydı olarak kapatılan bulgu:**

| ID | Bulgu | Sonuç |
|---|---|---|
| F-04 (P0/P1) | Opportunity'nin `PipelineDefinitionVersionId`/`PipelineStageId` tutarlılığı kanıtlanmamış | Doğrulama: bu alanların `private set`'i var ve hiçbir public komut onları set etmiyor — uyumsuzluk **şu an kurulamaz**. Gerçek bir CHECK/consistency kontrolü eklemek, hiçbir şeyin yazamadığı alanlar için scope creep olurdu. Bunun yerine `OpportunityPipelineFieldsTests` regresyon kilidi eklendi (bu doğru kalmayı bırakırsa test kırılır) ve delta plan'a Phase 2 önkoşulu olarak kaydedildi |
| F-08 (P0/P1) | `PipelineStage` şeklinin (stable key/code, active flag, capability/policy refs eksik) bilerek mi ertelendiği doğrulanmalı | Delta plan zaten "additive ve unenforced" diyordu; 2026-09-17'de bunun hangi alanları kapsadığı (stable key/code, active flag, capability/policy refs) ve Phase 2'nin `ChangePipelineStage` komutuyla ele alınacağı açıkça kayda geçirildi. Şema genişletilmedi |

Rapor kendi önerdiği 14 adversarial testin (T-01..T-14) tamamı bu sette karşılığını buluyor; F-04/F-08 için literal kod değişikliği yerine karar kaydı tercih edilmesinin gerekçesi yukarıda.
