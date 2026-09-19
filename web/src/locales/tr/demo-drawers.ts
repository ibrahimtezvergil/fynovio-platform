export default {
  page: {
    eyebrow: 'Design system',
    title: 'Detail Drawers & Slide-overs',
    description: 'Bir satıra tıklandığında ekranı terk etmeden sağdan açılan detay paneli — okuma, düzenleme ve onay katmanlarıyla.',
    summary: '{{count}} bölüm · {{surfaces}} yüzey',
    sectionsNavLabel: 'Panel bölümleri',
    sections: {
      choosing: 'Hangisi, ne zaman',
      rowToDetail: 'Satır → detay',
      sizeAndSide: 'Ölçü ve kenar',
      stacked: 'Katmanlı panel',
    },
    rowToDetailTitle: 'Satır → detay paneli',
    rowToDetailDescription:
      'Bir satıra tıklayın: panel sağdan girer, tablo arkada kalır. Başlıktaki oklarla kayıtlar arasında gezinin, «Düzenle» ile kirli-form korumasına sahip düzenleme paneline geçin.',
    readVsEditTitle: 'Okuma paneli mi, düzenleme paneli mi',
    readVsEditDescription: 'İkisi aynı bileşen değildir; kapanma davranışları farklıdır.',
    readPanelTitle: 'Okuma paneli — serbest kapanır',
    readPanelBody:
      'Esc, arka plana tıklama ve kapatma düğmesi aynı şeyi yapar. Kaybolacak bir şey olmadığı için kullanıcıyı hiçbir soruya tutmaz.',
    editPanelTitle: 'Düzenleme paneli — kirliyken sorar',
    editPanelBodyPre:
      'Form dokunulmamışken okuma paneli gibi davranır. İlk değişiklikten sonra kapatma isteği bir karara dönüşür:',
    editPanelBackToPanel: 'panele dön',
    editPanelBodyMid: 'ya da',
    editPanelDiscard: 'değişiklikleri at',
    editPanelBodyPost:
      '«Kaydet» üçüncü bir seçenek olarak dayatılmaz — kullanıcı yarım bıraktığı işi atmakta özgürdür.',
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      [
        'Panel bağlamı korur, sayfa değiştirir',
        'Detay paneli listeyi arkada bırakır: kaydırma konumu, seçim ve filtre yerinde kalır. Bunlardan biri kaybolacaksa doğru yüzey tam sayfadır.',
      ],
      [
        'Panel nerede olduğunu söyler',
        'Başlıkta «2 / 5» ve önceki/sonraki tuşları. Bunlar olmadan panel, tıklanan satırla bağını kaybeder ve rastgele bir modala dönüşür.',
      ],
      [
        'Gezinme listeyle aynı sırayı izler',
        'Sonraki, tablodaki bir sonraki satırdır — filtrelenmiş ve sıralanmış hâliyle. Kayıt kimliğine göre gezinmek kullanıcının gördüğü sırayı bozar.',
      ],
      [
        'Kirli form dışarı tıklamayla kapanmaz',
        'Okunan panel arka plana tıklayınca kapanır; içinde yazılmış bir form varsa aynı hareket bir karara dönüşür. Aksi hâlde tek bir kaymış tıklama yazılanları siler.',
      ],
      [
        'Tek katman kuralı',
        'Panelin üstüne yalnızca kararla biten bir diyalog çıkar. İkinci bir panel, kullanıcıya iki «geri» adımı ve iki kaydedilmemiş form bırakır.',
      ],
      [
        'Genişlik içerikten gelir, 768px sınırdır',
        'Ekranın yarısını geçen panel, «arkası görünür kalır» sözünü tutamaz. O noktada içerik zaten bir sayfayı hak ediyordur.',
      ],
      [
        'Satır tıklaması tek erişim yolu olamaz',
        'Her satırda gerçek bir düğme vardır; satırın tamamına tıklamak onun üzerine eklenen kolaylıktır. Klavye ve ekran okuyucu düğmeyi kullanır.',
      ],
      [
        'Panel başlığı kaydın kimliğidir',
        'Ünvan, durum rozeti ve kayıt numarası başlıkta durur. Panel açıkken hangi kayda baktığını sormak zorunda kalan kullanıcı, yanlış kaydı düzenler.',
      ],
    ],
  },
  choosing: {
    title: 'Hangisi, ne zaman',
    description:
      '«Bu satır hakkında daha fazlası» sorusunun dört cevabı var. Yanlış yüzeyi seçmek, sonradan bileşenle değil bilgi mimarisiyle düzeltilir.',
    headers: {
      surface: 'Yüzey',
      when: 'Ne zaman',
      why: 'Neden',
      avoid: 'Kaçının',
    },
    choices: {
      drawer: {
        surface: 'Detay paneli (slide-over)',
        when: 'Bir satırın tamamını görmek ya da kısa bir düzenleme yapmak.',
        why: 'Liste arkada görünür kalır, kaydırma konumu korunur, kapanınca aynı satırın üstündesiniz.',
        avoid: 'İçerik 768px genişliği ya da iki sekmeden fazlasını gerektiriyorsa.',
      },
      dialog: {
        surface: 'Diyalog (modal)',
        when: 'Tek bir kararla biten iş: onay, tek alanlık soru, kısa form.',
        why: 'Ekranın ortasına oturur ve tek bir soru sorar; arkadaki bağlam kullanılmayacaktır.',
        avoid: 'Kaydırma gerektiren, sekmeli ya da yan yana okunması gereken içerik.',
      },
      fullPage: {
        surface: 'Tam sayfa',
        when: 'Kaydın kendisi bir çalışma alanı: çok sekmeli detay, uzun form, düzenleyici.',
        why: "Kendi URL'i olur, paylaşılır, yer imine eklenir, tarayıcı geri tuşu çalışır.",
        avoid: 'Kullanıcı listeye hemen dönecekse — her gidiş dönüş listeyi yeniden kurar.',
      },
      inline: {
        surface: 'Satır içi genişletme',
        when: 'Birkaç ek alan ve satırlar arası karşılaştırma.',
        why: 'Kullanıcı iki kaydı aynı anda açık tutabilir; hiçbir şey üstünü örtmez.',
        avoid: 'Aksiyon içeren ya da beş alandan uzun detay — tablo satırı forma dönüşmemeli.',
      },
    },
  },
  recordExplorer: {
    caption: 'Cari listesi — bir satıra tıklayınca detay paneli sağdan açılır',
    headers: {
      account: 'Cari',
      owner: 'Sorumlu',
      city: 'Şehir',
      openDeals: 'Açık fırsat',
      lifetimeValue: 'Yaşam boyu değer',
      status: 'Durum',
    },
    openDetail: 'detayını aç',
  },
  detailDrawer: {
    tabs: {
      summary: 'Özet',
      contacts: 'Kişiler',
      documents: 'Belgeler',
      history: 'Hareketler',
    },
    tabsAria: 'Detay sekmesi',
    prevRecord: 'Önceki kayıt',
    nextRecord: 'Sonraki kayıt',
    identityLine: '{{segment}} · {{city}} · Sorumlu {{owner}}',
    emailAction: 'E-posta',
    callAction: 'Ara',
    openFullPage: 'Tam sayfada aç',
    fields: {
      email: 'E-posta',
      phone: 'Telefon',
      taxId: 'Vergi no',
      paymentTerm: 'Ödeme koşulu',
      openDeals: 'Açık fırsat',
      lifetimeValue: 'Yaşam boyu değer',
      lastContact: 'Son temas',
    },
    accountNote: 'Hesap notu',
    close: 'Kapat',
    edit: 'Düzenle',
  },
  editDrawer: {
    title: 'Cari kaydı düzenle',
    description: 'Değişiklikler kaydedilene kadar uygulanmaz. Kirli bir formda panel doğrudan kapanmaz.',
    fields: {
      name: 'Ünvan',
      owner: 'Sorumlu',
      status: 'Durum',
      statusHint: 'Rozet, seçim değiştikçe formun içinde güncellenir.',
      paymentTerm: 'Ödeme koşulu',
      note: 'Hesap notu',
    },
    cancel: 'Vazgeç',
    save: 'Kaydet',
    toastUpdated: 'Kayıt güncellendi',
    discardTitle: 'Kaydedilmemiş değişiklikler var',
    discardDescription: 'Paneli kapatırsanız bu formdaki değişiklikler kaybolur. Kaydetmek için panele dönün.',
    backToPanel: 'Panele dön',
    discardChanges: 'Değişiklikleri at',
  },
  sizeSection: {
    title: 'Ölçü ve kenar',
    description:
      "Panel genişliği içeriğe göre seçilir, tersi değil. 768px'i aşan bir panel arkasındaki listeyi görünür bırakma sözünü tutamaz — o noktada doğru cevap tam sayfadır.",
    widthLadderTitle: 'Genişlik merdiveni',
    widthLadderDescription:
      'Beş basamak. Panelin görevi arkasındaki bağlamı görünür tutmak; genişlik bu sözün sınırıdır.',
    widths: {
      sm: { label: 'sm · 384px', use: 'Tek sütun özet, birkaç alan.' },
      md: { label: 'md · 448px', use: 'Varsayılan. Okuma paneli, kısa form.' },
      lg: { label: 'lg · 512px', use: 'Sekmeli detay, orta boy form.' },
      xl: { label: 'xl · 576px', use: 'Yan yana iki sütunlu özet.' },
      '3xl': { label: '3xl · 768px', use: 'Sınır. Bundan genişi tam sayfadır.' },
    },
    widthCheck: 'Arkadaki tablo hâlâ görünür mü? Görünmüyorsa panel çok geniştir.',
    close: 'Kapat',
    sideChoiceTitle: 'Kenar seçimi',
    sideChoiceDescription:
      'Detay paneli her zaman sağdan girer. Soldan giren panel gezinmeye aittir; alttan giren dar ekrana.',
    sides: {
      right: { label: 'Sağ', note: 'Varsayılan. Okuma yönünün sonu; listeyi yerinden oynatmaz.' },
      left: { label: 'Sol', note: 'Gezinme ve ağaç yapılar. Detay için kullanılmaz.' },
      bottom: { label: 'Alt', note: 'Dar ekran. Başparmağa yakın, tek elle kapanır.' },
    },
    sideTitle: '{{side}} kenar',
    listVisibleTitle: 'Panel açıkken liste görünür kalır',
    listVisibleDescription:
      'Kaplama şeffaftır ve panel ekranın yarısını geçmez. Kullanıcı kapattığında tıkladığı satırın üzerindedir — bu, tam sayfanın veremediği tek şeydir.',
    openExample: 'Örneği aç',
    lookBehindTitle: 'Arkaya bakın',
    lookBehindDescription:
      'Tablo yerinde duruyor, kaydırma konumu korunuyor. Panel kapandığında hiçbir şey yeniden yüklenmez.',
  },
  stackedSection: {
    title: 'Katmanlı panel',
    description:
      'Bir detay panelinin üstüne yalnızca kararla biten bir diyalog çıkabilir — onay ya da tek alanlık soru. İkinci bir panel çıkamaz.',
    destructiveTitle: 'Yıkıcı aksiyon onayı',
    destructiveDescription:
      'Silme, panelin içinden tetiklenir ama panelde tamamlanmaz. Onay diyaloğu kapandığında panel hâlâ oradadır — kullanıcı bağlamını kaybetmez.',
    openPanel: 'Paneli aç',
    panelDescription: 'Panelin içindeki yıkıcı aksiyon, üstte bir onay diyaloğu açar.',
    addNote: 'Not ekle (tek alanlık diyalog)',
    deleteAccount: 'Cariyi sil',
    panelHint: 'Her iki diyalog da kapandığında panel yerinde kalır; Esc yalnızca en üstteki katmanı kapatır.',
    close: 'Kapat',
    deleteConfirmTitle: 'Cari kaydı silinsin mi?',
    deleteConfirmDescription: 'Nordwind Lojistik ve bağlı 3 açık fırsat kaldırılır. Bu işlem geri alınamaz.',
    cancel: 'Vazgeç',
    delete: 'Sil',
    deleteConfirmedState: 'silme onaylandı — panel açık kaldı',
    toastDeleted: 'Cari silindi',
    addNoteTitle: 'Hesap notu ekle',
    addNoteDescription: 'Tek alanlık bir soru — panelin üstünde durabilecek en ağır ikinci katman budur.',
    noteAria: 'Not',
    notePlaceholder: 'Kısa not…',
    add: 'Ekle',
    noteAddedState: 'not eklendi — panel açık kaldı',
    layerRuleName: 'Katman kuralı',
    noSecondPanelTitle: 'İkinci panel yok',
    noSecondPanelDescription:
      'Bir panelden ikinci bir panel açmak, kullanıcıya iki «geri» adımı ve iki kaydedilmemiş form bırakır. Detayın detayı gerekiyorsa o bir sayfadır.',
    practicalTestPre: 'Pratik test:',
    practicalTestPost:
      'tuşuna basıldığında ne kapanacağını kullanıcı önceden söyleyebiliyor mu? Cevap «duruma göre» ise katman sayısı bir fazladır.',
  },
  schema: {
    nameMin: 'En az 2 karakter girin.',
    nameMax: 'En fazla 80 karakter.',
    ownerRequired: 'Sorumlu seçin.',
    paymentTermRequired: 'Vade bilgisi girin.',
    noteMax: 'En fazla 400 karakter.',
  },
  customerStatus: {
    active: 'Aktif müşteri',
    prospect: 'Potansiyel',
    risk: 'Riskli',
    churned: 'Kaybedildi',
  },
  customers: {
    cr1042: {
      segment: 'Kurumsal · Lojistik',
      paymentTerm: '45 gün vadeli',
      lastContact: '2 saat önce',
      note: 'İskonto üst sınırı %15. Üstü için bölge müdürü onayı gerekiyor.',
      contacts: {
        0: { role: 'Satın alma müdürü' },
        1: { role: 'Filo operasyon şefi' },
      },
      documents: {
        0: { name: 'Cerceve-sozlesme-2026.pdf', date: '12 Oca 2026' },
        1: { name: 'Nordwind-teklif-v3.pdf', date: '3 Eyl 2026' },
      },
      history: {
        0: { time: '2 sa önce', title: 'Aşama güncellendi', note: 'Teklif Gönderildi → Görüşme' },
        1: {
          time: 'Dün 14:20',
          title: 'Revize teklif gönderildi',
          note: 'Üç senaryolu fiyat tablosu',
        },
        2: { time: '28 Ağu', title: 'Yerinde demo', note: 'Gebze deposu, operasyon ekibi' },
      },
    },
    cr1088: {
      segment: 'Kurumsal · Denizcilik',
      paymentTerm: '30 gün vadeli',
      lastContact: '4 gün önce',
      note: 'İhale süreci; karar kurulu ekim ortasında toplanıyor. Fiyattan çok API kapsamı konuşuluyor.',
      contacts: {
        0: { role: 'IT direktörü' },
        1: { role: 'Satın alma' },
      },
      documents: {
        0: { name: 'RFP-2026-warehouse-api.pdf', date: '22 Ağu 2026' },
      },
      history: {
        0: { time: '4 gün önce', title: 'Teknik değerlendirme', note: 'API kapsamı ve SLA soruları' },
        1: { time: '22 Ağu', title: 'İhale dosyası alındı', note: 'Son teslim 30 Eylül' },
      },
    },
    cr0977: {
      segment: 'Orta ölçek · İnşaat',
      paymentTerm: '60 gün vadeli',
      lastContact: '11 gün önce',
      note: 'İki faturada vade aşımı var. Yenileme görüşmesi öncesi finans onayı gerekiyor.',
      contacts: {
        0: { role: 'Genel müdür yardımcısı' },
      },
      documents: {
        0: { name: 'Odeme-plani-revize.xlsx', date: '19 Ağu 2026' },
        1: { name: 'Destek-sozlesmesi-2025.pdf', date: '4 Kas 2025' },
      },
      history: {
        0: { time: '11 gün önce', title: 'Tahsilat hatırlatması', note: 'FT-2026-0764 · 32 gün gecikme' },
        1: { time: '19 Ağu', title: 'Ödeme planı önerildi', note: 'Üç eşit taksit' },
      },
    },
    cr1130: {
      segment: 'Kurumsal · Perakende',
      paymentTerm: 'Peşin',
      lastContact: 'Dün',
      note: '40 kullanıcılık genişleme onaya gitti. Mağaza tarafı için ayrı mobil lisans konuşuluyor.',
      contacts: {
        0: { role: 'IT müdürü' },
        1: { role: 'Operasyon direktörü' },
        2: { role: 'Finans' },
      },
      documents: {
        0: { name: 'Genisleme-teklifi-40-kullanici.pdf', date: '1 Eyl 2026' },
      },
      history: {
        0: { time: 'Dün 09:40', title: 'Genişleme teklifi gönderildi', note: '40 kullanıcı, yıllık' },
        1: { time: '26 Ağu', title: 'Kullanım raporu paylaşıldı', note: 'Aylık aktif kullanıcı %78' },
      },
    },
    cr0851: {
      segment: 'Orta ölçek · Denizcilik',
      paymentTerm: '30 gün vadeli',
      lastContact: '3 ay önce',
      note: 'Sözleşme yenilenmedi; rakip çözüme geçtiler. Altı ay sonra yeniden temas planlandı.',
      contacts: {
        0: { role: 'Operasyon müdürü' },
      },
      documents: {
        0: { name: 'Fesih-bildirimi.pdf', date: '30 May 2026' },
      },
      history: {
        0: { time: '3 ay önce', title: 'Sözleşme feshi', note: 'Yenileme yapılmadı' },
        1: { time: '12 May', title: 'Kayıp analizi görüşmesi', note: 'Fiyat ve entegrasyon kapsamı' },
      },
    },
  },
}
