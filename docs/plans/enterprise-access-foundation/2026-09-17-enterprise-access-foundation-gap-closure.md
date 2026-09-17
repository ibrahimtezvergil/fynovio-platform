# Enterprise Access Foundation — Technical Gap Closure (pre-execution)

> **Kapsam:** Round 1–4 mimari kararları (owner onaylı) tamamlandı. Bu dosya, execution
> planı yazılabilmesi için gereken **HOW seviyesindeki** teknik boşlukları kapatır —
> hiçbiri owner'ın frozen ettiği business/architecture semantics'ini değiştirmiyor,
> yalnızca "Scope Lock" (round 3 §9) uyarınca **WHAT sabit, HOW detaylandırılıyor**.
> Round 1–4 dosyalarına dokunulmadı.

---

## 1. RoleAssignment scope — "reserve" değil, "kaldır"

Round 3 "Network koşulsuz kaldırılacak" dedi ama `OrganizationUnit`/tenant-wide ayrımını
nasıl temsil edeceğimizi belirlemedi. Karar: **`ScopeType`/`ScopeId` kolonlarının ikisi de
tamamen kaldırılıyor**, tek bir "reserved ama hep deny" değeri bırakılmıyor. Phase 1.5'te
her `RoleAssignment` zaten tenant-geneli. Organization'ın fact provider'ı geldiğinde yeni
bir migration'la `scope_kind`/`scope_ref` **eklenir** (additive, AGENTS.md'nin
expand/contract kuralına uygun) — bugünden boş/hep-deny bir kolon taşımak YAGNI ihlali
olurdu (round 1 risk #4 ile aynı gerekçe). Bu, Network hedge'ini de en temiz şekilde kapatır:
enum diye bir şey kalmıyor, sökülecek bir şey yok.

## 2. ActionKey / Action Registry tablosu — text PK

Round 1 önerdi, round 3 revize etmedi: `permissions` tablosunun yerini alan
`access.actions` tablosu **text primary key** (`action_key`) kullanır, surrogate
`bigint id` yok — doğal anahtar, on-prem kurulumlar arası id kayması riski olmaz.
`ActionKey` formatı: `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$` (en az üç segment,
`crm.opportunity.win` gibi).

## 3. Action Registry senkronizasyonu — CI analyzer değil, runtime + idempotent seed

Round 1 "CI unregistered ActionKey'e fail etmeli" dedi. Roslyn analyzer inşa etmek bu
faz için aşırı mühendislik (round 1'in kendi YAGNI uyarılarıyla çelişir). Karar:
- Kod tarafı kaynak: modül başına statik bir `ActionRegistryEntry[]` listesi
  (Phase 1.5'te yalnız `Access`'in kendi vocabulary'si — `crm.opportunity.*` **seed
  edilmiyor**, çünkü CRM bu fazda dokunulmuyor; Phase 2 kendi action key'lerini kendi
  kaydettiğinde ekler).
- Host startup'ta idempotent upsert (`AccessActionCatalogSeeder`): listede olmayan ama
  DB'de olan satırlar **silinmez**, `IsDeprecated=true` işaretlenir (round 1'in "rename
  = deprecate + yeni key" kararının otomatik sonucu).
- **Runtime fail-closed**: `AccessAuthorizer`, action registry'de kayıtlı+deprecated
  olmayan bir `ActionKey` görürse `Deny("action_not_registered")` döner. Bu, "CI
  reddeder" hedefinden daha güçlü bir garanti (build-time değil, hiçbir zaman
  çalışmaz) ve bugünkü tek-tüketicili (yalnız Access) sistem için yeterli. Çok modüllü
  gerçek build-time analyzer ihtiyacı doğarsa bu ayrı bir architecture delta'dır.

## 4. Contracts tipleri — minimal, genişletilebilir yüzey

Round 1'in listelediği tipler (`ActionKey`, `ActorContext`, `ResourceDescriptor`,
`AuthorizationRequest/Decision`, `IAuthorizer`, `AccessScope`, `IAccessScopeResolver`,
`IActionCatalog`) **PrincipalRef üzerinden** kurulur — ayrı bir `PrincipalId{type,id}`
Contracts tipi **eklenmiyor** çünkü cross-module identity key zaten `PrincipalRef`
(issuer+subject) olarak frozen (`Contracts/PrincipalRef.cs`'in kendi doc comment'i).
Access, `PrincipalRef`'i kendi içinde `ExternalIdentity` üzerinden `AccountId`'ye
çözer — bu bir Access implementation detayı, Contracts'a sızmaz.

`ActorContext` bu turda yalnız `TenantId + PrincipalRef + CorrelationId` taşır.
`ActingFor`/`ImpersonatedBy`/`AuthenticationContext` **eklenmiyor** (DESIGN/FREEZE,
round 1 risk #ilgili) — gerçek bir tüketicisi olmayan alanları şimdiden Contracts'a
koymak, sonradan eklemekten daha pahalı değil (record struct genişletmek, tek
tüketicili bu fazda, kırılma riski yok).

`ResourceDescriptor` bu turda yalnız `ResourceType + Id? + OwnerPrincipal?` taşır.
`OrgNode`/`Territory`/`Attributes` **eklenmiyor** — org/territory/restriction henüz
yok, boş alan taşımak yerine bu alanlar o fact provider'lar gerçek olduğunda eklenir.

`AuthorizationDecision` bu turda `Effect + ReasonCode + DecisionId + Revision` taşır.
`MatchedGrants/MatchedRestrictions/Obligations` **eklenmiyor** (restriction/field
security/step-up hiçbiri implement edilmiyor bu fazda).

## 5. Escalation guard — self-check, granular allowlist yok

Round 3'ün düzelttiği `CanExercise ≠ CanGrant` ilkesi doğru, ama round 1'in önerdiği
"hangi admin hangi role/PS'i verebilir" allowlist'i bugün için aşırı — hiçbir admin
katmanlaşması (birden fazla admin tier'ı) yok. Karar: Phase 1.5'te tek mekanizma,
**Grant/Revoke komutlarının kendisi de Access'in ürettiği `Authorize()` ile korunur**
(`access.role_assignment.grant`/`access.role_assignment.revoke` action'ları). Kim bu
action'a sahipse verebilir/geri alabilir — "hangi spesifik role'ü verebilir" ayrımı
yok. Granular delegation-boundary allowlist'i DESIGN/FREEZE'de kalır (round 3 §9
"Delegation" maddesiyle aynı yerde).

## 6. Bootstrap problemi — yeni bulgu, kapatılıyor

**Round 1–4'te fark edilmemiş gerçek bir boşluk:** `Authorize()` default-deny olduğu
için, bir tenant'ın **ilk** `RoleAssignment`'ı normal Grant akışıyla asla oluşamaz —
grant etmek isteyen principal'ın zaten `access.role_assignment.grant` yetkisine sahip
olması gerekir, ama daha hiç kimseye yetki verilmemiştir. Bu, her IAM sisteminin
çözmek zorunda olduğu bootstrap problemi (Salesforce org oluşturulurken varsayılan
System Administrator profili, Dataverse ortam oluşturulurken varsayılan System
Administrator security role'ü ile aynı ihtiyaç).

**Karar:** `BootstrapTenantAccessHandler` — **tek**, açıkça güvenilir, HTTP'ye
kesinlikle açılmayan bir handler. Bir tenant için varsayılan "Tenant Administrator"
Role + PermissionSet + ilk `RoleAssignment`'ı, `Authorize()` kontrolünden geçmeden
oluşturur. Bu, Decision A'nın "template → tenant-local instance (provisioning'de
kopyalanır)" mekanizmasının **bugünkü** somut karşılığıdır — gerçek `TenantLifecycle.
ProvisionTenant` komutu gelene kadar bu handler test fixture'ları / manuel çağrı
yoluyla tetiklenir. Evidence + outbox + idempotency **yine yazılır** (round 3'ün "no
silent DB update" ilkesi authorization-gate'siz olsa bile geçerli) — yalnızca
`Authorize()` adımı atlanır, GrantedBySource="bootstrap" olarak işaretlenir.

Bu, `TenantMembership.Invite`'ın da authorization-gate'siz olmasıyla aynı kategoridedir
— kimlik/erişimin kendisini kuran işlem, henüz var olmayan bir yetkiyle korunamaz.

## 7. PermissionSetItem `relation` — yalnız `owner`

Round 1/3 ile tutarlı, değişmedi: `relation` sütunu `NULL` (sınırsız, scope içinde her
kayıt) veya `'owner'` (yalnız `ResourceDescriptor.OwnerPrincipal == actor.Principal`)
değerlerini alır. CHECK constraint bu iki değerle sınırlı.

## 8. `PrincipalType` — Access-internal enum, Contracts'ta değil

`RoleAssignment`'ın `account_id`'si zaten Access'in kendi identity çözümlemesi
(`identity.accounts`) üzerinden geliyor. `PrincipalType` (round 1'in istediği
"service/agent/group'a açık, 1.5'te yalnız user") **Access'in kendi domain enum'u**
olarak eklenir (`enum PrincipalType { User }`, string conversion ile — `MembershipStatus`
kalıbıyla aynı), Contracts'a sızmaz. Bu, §4'teki "Contracts'a `PrincipalId` eklenmiyor"
kararıyla tutarlı.

## 9. CRM Owner semantics — Phase 1.5 kapsamı dışında, yalnız contract'ı etkiler

Round 3/4 `Owner = AssignedPrincipal` ve `Reassign()` gerekliliğini **business
semantics** olarak dondurdu. **Ama `Opportunity.Reassign()`'ın gerçek implementasyonu
Phase 2 kapsamıdır** — Phase 1.5 CRM koduna hiç dokunmuyor (Access foundation, owner'ın
kendi ayrımı: "Phase 2 Opportunity commands/API, Phase 1.5 paralel Access baseline").
Phase 1.5'in tek sorumluluğu: `ResolveAccessScope`'un `OwnedBy` terimi ve
`Authorize()`'ın `owner` relation kontrolü, Phase 2 `Reassign()`'ı yazdığında
**hiçbir Access contract değişikliği gerektirmeyecek** şekilde tasarlanmış olsun. Bu
zaten §4/§7'deki `ResourceDescriptor.OwnerPrincipal` + `relation="owner"` tasarımıyla
sağlanıyor — `OwnerPrincipal`'ın kaynağı (`AssignedPrincipal` olacak) tamamen CRM'in
PEP'inin sorumluluğu, Access bunu bilmez.

---

Bu 9 madde, execution planının kod yazabilmesi için gereken tüm HOW kararlarını
kapatıyor. Hiçbiri round 1–4'ün frozen ettiği bir WHAT'i değiştirmiyor.
