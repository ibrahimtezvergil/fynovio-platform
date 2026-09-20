# Phase 2.5A + 2.5B — Post-Implementation Audit Report

> **Denetim hedefi:** `f734c73` — Phase 2.5B'nin `main`'e merge edildiği hâl (`f0fbf82`) + Phase 2.6'nın ilk iki commit'i (`e01c3b7`, `f734c73`). İzole worktree, dal `audit/phase-2-5-verification`.
> **Kapsam dışı:** Phase 2.6'nın sonraki işi (`feat/phase-2-6-crm-production-readiness`, `b06905e`; Reassign kontrolü ve atanabilir-kullanıcı sorgusu dahil) **denetlenmedi**. Aşağıdaki Reassign bulgusu bu yüzden `f734c73` için geçerlidir.
> **Kaynak:** `Phase_2.5A_2.5B_Post_Implementation_Audit_Prompt.pdf` (§1–§23).
> **Kanıt kuralı:** Her PASS, bu oturumda kendi okuduğum bir `dosya:satır`a ya da kendi koştuğum bir komut çıktısına dayanır. Dört salt-okunur alt-ajan (A auth, B frontend, C backend, D testler) yalnızca *ipucu* üretti; hangi iddiaların doğrulandığı §8'de, hangilerinin çürütüldüğü §5'te açıkça yazılıdır. Doğrulanamayanlar **UNVERIFIED** diye işaretlidir.

## 1. Executive Result

**FAIL — BLOCKERS REMAIN** (katı okuma; ikisi de sahibin kararına bağlı kapsam boşluğu, güvenlik açığı değil).

- Phase 2.5A (kimlik doğrulama) kabul kriterlerini karşılıyor; tek gerçek kusur (return-URL sanitizer) bulundu ve düzeltildi.
- Phase 2.5B'de PDF'in kendi kapsam listesindeki iki madde teslim edilmemiş: **Edit** (backend'de skaler güncelleme komutu yok) ve **Reassign** (istemci sözleşmesi hazır ama hiçbir UI kontrolüne bağlı değil). PDF §21 "Reassign uses domain authorization" ve "CRUD/action flows use real backend APIs" maddeleri bu iki boşlukla tam karşılanmıyor.
- Kritik/Yüksek güvenlik bulgusu **yok**; tenant izolasyonu sağlam.

Bu iki madde önceki oturumlarda bilinçli olarak ertelenmişti (2.5B Final Report §9 G6, açık karar OD1). Sahibi bunları PDF kapsamından resmen çıkarırsa sonuç **PASS WITH NON-BLOCKING FINDINGS** olur; bu karar denetçinin değil sahibin.

## 2. Phase 2.5A Status

| Yetenek | Durum | Kanıt |
|---|---|---|
| Login (hatalı bilgi, yükleniyor, çift gönderim) | PASS | E2E #14 (tek genel mesaj); `sessionClient.ts:33-38` |
| Return URL / open-redirect | PASS **düzeltme sonrası** | Kusur ve düzeltme §6; E2E #16–21 |
| Logout (sunucu iptali, istemci temizliği, cache) | PASS | `AuthEndpoints.cs:161-172` çerez siler + `LogoutHandler`; `AuthEndpointsTests.cs:412` eski access token'ı anında öldürür; `sessionClient.ts:58-66` `cancelQueries`+`clear`; E2E #13 (token web storage'da yok) |
| Oturum yenileme / süre dolumu | PASS | `sessionClient.ts:101-138` tek-uçuş + Web Locks + 409 yeniden deneme; auth çağrıları `skipAuthRefresh` (`:9`) → döngü yok; E2E #37 (401 → yenile) |
| Protected routes | PASS | `ProtectedRoute.tsx:20-32` `unknown` → `RouteFallback` (içerik/veri flash'ı yok), diğer durumlar deterministik yönlendirme |
| Current-user / bootstrap | PARTIAL | Kimlik + aktif tenant + üyelikler `AuthResult` ile geliyor (`session.ts:44-63`) ve CRM'e bağlı değil. **Sunucu capabilities'i hiç tüketilmiyor** (bkz. §5 A2) |
| Tenant seçimi / geçişi | PASS | UI var (`TenantSwitcher.tsx`); sunucu üyeliği doğrular (`SelectTenantHandler.cs:9,66-70`); E2E #9, #22, #36 |
| Forgot/Reset/Change password, davet | PASS | E2E #1–8 |
| Register | PASS (kapalı) | E2E #24: kayıt kapalı, davet-only ekranı, API rotası yok. SSO/OIDC/SAML'i engelleyen bir karar **incelenmedi** (UNVERIFIED) |
| Ağ hatası / yüklenme durumları | PARTIAL | Login ağ-hatası istisna yolu için doğrudan test yok (TEST GAP, Low) |

## 3. Phase 2.5B Status

| Ekran/Akış | Durum | Kanıt |
|---|---|---|
| List | PASS | E2E #25, #38 (CRM grant yok → boş, kayıt var/yok ayırt edilemez) |
| Detail | PASS | E2E #39 (404), #40 (bozuk id API'ye gitmeden not-found); backend `GET` bare 404 (`OpportunityEndpoints.cs:106`) |
| Create | PASS | E2E #26, #29 (doğrulama), #34/#35 (idempotency) |
| **Edit** | **PARTIAL** | Backend'de skaler güncelleme komutu yok (`OpportunityEndpoints.cs:15-99`: create, lines, cancel-line, open, stage, win, lose, reassign). Teslim edilen "edit" = satır ekle/iptal + Open'da bitiş tarihi. E2E'de Journey C (alan düzenleme) yok |
| Stage Change | PASS | Kendi handler'ı: tenant-kapsamlı yükleme + kayıt yetkisi + hedef aşamanın tenant/pipeline sürümü/aktifliği + RowVersion (`ChangePipelineStageHandler.cs:35-73`); E2E #26 |
| **Reassign** | **PARTIAL** | Endpoint + `useReassignOpportunity` (`opportunities/api.ts:225`) var; **hiçbir kontrol yok** — `SummaryCard.tsx:44-48` yalnızca "bağımlılık" notu gösterir. Backend reddi kanıtlı: E2E #30 (viewer → 403). Phase 2.6 dalı bunu ele almış, **denetlenmedi** |
| Won | PASS | E2E #26 |
| Lost (+ neden zorunlu) | PASS | E2E #27, #28 (boş neden: doğrulama, istek yok) |

## 4. Security / Tenant Isolation Findings

**Critical: yok. High: yok.**

**Medium — S1: kaba yetkisi olmayan çağıran için 403/404 farkı (var/yok ayrımı).**
Komut handler'ları önce kaydı yükleyip (`ChangePipelineStageHandler.cs:35`) sonra yetkilendiriyor (`:40`). Tasarım dokümanı (`2026-09-19-crm-phase2-authorization-delta…md`, tablo satır 18–20) "kaba yetkisi yok → 403; kaba yetki var + kayıt yok → 404; kaba yetki var + kayıt reddedildi → 404" der. **Ölçüm** (geçici Host testi, commit edilmedi): `crm.opportunity.win` yetkisi olmayan çağıran `POST /opportunities/{var}/win` → **403**, `POST /opportunities/999999999/win` → **404**. Yani yetkisiz çağıran, kendi tenant'ında bir id'nin var olup olmadığını ayırt edebiliyor. Cross-tenant sızıntı **yok** (RLS; mevcut test `Cross_tenant_real_opportunity_id_is_not_found_over_http`). Mevcut testler bu kombinasyonu (kaba yetki yok + var olmayan id) hiç sınamıyor. **Düzeltilmedi**: 7 mutation handler'ı ve PDP'nin kaynak-siz istekteki semantiğine dokunur; Phase 2 backend kararı gerektirir → §7.

**Low — S2: `sanitizeReturnUrl` nokta-segmentleri normalize edince `//host` döndürüyordu.** *Düzeltildi* (§6). Gerçek tarayıcıda sömürülemedi: React Router 7.18.3 `validateNavigationTarget` "External navigation is not allowed" fırlatıyor; dış origin'e istek gitmedi (Playwright `page.route` ile ölçüldü). Etkisi: giriş sonrası hata ekranı; framework guard'ına bağımlı olmak ise kırılgan.

**Low — S3: geniş istisna eşlemesi.** `CrmProblemDetailsExceptionHandler.cs:32-33` `ArgumentException`→400 ve `InvalidOperationException`→409'u ham `exception.Message` ile döndürür. Alan istisnaları için istenen bu; ancak EF/Npgsql/DI kaynaklı bir `InvalidOperationException` iç metni sızdırıp 5xx yerine 409 görünebilir. **Gösterilmedi**, yalnızca kod yolu.

**Low — S4: sunucuya ulaşılamazken logout.** `sessionClient.ts:58-66` hatayı yutup istemci oturumunu kapatır; HttpOnly yenileme çerezi sunucuda geçerli kalabilir ve yeniden yüklemede oturum geri gelir. Yaygın bir değiş-tokuş; belgelenmesi yeterli.

**Doğrulanan sağlam noktalar:** tenant yalnızca JWT `tid` + üyelik/oturum doğrulamasından (`ActorContextMiddleware.cs:8-15,32-58`); hiçbir komut gövdesinde tenant yok (`OpportunityEndpoints.cs`); çerez `HttpOnly`, `SameSite=Strict`, `Secure` (yalnızca `AllowInsecureCookieInDevelopment` + Development'ta kapalı) (`RefreshCookieWriter.cs:62-72`); access token yalnızca bellekte (`session.ts:15`); aşama/pipeline kimliği doğrulaması (yukarıda). E2E #10–12: kurcalanmış tenant claim → erişim yok, 401≠403, CSRF/yabancı origin reddi.

## 5. Architecture Findings

- **A1 (UX GAP, Low):** "Yeni fırsat" düğmesi koşulsuz görünür (`OpportunitiesPage.tsx:45-48,87`); Create için yetki projeksiyonu yok. Backend reddi sağlam (E2E #30: viewer create → 403). PDF §12'nin "belli ki kullanılamayan eylemi gösterme" maddesi Create için karşılanmıyor.
- **A2 (OPTIONAL, Low):** Uygulama-düzeyi `CapabilityProvider` statik demo mock'u (`lib/capabilities/useCapability.ts:10`, `['pipeline.approval']`). Backend `/auth/me` capabilities döner (`AuthEndpoints.cs:241`) ama frontend `/auth/me`'yi hiç çağırmıyor. Opportunity özelliği bunun yerine doğru olanı yapıyor: backend `GET …/actions` projeksiyonu, kapalı-başarısız (`schema.ts:60-68`). CRM menüsü CRM erişimine göre gizlenmiyor.
- **A3:** Frontend zod şeması `nullish` alanları kasıtlı taşıyor (ileride alan-düzeyi maskeleme); `OpportunityStatus` tamsayı çözümü `min(0).max(3)` (`schema.ts:11-13`) → bilinmeyen değer parse hatasıyla hata durumuna düşer, sessiz `undefined` üretmez. ID'ler JS `number` (bigint → 2^53 altında güvenli); kimlik stratejisi zaten açık karar.
- **Alt-ajan iddiaları çürütüldü:** (a) A'nın "capabilities tenant değişiminde yenilenmiyor (Medium)" — desteklenmiyor: frontend `canInviteMembers`'a hiç başvurmuyor. (b) C'nin "Contract Mismatch (Critical)" — güvenli yöndeki kasıtlı tasarım. (c) C'nin "bilinmeyen enum sessizce undefined" — yanlış (A3). (d) D'nin "304 vitest / 35 E2E" ve "Reassign STRONG" — gerçek 363/38; Reassign kontrolü yok. (e) A'nın "open-redirect: 20+ vektör reddediliyor, PASS" — S2'yi kaçırdı.

## 6. Fixes Made

**F1 — `sanitizeReturnUrl` çıktısını da doğrula** (`7de8d41`, dal `audit/phase-2-5-verification`).
- *Problem:* `/.//evil.com`, `/a/..//evil.com`, `/./\evil.com`, `/%2e//evil.com` → `//evil.com` döndü.
- *Kök neden:* URL ayrıştırma nokta-segmentlerini çökertiyor; şekil kontrolü yalnızca ham/çözülmüş girdide, normalize çıktıda yoktu.
- *Dosyalar:* `web/src/lib/auth/returnUrl.ts` (çıktıda `isSafeRelative`), `returnUrl.test.ts` (+5 vaka), `web/e2e/login.e2e.ts` (+2 hostile URL, dış origin'e istek yakalama).
- *Kanıt:* düzeltmeden önce birim 5 kırmızı; E2E 2 kırmızı (giriş `/login`'de takıldı, ekran görüntüsünde hata ekranı); düzeltmeden sonra birim 31/31, E2E 6/6, tam paket 40/40.
- `b06905e` (Phase 2.6) ile `git merge-tree` temiz — çakışma yok.

## 7. Remaining Non-Blocking Findings (uygulanmadı)

1. **S1** düzeltmesi: mutation handler'larında kaba yetki kontrolünü kayıt yüklemeden önce yap (veya var olmayan kayıtta da kaba kontrolü çalıştır); önce PDP'nin kaynak-siz `AuthorizationRequest` semantiğini (owner-scoped grant için) netleştir. Regresyon testi: yetkisiz çağıran × {var, yok} id → ikisi de 403.
2. **Edit** ve **Reassign** kapsamı için sahip kararı (§1).
3. S3: alan-yaşam döngüsü ihlali için özel istisna tipi; `InvalidOperationException` geniş eşlemesini daralt.
4. A1/A2: Create için yetki projeksiyonu; sunucu capabilities'i gerçekten tüketilecekse bootstrap'a bağla.
5. **TEST GAP:** `opportunities.e2e.ts:286` (#7 "Field READ/WRITE") gövdesi boş, **geçen** test olarak sayılıyor → `test.skip` yap. Journey C (alan düzenleme) ve Journey E (Reassign) için E2E yok (E, Phase 2.6'da eklenmiş; denetlenmedi).
6. **TEST GAP (hermetik değil):** `Host.Tests … Enabling_smtp_without_a_host_fails_at_start_up` geliştiricinin user-secrets'ındaki `Email:Smtp:Host` yüzünden kırmızı (boş `HOME` ile geçti; ürün guard'ı `Program.cs:161` sağlam). Öneri: fixture varsayılanına `Email__Smtp__Host=""` ekle (env, user-secrets'ı ezer).
7. **Phase 2.6 bayrağı:** `Access.Tests … Concurrent_enables_of_one_tenant_enable_it_exactly_once` (`e01c3b7`) tam koşuda 1 kez kırmızı (beklenen 2, gerçek 1), izole koşuda 3/3 geçti → yük altında yarış/flaky. 2.5 kapsamı dışı.
8. Yeniden yükleme/geri tuşu davranışı, §17 responsive ve dialog/odak: kaynaktan kanıtlanamaz → **UNVERIFIED** (tarayıcıda sürülmedi).

## 8. Verification Evidence

Çalışma dizini: izole worktree (ana ağaçtaki canlı Phase 2.6 işine dokunulmadı). Alt-ajanların `git status` sonrası kapsam dışı yazma yapmadığı doğrulandı.

| Komut | Sonuç |
|---|---|
| `cd web && npm run check` (lint + tsc + vitest) | düzeltme öncesi **363/363**, sonrası **43 dosya / 368 test geçti**; lint yalnızca uyarı, çıkış 0 |
| `dotnet test fynovio-platform.slnx -m:1 -nodeReuse:false` *(sandbox `bind()`'ı engelliyor → sandbox dışı)* | CRM **142/142**, MasterData **30/30**, Access **291/292**, Host **166/167** |
| Access başarısızı, izole ×3 | 3/3 geçti (flaky, §7.7) |
| Host SMTP başarısızı, ×2 | 2/2 kırmızı; `HOME` boş dizine yönlendirilince geçti (§7.6) |
| `E2E_APP_PORT=5184 bash scripts/e2e.sh` (gerçek Postgres+API+Vite+Chrome) | düzeltme öncesi **38/38**; sonra **40/40** |
| Yeni hostile-URL E2E, düzeltmeden önce | 4 geçti / **2 kırmızı**; sonra 6/6 |
| Geçici Host probe (S1) | `(existing, missing) = (403, 404)`; geri alındı, commit edilmedi |

**Bizzat okuyup doğruladığım:** `returnUrl.ts`, `sessionClient.ts`, `session.ts`, `ProtectedRoute.tsx`, `TenantSwitcher.tsx`, `OpportunityEndpoints.cs`, `CrmProblemDetailsExceptionHandler.cs`, `ChangePipelineStageHandler.cs`, `RefreshCookieWriter.cs`, `ActorContextMiddleware.cs` (başlık/kritik satırlar), `schema.ts`, `AuthEndpoints.cs` logout, `SummaryCard.tsx`/`api.ts` (Reassign), tasarım dokümanı tablosu.
**Yalnızca alt-ajan raporuna dayanan (UNVERIFIED):** §13 durumlarının tek tek bileşen eşlemesi, §14 mutasyon-başına invalidation kodu (davranışsal olarak E2E #26/#27/#33/#36 ile örtüşüyor), tarih/para tipi ayrıntıları, çevrimdışı/çift-sekme senaryoları.

## 9. Final Architecture State

Tarayıcı → `ProtectedRoute` (yalnızca UX) → bellek-içi access token + HttpOnly yenileme çerezi → `ActorContextMiddleware` (JWT `tid`/`sid`, üyelik + oturum doğrulaması; tenant asla istemciden) → RLS'li tenant-kapsamlı yükleme → `IAuthorizer` (kaba + kayıt) → alan komutu (`Opportunity.ChangeStage/Win/Lose…`) + RowVersion + idempotency → outbox + evidence. Frontend ikinci bir yetki/iş kuralı katmanı **değil**: eylem görünürlüğü `GET …/actions` projeksiyonundan gelir, projeksiyon yoksa hiçbir yaşam döngüsü kontrolü sunulmaz. Tenant değişiminde/oturum sonunda TanStack cache `cancelQueries`+`clear` ile atılır. Sapma: kaba yetkisiz çağıran için 403/404 sırası (S1).

## 10. Phase 2.5 Completion Decision

- **Phase 2.5A: KAPANABİLİR** (F1 uygulandıktan sonra; başka engel yok).
- **Phase 2.5B: KAPANMAZ — katı okumada iki engel:**
  1. **Reassign** kontrolü yok (`f734c73`; Phase 2.6 dalı ele almış, denetlenmedi).
  2. **Edit** (skaler güncelleme) yok — backend komutu yok.
- Engel olmayan ama planlanması gereken: **S1** (403/404 sırası), test boşlukları (§7.5–7.7).

> **Nihai not:** Amaç uygulamanın iyi olduğunu söylemek değil, ilk gerçek kurumsal CRM dikey diliminin doğru, güvenli ve referans desen olmaya hazır olup olmadığını kanıtlamaktı. Güvenlik/izolasyon açısından hazır; kapsam açısından iki madde sahibin kararını bekliyor, bir yetkilendirme-sırası sapması ise referans desen olarak çoğaltılmadan önce düzeltilmeli.
