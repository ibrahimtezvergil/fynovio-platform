# AI Business OS — Owner Kararları (Karar Kaydı)

**Durum:** KABUL EDİLDİ — 2026-09-30, platform sahibi. Bu dosya bundan sonra AI Business OS yönünün **mimari baseline**'ıdır.
**Dal:** `docs/ai-business-os-architecture`
**İlgili kayıtlar:**
- `adr-business-os-principles.md`: 10 temel ilke (bağlayıcı).
- `adr-tier1-custom-fields.md`: K1–K3 (Accepted).
- `adr-workflow-runtime.md`: OD-2 ve Temporal'a geçiş kriterleri (Accepted, uygulanmadı).
- `2026-09-30-architecture-reconciliation.md`: gerekçeler ve kaynak satırları (rapor).

**Kabul ≠ uygulama.** Aşağıdaki kararların çoğu gelecekteki bir aşamayı yönetir. Şu an uygulanan tek şey tier-1 custom field dilimidir (K1–K3). Diğer her şey, ilgili aşama açıldığında bu kararlara göre yapılır.

**Kilitli portföyle ilişki:** Harici karar portföyü (`../enterprise ve B2B mimari araştırma/docs/architecture-analysis/`) git deposu değildir ve bu kayıtla **düzenlenmedi**. Aşağıda "sapma" olarak işaretlenen maddeler, bu repo içinde o dokümanların ilgili satırlarının yerine geçer. Portföye aynı notun işlenmesi ayrı bir owner işidir.

---

## Grup 1 — İlk dilim

| # | Karar | Not |
|---|---|---|
| **K1** | Doc 15 **tier 1 kabul**, **tier 2 kabul**, **§6 (Network scope ve alan bazlı kilit yönü) kabul**. | Doc 15'in başlığındaki "proposed" durumu bu kayıtla kapanır. Kabul, hepsinin şimdi uygulanacağı anlamına gelmez: ilk uygulama yalnızca tier-1 dilimidir. Tier 2 ve §6, kendi dilimleri açılınca uygulanır. |
| **K2** | `adr-tier1-custom-fields.md` **olduğu gibi onaylandı**. | İlk sürümün tipleri ve limitleri ADR'daki gibidir. Ölçüm gerektirirse ayrıca değiştirilir. |
| **K3** | CRM yetki şablonu **v3'e yükseltilir, development tenant'ları yeniden seed edilir**. | Versiyon/provenance invariant'ına yeni bir istisna yapılmaz. `party.create` istisnası tek seferlik olarak kalır. |

## Grup 2 — Mimari owner kararları

### OD-4 — ConfigChangeSet atomikliği → (a)
- ACID atomiklik yalnızca sahip modül içinde (Semantic Catalog).
- Modüller arası aktivasyon outbox ve idempotent tüketiciler üzerinden: `Published → Activating → Active | ActivationFailed`.
- **Tam aktivasyon gerçekleşmeden** yeni capability, eylem veya workflow kullanılabilir sayılmaz.

### OD-5 — ConfigChangeSet sahibi → (b)
- İlk sürümde Semantic Catalog kendi ChangeSet'ini yönetir.
- Şimdilik ayrı bir Business Control modülü açılmaz.
- Gerçek ihtiyaç doğduğunda Business Control, projeksiyon/orkestrasyon katmanı olarak eklenebilir.
- **Sapma:** doc 08:66 (change set'lerin SoR'u Business Control) yerine geçer.

### OD-7 — LLM sağlayıcısı ve veri kapsamı
- Mimari, **sağlayıcıdan bağımsız bir AI Gateway** arkasında kalır. Hiçbir sağlayıcı domain mimarisine sızmaz.
- İlk AI yapılandırma asistanı aşamasında modele **yalnızca metadata** gider: alan tanımları, pipeline tanımları, konfigürasyon, hassas olmayan şema metadata'sı.
- Kayıt ve müşteri verisi şimdilik **gönderilmez**.
- Secret ve credential'lar **hiçbir durumda** LLM bağlamına girmez.
- Restricted/hassas iş verisi için sonraki aşamada ayrı bir gizlilik/KVKK/veri yerleşimi kararı verilir.
- İlk geliştirme sağlayıcısı uygulama kolaylığına göre seçilebilir; bu seçim bir mimari karar **değildir**.

### OD-3 — Tenant tanımlı iş eylemleri → (b)
- Global `access.action_registry` platform/sistem eylemleri için değişmez ve kod sahipli kalır ("never written by a tenant").
- Tenant iş eylemleri Access'e ait ayrı bir tabloda tutulur.
- `object.*` ad alanı tenant tanımlı iş eylemlerine ayrılır.
- Sistem eylemleri ile iş eylemleri birbirine karıştırılmaz.
- Uygulama ayrıntısı (iki nullable referans ve "tam olarak biri" CHECK'i, `IActionCatalog` imza değişikliği) raporun §E bölümündedir; uygulama aşama 6'dadır.

### OD-2 — Workflow runtime → (b)
Ayrıntı ve Temporal'a geçiş kriterleri `adr-workflow-runtime.md`'dedir. Özet:
- Postgres üstünde dar bir runtime/spike: Trigger, Condition, Action, Wait, Approval.
- Döngü, paralel yürütme, iç içe workflow ve serbest kod yok.
- `IWorkflowRuntime` soyutlaması arkasında.
- Temporal şimdi eklenmez.
- **Ön koşul:** önce gerçek olay tüketme/abonelik altyapısı tamamlanır.
- **Sapma:** doc 09:19 ("not a home-built general workflow engine") yerine geçer; runtime genel değil, dar ve değiştirilebilir.

### OD-1 — Tenant tanımlı obje deposu → (b) sınırlı, tetikleyiciyle ertelenmiş
- Şimdi genel bir `object_records` sistemi **inşa edilmez**.
- Mimari bunu ileride **engellememelidir** (tanım kimlikleri, `ObjectType.backing` kavramı, `object.*` ad alanı buna yer bırakır).
- **Yeniden açılma tetikleyicisi:** gerçek bir pilot müşteride, CRM'e veya typed aggregate'lere doğal biçimde sığmayan bir iş objesi ortaya çıkması.
- O zaman yalnızca **hafif iş objeleri** için kullanılır.
- Para, stok, vergi, kimlik, güvenlik veya güçlü domain invariant'ı taşıyan kavramlar genel obje deposuna **taşınmaz**.
- **Sapma (yeniden açılınca):** doc 07:45 ve doc 15 §4 o gün ayrıca güncellenir; bugün yürürlükte kalırlar.

### OD-6 — Party custom field'ları → (a)
- Party, MasterData sahipliğinde kalır.
- CRM başka bir modülün aggregate değerlerini sahiplenmez.
- İlk dilimde Party custom field'ları **desteklenmez** (API reddeder).
- Semantic Catalog geldiğinde tanımların merkezi olabilir, ama Party **değerleri** MasterData'da kalır.

## Ek mimari karar

Uzun vadeli model: **yatay mimari + dikey ürün/GTM.** 10 temel ilke `adr-business-os-principles.md`'de bağlayıcı olarak kayıtlıdır.

## Aşama → karar eşlemesi

| Aşama | İçerik | Yöneten kararlar | Durum |
|---|---|---|---|
| 1 | Tier-1 custom fields (Opportunity) | K1, K2, K3, OD-6 | **Uygulanıyor** |
| 2 | Semantic Catalog + ChangeSet v1 | OD-4, OD-5, OD-6 | Bekliyor |
| 3 | AI Gateway + yapılandırma asistanı | OD-7 | Bekliyor |
| 4 | Semantik metrikler / NL raporlama | OD-7 (kayıt verisi için yeni karar) | Bekliyor |
| 5 | Olay tüketme altyapısı → workflow spike | OD-2 | Bekliyor |
| 6 | İş eylemleri, tenant objeleri | OD-3, OD-1 (tetikleyiciyle) | Bekliyor |
