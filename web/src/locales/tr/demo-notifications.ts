export default {
  page: {
    eyebrow: 'Design system',
    title: 'Toast & Alert',
    description: 'Anlık aksiyon geri bildirimi Toast ile, sayfa içinde kalıcı durum bilgisi Alert ile anlatılır.',
    rulesHeading: 'Bu sayfadaki kurallar',
    rules: [
      ['Toast geçici, Alert kalıcı', 'Bir aksiyonun sonucu Toast ile bildirilir ve kendi kendine kapanır; sayfanın kendi durumu Alert ile anlatılır ve kullanıcı kapatana kadar yerinde kalır.'],
      ['Renk tek başına anlam taşımaz', 'Her durumda ikon ve başlık da tekrarlanır — ton sadece rengi değil, ikonu da değiştirir.'],
      ['Konum sabit', 'Toast her zaman ekranın aynı köşesinde çıkar (sağ alt) — kullanıcı her seferinde nereye bakacağını yeniden öğrenmez.'],
      ['Kısa ve eylem odaklı', 'Başlık ne olduğunu söyler, açıklama tek cümlede neden/ne yapılacağını ekler. İki cümleyi geçen metin bir Alert\'e taşınmalı.'],
      ['Hata geri bildirimi asla sessiz değil', 'Başarısız bir işlem her zaman error Toast ile bildirilir — sadece konsola loglamak yeterli değildir.'],
      ['Geri alınabilir aksiyonlar action taşır', 'Silme gibi tersine çevrilebilir işlemler Toast\'un action butonuyla "Geri al" seçeneği sunar.'],
    ],
  },
  sections: {
    toast: {
      title: 'Toast',
      description: 'Ekranın köşesinde birkaç saniye görünüp kendiliğinden kaybolan bildirim. sonner ile tetiklenir, konumu sağ alt köşede sabittir (bkz. src/App.tsx).',
    },
    alert: {
      title: 'Alert',
      description: 'Sayfanın kendi durumunu anlatan, kullanıcı kapatana ya da koşul değişene kadar yerinde kalan bildirim kartı.',
    },
  },
  toast: {
    success: {
      title: 'Başarılı',
      description: 'Bir işlem beklendiği gibi tamamlandığında.',
      message: 'Fırsat aşaması güncellendi',
      trigger: 'Göster',
    },
    error: {
      title: 'Hata',
      description: 'Bir işlem başarısız olduğunda — sessizce loglamak yerine her zaman bildirilir.',
      message: 'Kayıt silinemedi — sunucuya ulaşılamıyor',
      trigger: 'Göster',
    },
    warning: {
      title: 'Uyarı',
      description: 'İşlem tamamlandı ama dikkat edilmesi gereken bir durum var.',
      message: '3 fırsatın kapanış tarihi geçti',
      trigger: 'Göster',
    },
    info: {
      title: 'Bilgi',
      description: 'Kullanıcının bilmesi gereken, aksiyon gerektirmeyen bir gelişme.',
      message: 'Rapor arka planda oluşturuluyor',
      trigger: 'Göster',
    },
    action: {
      title: 'Aksiyonlu',
      description: 'Geri alınabilir bir işlemde Toast, kapanmadan önce bir çıkış yolu sunar.',
      message: 'Fırsat silindi',
      deletedDescription: 'Ayşe Kaya — Kurumsal Paket',
      undoLabel: 'Geri al',
      restoredMessage: 'Fırsat geri yüklendi',
      trigger: 'Göster',
    },
    promise: {
      title: 'Beklemeli (promise)',
      description: "Sonucu önceden bilinmeyen bir işlem: loading olarak başlar, sonuca göre success ya da error'a döner.",
      loading: 'Teklif PDF olarak hazırlanıyor…',
      success: 'Teklif hazır — indirilebilir',
      error: 'Teklif hazırlanamadı',
      trigger: 'Göster',
    },
  },
  alerts: {
    twoFactor: {
      title: 'İki adımlı doğrulama açık',
      description: 'Hesap kimlik doğrulayıcı uygulama ile korunuyor.',
    },
    payment: {
      title: 'Ödeme yöntemi geçersiz',
      description: 'Kayıtlı kartın süresi doldu — faturalama bölümünden güncelle.',
    },
    storage: {
      title: 'Depolama alanı azalıyor',
      description: 'Kullanılan alan %92 — 30 gün sonra yeni dosya yüklenemez.',
    },
    maintenance: {
      title: 'Bakım planlandı',
      description: '3 Ekim 02:00–02:30 arası panel kısa süreliğine erişilemez olacak.',
    },
  },
}
