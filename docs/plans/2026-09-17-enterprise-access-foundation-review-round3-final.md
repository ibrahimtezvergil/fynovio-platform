# Enterprise Access Foundation — Phase 1.5 Round 3 Final Closure

> **Kapsam:** Bu bir redesign turu değil, closure turu. Round 1 ve Round 2 dosyalarına
> dokunulmadı, denetim izi olarak duruyorlar. Bu turda repo'ya karşı doğrulanan noktalar:
> `src/Modules/Access/**`, `src/Modules/CRM/Domain/Opportunity.cs`,
> `src/Modules/CRM/Application/CompleteOpportunityHandler.cs`, `src/Host/Program.cs`,
> `docs/schema/crm-sales-schema.md`, `AGENTS.md`. Repo durumu round 1'den bu yana
> değişmedi (git status: yalnızca üç yeni PDF/review dosyası eklenmiş, kod değişikliği yok).

---

## 1. Final Verdict

**ARCHITECTURE APPROVED.** Bu turda mimariyi REWORK seviyesine çekecek yeni bir bulgu
çıkmadı. Round 1 ve Round 2'de açık bırakılan noktaların tamamı ya bu turda net bir karara
bağlandı, ya da gerçekten owner kararı gerektirdiği için (haklı olarak) açık kalmaya devam
ediyor. Repo'ya karşı doğrulama, önceki iki turun tespitlerinde hiçbir tutarsızlık
bulmadı — Access modülü hâlâ tam olarak round 1'de tarif edilen durumda (RLS yok, test yok,
Host kaydı yok, tek migration, `Network` scope'u hâlâ enum'da, `roles.tenant_id` hâlâ
nullable). Bu, mimari kararların "zaten var olanı doğru tanımlaması" anlamına geliyor,
kararsızlık değil.

---

## 2. Closure Matrix

| Topic | Önceki belirsizlik | Final karar | Repo etkisi |
|---|---|---|---|
| **Identity ownership** | Identity ayrı context olarak sahiplendirilmemişti (round 2 bulgusu) | Identity; User/ExternalIdentity/ServicePrincipal/AgentIdentity/TenantMembership/Impersonation-ActingFor/WorkloadIdentityBinding'in **logical owner**'ı. Access yalnız bunları **tüketir** (Trusted ActorContext üzerinden) | Bkz. §6.1 — bugün Account/ExternalIdentity/TenantMembership fiziksel olarak Access assembly'sinde; bu doc 08 §2'nin adlandırılmış pilot istisnası, çelişki değil |
| **Entitlements** | Round 2'de yalnız pipeline'da yer tutucu, sınır gerekçesiz | Entitlements ≠ Access. Entitlement = "tenant bu capability'yi ticari olarak satın aldı mı", Access = "principal bu action'ı yapabilir mi". `AND` ile bileşirler. Phase 1.5 **implement etmez** | Repo'da hiç Entitlements modülü yok — delta değil, beklenen boşluk. `if (plan=="enterprise")` tarzı kod bugün yok, gelecekte de yasak olarak freeze edildi |
| **Sensitive Data Projection** | Round 2'de field security kanal listesi eksikti (outbox, evidence) | Field Security artık daha geniş bir **Sensitive Data Projection Policy**'nin bir görünümü. Her kanal (API, mutation input, read model, export, search/vector index, AI context, extension/connector payload, outbox, evidence, audit, log/telemetry) `classification → bu kanal saklayabilir mi → evet: korumalı depolama / hayır: omit-mask-redact` sorusunu cevaplamalı. "Hassas alan evidence'a asla girmemeli" **yanlış**; kontrollü/korumalı saklama geçerli bir sonuç | **Somut repo delta:** `CompleteOpportunityHandler` aynı `payloadJson`'ı hem `OutboxMessages` hem `EvidenceRecords`'a yazıyor (satır 74–94), hiçbir classification/projection kontrolü yok. Bu Phase 1.5'te düzeltilmiyor (DESIGN/FREEZE), ama execution planına girecek somut bir "mevcut durum" kaydı |
| **System catalog** | Round 1 copy-on-provision önerdi, round 2 bunu geri çekti | **OPEN kalmaya devam ediyor** (template/reconcile stratejisi owner kararı). Ama bu, tenant-safety kararını bloke etmiyor: **Action Registry = platform-owned; tenant Role = tenant-owned; tenant PermissionSet = tenant-owned** — Phase 1.5'te `tenant_id = NULL` ile temsil edilen "system role" kavramı hiç kurulmuyor | **Somut repo delta:** `roles.tenant_id` bugün `nullable: true` (migration L61). Target karar bunu `NOT NULL` yapıyor — bu, catalog stratejisinden **bağımsız** olarak kapanmış bir karar |
| **CRM Owner semantics** | "Owner ne demek" açıktı | **Business semantics FROZEN:** Owner = "bu kayıttan şu an sorumlu olan principal", `created_by`'dan farklı (creator = tarihsel provenance, ownership sonradan değişebilir) | **Fiziksel mapping doğrulanamadı** — bkz. §7. `AssignedPrincipal` var ama değiştirilebilir değil (Reassign() metodu yok, yalnız `Create()`'te set ediliyor). Bu yüzden bugün fiilen "yaratılışta atanan principal" gibi davranıyor, "şu anki sorumlu" gibi değil |
| **Network scope** | PDF'te "as required by migration plan" hedge'i vardı | **Koşulsuz:** Network normal bir RoleAssignment scope'u **olmayacak**. Üç senaryo (gerçek cross-tenant membership / governed cross-tenant analytics / gelecekteki explicit federation grant) ayrı modellenir | **Somut repo delta:** `RoleAssignmentScopeType.Network` hâlâ enum'da (`RoleAssignment.cs:9`). Sökümü execution planının bir maddesi, koşula bağlı değil |
| **TenantAccessRevision** | ConfigurationRevision / decision epoch / access revision ayrı ayrı türeyebilirdi | Tek kavram: **TenantAccessRevision** — tenant başına monoton, effective authorization config değiştiğinde artar, decision'da taşınır. `row_version` (satır bazlı optimistic concurrency) ile asla karıştırılmaz | Repo'da bugün hiçbir revision/epoch kavramı yok (yalnız `row_version` satır bazlı, `TenantMembership`/`RoleAssignment`/`Opportunity` üzerinde). Delta değil, saf ekleme — çakışma riski yok |
| **Scope lock** | Execution plan'ın DESIGN/FREEZE maddelerini sessizce IMPLEMENT'e taşıma riski | Execution plan **WHAT**'ı değiştiremez, yalnız **HOW**'ı detaylandırır. Yeni implementation ihtiyacı çıkarsa STOP → Architecture Delta → owner onayı | Süreç kararı, repo etkisi yok |
| **PEP + Idempotency order** | Round 2'de "handler template'te bir yerde codify edilecek" gevşek bırakılmıştı | Kesin sıra: authFirst, sonra idempotency lookup/replay (bkz. §4). Idempotency asla authorization bypass'ı olamaz | **Somut repo delta:** `CompleteOpportunityHandler.HandleAsync`, idempotency lookup'ı (satır 40-59) authorization'dan **önce** çalıştırıyor — çünkü authorization hiç yok. Handler bugünkü haliyle hedef sırayla uyumsuz, Phase 2'de yeniden yazılacak |

---

## 3. Final Ownership Matrix

Verilen tablo doğrulandı. **Gerçek bir architectural contradiction bulunamadı.** Tek not:
tablo "Automation" için "Workflow/Automation" yazıyor; doc 08'in kendi ismi "Workflow /
Rules"dur (aynı bounded context, isimlendirme farkı — çelişki değil, execution planında
doc 08'in adı kullanılmalı).

| Concern | Owner |
|---|---|
| Authentication | Identity |
| User identity | Identity |
| External identity | Identity |
| Tenant membership | Identity |
| Service/workload identity | Identity |
| Agent identity | Identity |
| Impersonation / ActingFor | Identity |
| Organization membership | Organization |
| Positions / org hierarchy | Organization |
| Action Registry | Platform + owning business domain vocabulary |
| PermissionSet / grants | Access |
| Role | Access |
| RoleAssignment | Access |
| Authorization decision | Access |
| Sharing evaluation | Access subdomain |
| Field Security evaluation | Access subdomain |
| Sensitive data projection rules | Shared security/platform contract + owning domain classification |
| Territory facts | Territory/business context |
| Tenant Entitlement | Entitlements |
| Domain lifecycle policy | Owning business domain |
| Approval required invariant | Owning business domain |
| Approval policy/path/version | Workflow/Rules |
| Human task lifecycle | Human Task / Approval runtime |
| Work Inbox | Projection / UX |
| Automation | Workflow/Rules *(isim: doc 08 ile hizalandı)* |

---

## 4. Final Authorization Command Pipeline

```text
 1. Authenticate
 2. Build trusted ActorContext
 3. Resolve active Tenant context
 4. Tenant membership/state validation
 5. (Future) Entitlement gate
 6. PEP constructs ResourceRef / required security facts
 7. Authorization (Access PDP: default-deny, grants ∧ scope ∧ relation ∧ ¬forbid)
 8. Idempotency lookup / replay decision
 9. Business policy
10. Approval/state requirement if applicable
11. Domain invariants
12. Mutation
13. Evidence + Outbox (Sensitive Data Projection Policy'ye tabi)
14. Commit
```

Kritik kural: adım 8'de dönen replay yanıtı, adım 7'nin **güncel** sonucudur — eski bir
idempotency kaydı, güncel authorization/tenant durumu tekrar değerlendirilmeden
döndürülemez. Idempotency state en az `{tenant, principal/actingFor, action, request
fingerprint}` ile bağlı olmalı. Bu, `CompleteOpportunityHandler`'ın bugünkü sırasını
(idempotency → resource → mutation, authorization hiç yok) tersine çeviren bir Phase 2
handler-template kararıdır; şema tasarımı bu turun kapsamı dışında.

---

## 5. Final Query Authorization Invariant

```text
Authorize(actor, action, row) == ALLOW
IFF
row ∈ ResolveAccessScope(actor, action, resourceType)
```

Doğrulandı, değişmedi. Bilinmeyen scope terimi adapter'da **deny** olarak ele alınır
(fail-closed). Bu eşdeğerlik execution planında bir **contract test** olarak IMPLEMENT NOW
listesinde kalmalı (round 1 §12, madde 15).

---

## 6. Current Repo Deltas

Kod/migration değişikliği yapılmadı; yalnızca liste.

### 6.1 Identity — logical vs physical (çelişki değil, kayıt)

`Account`, `ExternalIdentity`, `TenantMembership` bugün fiziksel olarak
`src/Modules/Access/Domain/Identity/` altında, `Access.csproj` assembly'sinde duruyor.
Bu, **doc 08 §2'nin adlandırılmış pilot istisnası** ("Identity ve Access may be one assembly
with explicit internal ownership") ve `docs/schema/identity-access-schema.md`'ın "Namespace
layout, Revision 3" notuyla zaten belgelenmiş bir durum — yeni bir bulgu değil, yalnızca
**target logical ownership (Identity) ile current physical location (Access assembly)
arasındaki farkın açıkça yazılması** gerekiyordu, artık yazıldı. Taşıma önerilmiyor.

`ServicePrincipal`, `AgentIdentity`, `Impersonation`/`ActingFor`, `WorkloadIdentityBinding`
bugün repo'da **hiç yok** — ne Contracts'ta ne Access'te. Bu DESIGN/FREEZE kapsamında,
Phase 1.5'te implement edilmiyor.

### 6.2 `roles.tenant_id` nullable → target NOT NULL

`RoleConfiguration.cs`: `tenant_id` nullable, "NULL = platform system role" yorumuyla.
Migration'da `nullable: true`. Target karar (§2) bunu tenant-owned + NOT NULL yapıyor.
System-role/NULL-tenant kavramı Phase 1.5 target modelinde hiç kurulmuyor.

### 6.3 `RoleAssignmentScopeType.Network` hâlâ enum'da

`RoleAssignment.cs:5-10`. Target karar bunu normal scope olarak reddediyor. Sökümü execution
planının koşulsuz bir maddesi.

### 6.4 `AssignedPrincipal` mutasyonsuz

`Opportunity.cs`: `AssignedPrincipal` yalnız `Create()`'te set ediliyor, `Win()`/`Open()`/
`Lose()`/`CancelLine()` dışında değiştirme metodu yok. "Owner = şu anki sorumlu" business
semantiği ile "field hiç değişmiyor" fiziksel gerçeği arasında **doğrulanamayan bir boşluk**
var — bkz. §7.

### 6.5 `CompleteOpportunityHandler` sıralaması hedef pipeline ile uyumsuz

Authorization hiç yok; idempotency lookup en baştan çalışıyor (satır 40-59), resource
yüklemeden ve authorization'dan önce. Aynı handler, tam `payloadJson`'ı hem outbox'a
(satır 74-84) hem evidence'a (satır 86-94) yazıyor — Sensitive Data Projection Policy
henüz uygulanmıyor. İkisi de Phase 2'de bu handler yeniden yazılırken düzeltilecek;
şimdi düzeltilmiyor.

### 6.6 Değişmeyenler (round 1/2 ile tutarlı, tekrar doğrulandı)

`tests/Access.Tests` yok. `AccessDbContext` Host'a kayıtlı değil. Tek migration
(`InitialAccessSchema`), RLS migration'ı yok. `PermissionSet`/`PermissionGrant` fiziksel
olarak yok (yalnız `Permission`/`RolePermission`). `role_permissions`'ta `tenant_id` yok.

---

## 7. Owner Decisions Still Open

Yalnızca gerçekten açık kalanlar:

### OPEN

**System Role / PermissionSet catalog lifecycle stratejisi.** A (template→provision copy),
B (platform-managed referenced definitions), C (copy + continuous reconciliation) arasında
seçim owner kararı. Bu turda seçilmedi, seçilmeyecek. **Not:** bu seçim, `roles.tenant_id
NOT NULL` kararını bloke etmiyor (§2, §6.2) — o zaten kapandı.

### REPO VERIFICATION (owner'a yalnızca fiziksel mapping sorusu)

**CRM Opportunity owner semantiği.** Business anlam (Owner = şu anki sorumlu principal)
FROZEN, tekrar tartışılmıyor. Ama repo bunu kesin göstermiyor: `AssignedPrincipal` tek aday
alan, fakat bugün değiştirilemez (no `Reassign()`), yani fiilen "yaratılışta atanan" gibi
davranıyor. Owner'a sorulacak soru **yalnızca** şu — business anlam değil, fiziksel model:

- (a) `AssignedPrincipal`'a bir `Reassign()` metodu eklenip bu alan Owner olarak mı
  kullanılacak, yoksa
- (b) ayrı, açık bir `OwnerPrincipalRef` alanı mı eklenecek (assignment'tan bağımsız)?

Bu, Phase 2 komutları (`OwnedBy` scope term'ünün neyi sorgulayacağı) yazılmadan önce
kapanmalı, ama "owner ne demek" sorusu değil — "hangi kolon" sorusu.

---

## 8. Final Freeze List

Execution planının artık değiştiremeyeceği kararlar (round 1 + round 2 + round 3 birleşik):

1. Tenant isolation ≠ authorization; RLS savunma derinliği, PDP uygulama katmanında.
2. Default deny; fail-closed (bilinmeyen action, eksik attribute, evaluator hatası → deny).
3. Functional grants additive (union); explicit deny grant modelinde yok.
4. Restriction/forbid ayrı katman, sıra bağımsız, forbid kazanır; Phase 1.5'te tenant-authored forbid yok.
5. ActionKey stabil, domain-owned business vocabulary; Access yalnız evaluate eder.
6. Role → PermissionSet → PermissionGrant(ActionKey, relation?) → RoleAssignment → Principal.
7. Role = security persona; Role ≠ organization hierarchy.
8. Principal/Actor contract'ları `account_id`'ye hard-code değil; PrincipalRef/PrincipalId{type,id} bazlı.
9. Owner relation (`OwnedBy`) Phase 1.5'te implement edilir.
10. Query scope: None/All/OwnedBy IMPLEMENT; Org/Territory/Team/Sharing DESIGN/FREEZE.
11. `Authorize(row)==ALLOW IFF row ∈ ResolveAccessScope(...)` — contract test olarak zorunlu.
12. Organization/Territory Access'e yalnız fact sağlayan context'ler, Access-owned hierarchy değil.
13. **Network normal RoleAssignment scope değil** (koşulsuz, hedge yok).
14. Sharing + Field Security = Access subdomain'leri, ayrı BC değil.
15. Sensitive Data Projection Policy: her kanal classification + "bu kanal saklayabilir mi" sorusuna tabi; "hassas alan evidence'a asla giremez" **yanlış** kural.
16. Approval sahipliği bölünmüş: business invariant (owning domain) / definition-path-resolver (Workflow-Rules) / runtime (Human Task) / projection (Work Inbox).
17. **TenantAccessRevision** tek kavram; `row_version` ile karıştırılmaz; ikinci bir revision/epoch türetilmez.
18. `CanExercise(permission) ≠ CanGrant(permission/role)` — delegated administration boundary modeli, "yalnız sahip olduğunu ver" kuralı değil.
19. **Action Registry platform-owned; tenant Role ve tenant PermissionSet tenant-owned** (`tenant_id NOT NULL`) — system-role/NULL-tenant kavramı Phase 1.5'te kurulmaz.
20. Idempotency asla authorization bypass'ı olamaz; authorization idempotency lookup'tan **önce** çalışır.
21. Access, domain'in private tablolarını doğrudan okumaz; ResourceRef/SecurityFacts PEP tarafından sağlanır.
22. CREATE action'ları için Resource, `id=null` type-level descriptor + context facts olabilir.
23. Generic rule engine yok; ortak AST/DSL yok; CEL/Cedar/OPA/OpenFGA runtime seçimi bu fazda yapılmaz.
24. Business kod commercial plan ismine bağlanmaz (`if (plan=="enterprise")` yasak); Entitlements ayrı, stable capability üzerinden çalışır.

---

## 9. Scope Lock

```text
Execution plan may refine HOW.
Execution plan may not change WHAT.
```

DESIGN/FREEZE olarak işaretli hiçbir capability (Organization scope, Team relations,
Sharing runtime, Field Security runtime/admin, Restriction policy engine, Territory,
ServicePrincipal/Agent runtime, ActingFor/impersonation runtime, Direct PermissionSet
assignment, Delegation, JIT, Break-glass, SoD, Access Review, Entitlements, Approval
runtime, System catalog reconciliation) execution planında kendiliğinden IMPLEMENT'e
dönüşemez. Yeni implementation ihtiyacı görülürse: **STOP → Architecture Delta → neden
gerekli → owner onayı → Phase 1.5 scope güncellemesi.** Owner onayı olmadan IMPLEMENT NOW
listesi büyümez.

---

## 10. Final Gate

```text
ARCHITECTURE APPROVED — READY FOR EXECUTION PLANNING
```

Blocker yok. İki madde owner girişi bekliyor ama bunlar execution planının **yazılmasını**
engellemiyor — yalnızca planın ilgili iki maddesinde (system catalog stratejisi, CRM owner
field mapping) "owner onayı bekleniyor" notuyla ilerlenmesi gerekiyor. Bunların dışında
kalan her şey (freeze list, ownership matrix, pipeline, query invariant, scope lock)
yoruma kapalı ve execution planı bunların üzerine yazılabilir.
