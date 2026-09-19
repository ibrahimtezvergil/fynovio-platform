export default {
  panel: {
    statusLabel: 'Durum',
    statusHint: 'Boş bırakmak «hepsi» demektir.',
    statusPlaceholder: 'Tüm durumlar',
    ownerLabel: 'Sorumlu',
    ownerPlaceholder: 'Tüm sorumlular',
    cityLabel: 'Şehir',
    cityPlaceholder: 'Tüm şehirler',
    amountLabel: 'Tutar aralığı',
    amountHint: 'Boş bırakılan uç sınırsızdır.',
    dateLabel: 'Sipariş tarihi',
    channelLabel: 'Satış kanalı',
    onlyTagged: 'Yalnızca etiketli siparişler',
    clearFilters: 'Filtreleri temizle',
  },
  grid: {
    order: 'Sipariş',
    account: 'Cari',
    owner: 'Sorumlu',
    caption:
      'Sipariş listesi. Sütun başlığına tıklayın, birden fazla alana göre sıralamak için Shift ile tıklayın. Şehir ve kanal sütun olarak gösterilmez; filtre panelinden daraltılır ve aktif çip satırında görünür.',
    sortByField: '{{field}} alanına göre sırala',
  },
  orderStatus: {
    pending: 'Beklemede',
    approved: 'Onaylandı',
    paid: 'Ödendi',
    shipped: 'Sevk Edildi',
    cancelled: 'İptal',
    refunded: 'İade',
  },
  channel: {
    direct: 'Doğrudan satış',
    partner: 'İş ortağı',
    online: 'Online',
  },
  sortField: {
    createdAt: 'Tarih',
    amount: 'Tutar',
    account: 'Cari ünvan',
    items: 'Kalem sayısı',
    status: 'Durum',
  },
  sortMenu: {
    summary: '{{field}} · {{arrow}}',
    label: 'Sırala',
    rulesHeading: 'Sıralama kuralları',
    reset: 'Sıfırla',
    noRules: 'Henüz sıralama kuralı yok. Aşağıdan bir alan ekleyin.',
    priority: '{{index}}. öncelik',
    toggleDirection: '{{field}} yönünü değiştir: {{direction}}',
    ascending: 'artan',
    descending: 'azalan',
    removeRule: '{{field}} kuralını kaldır',
  },
  chips: {
    activeLabel: 'Aktif:',
    removeAria: '{{label}} filtresini kaldır',
    clearAll: 'Tümünü temizle',
    search: 'Arama: “{{value}}”',
    status: 'Durum: {{label}}',
    owner: 'Sorumlu: {{owner}}',
    channel: 'Kanal: {{label}}',
    city: 'Şehir: {{city}}',
    amount: 'Tutar: {{min}} – {{max}}',
    dates: 'Tarih: {{from}} – {{to}}',
    onlyTagged: 'Yalnızca etiketliler',
  },
  explorer: {
    searchPlaceholder: 'Sipariş no, cari, sorumlu, şehir…',
    searchAria: 'Siparişlerde ara',
    filtersButton: 'Filtreler',
    recordsLabel: 'kayıt',
    emptyTitle: 'Bu filtrelerle eşleşen sipariş yok',
    emptyDescription:
      'Boşluk kayıt olmadığından değil, filtre çok dar olduğundan. Doğru öneri «yeni oluştur» değil, filtreyi gevşetmektir.',
    emptyAction: 'Filtreleri temizle',
  },
  savedViews: {
    customView: 'Özel görünüm — kayıtlı bir görünümden sapıldı',
    saveCurrent: 'Görünümü kaydet',
    nameLabel: 'Görünüm adı',
    namePlaceholder: 'ör. "Bu hafta, yüksek tutarlı"',
    cancel: 'Vazgeç',
    save: 'Kaydet',
    removeAria: '"{{label}}" görünümünü kaldır',
    all: { label: 'Tüm siparişler', description: 'Filtresiz liste, en yeniden eskiye.' },
    waiting: {
      label: 'Onay bekleyenler',
      description: 'Beklemedeki siparişler, en eski olan en üstte — sıradaki iş budur.',
    },
    highValue: {
      label: 'Yüksek tutarlı',
      description: '₺100.000 üzeri açık siparişler, tutara göre.',
    },
    risk: {
      label: 'İptal & iade',
      description: 'Kapanmış olumsuz kayıtlar — iade oranı incelemesi için.',
    },
    mine: {
      label: 'Benim portföyüm',
      description: 'Deniz Kaya üzerindeki tüm siparişler.',
    },
  },
  page: {
    eyebrow: 'Design system',
    title: 'Filter & Sort',
    description:
      'Data grid üzerindeki karmaşık filtreleme için genişletilebilir panel, aktif filtre çipleri ve çok alanlı sıralama.',
    summary: '{{count}} kayıt · {{views}} görünüm',
    sectionsNavLabel: 'Filtre bölümleri',
    sections: {
      filterModule: 'Filtre modülü',
      layers: 'Üç katman',
      sorting: 'Çoklu sıralama',
      views: 'Kayıtlı görünümler',
    },
    filterModuleTitle: 'Çalışan filtre modülü',
    filterModuleDescription:
      'Arama, genişletilebilir panel, çipler, çoklu sıralama ve kayıtlı görünümler — hepsi tek bir sorgu nesnesine bağlı. Sütun başlığına Shift ile tıklayın: ikinci sıralama kuralı eklenir. Panel, tablonun gösterdiğinden fazla alanı daraltır (şehir, kanal) — bu yüzden çip satırı olmadan liste açıklanamaz.',
    layersTitle: 'Üç katman',
    layersDescription:
      'Bir filtre modülü tek bir bileşen değil; farklı görünürlük sürelerine sahip üç katmandır.',
    layers: [
      [
        '1 · Her zaman görünen',
        'Arama kutusu, filtre düğmesi ve sıralama. Kullanıcı ekranı açtığı anda bunlar oradadır; hiçbiri açılıp kapanmaz.',
      ],
      [
        '2 · Genişletilebilir panel',
        'Altı ile on iki arasında alan. Varsayılan kapalıdır — sürekli açık bir panel, yedinci alanı arayan azınlık için herkesten dikey alan alır.',
      ],
      [
        '3 · Aktif filtre çipleri',
        'Panel kapalıyken listenin neden kısaldığını söyleyen tek şey. Her çip tek bir yüklemi kaldırır; toplu «temizle» ayrı bir düğmedir.',
      ],
    ],
    sortingTitle: 'Çoklu sıralama',
    sortingDescription:
      'Kurallar öncelik sırasıyla uygulanır: ilk ayıran kural kazanır, kalanlar eşitlik bozar. Menü kuralları numaralandırır, tablo başlığı aynı kuralı gösterir.',
    sortingCard1Title: 'İki giriş, tek state',
    sortingCard1Body:
      'Sütun başlığına tıklamak kuralları değiştirir, Shift ile tıklamak ekler. Aynı kural listesi sıralama menüsünde görünür — başlıkların menüden bağımsız çalıştığı bir tablo, kullanıcının göremediği ikinci bir sıralamaya sahiptir.',
    sortingCard2Title: 'Varsayılan yön alanın türünden gelir',
    sortingCard2Body:
      'Para ve tarih azalan, metin artan açılır. «En küçük tutar» nadiren sorulan bir sorudur; doğru varsayılan, kullanıcının atmak zorunda kalmadığı bir tıklamadır. Durum ise ne alfabetik ne sayısaldır — süreç sırasına göre sıralanır.',
    viewsTitle: 'Kayıtlı görünümler',
    viewsDescription:
      'Bir ekipte gerçekten kullanılan üç beş kombinasyon. Her sabah yeniden kurulmasınlar diye adlandırılır — ve her seferinde biraz farklı kurulmasınlar diye.',
    viewsNoSort: 'sıralama yok',
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      [
        'Boş kontrol filtre değildir',
        'Hiçbir seçim yapılmamış çoklu seçim «hiçbiri» değil «hepsi» demektir. Aksi hâlde panel açıldığı anda liste boşalır ve kullanıcı bir daha açmaz.',
      ],
      [
        'Panel kapalıyken de etkisi görünür',
        'Aktif çip satırı filtre panelinin makbuzudur. Çipsiz bir tasarımda kısalmış listenin nedeni ekranda hiçbir yerde yazmaz.',
      ],
      [
        'Her çip tek bir yüklemi kaldırır',
        'Üç durum seçiliyse üç çip vardır. Tek bir «Durum: 3 seçili» çipi, kullanıcıyı istemediği ikisini kaldırmak için paneli açmaya zorlar.',
      ],
      [
        'Filtreler VE ile birleşir',
        'Aynı alan içindeki değerler VEYA, alanlar arası VE. Bu kural her yerde aynıdır; ekran başına değişen bir mantık öğrenilemez.',
      ],
      [
        'Sıralama görünür ve çoklu olabilir',
        "İki alana göre sıralanan bir tabloda tek ok gösterilmez: kurallar öncelik numarasıyla listelenir. Başlıklar ve menü aynı state'i okur.",
      ],
      [
        'Sıralama alanın türüne uyar',
        'Durum alfabetik değil süreç sırasına göre sıralanır; para ve tarih önce azalan açılır. Doğru varsayılan, kullanıcının atmadığı bir tıklamadır.',
      ],
      [
        'Boş sonuç filtreyi suçlar',
        'Sonuç yoksa birincil aksiyon «yeni kayıt oluştur» değil «filtreleri temizle»dir. Boşluğun nedeni aksiyonu belirler.',
      ],
      [
        'Görünüm düzenlenince görünüm olmaktan çıkar',
        'Kayıtlı bir görünüm seçildikten sonra herhangi bir kontrole dokunulursa seçim düşer. Düzenlenmiş bir sorguyu hâlâ o görünüm gibi göstermek, yanlış listeye güvendirir.',
      ],
    ],
  },
}
