# Phase 2.6 — CRM Production Readiness & Reference Queries: Final Report

Tarih: 2026-09-20 · Dal: `feat/phase-2-6-crm-production-readiness` (henüz `main`'e merge edilmedi, push edilmedi) · Plan: `docs/plans/crm-phase2-6/2026-09-20-crm-production-readiness-plan.md`

## 1. Sonuç

Phase 2.5B'nin iki açık sahip kararı (OD1, OD2) ve Party araması (G2) kapatıldı; Product araması (G3) **bilinçli olarak yapılmadı**. Yeni bir bağlayıcı mimari karar gerekmedi: Phase 1.5'in donmuş kuralı (şablon → kiracıya özel kopya, reconciler yok, sessiz yayılım yok) korunarak ilerlendi.

| Kalem | Durum | Kanıt |
|---|---|---|
| **OD2** genel modül yetki kataloğu + versiyonlu sistem rol şablonu + kiracı modül etkinleştirme | Tamam | T1 `e01c3b7`, T2 `f734c73` |
| **OD1** Opportunity'ye duyarlı, yetki-farkında Assignable Principals sorgusu + Reassign UI | Tamam | T3 `d60abfc`, T5 `39a5531` |
| **G2** Party arama + müşteri seçici | Tamam | T4 `519b9be`, T5 |
| **G3** Product arama | **Yapılmadı (tasarım gereği)** | Bkz. §5 |
| E2E / güvenlik / entegrasyon testleri | Tamam | T6 `ecf6886` |

## 2. Ne inşa edildi

### OD2 — modül yetki kataloğu (production blocker)
- **Contracts:** `ModuleCapabilityManifest` (izin kümeleri + rol şablonları; `Validate()` joker karakteri ve tanımsız eylemi reddeder), `PermissionSetTemplate`, `RoleTemplate(GrantToTenantAdministrators)`.
- **Access:** `EnableTenantModuleHandler` — şablonun *o anki* sürümünü kiracıya özel `Role`/`PermissionSet` satırlarına kopyalar (`origin_module_key` + `origin_version`), yönetici olarak işaretli rolleri kiracının *mevcut* yöneticilerine atar, `TenantAccessRevision`'ı artırır, kanıt + outbox'ı aynı transaction'da yazar. Durumla idempotent: `AlreadyEnabled` hiçbir şeyi değiştirmez (şablon sonradan güncellense bile — reconciler yok). Kiracının kendi yazdığı çakışan anahtarı asla devralmaz (`TemplateKeyConflict`). Kayıt defterinde aktif olmayan eylem anahtarında fail-closed. Yeni `access.tenant_module_enablements` tablosu (UNIQUE tenant+module, RLS enabled+forced; migration `20260920165442`).
- **CRM:** `CrmModuleCapabilities` v1 — `crm_viewer`, `crm_sales_representative`, `crm_manager` (+ yöneticiler); `crm.*` joker yok, açık eylem anahtarları (`CrmActionKeys`).
- **Host:** `PlatformModules` modül manifestlerinin ve eylem kayıt defterinin tek birleşimini kurar (Access CRM'e referans vermez). Operatör komutları: `enable-tenant-module --tenant-id N --module crm` ve `bootstrap-tenant-admin ... --modules crm` (modüller *hiçbir şey yaratılmadan önce* doğrulanır).
- **Dev seed artık production yolunu kullanır:** `CrmDevSeed`'deki rol seed'i silindi; `DevSeeder` aynı `EnableTenantModuleHandler`'ı çağırır. Yalnızca pipeline ve örnek Party'ler dev-only kaldı. Yeni dev kullanıcısı `rep@fynovio.local`.

### OD1 — Assignable Principals
- `IAuthorizedPrincipalDirectory` (Contracts) → `AuthorizedPrincipalDirectory` (Access): aktif üyeler, platform issuer kimliği, gerekli tüm eylemler `owner` olarak PDP tarafından izinli. CRM gereksinimi: `crm.opportunity.read` + `crm.opportunity.change_stage` (salt-okur biri atanamaz).
- `GET /opportunities/{id}/assignable-principals` — kayıt üzerinde `crm.opportunity.reassign` ile korunur; kapalı fırsat → boş liste; mevcut sahip listelenmez; `search`/`take` sunucuda.
- `ReassignOpportunityHandler` hedefi **sunucuda yeniden doğrular** (`422 principal_not_assignable`); istemci listesi güvenilmez.
- Frontend aday süzmez: `ReassignDialog` yalnızca sorgunun döndürdüğünü gösterir; 422'de seçim temizlenir ve liste sunucudan yeniden sorulur.
- **Team/org/territory/record-policy:** Phase 1.5'te bu olgu sağlayıcıları **yok** (gap-closure §4 rezerve etmeyi reddetmişti). Dizin PDP'nin bugün değerlendirdiği her şeyi tüketir; PDP büyüdüğünde genişleme noktası dizinin kendisidir ve `AuthorizedPrincipalDirectoryTests` (PDP ile üye-üye uyum testi) uyumsuzluğu yakalar.

### G2 — Party arama
- `IPartySearch` (Contracts) → `PartyDirectory` (MasterData): kiracı kapsamlı, kaçışlı `ILIKE`, üst sınırlı, yalnızca kanonik (birleştirilmemiş) Party.
- `GET /crm/references/parties?search|ids` — yeni CRM eylemi `crm.reference.party.search`.
- **Bulunan gizli hata (düzeltildi):** `PartyDirectory` ve `PartyIdentityResolver` `app.tenant_id` GUC'unu hiç set etmiyordu; RLS'li runtime rolünde **hiç satır dönmüyordu**. Mevcut testlerin hepsi admin bağlantısıyla çalıştığı için gizli kalmıştı. Düzeltildi; runtime-rol regresyon testi eklendi (mutasyonla doğrulandı).
- UI: sayısal müşteri alanı yerine mevcut `AsyncCombobox` üzerinde `PartyPicker`. 403'te fail-closed: manuel id girişi yok. Detay sayfası müşteri adını gösterir (yetki yoksa `Taraf #id`).

## 3. Test kanıtı

| Paket | Sonuç |
|---|---|
| Access.Tests | 301 / 301 |
| CRM.Tests | 163 / 163 |
| MasterData.Tests | 38 / 38 |
| Host.Tests | 184 / 185 — tek kırmızı `EmailDeliveryTests.Enabling_smtp_without_a_host_fails_at_start_up` |
| Web vitest (`npm run check`) | 380 / 380 (2.5B sonunda 363), lint + typecheck temiz; `npm run build` temiz |
| Playwright (gerçek API + PostgreSQL + Chrome) | **40 / 40** (22 auth + 18 Opportunity; 2.5B'de 38) |
| `dotnet format --verify-no-changes` | temiz |

**Ön-var olan, bu işle ilgisiz kırmızı test:** `Enabling_smtp_without_a_host…` — geliştirici makinesindeki user-secrets'ta (Mailtrap) `Email:Smtp:Host` tanımlı olduğu için "host yok" senaryosu oluşmuyor. Phase 2.6 dosyalarına dokunmuyor.

**Mutasyon kontrolleri** (her koruyucu testte kodu bozup testin kırıldığı görüldü; kırılmayan bir test olursa gereksiz kod silindi):
- Access: template kopyası/çakışma/idempotency/RLS/CHECK testleri; dizin–PDP uyum matrisi (PDP kuralı değişince test kırılıyor).
- Host: assignable-principals endpoint testleri (yetki, kiracı, kapalı fırsat); party runtime-rol regresyonu.
- Web: (a) 422'de seçimi temizleme kaldırılınca 422 testi kırıldı; (b) `expectedVersion` bozulunca gönderim testi kırıldı; (c) temizlenen seçimde eski party id'si gönderilirse "stale id" testi kırıldı; (d) "422'de yeniden yükleme için ayrı remount" mutasyonu testi kırmadı — çünkü seçimi temizlemek zaten yeniden aramayı tetikliyordu; gereksiz `key` remount'u **silindi**.

E2E, gerçek API'de şunları kanıtlar: tam akış (müşteri seçilerek oluşturma, özette müşteri adı); Reassign yalnızca sunucunun listelediği kişiyi (`Dev Sales Representative`) sunar — viewer, yetkisiz üye ve mevcut sahip listede yok; viewer/rep için Reassign kontrolü yok ve API 403; doğrudan API ile viewer'a veya yabancı issuer'a atama **422**; başka kiracı yöneticisi aday listesine 404 alır; Party sorgusu kiracı izole (t2 "Umbrella" t1'de bulunmaz, id ile de çözülmez), viewer için 403 ve picker "yetkiniz yok" der, yazılabilir id alanı yoktur.

## 4. Operatör el kitabı
Root `README.md`: "Enabling a business module for a tenant (production)" ve `--modules` (çıkış kodları: `0` ok/AlreadyEnabled, `2` yapılandırma kapalı, `4` kiracı bootstrap edilmemiş, `5` şablon anahtar çakışması, `64` hatalı argüman/bilinmeyen modül). Şema: `docs/schema/identity-access-schema.md` Revision 10. `AGENTS.md` durum paragrafı güncellendi.

**Gerçek bir kiracıyı Opportunity'ye hazırlama sırası** (boşluk kapatma sonrası, hepsi operatör/HTTP, seed yok):
1. `bootstrap-tenant-admin --tenant-id N --email E --display-name X --modules crm` (yönetici + CRM rolleri).
2. `provision-crm-pipeline --tenant-id N --name "Sales pipeline" --stages "Qualification,Proposal,Negotiation"` — aşamaları operatör verir, ilki giriş aşamasıdır; çıkış kodları `0` ok/zaten var, `2` yapılandırma kapalı, `4` kiracı bootstrap edilmemiş, `64` hatalı argüman.
3. Müşteri: `POST /crm/references/parties` (`Idempotency-Key`, gövde `{ partyType, name, surname?, phone?, email? }`, `crm.reference.party.create`) — **yalnızca API**; arayüzde "yeni müşteri ekle" düğmesi yok.
4. `POST /opportunities` artık müşterinin varlığını doğrular (`422 party_not_found`).
Ayrıntı: root `README.md` ("Giving a tenant its first sales pipeline", "Customers (Parties) and opportunities").

**Dev veritabanı notu:** Phase 2.6 öncesi oluşturulmuş bir dev DB'de `crm_*` roller şablon dışı olarak zaten var; etkinleştirme bunları **devralmaz** (`TemplateKeyConflict`, seed uyarı loglar). Dev DB'yi yeniden oluşturun.

## 5. G3 — Product arama neden yapılmadı
Repoda Product master yok; `AddOpportunityLine` `EntityRef(tenant, "masterdata", "product", id)` üretir ama masterdata böyle bir kavramın sahibi değil. Bir ürün kataloğu uydurmak, sizin koyduğunuz sınırı (CRM içinde yeni ürün sahipliği/domain'i uydurma) ihlal ederdi. Sayısal `productId` girişi kaldı; alanın ipucu artık "ürün kataloğu henüz yok, kimlik sunucuda doğrulanmaz" der. Askıdaki `masterdata/product` referansı belgelendi. Bir Product master'ı olan modül (Inventory/Sales) geldiğinde `IPartySearch` ile aynı örüntü (Contracts arayüzü + tüketen tarafta eylem) uygulanır.

## 6. Bilinen boşluklar (bu kapsamda inşa edilmedi)
- ~~**P1** Production'da pipeline yaratan yol yok~~ → **kapatıldı** (bkz. §6a).
- ~~**P2** Party yaratan HTTP yolu yok; `CreateOpportunity` party'yi doğrulamaz~~ → **kapatıldı** (bkz. §6a).
- **L-3** MasterData'nın kendi yetkilendirme katmanı yok; hem okuma (`crm.reference.party.search`) hem yazma (`crm.reference.party.create`) yolunu CRM eylemi koruyor. Yeni bir katman bağlayıcı bir mimari karardır.
- Pipeline'ı provisioning'den **sonra** değiştirme yolu yok (aşama ekle/kaldır/yeniden sırala) — ayrı karar.
- Detay sayfasında sahip hâlâ ham `subject` olarak gösteriliyor (görünen ad için ayrı bir sorgu gerekir).
- Şablon **yükseltme** yolu (kiracıyı v1'den v2'ye taşıma) yok — Phase 1.5 kararı gereği yeni bir karar gerektirir.
- Yeni bir sistem (Sales, Inventory) eklemek: manifest yaz + `PlatformModules`'e ekle; başka kod gerekmez.

## 6a. Boşluk kapatma (aynı gün, "boşlukları hızlıca kapat")
Yalnızca yeni mimari karar gerektirmeyenler; her biri ayrı commit:
- **P2b** `CreateOpportunity`, müşteriyi `IPartyIdentityResolver` ile doğrular (sözleşmesinin kendi tarif ettiği komut-önkoşulu yüzeyi). Bilinmeyen/başka kiracı party → `422 party_not_found` (varlık oracle'ı yok). Çözümleme idempotency **replay kontrolünden sonra** (party sonradan birleşse/silinse bile replay çalışır), saklanan ref **merge survivor**, istek hash'i **girdi ref**'i üzerinde (iki deneme arası merge, retry'ı `idempotency_key_reused`'a çevirmez). Kabul edilen TOCTOU: resolver MasterData context'inde, CRM transaction'ı dışında okur. → `de4a389`
- **P1** Aşama şablonu **uydurmadan**: operatör aşamaları verir (`provision-crm-pipeline --tenant-id N --name ... --stages "A,B,C"`), ilk aşama giriş aşamasıdır. Durum bazında idempotent (pipeline'ı olan kiracıya dokunmaz), bootstrap edilmemiş kiracıyı reddeder, dev seed aynı handler'ı kullanır. → `d9a61bd`
- **P2a** `POST /crm/references/parties` (`crm.reference.party.create`): MasterData `IPartyRegistration` ile Contracts üzerinden yazma yüzeyi sunar; CRM önce yetkilendirir, sonra girdiyi doğrular ve devreder. **Şablon notu:** yeni eylem anahtarı CRM **v1**'e *yerinde* eklendi, v2'ye çıkılmadı — etkinleştirme bir kez kopyalar (reconciler yok), v2 olsaydı halihazırda etkinleştirilmiş her kiracı yeni yetkiyi hiç alamazdı; production'da kiracı yok. **İlk production kiracısından itibaren şablon içeriği değişikliği sürümü artırmak zorundadır.** → `939c1d9`
- **Bilinçli olarak kapatılmadı** (her biri sahip kararı ister): L-3, şablon yükseltme yolu, owner'ın görünen adı, pipeline'ı sonradan düzenleme.
- **Test:** CRM 185/185, MasterData 42/42, Access 301/301 (bir çalıştırmada 1 test kırmızı çıktı, aynı kodla tekrarında 301/301 geçti; hangi testin olduğu yakalanamadı — Access'e dokunulmadı, kararsız/zamanlamaya bağlı bir test olarak not edildi), Host 205/206 (tek kırmızı önceden beri bilinen Mailtrap user-secrets testi), Playwright 41/41 (P1, P2a ve P2b'nin üçü de uygulanmış halde, gerçek API + Chrome; yeni senaryo `9a-2` `party_not_found`).

## 7. Doğrulanamayanlar / dikkat
- **Görsel/tema kontrolü yapılmadı** (tarayıcı aracıyla açık/koyu tema, picker popup stili). Picker mevcut `AsyncCombobox` stilini yeniden kullanır; E2E gerçek Chrome'da çalıştı ama görsel doğrulama yerine geçmez.
- Bu fazda alt-ajan (subagent) kullanılmadı; tüm testler ve mutasyon kontrolleri ana oturumda çalıştırıldı.
- Repo içinde size ait, izlenmeyen `docs/architecture-analysis/Phase_2.5A_2.5B_Post_Implementation_Audit_Prompt.pdf` dosyasına dokunulmadı ve hiçbir commit'e alınmadı.
- Merge/push için onayınız bekleniyor (önceki merge/push yetkisi yalnızca Phase 2.5B içindi).

## 8. Commit'ler
`dab42a1` plan · `e01c3b7` T1 · `f734c73` T2 · `d60abfc` T3 · `519b9be` T4 · `39a5531` T5 · `ecf6886` T6 (E2E) · `5ff4e5c` docs + rapor · `b7aefca` `chore(graphify)` refresh.
