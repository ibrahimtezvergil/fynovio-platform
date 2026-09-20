export default {
  loginPage: {
    title: 'Panele giriş',
    description: 'Hesabınızla oturum açın.',
  },
  loginForm: {
    emailLabel: 'E-posta',
    passwordLabel: 'Şifre',
    submitting: 'Giriş yapılıyor',
    submit: 'Giriş yap',
    genericError: 'Giriş yapılamadı. Lütfen tekrar deneyin.',
    invalidCredentials: 'E-posta veya şifre hatalı.',
    rateLimited: 'Çok fazla deneme yapıldı. {{seconds}} saniye sonra tekrar deneyin.',
    rateLimitedNoWait: 'Çok fazla deneme yapıldı. Biraz sonra tekrar deneyin.',
  },
  schema: {
    emailInvalid: 'Geçerli bir e-posta girin.',
    passwordRequired: 'Şifrenizi girin.',
  },
  session: {
    restoring: 'Oturum geri yükleniyor',
  },
  tenantSelector: {
    title: 'Kuruluş seçin',
    description: 'Hesabınız birden fazla kuruluşa bağlı. Devam etmek için birini seçin.',
    tenantLabel: 'Kuruluş {{id}}',
    notPermitted: 'Bu kuruluşa erişiminiz yok.',
    genericError: 'Kuruluş seçilemedi. Lütfen tekrar deneyin.',
  },
  tenantSwitcher: {
    label: 'Kuruluş',
    failed: 'Kuruluş değiştirilemedi.',
  },
  noAccess: {
    title: 'Erişiminiz yok',
    noMembershipDescription: 'Hesabınız henüz bir kuruluşa bağlı değil. Yöneticinizden davet isteyin.',
    forbiddenDescription: 'Bu içeriği görüntülemek için yetkiniz yok.',
    backToDashboard: 'Panele dön',
    signOut: 'Çıkış yap',
  },
}
