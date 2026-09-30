# AI Business OS — Mimari Uzlaştırma Raporu

**Durum:** ÖNERİ — bağlayıcı değil. Hiçbir kilitli kararı değiştirmez; değiştirilmesi gerekenleri §A2'de owner kararı olarak listeler.
**Tarih:** 2026-09-30
**Revizyon:** r2 — GPT'nin r1 değerlendirmesinden sonra §O eklendi (anayasa ilkeleri, Solution Package, Semantic Catalog kapsamı).
**Girdi:** GPT brief'i (30 madde), önceki Sonnet/Opus değerlendirmeleri, mevcut kod (2026-09-30 itibarıyla), `AGENTS.md`, `docs/schema/*`, harici karar portföyü `../enterprise ve B2B mimari araştırma/docs/architecture-analysis/` (doc 07, 08, 09, 14, 15).

**Etiketler** (portföyün kendi düzeni): **[V]** kodda/dokümanda doğrulandı (`dosya:satır`), **[R]** öneri, **[doğrulanmadı]** tahmin veya kontrol edilmedi.

---

## A. Mimari hüküm

**Hüküm: KISMEN UYUMLU.** Kod tarafında yeniden yazım gerektiren bir engel yok; engeller dört kilitli karar ve iki somut kod kısıtı.

Uyumlu olan taraf beklenenden güçlü: kilitli hedef mimari (doc 08) GPT'nin istediği modüllerin çoğunu zaten adıyla öngörüyor:

| GPT'nin istediği | Doc 08'deki karşılığı [V] |
|---|---|
| Semantic Metadata, MetricDefinition | **Semantic Catalog** — "entity/capability/metric definitions" (08:43) |
| ConfigChangeSet, impact analysis | **Business Control** — "Blueprint and change sets as SoR; Process Graph, WHY and impact as projections" (08:66) |
| AI Gateway, budget, tool allowlist | **AI Governance / Tool Gateway** — "No direct SQL, inferred privileges, stale approvals" (08:74) |
| NL → güvenli sorgu | **Analytics** — "No unrestricted OLTP joins/AI SQL" (08:62) |
| Workflow | **Workflow / Rules** (08:55) + **Human Tasks** (08:56) |
| Integration Kernel | **Integration** — connectors, credential refs, DLQ/replay (08:54, 07:78) |
| Custom code sandbox | **Extensions** — "no external code in trusted host" (08:75) |

Yani yön portföyle çelişmiyor; portföy bu modülleri "sonra" diye bırakmış, GPT bunları "şimdi ve AI ile" istiyor. Çatışma **ne** inşa edileceğinde değil, **dinamik iş objesi deposu**, **workflow runtime'ının kimde olduğu**, **tenant tanımlı yetki eylemleri** ve **atomiklik** konularında.

İki somut kod kısıtı:
1. `PermissionSetItem.ActionKey` global, kod sahipli `action_registry` tablosuna FK ile bağlı [V `src/Modules/Access/Persistence/Configurations/PermissionSetItemConfiguration.cs:25`]. Registry "Never written by a tenant" [V `src/Modules/Access/Domain/Authorization/ActionRegistryEntry.cs:5`]. Bugün tenant tanımlı bir eyleme yetki verilemez.
2. Olay tüketme altyapısı yok. Tek outbox dispatcher yalnızca CRM outbox'ını okuyor, olayı **loglayıp işlenmiş işaretliyor**, abone yok [V `src/Worker/OutboxDispatcherService.cs:9-13, 49-54`]. Workflow tetikleyicileri bunun üstüne kurulamaz.

### A2. Karar bekleyen owner kararları

Bu rapor bunların hiçbirini çözmez. Her biri açık bir owner onayı ister.

| # | Çatışma | Kilitli kaynak | Seçenekler | Öneri [R] |
|---|---|---|---|---|
| **OD-1** | Tenant tanımlı objeler (`ServiceRequest` gibi) için genel kayıt deposu (`object_records`, JSONB) | doc 07:45 "no universal dynamic business entity store"; doc 15 §4 "not a platform-wide generic entity store"; doc 08:106 "Bad: generic EAV entities for all ERP domains" | (a) Hayır — yalnızca typed aggregate'ler üstünde custom field; (b) Sınırlı evet — ayrı bir modülde, para/stok/vergi/kimlik semantiği taşımayan objeler için; (c) Salesforce tarzı her şey genel | **(b), ama ertelenmiş.** Tetikleyici: bir pilot müşterinin CRM'e sığmayan gerçek bir objeye ihtiyacı olması. JSONB doküman EAV değildir, ama 07:45 EAV'den geniş bir yasak koyuyor; bu yüzden açık karar şart. |
| **OD-2** | Ay 3'teki "küçük workflow motoru" | doc 09:19 "ADOPT, deferred activation… not a home-built general workflow engine"; doc 09:99 tetikleyici: "at least two real processes with restart/versioning requirements… in a spike" | (a) Temporal'ı şimdi benimse; (b) Postgres üstünde dar bir yorumlayıcı, açıkça **spike** olarak, `IWorkflowRuntime` arayüzü arkasında; (c) iki gerçek süreç çıkana kadar bekle | **(b).** Doc 09 "definitions ve capability adapters bizim" diyor; tanım modelini biz yazarız, runtime değiştirilebilir kalır. Karar kaydı değiştirme koşulunu (Temporal'a geçiş tetikleyicisi) yazmalı. |
| **OD-3** | Tenant tanımlı iş eylemleri (`object.service_request.approve`) | `ActionRegistryEntry` "never written by a tenant" (Phase 1.5 dondurulmuş kapsamı); FK kısıtı yukarıda | (a) Registry'ye nullable `tenant_id` ekle — doğal anahtarı bozar, **reddedilir**; (b) Access'e ait ayrı `tenant_business_actions` tablosu + `PermissionSetItem`'da iki nullable referans ve "tam olarak biri" CHECK'i | **(b).** Mevcut registry "sistem eylemleri" sınıfı olarak aynen kalır, invariant bozulmaz. Detay §E. |
| **OD-4** | GPT §7: ConfigChangeSet'in birden çok tanımı **atomik** değiştirmesi | `AGENTS.md` bağlayıcı kural: "No cross-module ACID transactions"; doc 07:98 "each owner activates its part with idempotent status reporting" | (a) Atomiklik yalnızca tek modül içinde (Semantic Catalog); diğer sahipler (Access, Workflow) outbox ile aktive eder ve durum bildirir; (b) tüm tanımları tek modüle topla (workflow ve yetki dahil) — sahiplik kurallarını bozar | **(a).** Bu bir doc kararı değil, bağlayıcı `AGENTS.md` kuralı; GPT'nin "atomically" ifadesi bu haliyle uygulanamaz. §H'de `Published → Activating → Active` modeli. |
| **OD-5** | ConfigChangeSet'in sahibi | doc 08:66 change set'leri **Business Control**'e veriyor; doc 07:9 "control modules govern their own configurations" | (a) Business Control modülü açılır; (b) ilk sürümde Semantic Catalog kendi değişiklik setini yönetir, Business Control sonra projeksiyon olarak gelir | **(b)** tek geliştirici için. Doc 07:9 ile uyumlu; 08:66'dan sapma olarak kaydedilmeli. |
| **OD-6** | Party için custom field | `TenantFieldAggregateType.Party` CRM'de tanımlı [V `src/Modules/CRM/Customization/TenantFieldDefinition.cs:7`], ama Party MasterData'da ve orada `custom_fields` kolonu yok [V `docs/schema/crm-sales-schema.md:129` kolonu yalnızca `opportunities`'de; MasterData'da eşleşme yok] | (a) Party alanlarını MasterData sahiplensin; (b) CRM tarafında Party için ayrı bir overlay tablosu | **(a)** — sahiplik kuralı. Bugünkü enum değeri bir sınır kokusu; kaldırılmalı ya da MasterData'ya taşınmalı. |
| **OD-7** | LLM sağlayıcısına veri gönderimi | doc 09:75 "AI model providers — INTEGRATE later… No model may become policy authority" | Sağlayıcı seçimi, veri yerleşimi, KVKK aktarım dayanağı, hangi alanların modele gidebileceği | Ürün/hukuk kararı. Mimari tarafı: AI Gateway'de alan hassasiyetine göre maskeleme (§F `sensitivity`). |

Ayrıca: doc 15'in başlığı kendini "proposed decision awaiting the same approval gate" olarak tanımlıyor [V 15:3], ama `tenant_field_definitions` şemaya ve koda girmiş. Tier 1'in kabul edilmiş sayıldığını bir satırla teyit etmek gerekiyor.

---

## B. Mevcut mimarinin yeniden kullanım haritası

| Alt sistem | Bugünkü durum [V] | Sınıf | Not |
|---|---|---|---|
| `TenantId`, tenant GUC (`SetTenantContextAsync`), RLS ENABLE+FORCE | Tüm tenant tablolarında; runtime rolü `fynovio_app` [V `src/Host/appsettings.Development.json:9`] | **Doğrudan** | Yeni tüm tablolar aynı kural. |
| Composite tenant-safe FK + CHECK disiplini | `AGENTS.md` bağlayıcı çekirdek #1, #6 | **Doğrudan** | |
| Idempotency kayıtları (modül başına) | Var; süre sonu hiç uygulanmıyor (`AGENTS.md` Status 2026-09-21) | **Doğrudan** | Workflow adımları aynı anahtar deseniyle. |
| Outbox (modül başına) | Satırlar aynı `SaveChanges()` ile yazılıyor; dispatcher yalnız CRM, log + işaretle, tüketici yok [V `OutboxDispatcherService.cs:37-54`] | **Genişlet** | Tüketici defteri, abonelik, DLQ gerekiyor (doc 08 Integration). Worker'ın bağlantı rolü ve tenant GUC'u set etmemesi RLS altında satır görmemesine yol açabilir — **[doğrulanmadı]**: Worker kendi `appsettings`'inde bağlantı dizesi tanımlamıyor, `CrmConnectionString.Resolve()` fallback'ine düşüyor. |
| Evidence (modül başına, append-only) | Var [V `src/Modules/CRM/Evidence/EvidenceRecord.cs`] | **Doğrudan** | ChangeSet onay/yayın, AI önerileri ve agent eylemleri buraya. |
| PDP `AccessAuthorizer` | Default-deny; RBAC + tenant kapsamı + yalnızca `owner` ilişkisi [V `AccessAuthorizer.cs:8-10, 58-71`] | **Genişlet** | İş eylemleri (OD-3), yeni ilişkiler, alan seviyesi. |
| `AccessScope` (None / All / AnyOf(OwnedBy)) | Sorgu filtresi sözleşmesi, bilinmeyen terim = None [V `src/Contracts/AccessScope.cs`] | **Doğrudan / Genişlet** | Semantik sorgu derleyicisi aynı sözleşmeyi uygular. |
| Action registry (`ActionRegistryEntry`) | Global, kod manifestinden seed, tenant yazamaz | **Doğrudan (sistem eylemleri)** | GPT'nin "kilitli sistem eylemleri" sınıfı zaten bu. |
| `ActionKey` biçimi | `^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*){2,}$` [V `src/Contracts/ActionKey.cs:26`] | **Doğrudan** | `object.service_request.approve` biçime uyuyor. |
| `IActionCatalog.IsActiveAsync(ActionKey)` | Tenant parametresi yok [V `src/Contracts/IActionCatalog.cs`] | **Genişlet** | Tenant eylemleri için imza değişmeli → `Contracts` değişikliği. |
| `ModuleCapabilityManifest`, rol/permission-set şablonları, `TenantModuleEnablement` | Reconciler'sız şablon kopyalama | **Desen** | Dikey "çözüm paketleri" (şablon + tanımlar) aynı desenle. |
| Pipeline draft / validate / publish | Aggregate'e özgü; `ValidatePipelineDraftHandler` zaten etki sayısı hesaplıyor [V `ValidatePipelineDraftHandler.cs:22-24`] | **Desen** | ConfigChangeSet'in ve etki analizinin çekirdek fikri burada. |
| `Opportunity` state machine | Güçlü tipli, kapsamlı testli | **Doğrudan (typed kalır)** | |
| `CrmSettings` | Typed; yorumunda "workflow, custom fields ve authorization burada yaşamaz" [V `CrmSettings.cs:6-7`] | **Doğrudan** | Mevcut kod zaten platform yeteneklerini ayrı bir yere bekliyor. |
| `OpportunityCreationSteps` | Tier-2 akış tanımı, JSON dizisi [V `CrmSettings.cs:13`] | **Desen** | `FormDefinition`'ın atası. |
| `TenantFieldDefinition` + `opportunities.custom_fields` jsonb | Tanımlı ama hiçbir handler okuyup yazmıyor; 4 tip | **Genişlet → Semantic Catalog'a taşı** | §F, §N. |
| `OpportunityType`, `LostReason`, `CustomerNeed` katalogları | Veri tabanlı katalog, `ConfigurationStatus` | **Doğrudan** | |
| `EntityRef` + `ILinkTargetResolver` / `ILinkTargetDirectory` | Okuyanın yetkisiyle hidrasyon, saklanmaz [V `AGENTS.md` Status 2026-09-21] | **Doğrudan** | Dinamik objelerden typed aggregate'lere ilişki (`RelationDefinition`) için hazır mekanizma. |
| `IPartyDirectory` tarzı `Contracts` arayüzleri | Modüller arası okuma deseni | **Desen** | CRM'in tanımları Semantic Catalog'dan okuması için `ISemanticDefinitionReader`. |
| Frontend: `DataTable` (TanStack), recharts, kanban/timeline/form demoları, `lib/actions` | Action tipi "AI-Ready UI Metadata" sözleşmesiyle yazılmış [V `web/src/lib/actions/types.ts:6-11`] | **Genişlet** | Bileşen kataloğu var; isimle çözülen bir `ComponentRegistry` ve spec renderer yok. Frontend `useCapability` hâlâ mock. |
| Worker (`BackgroundService`) | Tek polling döngüsü | **Genişlet** | Workflow zamanlayıcıları buraya. |
| AI Gateway, semantik sorgu derleyicisi, workflow runtime, bağımlılık grafiği, integration runtime, secret store | Yok | **Yeni alt sistem** | |

---

## C. Hedef mantıksal mimari

Modül isimleri doc 08'den alınır; paralel isimler icat edilmez.

```mermaid
flowchart LR
  U[Kullanıcı niyeti] --> AI[AI Governance / Tool Gateway]
  AI -->|yalnızca öneri| CS[Semantic Catalog<br/>ConfigChangeSet]
  CS --> V[Doğrulama + Bağımlılık + Etki + Simülasyon]
  V --> AP[Onay]
  AP --> PUB[Yayın: tek modül içi atomik]
  PUB -->|outbox| ACC[Access: iş eylemleri projeksiyonu]
  PUB -->|outbox| WF[Workflow/Rules: tanım aktivasyonu]
  PUB -->|outbox| CRM[CRM ve diğer sahipler: alan doğrulama]
  WF -->|komut, PDP kontrolü| CRM
  AN[Analytics: semantik sorgu] -->|modül sorgu adaptörü| CRM
  AN --> UI[Spec renderer + ComponentRegistry]
```

| Mantıksal katman | Modül (doc 08) | Sahip olduğu | Sahip olmadığı |
|---|---|---|---|
| Kimlik / Tenancy | TenantLifecycle, Identity+Access | Tenant, üyelik, oturum | — |
| PDP / PEP | Access (PDP); her modülün handler'ı (PEP) | Rol, permission set, sistem + iş eylemi kayıtları, `TenantAccessRevision` | Domain tabloları |
| Semantik metadata | **Semantic Catalog** (yeni) | ObjectType, FieldDefinition, RelationDefinition, ActionDefinition, MetricDefinition, ViewDefinition, DependencyEdge (projeksiyon), ConfigChangeSet (OD-5) | Değerler (değerler sahip modülde) |
| Dinamik objeler | **OD-1'e bağlı** — ayrı bir iş modülü | `object_records` | Para/stok/vergi semantiği |
| Metrik / Semantik sorgu | Analytics | Sorgu planı, limitler, önbellek | Domain SQL'i (sahip modül derler) |
| Dinamik görünüm | Web (Experience plane) | ComponentRegistry, renderer | Tanım (Semantic Catalog'da) |
| Workflow | Workflow/Rules | Tanım aktivasyonu, çalıştırmalar, zamanlayıcılar | Domain invariant'ları |
| Onay | Pilotta sahip modül; sonra Human Tasks (doc 08:77) | Onay görevi | İş sonucu |
| Entegrasyon | Integration | Connector tanımları, credential **referansları**, teslim defteri | İş dönüşüm kuralları |
| AI Gateway | AI Governance / Tool Gateway | Model/prompt/araç sürümleri, bütçe defteri, trace | Nihai yetki kararı |
| Politika / Bütçe | Entitlements/Metering (ticari), AI Governance (model bütçesi) | Kota, kullanım | Kullanıcı yetkisi |
| Denetim | Evidence (modül başına niyet kayıtları) | Kanıt | — |

Temel kurallar [R]:
- **Tanım Semantic Catalog'da, değer sahibinde.** CRM `custom_fields` değerlerini tutar, tanımları `ISemanticDefinitionReader` (Contracts) ile okur. Bu, doc 15'in "tier 1 CRM'in kendi şemasında yaşar" kararıyla ve GPT §5'in "tek FieldDefinition" isteğiyle aynı anda uyumlu.
- **AI hiçbir modüle doğrudan yazmaz.** Tek yazma yolu ChangeSet (konfigürasyon) veya katalogdaki komutlar (veri), ikisi de PDP'den geçer.

---

## D. Güçlü tipli ve dinamik sınır

**Kural [R]:** Bir kavram para, stok, vergi, kimlik, yetki ya da çok satırlı invariant taşıyorsa **typed** kalır. Yalnızca şekil, etiket, görünüm ve iş akışı tercihi taşıyorsa **metadata** olur.

| Typed kalır | Neden |
|---|---|
| `Opportunity` yaşam döngüsü (Draft/Open/Won/Lost), `OpportunityLine` tutarları | State machine + para yuvarlama kuralı (`AGENTS.md` "one declared money-rounding rule per aggregate") |
| `Party` kimliği, merge/survivorship | MasterData invariant'ı |
| İleride Quote, Order, Invoice, Payment, Stock, GL | Para/stok/vergi |
| Rol, grant, sistem eylemleri, `TenantAccessState` | Güvenlik |
| Tenant yaşam döngüsü, Evidence, Idempotency, Outbox zarfı | Platform garantileri |
| Pipeline iskeleti (Won/Lost sistem aşamaları) | State machine ile bağlı [V `PipelineDefinitionVersion.AddWonStage`] |

| Metadata olur | Not |
|---|---|
| Typed aggregate'lerde custom field | Değer sahip modülün jsonb kolonunda |
| Tenant objeleri | OD-1'e bağlı; para ve stok alanı taşıyamaz |
| İlişkiler | Typed hedeflere `EntityRef` ile |
| İş eylemleri (`object.*`) | Sistem eylemlerinden ayrı sınıf |
| Metrik ve sorgu tanımları | |
| Form, tablo, kanban, dashboard görünümleri | |
| Workflow tanımları | Runtime OD-2 |
| Entegrasyon eşlemeleri | |
| Etiketler, katalog değerleri, pipeline aşamaları | Zaten veri |

Tenant alan tiplerinde **Money yok** (ilk sürümde): para semantiği bir yuvarlama noktası ister ve bu typed aggregate'in işi. `Decimal` var, "para" değil.

---

## E. Dinamik yetkilendirme tasarımı

### Mevcut durum [V]
- PDP sırası: eylem registry'de aktif mi → principal tanınıyor mu → aktif rol atamaları → permission set kalemleri → `Relation is null` ise tenant kapsamı, `owner` ise kaynak sahibi eşleşmesi [V `AccessAuthorizer.cs:43-71`].
- `PermissionSetItem.Relation` yalnızca `null` veya `"owner"`; yeni ilişki "evaluator var olduğunda yeni bir CHECK" olarak eklenir [V `PermissionSetItem.cs:6-9, 36-37`].
- `ResourceDescriptor` yalnızca `ResourceType`, `Id`, `OwnerPrincipal` taşır [V `src/Contracts/ResourceDescriptor.cs`].
- Alan seviyesi yetki: yok.

### İki sınıf [R]

| | Sistem eylemleri | İş eylemleri |
|---|---|---|
| Örnek | `crm.opportunity.win`, `access.role_assignment.grant`, `crm.settings.update` | `object.service_request.approve`, `object.lead.assign` |
| Kaynak | Kod manifesti → `access.action_registry` (bugünkü tablo) | Semantic Catalog `ActionDefinition` → Access projeksiyonu `access.tenant_business_actions` |
| Kim yazar | Yalnızca deploy | Onaylanmış ChangeSet'in aktivasyonu; Access **kendi** komutuyla yazar |
| Ad alanı | `object.` dışındaki her şey | Yalnızca `object.<object_key>.<verb>` |
| AI önerebilir mi | Hayır | Evet, ChangeSet ile |
| Kapsam | Global | `tenant_id` |

`"Never written by a tenant"` invariant'ı korunur: tenant hiçbir zaman `action_registry`'ye yazmaz.

### Veri modeli değişikliği (OD-3 onaylanırsa)
- `access.tenant_business_actions (tenant_id, action_key, object_type_key, risk_class, status, source_definition_version)`, PK `(tenant_id, action_key)`, CHECK `action_key LIKE 'object.%'`.
- `access.permission_set_items`: `system_action_key` (FK → `action_registry`) ve `business_action_key` (composite FK → `tenant_business_actions (tenant_id, action_key)`); CHECK "tam olarak biri dolu".
- `IActionCatalog.IsActiveAsync(TenantId, ActionKey)` — `Contracts` imza değişikliği; PDP önce ad alanına göre hangi tabloya bakacağını seçer.
- Bir iş eylemi deprecate edilince grant'lar geçersiz olur (PDP `action_not_registered` ile reddeder); `TenantAccessRevision` artar.

### Beş yetki boyutu

| Boyut | Mekanizma [R] |
|---|---|
| Obje tipi | `object.<type>.read` / `.create` / `.update` standart eylemleri, ObjectType yayınında otomatik üretilir |
| Eylem | Özel iş eylemleri (`approve`, `close`) aynı tabloda |
| Kayıt | Mevcut `Relation` + `AccessScope`; `object_records.owner_principal` typed kolon → `OwnedBy` doğrudan çalışır. Takım/bölge ilişkileri, sağlayıcıları yazıldığında (mevcut kural) |
| Alan | Alan başına eylem **üretilmez** (anahtar patlaması). `FieldDefinition.sensitivity ∈ {Standard, Restricted}`; Restricted alanları okumak/yazmak için obje başına tek eylem: `object.<type>.restricted_fields.read` / `.write`. PEP okuma projeksiyonunda alanı düşürür, yazmada reddeder. Typed aggregate'ler için de aynı: `crm.opportunity.restricted_fields.read` (sistem eylemi) |
| Tenant bağlamı | Değişmez: GUC + RLS + her sorguda `TenantId` |

**Agent'lar:** agent, çağıran kullanıcının adına ve **en fazla onun yetkisiyle** çalışır (doc 08:74 "no inferred privileges"). Otonom (kullanıcısız) workflow eylemleri için ayrı bir servis principal'ı, açıkça atanmış bir rolle; `high` risk sınıfı eylemler bu role varsayılan olarak verilmez.

---

## F. Metadata modeli (ilk sürüm)

Ortak kolonlar (tüm tanım tablolarında): `tenant_id`, `id`, `key` (değişmez), `label`, `status` (`Draft | Published | Deprecated | Archived`), `version`, `owner_scope` (`Tenant | Network`, doc 15 §6), `owner_ref`, `is_locked` (doc 15 §6 alan/adım bazlı kilit), `change_set_id`, `row_version`, `created_at`, `updated_at`.

**Anahtar kuralı** (`ActionKey` ile aynı felsefe [V `ActionKey.cs:5-7`]): `key` yayından sonra değişmez; yeniden adlandırma `label` değişikliğidir. Anahtar değiştirmek deprecate + yeni tanım + veri taşımadır.

| Tablo | Ana kolonlar | İlk sürüm kapsamı |
|---|---|---|
| `object_types` | `kind` (`System | Tenant`), `backing` (`crm.opportunity` gibi typed aggregate ya da `record`), `title_field_key` | Yalnızca `System` (Opportunity). `Tenant` OD-1'den sonra |
| `field_definitions` | `object_type_id`, `value_type` (`Text, LongText, Number, Decimal, Boolean, Date, DateTime, Select, MultiSelect, Email, Phone, Url`; sonra `Reference`), `config jsonb` (seçenekler kararlı `option_key` ile, min/max, regex, hassasiyet), `is_required`, `sensitivity`, `sort_order` | Tamamı |
| `relation_definitions` | `from_object_type_id`, hedef (`EntityRef` bağlamı+tipi veya tenant obje tipi), `cardinality` (yalnızca `ManyToOne`), `on_target_unavailable` | Sonra |
| `action_definitions` | `object_type_id`, `action_key`, `kind` (`Create, Update, Transition, Custom`), `risk_class`, `reversibility` (`Reversible, Compensatable, Irreversible`), `compensation_action_key`, `idempotent`, `approval_policy` | OD-3'ten sonra |
| `metric_definitions` | `object_type_id`, `aggregation` (`Count, Sum, Avg, Min, Max, CountDistinct`), `measure_field_key`, `filter` (ifade ağacı), `time_field_key`; türetilmiş metrik: `formula` = metrik anahtarları üstünde oran/fark | Analitik diliminde |
| `view_definitions` | `kind` (`Table, Form, Kanban, Detail, Dashboard`), `object_type_id`, `spec jsonb`, `spec_version`; kişisel/paylaşılan | İlk dilimde yalnızca Form ve Table |
| `dependency_edges` | `from_kind, from_id, from_version`, `to_kind, to_id`, `edge_kind` (`ReadsField, FiltersOn, WritesField, MapsField, GrantsAction, Renders`) | **Projeksiyon**: yayında tanım spec'lerinden hesaplanır, elle yazılmaz, yeniden üretilebilir |
| `config_change_sets` | `source` (`Human, Ai, Template`), `proposer`, `ai_trace_ref`, `status`, `base_revision`, `content_hash` | §H |
| `config_change_items` | `op` (`Create, Update, Deprecate, Archive`), `definition_kind`, `definition_key`, `before jsonb`, `after jsonb` | §H |
| `tenant_config_state` | `revision` | `TenantAccessState` deseni [V] |

**Tek ifade modeli [R]:** metrik filtreleri, alan doğrulama kuralları ve workflow koşulları aynı JSON ifade ağacını kullanır (`and/or/not`, karşılaştırma, `in`, `is_null`, tarih aritmetiği; döngü yok, fonksiyon çağrısı yok). Neden kendi ağacımız: FieldDefinition'a karşı **tip denetimi** ve **bağımlılık çıkarımı** (hangi alanlara dokunuyor) yapabilmek; aynı ağaç hem SQL'e hem bellek içi değerlendiriciye derlenebilir. JsonLogic'in .NET kütüphaneleri var ama tipsiz; olgun bir .NET CEL uygulaması **[doğrulanmadı]**.

**Kasıtlı olarak modellenmeyenler:** WorkflowDefinition, IntegrationDefinition, QueryDefinition. Projenin kendi kuralına uyulur: enum değeri veya tablo, değerlendiricisi var olduğunda eklenir, önceden boş rezerve edilmez [V `PermissionSetItem.cs:6-9`, `ResourceDescriptor.cs` yorumu].

---

## G. Şema evrimi stratejisi

| Değişiklik | Sınıf | Mevcut veri | Gereken |
|---|---|---|---|
| Yeni opsiyonel alan | Eklemeli | Dokunulmaz (eksik = null) | Doğrulama |
| Opsiyoneli zorunlu yapma | Daraltıcı | İhlal eden kayıt sayısı | Varsayılan değer veya backfill işi + onay |
| Etiket değişikliği | Güvenli | — | — |
| Anahtar değişikliği | **Yasak** | — | Deprecate + yeni alan + taşıma işi |
| Tip genişletme (`Text→LongText`, `Number→Decimal`, seçenek ekleme) | Eklemeli | Dokunulmaz | Doğrulama |
| Tip daraltma / değiştirme | Yıkıcı | Dönüştürülemeyen değer raporu | Taşıma planı + kuru çalıştırma sayıları + açık onay |
| Seçenek kaldırma | Yıkıcı | Değerler okunabilir kalır | Deprecate + isteğe bağlı eşleme (`eski → yeni`) |
| Alan silme | Yıkıcı | Değerler jsonb'de kalır | `Deprecated` (yeni yazma yok, okuma var, bağımlılar işaretli) → bağımlı sayısı 0 olunca `Archived` |

İlkeler [R]:
- **Expand/contract**, `AGENTS.md` migration kuralının metadata karşılığı.
- Yazma, yazıldığı andaki yayınlanmış tanım sürümüne göre doğrulanır; okuma toleranslıdır (bilinmeyen anahtar yok sayılır, eksik = null).
- Veri taşıma, izlenen bir **ConfigMigrationJob**: parti parti, idempotent, kayıt başına `row_version` artışı + outbox + evidence. Çalıştırma sırasında ChangeSet `Activating` durumunda kalır.
- jsonb anahtarı `field key`'dir (değişmez olduğu için güvenli, SQL'de ve indekste okunur).
- Sık sorgulanan alan: ölçüldüğünde expression index veya generated column; varsayılan değil.
- "No soft delete" kuralı satırlar içindir; tanımın `Deprecated` durumu bir domain durumudur, kuralla çelişmez.

---

## H. ConfigChangeSet yaşam döngüsü

```
Draft → Validated → (Simulated) → AwaitingApproval → Approved → Published → Activating → Active
  ↘ Discarded                         ↘ Rejected                         ↘ ActivationFailed
  ↘ Superseded (çakışma)
```

| Aşama | Ne olur [R] |
|---|---|
| Draft | Kalemler eklenir; kaynak `Human`, `Ai` veya `Template`. AI önerisi her zaman buradan başlar |
| Validate | Şema, referans bütünlüğü, anahtar/ad alanı kuralları (`object.` dışı eylem reddedilir), ifade tip denetimi, önerenin yetkisi |
| Impact | Bağımlılık kenarları + etkilenen kayıt sayıları (`ValidatePipelineDraftHandler`'daki etki sayısının genellemesi) |
| Simulate | Alan: ihlal eden kayıt sayısı. Metrik: önizleme sorgusu. Workflow: son N olay üzerinde kuru çalıştırma → "şu eylemler tetiklenirdi" listesi. İlk sürümde opsiyonel |
| Approve | Onay `content_hash`'e bağlıdır; içerik değişirse onay düşer (bayat onay yok, doc 08:74). AI kaynaklı set insan onayı olmadan yayınlanamaz. SoD şu an NOT APPLICABLE (`AGENTS.md`), tek onaylayıcı |
| Publish | Semantic Catalog içinde **tek transaction**: tanım sürümleri + `tenant_config_state.revision++` + outbox `SemanticDefinitionsPublished` + evidence |
| Activate | Diğer sahipler (Access, Workflow, CRM önbelleği) olayı tüketir, idempotent aktive eder, durum bildirir (doc 07:98). Hepsi bildirince `Active` |

**Atomiklik:** yalnızca Semantic Catalog içinde (OD-4). Modüller arası bütünlük "yayınlandı ama henüz her yerde aktif değil" ara durumunu açıkça gösterir; bu durumda yeni eylem veya workflow **kullanılamaz** (PDP registry'de görmez → reddeder, güvenli taraf).

**Çakışma ve eşzamanlılık:** set `base_revision` taşır. Yayında revizyon farklıysa kalemler yeniden karşılaştırılır; aynı tanıma başka bir set dokunduysa set `Superseded` olur ve yeniden önerilir. Tenant başına yayın, `tenant_config_state` satırında kilitle sıraya alınır.

**Geri alma:** yalnızca **ileri yönlü**. "Geri al", ters kalemlerden üretilen yeni bir ChangeSet'tir. Yıkıcı veri taşıması yapılmışsa ters set üretilemez ve bu, etki analizinde onaydan önce "geri alınamaz" olarak gösterilir. Yan etkiler (gönderilmiş e-posta vb.) konfigürasyon geri almasıyla geri gelmez, bkz. `ActionDefinition.reversibility`.

---

## I. Raporlama mimarisi

```
NL → [AI: yalnızca yapılandırılmış çıktı] SemanticQuery JSON
   → Doğrulama (anahtarlar, yetki, limitler)
   → Plan → Sahip modülün sorgu adaptörü → parametreli SQL
   → Salt okunur çalıştırma → Sonuç
   → ViewDefinition spec → ComponentRegistry → recharts / DataTable
```

- **SemanticQuery** = `{ metrics[], dimensions[] (alan anahtarı + zaman tanesi), filter (ifade ağacı), timeRange, compareTo?, limit }`. LLM SQL üretmez; bu JSON'u bir araç şeması ile üretir.
- **Doğrulama:** tüm anahtarlar yayınlanmış olmalı; kullanıcının obje `read` yetkisi ve Restricted alanlar için ayrı yetkisi; limitler (en fazla 3 boyut, satır sınırı, zaman aralığı sınırı).
- **Sınır kararı [R]:** Analytics, CRM tablolarını doğrudan okuyamaz (doc 08:30 "no foreign table access"). İki yol: (a) her sahip modül `Contracts`'taki `IMetricQuerySource`'u uygular ve kendi SQL'ini kendi derler; (b) outbox olaylarıyla beslenen analitik projeksiyon tabloları. **(a) önce**, çünkü (b) çalışan bir olay tüketme altyapısı istiyor ve o yok [V §A].
- **Çalıştırma güvenliği:** runtime rolü + tenant GUC + RLS, `SET TRANSACTION READ ONLY`, `statement_timeout`, satır sınırı, `AccessScope` filtresi SQL'e çevrilir (post-filter yok, [V `AccessScope.cs` yorumu]).
- **Görselleştirme:** grafik türünü kurallar seçer (zaman boyutu → çizgi, kategori → çubuk, tek metrik → kart); AI önerebilir, kural doğrular.
- **Kaydetme:** geçici görünüm kaydedilmez. Kişisel kaydedilmiş görünüm ChangeSet gerektirmez (düşük risk). Paylaşılan dashboard ChangeSet ile yayınlanır.

---

## J. Minimal workflow motoru (OD-2 onayına bağlı)

**Ön koşul:** çalışan olay tüketme altyapısı (abonelik + tüketici defteri + tenant GUC ile okuma). Bugün yok [V].

| Öğe | İlk sürüm |
|---|---|
| Trigger | Outbox olay tipi (ör. `enterprise.crmsales.opportunity.stage_changed.v1`) + ifade filtresi |
| Condition | Ortak ifade ağacı |
| Action | Yalnızca katalogdaki eylemler (`ActionDefinition` veya sistem eylemi) + giriş eşlemesi; her çalıştırmada PDP kontrolü |
| Wait | Süre veya "şu olay gelene kadar, en fazla X" |
| Approval | Pilotta sahip modülün onay görevi; zaman aşımı dalı |
| Akış | Doğrusal adımlar + tek seviye if/else; döngü yok, paralel yok, iç içe workflow yok |
| Limitler | En fazla 20 adım, en fazla 30 gün çalışma, tanım başına hız sınırı |

**Runtime (Postgres, Worker içinde):** `workflow_runs` (tanım sürümüne sabitlenmiş, `trigger_event_id` benzersiz → idempotent başlatma), `workflow_timers (due_at)`, `workflow_step_executions` (idempotency anahtarı = `run_id + step_index`). `FOR UPDATE SKIP LOCKED` ile polling. `IWorkflowRuntime` arayüzü arkasında; doc 09:99 tetikleyicisi gerçekleşince Temporal ile değiştirilebilir.

**Döngü koruması:** workflow'un tetiklediği olaylar `causation_id` taşır; nedensellik zinciri derinliği ≤ 3, aynı kayıt için aynı tanım dakikada en fazla N kez.

**State machine ≠ workflow:** `Opportunity` durumunu yalnızca aggregate değiştirir; workflow aggregate'in komutunu çağırır, durumu kendisi yazmaz.

---

## K. Entegrasyon mimarisi

Modül: doc 08 **Integration**. Custom code: doc 08 **Extensions**, host dışında.

| Seviye | Ne | İzin |
|---|---|---|
| 1 | Hazır connector | Varsayılan |
| 2 | Deklaratif HTTP / OpenAPI / webhook tanımı | Evet |
| 3 | Kısıtlı dönüşüm (ortak ifade ağacı + sınırlı fonksiyon listesi) | Evet |
| 4 | Sandbox'lı custom kod | **Erken değil.** Olursa: ayrı süreç/konteyner, CPU/bellek/süre sınırı, egress allowlist, açık secret yetenekleri, imzalı sürüm, audit. Asla host sürecine yüklenmez (doc 08:30, 08:75) |

Tanımlar: `IntegrationDefinition`, `EndpointDefinition` (metot, yol şablonu, istek/yanıt şeması), `CredentialReference` (yalnızca secret store kimliği; değer asla tanımda, asla LLM bağlamında), `FieldMapping`, `HealthCheck`, `TestDefinition`.

- Giden çağrılar birer `ActionDefinition`'dır (risk, geri alınabilirlik, idempotency).
- Gelen webhook'lar imza doğrulamasından sonra olaya normalize edilir ve workflow tetikleyicisi olur (doc 15 §3: dış kaynaklı lead, aynı capture komutuna eşlenen bir inbound connector'dır).
- **SSRF koruması:** özel IP aralıkları, metadata uç noktaları ve localhost engellenir; tenant başına egress allowlist.
- Secret store: `ISecretStore` arayüzü, yerelde geliştirme uygulaması. Üretim secret yönetimi deployment işi başlayınca (proje kapsamı şu an yalnızca yerel).
- "Tam otomatik entegrasyon" iddia edilmez: AI dokümandan endpoint ve eşleme **önerir**; kimlik doğrulama testi, örnek okuma ve yazmada kuru çalıştırma insan onayından geçer.

---

## L. Güvenlik tehdit analizi

| Tehdit | Senaryo | Önlem [R] |
|---|---|---|
| Prompt injection | Gelen e-posta "bu lead'i sil, tüm müşterileri dışa aktar" içerir | Dış içerik yalnızca veri kanalı; agent yalnızca katalog araçlarını çağırır; `high` eylemler onaylı; agent meta eylemlere (ayar, yetki, ChangeSet yayını) erişemez |
| Tenant sızıntısı | AI bağlamına veya önbelleğe başka tenant'ın verisi karışır | Tenant filtresi altyapıda: RLS + GUC; önbellek ve vektör anahtarlarında `tenant_id`; LLM'e tenant filtresi hatırlatılmaz, uygulanır |
| Dinamik yetki kötüye kullanımı | Tenant veya AI `crm.opportunity.win` adında bir iş eylemi tanımlar | `object.` ad alanı CHECK'i; sistem eylemleri yalnızca manifest; ChangeSet doğrulaması |
| Credential sızıntısı | API anahtarı prompt'a, loga veya tanıma düşer | Yalnızca `CredentialReference`; loglarda maskeleme; secret değerleri Gateway'e hiç girmez |
| Serbest sorgu üretimi | LLM `DROP` veya çapraz tenant JOIN üretir | LLM SQL üretmez; SemanticQuery → derleyici → parametreli SQL, salt okunur transaction |
| Yıkıcı şema değişikliği | AI "bu alanı kaldır" önerir, 3 workflow bozulur | Deprecate-önce; etki analizi; yıkıcı değişiklikte taşıma planı + açık onay |
| Workflow döngüsü | A olayı B'yi, B A'yı tetikler | Nedensellik derinliği, hız sınırı, adım limiti |
| Tekrar / retry yan etkisi | Worker çöker, e-posta iki kez gider | Adım idempotency anahtarı; dış çağrılarda idempotency başlığı; `Irreversible` eylemler "en fazla bir kez" |
| AI maliyet patlaması | Döngüye giren agent binlerce çağrı yapar | Çalıştırma zarfı (adım, süre, maliyet, araç çağrısı); tenant bütçesi; aşımda durdur |
| SSRF | Tenant entegrasyon URL'si iç ağa işaret eder | Egress allowlist, özel IP engeli |
| Bayat onay | Onaydan sonra set değiştirilir | Onay `content_hash`'e bağlı |
| Onay yorgunluğu | Her şeye onay istenir, kullanıcı körü körüne onaylar | Risk bazlı otonomi basamakları (OBSERVE → SHADOW → ASSIST → AUTONOMOUS), düşük riskte gruplu onay |
| Kişisel veri LLM'e | Restricted alanlar sağlayıcıya gider | `sensitivity` maskeleme, OD-7 |

---

## M. Göç planı (yeniden yazım yok)

**Kapasite notu:** doc 14 başlangıç planını **üç mühendis** varsayımıyla yazmış [V 14:73]; burada tek geliştirici var. GPT'nin 3 aylık planı tek kişi için kabaca 6 ay eder **[tahmin]**. Aşağıdaki süreler de tahmindir.

| Aşama | İçerik | Kapı |
|---|---|---|
| 0 | OD-1…OD-7 kararları; doc 15 §7'nin istediği tier-1 veri şekli ADR'ı | Owner onayı |
| 1 | **İlk dilim (§N)**: custom field'lar uçtan uca, CRM içinde | Testler + gerçek kullanım |
| 2 | Semantic Catalog modülü; FieldDefinition'ın CRM'den taşınması; `ISemanticDefinitionReader`; ChangeSet v1 (yalnızca alan ve görünüm) | OD-4, OD-5 |
| 3 | AI Gateway minimal + AI kurulum asistanı (yalnızca ChangeSet taslağı üretir) | OD-7 |
| 4 | Semantik metrikler: `IMetricQuerySource` CRM uygulaması + NL → SemanticQuery | — |
| 5 | Olay tüketme altyapısı → workflow spike'ı | OD-2 |
| 6 | İş eylemleri, tenant objeleri | OD-3, OD-1 + gerçek pilot ihtiyacı |

Her aşama tek başına değerli ve geri alınabilir; bir sonraki başlamazsa önceki boşa gitmez.

---

## N. İlk uygulama dilimi

**Hedef:** en az kapsamla en çok mimari öğrenme; gösterişli AI demosu değil. **Tamamı zaten onaylanmış kararların içinde** (doc 15 tier 1 ve tier 2); hiçbir kilitli kararı yeniden açmaz, yalnızca doc 15 §7'nin istediği kısa ADR'ı gerektirir.

**Kapsam:**
1. **ADR:** tier-1 veri şekli — jsonb, `field key` ile anahtarlı; tanımlar şimdilik CRM'de, aşama 2'de Semantic Catalog'a taşınacak; `owner_scope` kolonu yalnızca `Tenant` değerine izin veren CHECK ile (doc 15 §6 her kaydın bunu taşımasını istiyor; `Network` değeri Organization modülü gelince).
2. `TenantFieldDefinition` genişletme: değişmez `key`, `label`, `Select`/`MultiSelect` + seçenekler, doğrulama `config`, `status` (`Active | Deprecated`), `sort_order`, `is_locked`, `row_version`. Migration eklemeli.
3. **Değer yazma yolu:** `CreateOpportunity` `custom_fields`'ı tanımlara göre doğrular. Bugün genel bir güncelleme komutu yok [V `src/Modules/CRM/Application` listesinde `Update*Opportunity*` yok], bu yüzden yeni `UpdateOpportunityCustomFields` komutu: idempotent, `row_version` kontrollü, outbox + (gerekirse) evidence.
4. **Ayarlar UI:** mevcut `crm-settings` özelliğinde alan yönetimi (ekle, düzenle, deprecate).
5. **Metadata ile render:** yeni fırsat formunda ve detay sayfasında alan bölümü; fırsat listesinde `DataTable` kolonları. Render tamamen tanımdan, AI yok.
6. **İlk etki analizi:** ilk sürümde alanlara tek tek referans veren kayıtlı bir görünüm yok (form ve liste tüm aktif alanları `sort_order` ile render ediyor), bu yüzden henüz bağımlılık kenarı da yok. Deprecate ekranı **veri etkisini** gösterir: bu alanda değeri olan fırsat sayısı. `dependency_edges` ilk kayıtlı `ViewDefinition` ile gelir. (r2 düzeltmesi: r1 burada form/liste kolon bağımlılığı öngörüyordu; ADR yazılırken bunların alan referansı tutmadığı görüldü. Bkz. `adr-tier1-custom-fields.md` karar 12.)
7. **Testler:** RLS (runtime rolü), tenant izolasyonu, tip/zorunluluk/seçenek doğrulaması, idempotency, eşzamanlılık, deprecate edilmiş alana yazma reddi.

**Kapsam dışı:** AI, ChangeSet, Semantic Catalog modülü, Party alanları (OD-6), tenant objeleri, metrikler, workflow.

**Bu dilim neyi doğrular:** alan tanım modeli, jsonb doğrulama ve sorgulama, metadata ile render, bağımlılık çıkarımı. Sonraki her katman (ChangeSet, AI önerisi, metrik, workflow koşulu) bunların üstüne kurulur.

**Geri alınabilirlik:** yalnızca eklemeli migration; tablo zaten var; özellik ayar ekranından kapatılabilir.

**Tahmini süre:** tek geliştirici için 2–4 hafta **[tahmin]**.

**Ölçülebilir kullanıcı değeri:** bir tenant, geliştirici müdahalesi olmadan kendi fırsat alanlarını tanımlayıp formda ve listede kullanabilir. Bugün bu mümkün değil.

**Bağımlılık:** bu dilim OD-1…OD-7'nin hiçbirine bağlı değil. Gereken tek şey doc 15 tier 1'in kabul edildiğinin teyidi (doc 15 başlığı hâlâ "proposed" [V 15:3]) ve tier-1 veri şekli ADR'ı. OD kararları paralel yürüyebilir.

---

## O. Anayasa ilkeleri ve sözlük eklemeleri (r2)

### O1. Temel ilkeler [R — owner onayıyla bağlayıcı olur]

1. **AI önerir, çekirdek yürütür.** AI'ın tek yazma yolu ChangeSet (konfigürasyon) veya katalogdaki, PDP'den geçen komutlardır (veri).
2. **Invariant'ın olduğu yerde typed, değişkenliğin olduğu yerde metadata.** Sınır kuralı §D.
3. **Konfigürasyon değişiklikleri versiyonlu, ileri yönlü ve etki bilinçlidir.** "Geri al" ters bir ChangeSet'tir (§H).
4. **Dinamik UI dinamik metadata demektir, asla çalışma zamanında üretilen kod değil.**
5. **Mimari yatay, ürün somut bir dikey Solution Package'tan başlar.**

Bu ilkeler OD-1…OD-7'yi kapatmaz. Özellikle "tenant'a özgü hafif objeler" (OD-1) ve "kendi dar workflow runtime'ımız" (OD-2) hâlâ kilitli kararlarla (doc 07:45, doc 09:19/99) çelişir ve ayrı owner onayı ister.

### O2. Solution Package (sözlüğe eklenir, tablo açılmaz)

Dikey pazara giriş ile yatay mimari arasındaki köprü. "Boş Business OS, ne istersen söyle" yerine "işletme tipini seç, AI sana göre uyarlar".

- **Ad:** *Solution Package*, "Blueprint" değil. Doc 08'de Blueprint, Business Control'ün sahip olduğu gereksinim ve fit/gap versiyonları anlamında zaten kullanılıyor [V 08:66, 07:74].
- **İçerik:** alan, pipeline, metrik, görünüm, iş eylemi, workflow ve entegrasyon tanımlarından oluşan versiyonlu bir tanım paketi.
- **Uygulama akışı:** Solution Package → tenant'a özel ChangeSet (`source = Template`) → AI uyarlaması (aynı ChangeSet'e kalem ekler) → doğrulama/etki → onay → yayın.
- **Mevcut desen:** `ModuleCapabilityManifest` + `TenantModuleEnablement`, şablonu provenance (`origin_module_key`, `origin_version`) ile tenant'a kopyalıyor [V `AGENTS.md` Status 2026-09-20].
- **Açık problem — yükseltme:** tenant paketi özelleştirdikten sonra paketin v2'si nasıl uygulanır? Mevcut kodda da açık: yetki şablonları için reconciler ve yükseltme yolu yok (`AGENTS.md` "no template upgrade path"). Kısmi cevap doc 15 §6: kilitli alan/adımlar paketin, kilitsizler tenant'ın; yükseltme yalnızca kilitli kısmı ve tenant'ın dokunmadığı kısmı değiştirir, çakışmalar ChangeSet'te gösterilir. Tasarımı, ilk gerçek paket v2'si ortaya çıktığında yapılır.

### O3. Semantic Catalog'un kapsamı

Semantic Catalog **yalnızca bir custom field alt sistemi değildir**; uzun vadede işletmenin operasyonel ontolojisidir: Object, Relation, Event, Action, State, Metric, Policy.

Nüans: Event ve State typed dünyada zaten var (outbox olay tipleri, ör. `enterprise.crmsales.opportunity.stage_changed.v1`; `Opportunity` state machine'i). Katalog bunları **tanımlar ve tarif eder** (salt okunur kayıt: hangi olay var, yükü ne, hangi durumlar var), **sahiplenmez**. Sahiplik, doc 08'deki gibi üreten modülde kalır. Policy'nin sahibi Access'tir; katalog yalnızca referans verir.

Şimdi kodlanmaz. İlk sürüm kapsamı §F'deki gibi kalır.
