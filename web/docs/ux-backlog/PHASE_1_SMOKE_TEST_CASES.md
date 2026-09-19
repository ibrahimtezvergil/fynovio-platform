# Faz 1 — Smoke Test Senaryoları

Faz 1 UX backlog’u için manuel, sürüm öncesi zorunlu kontroller. Senaryoları
MSW destekli uygulamada çalıştırın; backend hesabı ya da fixture kurulumu gerekmez.

## Ön koşullar

1. Uygulamayı `npm run dev` ile başlatın ve ekranda belirtilen mock kullanıcıyla giriş yapın.
2. Temiz bir tarayıcı profiliyle başlayın; senaryo belirtiyorsa `localStorage` verisini temizleyin.
   Böylece kalıcı görünümler ve akıllı varsayılanlar tutarlı sonuç verir.
3. Görsel senaryoları bir kez açık, bir kez koyu temada çalıştırın. Ana turda rahat yoğunluğu;
   tablo senaryolarında ayrıca sıkı yoğunluğu kullanın.

| ID | Alan | Adımlar | Beklenen sonuç |
| --- | --- | --- | --- |
| SMK-01 | Komut paleti | Korumalı herhangi bir sayfada macOS için `⌘K`, diğerleri için `Ctrl+K` tuşlarına basın; “Pipeline” aratıp seçin. | Odaklanmış, aranabilir palet açılır. Rota/işlem gruplarını gösterir, seçimden sonra kapanır ve Pipeline’a gider. |
| SMK-02 | Kenar çubuğu kısayolu | Form alanına değil sayfaya odaklanın; `P` tuşunun sağındaki fiziksel tuşla `⌘[` / `Ctrl+[` kullanın. Tekrarlayın. | Kenar çubuğu her tuş basımında tam görünüm ve ikon şeridi arasında bir kez geçiş yapar; tarayıcı geri gitmez. Bir input’a yazarken kısayol çalışmaz. |
| SMK-03 | Toplu işlemler | Pipeline’ı açın, iki satır seçip sorumlu atayın; yeniden satır seçip aşamayı değiştirin. | Seçim çubuğu doğru sayıyı gösterir, işlemler yalnızca seçili satırlara uygulanır, bildirim görünür ve çubuk her işlemden sonra temizlenir. |
| SMK-04 | Hızlı inceleme çekmecesi | Pipeline tablo görünümünde satırın kontrol olmayan bir alanına tıklayın; önceki/sonraki kayıtları kullanın; Escape ile kapatın. | Çekmece seçilen fırsatı, aktif filtrelenmiş listedeki yerini, tutarlarını ve aşamasını gösterir. Önceki/sonraki aynı listede kalır. Onay kutusu ve satır menüsü tıklamaları çekmeceyi açmaz. |
| SMK-05 | Otomatik sonraki kayda geçiş | Hızlı inceleme çekmecesinde hazır olmayan bir fırsatı açıp “Tamamlandı olarak işaretle”yi seçin. | Fırsat Hazır durumuna geçer, mutasyon sonucu görünür ve çekmece sonraki görünür kayda ilerler; son kayıtta çekmece kapanır. |
| SMK-06 | Detaya geçiş | Dashboard’da “Aşama dağılımı” içindeki bir öğeye tıklayın. | Uygulama URL’de `?stage=<stage>` ile Pipeline’ı açar; yalnızca o aşamadaki fırsatlar görünür ve aktif filtre temizlenebilir. |
| SMK-07 | Çoklu görünüm | Pipeline’da bir filtre uyguladıktan sonra Tablo → Pano → Tablo geçişi yapın. | Tablo ve pano aynı filtrelenmiş fırsat kümesini gösterir. Pano sütunlarında aşama etiketi, kayıt sayısı, fırsat adı, müşteri ve tutar korunur. |
| SMK-08 | Önce/sonra farkı | `/demo/timeline` sayfasını açın ve değişiklik içeren bir denetim kaydını inceleyin. | Değişen alan, eski değer, ok ve yeni değer görünür; değişmeyen alanlar çizilmez. Değerler iki temada da okunur. |
| SMK-09 | Kişisel kaydedilmiş görünüm | `/demo/filters` sayfasında filtre/sıralamayı değiştirip adlandırılmış görünüm kaydedin; sayfayı yenileyin ve görünümü seçin. | Kaydedilmiş görünüm mevcut mock kullanıcı için kalır ve saklanan durumu geri yükler. Silme işleminden sonra yenilemede görünüm geri gelmez. |
| SMK-10 | Akıllı varsayılanlar | `/demo/drawers` sayfasında sorumlu, durum ve ödeme vadesi girerek müşteri düzenleyip kaydedin; yenileyin ve tarayıcı depolamasında `fynovio-smart-defaults` kaydını inceleyin. | En son kullanılan sorumlu, durum ve ödeme vadesi kişisel form hafızası olarak saklanır. Ad ve not saklanan değerlerde bulunmaz. |
| SMK-11 | Genel görsel regresyon | SMK-03–SMK-08 senaryolarını koyu temada; tablo içerenleri ayrıca sıkı yoğunlukta tekrarlayın. | Veri bölgeleri dışında taşma, kırpılma, okunmayan kontrast, kaybolan odak halkası veya erişilemeyen etkileşimli kontrol görülmez. |

## Otomatik temel kontrol

Teslimden önce `npm run check` çalıştırın. Bu komut lint, TypeScript ve mevcut
unit test paketini kapsar; yukarıdaki senaryolar ise unit testlerde özellikle
tam temsil edilmeyen kullanıcı yolculuklarını kontrol eder.
