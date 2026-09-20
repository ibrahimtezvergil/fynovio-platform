# Phase 2.5A + 2.5B — Post-Implementation Audit Report

> **Kaynak:** `Phase_2.5A_2.5B_Post_Implementation_Audit_Prompt.pdf` (§1–§23).
> **Kanıt kuralı:** Her PASS, bu oturumda kendi okuduğum bir `dosya:satır`a ya da kendi koştuğum bir komut çıktısına dayanır. Dört salt-okunur alt-ajan (A auth, B frontend, C backend, D testler) yalnızca *ipucu* üretti; hangi iddiaların doğrulandığı §8'de, hangilerinin çürütüldüğü §5'te yazılıdır. Doğrulanamayanlar **UNVERIFIED** diye işaretlidir.

## 1. Executive Result

**PASS WITH NON-BLOCKING FINDINGS** — denetim hedefi **`b06905e`** (`feat/phase-2-6-crm-production-readiness` ucu) **+ bu raporun düzeltme commit'i F1**, iki koşulla:

1. **F1 uygulanmış olmalı** (return-URL sanitizer düzeltmesi; §6). Düzeltme olmadan 2.5A'da gerçek bir kusur var.
2. **Edit'in kapsamı sahibin resmî kararıyla kapatılmalı** (§7.2): Phase 2 backend'i skaler güncelleme komutu sunmuyor; bu bir frontend teslimat hatası değil, belgelenmiş üst-akım API sözleşmesi kararı (2.5B Final Report §9 G6).

**Denetim hedefleri.** Tam denetim `f734c73` (Phase 2.5B'nin `main`'e merge edildiği hâl `f0fbf82` + Phase 2.6'nın ilk iki commit'i) üzerinde yapıldı. Reassign'ın `f734c73`'te bağlı olmadığı görülünce, kullanıcının bulunduğu dal `b06905e`'ye **hedefli doğrulama** yapıldı (yalnızca Reassign yolu + tüm test paketleri + tam E2E); diğer bölümler `b06905e` için baştan denetlenmedi. `b06905e`, `f734c73`'ün torunudur; kimlik doğrulama dosyalarına (`web/src/lib/auth`, `routes`, `login.e2e.ts`) dokunmaz. Ancak Phase 2.6 Opportunity **create formunu, şemayı ve detay sayfasını** değiştirmiştir (`OpportunityForm`, `PartyPicker`, `ReassignDialog`, `schema.ts`, referans sorguları). Bu değişiklikler yalnızca tam paketin yeşil olması ölçüsünde (vitest 385/385, E2E 42/42) doğrulandı; kodları satır satır denetlenmedi.

**Kısa özet:** `f734c73` tek başına **FAIL — BLOCKERS REMAIN** olurdu (Reassign kontrolü yok). Bu blocker `b06905e`'de çözülmüş ve doğrulanmıştır. Kritik/Yüksek güvenlik bulgusu **yok**; tenant izolasyonu sağlam.

## 2. Phase 2.5A Status

| Yetenek | Durum | Kanıt |
|---|---|---|
| Login (hatalı bilgi, yükleniyor, çift gönderim) | PASS | E2E #14 (tek genel mesaj); `sessionClient.ts:33-38` |
| Return URL / open-redirect | PASS **düzeltme sonrası** | Kusur ve düzeltme §6; E2E #16–21 |
| Logout (sunucu iptali, istemci temizliği, cache) | PASS | `AuthEndpoints.cs:161-172` çerez siler + `LogoutHandler`; `AuthEndpointsTests.cs:412` eski access token'ı anında öldürür; `sessionClient.ts:58-66` `cancelQueries`+`clear`; E2E #13 (token web storage'da yok) |
| Oturum yenileme / süre dolumu | PASS | `sessionClient.ts:101-138` tek-uçuş + Web Locks + 409 yeniden deneme; auth çağrıları `skipAuthRefresh` (`:9`) → döngü yok; E2E #37 (401 → yenile) |
| Protected routes | PASS | `ProtectedRoute.tsx:20-32` `unknown` → `RouteFallback` (içerik/veri flash'ı yok), diğer durumlar deterministik yönlendirme |
| Current-user / bootstrap | PARTIAL | Kimlik + aktif tenant + üyelikler `AuthResult` ile geliyor (`session.ts:44-63`), CRM'e bağlı değil. **Sunucu capabilities'i hiç tüketilmiyor** (§5 A2) |
| Tenant seçimi / geçişi | PASS | UI var (`TenantSwitcher.tsx`); sunucu üyeliği doğrular (`SelectTenantHandler.cs:9,66-70`); E2E #9, #22, #36 |
| Forgot/Reset/Change password, davet | PASS | E2E #1–8 |
| Register | PASS (kapalı) | E2E #24: kayıt kapalı, davet-only ekranı, API rotası yok. SSO/OIDC/SAML'i engelleyen bir karar **incelenmedi** (UNVERIFIED) |
| Ağ hatası | PARTIAL | Login ağ-hatası istisna yolu için doğrudan test yok (TEST GAP, Low) |

## 3. Phase 2.5B Status

| Ekran/Akış | Durum | Kanıt |
|---|---|---|
| List | PASS | E2E #25, #38 (CRM grant yok → boş, kayıt var/yok ayırt edilemez) |
| Detail | PASS | E2E #39 (404), #40 (bozuk id API'ye gitmeden not-found); backend `GET` bare 404 (`OpportunityEndpoints.cs:106`) |
| Create | PASS | E2E #26, #29 (doğrulama), #34/#35 (idempotency) |
| **Edit** | **PARTIAL (sahip kararı)** | Backend'de skaler güncelleme komutu yok (`OpportunityEndpoints.cs:15-99`: create, lines, cancel-line, open, stage, win, lose, reassign). Teslim edilen "edit" = satır ekle/iptal + Open'da bitiş tarihi. E2E'de Journey C (alan düzenleme) yok |
| Stage Change | PASS | Kendi handler'ı: tenant-kapsamlı yükleme + kayıt yetkisi + hedef aşamanın tenant/pipeline sürümü/aktifliği + RowVersion (`ChangePipelineStageHandler.cs:35-73`); E2E #26 |
| **Reassign** | `f734c73`: **PARTIAL** · `b06905e`: **PASS** | `f734c73`: endpoint + `useReassignOpportunity` var, **kontrol yok** (`SummaryCard.tsx:44-48` yalnızca bağımlılık notu). `b06905e`: `ReassignDialog` + `AssigneePicker` bağlı; handler hedefi sunucuda yeniden doğrular (`ReassignOpportunityHandler.cs:59-61` → `IsPrincipalPermittedAsync`, `CrmAssignmentPolicy.cs`: read + change_stage şartı); aday dizini tenant + aktif üyelik + PDP-türevli (`AuthorizedPrincipalDirectory.cs:56-78`), yoksa/pasifse kapalı-başarısız; E2E 6b: viewer ve yabancı issuer → **422 `principal_not_assignable`**, kayıt değişmedi, başka tenant adminin aday listesi → **404** |
| Won | PASS | E2E #26 |
| Lost (+ neden zorunlu) | PASS | E2E #27, #28 (boş neden: doğrulama, istek yok) |

## 4. Security / Tenant Isolation Findings

**Critical: yok. High: yok.**

**Medium — S1: kaba yetkisi olmayan çağıran için 403/404 farkı (var/yok ayrımı).**
Komut handler'ları önce kaydı yükleyip (`ChangePipelineStageHandler.cs:35`) sonra yetkilendiriyor (`:40`). Tasarım dokümanı (`2026-09-19-crm-phase2-authorization-delta…md`, tablo satır 18–20) iki satırı ayrı ayrı tanımlar: "kaba yetkisi yok → 403" ve "kaba yetki var + kayıt yok → 404". **İkisinin kesişimini (kaba yetkisi yok VE kayıt yok) tanımlamaz**; PDF §10 matrisi de bu hücreyi belirtmez. Uygulama o hücrede **404** döndürüyor. **Ölçüm** (geçici Host testi, commit edilmedi; gövde Ek A'da): `crm.opportunity.win` yetkisi olmayan çağıran `POST /opportunities/{var}/win` → **403**, `POST /opportunities/999999999/win` → **404**. Sonuç: yetkisiz çağıran, kendi tenant'ında bir id'nin var olup olmadığını ayırt edebiliyor. Cross-tenant sızıntı **yok** (RLS; `Cross_tenant_real_opportunity_id_is_not_found_over_http`). Mevcut testler bu kombinasyonu sınamıyor. **Düzeltilmedi**: 7 mutation handler'ı ve PDP'nin kaynak-siz istekteki semantiğine dokunur, önce tasarım kararı gerekir → §7.1. `b06905e`'nin Reassign handler'ında da aynı yükle-sonra-yetkilendir sırası var (`ReassignOpportunityHandler.cs:36`); ayrıca probe edilmedi.

**Low — S2: `sanitizeReturnUrl` nokta-segmentleri normalize edince `//host` döndürüyordu.** *Düzeltildi* (§6). Gerçek tarayıcıda sömürülemedi: React Router 7.18.3 `validateNavigationTarget` "External navigation is not allowed" fırlatıyor; dış origin'e istek gitmedi (Playwright `page.route` ile ölçüldü). Etkisi: giriş sonrası hata ekranı; framework guard'ına bağımlı olmak kırılgan.

**Low — S3: geniş istisna eşlemesi.** `CrmProblemDetailsExceptionHandler.cs:32-33` (`f734c73`) `ArgumentException`→400 ve `InvalidOperationException`→409'u ham `exception.Message` ile döndürür. Alan istisnaları için istenen bu; ancak EF/Npgsql/DI kaynaklı bir `InvalidOperationException` iç metni sızdırıp 5xx yerine 409 görünebilir. **Gösterilmedi**, yalnızca kod yolu.

**Low — S4: sunucuya ulaşılamazken logout.** `sessionClient.ts:58-66` hatayı yutup istemci oturumunu kapatır; HttpOnly yenileme çerezi sunucuda geçerli kalabilir, yeniden yüklemede oturum geri gelir. Yaygın bir değiş-tokuş; belgelenmesi yeterli.

**Doğrulanan sağlam noktalar:** tenant yalnızca JWT `tid` + üyelik/oturum doğrulamasından (`ActorContextMiddleware.cs:8-15,32-58`); hiçbir komut gövdesinde tenant yok (`OpportunityEndpoints.cs`); çerez `HttpOnly`, `SameSite=Strict`, `Secure` (yalnızca `AllowInsecureCookieInDevelopment` + Development'ta kapalı) (`RefreshCookieWriter.cs:62-72`); access token yalnızca bellekte (`session.ts:15`); aşama/pipeline kimliği doğrulaması. E2E #10–12: kurcalanmış tenant claim → erişim yok, 401≠403, CSRF/yabancı origin reddi.

## 5. Architecture Findings

- **A1 (UX GAP, Low):** "Yeni fırsat" düğmesi koşulsuz görünür (`OpportunitiesPage.tsx:45-48,87`); Create için yetki projeksiyonu yok. Backend reddi sağlam (E2E #30: viewer create → 403). PDF §12'nin "belli ki kullanılamayan eylemi gösterme" maddesi Create için karşılanmıyor.
- **A2 (OPTIONAL, Low):** Uygulama-düzeyi `CapabilityProvider` statik demo mock'u (`lib/capabilities/useCapability.ts:10`, `['pipeline.approval']`). Backend `/auth/me` capabilities döner (`AuthEndpoints.cs:241`) ama frontend `/auth/me`'yi hiç çağırmıyor. Opportunity özelliği bunun yerine doğru olanı yapıyor: backend `GET …/actions` projeksiyonu, kapalı-başarısız (`schema.ts:60-68`). CRM menüsü CRM erişimine göre gizlenmiyor.
- **A3:** Frontend zod şeması `nullish` alanları kasıtlı taşıyor (ileride alan-düzeyi maskeleme); `OpportunityStatus` tamsayı çözümü `min(0).max(3)` (`schema.ts:11-13`) → bilinmeyen değer parse hatasıyla hata durumuna düşer, sessiz `undefined` üretmez. ID'ler JS `number` (bigint → 2^53 altında güvenli); kimlik stratejisi zaten açık karar.
- **Alt-ajan iddiaları çürütüldü:** (a) A'nın "capabilities tenant değişiminde yenilenmiyor (Medium)" — desteklenmiyor: frontend `canInviteMembers`'a hiç başvurmuyor. (b) C'nin "Contract Mismatch (Critical)" — güvenli yöndeki kasıtlı tasarım. (c) C'nin "bilinmeyen enum sessizce undefined" — yanlış (A3). (d) D'nin "304 vitest / 35 E2E" ve "Reassign STRONG" — gerçek 363/38 ve `f734c73`'te kontrol yoktu. (e) A'nın "open-redirect: 20+ vektör reddediliyor, PASS" — S2'yi kaçırdı.

## 6. Fixes Made

**F1 — `sanitizeReturnUrl` çıktısını da doğrula.** Commit'ler: `7de8d41` (dal `audit/phase-2-5-verification`, `f734c73` tabanlı) ve `93a306a` (aynı değişiklik, bu dalda `b06905e` üzerine cherry-pick; çakışma yok).
- *Problem:* `/.//evil.com`, `/a/..//evil.com`, `/./\evil.com`, `/%2e//evil.com` → `//evil.com` döndü.
- *Kök neden:* URL ayrıştırma nokta-segmentlerini çökertiyor; şekil kontrolü yalnızca ham/çözülmüş girdide, normalize çıktıda yoktu.
- *Dosyalar:* `web/src/lib/auth/returnUrl.ts` (çıktıda `isSafeRelative`), `returnUrl.test.ts` (+5 vaka), `web/e2e/login.e2e.ts` (+2 hostile URL, dış origin'e istek yakalama).
- *Kanıt:* düzeltmeden önce birim 5 kırmızı; E2E 2 kırmızı (giriş `/login`'de takıldı, ekran görüntüsünde hata ekranı); düzeltmeden sonra birim 31/31, E2E 6/6, tam paket yeşil (§8).

## 7. Remaining Non-Blocking Findings (uygulanmadı)

1. **S1** düzeltmesi: mutation handler'larında kaba yetki kontrolünü kayıt yüklemeden önce yap (veya var olmayan kayıtta da kaba kontrolü çalıştır); önce PDP'nin kaynak-siz `AuthorizationRequest` semantiğini (owner-scoped grant için) ve tasarımdaki tanımsız hücreyi netleştir. Referans desen olarak sonraki modüllere çoğaltılmadan önce yapılmalı. Regresyon testi: yetkisiz çağıran × {var, yok} id → ikisi de aynı durum.
2. **Edit** kapsamı için sahip kararı (§1 koşul 2). Backend skaler güncelleme komutu eklenirse frontend formu ayrıca gerekir.
3. S3: alan-yaşam döngüsü ihlali için özel istisna tipi; `InvalidOperationException` geniş eşlemesini daralt.
4. A1/A2: Create için yetki projeksiyonu; sunucu capabilities'i gerçekten tüketilecekse bootstrap'a bağla.
5. **TEST GAP:** `opportunities.e2e.ts` "Field READ/WRITE" testinin gövdesi boş, **geçen** test olarak sayılıyor → `test.skip` yap. Journey C (alan düzenleme) için E2E yok.
6. **TEST GAP (hermetik değil):** `Host.Tests … Enabling_smtp_without_a_host_fails_at_start_up` geliştiricinin user-secrets'ındaki `Email:Smtp:Host` yüzünden kırmızı (boş `HOME` ile geçti; ürün guard'ı `Program.cs:161` sağlam). Öneri: fixture varsayılanına `Email__Smtp__Host=""` ekle (env, user-secrets'ı ezer). `f734c73` ve `b06905e`'de aynı.
7. **Phase 2.6 test bayrağı:** `Access.Tests … Concurrent_enables_of_one_tenant_enable_it_exactly_once` (`e01c3b7`) tam koşuda **iki ayrı koşuda da** kırmızı (beklenen 2, gerçek 1), izole koşuda 3/3 geçti → yük değil, testler arası sıra/paylaşılan durum bağımlılığı. 2.5 kapsamı dışı; 2.6 sahibine.
8. Geri tuşu davranışı, yeniden yükleme, §17 responsive ve dialog/odak: kaynaktan kanıtlanamaz → **UNVERIFIED** (tarayıcıda sürülmedi).

## 8. Verification Evidence

Dosyalar bu oturumda izole worktree'lerde koşuldu (ana ağaçtaki canlı işe dokunulmadı). Alt-ajanların kapsam dışı yazma yapmadığı `git status` ile doğrulandı. `dotnet test` sandbox'ta vstest soket `bind()`'ı engellendiği için sandbox dışında koşuldu.

| Komut | `f734c73` | `b06905e` + F1 |
|---|---|---|
| `cd web && npm run check` (lint+tsc+vitest) | öncesi 363/363; F1 sonrası **368/368** | **44 dosya / 385 test** geçti; lint yalnızca uyarı, çıkış 0 |
| `dotnet test fynovio-platform.slnx -m:1 -nodeReuse:false` | CRM 142/142, MasterData 30/30, Access 291/292, Host 166/167 | CRM 163/163, MasterData 38/38, Access 300/301, Host 184/185 |
| `E2E_APP_PORT=5184 bash scripts/e2e.sh` (gerçek Postgres+API+Vite+Chrome) | öncesi 38/38; F1 sonrası **40/40** *(biri boş gövde, §7.5)* | **42/42** *(biri boş gövde)*; Reassign 6, 6b, 6c dahil |
| Hostile-URL E2E, F1'den önce | 4 geçti / **2 kırmızı** | — |
| Hostile-URL E2E, F1'den sonra | 6/6 | (tam pakette geçti) |

İki kırmızı test iki hedefte de aynı (§7.6, §7.7); ikisi de 2.5A/2.5B ürün davranışı değil. Not: ilk `b06905e` E2E denemem, taze worktree'de `dotnet restore` yapılmadığı için (NETSDK1004) başlamadı — üründen değil ortamdan; restore sonrası geçti.

**Bizzat okuyup doğruladığım:** `returnUrl.ts`, `sessionClient.ts`, `session.ts`, `ProtectedRoute.tsx`, `TenantSwitcher.tsx`, `OpportunityEndpoints.cs`, `CrmProblemDetailsExceptionHandler.cs`, `ChangePipelineStageHandler.cs`, `RefreshCookieWriter.cs`, `ActorContextMiddleware.cs`, `schema.ts`, `AuthEndpoints.cs` logout, `SummaryCard.tsx`/`api.ts`, tasarım dokümanı tablosu; `b06905e`'de `ReassignOpportunityHandler.cs`, `CrmAssignmentPolicy.cs`, `AuthorizedPrincipalDirectory.cs`, E2E 6b.
**Yalnızca alt-ajan raporuna dayanan (UNVERIFIED):** §13 durumlarının tek tek bileşen eşlemesi, §14 mutasyon-başına invalidation kodu (davranışsal olarak E2E #26/#27/#33/#36 ile örtüşüyor), tarih/para tipi ayrıntıları, çift-sekme senaryoları. `b06905e`'nin Reassign dışındaki yeni yüzeyleri (PartyPicker / referans sorguları) yalnızca E2E 6c geçtiği ölçüde doğrulandı; kodu okunmadı.

### Ek A — S1 probe gövdesi (geçici, commit edilmedi)
`tests/Host.Tests/OpportunityEndpointsTests.cs` içinde, mevcut `Cross_tenant_…` testinin altına:
```csharp
[Fact]
public async Task PROBE_coarse_denial_status_for_existing_vs_missing_id()
{
    var tenantId = 5090;
    var owner = new PrincipalRef(JwtTestTokenFactory.Issuer, $"owner-{Guid.NewGuid():N}");
    var opportunityId = await SeedOpenOpportunityAsync(tenantId, owner);
    var (callerSubject, _) = await SeedGrantAsync(tenantId, "crm.opportunity.create", relation: null); // win yetkisi yok

    using var client = AuthorizedClient(callerSubject, tenantId);
    var existing = await client.PostAsJsonAsync($"/opportunities/{opportunityId}/win", new { ExpectedVersion = 3 });
    var missing = await client.PostAsJsonAsync("/opportunities/999999999/win", new { ExpectedVersion = 3 });

    Assert.Equal(((int)existing.StatusCode, (int)missing.StatusCode), (403, 403)); // gözlenen: (403, 404)
}
```

## 9. Final Architecture State

Tarayıcı → `ProtectedRoute` (yalnızca UX) → bellek-içi access token + HttpOnly yenileme çerezi → `ActorContextMiddleware` (JWT `tid`/`sid`, üyelik + oturum doğrulaması; tenant asla istemciden) → RLS'li tenant-kapsamlı yükleme → `IAuthorizer` (kaba + kayıt) → alan komutu (`Opportunity.ChangeStage/Win/Lose…`) + RowVersion + idempotency → outbox + evidence. Frontend ikinci bir yetki/iş kuralı katmanı **değil**: eylem görünürlüğü `GET …/actions` projeksiyonundan gelir, projeksiyon yoksa hiçbir yaşam döngüsü kontrolü sunulmaz. Reassign hedefi hem UI'da (sunucunun listesinden) hem de handler'da (`IsPrincipalPermittedAsync`) sunucu tarafından doğrulanır; frontend aday filtrelemez. Tenant değişiminde/oturum sonunda TanStack cache `cancelQueries`+`clear` ile atılır. Sapma: kaba yetkisiz çağıran için 403/404 sırası (S1).

## 10. Phase 2.5 Completion Decision

- **Phase 2.5A: KAPANABİLİR** — F1 uygulandıktan sonra.
- **Phase 2.5B: KAPANABİLİR** — `b06905e` ucunda ve F1 ile; **tek koşul:** Edit'in kapsam dışı olduğunun sahip tarafından kaydedilmesi (§7.2). `f734c73` tek başına kapanamazdı (Reassign yok).
- Kapanışı engellemeyen ama referans desen olmadan önce çözülmesi gereken: **S1** (403/404 sırası), test boşlukları (§7.5–7.7).

> **Nihai not:** Amaç uygulamanın iyi olduğunu söylemek değil, ilk gerçek kurumsal CRM dikey diliminin doğru, güvenli ve referans desen olmaya hazır olup olmadığını kanıtlamaktı. Güvenlik/izolasyon açısından hazır; kapsam açısından bir madde (Edit) sahibin kaydını bekliyor; bir yetkilendirme-sırası sapması (S1) referans desen olarak çoğaltılmadan önce düzeltilmeli.
