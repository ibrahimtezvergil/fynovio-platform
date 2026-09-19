export default {
  quote: {
    draft: 'Taslak',
    sent: 'Gönderildi',
    approved: 'Onaylandı',
    rejected: 'Reddedildi',
    expired: 'Süresi Doldu',
    cancelled: 'İptal',
  },
  invoice: {
    draft: 'Taslak',
    pending: 'Beklemede',
    partial: 'Kısmi Ödendi',
    paid: 'Ödendi',
    overdue: 'Vadesi Geçti',
    refunded: 'İade Edildi',
  },
  shipment: {
    preparing: 'Hazırlanıyor',
    packed: 'Paketlendi',
    shipped: 'Sevk Edildi',
    transit: 'Yolda',
    delivered: 'Teslim Edildi',
    returned: 'İade',
  },
  stock: {
    in_stock: 'Stokta',
    low: 'Azalıyor',
    critical: 'Kritik Seviye',
    out: 'Tükendi',
    discontinued: 'Üretimi Durdu',
  },
  approval: {
    draft: 'Taslak',
    waiting: 'Onay Bekliyor',
    approved: 'Onaylandı',
    rejected: 'Reddedildi',
    revision: 'Revizyon İstendi',
  },
  sync: {
    synced: 'Eşitlendi',
    syncing: 'Eşitleniyor',
    failed: 'Başarısız',
    paused: 'Duraklatıldı',
  },
  member: {
    active: 'Aktif',
    invited: 'Davet Edildi',
    suspended: 'Askıya Alındı',
    archived: 'Arşivlendi',
  },
  vocabulary: {
    pipeline: {
      label: 'Satış fırsatı aşaması',
      description:
        'src/types içindeki STAGES — tablo, pano ve grafik ile aynı sıra. «Beklemede» bir adım değil, park yeridir; bu yüzden en sonda ve nötr tonda.',
    },
    quote: {
      label: 'Teklif',
      description:
        'İki bitiş yolu var ve ikisi aynı ton olamaz: «Reddedildi» müşterinin kararı (negatif), «İptal» bizim kararımız (nötr).',
    },
    invoice: {
      label: 'Fatura & ödeme',
      description:
        '«Beklemede» ile «Vadesi Geçti» ayrı durumlardır: biri normal akış, diğeri müdahale ister. Aynı tonu paylaşmaları en sık yapılan hata.',
    },
    shipment: {
      label: 'Sevkiyat',
      description: 'Dört ara adım tek yönlü ilerler; yalnızca «İade» geri döner ve tek negatif ton odur.',
    },
    stock: {
      label: 'Stok seviyesi',
      description:
        'Burada ton bir ölçek taşır: yeşil → kehribar → kırmızı. Sıralı bir dizide tonlar rastgele dağıtılmaz.',
    },
    approval: {
      label: 'Onay akışı',
      description: 'Tek ikonlu sözlük. Onay bir hükümdür; işaret metni tekrar eder, süslemez.',
    },
    sync: {
      label: 'Entegrasyon durumu',
      description:
        'Teknik durumlar sürekli değişir ve genelde ekranın kenarındadır; ikon uzaktan tanınmayı hızlandırır.',
    },
    member: {
      label: 'Kullanıcı hesabı',
      description:
        '«Askıya Alındı» geri döndürülebilir, «Arşivlendi» değildir. Geri dönüşü olan durum uyarı tonunda, olmayan nötr tonda.',
    },
  },
  tone: {
    green: { meaning: 'Tamamlandı, onaylandı, sağlıklı', examples: 'Ödendi · Teslim Edildi · Aktif' },
    blue: { meaning: 'Başladı, bildirildi, bilgilendirme', examples: 'Gönderildi · Davet Edildi' },
    teal: { meaning: 'İlerliyor, ara adım', examples: 'Kısmi Ödendi · Sevk Edildi' },
    purple: { meaning: 'Özel akış, insan müdahalesi', examples: 'Revizyon İstendi · İade Edildi' },
    amber: { meaning: 'Bekliyor, dikkat ister ama hata değil', examples: 'Onay Bekliyor · Azalıyor' },
    red: { meaning: 'Başarısız, gecikmiş, engelleyici', examples: 'Vadesi Geçti · Reddedildi' },
    gray: { meaning: 'Nötr, pasif, henüz başlamamış', examples: 'Taslak · İptal · Arşivlendi' },
    outline: { meaning: 'Kayıt dışı — sayılmaz, filtrelenmez', examples: 'Üretimi Durdu' },
  },
  page: {
    eyebrow: 'Design system',
    title: 'Status Badges',
    description:
      '«Beklemede», «Onaylandı», «İptal», «Ödendi» — süreç durumlarını taşıyan renk kodlu rozetler ve alan sözlükleri.',
    summary: '{{count}} sözlük · {{total}} durum',
    sections: {
      toneMeanings: 'Ton anlamları',
      vocabularies: 'Söz dağarcıkları',
      formats: 'Biçimler',
      inTable: 'Tabloda kullanım',
    },
    toneTable: { tone: 'Ton', when: 'Ne zaman', examples: 'Örnek durumlar' },
    toneLadderTitle: 'Ton merdiveni',
    toneLadderDescription:
      'Sekiz ton, sekiz rol. Bir alan sözlüğü tonları bu anlamların dışında kullanamaz — «Ödendi»nin kehribar olduğu bir ekran, sonraki ekranda kehribarı okunamaz kılar.',
    vocabTitle: 'Alan sözlükleri',
    vocabDescription:
      'Her durum kümesi bir kez, görüntülendiği sırayla tanımlanır. Rozet, filtre, pano sütunu ve grafik açıklaması aynı kaydı okur.',
    formatsTitle: 'Biçimler',
    formatsDescription: 'Tek bileşen, üç ayar: işaret (nokta ya da ikon), ölçü (md ya da sm) ve dolgusuz «outline» tonu.',
    dotTitle: 'Nokta — varsayılan',
    dotDescription: 'Nokta rengi metinden miras alır, yani ton tek yerden gelir. Sözlüklerin büyük çoğunluğu bunu kullanır.',
    iconTitle: 'İkon — hüküm bildiren sözlükler',
    iconDescription: 'İkon noktanın yerine geçer, yanına değil. Onay akışı ve entegrasyon durumu dışında kullanılmaz.',
    smTitle: 'sm — yoğun yüzeyler',
    smDescription: "22px yükseklik: kanban kartı, iç içe liste, satır içi özet. 52px'lik tablo satırında hâlâ md kullanılır.",
    smHint: '↑ sm · md ↓',
    urgent: 'Acil',
    outlineTitle: 'outline — kayıt dışı',
    outlineDescription:
      'Dolgusuz rozet «bu kayıt artık sayılmıyor» der: arşivlenmiş, kapsam dışı, üretimi durmuş. Dolgulu tonlarla aynı listede en fazla bir tane bulunur.',
    outlineDiscontinued: 'Üretimi Durdu',
    outlineOutOfScope: 'Kapsam Dışı',
    outlineArchive: 'Arşiv',
    tableTitle: 'Veri tablosunda',
    tableDescription:
      'Durum sütunu sayılarla birlikte sağa yaslanır. Rozete tıklayarak durumu değiştirin — sözlük, menü ve hücre aynı kaydı okuyor.',
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      ['Renk asla tek taşıyıcı değil', 'Her rozette nokta ya da ikon ve okunur bir etiket vardır. Gri baskıda, renk körlüğünde ve düşük kontrastlı ekranda anlam yerinde kalır (WCAG 1.4.1).'],
      ['Ton bir hue, anlam sözlükte', 'Kehribar «uyarı» demek değildir; ne demek olduğuna her alan kendi sözlüğünde karar verir. Tek kural: aynı sözlükte iki durum aynı tonu paylaşamaz.'],
      ['Sıra tanımın parçası', 'Bir durum kümesi her yerde aynı sırada çizilir — rozet listesinde, filtrede, pano sütunlarında. Alfabetik sıralama süreci gizler.'],
      ['Rozet sıfat değil, isim', '«Ödendi», «Onay Bekliyor» — sürecin adı. «Kritik!», «Dikkat» gibi ünlemler durum değil yorumdur ve iki farklı kayıtta aynı şeyi anlatmaz.'],
      ['Bir kayıt bir rozet', 'Yan yana iki durum rozeti okuyucuya hangisinin kaydın durumu olduğunu sordurur. İkinci eksen gerekiyorsa o bir etikettir, rozet değil.'],
      ['Tabloda her zaman sağda', 'Durum sütunu sayılarla birlikte sağa yaslanır. Ortak bir dikey kenar, bir rozet sütununu taranabilir kılan tek şeydir.'],
      ['İkon istisnadır', 'İkonlu varyant yalnızca hüküm bildiren (onay) ya da ekranın kenarında sürekli değişen (entegrasyon) sözlüklerde kullanılır. Her yerde ikon, hiçbir yerde vurgu demektir.'],
      ['Durum değişikliği geçmişe yazılır', 'Rozetin değişmesi tek başına yeterli değil; aynı hareket aktivite akışına «eski → yeni» olarak düşer. Rozet şimdiyi, akış nedeni anlatır.'],
    ],
  },
  grid: {
    caption: 'Fatura listesi — durum sütunundan her satırın durumu değiştirilebilir',
    invoiceHeader: 'Fatura',
    accountHeader: 'Cari',
    dueHeader: 'Vade',
    amountHeader: 'Tutar',
    statusHeader: 'Durum',
    changeAriaLabel: '{{id}} durumu: {{label}}. Değiştir.',
    menuLabel: 'Fatura durumu',
    due: {
      'FT-2026-0841': '28 Ağu 2026',
      'FT-2026-0839': '2 Eyl 2026',
      'FT-2026-0844': '15 Eyl 2026',
      'FT-2026-0846': '21 Eyl 2026',
      'FT-2026-0850': '—',
      'FT-2026-0812': '9 Ağu 2026',
    },
  },
}
