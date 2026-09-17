# Enterprise Access Foundation — Phase 1.5 Adversarial Architecture Review

> **Durum:** yalnızca inceleme. Kod, migration ya da paket değişikliği yok. İncelenen:
> `docs/plans/enterprise-access-foundation/Enterprise_Access_Foundation_Phase_1_5_Target_Architecture.pdf` (11 sayfa, tamamı).
> Ölçüt: `AGENTS.md`, `docs/schema/identity-access-schema.md`, doc 08 / doc 19 (araştırma projesi),
> `src/Modules/Access/**`, `src/Contracts/**`, `src/Host/Program.cs`, `tests/**`.
> Tarih: 2026-09-17. Owner onayı olmadan execution planına geçilmez.

---

## 1. Executive Verdict

**PDF'e karar: APPROVE WITH REVISIONS.**

PDF'in kavramsal yönü doğru (tenant ≠ authz, default deny, additive grant, ayrı restriction katmanı,
Authorize/ResolveAccessScope ayrımı, generic rule engine reddi). Ancak bu hâliyle execution
boundary'si olarak kabul edilemez. En önemli 5 neden:

1. **PDF eski Phase 1.5'i değiştirmiyor, atlıyor.** Owner'ın §5.D kararıyla Phase 2'yi kilitleyen
   baseline (Host registration, `role_assignments → roles` tenant-safe FK, `(tenant_id, name)`
   uniqueness, `row_version DEFAULT 1`, Access RLS, grant/revoke evidence, `tests/Access.Tests`,
   authorization contract) PDF §26'daki 18 satırlık IMPLEMENT NOW listesinde yok. Yalnızca
   "Access RLS + negative tests" var. Yani PDF, uygulamaya bağlı olmayan, testi olmayan ve
   `AGENTS.md` binding-core kural #1'i (tenant-safe composite FK) ihlal eden bir modülün üstüne
   control plane tasarlıyor.
2. **Cross-tenant network kapsamı hiç yok.** doc 19 §6–7'de owner'ın onayladığı canlı gereksinim
   (üretici çalışanı bayi verisini governed analytics üzerinden görür) ve repo'daki
   `RoleAssignmentScopeType.Network` PDF'in modelinde hiçbir yere oturmuyor.
3. **PDF'in "tek enum'a koyma" dediği yapı repo'da zaten var.** `RoleAssignmentScopeType
   { Tenant, OrganizationUnit, Network }` hem organizasyonel hem cross-tenant kapsamı tek enum'da
   topluyor. `OrganizationUnit` şu an çözülemez, çünkü `Organization` modülü `Class1.cs`
   placeholder'ı ve `ResolveScopeAt` yok. PDF bu çelişkiyi belirtmiyor.
4. **"Kim, hangi kayıtları" boyutunun nerede konfigüre edileceği tanımsız.** PDF Own/Team'i
   relationship, Org/Territory'yi scope olarak ayırıyor (doğru). Ama "Sales Rep yalnız kendi
   fırsatını kazanabilir, Manager hepsini" bilgisinin hangi tabloda/konseptte duracağını
   söylemiyor. Dataverse bunu privilege'ın access level'ında, HubSpot permission'ın
   Everything/Team/Owned seçiminde tutuyor. Bu boşluk Phase 2'nin ilk gerçek ihtiyacı.
5. **Access'in domain tablolarını sorgulayamayacağı (doc 08) PDF'in contract'ına yansımamış.**
   `Authorize(Principal, Action, Resource, Context)` resource attribute'larını kimin sağladığını,
   create action'larında (henüz instance yokken) Resource'un ne olduğunu ve query scope'un nasıl
   bir veri yapısı olarak döndüğünü tanımlamıyor. Bunlar sonradan değişirse her PEP yeniden
   yazılır, yani contract'ın freeze edilmesi gereken asıl kısmı bunlar.

Ek olarak: PDF'in scope'u (18 IMPLEMENT NOW) boş bir modül için şişkin. Direct PermissionSet
assignment ve authorized-actions discovery gibi tüketicisi olmayan yollar bugün implement ediliyor.
Buna karşılık outbox, idempotency ve evidence gibi `AGENTS.md` binding-core gereksinimleri hiç
anılmıyor.

---

## 2. Repository Current State (doğrulanmış)

### 2.1 Access modülü — dosya düzeyi

| Alan | Gerçek durum | Kaynak |
|---|---|---|
| Proje | `Access.csproj`, yalnız `Contracts` referansı (kural uyumlu) | `src/Modules/Access/Access.csproj` |
| Katmanlar | Yalnız `Domain/` + `Persistence/`. **Application katmanı yok, command/query yok, Authorize yok, PDP/PEP yok** | dizin listesi |
| Identity entity'leri | `Account` (platform-global, tenant_id yok), `ExternalIdentity` (issuer+subject UNIQUE → `PrincipalRef` fiziksel karşılığı), `TenantMembership` (Invited/Active/Disabled, row_version) | `Domain/Identity/*.cs` |
| Authorization entity'leri | `Role` (`TenantId?` nullable: NULL = system role, `IsSystem`), `Permission` (`Key`, `Description`; tenant yok), `RolePermission` (composite PK join), `RoleAssignment` (`TenantId`, `AccountId`, `RoleId`, `ScopeType`, `ScopeId?`, `ValidFrom`, `ValidTo?`, `RowVersion`, `Grant()`/`Revoke()`) | `Domain/Authorization/*.cs` |
| Role modeli | Role → Permission doğrudan (`role_permissions`). **PermissionSet yok.** Role adı için uniqueness yok | `RoleConfiguration.cs` |
| Permission modeli | Global katalog, `key` UNIQUE, surrogate `bigint id`. **owner_module / resource_type / risk_class / lifecycle yok** (PDF §23'ün istediği metadata) | `PermissionConfiguration.cs` |
| Assignment | Var, effective-dated (`valid_from`/`valid_to`), revoke = `valid_to` set. Principal = `account_id` (bigint), **PrincipalRef değil, principal_type yok**. `valid_to > valid_from` CHECK yok | `RoleAssignment.cs`, migration |
| Scope | Tek enum `tenant|organization_unit|network` + `scope_id` bigint. Kod yorumu bunu kasıtlı olarak "under-validated" bırakıyor (doc 19 §9 açık) | `RoleAssignment.cs:77-93` |
| Tenant boundary | `role_assignments.role_id → roles.id` **tekli FK**: bir tenant başka tenant'ın rolünü atayabilir. `AGENTS.md` binding-core #1 ihlali; owner memory'de "açık karar" olarak işaretli | migration L143-149, `identity-access-schema.md` L11-12 |
| `role_permissions` | tenant_id yok. Tenant rolüne ait satırlar tenant'sız, RLS uygulanamaz | migration L152-177 |
| DbContext | `AccessDbContext`, iki schema (`identity`, `access`) tek assembly. doc 08 §2 pilot istisnası | `AccessDbContext.cs` |
| Migration | Tek: `20260914231747_InitialAccessSchema`. **RLS migration yok** (CRM ve MasterData'da var). DB-level `row_version DEFAULT 1` yok | `Persistence/Migrations/` |
| Concurrency | `RowVersionInterceptor` (Access'e özgü kopya) | `RowVersionInterceptor.cs` |
| Host | **Kayıtlı değil.** `Program.cs` yalnız `CrmDbContext`, `MasterDataDbContext`, `IPartyDirectory`, `IPartyIdentityResolver` kaydediyor | `src/Host/Program.cs` |
| Worker | Boş hosted service, Access yok | `src/Worker/Program.cs` |
| Testler | **`tests/Access.Tests` yok.** Yalnız `CRM.Tests`, `MasterData.Tests` | `tests/` |
| Outbox / Idempotency / Evidence | Access'te **hiçbiri yok** (CRM ve MasterData'da var) | dizin listesi |
| Connection string | Varsayılan `Username=postgres` (superuser). RLS eklense bile bu bağlantıyla bypass edilir | `AccessConnectionString.cs` |

### 2.2 Contracts düzeyi

`src/Contracts/`: `TenantId`, `EntityRef`, `EntityVersion`, `IHasRowVersion`, `PrincipalRef`,
`PartyRef`, `PartyType`, `PartyDirectoryEntry`, `IPartyDirectory`, `IPartyIdentityResolver`.
**Hiçbir authorization primitive'i yok**: `ActionKey`, `AuthorizationRequest/Decision`,
`IAuthorizer`, `AccessScope` ya da `ActorContext` bulunmuyor.

### 2.3 Bağımlılık gerçekliği

- `tests/CRM.Tests/Architecture/ModuleBoundaryTests.cs`, CRM'in `Access`, `MasterData`,
  `Organization`, `TenantLifecycle` namespace'lerine bağımlı olmadığını test ediyor. Graphify
  sorgusu da Access'e dışarıdan kenar göstermiyor. **Bugün hiçbir modül Access'e bağımlı değil.**
  Bu yüzden Access'i şimdi yeniden şekillendirmenin migration riski düşük. "Şimdi yap"
  argümanını güçlendiren bir durum.
- `CRM.Application.CompleteOpportunityHandler` **hiç authorization yapmıyor**. `TenantId` ve
  `PrincipalRef`'i command'dan alıyor, yani trusted actor context yok. Evidence action adı
  `"Opportunity.Win"` (PDF'in `crm.opportunity.win` biçimiyle uyumsuz). Idempotency replay kontrolü
  her şeyden önce çalışıyor. Authorization eklendiğinde sıralama sorusu doğacak (bkz. §9).
- `Opportunity` üzerinde "Own" için kullanılabilecek tek alan `AssignedPrincipal` (issuer+subject).
  Owner / owning team / org unit alanı yok. Assignee'nin owner sayılıp sayılmayacağı CRM kararı.
- `Organization`, `TenantLifecycle`: `Class1.cs` placeholder'ları. `src/Modules/Sales/`: yalnız
  `bin/obj`, kaynak yok.

### 2.4 PDF ↔ repo çelişkileri

| PDF iddiası | Repo gerçeği |
|---|---|
| §2 "Access exists but not yet at same maturity" | Olgunluk farkı değil, **sıfır enforcement**: Application yok, Host yok, test yok, RLS yok, bir binding-core ihlali var |
| §20 pipeline'da "Tenant Boundary / RLS" adımı | Access tablolarında RLS yok. Host varsayılanı superuser, RLS bypass |
| §7 Own/Team ≠ Org/Territory, tek enum olmamalı | Repo tam da tek enum kullanıyor (`RoleAssignmentScopeType`) ve bu enum ayrıca cross-tenant `network` içeriyor |
| §29 "Reuse existing Role/Permission/TenantId/PrincipalRef" | Assignment `PrincipalRef` değil `account_id` kullanıyor. Role'ün tenant-safety'si çözülmemiş |
| §23 registry metadata (owner module, resource type, risk class) | `permissions` tablosunda yok |
| §24 "Access revision" | Repo bunu **"decision epoch"** adıyla, tasarlanmamış açık madde olarak zaten anıyor (`identity-access-schema.md` L112, doc 19 §9). Tek kavram olarak birleştirilmeli |
| §9 Approval ayrı bounded context | doc 08: pilotta **Sales** version-bound approval aggregate'ine sahip, **Human Tasks** görev, **Workflow/Rules** definition sahibi. PDF'in "Approval" BC'si bu satırlarla uzlaştırılmamış |

---

## 3. Benchmark Corrections

| Platform | PDF'te ne diyor | Düzeltme / nüans | Kaynak durumu |
|---|---|---|---|
| **Salesforce** | "Restriction Rules" record scope katmanında | Restriction Rules **Account, Contact, Opportunity, Case için yok**. Yalnız custom objects, external objects, quotes, contracts, events, tasks, timesheets için var. Grant zincirinde gerçekten çıkarım yapan mekanizma, **Permission Set Group içindeki muting permission set**. Bu, grup kapsamında composition-time bir çıkarma; runtime deny değil. Ayrıca **terminoloji tuzağı**: Salesforce'ta *Role* = veri görünürlüğü hiyerarşisi (organizasyonel), persona ise Profile/PSG. PDF'in "Role = persona, Role ≠ hierarchy" kararı Salesforce diliyle ters, dokümantasyonda açıkça not edilmeli. "View All / Modify All" object permission'ları sharing'i bypass eder. Bu, "functional permission record scope yaratmaz" kuralının bilinçli istisnası | Birincil (help.salesforce.com, Trailhead) |
| **Dynamics 365 / Dataverse** | Security Roles + privileges; record scope ayrı sütunda | Dataverse'te scope (User / BU / Parent:Child BU / Organization) **privilege'ın içindeki access level**. Yani scope, functional grant'ten ayrı bir katman değil, grant'in parçası. Grant'ler **kümülatif, en geniş olan kazanır, deny yok**. Hierarchy security (manager / position) role'den ayrı bir özellik. Column security profile'ları kullanıcının **zaten erişebildiği kayıtlara** uygulanır (field security, record authz'dan sonra gelir) | Birincil (learn.microsoft.com) |
| **SAP S/4HANA** | Field sütununda "Authorization restrictions" | Yanlış kolon. SAP restriction'ları **field security değil**, business role üzerindeki **org değeri kapsamı** (company code, plant, sales org…). Read / Write / Value Help ayrımıyla verilir. Bakımı yapılmamış restriction alanı = erişim yok (default deny). Business Role ≈ Role, Business Catalog ≈ PermissionSet, restriction ≈ **role üzerindeki** scope (assignment üzerinde değil). Flexible Workflow ve Responsibility Management approver belirleme için authz'dan ayrı | Birincil / yarı-birincil (SAP Learning, SAP KBA 2733842, SAP Community) |
| **Oracle Fusion** | Job Role → Duty Role → Privilege | Doğru. Ek: data access, **kullanıcı-rol atamasına bağlanan security context** (BU, ledger…) ile verilir ("Manage Data Access for Users"). Bu, önerilen "scope assignment üzerinde" modelinin en yakın benchmark'ı | Birincil (docs.oracle.com) |
| **ServiceNow** | Roles + ACL | Eksik: **Deny-Unless ACL'leri Allow-If'lerden önce değerlendirilir. Deny-Unless'ı geçmek erişim vermez, en az bir Allow-If gerekir.** Bu, PDF'in "restriction layer grant'ten ayrı" kararının doğrudan endüstri karşılığı. Kayıt işlemi için table ACL **ve** field ACL birlikte geçmeli. Liste güvenliği için query ACL / security data filter ayrı | Birincil + community |
| **HubSpot** | Own / Team / All | Doğru ve önemli: Own/Team/All **permission'ın üzerinde** seçiliyor (View: Everything / Team / Owned; Edit ayrı). Team/Owned, **owner property**'ye dayanır. Bu, "relation qualifier capability'nin yanında durur" önerisini destekliyor (§8) | Birincil (knowledge.hubspot.com) |
| **Zoho** | Profiles / roles, hierarchy + sharing + territories | Bu inceleme turunda birincil kaynaktan **doğrulanmadı**. Genel bilgiyle tutarlı ama karar dayanağı yapılmamalı | Doğrulanmadı |
| **NIST SP 800-162** | ABAC | Tanımlar genel olarak doğru. Bu turda metin çekilmedi. ABAC'ın subject / object / environment attribute + policy üçlüsü PDF §18 ile uyumlu | Doğrulanmadı (genel bilgi) |
| **Cedar / AVP** | permit/forbid | Semantik: **default deny; herhangi bir forbid true ise Deny; değilse herhangi bir permit true ise Allow; sıra bağımsız.** PDF'in "restriction layer after grant paths" sıralaması semantik için yanıltıcı. Kesişim sıra bağımsızdır, sıra yalnız performans ve açıklama için önemlidir | Birincil (docs.cedarpolicy.com) |
| **OpenFGA / Zanzibar** | Immutable models | Doğru: her model yazımı yeni immutable versiyon. Ek: conditions (CEL) ve contextual tuples ile sınırlı ABAC yapılabiliyor. **ListObjects, bilinmeyen per-object attribute'lar gerektiğinde (ör. her dokümanın status'u) kullanılamaz**, büyük liste filtreleme için tek başına çözüm değil. Zanzibar'ın "zookie" / new-enemy problemi, config revision ile cache tutarlılığı sorusunun karşılığı | Birincil (openfga.dev) |
| **OPA** | Filtering & decision logs | Doğru: Compile API partial evaluation ile residual AST döner, bu SQL'e çevrilebilir. Decision log'lar ilişkisel sync insert değil, toplu / async telemetry (#35'i destekler) | Birincil (openpolicyagent.org) |

**Çapraz sonuç (düzeltilmiş):** Liderler "ne yapabilir" ile "hangi kayıtlarda"yı ayırıyor, ama bu
ayrımı **farklı yerlere** koyuyorlar:

- Dataverse ve HubSpot: scope permission'ın içinde.
- Oracle: assignment üzerinde.
- SAP: role üzerinde.
- Salesforce: org-wide default + hiyerarşi + sharing.

PDF "hepsi ayırıyor" diyerek bu tasarım seçimini gizliyor. Bizim seçimimiz açıkça yazılmalı (§8).

---

## 4. Architecture Decisions Review

| # | Decision | Karar | Gerekçe |
|---|---|---|---|
| 1 | Tenant isolation ≠ authorization | **KEEP** | doc 08 / 19 ile tutarlı. RLS savunma derinliği, authz uygulama katmanında |
| 2 | Default deny | **KEEP** | Cedar ve SAP semantiği. Ek kural: evaluator hatası, bilinmeyen action ve eksik attribute **fail-closed** |
| 3 | Functional grants additive | **KEEP** | Dataverse kümülatif model. Birlik (union) semantiği önbelleklenebilir ve açıklanabilir |
| 4 | Restriction layer ayrı | **REVISE** | Ayrılık doğru (ServiceNow Deny-Unless, Cedar forbid). Semantik **forbid-overrides + sıra bağımsız** olarak yazılmalı. Restriction tipleri platform-owned kapalı küme. Phase 1.5'te **tenant tarafından yazılan forbid yok**. İleride PSG-muting benzeri composition-time çıkarma ayrı ele alınır |
| 5 | Permission = stable ActionKey | **REVISE** | Doğru, ama format (`<module>.<resource>.<verb>`), immutability (rename yok, deprecate var), resource_type bağı ve registry metadata freeze edilmeli. Repo'daki `"Opportunity.Win"` evidence adı bu vocabulary'ye bağlanmalı |
| 6 | PermissionSet first-class (Role → PS → Permission) | **KEEP** | SF PSG → PS, SAP Role → Catalog, Oracle Job → Duty ile birebir. **Kısıt:** PS yalnız action içerir. PS-in-PS ve role inheritance yok |
| 7 | Role = security persona, ≠ org hierarchy | **KEEP** | Salesforce isim çakışması dokümante edilmeli (§3) |
| 8 | Direct User → PS istisnai assignment | **REVISE** | Tüketicisi olmayan ikinci grant yolu. **Contract ve şema şekli freeze edilsin, Phase 1.5'te implement edilmesin.** JIT/delegation gerçek senaryoyla geldiğinde eklensin |
| 9 | Assignment first-class, effective-dated | **KEEP** | Zaten var. Eklenecekler: `valid_to > valid_from` CHECK, yarı-açık aralık, revoke = kapatma (silme yok), gelecekte başlayan atamanın değerlendirme anına göre çözülmesi |
| 10 | Own / Team / Sharing = ilişki modeli | **REVISE** | "Team" iki farklı şey: **organizasyonel takım üyeliği** (Organization fact) ve **kayıt takımı** (record team, sharing/domain). Aynı isim altında kalırsa karışır. Ayrıca bu ilişkilerin nerede şart koşulacağı tanımsız → §8 `relation` qualifier |
| 11 | Org / Territory = organizational scope, tek enum değil | **KEEP** (repo'yu REVISE) | Karar doğru. Repo'daki `RoleAssignmentScopeType` bunu ihlal ediyor ve cross-tenant `network`'ü de karıştırıyor |
| 12 | Sharing functional permission yaratmaz | **KEEP** | Salesforce / Dataverse ile aynı. İstisna yalnız "tenant scope'lu assignment" (View All benzeri) |
| 13 | Organization ayrı BC | **KEEP** | doc 08 zaten öyle diyor. "Position" HR ile sınır belirsiz (doc 08 §4 event-storming ister) |
| 14 | Territory ayrı BC | **REVISE** | Mantıksal olarak ayrı, ama **ticari (Sales-yakın) bir context**, platform çekirdeği değil. Access'e yalnız scope fact'i sağlar. Tasarımı Territory gerçek ihtiyacı gelene kadar bekleyebilir (owner memory: Territory ertelenmiş) |
| 15 | Sharing ayrı subdomain / evaluator | **REVISE** | **Access içinde subdomain** olmalı, ayrı BC değil. Share grant'i yetki verisidir, Access revision'ına bağlı yaşar, "kim paylaşabilir" de Access action'ıdır |
| 16 | Field Security ayrı evaluator | **REVISE** | Evaluator ve policy **Access içinde**. **Alan sınıflandırması (hangi alan hassas) domain'e ait** ve ActionKey gibi manifest'le bildirilir. Maskeleme icrası domain / API serialization'da |
| 17 | Field security tüm kanallarda | **KEEP** | Eklenecek kanallar: outbox event payload'ları, evidence `detail` (CRM evidence şu an tam payload JSON yazıyor), search index, analytics projection, log'lar |
| 18 | Approval ≠ Authorization | **KEEP** | |
| 19 | Approval assignment record access yaratmaz | **KEEP** | Approver, approval'ın sahip olduğu snapshot'ı görür, canlı kaydı değil |
| 20 | Instance immutable definition version'a pin | **KEEP** | doc 08 "version-bound approval" ile tutarlı |
| 21 | Approver resolver plugin modeli | **KEEP (design only)** | Oracle AMX approver tipleri ile uyumlu. Phase 1.5'te implement edilmez |
| 22 | Automation ayrı BC | **KEEP** | İsim doc 08 ile hizalanmalı: **Workflow/Rules** |
| 23 | Trigger / Condition / Action Automation'a ait | **KEEP** | |
| 24 | Automation / AI / worker bypass etmez | **KEEP** | Agent = kendi grant'i ∩ adına çalıştığı kullanıcının grant'i (doc 08 AI Gateway: "no inferred privileges"). Outbox dispatch gibi iş-dışı bakım işleri için ayrı, dar bir system principal allowlist'i |
| 25 | Principal + Action + Resource + Context | **REVISE** | Eksikler: (a) Tenant, ActorContext'ten gelir ve ResourceRef tenant'ı ile eşleşmek zorunda; (b) **create action'ları için type-only resource** (instance yok, parent/scope attribute var); (c) **resource attribute'larını PEP sağlar**, çünkü Access domain tablosu okuyamaz; (d) Context = request attribute'ları (auth strength, channel, on-behalf-of zinciri) |
| 26 | Full ABAC engine yok, contract ABAC-ready | **KEEP** | |
| 27 | OpenFGA yok, provider seam yeterli | **KEEP** | |
| 28 | Authorize ≠ ResolveAccessScope | **KEEP** | En değerli karar. Dönüş tipi freeze edilmeli (§10) |
| 29 | UI source of truth değil | **KEEP** | doc 08 global kural |
| 30 | RLS ilk aşamada yalnız tenant | **KEEP** | Ek: Host runtime bağlantısı unprivileged role olmadan RLS iddiası boş |
| 31 | Domain ActionKey sahibi | **KEEP** | Mekanizma freeze edilmeli: modül manifest'i → Contracts arayüzü → Host → Access doğrular (§8) |
| 32 | Business modules Contracts'a bağımlı | **KEEP** | Bugün Contracts'ta hiç authz tipi yok, Phase 1.5'in ilk işi bu |
| 33 | Configuration revision | **REVISE** | Tenant başına monoton sayaç ve repo'daki "decision epoch" ile **tek kavram**. Revision tek başına açıklama sağlamaz: before/after içeren config evidence ile birleşince geçmiş yeniden kurulabilir. Temporal config tabloları kurulmamalı |
| 34 | Config audit ≠ business evidence | **KEEP (nüanslı)** | Kavramsal olarak farklı, ama `AGENTS.md` authorization değişikliklerini risk-catalogued sayıyor. Yani **Access config audit = Access'in kendi evidence kaydı**, aynı mekanizmayla |
| 35 | Her read decision sync DB insert değil | **KEEP** | OPA decision log modeli. Telemetry'nin sahibi / yeri freeze edilmeli (§11), yoksa sahipsiz TODO kalır |
| 36 | Tenant ActionKey semantiğini değiştiremez | **KEEP** | Ek: tenant admin **kendinde olmayan bir yetkiyi veremez** (privilege escalation guard) |
| 37 | Tek generic rule engine yok | **KEEP** | doc 08 §5 "universal PolicyService" zaten "Bad" listesinde |
| 38 | Ortak ConditionExpression/AST ileride | **REVISE** | Kendi AST'ini icat etme. İleride gerekirse **CEL uyumlu, yan etkisiz, tipli** bir ifade alt kümesi (OpenFGA conditions ve Kubernetes aynı yolu kullanıyor). Phase 1.5'te hiç kurulmaz |

---

## 5. Missing Enterprise Capabilities

| Yetenek | Foundation'a etkisi | Bugün ne yapılmalı |
|---|---|---|
| **Cross-tenant network scope** (üretici → bayi) | Canlı gereksinim (doc 19). Tenant-içi scope modeline sığmaz | Assignment scope'undan **çıkar**. Ayrı "governed analytics access" grant'i olarak freeze et. OLTP authz'a girmez |
| **Principal type** (user / service / agent / group) | `account_id` yalnız insanı temsil ediyor | `principal_type` + `principal_id` freeze. 1.5'te yalnız `user` |
| **Service principal / API client** | Worker, outbox, entegrasyonlar | Identity'nin `BindWorkloadIdentity`'si (doc 08). Contract'ta yer açılır |
| **Agents (on-behalf-of)** | Etkin yetki = agent ∩ kullanıcı | ActorContext'te `ActingFor` zinciri freeze |
| **Groups** (IdP group) | Assignment principal'ı grup olabilir | `principal_type=group` rezerve. IdP group → role eşleme ileride `source=idp_sync` provenance ile |
| **Delegation** | Access delegation'ı (yetki devri) ≠ approval delegation'ı (görev devri) | İkisi ayrı sahiplikte. Access delegation'ı = delegator'ı aşamayan, süreli PS assignment |
| **Impersonation / support access** | Kimlik değiştirme değil. Actor zinciri + süre + gerekçe + evidence | ActorContext'e `ImpersonatedBy` alanı freeze. 1.5'te yok |
| **Break-glass** | Restriction layer'ı aşan tek meşru yol | Tasarım notu. Ayrı yüksek-risk evidence ve sonradan review |
| **JIT** | = effective-dated assignment + access request onayı | #8 ile aynı şema. Onay kısmı Workflow'da |
| **SoD** | Çakışan PS çiftleri. Önleyici (assignment anında) + tespit edici | PS first-class olduğu için SoD bu seviyede tanımlanır. Implement edilmez |
| **Access review / recertification** | Assignment'ta provenance, `granted_by`, `reason` gerekir | Bu alanlar **şimdi** eklenir (ucuz, sonradan backfill edilemez) |
| **Identity federation** | Issuer güveni, multi-IdP | `ExternalIdentity` zaten var. Tenant başına güvenilen issuer listesi Identity işi |
| **External / portal users** | Farklı principal realm'i, farklı membership türü | Phase 6 ertelendi. `principal_type` ve membership kind uzatılabilir kalmalı |
| **Consent** | Amaç temelli işleme. Authz değil | Feedback/Consent domain'i (Phase 8). Access'e girmez |
| **Entitlements / licensing** | Grant'ten önce çalışan ayrı kapı | doc 08 Entitlements "no user authorization". Pipeline'da yer ayrılır |
| **Step-up auth** | Decision obligation'ı | `Obligations` alanı freeze, akış yok |
| **Privilege escalation guard** | Tenant admin'in kendine / başkasına fazla yetki vermesi | 1.5'te **implement edilir**: `access.*` action'ları + "yalnız sahip olduğun PS'i verebilirsin" |
| **Platform operator erişimi** | Tenant dışından destek personeli | Tasarım notu. Tenant içi assignment ile karıştırılmaz |
| **Revocation latency** | Cache varsa revoke ne kadar sürede etkili olur | SLA kararı (§13) |

---

## 6. Domain Boundary Review

| Context | Karar | Not |
|---|---|---|
| **Identity** | PDF'te **eksik**. Ayrı mantıksal context (doc 08). Pilotta Access ile aynı assembly | Principal çözümleme, membership, workload identity binding, impersonation kaydı |
| **Access** | KEEP, genişletilmiş | Action registry (projeksiyon), PermissionSet, Role, Assignment, PDP, query scope, restriction evaluation, **Sharing subdomain**, **Field Security subdomain**, decision explanation, config revision/evidence |
| **Organization** | KEEP (ayrı BC) | Legal entity, BU, department, branch, org team, membership, çoklu hiyerarşi. Access'e `ResolveScopeAt` / `IsWithin(node, ancestor, hierarchyRef, at)` fact contract'ı sağlar. Position ↔ HR sınırı açık |
| **Territory** | REVISE: ayrı ama ticari context | Sales/CRM operasyonlarına yakın. Access'e "principal X, resource Y'nin territory'sinde" fact'i sağlar. Kendi versioning'i var |
| **Sharing** | REVISE: Access subdomain'i | Record grant + provenance. Kural tabanlı share'ler (source=rule/team/territory) türetilmiş satırlardır, kaynağı başka context olabilir |
| **Field Security** | REVISE: Access subdomain'i | Policy Access'te, sınıflandırma domain manifest'inde, icra PEP / serialization'da |
| **Approval** | REVISE: doc 08 ile uzlaştırılmalı | Onay invariant'ı (QuoteVersion approved) **owning domain**'de (Sales). Definition / version / resolver **Workflow/Rules**'da (ya da ondan ayrılmış bir Approval capability'sinde). Görev **Human Tasks**'ta. PDF'in tek "Approval" kutusu bu üç sahipliği birleştiriyor. **Owner kararı gerekli** |
| **Automation** | KEEP, ad: Workflow/Rules | Kendi service principal'ıyla domain command'ı çağırır |
| **Entitlements** | PDF'te **eksik** | Ayrı (doc 08). Pipeline'da grant'ten önce |
| **Tenant Network** | PDF'te **eksik** | Sahipliği açık (doc 19 §7). Access yalnız tüketir |

Kural: Access yalnız **fact tüketir**. Organization, Territory ve domain modülleri fact
**sağlayıcıdır**. Hiçbir sağlayıcı Access'in tablosunu okumaz, Access de onlarınkini okumaz.

---

## 7. Target Conceptual Model (düzeltilmiş)

```text
IDENTITY                          ACCESS (control plane)
─────────                         ─────────────────────────────────────────────
Account ─┬─ ExternalIdentity      ActionRegistry (code manifest → DB projection)
         └─ TenantMembership        ActionKey  crm.opportunity.win
Principal{type,id}                  owner_module, resource_type, risk_class, status
  user | service | agent | group
                                  PermissionSet (tenant row; origin: system|tenant)
ActorContext (trusted, per req)     └─ PermissionSetItem
  tenant, principal, authStrength,        action_key
  actingFor?, impersonatedBy?             relation?  (null=any in scope | owner | ...)
                                  Role (persona; tenant row; origin: system|tenant)
                                    └─ RolePermissionSet
                                  Assignment (effective-dated, provenance)
                                    principal ── role
                                    scope: Tenant | OrgNode(ref, hierarchyRef)
                                  [later] PermissionSetAssignment (same shape)
                                  [later] Restriction (platform-owned types, forbid)
                                  [later] RecordShare (subdomain)
                                  [later] FieldPolicy (subdomain)
                                  TenantAccessRevision (monotonic = decision epoch)
                                  AccessEvidence + Outbox + Idempotency

  PDP:  Authorize(ActorContext, ActionKey, ResourceDescriptor, RequestContext)
          → AuthorizationDecision{effect, reason, decisionId, revision,
                                  matchedGrants, matchedRestrictions, obligations}
        ResolveAccessScope(ActorContext, ActionKey, ResourceType)
          → AccessScope (closed filter AST)  → domain adapter → SQL
  Fact providers (Contracts seams):
        IOrganizationScopeFacts   ITerritoryFacts   IRelationFacts(owner/team)

DOMAIN (CRM, Sales…)              OTHER CONTEXTS
  owns ActionKeys + field class     Organization, Territory(commercial),
  PEP in command handler            Workflow/Rules(+approval defs),
  business policy / invariants      Human Tasks, Entitlements, Tenant Network
  approval invariant (Sales)
```

Scope boyutları (üç eksen, üç ayrı yer):

| Eksen | Soru | Nerede |
|---|---|---|
| Capability | Ne yapabilir? | PermissionSetItem.action_key |
| Organizational scope | Hangi org düğümünde? | Assignment.scope |
| Relationship qualifier | Hangi ilişkideki kayıtlarda? | PermissionSetItem.relation |
| Explicit share | İstisnai kayıt erişimi | RecordShare (sonra) |
| Restriction | Hangi koşulda asla? | Restriction (sonra) |

Bu seçim, Oracle'ın (scope assignment'ta) ve HubSpot/Dataverse'in (depth capability'de) sentezidir.
"Sales Manager — İstanbul BU" tek role ile, farklı atamalarla ifade edilir. "Rep yalnız kendi
kaydı" ise PS tanımında durur.

---

## 8. Target Data Model (Phase 1.5 minimum)

Genel kurallar: her tenant tablosunda `tenant_id NOT NULL`, composite FK, RLS ENABLE+FORCE,
unprivileged role testi, `row_version NOT NULL DEFAULT 1`, soft delete yok (status).

### 8.1 System vs tenant katalog: öneri (owner kararı)

Açık karar "system role'lerin `tenant_id = NULL` olması tenant-safe FK'yi engelliyor" için önerim
**copy-on-provision**: platform-tanımlı role ve PermissionSet'ler kod manifest'inde durur, tenant
provisioning'de ve platform upgrade'inde **her tenant'a `origin='system'`, `system_key` ile
materyalize edilir**. Tenant bu satırları düzenleyemez, ancak klonlayabilir. Böylece:

- `tenant_id` her yerde NOT NULL olur, composite FK ve RLS tek tip çalışır;
- Dataverse (ortam başına predefined roller) ve Salesforce (org başına standart profiller) ile uyumludur;
- bedeli bir **reconciler** (upgrade'de system satırlarını senkronlayan idempotent iş) ve
  "system satırı değiştirilemez" invariant'ıdır.

Alternatifler (nullable tenant + RLS `OR tenant_id IS NULL`, ya da ayrı platform tabloları +
polimorfik referans) composite FK'yi kaybettiriyor. Bu karar owner memory'de açık olarak
işaretli, **owner onayı olmadan kapatılmaz**.

### 8.2 Tablolar

| # | Tablo | Kolonlar (özet) | Neden şimdi |
|---|---|---|---|
| 1 | `access.actions` (mevcut `permissions`'ın yerine) | `action_key text PK`, `owner_module`, `resource_type`, `verb`, `risk_class`, `status (active|deprecated)`, `description` — tenant yok, runtime role'e yalnız SELECT | PS item'larının FK hedefi. **Kaynak = kod manifest'i**, tablo = startup/migration projeksiyonu. Surrogate `bigint` yerine doğal anahtar (on-prem kurulumlar arası id kayması olmaz). CI: kodda kullanılan her ActionKey registry'de olmalı |
| 2 | `access.permission_sets` | `tenant_id, id, key, name, origin (system|tenant), system_key?, status, row_version`; UNIQUE `(tenant_id, key)`; CHECK origin/system_key tutarlılığı | First-class capability bundle |
| 3 | `access.permission_set_items` | `tenant_id, permission_set_id, action_key, relation?`; PK `(tenant_id, permission_set_id, action_key)`; FK `(tenant_id, permission_set_id)`; FK `action_key → actions`; CHECK `relation IN ('owner')` (1.5'te kapalı küme) | Capability + relationship qualifier. `relation` 1.5'te yalnız `NULL` ve `owner`, sonraki değerler (`team_member`, `shared`) additive CHECK genişlemesi |
| 4 | `access.roles` (revize) | `tenant_id NOT NULL, id, key, name, origin, system_key?, status, row_version`; UNIQUE `(tenant_id, key)`, UNIQUE `(tenant_id, name)`; `UNIQUE (tenant_id, id)` (composite FK hedefi) | Persona |
| 5 | `access.role_permission_sets` (mevcut `role_permissions`'ın yerine) | `tenant_id, role_id, permission_set_id`; iki composite FK | Role → PS |
| 6 | `access.role_assignments` (revize) | `tenant_id, id, principal_type (user), principal_id, role_id, scope_kind (tenant|org_node), scope_ref?, hierarchy_ref?, valid_from, valid_to?, source (manual|system|provisioning), granted_by_principal_id, reason?, row_version`; FK `(tenant_id, role_id)`; CHECK `valid_to IS NULL OR valid_to > valid_from`; CHECK `scope_kind='tenant' ⇔ scope_ref IS NULL` | Tek assignment = tek scope. Çoklu BU = çoklu satır, bu yüzden **ayrı `scope_binding` tablosu gerekmiyor**. `network` bu tablodan **çıkarılır**. `org_node` 1.5'te şemada rezerve, Organization fact provider'ı gelene kadar evaluator bunu **deny** eder (fail-closed) |
| 7 | `access.tenant_access_state` | `tenant_id PK, revision bigint NOT NULL DEFAULT 0, row_version` | Her config değişikliği aynı transaction'da `revision+1`. Decision epoch + cache anahtarı + eşzamanlı config değişikliklerinin serileştirilmesi |
| 8 | `access.evidence_records` | CRM kalıbı + `revision_before/after`, `before/after` JSON | `AGENTS.md`: authorization değişiklikleri risk-catalogued. Config audit = bu tablo |
| 9 | `access.outbox_messages` | CRM kalıbı | doc 08: `AccessPolicyChanged/Revoked` yayınlanır. Worker/diğer süreçlerdeki cache invalidation için gerekli. Binding core #3 |
| 10 | `access.idempotency_records` | CRM kalıbı | Grant/Revoke state-changing command, binding core #4 |

`identity.*` tabloları: yalnız RLS (`tenant_memberships`), `row_version DEFAULT 1` ve Host kaydı.
Şekil değişmez. `principal_id`'nin `identity` tarafındaki karşılığı `accounts.id`'dir. Cross-schema
FK yok (mevcut karar korunur).

### 8.3 Özel soruların cevapları

| Soru | Cevap |
|---|---|
| Ayrı `scope_binding`? | Hayır (1.5). Assignment başına tek scope. Çoklu scope çoklu satırdır |
| Tek generic assignment aggregate? | Kavramsal olarak evet (`Assignment<TGrantable>`), fiziksel olarak **iki kardeş tablo** (role / PS). Nullable-FK'li polimorfik tablo composite FK'yi zayıflatır. 1.5'te yalnız role tablosu |
| Principal type? | `principal_type` + `principal_id`. PrincipalRef (issuer, subject) Identity'de çözülür, Access dahili id ile çalışır. Kapalı küme: `user|service|agent|group`, 1.5'te `user` |
| ActionKey registry DB mi kod mu? | **Kod manifest'i sahiptir**. DB salt-okunur projeksiyondur. Tenant yazamaz |
| System vs tenant PS ayrımı? | Evet, `origin` kolonu + copy-on-provision (§8.1) |
| Role inheritance? | **Hayır.** Yeniden kullanım PS ile sağlanır. Role hiyerarşisi org hiyerarşisiyle karıştırılma riskini geri getirir |
| Explicit deny? | Grant modelinde **hiçbir zaman**. Ayrı restriction katmanında, platform-owned tiplerle, sonraki fazda |
| Precedence / conflict? | Grant'ler arası çakışma yok (union). Restriction varsa forbid kazanır. Açıklama için tüm eşleşen grant'ler döner |
| Effective dates? | Evet (zaten var). Değerlendirme "şimdi"ye göre, cache anahtarında en yakın `valid_from/valid_to` sınırı TTL'yi keser |
| Revision stratejisi? | Tenant başına tek monoton sayaç (§8.2 #7). Global değil, satır başına değil |
| Cache / revocation? | **Tasarımı şimdi freeze et, implement etme**: request-scope etkin grant cache'i (revision anahtarlı). Süreçler arası cache yalnız outbox invalidation ile. İlk sürümde süreçler arası cache yok, bu yüzden revocation anında etkili |

**Kurulmayacaklar (1.5):** `permission_set_assignments`, `scope_bindings`, `restrictions`,
`record_shares`, `field_policies`, `decision_logs`, `delegations`, `sod_rules`,
territory/org tabloları.

---

## 9. Authorization Evaluation Pipeline (final)

```text
 0  Authentication (OIDC/BFF) → Trusted ActorContext
      tenant = session'dan (command gövdesinden DEĞİL), principal{type,id}, authStrength,
      actingFor?, impersonatedBy?
 1  Tenant gate: membership Active (Identity) ∧ tenant not suspended (TenantLifecycle)
 2  Entitlement gate: modül/özellik tenant için lisanslı mı (Entitlements) — [sonra]
      (feature flag burada DEĞİL: flag yetki değildir — doc 08 Release satırı)
 3  Action registry: ActionKey kayıtlı ve active mi? değilse → Deny (+ hata telemetrisi)
 4  PEP resource yükler (aynı transaction, RLS aktif) → ResourceDescriptor
      {type, id?, tenant, owner?, orgNode?, parent?, attributes{stage,amount,...}}
      create için id yok, parent/scope attribute var
 5  PDP kararı — algebra, sıra değil:
      Permit ⇔  ∃ grant g ∈ Union(assignments aktif @now → roles → PS → items[action])
                 : InScope(g.assignment.scope, resource)
                 ∧ RelationHolds(g.item.relation, principal, resource)
                ∧ ¬∃ restriction r : r.applies ∧ r.forbids        [r: sonra]
      Fact provider hatası / eksik attribute → Deny (fail-closed)
 6  Decision = {effect, reasonCode, decisionId, revision, matchedGrants,
                matchedRestrictions, obligations[stepUp?, fieldMask?, evidenceRequired?]}
      obligation karşılanamıyorsa → Deny
 7  Field security
      write: değişen alanlar için alan-yazma kontrolü → mutasyondan ÖNCE
      read : satır yetkisinden SONRA projeksiyon/maskeleme (Dataverse column security)
 8  Domain business policy (CRM/Sales) — state-machine yasallığı, stage geçişi,
      tenant süreç kuralları
 9  Approval requirement — [sonra] yetkili submit edilir; onay gerekirse komut
      "pending approval" üretir, etki onaydan sonra
10  Domain invariants → execute → state + outbox + evidence(decisionId, revision)
      + idempotency, tek SaveChanges
```

Sorulan sorular:

- **Field security önce mi sonra mı?** Sonra. Write'ta mutasyondan önce, read'de satır
  filtresinden sonra.
- **Approval önce mi sonra mı?** Sonra. Önce "submit / approve etme yetkisi var mı" (Access),
  sonra "bu işlem onay gerektiriyor mu ve onaylandı mı" (domain / approval).
- **Domain state authz context'inin parçası olabilir mi?** Evet, **attribute olarak** (PEP
  sağlar; ör. `stage`, `amount`, `status`). Ama durum geçişinin yasallığı Access'e ait değildir.
- **Business vs security policy sınırı:** Security policy = "bu principal bu kaynak üzerinde bu
  action'ı yapmaya yetkili mi" (kim / nerede / hangi koşulda). Business policy = "bu işlem bu
  durumda geçerli mi" (ne zaman / hangi sırayla / hangi değerlerle). Test: kural principal'a
  bakmıyorsa business policy'dir.
- **Stage capability (CRM pipeline restrictions):** "Won'a geçiş" yetkisi Access'te bir ActionKey
  (`crm.opportunity.win`). "Negotiation'dan Won'a geçilebilir mi", "Won için zorunlu alanlar"
  CRM'e aittir. Stage başına yetki gerekiyorsa CRM ya ayrı action tanımlar ya da `stage`'i
  attribute olarak verir ve ileride restriction ile yazılır. Stage kuralları Access tablosuna
  girmez.
- **Step-up / MFA:** Decision `obligation`'ı. PEP karşılanmamış obligation'da reddeder.
  1.5'te alan freeze edilir, akış yok.
- **Entitlement:** Adım 2, grant'ten önce. Ayrı modül, ayrı contract.
- **Feature flag:** Pipeline'ın parçası değil. Flag kapalıysa action registry'de "unavailable"
  gibi davranabilir ama yetki kaynağı olamaz.
- **Service principal / agent:** Aynı pipeline. Service principal kendi assignment'larıyla.
  Agent için etkin yetki = agent grant'leri ∩ `actingFor` kullanıcının grant'leri, ve
  `risk_class=high` action'larda insan onayı obligation'ı (doc 08 AI Gateway).
- **Idempotency replay sırası (repo bulgusu):** `CompleteOpportunityHandler` replay'i
  authorization'dan önce döndürüyor. Replay anahtarı principal'ı içerdiği için sızıntı dar, ama
  **yetkisi geri alınmış principal eski sonucu görmeye devam eder.** Karar: replay'den önce en azından
  tenant gate + action authorize (resource'suz ön kontrol) çalışmalı. Bu, freeze edilecek PEP
  kalıbının parçası.

---

## 10. Query Authorization Strategy

### 10.1 Seçenekler

| Seçenek | Artı | Eksi | Karar |
|---|---|---|---|
| Authorized query specification (domain'e özgü spec) | Basit | Her domain yetki mantığını kopyalar | Tek başına hayır |
| Policy → SQL translation (OPA Compile tarzı) | Tek kaynak, doğru sayfalama | Genel policy dili gerekir. Access domain kolonlarını bilemez (doc 08) | **Mini sürümü evet** (kapalı AST) |
| Security projection (materialized ACL tablosu) | Hızlı liste | Senkronizasyon, revocation gecikmesi | Search / Analytics için sonra |
| Precomputed ACL / security token | Search motorları için standart | Yazma yükü, patlama | Search modülü geldiğinde |
| Relation graph query (ListObjects) | ReBAC doğal | Büyük listede ve per-object attribute'ta zayıf (OpenFGA dokümanı) | Hayır |
| Domain-local authorization query adapter | Access domain tablosu okumaz, domain SQL'i kendisi yazar | Adapter test disiplini gerekir | **Evet** |

### 10.2 Öneri: residual AST + domain adapter

```text
ResolveAccessScope(actor, actionKey, resourceType) → AccessScope

AccessScope := None
             | All                              // tenant içinde tamamı (RLS zaten tenant'ı keser)
             | AnyOf(ScopeTerm[])               // birleşim
ScopeTerm   := OwnedBy(principalIds[])
             | InOrgNodes(nodeRefs[], hierarchyRef)      [sonra]
             | InTerritories(territoryRefs[], modelVer)  [sonra]
             | SharedWith(principalIds[])                [sonra]
             | Unsupported(term)                          // adapter bunu → deny ele alır
  + revision, decisionId
```

- Access, aynı evaluator'ı "resource bilinmiyor" moduyla çalıştırıp bu **kapalı cümleyi** üretir
  (OPA partial evaluation'ın küçük, tipli karşılığı).
- CRM, `IAccessScopeTranslator<Opportunity>` ile her terimi kendi kolonlarına çevirir
  (`OwnedBy → assigned_principal_* IN (...)`). **Tanımadığı terimle karşılaşırsa hiçbir satır
  döndürmez** (fail-closed). Böylece yeni terim eklemek eski adapter'ları sessizce açmaz.
- Filtre **SQL'de** uygulanır. Sayfalama ve toplam sayısı doğru kalır. Post-filter yasak.
- Sayfadaki satırlar için "authorizedActions" gerekiyorsa: **in-process batch** `Authorize`
  (tek request cache'i, revision anahtarlı). Satır başına remote çağrı yok.
- **Bugün freeze edilecek arayüzler:** `ResolveAccessScope` imzası, `AccessScope` AST'nin kök
  şekli ve genişleme kuralı (yeni terim = yeni case, adapter bilmiyorsa deny), translator
  arayüzü, contract testi (aynı fixture için `Authorize(row)` ile `row ∈ ResolveAccessScope`
  eşdeğerliği). **Bu eşdeğerlik testi, iki yolun ayrışmasını önleyen tek gerçek güvence.**
- **1.5'te implement:** `None`, `All`, `OwnedBy`. Yalnız `All` ile test edilen bir scope contract
  hiçbir şey kanıtlamaz (RLS zaten tenant'ı süzüyor). `OwnedBy`, seam'in gerçek olduğunu gösteren
  asgari terimdir.

---

## 11. Versioning / Audit / Evidence

| Kavram | Sahip | Mekanizma | 1.5 |
|---|---|---|---|
| Access config revision (= decision epoch) | Access | `tenant_access_state.revision`, config değişikliğiyle aynı transaction | Implement |
| Access config audit | Access | `access.evidence_records` (before/after, revision, actor, reason) | Implement |
| Access change fact | Access | Outbox `access.policy.changed.v1` / `access.assignment.revoked.v1` | Implement |
| Authorization decision telemetry | Access (emit) / Operations (store) | Yapılandırılmış log / OTel event: decisionId, actor, action, resource, effect, reason, revision. Örnekleme + saklama kuralı. **DB'ye sync yazılmaz** | Implement (yalnız emit) |
| High-risk decision evidence | Domain | Domain evidence kaydına `decision_id` + `revision` eklenir (CRM `evidence_records`) | Implement (CRM'de alan ekleme, Phase 2 ile) |
| Business evidence | Domain | Mevcut CRM / MasterData kalıbı | Değişmez |
| Approval versioning | Approval sahibi (§6) | Instance → immutable definition version | Design only |
| Action registry versioning | Kod | Git + deprecate (rename yok) | Implement (CI kontrolü) |
| Explain | Access | Decision alanları + revision + config evidence → "o anda neden" yeniden kurulabilir | Contract implement, UI yok |

Doğrulama: PDF'in ayrımları doğru. Eksik olan, **telemetry'nin nereye gittiği** ve
**config audit'in `AGENTS.md` açısından evidence sayılması**. İkisi de yukarıda bağlandı.
Sınır: bir karar yüksek risk ise kanıtı *domain* yazar (işlemle aynı transaction), Access yalnız
`decisionId`'yi sağlar.

---

## 12. Phase 1.5 Real Scope

### IMPLEMENT NOW

**A. Eski baseline (zorunlu, binding core + owner §5.D):**

1. `tests/Access.Tests` (Domain / Architecture / Integration, Testcontainers).
2. Access RLS migration (hand-written istisna kalıbı) + unprivileged role testleri + `create-runtime-role.sql` access/identity grant bloğu.
3. Tenant-safe composite FK'ler (roles, PS, items, assignments) + `(tenant_id, key/name)` uniqueness.
4. `row_version DEFAULT 1`, `valid_to > valid_from` CHECK.
5. `AccessDbContext` Host kaydı (runtime bağlantısı unprivileged role ile).
6. Grant/Revoke command'ları: idempotency + outbox + evidence, tek SaveChanges.
7. NetArchTest: Access yalnız Contracts'a bağımlı; CRM/MasterData Access'e bağımlı değil (mevcut test genişler).

**B. Control-plane çekirdeği:**

8. Contracts: `ActionKey`, `PrincipalId{type,id}`, `ActorContext`, `ResourceDescriptor`, `AuthorizationRequest`, `AuthorizationDecision` (obligations dahil), `IAuthorizer`, `AccessScope` AST, `IAccessScopeResolver`, `IActionCatalog`.
9. Action registry: modül manifest'i (CRM `crm.opportunity.*`) → Host → Access projeksiyonu. CI: kayıtsız / çift ActionKey'e fail.
10. PermissionSet, PS items (`relation`: NULL | owner), Role (origin), Role → PS, Role assignment (principal_type=user, scope_kind=tenant; org_node rezerve + deny).
11. System katalog materializasyonu + reconciler (§8.1, **owner onayına bağlı**).
12. PDP: RBAC + tenant scope + owner relation, default deny, fail-closed, request-scope cache.
13. `ResolveAccessScope`: None / All / OwnedBy + eşdeğerlik contract testi.
14. PEP kalıbı: trusted ActorContext, replay-öncesi ön kontrol, resource yükle → authorize → field (yalnız kalıp) → invariants.
15. Tenant access revision (+ decision'da dönmesi).
16. Decision telemetry emit (log/OTel), DB yok.
17. Privilege escalation guard (`access.*` action'ları, "sahip olmadığını veremezsin").
18. Mevcut `permissions` / `role_permissions` / `RoleAssignmentScopeType.Network` sökümü (tüketici yok).

### DESIGN / FREEZE NOW, IMPLEMENT LATER

- Direct PS assignment (şema şekli = role assignment kardeşi) + JIT + Access delegation.
- Org-node scope evaluation (`IOrganizationScopeFacts`, hierarchyRef, as-of time).
- Team relation (org team ve record team ayrımıyla).
- Restriction layer semantiği (forbid-overrides, platform-owned tipler).
- Sharing subdomain (`RecordShare` + provenance + `SharedWith` terimi).
- Field security (domain alan sınıflandırma manifest'i, write/read noktaları, mask obligation'ı).
- Principal type'lar: service / agent / group; ActorContext `actingFor` / `impersonatedBy`.
- Step-up obligation akışı; entitlement gate arayüzü.
- Cross-process cache + outbox invalidation, revocation SLA.
- Cross-tenant network: governed analytics grant'i (OLTP dışı).
- Approval: sahiplik uzlaştırması (Sales / Workflow / Human Tasks), definition version pin, resolver arayüzü.
- SoD (PS düzeyinde), access review alanları (şimdiden provenance ile beslenir).

### DO NOT BUILD YET

Generic rule engine; kendi AST / DSL'i; OPA / OpenFGA / Cedar runtime; territory tabloları /
engine; org tabloları (Organization modülü ayrı faz); record share tabloları; field policy
tabloları; restriction tabloları; approval runtime / designer / resolver'lar; delegation /
substitution / escalation / absence / quorum / parallel approval; SoD engine; decision log
tablosu; admin UI'ları; IdP group sync; impersonation; break-glass; portal user realm;
cross-process cache.

---

## 13. Freeze Decisions (owner onayı gerekli)

1. **Scope yerleşimi:** org scope assignment'ta, relation qualifier PS item'da, share ayrı (§7).
2. **System katalog:** copy-on-provision (`origin`, `system_key`, reconciler) mı, alternatif mi? (açık karar kapanır)
3. **Principal kimliği:** Access `principal_type + principal_id` (Identity dahili id) ile çalışır. PrincipalRef sınırda çözülür.
4. **`network` scope'u** role assignment'tan çıkarılır, governed analytics grant'i olarak ayrı ele alınır.
5. **ActionKey:** format `<module>.<resource>.<verb>`, kod manifest'i sahip, rename yok, deprecate var, DB projeksiyon.
6. **Authorize contract:** ActorContext (tenant session'dan) + ActionKey + ResourceDescriptor (PEP sağlar; create için type-only) + RequestContext → Decision (effect, reason, decisionId, revision, matched*, obligations).
7. **AccessScope AST** kök şekli ve "bilinmeyen terim = deny" kuralı + eşdeğerlik testi.
8. **Grant semantiği:** union. Restriction semantiği: forbid-overrides, sıra bağımsız, platform-owned. Grant modelinde deny asla.
9. **Role inheritance yok, PS nesting yok.**
10. **Direct PS assignment 1.5'te yok** (şema şekli freeze).
11. **Revision = decision epoch**, tenant başına monoton.
12. **Config audit = Access evidence.** Decision telemetry DB'ye yazılmaz. High-risk decision kanıtı domain evidence'ında `decisionId` ile.
13. **PEP sırası:** tenant gate → (replay öncesi) ön yetki → resource yükle → authorize → field write → business policy → invariants → commit.
14. **Sharing ve Field Security Access subdomain'i.** Alan sınıflandırması domain'de.
15. **Approval sahipliği** (Sales invariant / Workflow definitions / Human Tasks). PDF'in tek Approval BC'si doc 08 ile uzlaştırılacak.
16. **Territory** ticari context. Access'e yalnız fact.
17. **Revocation SLA:** 1.5'te anında (süreçler arası cache yok). Cache eklendiğinde üst sınır ne olacak?
18. **Owner semantiği (CRM):** `AssignedPrincipal` = owner mı? (CRM kararı, `OwnedBy` buna bağlı)
19. **Agent yetki kuralı:** kesişim + high-risk için insan obligation'ı.

---

## 14. YAGNI / Overengineering Risks

1. **18 satırlık IMPLEMENT NOW**, testi ve Host kaydı olmayan bir modülde. Baseline'ı geciktirir, Phase 2'yi kilitli tutar.
2. **Direct PS assignment**: tüketicisi olmadan ikinci grant yolu. Test ve açıklama yüzeyi ikiye katlanır.
3. **Authorized-actions discovery API**: UI yokken HTTP yüzeyi. Contract (batch authorize) yeterli.
4. **Org / territory evaluator'larını fact provider'ı olmadan yazmak**: sahte veriyle test edilen kod.
5. **Kendi ConditionExpression AST'i**: ihtiyaç doğmadan dil tasarımı. CEL varken tekerleği yeniden icat etmek.
6. **Decision log tablosu**: her okumada sync insert, yazma yükü, saklama problemi.
7. **Generic `Assignment` polimorfik tablosu**: composite FK'yi zayıflatır, tek kazancı estetik.
8. **Scope binding tablosu**: çoklu satırın zaten çözdüğü problem.
9. **System katalog için karmaşık versiyonlama** (katalog versiyonu, tenant pinning): reconciler + deprecate yeterli.
10. **Tenant-authored restriction / forbid**: yanlış yapılandırmayla kilitlenen tenant'lar, destek yükü.
11. **Role inheritance**: PS'in çözdüğü problemi ikinci kez çözmek.
12. **Cross-process authorization cache**: tek süreçte gereksiz, revocation hatası kaynağı.
13. **Approval resolver'larını "plugin" altyapısıyla kurmak**: tek resolver bile yokken extension point.

---

## 15. Migration Risks

1. **`permissions` → `actions` (bigint id → text key)**: tüketici yok, ama seed verisi varsa yeniden yüklenmeli. Expand/contract: yeni tablo → kopya → eski tabloyu bırak.
2. **`roles.tenant_id` NULL → NOT NULL**: mevcut system rol satırları her tenant'a çoğaltılmalı. Yerel DB'de veri yoksa risk sıfır, yine de migration iki adım olmalı.
3. **`role_permissions` → `role_permission_sets`**: her mevcut rol için bir "legacy" PS üretip bağlamak gerekir (veri varsa).
4. **`account_id` → `principal_type + principal_id`**: kolon adı değişikliği generated migration'da drop+add olarak çıkabilir. Rename'in EF tarafından doğru algılandığı kontrol edilmeli.
5. **`scope_type='network'` satırları** varsa taşınacak hedef henüz yok. Önce veri olmadığı doğrulanmalı.
6. **RLS eklenince Host / test bağlantısı superuser kalırsa** testler yeşil ama koruma yok. FF03 unprivileged-role testi zorunlu.
7. **Composite FK için `UNIQUE (tenant_id, id)`** hedef index'leri önce eklenmeli. Sıralama hatası migration'ı kırar.
8. **Reconciler'ın idempotent olmaması**: upgrade'de system PS'leri çoğaltır.
9. **CRM evidence action adları** (`"Opportunity.Win"`) ile ActionKey (`crm.opportunity.win`) arasında eşleme: geçmiş evidence değiştirilemez, yalnız ileriye dönük hizalama ve eşleme tablosu (dokümantasyon) gerekir.
10. **Tek assembly'deki Identity + Access**: RLS policy'leri iki schema'yı kapsamalı. Migration history `access` schema'sında, bu yüzden identity RLS de aynı migration zincirinde.
11. **Phase 2 ile paralel yürüme**: CRM PEP kalıbı contract freeze edilmeden yazılırsa yeniden yazılır. 1.5 contract görevi Phase 2'den önce kapanmalı.

---

## 16. "How This Architecture Could Fail"

1. **Scope ve query yolları ayrışır:** `Authorize` bir satırı reddederken listede görünür (ya da tersi). Eşdeğerlik testi yoksa kaçınılmaz.
2. **Fact provider gecikmesi:** Organization hiyerarşisi değişir, Access eski fact'le karar verir. `as-of` ve `HierarchyChanged` invalidation tasarlanmazsa yanlış erişim oluşur.
3. **Sistem katalog drift'i:** Platform yeni action ekler, reconciler bazı tenant'larda çalışmaz, özellik bir tenant'ta sessizce kilitli kalır.
4. **Relation qualifier yetmez:** "Rep kendi ve kendi takımındaki alt kademe kayıtları" gibi hiyerarşik ownership ihtiyacı gelir. `relation` kapalı kümesi ile org scope'un kesişimi ifade edilemezse ikinci bir scope dili doğar.
5. **Resource attribute'larını PEP eksik sağlar:** Restriction'lar geldiğinde her handler'ın descriptor'ı güncellenmeli. Unutulan attribute fail-closed ise işlev kırılır, fail-open ise güvenlik açığı olur.
6. **Revision hot-row darboğazı:** Büyük tenant'ta toplu atama (IdP sync) tek satırı kilitler, config işlemleri serileşir.
7. **Agent / automation için "system" kısayolu:** Bir ekip Worker'da `SystemPrincipal`'ı her şeyi yapabilen kimlik olarak kullanır, bypass geri gelir.
8. **Field security bir kanalda unutulur:** Outbox payload'ı veya evidence detail'i hassas alanı taşır, Analytics projeksiyonu maskesiz veri yayar.
9. **Approval sahipliği bölünür:** Sales kendi approval'ını, Workflow ikinci bir approval'ı yazar. doc 08'in "iki aktif görev otoritesi olmasın" kuralı ihlal edilir.
10. **Network erişimi OLTP'ye sızar:** Üretici panosu "hızlı çözüm" diye bayi tenant tablosunu sorgular. Tenant-per-dealer kararı fiilen delinir.
11. **Idempotency replay yetkiyi aşar:** Yetkisi alınmış kullanıcı eski anahtarla başarılı cevap almaya devam eder.
12. **Salesforce terminolojisiyle gelen danışman / müşteri** "Role hierarchy" bekler, ekip Role'e parent ekler ve persona / hiyerarşi karışır.
13. **Tenant admin kendine yetki yükseltir:** Escalation guard yoksa `access.*` yetkisi fiilen "her şey" yetkisidir.
14. **Search / Analytics kendi ACL kopyasını üretir,** revocation fact'lerini tüketmez, silinen yetki indekste yaşar.

---

## 17. Final Recommendation

**"Eski Phase 1.5 planını revize et"** (revize + genişlet).

Gerekçe: eski baseline maddeleri `AGENTS.md` binding core'u ve owner'ın §5.D kararıdır. Hiçbiri
düşmez. PDF'in control-plane modeli bunların **üstüne** eklenir, ama PDF'in kapsamı daraltılır ve
eksik sınırlar (network, identity, entitlements, approval sahipliği) düzeltilir. "Tamamen değiştir"
değil, çünkü eski planın özü (tenant-safety, RLS, test, Host, contract) hiç değişmeden geçerli.

### OLD → NEW

| OLD (eski 1.5 / PDF) | NEW |
|---|---|
| Role → Permission (`role_permissions`) | Role → PermissionSet → ActionKey (`role_permission_sets`, `permission_set_items`) |
| `permissions` (bigint id, metadata yok, DB kaynak) | `actions` (text PK, owner_module/resource_type/risk_class/status; kod manifest'i kaynak) |
| `roles.tenant_id NULL` = system rol | Copy-on-provision, `origin` + `system_key`, `tenant_id NOT NULL` *(owner onayı)* |
| `role_assignments.role_id` tekli FK | Composite `(tenant_id, role_id)` FK |
| `account_id` | `principal_type` + `principal_id` |
| `RoleAssignmentScopeType {Tenant, OrganizationUnit, Network}` | `scope_kind {tenant, org_node}` (+hierarchy_ref); **network ayrı governed-analytics grant'i** |
| PDF: Own/Team/Org/Territory yeri tanımsız | Org scope → assignment; relation → PS item; share → ayrı |
| PDF: direct PS assignment IMPLEMENT NOW | Freeze only |
| PDF: authorized-actions discovery IMPLEMENT NOW | Batch in-process authorize contract'ı; HTTP yok |
| PDF: query scope yalnız TenantScope | `None / All / OwnedBy` + eşdeğerlik testi |
| PDF: pipeline sıralı restriction | Algebra: union grant ∧ ¬forbid, sıra bağımsız, fail-closed |
| PDF: "Tenant Boundary / RLS" pipeline adımı | Tenant gate (membership + tenant state) uygulama katmanında; RLS savunma derinliği |
| PDF: entitlement / feature flag yok | Entitlement gate grant'ten önce; flag yetki değil |
| PDF: Resource belirsiz | ResourceDescriptor PEP'ten; create için type-only |
| PDF: Access revision ayrı kavram | Revision = decision epoch (tek sayaç) |
| PDF: config audit | Access evidence + outbox + idempotency (binding core) |
| PDF: decision telemetry (yer belirsiz) | Log/OTel emit, DB yok; high-risk kanıt domain evidence'ında `decisionId` ile |
| PDF: Sharing / Field Security ayrı BC | Access subdomain'leri; alan sınıflandırması domain'de |
| PDF: Approval tek BC | Sales invariant / Workflow definitions / Human Tasks. Doc 08 ile uzlaştır *(owner kararı)* |
| PDF: Territory ayrı BC (platform) | Ticari context, Access'e fact |
| PDF: ortak ConditionExpression AST | İleride CEL-uyumlu alt küme; şimdi hiçbir şey |
| PDF: Identity ve network yok | Identity context'i, principal type'lar, ActorContext zinciri; tenant network sahipliği açık madde |
| Eski baseline (Host, RLS, test, uniqueness, row_version, evidence) PDF'te yok | **Aynen korunur, IMPLEMENT NOW'un A bölümü** |
| PDF: 18 satır IMPLEMENT NOW | 7 baseline + 11 çekirdek; org/territory/share/field/restriction yalnız freeze |

---

### Kaynaklar

- Salesforce: [Restriction Rules](https://help.salesforce.com/s/articleView?id=sf.security_restriction_rule.htm&language=en_US), [Restriction Rules Developer Guide](https://developer.salesforce.com/docs/atlas.en-us.restriction_rules.meta/restriction_rules/restriction_rules_about.htm), [Permission Set Group Muting Dependencies](https://help.salesforce.com/s/articleView?id=platform.perm_set_groups_muting_dependencies.htm&language=en_US&type=5), [Trailhead: Mute Permissions](https://trailhead.salesforce.com/content/learn/modules/permission-set-groups/mute-permissions-in-permission-set-groups)
- Microsoft: [Security roles and privileges](https://learn.microsoft.com/en-us/power-platform/admin/security-roles-privileges), [Security concepts in Dataverse](https://learn.microsoft.com/en-us/power-platform/admin/wp-security-cds), [Hierarchy security](https://learn.microsoft.com/en-us/power-platform/admin/hierarchy-security), [Column-level security](https://learn.microsoft.com/en-us/power-platform/admin/field-level-security)
- SAP: [Defining Restrictions for Business Roles](https://learning.sap.com/courses/implementing-sap-s-4hana-cloud-public-edition/defining-restrictions-for-business-roles_c9db7dc6-142e-4082-bed0-87ba818a3ede), [KBA 2733842](https://userapps.support.sap.com/sap/support/knowledge/en/2733842), [Using Restrictions (SAP Community)](https://community.sap.com/t5/enterprise-resource-planning-blog-posts-by-sap/using-restrictions-to-enhance-user-authorizations-in-the-sap-s-4hana-cloud/ba-p/13575978)
- Oracle: [Data Access (Fusion 25D)](https://docs.oracle.com/en/cloud/saas/applications-common/25d/faser/data-access.html), [Understanding Security](https://docs.oracle.com/cd/E56614_01/common_op/OASCP/F1226046AN126CC.htm)
- ServiceNow: [Deny-Unless ACL](https://www.servicenow.com/docs/r/platform-security/access-control/acl-denial-behavior.html), [Planning Your Access Control Strategy](https://www.servicenow.com/community/platform-privacy-security-blog/planning-your-access-control-strategy/ba-p/3446016)
- HubSpot: [HubSpot user permissions guide](https://knowledge.hubspot.com/user-management/hubspot-user-permissions-guide), [Manage user permissions](https://knowledge.hubspot.com/user-management/manage-user-permissions)
- Cedar: [Authorization](https://docs.cedarpolicy.com/auth/authorization.html), [Security](https://docs.cedarpolicy.com/other/security.html)
- OpenFGA: [Immutable Authorization Models](https://openfga.dev/docs/getting-started/immutable-models), [Conditions](https://openfga.dev/docs/modeling/conditions), [Contextual Tuples](https://openfga.dev/docs/interacting/contextual-tuples)
- OPA: [Data Filtering](https://www.openpolicyagent.org/docs/filtering), [SQL Filtering Tutorial](https://www.openpolicyagent.org/docs/filtering/tutorial-sql-filtering), [REST API (Compile)](https://www.openpolicyagent.org/docs/rest-api)
- Doğrulanmadı (bu tur): Zoho CRM, NIST SP 800-162 metni.
