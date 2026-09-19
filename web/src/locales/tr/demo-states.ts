export default {
  page: {
    eyebrow: 'Design system',
    title: 'Empty & Loading States',
    description: 'Veri yokken yönlendiren illüstrasyonlar, veri yoldayken gelecek düzeni tutan iskeletler.',
    summary: '{{count}} bölüm · {{illustrations}} illüstrasyon',
    sectionsNavLabel: 'Durum bölümleri',
    sections: {
      choosing: 'Hangisi, ne zaman',
      illustrations: 'İllüstrasyonlar',
      emptyStates: 'Boş durumlar',
      skeleton: 'Skeleton Loader',
      inlineLoading: 'Kısmi yükleme',
    },
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      [
        'İskelet gelecek içeriğin ölçüsüdür',
        'Bir skeleton, yerini tuttuğu bileşenin yüksekliğini ve düzenini birebir taşır. Farklı bir ölçü, veri geldiğinde ikinci bir yerleşim kaymasıdır — bekleme süresini kısaltmak yerine uzatmış olursunuz.',
      ],
      [
        '200ms altı istek iskelet göstermez',
        'Anında dönen bir istekte beliren ve kaybolan iskelet, yüklemeden daha rahatsız edicidir. Kısa isteklerde hiçbir şey göstermeyin; uzun olduğu bilinenlerde doğrudan iskeletle açın.',
      ],
      [
        'Ekrandaki veri iskelete dönmez',
        'Arka planda yenileme sırasında mevcut içerik yerinde kalır ve opaklığı düşer. Okunmakta olan bir tabloyu gri bloklara çevirmek, kullanıcıdan zaten sahip olduğu bilgiyi geri almaktır.',
      ],
      [
        'Boş durum üç şeyi birden söyler',
        'Ne olduğu (başlık), neden öyle olduğu (açıklama) ve bundan sonra ne yapılacağı (aksiyon). Üçünden biri eksikse kullanıcı ekranda sıkışır.',
      ],
      [
        'Aksiyon boşluğun nedenine bağlıdır',
        'Filtre sonucu boşsa «oluştur» yanlış öneridir — doğru öneri filtreyi temizlemektir. Hata durumunda ise tek doğru birincil aksiyon «tekrar dene»dir.',
      ],
      [
        'İllüstrasyon renk taşımaz',
        'Çizimler currentColor ile kurulur; tonu çağıran taraf verir. Böylece tek dosya hem açık hem koyu temada, hem nötr hem hata bağlamında çalışır.',
      ],
      [
        'Bekleme her zaman duyurulur',
        'Her iskelet bloğu aria-hidden, onları saran bölge role="status" + aria-live="polite" taşır. Ekran okuyucu dikdörtgen saymaz, bir kez «Yükleniyor» duyar.',
      ],
      [
        'Parıltı süs, düzen mesajdır',
        'prefers-reduced-motion açık olduğunda tarama animasyonu durur; iskeletin tuttuğu yerleşim aynen kalır. Bilgi hareketin içinde değil, biçimin içindedir.',
      ],
    ],
  },
  choosing: {
    title: 'Hangisi, ne zaman',
    description:
      'Ekranda içerik yoksa sebebi tek değildir. Durumu seçen şey görünüş değil, boşluğun nedeni — ve neden, kullanıcıya önerilecek aksiyonu belirler.',
    headers: {
      state: 'Durum',
      when: 'Ne zaman',
      shows: 'Ne gösterir',
      action: 'Aksiyon',
    },
    rows: [
      {
        name: 'Yükleniyor',
        when: 'İstek yolda, sonuç henüz bilinmiyor.',
        shows: 'Gelecek içeriğin iskeleti — boyutu ve düzeni birebir aynı.',
        action: 'Yok. Bekleme ekranı aksiyon istemez.',
      },
      {
        name: 'İlk kez boş',
        when: 'İstek başarılı, kayıt hiç oluşturulmamış.',
        shows: 'İllüstrasyon + neden boş olduğunu söyleyen tek cümle.',
        action: 'Birincil: ilk kaydı oluştur.',
      },
      {
        name: 'Filtre sonucu boş',
        when: 'Veri var, bu kesit yok.',
        shows: 'Aramayı hatırlatan illüstrasyon, uygulanan filtrenin özeti.',
        action: 'Birincil: filtreleri temizle. Oluşturma önerilmez.',
      },
      {
        name: 'Hata',
        when: 'İstek başarısız oldu.',
        shows: 'Ne olduğu ve kullanıcının suçu olmadığı — teknik ayrıntı değil.',
        action: 'Birincil: tekrar dene.',
      },
      {
        name: 'Bağlantı yok',
        when: 'Ağ erişilemez, panel çalışıyor.',
        shows: 'Sorunun panelde değil bağlantıda olduğu.',
        action: 'Birincil: yeniden bağlan. İkincil: çevrimdışı görünüm.',
      },
      {
        name: 'Yetki yok',
        when: 'Kayıt var, bu kullanıcıya kapalı.',
        shows: 'Erişimin nasıl alınacağı — kaydın içeriği asla sızdırılmaz.',
        action: 'Birincil: erişim iste.',
      },
      {
        name: 'Hepsi tamam',
        when: 'Kuyruk boş çünkü iş bitti.',
        shows: 'Olumlu bir kapanış — boşluk bir eksiklik değil.',
        action: 'Yok ya da ikincil: arşive git.',
      },
    ],
  },
  illustrations: {
    title: 'İllüstrasyonlar',
    description:
      'src/components/common/illustrations.tsx — altı çizim, tek dosya. Renk yoktur: konturlar currentColor, dolgular token, tonu her zaman çağıran verir.',
    entries: {
      emptyBox: 'Koleksiyon hiç doldurulmadı',
      noResults: 'Sorgu hiçbir kayıtla eşleşmedi',
      broken: 'İstek başarısız oldu',
      offline: 'Ağ bağlantısı yok',
      noAccess: 'Kayıt var, yetki yok',
      allDone: 'Kuyruk bitti, boşluk olumlu',
    },
  },
  emptyStates: {
    title: 'Boş durumlar',
    description:
      "Her biri EmptyState'in illustration prop'u ile kurulur. Başlık ne olduğunu, açıklama nedenini, buton çıkış yolunu söyler — üçü birden, eksiksiz.",
    firstEmpty: {
      cardTitle: 'İlk kez boş',
      cardDescription: 'Kayıt hiç oluşturulmamış. Birincil aksiyon ilkini oluşturmaktır.',
      title: 'Henüz fırsat yok',
      description: 'İlk fırsatı oluşturduğunuzda pipeline burada canlanır.',
      action: 'Fırsat oluştur',
    },
    filteredEmpty: {
      cardTitle: 'Filtre sonucu boş',
      cardDescription: 'Veri var, bu kesit yok. Oluşturma önerilmez — filtreyi gevşetmek önerilir.',
      title: 'Eşleşen kayıt yok',
      description: '«Beklemede» + son 7 gün filtresi hiçbir fırsatla eşleşmedi.',
      action: 'Filtreleri temizle',
    },
    error: {
      cardTitle: 'Hata',
      cardDescription: 'İstek başarısız oldu. Kullanıcı suçlanmaz, teknik ayrıntı gösterilmez.',
      title: 'Liste yüklenemedi',
      description: 'Sunucuya ulaşılamadı. Sorun sizde değil — tekrar denemek çoğu zaman yeterli.',
      action: 'Tekrar dene',
    },
    offline: {
      cardTitle: 'Bağlantı yok',
      cardDescription: 'Panel çalışıyor, ağ erişilemiyor. Ayrımı söylemek kullanıcıyı yanlış aramadan kurtarır.',
      title: 'Çevrimdışısınız',
      description: 'Bağlantı geri geldiğinde veriler kaldığı yerden yüklenecek.',
      action: 'Yeniden bağlan',
    },
    noAccess: {
      cardTitle: 'Yetki yok',
      cardDescription: 'Kayıt var ama bu kullanıcıya kapalı. İçeriğe dair hiçbir ipucu sızdırılmaz.',
      title: 'Bu alana erişiminiz yok',
      description: 'Raporlar modülü yalnızca yöneticilere açık. Erişim talebi ekip yöneticinize gider.',
      action: 'Erişim iste',
    },
    allDone: {
      cardTitle: 'Hepsi tamam',
      cardDescription: 'Kuyruk boş çünkü iş bitti. Bu boşluk bir eksiklik değil, bir sonuç.',
      title: 'Bugün için her şey hazır',
      description: 'Bekleyen onay kalmadı. Yeni bir talep geldiğinde burada görünecek.',
      action: 'Arşive git',
    },
  },
  skeleton: {
    title: 'Skeleton Loader',
    description:
      'Spinner ne geleceğini söylemez, iskelet söyler. Her örnek gerçek bileşenin ölçüleriyle çizilir — «Tekrar» ile geçişi yeniden izleyin.',
    metricCard: {
      title: 'Metrik kartları',
      description: "Damga, etiket, 32px rakam ve delta satırı — MetricCard'ın birebir izi.",
    },
    tableCard: {
      title: 'Veri tablosu',
      description: 'Sütun başlıkları satırlardan önce bilinir, bu yüzden iskelette de gerçek kalır.',
    },
    listCard: {
      title: 'Liste',
      description: '30px damga, bir başlık ve daha kısa bir alt satır — satır yüksekliği sabit.',
    },
    chartCard: {
      title: 'Grafik',
      description: 'Gri bir dikdörtgen değil, çubuk: okuyucu sayıdan önce cevabın biçimini öğrenir.',
    },
    formCard: {
      title: 'Form',
      description: 'Alanlar gerçek kontrol yüksekliğinde (--nx-control-height) durur, etiketler kısadır.',
    },
    profileCard: {
      title: 'Kimlik bloğu',
      description: 'Avatar dairedir; iskelet de dairedir. Şekil, boyut kadar bilgi taşır.',
    },
    textCard: {
      title: 'Metin',
      description: 'Son satır kısadır — tam genişlikte biten bir paragraf iskeleti çubuğa benzer.',
    },
    labels: {
      summaryMetrics: 'Özet metrikler',
      dealsList: 'Fırsat listesi',
      upcomingActivities: 'Yaklaşan aktiviteler',
      weeklyChart: 'Haftalık fırsat grafiği',
      dealForm: 'Fırsat formu',
      userCard: 'Kullanıcı kartı',
      descriptionText: 'Açıklama metni',
    },
  },
  loadingPreview: {
    loadingPill: 'Yükleniyor',
    loadedPill: 'Yüklendi',
    retry: 'Tekrar',
    regionLabel: '{{label}} yükleniyor',
  },
  tableHeaders: {
    company: 'Firma',
    stage: 'Aşama',
    amount: 'Tutar',
  },
  loaded: {
    metrics: {
      openDeals: { label: 'Açık fırsat', context: 'geçen aya göre' },
      won: { label: 'Kazanılan', context: 'bu çeyrek' },
      avgAmount: { label: 'Ortalama tutar', context: 'fırsat başına' },
      conversion: { label: 'Dönüşüm', context: 'aday → kazanç' },
    },
    activities: {
      quoteFollowUp: 'Teklif takibi — {{company}}',
      demoCall: 'Demo görüşmesi — {{company}}',
      contractReview: 'Sözleşme incelemesi',
      weeklyMeeting: 'Haftalık pipeline toplantısı',
      metaLegal: 'Hukuk',
      metaSalesTeam: 'Satış ekibi',
    },
    chart: {
      days: {
        mon: 'Pzt',
        tue: 'Sal',
        wed: 'Çar',
        thu: 'Per',
        fri: 'Cum',
        sat: 'Cmt',
        sun: 'Paz',
      },
      dealsLabel: 'Fırsat',
      title: 'Haftalık yeni fırsat',
      weeklyChange: '+18% hafta',
    },
    form: {
      companyLabel: 'Firma',
      contactLabel: 'Yetkili',
      amountLabel: 'Tutar',
      cancel: 'Vazgeç',
      save: 'Kaydet',
    },
    profile: {
      salesPill: 'Satış',
      activePill: 'Aktif',
    },
    text: 'Fırsat, teklif gönderildikten sonra 14 gün boyunca beklemede tutulur. Bu süre içinde yetkiliden yanıt gelmezse otomatik hatırlatma tetiklenir ve fırsatın sahibi bilgilendirilir. Süre dolduğunda kayıt kapanmaz — yalnızca «Beklemede» aşamasına taşınır, böylece tahmin raporundan çıkar ama geçmişten silinmez.',
  },
  inlineLoading: {
    title: 'Kısmi yükleme',
    description: 'Sayfanın tamamı değil, bir parçası bekliyorsa iskelet doğru araç değildir — ekranda olanı korumak gerekir.',
    refetchCard: {
      title: 'Arka planda yenileme',
      description: 'Veri zaten ekrandaysa iskelete dönülmez; mevcut içerik %50 opaklıkla okunabilir kalır.',
    },
    loadMoreCard: {
      title: 'Sayfa ekleme',
      description: 'Yeni sayfa listenin altına eklenir. Yüklenen satırlar asla yerinden oynamaz.',
    },
    pendingCard: {
      title: 'Bekleyen aksiyon',
      description: "Buton kilitlenir, ikon spinner'a döner, genişlik sabit kalır — satır oynamaz.",
    },
    saving: 'Kaydediliyor',
    save: 'Kaydet',
    pendingHint: 'İstek yolda — buton kilitli.',
    idleHint: 'Butona basın.',
    updating: 'Güncelleniyor — eski veri okunabilir kalır',
    current: 'Güncel',
    refresh: 'Yenile',
    nextPageLoading: 'Sonraki sayfa yükleniyor',
    loadMore: 'Daha fazla yükle',
  },
}
