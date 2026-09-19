# CRM Phase 2 — Test Kapsamı ve Senaryolar

> Bu dosya, `crm-phase2/opportunity-commands-api` branch'inde (24 görevlik uygulama planı) yazılan tüm test dosyalarının ve her testin kapsadığı senaryo/beklentinin envanteridir. Kaynak: `main..HEAD` diff'inde değişen `tests/CRM.Tests/**` ve `tests/Host.Tests/**` dosyaları (`git log --name-only main..HEAD -- 'tests/*'`).
>
> Toplam: full solution 213 test geçiyor (Access.Tests 60, CRM.Tests 121, Host.Tests 2, MasterData.Tests 30). Bu dosya CRM.Tests + Host.Tests'in içeriğini detaylandırır; Access.Tests/MasterData.Tests Phase 2 kapsamında sadece dolaylı olarak (JWT/tenant membership altyapısı için) dokunuldu, ayrı modüllerin kendi test paketleri oldukları için burada listelenmiyor.

---

## 1. Domain katmanı (`tests/CRM.Tests/Domain/`)

Saf domain mantığı — veritabanı yok, sadece `Opportunity`/`PipelineDefinitionVersion` aggregate'lerinin kuralları.

### `OpportunityStateMachineTests.cs` — durum makinesi ve geçiş kuralları
| Test | Senaryo → Beklenti |
|---|---|
| `Create_starts_in_draft` | Yeni oluşturulan opportunity `Draft` durumunda başlar, `ExpiryDate` null. |
| `Create_rejects_a_currency_that_is_not_three_letters` | 4 karakterli para birimi kodu (`"TRYX"`) → `ArgumentException`. |
| `Open_requires_a_future_expiry_date` | Geçmiş bir `expiryDate` ile `Open()` → `ArgumentOutOfRangeException`. |
| `Open_moves_to_open_and_stamps_opened_date` | `Open()` sonrası durum `Open`, `OpenedDate` ve `ExpiryDate` dolu. |
| `Win_is_rejected_while_draft` | `Draft` durumda `Win()` → `InvalidOperationException`. |
| `Win_is_rejected_without_an_active_required_line` | Tek satır var ama `isOptional: true` → aktif zorunlu satır yok → `Win()` reddedilir. |
| `Win_succeeds_with_one_active_required_line` | Bir zorunlu satırla `Win()` → durum `Won`, `WonDate` dolu. |
| `Lose_requires_a_reason` | Boş/whitespace `lostReason` → `ArgumentException`. |
| `Lose_from_draft_is_allowed` | Draft'tan doğrudan `Lose()` legal → durum `Lost`, `LostDate` dolu. |
| `AddLine_is_rejected_after_open` | Open sonrası `AddLine()` → `InvalidOperationException`. |
| `AddLine_is_rejected_after_won` | Won sonrası `AddLine()` → `InvalidOperationException`. |
| `AddLine_is_rejected_after_lost` | Lost sonrası `AddLine()` → `InvalidOperationException`. |
| `Open_is_rejected_when_already_open` | İkinci `Open()` çağrısı → `InvalidOperationException`. |
| `Open_is_rejected_when_won` | Won durumdayken `Open()` → `InvalidOperationException`. |
| `Open_is_rejected_when_lost` | Lost durumdayken `Open()` → `InvalidOperationException`. |
| `Win_is_rejected_when_already_won` | İkinci `Win()` çağrısı → `InvalidOperationException`. |
| `Win_is_rejected_when_lost` | Lost durumdayken `Win()` → `InvalidOperationException`. |
| `Lose_is_rejected_when_already_won` | Won durumdayken `Lose()` → `InvalidOperationException`. |
| `Lose_is_rejected_when_already_lost` | İkinci `Lose()` çağrısı → `InvalidOperationException`. |
| `CancelLine_after_lost_is_rejected` | Lost sonrası satır iptali → `InvalidOperationException`. |
| `Reassign_changes_the_assigned_principal` | `Reassign(newOwner)` → `AssignedPrincipal` güncellenir. |
| `Reassign_is_rejected_once_won` | Won durumda `Reassign()` → `InvalidOperationException`. |
| `Reassign_is_rejected_once_lost` | Lost durumda `Reassign()` → `InvalidOperationException`. |
| `ChangeStage_only_while_open` | Draft'ta `ChangeStage()` → `InvalidOperationException`. |
| `ChangeStage_sets_the_new_stage_while_open` | Open'da `ChangeStage(30)` → `PipelineStageId = 30`, `PipelineDefinitionVersionId` değişmez. |

*Not: bu dosyanın son 12 testi (`AddLine_is_rejected_after_won`'dan itibaren) Phase 1 coverage raporunun (F-09) eksik bıraktığı terminal-state/re-entrancy reddi matrisini tamamlamak için Phase 2'de eklendi.*

### `OpportunityMoneyTests.cs` — para/yuvarlama kuralı
| Test | Senaryo → Beklenti |
|---|---|
| `AddLine_computes_the_line_total` | 3 × 33.33 → `LineTotal = 99.99`. |
| `AddLine_rejects_a_unit_price_with_more_than_two_decimals` | `33.335` birim fiyat → `ArgumentException`. |
| `Create_rejects_an_estimated_amount_with_more_than_two_decimals` | `10.001` tahmini tutar → `ArgumentException`. |
| `Win_derives_the_total_from_active_required_lines` | İki satırdan (`2×50` + `1×25.50`) toplam `TotalAmount = 125.50` türetilir. |
| `Win_excludes_optional_lines_from_the_total` | `isOptional: true` satır toplama dahil edilmez. |
| `Win_excludes_canceled_lines_from_the_total` | İptal edilmiş satır toplama dahil edilmez. |

### `OpportunityPipelineFieldsTests.cs` — pipeline alanlarının atanma noktası
| Test | Senaryo → Beklenti |
|---|---|
| `Open_assigns_the_supplied_pipeline_version_and_stage` | `Open(version:10, stage:20)` → her iki alan da set edilir. |
| `Open_with_no_pipeline_configured_leaves_both_fields_null` | Pipeline verilmezse her iki alan da null kalır. |
| `Open_rejects_a_stage_supplied_without_its_version` | Stage var, version null → `ArgumentException`. |
| `No_command_other_than_Open_assigns_a_pipeline_stage_or_version` | `Open()` dışındaki hiçbir komut (`Win()` dahil) bu alanlara dokunmaz — Phase 1 versiyonundan daraltıldı (artık `Open()` tek istisna). |

### `OpportunityRowVersionTests.cs` — `RowVersion` artış noktası
| Test | Senaryo → Beklenti |
|---|---|
| `A_new_opportunity_starts_at_version_one` | Yeni kayıt `RowVersion = 1`. |
| `Every_mutation_increments_the_version_once` | `AddLine`→2, `Open`→3, `Win`→4 — her domain metodu tam bir artış yapar. |
| `Canceling_a_line_increments_the_root_version` | Satır iptali root aggregate'in versiyonunu artırır (satır kendi başına versiyonlanmaz). |
| `Canceling_a_line_of_another_opportunity_is_rejected` | Başka bir opportunity'nin satırını iptal etmeye çalışmak → `InvalidOperationException`. |
| `Canceling_a_line_after_completion_is_rejected` | Won sonrası satır iptali → `InvalidOperationException`. |
| `A_rejected_mutation_does_not_change_the_version` | Reddedilen bir `Win()` çağrısı versiyonu **artırmaz**. |

### `PipelineDefinitionVersionTests.cs` — pipeline tanım/versiyon/aşama kuralları
| Test | Senaryo → Beklenti |
|---|---|
| `AddVersion_rejects_a_duplicate_version_number` | Aynı `versionNumber` ikinci kez eklenirse → `InvalidOperationException`. |
| `AddStage_rejects_a_duplicate_sort_order_within_one_version` | Aynı versiyon içinde aynı `sortOrder` → `InvalidOperationException`. |
| `Two_versions_can_reuse_the_same_stage_names` | Farklı versiyonlar aynı aşama adını kullanabilir. |
| `Two_versions_can_reuse_the_same_sort_orders` | Farklı versiyonlar aynı `sortOrder` değerlerini kullanabilir. |
| `AddStage_the_first_stage_added_becomes_entry_by_default` | Bir versiyona eklenen ilk aşama otomatik `IsEntry = true`, `IsActive = true`. |
| `AddStage_a_second_stage_is_not_entry_by_default` | İkinci eklenen aşama `IsEntry = false`. |
| `MarkEntry_moves_the_entry_flag_to_the_target_stage_only` | `MarkEntry(second)` → eski entry `false`, yeni entry `true` (tek aşamada entry). |

---

## 2. Uygulama katmanı — komut handler'ları (`tests/CRM.Tests/Application/`)

Her handler testi gerçek Postgres'e karşı çalışır (`PostgresFixture`, `CreateAdminContext()` = superuser bağlantısı, RLS'i bypass eder — bkz. §4). Ortak desen: **yetkilendirme (allow/deny) + idempotency (replay) + domain kuralı ihlali**.

### `CreateOpportunityHandlerTests.cs`
- `Creating_persists_a_draft_opportunity_with_evidence_and_outbox` — başarılı create → `Draft` durumda kayıt + 1 outbox mesajı + 1 evidence kaydı.
- `Creating_is_denied_without_a_grant_for_the_action` — `StubAuthorizer.AlwaysDeny` → `OpportunityAuthorizationDeniedException`, hiçbir satır yazılmaz.
- `Retrying_with_the_same_key_replays_the_same_opportunity_id` — aynı idempotency key ile ikinci çağrı → `Replayed = true`, aynı `OpportunityId` döner.

### `AddOpportunityLineHandlerTests.cs`
- `Adding_a_line_to_a_draft_opportunity_persists_it_and_writes_evidence` — draft'a satır eklenir, evidence yazılır.
- `Adding_a_line_to_an_opened_opportunity_is_rejected_by_the_domain_invariant` — Open sonrası satır eklemek → `InvalidOperationException` (domain guard, HTTP katmanına ulaşmadan).

### `CancelOpportunityLineHandlerTests.cs`
- `Canceling_a_line_persists_the_cancellation_and_writes_evidence` — satır `IsCanceled = true` olur.
- `Retrying_with_the_same_key_replays_without_reapplying_the_cancellation` — replay'de tekrar iptal işlenmez, evidence tek kayıt kalır.

### `OpenOpportunityHandlerTests.cs`
- `Opening_assigns_the_tenants_entry_stage_when_a_pipeline_exists` — tenant'ın pipeline'ı varsa `Open()` otomatik entry stage'i atar (`result.PipelineStageId == entryStage.Id`).
- `Opening_leaves_pipeline_fields_null_when_the_tenant_has_no_pipeline` — pipeline yoksa `PipelineStageId` null kalır, hata fırlatılmaz.

### `ChangePipelineStageHandlerTests.cs`
- `Changing_to_an_active_stage_in_the_same_version_succeeds` — aynı versiyondaki aktif bir aşamaya geçiş başarılı.
- `Changing_to_an_inactive_stage_is_rejected` — `Deactivate()` edilmiş aşamaya geçiş → `InvalidPipelineTransitionException`.
- `Changing_to_a_stage_from_a_different_version_is_rejected` — farklı bir pipeline versiyonuna ait aşama → `InvalidPipelineTransitionException` (cross-aggregate doğrulama, handler'da — domain katmanı başka bir aggregate'in tablosunu sorgulamaz).

### `LoseOpportunityHandlerTests.cs`
- `Losing_persists_the_reason_and_writes_evidence_and_outbox` — `Lost` durumu, `LostReason` kalıcı, 1 outbox + 1 evidence.
- `Losing_is_denied_without_a_grant_for_the_action` → `OpportunityAuthorizationDeniedException`.
- `Retrying_with_the_same_key_replays_without_reapplying_the_loss` — replay'de tek outbox/evidence kaydı kalır.

### `ReassignOpportunityHandlerTests.cs`
- `Reassigning_persists_the_new_owner_and_writes_evidence_and_outbox` — `AssignedPrincipal` yeni sahibe güncellenir, outbox+evidence yazılır.
- `Reassigning_is_denied_without_a_grant_for_the_action` → reddedilir, sahip **değişmez** (untouched assert).
- `Retrying_with_the_same_key_replays_without_reapplying_the_reassignment` — replay'de tekrar uygulanmaz.

### Entegrasyon klasöründeki komut testi — `WinOpportunityHandlerTests.cs` (`tests/CRM.Tests/Integration/`)
Bu handler, template görevi gördüğü için (ActorContext + auth + reordered idempotency×concurrency pipeline) en geniş senaryo setine sahip:
- `Winning_writes_state_outbox_and_evidence_together` — durum `Won`, `TotalAmount = 100.00`, outbox event type `enterprise.crmsales.opportunity.won.v1`, `AggregateVersion` güncel `RowVersion`'a eşit, evidence action `"Opportunity.Win"`.
- `Winning_is_denied_without_a_grant_for_the_action` — reddedilince durum `Open` olarak kalır (untouched).
- `Winning_with_a_stale_expected_version_is_a_concurrency_conflict` — yanlış `expectedVersion` (999) → `OpportunityConcurrencyConflictException`.
- `Retrying_with_the_same_key_replays_the_stored_response` — replay aynı `TotalAmount`'ı döner, outbox tek kayıt kalır.
- `Reusing_a_key_for_a_different_request_is_rejected` — aynı idempotency key farklı bir opportunity için kullanılırsa → `IdempotencyKeyReusedException`.
- `Two_concurrent_first_attempts_with_the_same_key_leave_exactly_one_winner_and_the_loser_replays_it` — eşzamanlı ikinci istek (unique-violation yakalanarak) kazananın sonucunu replay eder, ikinci bir outbox mesajı **yazılmaz** — Phase 2'de düzeltilen "raw uncaught `DbUpdateException`" bug'ının regresyon testi.

---

## 3. Uygulama katmanı — sorgu handler'ları (`tests/CRM.Tests/Application/`)

Sorgular hiçbir zaman `OpportunityAuthorizationDeniedException` fırlatmaz — reddedilme ile "bulunamadı" ayrımı dışarıya sızdırılmaz (architecture plan §15, tenant-non-leak kuralı).

### `GetOpportunityHandlerTests.cs`
- `Returns_the_opportunity_when_authorized` — yetkiliyken dolu DTO döner.
- `Returns_null_when_denied_indistinguishable_from_not_found` — reddedilince `null` döner (exception yok).
- `Returns_null_for_a_genuinely_missing_opportunity` — gerçekten yok olan bir ID de `null` döner — iki durum ayırt edilemez.

### `ListOpportunitiesHandlerTests.cs`
- `All_scope_returns_every_opportunity_in_the_tenant` — `AccessScope.All` → tenant'taki tüm kayıtlar.
- `None_scope_returns_nothing` — `AccessScope.None` → boş sonuç.
- `OwnedBy_scope_returns_only_that_principals_opportunities` — `AccessScope.AnyOf([OwnedBy(seller)])` → yalnızca o principal'a ait kayıtlar; SQL `WHERE`'e uygulanır (fetch-then-filter değil).

*Not: handler'ın kendisi `Where(o => o.TenantId == query.TenantId)`'i açıkça uygular — testin çalıştığı `CreateAdminContext()` superuser bağlantısı RLS'i bypass ettiği için, bu açık filtre olmadan cross-tenant veri sızabilirdi (Phase 2'de bulunan gerçek hata, code-quality review'de yakalandı). Ayrıca sayfalama `OrderByDescending(CreatedAt).ThenByDescending(Id)` ile deterministik hale getirildi (tek başına `CreatedAt` toplam sıralama garanti etmiyordu).*

### `GetPipelineStagesHandlerTests.cs`
- `Returns_every_stage_for_the_requested_version_in_sort_order` — bir versiyonun tüm aşamaları `sortOrder`'a göre sıralı döner, ilk aşama `IsEntry = true`.

### `GetOpportunityAvailableActionsHandlerTests.cs`
- `An_open_opportunity_with_a_billable_line_can_win_lose_change_stage_and_reassign` — Open + faturalanabilir satır → `CanOpen=false, CanChangeStage=true (hedef listesinde mevcut aşama hariç diğer aktif aşamalar), CanWin=true, CanLose=true, CanReassign=true`.
- `A_draft_opportunity_can_only_open_and_lose` — Draft → `CanOpen=true, CanChangeStage=false, CanWin=false, CanLose=true, CanReassign=true`.
- `Denied_authorization_makes_that_action_unavailable_even_when_the_domain_guard_passes` — `DenyingAuthorizer("crm.opportunity.win")` ile sadece `win` reddedilir → domain guard geçse bile `CanWin=false`, ama `CanLose=true` kalır (denial'ın tek bir action key'e izole olduğunun kanıtı — bu test, ilk review turunda authorization×domain-guard AND mantığının hiç kanıtlanmadığı fark edilince follow-up olarak eklendi).

---

## 4. Entegrasyon testleri (`tests/CRM.Tests/Integration/`)

### `OpportunityPersistenceTests.cs` — temel kalıcılık
- `Losing_a_draft_opportunity_persists`, `Losing_an_open_opportunity_persists` — her iki başlangıç durumundan da `Lose()` kalıcı.
- `Line_total_and_won_total_are_persisted` — `LineTotal` ve `TotalAmount` DB'ye doğru yuvarlanmış şekilde yazılır/okunur.

### `OpportunityConcurrencyTests.cs` — satırlar-arası invariant'ın eşzamanlılık koruması
- `Canceling_the_last_required_line_conflicts_with_completing` — iki context aynı opportunity'yi yükler; biri son zorunlu satırı iptal ederken diğeri `Win()` çağırırsa → ikinci `SaveChangesAsync()` → `DbUpdateConcurrencyException`. ("En az bir aktif zorunlu satır" kuralı tek satırlık CHECK ile ifade edilemeyen bir invariant olduğu için, satır değişikliği root'un `RowVersion`'ını artırmalı — aksi halde iki eşzamanlı işlem kuralı birlikte delebilir.)

### `PipelineConstraintTests.cs` — DB seviyesinde referential integrity + unique index'ler
- `Version_referencing_a_foreign_tenants_definition_violates_composite_fk` — raw SQL ile yanlış tenant'ın definition'ına referans → `PostgresException` (FK violation).
- `Stage_referencing_a_foreign_tenants_version_violates_composite_fk` — aynısı stage↔version için.
- `Duplicate_pipeline_name_in_the_same_tenant_is_rejected_at_the_db` — aynı tenant'ta aynı isim ikinci kez → `DbUpdateException`.
- `Same_pipeline_name_in_different_tenants_is_allowed` — farklı tenant'larda aynı isim serbest.
- `Duplicate_version_number_on_the_same_definition_is_rejected_at_the_db` — domain guard'ı bypass eden (yeniden yüklenmiş, `_versions` boş) bir senaryoda DB'nin kendi unique index'i devreye girer.
- `Duplicate_stage_name_on_the_same_version_is_rejected_at_the_db`, `Duplicate_sort_order_on_the_same_version_is_rejected_at_the_db` — aynı mantık stage seviyesinde.
- `Two_entry_stages_in_the_same_version_violate_the_partial_unique_index` — reflection ile ikinci bir aşamayı zorla `IsEntry=true` yapıp kaydetmek → partial unique index ihlali (`DbUpdateException`) — DB'nin, aggregate'in `MarkEntry`'sinden bağımsız gerçek bir güvenlik ağı olduğunun kanıtı.
- `MarkEntry_persisted_via_the_safe_two_phase_pattern_moves_entry_either_direction` — tek `SaveChangesAsync()`'in EF'in UPDATE sırası belirsizliği yüzünden güvenli olmadığını, iki fazlı (önce eski entry'yi `false` yap+kaydet, sonra yeniyi `true` yap+kaydet) deseninin her iki yönde de (yüksek-Id'ye ve düşük-Id'ye) güvenli olduğunu kanıtlar. *(Bu test, yazılırken gerçek bir hatayı ortaya çıkardı: tek save'te entry'yi düşük-Id'li bir aşamaya taşımak spurious `23505` fırlatıyordu.)*

### `PipelineDefinitionPersistenceTests.cs`
- `Definition_version_and_stage_persist_and_reload_with_correct_relationships` — `PipelineDefinition → Version → Stage` hiyerarşisinin tam round-trip'i; her seviye ayrı `SaveChangesAsync()` ile kaydedilip yeniden yüklendiğinde tenant/ilişki/alan değerleri doğru. *(Bu testi yazmak Phase 1'de gerçek bir defect ortaya çıkardı — kardeş commit `AddVersion`/`AddStage`'i düzeltti.)*

### `PipelineRlsTests.cs` — pipeline tablolarında RLS
- `Every_table_in_the_crm_schema_has_row_level_security_enabled_forced_and_policied` — **fitness function**: `crm` şemasındaki *her* tablo (mevcut + gelecekteki) `pg_class`/`pg_policies` katalogundan RLS enabled+forced+policied olarak doğrulanır — hardcoded tablo listesi yüzünden yeni bir tablo sessizce RLS'siz kalamaz.
- `Runtime_role_cannot_read_another_tenants_pipeline_definition` / `..._version` / `..._pipeline_stage` — runtime rolüyle (superuser değil) başka tenant'ın pipeline verisi görünmez.
- `Runtime_role_cannot_write_a_pipeline_definition_for_another_tenant` — başka tenant adına yazma → `PostgresException` (`InsufficientPrivilege`).

### `TenantIsolationTests.cs` — opportunity tablolarında RLS/tenant izolasyonu
- `Runtime_role_cannot_read_another_tenants_rows` — sadece kendi tenant'ının kayıtları görünür.
- `Runtime_role_cannot_read_a_guessed_id_from_another_tenant` — ID tahmin edilse bile `SingleOrDefaultAsync` null döner.
- `Runtime_role_sees_nothing_without_a_tenant_context` — `SetTenantContextAsync` çağrılmadan hiçbir satır görünmez.
- `Tenant_context_does_not_survive_on_a_pooled_connection` — bağlantı havuzunda (`MaxPoolSize=1`) bir önceki transaction'ın tenant context'i, yeni context'e sızmaz.
- `Runtime_role_cannot_write_a_row_for_another_tenant` — başka tenant adına satır ekleme → `InsufficientPrivilege`.
- `Tenant_context_requires_an_explicit_transaction` — transaction açmadan `SetTenantContextAsync()` çağırmak → `InvalidOperationException`.
- `Runtime_role_cannot_update_or_delete_evidence` — evidence tablosu runtime rol için append-only; `DELETE` → `InsufficientPrivilege`.

### `PartyBackfillVerificationTests.cs`
- `A_party_seeded_through_masterdata_is_readable_back` — `PostgresFixture`'ın çift-DbContext (CRM+MasterData) migration kablolamasının ve masterdata şema izinlerinin gerçekten çalıştığının kanıtı.

### `LegacyDataMigrationTests.cs` — geriye dönük migration doğruluğu (`IClassFixture<LegacyMigrationFixture>`, önceden var olan N-1 satırlar seed edilir)
- `Legacy_status_remaps_to_the_new_vocabulary` *(Theory, 4 durum)* — eski durum sözlüğü (`waiting/offered/completed/canceled`) → yeni (`Draft/Open/Won/Lost`) doğru eşlenir.
- `Legacy_completed_opportunitys_sale_date_survives_as_won_date` — eski `sale_date` → yeni `WonDate`.
- `Legacy_canceled_opportunitys_reason_and_date_survive_as_lost_fields` — eski iptal nedeni/tarihi → `LostReason`/`LostDate`.
- `Legacy_opportunitys_party_id_survives_the_rename_to_party_ref_party_id` — kolon adı değişse de veri korunur.
- `Legacy_crm_party_backfills_into_masterdata_with_identity_and_tenant_preserved` — `crm.parties` düşürülmeden önce `masterdata.parties`'e kimlik/tenant korunarak backfill edilmiş.

### `OutboxDispatcherServiceTests.cs` — Worker dispatcher (Task 23)
- `A_single_dispatch_pass_marks_every_pending_message_processed` — bekleyen bir outbox mesajı `DispatchOnceAsync()` sonrası `ProcessedAt` dolu olarak işaretlenir. (`SingleContextScopeFactory`/`NonDisposingScope` test-only stub'ları, `IServiceScopeFactory`'yi tek bir zaten-kurulmuş context'e sabitler.)

---

## 5. Mimari sınır testi (`tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`)

`NetArchTest` ile derleme-zamanı fitness function'lar (FF01, doc 12):
- `Crm_does_not_depend_on_another_module` — CRM assembly'si `Access`/`MasterData`/`Organization`/`TenantLifecycle`'a bağımlı olamaz.
- `Crm_domain_does_not_depend_on_persistence` — `CRM.Domain` namespace'i `CRM.Persistence`'a bağımlı olamaz.
- `Crm_does_not_reference_Access` — (daraltılmış/tekrar eden kontrol) `Access`'e hiçbir bağımlılık olamaz.

---

## 6. HTTP / Host katmanı (`tests/Host.Tests/OpportunityEndpointsTests.cs`)

Faz 2'nin ilk API-seviyesi entegrasyon test projesi — gerçek `WebApplicationFactory<Program>` + Testcontainers Postgres üzerinden HTTP üzerinden çalışır (Task 22).

- `Creating_an_opportunity_without_a_bearer_token_is_unauthorized` — `Authorization` header'ı olmadan `POST /opportunities` → `401 Unauthorized`.
- `Creating_an_opportunity_for_a_tenant_the_caller_does_not_belong_to_is_forbidden` — geçerli bir JWT ama üyesi olunmayan bir tenant için → `403 Forbidden`.

*Bilinçli olarak sadece 2 test: tam "authorized happy path" testi (Account/ExternalIdentity/TenantMembership/RoleAssignment/Role/PermissionSet seed + MasterData Party gerektirir) kod içinde bir yorum olarak bir sonraki adım için bırakıldı — bu iki test zaten asıl iddiayı (authentication/tenant-membership kapısının gerçek HTTP üzerinden uçtan uca çalıştığını) kanıtlıyor.*

Bu proje kurulurken iki gerçek bug bulunup düzeltildi (Task 22 follow-up, `8ec8fa6`):
1. **Migration sıralaması**: `WebApplicationFactory.Services`'e ilk erişim `Program.cs`'in `Main`'ini (action-catalog seed dahil) tetikliyordu; migration'lar artık factory hiç oluşturulmadan önce bağımsız `DbContext` örnekleriyle uygulanıyor.
2. **Connection string önceliği**: `appsettings.Development.json`'daki literal değerler env var'ları `??` ile eziyordu; çözüm `ConnectionStrings__*` (nested config key) env var'larını set etmek.

---

## Özet — dosya/test sayıları

| Katman | Dosya sayısı | Test (Fact/Theory) sayısı |
|---|---|---|
| Domain | 5 | 42 |
| Application (komut) | 6 | 15 |
| Application (sorgu) | 4 | 9 |
| Integration (komut — WinOpportunity) | 1 | 6 |
| Integration (kalıcılık/eşzamanlılık/constraint/RLS/migration) | 8 | 27 |
| Integration (outbox dispatcher) | 1 | 1 |
| Architecture | 1 | 3 |
| Host.Tests (HTTP) | 1 | 2 |
| **Toplam (bu dosyada detaylandırılan)** | **27** | **105** |

*(CRM.Tests projesinin toplamı 121 — aradaki fark, Phase 1/1.5'ten kalan ve Phase 2'de dokunulmayan diğer test dosyalarından gelir.)*
