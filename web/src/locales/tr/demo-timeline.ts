export default {
  activityType: {
    call: 'Arama',
    email: 'E-posta',
    meeting: 'Görüşme',
    note: 'Not',
    stage: 'Durum değişikliği',
    document: 'Belge',
    payment: 'Ödeme',
    task: 'Görev',
    system: 'Sistem',
  },
  direction: {
    in: 'Gelen',
    out: 'Giden',
  },
  feedFilter: {
    all: 'Tümü',
  },
  composer: {
    segmentAriaLabel: 'Kaydedilecek hareket türü',
    textareaAriaLabel: 'Hareket açıklaması',
    placeholder: 'Ne konuşuldu? Tek satır özet, gerekirse detay…',
    helper: 'Kayıt akışın başına düşer ve «şimdi» olarak damgalanır.',
    submit: 'Kaydet',
  },
  feed: {
    emptyTitle: 'Hareket yok',
    defaultEmptyDescription: 'Bu filtreye uyan hareket yok. Filtreyi genişletin.',
    pinnedHeading: 'Sabitlenmiş',
  },
  customer: {
    a1: {
      title: 'Sabitlenmiş not — pazarlık sınırı',
      detail:
        'Bu hesapta iskonto üst sınırı %15. Üstü için bölge müdürü onayı gerekiyor; son iki yenilemede de bu sınırda kapandı.',
    },
    a2: {
      title: 'Fırsat aşaması güncellendi',
      change: { field: 'Aşama', from: 'Teklif Gönderildi', to: 'Görüşme' },
    },
    a3: {
      title: 'Satın alma müdürüyle görüşüldü',
      detail: 'Filo takip yenilemesinde 120 araçlık pakete geçmek istiyorlar. Bütçe onayı gelecek hafta netleşecek.',
      duration: '18 dk',
    },
    a4: {
      title: 'Revize teklif gönderildi',
      detail: 'Üç senaryolu fiyat tablosu ve 24 aylık toplam sahip olma maliyeti eklendi.',
    },
    a5: {
      title: 'Teklif müşteri tarafından görüntülendi',
      detail: 'Nordwind-teklif-v3.pdf · 6 dk okuma, fiyat sayfasında 2 dk.',
    },
    a6: {
      title: 'Yerinde demo — Gebze depo',
      detail: 'Operasyon ekibi rota ekranını denedi. Mobil sürücü uygulaması ayrı fırsat olarak açıldı.',
      duration: '1 sa 20 dk',
    },
    a7: {
      title: 'Görev tamamlandı: referans müşteri listesi',
      detail: 'Lojistik sektöründen üç referans hesabın iletişim izni alındı.',
    },
    a8: {
      title: 'Cevapsız arama',
      detail: 'Müşteri santral üzerinden aradı, karşılanmadı. Geri arama görevi otomatik oluşturuldu.',
    },
    a9: {
      title: 'Fırsat aşaması güncellendi',
      change: { field: 'Aşama', from: 'İletişim Kuruldu', to: 'Teklif Gönderildi' },
    },
    a10: {
      title: 'İlk teklif gönderildi',
    },
  },
  audit: {
    u1: {
      title: 'Tahsilat eşleşti',
      detail: 'FT-2026-0841 · Banka ekstresinden otomatik eşleştirildi.',
      change: { field: 'Fatura durumu', from: 'Vadesi geçti', to: 'Ödendi' },
    },
    u2: {
      title: 'Sipariş satırı güncellendi',
      detail: 'SP-11482 · Satır 3, alüminyum profil 40x40.',
      change: { field: 'Miktar', from: '250 ad', to: '400 ad' },
    },
    u3: {
      title: 'İrsaliye oluşturuldu',
      detail: 'IR-2026-3312 · 4 kalem, Gebze deposundan çıkış.',
    },
    u4: {
      title: 'Tedarikçi kartı güncellendi',
      change: { field: 'Ödeme vadesi', from: '30 gün', to: '45 gün' },
    },
    u5: {
      title: 'Satın alma talebi onaylandı',
      detail: 'PR-2291 · 12 kalem depo malzemesi, ₺84.500.',
      change: { field: 'Talep durumu', from: 'Onay bekliyor', to: 'Onaylandı' },
    },
    u6: {
      title: 'Stok seviyesi kritik eşiğin altına düştü',
      detail: 'SKU 44-1180 · Gebze deposu.',
      change: { field: 'Eldeki stok', from: '84 ad', to: '31 ad' },
    },
  },
  page: {
    eyebrow: 'Design system',
    title: 'Activity Feed & Timeline',
    description:
      'Bir müşteri ya da projede ne yapıldığını kronolojik anlatan akış: arama, e-posta, durum değişikliği, denetim izi.',
    summary: '{{count}} bölüm · 2 akış türü',
    navLabel: 'Akış bölümleri',
    sections: {
      customerHistory: 'Müşteri geçmişi',
      auditTrail: 'Denetim izi',
      compactFeed: 'Yoğun akış',
    },
    customerSection: {
      title: 'Müşteri geçmişi (CRM)',
      description: 'Bir hesabın tüm temas noktaları tek akışta. Tür süzgeci akışı daraltır, besteci yeni kaydı en başa ekler.',
    },
    auditSection: {
      title: 'Denetim izi (ERP)',
      description:
        'Aynı bileşen, farklı ağırlık: satırların çoğu bir alan değişikliği. Değer çifti «eski → yeni» olarak çizilir, cümleye çevrilmez.',
    },
    compactSection: {
      title: 'Yoğun akış',
      description:
        'Detay panelinde ya da dar bir kartta kullanılan sıkışık varyant. Ray ve kutucuklar düşer; sabit zaman sütunu ve ton noktası kalır.',
      recentHeading: 'Son hareketler',
      auditHeading: 'Denetim izi',
    },
    visibleCount: '{{visible}} / {{total}} hareket',
    customerEmptyDescription: 'Seçili türlerde hareket yok. Bir türü kaldırın ya da «Tümü»ne dönün.',
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      ['Akış her zaman yeniden eskiye', 'Bir geçmişi yukarıdan aşağı okutmak, kullanıcıyı «az önce ne oldu» sorusunun cevabı için sayfanın sonuna sürüklemektir.'],
      ['Bir satır bir olaydır', 'İki şey aynı anda olduysa iki satırdır. Tek satıra sıkıştırılmış «aradı ve teklif gönderdi» filtrelenemez, sayılamaz ve zaman damgası taşıyamaz.'],
      ['Kim, ne, ne zaman — üçü de zorunlu', 'Aktör olmayan satır sisteme aittir ve «Sistem» diye yazılır. Boş bırakmak, kaydı denetim için işe yaramaz kılar.'],
      ['Alan değişikliği cümle değil, çifttir', 'ERP tarafında değişiklik «eski → yeni» olarak çizilir. Düz metne çevrilmiş bir denetim satırı, bir sütun boyunca taranamaz.'],
      ['Zaman göreli, başlık mutlak', 'Satırın kendisi «3 saat önce» der; grup başlığı «Bugün» ya da tam tarihtir. İkisi birlikte hem yakınlığı hem takvimi verir.'],
      ['Sabitleme kronolojiyi bilerek bozar', 'Sabitlenmiş kayıt ayrı başlık altında, akışın dışında durur. Aksi halde sıralamanın bozulduğu izlenimi verir.'],
      ['Tür rengi tek başına taşımaz', 'Her satırda tür adı yazılıdır; ton yalnızca tarama hızını artırır. Gri baskıda da akış okunur kalır.'],
      ['Yazma alanı akışın başındadır', 'Not eklemeyi modala taşımak akışı bayatlatır. Kayıt, sonucun görüneceği yerde alınır.'],
    ],
  },
}
