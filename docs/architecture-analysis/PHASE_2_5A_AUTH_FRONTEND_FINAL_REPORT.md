# Phase 2.5A — Authentication Frontend: Final Report

> **Durum:** kod, testler ve dokümantasyon tamam. **Dal:** `feat/phase-2-5a-auth` (henüz `main`'e merge edilmedi). **Plan:** `PHASE_2_5A_AUTH_FRONTEND_IMPLEMENTATION_PLAN.md`. Phase 2.5B (CRM UI) kapsam dışıdır ve başlatılmadı.
> Bu rapordaki her iddia repodaki bir teste, commit'e veya koda dayanır. Kanıtı olmayan yer "kanıt yok" diye işaretlenmiştir.

## 1. Slice özeti

| Slice | Ne yapıldı | Commit'ler |
|---|---|---|
| **S1 — Backend auth core** | `identity` şemasında `account_credentials`, `auth_sessions`, `auth_refresh_tokens`, `auth_events`; `membership_self_view` RLS politikası (`app.account_id`); parola hash'i (`PasswordHasher`), politika, lockout; erişim JWT'si (`sid`, `jti`, `tid`, TTL 10 dk, ClockSkew 30 sn); dönen HttpOnly refresh cookie'si + reuse tespiti; `/auth/{config,login,refresh,logout,me,tenants/select}`; CSRF/Origin guard, CORS, rate limiting; `ActorContextMiddleware`'de `sid` iptal kontrolü; dev seed. | `92549c9` … `6825c31`, `de8e596` |
| **S2 — Frontend auth core** | `lib/auth` oturum modülü (bellekte access token, persist yok), `SessionGate`, refresh'te single-flight (Web Locks), 401→refresh→tek retry, `sanitizeReturnUrl`, guard'lar, `TenantSelector`/`TenantSwitcher`, `NoAccessPage`, Vite `/api` proxy, eski `fynovio-auth` localStorage anahtarının temizlenmesi, `usePermission` fail-closed. | `c7dc626` … `b2b3a65`, `d80079e` |
| **S3 — Backend davet & parola yaşam döngüsü** | `account_tokens` (yalnızca hash), `AccountTokenService` (tek kullanımlık), davet oluştur/önizle/kabul, forgot/reset/change, kayıt (config'le kapalı), tenant-admin bootstrap komutu, `IEmailSender` → in-memory outbox → arka plan worker → opsiyonel SMTP, dev mailbox, `identity.membership.invite` aksiyonu, `/auth/me` capabilities. Şema Revision 8/9. | S3a `c7a7602` … `7c59abe`; S3b `a805988`; docs `1cff172` |
| **S4 — Frontend davet & parola yaşam döngüsü** | `/accept-invite`, `/reset-password` (token URL fragment'ından belleğe alınıp adres çubuğundan silinir), `/forgot-password` (nötr onay), `/register` (davet-only ekranı), `/account/security` (parola değiştirme). +64 web testi. | `a8c247d` |
| **S5 — Sertleştirme, E2E, doküman** | Playwright suite'i (gerçek API + geçici PostgreSQL + Chrome, 22 test), `Fynovio.Auth` meter sayaçları (`auth.login.failed`, `auth.refresh.reuse`, `auth.ratelimit.rejected`), SPA'ya `same-origin` referrer politikası, graphify yenileme, bu rapor. | `4340d78`, `dc23890`, `ef953fa`, `3080340`, `80694be` |

## 2. Plan §6 güvenlik tablosu — satır satır kanıt

Test adları repodaki gerçek adlardır (`tests/Host.Tests/Authentication`, `tests/Access.Tests/**`, `web/src/**`, `web/e2e`). "Mutation" sütunu için bkz. §3: yalnızca repoda kaydı olan eşlemeler yazıldı.

### 2.1 XSS ile token çalma
- **Kontrol:** access token yalnızca bellekte; refresh cookie HttpOnly; eski `fynovio-auth` anahtarı boot'ta silinir.
- **Backend:** `Cookie_is_httponly_secure_samesite_strict_with_configured_name_path_and_expiry_and_no_domain`, `Login_with_one_membership_authenticates_and_sets_a_hardened_cookie`.
- **Frontend:** `storageHygiene.test.tsx` — "after a full login → tenant select → refresh → logout flow neither storage holds anything auth-related", "the session store is not persisted", "SessionGate purges the legacy `fynovio-auth` entry at boot", "a legacy persisted 'authenticated' flag cannot restore a session by itself"; `session.test.ts` — "an 'authenticated' payload without a token can never yield an authenticated session".
- **E2E:** `login.e2e.ts` "signs in, survives a reload, holds no token in web storage, and signs out for good".
- **Mutation:** frontend kayıtları içinde (S2: 9); satır eşlemesi repoda yok.
- **Not:** planın "mevcut DOMPurify kuralı korunur" maddesine bu fazda dokunulmadı; ayrıca yeniden doğrulanmadı.

### 2.2 CSRF (cookie'li endpoint'ler)
- **Kontrol:** SameSite=Strict + `X-Requested-With` + Origin/Referer allow-list + `Sec-Fetch-Site`.
- **Backend:** `CsrfGuardAndCookieTests` (`Missing_custom_header_is_rejected`, `Wrong_custom_header_value_is_rejected`, `Foreign_origin_is_rejected`, `Opaque_null_origin_is_rejected`, `Cross_site_fetch_metadata_is_rejected`, `Foreign_referer_is_rejected_when_there_is_no_origin`, `Unparseable_referer_is_rejected`, allow-list ve same-origin geçişleri); `AuthEndpointsTests.State_changing_endpoints_reject_requests_without_the_custom_header`, `…_reject_a_foreign_origin_and_cross_site_fetches`, `…_accept_an_allow_listed_origin`; `InvitationEndpointsTests.Accepting_needs_the_csrf_header_and_a_listed_origin_but_previewing_does_not`; `PasswordEndpointsTests.Reset_needs_the_csrf_header`, `Change_requires_a_bearer_token_and_the_csrf_header`, `Forgot_is_rate_limited_per_client_and_guarded_against_csrf`.
- **E2E:** `isolation.e2e.ts` "state-changing auth calls without the CSRF header, or from a foreign origin, are refused"; `login.e2e.ts` meta-referrer assertion (`ef953fa`) — `no-referrer` same-origin POST'lara `Origin: null` yazdırıp guard'ı kırardı, bu yüzden `same-origin`.
- **Mutation:** S3b endpoint güvenlik assertion'ları 18'lik sette (bkz. §3); S1 CSRF testleri için kayıt yok.

### 2.3 CORS
- **Kontrol:** açık liste (`Authentication:AllowedOrigins`, varsayılan boş), wildcard yok.
- **Backend:** `Cors_preflight_from_a_listed_origin_echoes_that_origin_with_credentials_never_a_wildcard`, `Cors_is_off_entirely_when_no_origin_is_configured`; `AllowedOrigins_CanBeEmpty`, `AllowedOrigins_CanContainMultipleUrls`.
- **E2E:** tüm suite same-origin (Vite proxy) çalışır; CORS'a ihtiyaç duymaz.
- **Mutation:** kayıt yok.

### 2.4 Session fixation / replay
- **Kontrol:** login başına yeni session, refresh rotasyonu, reuse ⇒ session ailesi iptal, access token `sid`'e bağlı ve iptal kontrollü.
- **Access:** `ReuseDetection_RevokesSessionAndLegitimateToken`, `RefreshIdleExpiry_TokenInvalidAfterIdlePeriod`, `RefreshAbsoluteExpiry_SessionInvalidAfterAbsolutePeriod`, `RefreshRevalidatesTenant_DisabledMembershipClearsActiveTenant`, `SessionValidator_IsActiveAsync_CorrectResults`; domain `RefreshTokenTests`.
- **Host:** `Replaying_a_rotated_cookie_revokes_the_whole_session_including_the_new_cookie`, `Two_concurrent_refreshes_with_the_same_cookie_never_both_succeed`, `Logout_clears_the_cookie_and_kills_the_previously_issued_access_token_immediately`, `Me_rejects_missing_forged_expired_sidless_and_revoked_tokens_with_401`; `AccessTokenIssuerTests.Every_token_gets_a_fresh_jti`.
- **E2E:** `login.e2e.ts` "…signs out for good"; `account.e2e.ts` "changing the password keeps this session and signs the other device out cleanly".
- **Mutation:** `d6ccfbf` — reuse iptalini, refresh'te tenant temizlemeyi ve session validator'ın revoked/expired kontrolünü kaldırmak ilgili testleri kırmızıya çevirdi (kayıtlı).

### 2.5 Credential stuffing / brute force
- **Kontrol:** IP ve tanımlayıcı başına rate limit, lockout, tekdüze hata, dummy-hash doğrulaması.
- **Access:** `LockoutExactness_FailedAttemptsLockAtThreshold`; domain `RecordFailedAttempt_AtThreshold_LocksAccount`; `A_wrong_current_password_fails_counts_toward_lockout_and_changes_nothing`.
- **Host:** `The_per_client_login_limit_answers_429_with_retry_after_and_a_problem_body`, `The_per_identifier_login_limit_applies_across_attempts_but_not_to_other_identifiers`, `Token_endpoints_are_rate_limited_per_client_with_retry_after`; `IdentifierRateLimiterTests.TryAcquire_admits_exactly_the_limit_under_concurrency`.
- **Sayaçlar (S5):** `Failed_sign_ins_are_counted_by_reason_and_a_success_is_not`, `A_successful_sign_in_counts_nothing`, `Refresh_token_reuse_is_counted_once_per_detection`, `Refused_requests_are_counted_by_the_policy_that_refused_them` — 4 metrik testi.
- **Frontend:** `client.test.ts` "surfaces Retry-After on a 429"; `LoginForm.test.tsx` 429 durumu.
- **Sınır:** dummy-hash (`PasswordService.VerifyDummy`) var, ama **süre eşitliğini ölçen test yok**; testler yalnızca sonuçların özdeş olduğunu doğrular.
- **Mutation:** metrik 4 (bkz. §3); rate-limit testleri için ayrı kayıt yok.

### 2.6 Hesap enumeration
- **Kontrol:** tekdüze `invalid_credentials`, forgot her zaman 202, geçersiz token durumları tek hata, kayıt nötr.
- **Access:** `IdenticalErrorResults_UnknownWrongLockedNoMembership`, `SelectTenantIdenticalErrors_UnknownDisabledInvitedNoMembership`, `Every_invalid_cause_yields_the_same_null`, `Registering_an_existing_address_looks_identical_and_changes_nothing`, `An_unknown_account_gets_the_same_answer_as_a_wrong_password`.
- **Host:** `Login_failures_are_indistinguishable_for_unknown_wrong_locked_and_credentialless_accounts`, `Selecting_an_unavailable_tenant_gives_the_same_403_for_unknown_disabled_invited_and_foreign_tenants`, `Forgot_answers_202_with_the_same_body_for_a_known_and_an_unknown_address_and_mails_only_the_known_one`, `A_used_an_expired_and_a_made_up_token_are_all_answered_identically`, `A_slow_mail_transport_cannot_slow_the_response_so_timing_does_not_reveal_which_addresses_exist`.
- **E2E:** `login.e2e.ts` "wrong credentials: one generic message, still on the login page"; `account.e2e.ts` "a link the server never issued, and a link without a token, both end on the invalid-link screen"; `isolation.e2e.ts` "a member cannot select a tenant they do not belong to".
- **Mutation:** S3b Host assertion'ları 18'lik sette.

### 2.7 DB'den token çalınması
- **Kontrol:** yalnızca hash saklanır, tek kullanım, kısa TTL, consumed/revoked durumu.
- **Access:** `Only_the_hash_is_persisted_never_the_secret`, `Token_hash_is_unique`, `Database_rejects_an_unknown_purpose`, `Consuming_is_single_use_even_when_many_callers_race`, `An_expired_token_cannot_be_consumed`, `Outstanding_until_expired_consumed_or_revoked`, `The_setup_token_is_single_use_and_expires`; `TokenSecretsTests`.
- **Host:** `Reset_sets_the_password_signs_every_session_out_and_the_link_works_once`, `Reset_refuses_an_expired_link_and_a_weak_password_without_burning_the_link`, `Inviting_again_replaces_the_earlier_link`.
- **E2E:** `account.e2e.ts` "forgot → reset link → new password → sign in; the link works once".
- **Mutation:** S3b/S3a seti (18); satır eşlemesi repoda yok.

### 2.8 Open redirect
- **Kontrol:** `sanitizeReturnUrl` (tek `/` ile başlayan same-origin yol; `//`, `\`, şema, kontrol karakteri, kodlanmış varyantlar reddedilir).
- **Frontend:** `returnUrl.test.ts` — 16 hostile girdili `it.each` (`//evil.com`, `/\evil.com`, `https://…`, `javascript:`, `data:`, `%2F%2Fevil.com`, `/%2Fevil.com`, `/%5Cevil.com`, sekme/newline/NUL, bozuk yüzde kaçışı, boş); `guards.test.tsx` "never follows a hostile returnUrl — falls back to the dashboard (no open redirect)".
- **E2E:** `login.e2e.ts` "an open-redirect attempt (…) never leaves the app"; "a protected deep link goes to login and, after signing in, back to the same page".
- **Mutation:** frontend kayıtları içinde; satır eşlemesi yok.

### 2.9 Host-header poisoning
- **Kontrol:** bağlantılar yapılandırılmış `PublicAppBaseUrl`'den üretilir; `ForwardedHeaders` yalnızca `Authentication:KnownProxies` ile.
- **Host:** `EmailTests.The_link_comes_from_the_configured_base_url_with_the_token_in_the_fragment`; `PublicAppBaseUrl_MustBeAbsoluteHttpUrl`, `PublicAppBaseUrl_RejectsRelativePaths`.
- **Kanıt yok:** `UseForwardedHeaders` + `KnownProxies` bağlaması (`Program.cs:271-281`) için otomatik test yok — yalnızca kod incelemesi.

### 2.10 Secret sızıntısı
- **Kontrol:** parola/token/link log, evidence, event ve yanıtlara girmez.
- **Host:** `No_secret_reaches_the_logs_during_login_refresh_logout_and_a_failed_login`, `No_secret_reaches_the_logs_across_invite_accept_forgot_reset_and_change`, `Cookie_value_never_reaches_the_logs`, `Formatting_a_rendered_message_never_prints_the_token`, `A_failing_transport_changes_no_response_and_leaves_no_secret_in_the_logs`, `Without_an_smtp_provider_nothing_is_sent_and_the_log_says_so_without_the_link`, `Login_rejects_oversized_input_malformed_json_and_wrong_content_type_without_echoing_the_password`.
- **Access:** `EventsHygiene_NoSecretsInEventDetails`, `Email_messages_never_print_their_values`, `No_password_or_hash_reaches_the_audit_events` (ChangePassword ve SelfRegistration), `The_event_and_evidence_never_contain_the_setup_token`.
- **Frontend:** `useFragmentToken.test.tsx` (token adres çubuğundan silinir). `lib/telemetry.ts` yalnızca genel `console.debug('[telemetry]', event, props)` çağırır; auth gövdesi loglamaz.
- **Repo taraması:** takipli dosyalarda Mailtrap kullanıcı adı/parolası yok; `README.md` yalnızca `<mailtrap username>` / `<mailtrap password>` yer tutucularını içerir.
- **Bilinçli istisna:** `bootstrap-tenant-admin` tek kullanımlık kurulum bağlantısını **yalnızca stdout'a** basar (operatör içindir).
- **Mutation:** S3b'deki sızıntı assertion'ları 18'lik sette.

### 2.11 Tenant yükseltme
- **Kontrol:** `tid` yalnızca sunucuda, Active membership doğrulamasından sonra üretilir; `ActorContextMiddleware` her istekte membership'i yeniden doğrular; RLS.
- **Host:** `Selecting_an_unavailable_tenant_gives_the_same_403_…`, `A_token_for_a_tenant_the_account_has_left_is_403_not_401`, `Login_with_several_memberships_requires_a_selection_and_the_selected_token_works`, `Authentication_and_authorization_failures_stay_distinct_401_vs_403`, `Only_a_permitted_member_can_invite_and_only_into_their_own_tenant`.
- **Access (RLS, `AuthRlsAndPermissionTests`):** `Account_context_shows_only_that_accounts_memberships_across_tenants`, `No_context_at_all_shows_no_memberships`, `Account_context_grants_no_write_path`, `Tenant_isolation_behaviour_is_unchanged_by_the_self_view_policy`, `Account_context_is_transaction_local_and_does_not_leak_to_the_next_transaction`, `Auth_events_can_be_inserted_but_never_updated_or_deleted_by_the_runtime_role`, `Shipped_runtime_role_script_keeps_auth_events_append_only`, `Down_removes_tables_and_policy_and_Up_restores_them`.
- **E2E (`isolation.e2e.ts`):** "a member cannot select a tenant they do not belong to"; "editing the tenant claim of an access token gets no access (the signature no longer matches)"; "401 and 403 stay distinct: no token → 401; a token without the grant → 403"; `login.e2e.ts` "an administrator of two tenants chooses one, and can switch to the other".
- **Mutation:** `03bd00f` — `USING(true)`, `FOR ALL` ve eksik `auth_events` REVOKE'u ilgili testleri kırmızıya çevirdi (kayıtlı).

### 2.12 Kayıt (registration) suistimali
- **Kontrol:** varsayılan kapalı; açıkken membership/rol/tenant vermez; production'da onay bayrağı.
- **Host:** `Registration_is_off_by_default_the_route_does_not_exist`, `Registration_when_enabled_creates_an_identity_with_no_tenant_access_and_answers_neutrally`, `Enabling_registration_outside_development_needs_an_explicit_acknowledgement`, `Config_exposes_the_password_policy_and_reports_self_registration_disabled`.
- **Access:** `Registering_creates_an_identity_and_nothing_that_grants_access`, `A_registered_account_can_sign_in_but_lands_in_the_no_membership_state`, `Concurrent_registrations_of_one_address_create_one_account_and_all_look_accepted`.
- **Frontend:** `RegisterPage.test.tsx` (5 test, davet-only durumu).
- **E2E:** `login.e2e.ts` "registration is disabled: the invite-only screen, and the API route does not exist".
- **Mutation:** S3b seti (18).

### 2.13 Transport
- **`Cache-Control: no-store`:** `AuthEndpointsTests.Config_exposes_the_password_policy_and_reports_self_registration_disabled` ve `…Login_with_one_membership_authenticates_and_sets_a_hardened_cookie`, `PasswordEndpointsTests.Forgot_answers_202_with_the_same_body_…`, `InvitationEndpointsTests.A_tenant_administrator_invites_and_the_invitee_accepts_a_new_account_and_is_signed_in`.
- **Cookie bayrakları:** bkz. §2.1.
- **Referrer-Policy — plandan sapma:** plan, token sayfalarında `Referrer-Policy: no-referrer` **header'ı** öngörüyordu. `CsrfOriginGuard.SetNoReferrerPolicy` yardımcısı yazıldı ama **hiçbir yerde çağrılmıyor (ölü kod)**. Fiilî koruma: SPA kabuğunda `<meta name="referrer" content="same-origin">` (`dc23890`, E2E'de doğrulanır: `ef953fa`) + token'ların URL **fragment**'ında taşınması (tarayıcı fragment'ı `Referer`'a koymaz; `EmailTests.The_link_comes_from_the_configured_base_url_with_the_token_in_the_fragment`).
- **Kanıt yok:** HSTS ve HTTPS yönlendirmesi (`Program.cs:287-288`, yalnızca Development dışı) için otomatik test yok.

## 3. Mutation check kayıtları

Yöntem: bir güvenlik davranışı kodda bilinçli olarak bozulur; ilgili testin (ve yalnızca onun) kırmızıya döndüğü görülür, sonra değişiklik geri alınır.

| Küme | Adet | Kaynak | Ayrıntı |
|---|---|---|---|
| Backend | **18** | Plan §16 S3b: "every security assertion was mutation-checked (18 mutations, each killed by exactly its test)" | Tek tek liste repoda yok. |
| Metrik sayaçları | **4** | S5 (`dc23890`) metrik testleri: `AuthMetricsTests` (3) + `Refused_requests_are_counted_by_the_policy_that_refused_them` | Sayı görevle bildirildi; tek tek liste repoda yok. |
| Frontend | **11** | Plan §16 S2: "nine mutation checks recorded in the commit history" | 9'u S2'ye ait; kalan 2'nin hangi slice'a ait olduğu ve hiçbirinin tek tek listesi repoda yok. |
| S1 (yukarıdaki sayıya dahil olup olmadığı kayıtlı değil) | 6 | `d6ccfbf` (3: reuse iptali, refresh'te tenant temizleme, validator revoked/expired) ve `03bd00f` (3: `USING(true)`, `FOR ALL`, eksik `auth_events` REVOKE) | Commit mesajlarında adıyla kayıtlı. |

**Dürüst sınır:** sayılar (18 + 4 backend/metrik, 11 frontend) plan ve commit geçmişindeki kayıtlardır; §2'deki satırlar için yalnızca `d6ccfbf` ve `03bd00f` mutation'ları **adıyla** bir satıra bağlanabildi. Diğer satırlarda "kümede" denmesi, o testin ilgili slice'ın mutation setinin parçası olduğu anlamına gelir; testle mutation arasındaki birebir eşleme bu repoda kayıtlı değildir.

## 4. Sapmalar

| # | Konu | Durum |
|---|---|---|
| D6 | Tenant görünen adı | **Ertelendi.** `TenantLifecycle` stub, tenant entity yok; membership yalnızca `tenantId` taşır, UI "Tenant {id}" gösterir. |
| D7 | Kayıt için e-posta doğrulaması | **Ertelendi.** Production'da kaydı açmak `AcknowledgeUnverifiedEmail=true` ister, yoksa başlangıçta hata verir (`Enabling_registration_outside_development_needs_an_explicit_acknowledgement`). |
| D10 | Frontend CI | `web/.github` workflow'u monorepoda pasif. Frontend + Playwright job'larının root `ci.yml`'e (path-filtered) taşınması **takip işi**. |
| — | npm script adı | Planda `test:e2e`, gerçekte `npm run e2e`. |
| — | Yeni şema değişikliği | S3b/S4/S5'te yok; Revision 8/9 (S1/S3a) tüm tabloları ve politikaları kapsıyor. |
| — | Bootstrap komutu | Planda S1'de, S3'e taşındı (tek kullanımlık kurulum token'ı `account_tokens` ister). |
| — | E-posta gönderimi | İstek yolunda değil; sınırlı in-memory outbox + arka plan worker (aksi halde forgot/invite süresi hangi adreslerin var olduğunu ele verirdi). SMTP opt-in ve additive; D5 "sağlayıcı ertelendi" şimdi "genel SMTP var, vendor açık". |
| — | `/auth/password/change` yanlış mevcut parola | 400 `invalid_current_password` (401 değil; SPA refresh interceptor'ı bunu süresi dolmuş oturum sanırdı). |
| — | Referrer-Policy | `no-referrer` header'ı yerine SPA meta etiketi `same-origin` (bkz. §2.13). |
| — | `/auth/me` `capabilities` | S1'de yok, S3b'de eklendi (`identity.membership.invite` aksiyonu gerekiyordu). |

## 5. Test sayıları (son tam regresyon)

| Süit | Sonuç |
|---|---|
| `Access.Tests` | 258 |
| `CRM.Tests` | 135 |
| `MasterData.Tests` | 30 |
| `Host.Tests` | 149 |
| .NET toplamı | 572 |
| Web (vitest) | 249 test, 34 dosya |
| Playwright E2E | 22 / 22 |
| `dotnet format` | temiz |
| `npm run build` | başarılı |

Bu değerler `dc23890` sonrasındaki son tam regresyondan gelir. Sonraki commit'ler (`ef953fa`, `3080340`, `80694be`) yalnızca bir E2E assertion'ı, plan metni ve graphify çıktısı değiştirir; `ef953fa` tam E2E ile geçti.

## 6. Artık riskler

1. **In-memory outbox ve rate limiter instance başına.** E-posta outbox'ı kalıcı değil (çökmede kuyruktaki mail kaybolur); IP/tanımlayıcı rate limiter'ları çok instance'lı dağıtımda paylaşılmaz. Kalıcı outbox ve paylaşımlı store takip işi.
2. **Hosting katmanında güvenlik başlıkları yok:** CSP, `frame-ancestors` ve `X-Content-Type-Options` ne API'de ne SPA kabuğunda ayarlı (kod ve `index.html` taraması boş döndü).
3. **`/dev/mailbox` kimlik doğrulamasız.** Yalnızca Development'ta eşlenir (`The_dev_mailbox_does_not_exist_outside_development` ile doğrulanır), ama Development'ta içerik herkese açıktır.
4. **Kayıt akışında küçük timing farkı.** Hash maliyeti eşit; fark yalnızca insert'in yapılıp yapılmamasından gelir. Login'de de dummy-hash için süre testi yok (§2.5).
5. **Gerçek SMTP (Mailtrap) teslimatı DOĞRULANMADI.** Sandbox'ta ağ yok; SMTP yolu yalnızca testlerde kapalı/başarısız transport ile denendi. Kullanıcının kendi ortamında doğrulaması gerekir (bkz. README "Mailtrap").
6. **Bootstrap iki adım arasında çökerse credential'sız hesap kalabilir** (schema Rev 9). Bu çökme senaryosu için otomatik test yok.
7. **S3 öncesi bootstrap edilmiş dev DB'lerinde `identity.membership.invite` grant'i yok.** Bu tenant'larda davet oluşturulamaz; yeniden bootstrap veya elle grant gerekir.
8. **Nested `web/.claude` pasif.** İçe aktarılan frontend'in kendi `.claude` yapılandırması etkin değildir.
9. **Production e-posta sağlayıcısı hâlâ açık** (genel SMTP var, vendor kararı yok).

Rapor sırasında ek olarak tespit edilenler (kod değiştirilmedi):
- `CsrfOriginGuard.SetNoReferrerPolicy` çağrılmayan ölü kod; ya kullanılmalı ya silinmeli.
- HSTS/HTTPS yönlendirmesi ve `ForwardedHeaders`/`KnownProxies` bağlaması için otomatik test yok.
- Mutation check'lerin çoğunun tek tek kaydı yok (§3).

## 7. Takip işleri
- D10: frontend + Playwright CI'ın root `ci.yml`'e taşınması.
- Kalıcı e-posta outbox'ı, paylaşımlı rate-limit store'u.
- Production e-posta sağlayıcısı kararı; e-posta doğrulaması (D7); tenant görünen adları (D6).
- Hosting katmanı için CSP / `frame-ancestors` / `X-Content-Type-Options`.
- Ölü `SetNoReferrerPolicy` kararı; HSTS/forwarded-headers testleri.
- Phase 2.5B (CRM UI) — bu fazın kapsamı dışında, başlatılmadı.
