# Phase 2.5B — CRM Opportunity Frontend Integration: Final Report

> **Durum:** kod, testler ve dokümantasyon tamam. **Dal:** `feat/phase-2-5b-crm-opportunity-frontend` (henüz `main`'e merge edilmedi). **Plan:** `PHASE_2_5B_CRM_FRONTEND_IMPLEMENTATION_PLAN.md`. Phase 3 (Customer Need Intelligence) başlatılmadı.
> Her iddia bir teste, commit'e veya koda dayanır; kanıtı olmayan yer "kanıt yok" diye işaretlenmiştir.

## 1. Uygulanan rotalar / ekranlar / bileşenler

| Rota | Ekran | Not |
|---|---|---|
| `/crm/opportunities` | Liste | durum filtresi (`?status=`), sayfalama (`?page=`, `take+1` ile "sonraki sayfa var mı"), yükleniyor / boş / filtreli-boş / 403 / hata+yeniden-dene / arka-plan yenileme durumları |
| `/crm/opportunities/new` | Oluşturma formu | react-hook-form + zod; yalnızca Phase 2 sözleşmesindeki alanlar |
| `/crm/opportunities/:id` | Detay | özet, aşama kartı, kalem kartı, işlem çubuğu; 404 / 403 / hata durumları |

Bileşenler: `web/src/features/opportunities/` — `api.ts` (anahtar fabrikası, sorgular, komutlar), `schema.ts` (zod + wire adaptörleri), `lib/problem.ts` (ProblemDetails→UX tek eşleme), `lib/useKeyedCommand.ts`, `components/{SummaryCard,PipelineCard,LinesCard,LifecycleDialogs,LineDialogs,CommandDialog,OpportunityForm,OpportunityTable,ProblemNotice,QueryProblemState,ApiModeChip,OpportunityStatusBadge}`. Altyapı: `lib/mutations/{attemptKey,useSingleFlight}.ts`, `lib/apiMode.ts`. CRM kenar çubuğuna "Fırsatlar" girişi eklendi (`nav.ts`), TR + EN katalog (`locales/{tr,en}/opportunities.ts`, anahtar eşitliği testli).

## 2. Tüketilen gerçek API uç noktaları

`GET /opportunities` · `GET /opportunities/{id}` · `GET /opportunities/{id}/actions` · `GET /pipelines/{versionId}/stages` · `POST /opportunities` · `…/{id}/lines` · `…/{id}/lines/{lineId}/cancel` · `…/{id}/open` · `…/{id}/stage` · `…/{id}/win` · `…/{id}/lose`. `…/{id}/reassign` sözleşmeye bağlı ve birim-testli ama **hiçbir kontrole bağlı değil** (§4, OD1). Hiçbir Opportunity/pipeline isteği mock'lanmaz; bunu bir vitest koruması zorlar (`src/mocks/noOpportunityMocks.test.ts`) ve geliştirme modunda sayfa başlığında "Gerçek API · /api" göstergesi vardır.

## 3. Authorization / FLS / obligation entegrasyonu

- **Backend-otoriter aksiyonlar:** Open / Aşama değiştir / Win / Lose / Reassign görünürlüğü yalnızca `GET …/actions` projeksiyonundan gelir. Projeksiyon yüklenmemişse ya da hata verdiyse **hiçbir** yaşam döngüsü kontrolü sunulmaz (fail-closed) ve açık bir bildirim gösterilir. Test: durumu `Draft` ama `canOpen=false` iken buton yok.
- **Aşamalar:** ad kodlanmamış, enum yok; adlar `/pipelines/{versionId}/stages`'ten, seçilebilir hedefler tam olarak `allowedTargetStageIds`'in `sortOrder` sıralı adlandırılmış hâlidir (pasif ve mevcut aşama asla çıkmaz — E2E'de tam liste doğrulanır).
- **Kayıt düzeyi:** reddedilen okuma ile olmayan/başka-tenant kaydı API'de aynı 404'tür; UI ikisini ayırmaz (E2E: `single` kullanıcısı ve tenant-2 token'ı).
- **Alan düzeyi READ/WRITE ve obligation/`RequireApproval`:** Phase 1.5 bunları bilinçli olarak içermez (`PHASE_2_CLOSURE_REPORT.md` §1). UI, DTO'nun taşıdığını olduğu gibi gösterir; şemadaki alanlar `nullish` olduğundan ileride maskelenen bir alan "yok" olarak render edilir, yeniden türetilmez. Onay/engel UI'ı eklenmedi. E2E matrisindeki "Field READ/WRITE" satırı **görünür bir `test.skip`** olarak durur (plan G7).
- **Paralel izin modeli yok:** Opportunity özelliği `usePermission`/`mockPermissionPolicy` kullanmaz.
- **Belgelenmiş tek istisna (G4):** backend'de `canAddLine`/`canCancelLine` yok; bu iki kontrolün *sunulması* `lineEditability()` içinde tek yerde yaşam döngüsünden türetilir, backend her çağrıda yeniden yetkilendirir. Backend projeksiyonu bunları eklediği an tek dosya değişir.

## 4. Concurrency ve idempotency davranışı

- **Concurrency:** her komut kullanıcının gördüğü `rowVersion`'ı `expectedVersion` olarak yollar (tip düzeyinde zorunlu: `CommandBase`). 409 `concurrency_conflict` → satır içi uyarı + **Son hâlini yükle**; girilen değerler korunur, hiçbir şey otomatik yeniden gönderilmez, sunucudaki daha yeni sürümün üzerine yazılmaz. Kanıt: vitest (`… stale write … never retries by itself`) ve E2E 8 (iki oturum: B kalem ekler, A bayat sayfadan Open dener → çakışma, sunucu durumu değişmez, yenile → B'nin satırı görünür, A açıkça yeniden gönderir → başarılı).
- **Idempotency:** mantıksal eylem başına bir anahtar (`AttemptKeys`). Sonucu bilinmeyen (ağ hatası, timeout, 5xx, 408/429) durumda anahtar **tutulur** ve kullanıcının yeniden denemesi aynı anahtarı kullanır; kesin yanıtta (2xx/4xx) bırakılır; farklı gövde her zaman yeni anahtar alır (aynı anahtar + farklı gövde 409 olurdu). Anahtar axios interceptor'ında değil çağrı anında açıkça set edilir → 401→refresh→replay aynı başlığı taşır. Aynı-tick çift tıklama tek istek üretir (`useSingleFlight`). Kanıt: `attemptKey.test.ts` (5+9), vitest (ağ hatası sonrası aynı anahtar/gövde; değer değişince yeni anahtar; çift tıklama tek istek), E2E 9a (UI çift tıklama → tam bir kayıt), E2E 9b (HTTP: aynı anahtar+gövde → `replayed: true` ve tek kayıt; aynı anahtar+farklı gövde → 409 `idempotency_key_reused`).
- **Sunucu onaylı güncelleme:** yaşam döngüsü/yetki hassas hiçbir mutasyonda iyimser (optimistic) güncelleme yok; başarıdan sonra detay + aksiyonlar + listeler invalidate edilir.

## 5. Tenant değişimi ve cache izolasyonu

İki bağımsız mekanizma: (1) tüm sorgu anahtarları aktif tenant id'siyle köklenir (`opportunityKeys`, `stages`); (2) `selectTenant`/login/logout/expiry `queryClient.cancelQueries()` + `clear()` çağırır (`cancelQueries` bu fazda eklendi). Kanıt: `tenantIsolation.test.tsx` (5 test; iki mutasyon — `clear()`'ı kaldırmak ve anahtar kökünden tenant'ı çıkarmak — ilgili testleri kırmızıya çevirdi) ve E2E 10 (tenant 1'de oluşturulan kayıt tenant 2'de listede yok, detay URL'i 404 durumu, API'de 404 gövdesi olmayan-id ile birebir aynı).

## 6. Hata durumu uygulaması

`lib/problem.ts` tek eşleme katmanıdır (`toApiError` zaten tek ayrıştırma katmanı; ikinci ayrıştırıcı eklenmedi). Sınıflar: validation, unauthorized, forbidden, notFound, concurrency, idempotencyConflict, lifecycle, pipeline, rateLimited, unavailable. Sunucu metni yalnızca `validation_error`, `illegal_lifecycle_transition`, `invalid_pipeline_transition` için (backend'in bilerek yazdığı alan cümleleri) gösterilir; 404/403/5xx/ağ için yalnızca yerelleştirilmiş genel metin (test: `NullReferenceException` içeren 500 gövdesi hiçbir yerde görünmez). 401 → 2.5A oturum kurtarma (E2E 11: ilk liste GET'i 401, sayfa yenilenip login'e düşmeden listeyi gösterir). Sorgu hataları sayfa durumudur (`throwOnError: false`), sayfa çökmesi değil.

## 7. Eklenen testler ve durum

| Küme | Sonuç |
|---|---|
| Web (vitest) | **363 test / 43 dosya yeşil** (önceki 249'a göre +114'ü bu fazda) |
| Playwright E2E (gerçek API + geçici PostgreSQL + Chrome) | **38/38** (22 mevcut auth testi + 16 Opportunity; regresyon yok) |
| `Host.Tests` | 150/151 — tek kırmızı `EmailDeliveryTests.Enabling_smtp_without_a_host_fails_at_start_up`; bu makinedeki Mailtrap user-secrets (`Email:Smtp:Host`) yüzünden önkoşulu sağlanmıyor, bu faz öncesinden var olan ve ilgisiz bir test (2.5A raporu §6 madde 5'teki user-secrets ile aynı kök). Yeni `DevSeederTests` (3 yeni) yeşil |
| `dotnet format --verify-no-changes` | temiz |
| `npm run check` + `npm run build` | temiz |

Mutasyon kontrolleri (bu oturumda yapılan, geri alınan): tenant cache `clear()` kaldırıldı → 3 test kırmızı; anahtar kökünden tenant çıkarıldı → 2 test kırmızı; `useOpportunity`'nin `enabled` düzeltmesi kaldırıldı → yeni "geçersiz id API'ye gitmez" testi kırmızı.

## 7a. Süreç notu (subagent-driven-development)

Testlerin bir kısmı (liste/create, çapraz-kesen korumalar, E2E yazımı) subagent'lara verildi; her biri spec incelemesinden geçti. Bulgular: (a) ilk liste/create testleri 4 spec boşluğu içeriyordu (link, URL sıfırlama, sayfa sıfırlama, hata cümlesi) — düzeltildi; (b) tenant-izolasyon testleri iki noktada boş geçiyordu (yavaş isteği beklemeden yokluk kontrolü; paylaşılmayan client) — düzeltildi ve mutasyonla kanıtlandı; (c) E2E spec'i hiç koşulmadan yazıldı ve ilk koşuda 13/16 kırmızıydı (CRM kenar çubuğu dashboard'da yoktur; sabit `expectedVersion: 0`; kısa buton adlarının çoklu eşleşmesi; gerçek API davranışları: replay `201`, reddedilen okuma `404`, gövdesiz 404) — hepsi gerçek API'ye karşı koşup düzeltildi; (d) **üretim kodu için ayrılan otomatik kod incelemesi güvenilmezdi**: "sorun yok" dedi ama raporunda doğrulanabilir bir yanlış vardı (Türkçe toast metinlerini katalogda olmayan bir şekilde alıntıladı). Bu rapor kanıt sayılmadı; onun yerine gerçek API'ye karşı E2E ve tek tek doğrulanan kontroller kullanıldı. Bağımsız bir insan/ikinci-model kod incelemesi yapılmadı (bkz. §10).

## 8. Güvenlik incelemesi (spec §26 kontrol listesi)

| Madde | Sonuç | Kanıt |
|---|---|---|
| Auth access-token kalıcılığı yeniden getirilmedi | ✔ | E2E 1 ve 2 `expectNoTokenInStorage`; hiçbir yeni dosya storage kullanmaz |
| Tenant id form verisinden alınmıyor | ✔ | Create gövdesi tam olarak `{partyId,currency,estimatedAmount}` (vitest `toEqual`); tenant/sahip alanı yok testi |
| Yalnızca-frontend yetki varsayımı yok | ✔ (G4 istisnası belgeli) | aksiyonlar projeksiyondan; projeksiyon yoksa hiçbiri |
| Hassas alan cache/log/telemetri'ye sızmıyor | ✔ kısmen | telemetri yalnızca `mutationKey`/hata iletisi loglar, değişken/anahtar loglamaz; alan düzeyi güvenlik yok (G7) → "kanıt yok" |
| Tenant değişiminden sonra cross-tenant cache yok | ✔ | §5 |
| Reassign'da rastgele principal listelemesi yok | ✔ | hiçbir UI principal toplamaz; `reassignContract.test.tsx` hiçbir bileşenin hook'u kullanmadığını doğrular; E2E 5 (viewer → 403) |
| Sürüm çakışması yönetimi olmadan komut yok | ✔ | `CommandBase.expectedVersion` zorunlu |
| Idempotency anahtarı loglanmıyor | ✔ | anahtar yalnızca istek başlığında |
| Ham ProblemDetails/stack kullanıcıya gösterilmiyor | ✔ | §6 |

## 9. Ertelenen maddeler ve nedenleri

- **OD1 — Reassign:** uygun-atanan (eligible-assignee) kaynağı yok. Spec §12'nin kendi geri dönüşü uygulandı: komut bağlı ve testli, ama UI yalnızca `canReassign=true` iken açık bir "üye dizini henüz yok" bağımlılık notu gösterir. Öneri: tenant için yetkili bir atanabilir-principal sorgusu (backend). **Karar sizin.**
- **OD2 — üretimde CRM yetkisi:** `BootstrapTenantAccessHandler` yalnızca Access'in kendi eylemlerini verir; `crm.*` yetkisi bugün **yalnızca Development seed'i** ile gelir (bu fazda eklendi). Gerçek bir tenant yöneticisi üretimde CRM'i kullanamaz. Phase 1.5'in açık "sistem-şablon yaşam döngüsü" kararına bağlı. **Karar sizin.**
- **G2/G3:** Party ve ürün arama uç noktaları yok → formlarda sayısal kimlik girişi; detayda "Taraf #id". **G5:** liste özetinde taraf/aşama-sürümü/rowVersion/toplam yok → listede aşama adı ve satır içi mutasyon yok. **G6:** Phase 2'de skaler alan güncelleme komutu yok → "Düzenle" = kalem düzenleyici + Open'ın son geçerlilik tarihi. **G8:** sahip için görünen ad yok.
- Görsel/tema doğrulaması (claude-in-chrome ile açık/koyu tema, dar viewport) **yapılmadı**; yalnızca Playwright (Chrome, varsayılan tema) ve jsdom testleri koştu.
- `web/.github` CI'ının kök `ci.yml`'e taşınması (2.5A D10) hâlâ takip işi; bu faz da yeni web+E2E testlerini CI'a bağlamadı.

## 10. Bilinen sınırlamalar

- Liste toplam sayı vermez (sözleşmede yok); sayfalama "önceki/sonraki".
- Sahibin görünen adı, müşteri adı, ürün adı yok (yalnızca kimlikler).
- Bu makinede E2E'nin varsayılan portu 5174 başka bir `node` süreci tarafından tutuluyordu (dokunulmadı); E2E `E2E_APP_PORT=5184` ile koşuldu.
- Bağımsız kod incelemesi (insan veya ikinci model) yapılmadı; `requesting-code-review` adımı için ayrılan otomatik inceleme güvenilmez çıktığından yerine kanıt tabanlı kontroller kullanıldı (§7a). Merge öncesi gerçek bir inceleme önerilir.
- Backend değişikliği yalnızca Development seed'i: `CrmDevSeed.cs` (+ `DevSeeder.cs`, testler). Şema/migration/Phase 2 sözleşmesi değişmedi.

## 11. Phase 3 hazırlık ifadesi

**READY** (geliştirme kapsamı için): Opportunity ekranları, gerçek Phase 2 API'sine ve Phase 1.5 yetkilendirmesine bağlı; Customer Need alanı DTO'da olmadığı için dokunulmadı. **Üretim öncesi engel:** OD2 (CRM yetkilerinin üretimde nasıl verileceği); **kullanılabilirlik boşluğu:** OD1, G2, G3 (dizin/arama uç noktaları).
