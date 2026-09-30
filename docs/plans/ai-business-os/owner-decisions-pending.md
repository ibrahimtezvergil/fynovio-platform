# AI Business OS — Onay Bekleyen Kararlar

**Tarih:** 2026-09-30
**Dal:** `docs/ai-business-os-architecture`
**Kaynaklar:** `2026-09-30-architecture-reconciliation.md` (rapor, r2), `adr-tier1-custom-fields.md` (ADR)

Bu dosya, ilerlemek için owner'ın vermesi gereken kararları tek yerde toplar. Hiçbiri henüz verilmedi. Her maddenin sonunda cevap satırı var; doldurulan madde bir sonraki adımı açar.

İki grup var:
- **Grup 1 — İlk dilim için gerekli (3 karar).** Bunlar verilince custom field işine başlanabilir. Hiçbir kilitli kararı yeniden açmazlar.
- **Grup 2 — Mimari owner kararları (OD-1 … OD-7).** İlk dilimi bloklamaz; sonraki aşamaları belirler. Her biri kilitli bir kaynakla çelişir, bu yüzden açık onay ister.

---

## Grup 1 — İlk dilim (tier-1 custom fields) için onaylar

### K1. Doc 15 tier 1 kabul edildi mi?

**Durum:** Doc 15 (`../enterprise ve B2B mimari araştırma/docs/architecture-analysis/15_TENANT_PROCESS_CUSTOMIZATION.md`) başlığında kendini hâlâ "proposed decision awaiting the same approval gate" olarak tanımlıyor (15:3). Öte yandan önerdiği tier-1 yapısı zaten koda ve şemaya girmiş:
- `crm.tenant_field_definitions` tablosu (RLS açık),
- `crm.opportunities.custom_fields jsonb` kolonu,
- `docs/schema/crm-sales-schema.md` içinde "Tier-1 tenant custom fields (15 §7)" bölümü.

Yani uygulama tier 1'i kabul edilmiş gibi davranıyor, doküman ise etmiyor.

**Tier 1 ne diyor (özet):** tenant, şema migration'ı olmadan tipli ek alanlar tanımlayabilir; alanlar CRM modülünün kendi şemasında yaşar; genel bir EAV/entity deposu değildir; pilotta platform admin, sonra tenant admin yönetir.

**Seçenekler:**
- **(a) Evet, tier 1 kabul edildi.** Tier 2 (akış tanımı) ve §6 (Network kapsamı, alan bazlı kilit) de kabul edilmiş sayılır mı, ayrıca belirt.
- **(b) Hayır / değişiklik var.** Ne değişecekse yaz; ADR buna göre güncellenir.

**Öneri:** (a). Kod zaten bu karara göre yazılmış; teyit yalnızca dokümanı gerçekle hizalar.

**Karar sonrası:** ADR'ın "Precondition" bölümü kapanır.

> **Cevap:** ☐ (a) Kabul — tier 1 ☐ tier 2 ☐ §6 ☐ (b) Değişiklik: ____________

---

### K2. Tier-1 custom field ADR'ı onaylanıyor mu?

**Dosya:** `adr-tier1-custom-fields.md` (durum: Proposed).

**Özetle ne karar veriyor:**

| # | Karar | Neden |
|---|---|---|
| 1 | Değerler aggregate'in `custom_fields jsonb` kolonunda tek JSON obje; tanımlar şimdilik `crm.tenant_field_definitions`'da, aşama 2'de Semantic Catalog'a taşınır | EAV değil; değer depolaması taşımada değişmez |
| 2 | `field_name` değişmez anahtar (`^[a-z][a-z0-9_]{1,62}$`); görünen ad için ayrı `label` | `ActionKey` ile aynı felsefe; yeniden adlandırma veri bozmaz |
| 3 | 11 tip: `text`, `long_text`, `number`, `decimal`, `boolean`, `date`, `select`, `multi_select`, `email`, `phone`, `url`. **Para tipi yok** | Para bir yuvarlama noktası ister; bu typed aggregate'in işi |
| 4 | JSON kodlaması: sayılar JSON number, tarih string, çoklu seçim dizi; bilinmeyen anahtar yazmada reddedilir | Tutarlı doğrulama |
| 5 | Yeni kolonlar: `label`, `config`, `status` (Active/Deprecated), `sort_order`, `owner_scope` (yalnızca `Tenant`), `row_version`, `updated_at`. `is_locked` şimdilik yok | `owner_scope` doc 15 §6 gereği; kilit ancak Network kapsamıyla anlamlı |
| 6 | Limitler: tip başına 100 aktif alan, 50 seçenek, 64 KB | Kötüye kullanım ve performans |
| 7 | Zorunlu alan eski kayıtları geriye dönük geçersiz yapmaz; kaydın bir sonraki alan güncellemesinde istenir. Deprecated alana yazma `422`, eski değer okunur | Salesforce benzeri, veri kaybı yok |
| 8 | `CreateOpportunity`'ye opsiyonel `CustomFields`; yeni `UpdateOpportunityCustomFields` komutu (tam değiştirme, `expectedVersion`, idempotent, outbox'ta yalnızca değişen anahtarlar, değerler değil) | Bugün genel bir güncelleme komutu yok |
| 9 | Tanım yönetimi `crm.settings.update`, tanım okuma `crm.settings.read`, değer yazma yeni `crm.opportunity.update_custom_fields` | Mevcut yetki seti yeniden kullanılır (K3'e bakın) |
| 10 | Party için alan tanımı API'de reddedilir (OD-6'ya kadar) | Party MasterData'da, orada kolon yok |
| 11 | Custom field'lara indeks yok; ölçülünce expression index | Erken optimizasyon yok |
| 12 | Deprecate ekranı veri etkisini gösterir (değeri olan fırsat sayısı); bağımlılık kenarları ilk kayıtlı görünümle gelir | Henüz alana referans veren görünüm yok |

**Seçenekler:**
- **(a) Olduğu gibi onayla.**
- **(b) Değişikliklerle onayla** (hangi madde, ne değişecek).
- **(c) Reddet.**

**Öneri:** (a). Tartışmaya en açık maddeler: 3 (tip listesi; ilk sürümde daha az tiple başlanabilir) ve 6 (limit değerleri).

**Karar sonrası:** ADR "Accepted" olur, adım adım uygulama planı (`docs/plans/ai-business-os/`) yazılır, bu dalda koda başlanır.

> **Cevap:** ☐ (a) ☐ (b) Değişiklikler: ____________ ☐ (c)

---

### K3. Yeni yetki eylemi için CRM şablon versiyonu

**Bağlam:** Alan değeri yazmak için yeni eylem gerekiyor: `crm.opportunity.update_custom_fields`. Bu eylemin "CRM — work opportunities" yetki setine eklenmesi gerekiyor, yani CRM modül şablonunun içeriği değişiyor.

Mevcut durum [V `src/Modules/CRM/Application/CrmModuleCapabilities.cs`]:
- Şablon şu an **v2**.
- Kural: "içerik değişirse versiyon artırılır".
- Şablon tenant'a **bir kez kopyalanır**, reconciler yok. Yani versiyon artırılırsa daha önce etkinleştirilmiş tenant'lar yeni eylemi **almaz**.
- Bir kez istisna yapılmış: `crm.reference.party.create`, "henüz production yok" gerekçesiyle v1'e yerinde eklenmiş. Aynı yorumda "ilk production tenant'tan itibaren içerik değişikliği versiyonu artırmak zorunda" yazıyor.

**Seçenekler:**

| | (a) v3'e yükselt + dev tenant'ları yeniden seed et | (b) v2'ye yerinde ekle |
|---|---|---|
| Kuralla uyum | Tam uyumlu | İkinci istisna olur |
| Mevcut dev tenant'lar | Yeniden seed gerekir (veya elle yetki eklenir) | Yeni tenant'lar alır; mevcutlar yine almaz (kopyalama tek seferlik), elle ekleme gerekir |
| Provenance | Hangi tenant'ın hangi içeriği aldığı doğru kaydedilir | v2 iki farklı içeriği temsil eder |
| Emsal | Doğru emsal | "Production yok" gerekçesi her seferinde kullanılabilir hale gelir |

Not: iki seçenekte de mevcut dev tenant'lar eylemi otomatik almaz; fark kuralın ve provenance'ın korunması.

**Öneri:** (a). İlk istisna açıkça tek seferlik kaydedilmişti; ikinci kez tekrarlamak kuralı fiilen kaldırır.

> **Cevap:** ☐ (a) v3 + reseed ☐ (b) v2 yerinde

---

## Grup 2 — Mimari owner kararları (OD-1 … OD-7)

İlk dilimi bloklamazlar. Önerilen karar sırası (neyi blokladıklarına göre): **OD-4 → OD-5 → OD-7 → OD-3 → OD-2 → OD-1**, OD-6 aşama 2'ye kadar bekleyebilir.

Detaylı gerekçe ve kaynak satırları için raporun §A2 bölümüne bakın.

### OD-4. ConfigChangeSet atomikliği

**Çatışma:** GPT brief'i ChangeSet'in birden çok tanımı (obje, alan, eylem, workflow…) **atomik** değiştirmesini istiyor. `AGENTS.md` bağlayıcı kuralı: "No cross-module ACID transactions". Tanımlar farklı modüllerin sahipliğindeyse (Semantic Catalog, Access, Workflow) tek transaction yasak.

**Seçenekler:**
- **(a)** Atomiklik yalnızca Semantic Catalog içinde. Diğer sahipler outbox olayını tüketip kendi parçalarını aktive eder ve durum bildirir: `Published → Activating → Active | ActivationFailed`. Aktivasyon tamamlanana kadar yeni eylem/workflow kullanılamaz (güvenli taraf). Doc 07:98 bu şekli zaten tarif ediyor.
- **(b)** Tüm tanımları (yetki ve workflow dahil) tek modülde topla. Modül sahiplik kurallarını bozar.

**Öneri:** (a).
**Bloklayan:** aşama 2 (Semantic Catalog + ChangeSet v1).

> **Cevap:** ☐ (a) ☐ (b) ☐ Başka: ____________

### OD-5. ConfigChangeSet'in sahibi

**Çatışma:** Doc 08:66 change set'leri **Business Control** modülüne veriyor. Doc 07:9 ise "her kontrol modülü kendi konfigürasyonunu yönetir" diyor.

**Seçenekler:**
- **(a)** Business Control modülü şimdi açılır, ChangeSet onun.
- **(b)** İlk sürümde Semantic Catalog kendi ChangeSet'ini yönetir; Business Control sonra gelir ve ChangeSet'leri projeksiyon olarak okur. Doc 08:66'dan kayıtlı bir sapma olur.

**Öneri:** (b). Tek geliştirici için bir modül daha açmak şimdi gereksiz; doc 07:9 ile uyumlu.
**Bloklayan:** aşama 2.

> **Cevap:** ☐ (a) ☐ (b)

### OD-7. LLM sağlayıcısına veri gönderimi

**Çatışma değil, eksik karar:** doc 09:75 "AI model providers — INTEGRATE later; no model may become policy authority". AI kurulum asistanı (aşama 3) bir sağlayıcıya veri gönderecek.

**Karar verilecekler:**
- Hangi sağlayıcı(lar)?
- Veri yerleşimi (AB/TR bölge şartı var mı)?
- KVKK yurt dışı aktarım dayanağı.
- Modele gidebilecek veri: yalnızca şema/metadata mı, yoksa kayıt verisi de mi? Restricted alanlar maskelenir mi?

**Öneri:** Aşama 3'te yalnızca **metadata** (alan tanımları, pipeline, ayarlar) gönderilsin, kayıt verisi gönderilmesin. Bu, KVKK riskini neredeyse sıfırlar ve kararı kayıt verisine ihtiyaç duyulan aşamaya (aşama 4, NL raporlama) erteler.
**Bloklayan:** aşama 3.

> **Cevap:** Sağlayıcı: ______ Bölge: ______ Veri kapsamı: ☐ yalnız metadata ☐ kayıt verisi de

### OD-3. Tenant tanımlı iş eylemleri

**Çatışma:** `access.action_registry` global ve yalnızca koddan dolar ("never written by a tenant"). `permission_set_items.action_key` bu tabloya FK ile bağlı. Sonuç: tenant'ın tanımladığı bir eyleme (`object.service_request.approve`) bugün yetki verilemez.

**Seçenekler:**
- **(a)** Registry'ye nullable `tenant_id`. Doğal anahtarı bozar. **Önerilmez.**
- **(b)** Access'e ait ayrı `tenant_business_actions` tablosu; `permission_set_items`'da iki nullable referans (`system_action_key`, `business_action_key`) ve "tam olarak biri dolu" CHECK'i; `object.` ad alanı tenant eylemlerine ayrılır; `IActionCatalog.IsActiveAsync` tenant parametresi alır (`Contracts` değişikliği).

**Öneri:** (b). Mevcut registry "kilitli sistem eylemleri" olarak aynen kalır.
**Bloklayan:** aşama 6 (tenant objeleri ve eylemleri).

> **Cevap:** ☐ (b) ☐ Başka: ____________

### OD-2. Workflow runtime

**Çatışma:** Doc 09:19 "Workflow runtime: ADOPT, deferred… not a home-built general workflow engine". Doc 09:99 tetikleyici: "en az iki gerçek süreç, restart/versioning ihtiyacıyla, bir spike'ta". GPT ise erken, küçük bir workflow istiyor.

**Seçenekler:**
- **(a)** Temporal'ı şimdi benimse (ayrı sunucu, işletim maliyeti).
- **(b)** Postgres üstünde dar bir yorumlayıcı (Trigger / Condition / Action / Wait / Approval; döngü ve paralellik yok), açıkça **spike** olarak, `IWorkflowRuntime` arayüzü arkasında. Karar kaydı Temporal'a geçiş tetikleyicisini yazar.
- **(c)** İki gerçek süreç ortaya çıkana kadar bekle.

**Öneri:** (b). Doc 09 tanımların ve adaptörlerin bizim olmasını istiyor; runtime değiştirilebilir kalıyor.
**Ön koşul:** hangi seçenek olursa olsun, önce olay tüketme altyapısı gerekiyor (bugünkü dispatcher yalnızca CRM olaylarını loglayıp işaretliyor).
**Bloklayan:** aşama 5.

> **Cevap:** ☐ (a) ☐ (b) ☐ (c)

### OD-1. Tenant tanımlı objeler için genel kayıt deposu

**Çatışma:** Doc 07:45 "no universal dynamic business entity store"; doc 15 §4 "not a platform-wide generic entity store"; doc 08:106 "Bad: generic EAV entities for all ERP domains". Business OS vizyonu ise tenant'ın `ServiceRequest` gibi yeni objeler tanımlamasını istiyor.

**Seçenekler:**
- **(a) Hayır.** Yalnızca typed aggregate'ler üstünde custom field. Vizyonun "yeni süreç tanımla" kısmı olmaz.
- **(b) Sınırlı evet.** Ayrı bir modülde `object_records (tenant_id, object_type_id, data jsonb, …)`; para/stok/vergi/kimlik semantiği taşıyamaz; typed aggregate'lerin invariant'larına katılamaz. JSONB doküman EAV değildir, ama doc 07:45'teki yasak EAV'den geniş olduğu için karar açıkça değiştirilmelidir.
- **(c)** Salesforce tarzı her şey genel.

**Öneri:** (b), **ama ertelenmiş.** Tetikleyici: bir pilot müşterinin CRM'e sığmayan gerçek bir objeye ihtiyacı olması.
**Bloklayan:** aşama 6.

> **Cevap:** ☐ (a) ☐ (b) şimdi ☐ (b) tetikleyiciyle ertelenmiş ☐ (c)

### OD-6. Party custom field'larının sahibi

**Çatışma:** CRM'deki `TenantFieldAggregateType` enum'unda `Party` değeri var, ama Party MasterData modülünde ve orada `custom_fields` kolonu yok. CRM başka modülün aggregate'i için alan tanımlıyor; bu bir sınır kokusu.

**Seçenekler:**
- **(a)** Party alanlarını MasterData sahiplenir (kolon + tanım MasterData'da, ya da aşama 2'de Semantic Catalog'dan tanım, MasterData'da değer).
- **(b)** CRM tarafında Party için ayrı bir overlay tablosu.

**Öneri:** (a), modül sahiplik kuralı. İlk dilimde Party tanımları API'de reddedilir.
**Bloklayan:** müşteri kartına özel alan eklenmesi (aşama 2).

> **Cevap:** ☐ (a) ☐ (b)

---

## Özet tablo

| # | Konu | Öneri | Blokladığı |
|---|---|---|---|
| K1 | Doc 15 tier 1 kabulü | Kabul | İlk dilim |
| K2 | Tier-1 ADR | Olduğu gibi onay | İlk dilim |
| K3 | CRM şablon versiyonu | v3 + reseed | İlk dilim |
| OD-4 | ChangeSet atomikliği | Yalnız Semantic Catalog içinde | Aşama 2 |
| OD-5 | ChangeSet sahibi | Semantic Catalog (şimdilik) | Aşama 2 |
| OD-7 | LLM veri gönderimi | Yalnız metadata | Aşama 3 |
| OD-3 | Tenant iş eylemleri | Ayrı Access tablosu | Aşama 6 |
| OD-2 | Workflow runtime | Dar yorumlayıcı, spike | Aşama 5 |
| OD-1 | Genel kayıt deposu | Sınırlı, ertelenmiş | Aşama 6 |
| OD-6 | Party alanları | MasterData | Aşama 2 |
