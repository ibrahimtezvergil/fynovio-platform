import { addDays, format, startOfWeek } from 'date-fns'
import { delay, http, HttpResponse } from 'msw'
import { endpoints } from '@/api/endpoints'
import type { CalendarEvent } from '@/features/calendar/types'
import { API_BASE } from '@/mocks/apiBase'

/**
 * Everything is anchored to the Monday of the current week, so the page is
 * populated whenever it is opened instead of pointing at a dead month.
 * Swapping in a real backend replaces this handler and nothing else.
 */
const WEEK_START = startOfWeek(new Date(), { weekStartsOn: 1 })

/** `dayOffset` counts from this Monday; negative reaches into last week. */
function at(dayOffset: number, hour: number, minute = 0): string {
  const date = addDays(WEEK_START, dayOffset)
  date.setHours(hour, minute, 0, 0)
  return format(date, "yyyy-MM-dd'T'HH:mm:ss")
}

function day(dayOffset: number): string {
  return format(addDays(WEEK_START, dayOffset), 'yyyy-MM-dd')
}

const EVENTS: CalendarEvent[] = [
  // ---- previous week: the calendar has to look lived-in backwards too
  {
    id: 'e-01',
    title: 'Aylık gelir kapanışı',
    start: at(-4, 16, 0),
    end: at(-4, 17, 30),
    allDay: false,
    kind: 'meeting',
    owner: 'Deniz Arslan',
    location: 'Toplantı Odası 2',
    account: 'İç ekip',
  },
  {
    id: 'e-02',
    title: 'Nordsen teklif revizyonu',
    start: at(-2, 11, 0),
    end: at(-2, 12, 0),
    allDay: false,
    kind: 'call',
    owner: 'Selin Kaya',
    account: 'Nordsen Makina',
  },

  // ---- current week
  {
    id: 'e-03',
    title: 'Haftalık satış standup',
    start: at(0, 9, 30),
    end: at(0, 10, 0),
    allDay: false,
    kind: 'meeting',
    owner: 'Deniz Arslan',
    location: 'Google Meet',
    account: 'İç ekip',
    notes: 'Hafta açılışı: kapanan fırsatlar, tıkanan aşamalar, haftanın hedefi.',
  },
  {
    id: 'e-04',
    title: 'Aycell Enerji — ürün demosu',
    start: at(0, 14, 0),
    end: at(0, 15, 30),
    allDay: false,
    kind: 'meeting',
    owner: 'Selin Kaya',
    location: 'Müşteri ofisi, Ankara',
    account: 'Aycell Enerji',
    notes: 'Saha ekibi için mobil modül ve çevrimdışı kullanım senaryosu istendi.',
  },
  {
    id: 'e-05',
    title: 'Teklif taslağını hazırla',
    start: at(0, 17, 0),
    end: at(0, 18, 0),
    allDay: false,
    kind: 'task',
    owner: 'Mert Yıldız',
    account: 'Aycell Enerji',
  },
  {
    id: 'e-06',
    title: 'Bilen Lojistik — ilk temas',
    start: at(1, 10, 0),
    end: at(1, 10, 45),
    allDay: false,
    kind: 'call',
    owner: 'Mert Yıldız',
    account: 'Bilen Lojistik',
  },
  {
    id: 'e-07',
    title: 'Sözleşme imza toplantısı',
    start: at(1, 13, 0),
    end: at(1, 14, 0),
    allDay: false,
    kind: 'meeting',
    owner: 'Deniz Arslan',
    location: 'Toplantı Odası 1',
    account: 'Kervan Gıda',
  },
  {
    id: 'e-08',
    title: 'Q3 tahmin dosyası teslimi',
    start: day(1),
    end: null,
    allDay: true,
    kind: 'deadline',
    owner: 'Deniz Arslan',
    account: 'İç ekip',
    notes: 'Finansa gidecek nihai tahmin. Aşama bazlı ağırlıklı tutarla birlikte.',
  },
  {
    id: 'e-09',
    title: 'Onboarding — Kervan Gıda',
    start: at(2, 9, 0),
    end: at(2, 11, 0),
    allDay: false,
    kind: 'meeting',
    owner: 'Selin Kaya',
    location: 'Zoom',
    account: 'Kervan Gıda',
  },
  {
    id: 'e-10',
    title: 'Fiyatlandırma çalıştayı',
    start: at(2, 14, 0),
    end: at(2, 16, 30),
    allDay: false,
    kind: 'meeting',
    owner: 'Mert Yıldız',
    location: 'Toplantı Odası 2',
    account: 'İç ekip',
  },
  {
    id: 'e-11',
    title: 'Gecikmiş fırsatları temizle',
    start: at(2, 17, 0),
    end: at(2, 18, 0),
    allDay: false,
    kind: 'task',
    owner: 'Selin Kaya',
    account: 'İç ekip',
  },
  {
    id: 'e-12a',
    title: 'Depo sayım kontrolü',
    start: at(2, 11, 30),
    end: at(2, 12, 30),
    allDay: false,
    kind: 'task',
    owner: 'Mert Yıldız',
    account: 'Kervan Gıda',
  },
  {
    id: 'e-12b',
    title: 'Tedarikçi teyit araması',
    start: at(2, 13, 0),
    end: at(2, 13, 30),
    allDay: false,
    kind: 'call',
    owner: 'Deniz Arslan',
    account: 'Bilen Lojistik',
  },
  {
    id: 'e-12',
    title: 'Metrosan — teknik değerlendirme',
    start: at(3, 10, 30),
    end: at(3, 12, 0),
    allDay: false,
    kind: 'meeting',
    owner: 'Mert Yıldız',
    location: 'Müşteri ofisi, İzmir',
    account: 'Metrosan A.Ş.',
  },
  {
    id: 'e-13',
    title: 'Referans görüşmesi',
    start: at(3, 15, 0),
    end: at(3, 15, 30),
    allDay: false,
    kind: 'call',
    owner: 'Deniz Arslan',
    account: 'Nordsen Makina',
  },
  {
    id: 'e-14',
    title: 'Selin — yıllık izin',
    start: day(4),
    end: day(7),
    allDay: true,
    kind: 'away',
    owner: 'Selin Kaya',
    notes: 'Devralan: Mert Yıldız. Açık fırsatlar geçici olarak ona atandı.',
  },
  {
    id: 'e-15',
    title: 'Hafta kapanış raporu',
    start: at(4, 16, 30),
    end: at(4, 17, 30),
    allDay: false,
    kind: 'task',
    owner: 'Deniz Arslan',
    account: 'İç ekip',
  },

  // ---- next two weeks
  {
    id: 'e-16',
    title: 'Aycell Enerji — teklif sunumu',
    start: at(7, 11, 0),
    end: at(7, 12, 30),
    allDay: false,
    kind: 'meeting',
    owner: 'Mert Yıldız',
    location: 'Müşteri ofisi, Ankara',
    account: 'Aycell Enerji',
  },
  {
    id: 'e-17',
    title: 'Bilen Lojistik — pilot kurulum',
    start: at(8, 9, 0),
    end: at(8, 17, 0),
    allDay: false,
    kind: 'task',
    owner: 'Selin Kaya',
    account: 'Bilen Lojistik',
  },
  {
    id: 'e-18',
    title: 'Metrosan sözleşme son günü',
    start: day(9),
    end: null,
    allDay: true,
    kind: 'deadline',
    owner: 'Deniz Arslan',
    account: 'Metrosan A.Ş.',
  },
  {
    id: 'e-19',
    title: 'Ürün yol haritası bilgilendirmesi',
    start: at(10, 14, 0),
    end: at(10, 15, 0),
    allDay: false,
    kind: 'meeting',
    owner: 'Deniz Arslan',
    location: 'Google Meet',
    account: 'İç ekip',
  },
  {
    id: 'e-20',
    title: 'Kervan Gıda — çeyreklik değerlendirme',
    start: at(14, 10, 0),
    end: at(14, 11, 30),
    allDay: false,
    kind: 'meeting',
    owner: 'Selin Kaya',
    location: 'Toplantı Odası 1',
    account: 'Kervan Gıda',
  },
  {
    id: 'e-21',
    title: 'Yenileme hatırlatma araması',
    start: at(15, 11, 0),
    end: at(15, 11, 30),
    allDay: false,
    kind: 'call',
    owner: 'Mert Yıldız',
    account: 'Nordsen Makina',
  },
  {
    id: 'e-22',
    title: 'Fatura mutabakatı',
    start: day(16),
    end: null,
    allDay: true,
    kind: 'deadline',
    owner: 'Mert Yıldız',
    account: 'İç ekip',
  },
]

export const calendarHandlers = [
  http.get(`${API_BASE}${endpoints.calendar.events}`, async () => {
    await delay(220)
    return HttpResponse.json(EVENTS)
  }),
]
