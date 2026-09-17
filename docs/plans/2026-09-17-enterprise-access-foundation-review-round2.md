# Enterprise Access Foundation — Phase 1.5 İnceleme (Round 2)

> **Girdi:** `docs/plans/Enterprise_Access_Foundation_Phase_1_5_RECONCILED_FINAL.pdf` (12 sayfa,
> tamamı okundu). Bu doküman round 1'in ([[2026-09-17-enterprise-access-foundation-review.md]])
> mutabakat turu — PDF'in kendi §2'si "What Changed After the Claude Review" tablosuyla bunu
> açıkça belirtiyor. Round 1 dosyası **değiştirilmedi**, denetim izi olarak kalıyor.
> Repo durumu round 1'den beri değişmedi (yeni commit yok), bu yüzden repo yeniden taranmadı.

---

## 1. Executive Verdict

**Round 1'de PDF ile owner'ın §17 tercihi ayrışıyordu** (PDF "COMPLETELY REVISE", benim kararım
"REVISE"). **Bu turda ayrışma kapandı:** PDF'in kendi §30'u da artık "REVISE the old Phase 1.5
plan" diyor ve gerekçesi round 1'deki gerekçeyle örtüşüyor (eski baseline + üstüne dar bir
control-plane). Bu, iki bağımsız değerlendirmenin aynı sonuca yakınsaması — güçlü bir sinyal.

**Karar: APPROVE (küçük revizyonlarla).** Round 1'in 5 executive gerekçesinin dördü PDF'e
doğrudan işlenmiş (aşağıda §2). Round 1'de "rework" gerektirecek büyüklükte hiçbir madde
kalmadı; kalanlar 2 kabul edilmesi gereken itiraz ve 4 gerçek eksik — bunlar execution
planına geçmeden önce kapatılması gereken küçük maddeler, mimariyi kırmıyor.

---

## 2. Round 1 Bulgularının PDF'e Yansıması

| Round 1 bulgusu | PDF'te karşılığı | Durum |
|---|---|---|
| Eski baseline (Host, RLS, test, FK, uniqueness, evidence) atlanmış | §1 "old baseline + control plane"; §5 ve §25'in başında baseline maddeleri IMPLEMENT NOW'da | **Kapandı** |
| `RoleAssignmentScopeType.Network` tek enum'da cross-tenant'ı karıştırıyor | §13 "Network is NOT an ordinary RoleAssignment scope", ayrı üç senaryo tablosu | **Kapandı** (§25 #20'nin hedge'ine bkz. §4) |
| Own/Team scope'un nerede duracağı tanımsız | §10 "Relation and Scope — Implement Only What Is Real": None/All/OwnedBy IMPLEMENT, Org/Territory/Team/Sharing DESIGN/FREEZE | **Kapandı** |
| `Authorize` contract'ının resource/create/context boşlukları | §8 ActorContext, §24 pipeline'da "PEP loads/provides ResourceRef + security facts", §24 not: "CREATE actions... type-level resource descriptor" | **Kapandı** |
| `account_id`'nin PrincipalRef'e bağlanmaması | §8 PrincipalRef{type,id} + gelecek principal type'lar listesi | **Kapandı** |
| Revision = decision epoch tek kavram olmalı | §19 "ConfigurationRevision and 'decision epoch' should be one concept: TenantAccessRevision" | **Kapandı** |
| Approval'ın tek BC olarak doc 08 ile çelişmesi | §17 üçlü sahiplik: CRM/Sales (invariant) → Workflow/Rules (definition/resolver) → Human Task (runtime) → Work Inbox (projeksiyon) | **Kapandı**, doc 08 isimleriyle birebir |
| Sharing/Field Security ayrı BC olmamalı | §15 "logical Access subdomains/evaluators, not necessarily separate deployable services" | **Kapandı** |
| Query authorization ↔ Authorize eşdeğerliği | §11 "Critical invariant: Authorize(actor,action,row)==ALLOW IFF row is included by ResolveAccessScope(...)"; §25 #15 test olarak IMPLEMENT NOW'da | **Kapandı** |
| Generic rule engine reddi, ama ortak AST gelecek | §18 "Do not build one generic rule engine... shared constrained expression library may emerge later"; expression-language kararı: CEL/Cedar/DMN seçimi şimdi yapılmıyor | **Kapandı**, round 1'in "kendi AST'ini icat etme" uyarısıyla uyumlu |
| Idempotency replay authorization'ı bypass ediyor | §23 "An idempotency replay must not become an authorization bypass" + sıralı pipeline | **Kapandı** (mekanizma detayına bkz. §4) |
| System katalog / copy-on-provision açık karar | §21 "OPEN DECISION" olarak işaretli | Bkz. §3 — PDF benim önerimi değil, "aç bırak" seçeneğini aldı, bu **doğru** |
| Privilege escalation guard: "sahip olmadığını veremezsin" | §20 bunu **açıkça reddediyor** | Bkz. §3 — PDF haklı |

---

## 3. PDF'in İtiraz Ettiği İki Nokta — İkisinde de PDF Haklı

**a) Escalation guard (§20).** Round 1'de önerdiğim kural ("tenant admin yalnız sahip olduğu
yetkiyi verebilir") yanlıştı. PDF'in düzeltmesi doğru: bir Security Administrator, kendisi
kullanamayacağı bir Finance rolünü **atayabilmeli** — bu, delegasyon modelinin normal biçimi
(Oracle'ın Job/Duty/Privilege ayrımında da provisioning yetkisi ile exercise yetkisi ayrıdır).
`CanExercise(permission) ≠ CanGrant(permission/role)` kabul ediyorum, round 1'deki #17
maddesini bu haliyle geri çekiyorum.

Ama PDF'in bıraktığı boşluk da gerçek: §20 "Phase 1.5 must prevent unrestricted escalation"
diyor ama mekanizma vermiyor; IMPLEMENT NOW #19 yalnızca "delegated-administration guard
baseline" yazıyor, neyin baseline'ı olduğunu tanımlamıyor. 1.5'te admin UI yokken kurulabilecek
en dar mekanizma: tenant-scoped `access.*` action'ları + bir admin rolünün **verebileceği**
role/PS key'lerinin açık bir allowlist'i (delegation boundary), "kendinde olanı ver" değil.
Bu, freeze edilmesi gereken şekil.

**b) System katalog / copy-on-provision (§21).** Round 1'de bunu önerdim ve owner onayı
gerektiğini de söylemiştim. PDF bunu **OPEN** bırakarak daha doğru bir süreç izliyor — bu,
owner memory'deki "genuinely open, do not resolve without asking" ilkesiyle tam örtüşüyor.
Kabul ediyorum: bu turda **kapatılmamalı**.

Ancak PDF'in kurmadığı bağlantı şu: "template stratejisi açık" demek, "`roles.tenant_id NOT
NULL` de açık" demek değil. §21 zaten "Roles and PermissionSets can initially be tenant-local"
diyor — yani template/system-role fikri hiç kurulmazsa, mevcut `tenant_id NULLABLE` (system
rol = NULL) tasarımı hiç yapılmaz, `tenant_id NOT NULL` + composite FK **gün 1'den** çalışır.
Bu zaten §25 IMPLEMENT NOW #3'te var. Yani "sistem katalog stratejisi açık" ifadesi
"tenant-safe FK'yi implement edemeyiz" diye okunmamalı — tam tersi, template kararı
ertelendiği için FK kararı **hemen** kapanabiliyor.

---

## 4. Doğrulanması Gereken Uyumlar (round 1'in izlediği maddeler)

Bunlar PDF'te doğru yönde değişmiş, ama execution planına geçilmeden önce netleştirilmeli:

1. **§25 #20'nin hedge'i.** "Remove/retire legacy Role->Permission and ambiguous Network-scope
   semantics **as required by migration plan**." Bu koşul bir kaçış kapısı: migration planı
   yazılırken Network enum değerinin sökümü unutulursa, eski çelişki (tek enum'da cross-tenant)
   sessizce hayatta kalır. Execution planında bu bir **zorunlu adım** olarak, koşulsuz
   yazılmalı — "migration planı gerektirirse" değil.
2. **§19 TenantAccessRevision'ın tek sayaç olduğu.** Metin "should be one concept" diyor ama
   §29'daki final mimaride hem `TenantAccessRevision` hem de decision alanında "revision" ayrı
   ayrı geçiyor. Execution planında şema tek bir `tenant_access_state.revision` kolonu olarak
   sabitlenmeli, ikinci bir "config revision" tablosu türememeli.
3. **§10/§26 kapsam tutarlılığı.** §10 Org/Territory/Team/Sharing'i DESIGN/FREEZE olarak
   işaretliyor, §26 aynı listeyi tekrarlıyor — tutarlı. Execution planı yazılırken bu listenin
   genişletilmediği (özellikle Territory veya Team'in "aslında kolay, şimdi yapalım" diye
   IMPLEMENT'e kaymadığı) kontrol edilmeli.
4. **§23'ün "codified once in the handler template" sözü sahipsiz kalmasın.** §25 #16 "Server-
   side PEP pattern / adapter contract" bu vaadin IMPLEMENT NOW karşılığı olarak okunmalı;
   execution planında açıkça bu maddeye bağlanmalı, yoksa "bir yerde codify edilecek" diye
   havada kalan bir cümle olarak kalır.

---

## 5. Hâlâ Eksik Olanlar (round 1'den taşınan, PDF'te hâlâ kapanmamış — 4 madde)

1. **Identity ayrı bir context olarak sahiplendirilmemiş.** §29'un diyagramında "IDENTITY" bir
   kutu olarak duruyor, ama §17'nin sahiplik ayrımı ve §28'in freeze tablosu membership /
   workload-identity-binding / impersonation-kaydı sahipliğini hiç atamıyor. Round 1 §6'daki
   bulgu aynen geçerli.
2. **Entitlements yalnızca pipeline'da bir yer tutucu.** §24 adım 4 "(Future) entitlement
   gate" diyor, ama bunun neden Access'in *dışında* bir modül olması gerektiği (doc 08'in
   Entitlements satırı: "No user authorization") hiç gerekçelendirilmemiş. Execution planında
   bu sınır doc 08 referansıyla açıkça yazılmalı, yoksa biri "zaten pipeline'da, Access'e
   koyalım" der.
3. **Field security kanal listesi eksik.** §15 "backend reads/writes/exports/search/extensions/
   AI context" diyor ama **outbox event payload'ı** ve **evidence `detail` alanı**nı saymıyor.
   Repo'da somut kanıt: `CompleteOpportunityHandler`, tam `payloadJson`'ı hem outbox mesajına
   hem evidence'a yazıyor (`src/Modules/CRM/Application/CompleteOpportunityHandler.cs:74-94`).
   Field security kuralları geldiğinde bu iki kanal unutulursa hassas alan sızıntısı olur.
4. **Owner kararlarının netliği korunmalı.** §28 iki maddeyi doğru şekilde açık bırakıyor
   (system catalog stratejisi, CRM owner fact). Bunlar bu turda da **kapatılmamalı** —
   yalnızca hatırlatma: execution planı bu iki maddeyi "owner onayı olmadan" varsayılan bir
   değere sabitlememeli.

---

## 6. Final Recommendation

**PDF'in kendi §30'u ile owner'ın round 1 kararı artık aynı yerde buluşuyor: "REVISE the old
Phase 1.5 plan."** Round 1'in ayrıntılı OLD→NEW tablosu (bkz. round 1 §17) büyük ölçüde hâlâ
geçerli ve PDF onu doğruluyor. Bu turda eklenecek tek şey:

- Round 1 §13 madde 17 (privilege escalation guard) **"CanExercise ≠ CanGrant + delegation
  boundary allowlist"** olarak düzeltilsin.
- Round 1 §13 madde 2 (system katalog) **OPEN** kalsın, owner'a fresh ask gerekiyor; ama
  `roles.tenant_id NOT NULL` + composite FK bu karardan **bağımsız olarak** hemen implement
  edilebilir.
- Execution planına geçmeden önce §4'teki 4 maddenin (hedge, tek revision sayacı, scope
  tutarlılığı, handler template sahipliği) ve §5'teki 4 eksiğin (Identity, Entitlements sınırı,
  field security kanalları, owner kararlarının açık kalması) plana açıkça yazılması yeterli.

Bu, mimariyi kırmayan, execution planına geçişi engellemeyen bir "kapat ve ilerle" listesi.
Owner onayı gereken tek şey değişmedi: **system katalog stratejisi** ve **CRM owner fact**
(§28'de zaten doğru işaretli).

---

*Round 1 dosyası: `docs/plans/2026-09-17-enterprise-access-foundation-review.md` (tam benchmark
kaynakları ve ayrıntılı veri modeli/pipeline/freeze listesi orada duruyor, bu round onu
tekrarlamıyor).*
