# 12 · Content & Formatting

Copy is interface. A vague label costs more than a misaligned card.

---

## Language

- **UI copy is Turkish.** Every user-facing string, without exception.
- **Code is English.** Identifiers, comments, commit messages, these documents,
  and the schema keys behind a Turkish label.

```tsx
// ✅
const STAGE_META = { quoted: { label: 'Teklif Gönderildi', tone: 'amber' } }

// ❌
const asamaMeta = { teklif: { etiket: 'Teklif Gönderildi' } }
```

---

## Voice and tone

Professional, direct, unexcited. The user is at work.

| | ✅ | ❌ |
| --- | --- | --- |
| Plain over clever | "Teklif gönderildi" | "Harika! Teklifin yola çıktı 🚀" |
| Second person, no baby talk | "Müşteri seçin" | "Hadi bir müşteri seçelim" |
| No blame | "Bu e-posta zaten kayıtlı" | "Yanlış e-posta girdiniz" |
| No filler | "Kaydedildi" | "İşleminiz başarıyla tamamlanmıştır" |

- **Sentence case** everywhere. Not Title Case, not ALL CAPS — except
  `.nx-eyebrow`, which is uppercase by design.
- **No exclamation marks.** No emoji in product copy.
- **Never apologise for the system's behaviour.** State the fact and the fix.

---

## Microcopy patterns

### Buttons carry the verb

The button says what will happen, not "OK". This matters most in confirmations,
where "OK" forces the user to re-read the question.

| ✅ | ❌ |
| --- | --- |
| "Sil" | "Tamam" |
| "Teklifi gönder" | "Gönder" *(when several things could be sent)* |
| "Vazgeç" | "Hayır" |

### Errors say what would be right

| ❌ | ✅ |
| --- | --- |
| "Geçersiz" | "IBAN 26 karakter olmalı" |
| "Hata oluştu" | "Kayıt kaydedilemedi. Tekrar deneyin." |
| "Zorunlu alan" | "Müşteri seçin" |

Never surface a status code or a stack trace. If the cause is unknown, say what
the user can do next.

### Empty states say all three things

What happened · why · what to do next.

> **Henüz fırsat yok**
> Yeni bir fırsat ekleyin ya da CRM'den içe aktarın.
> `[ Yeni fırsat ]`

And "no results for this filter" is a **different** message with a different
action ("Filtreleri temizle"). See [09 States & Feedback](./09-states-and-feedback.md).

### Labels

Nouns for fields ("Teslim tarihi"), verbs for actions ("Teslimatı planla").
Never a placeholder as the label — it disappears exactly when it is needed.

---

## Numbers, dates, currency

**All formatting goes through `Intl`, locale `tr-TR`.** No manual string
building, no `toFixed()` in a component, no hand-written month names.

Shared formatters live in:

| Where | What |
| --- | --- |
| `src/lib/datetime.ts` | `relativeTime`, `formatTime`, day formatting — everything that shows *when* |
| `src/features/*/data/format.ts` | Feature-local money and percent formatters |

### The rules

| Kind | Format | Example |
| --- | --- | --- |
| Currency | `Intl.NumberFormat('tr-TR', { style: 'currency', currency: 'TRY' })` | `₺1.250.000` |
| Currency in a grid | Same, `maximumFractionDigits: 0` | Kuruş is noise in a scan column |
| Percent | One decimal, Turkish separator | `42,5` — never `42.5` |
| Date | `day: 'numeric', month: 'short', year: 'numeric'` | `12 Eyl 2026` |
| Date (long) | `month: 'long'` | `12 Eylül 2026` |
| Time | `hour/minute: '2-digit'` | `14:32` |
| Relative | `Intl.RelativeTimeFormat`, `numeric: 'auto'` | `3 dakika önce`, `dün` |
| Under a minute | Literal | `şimdi` |
| Null / not applicable | Em dash | `—` |

### Two rules that are easy to get wrong

**Digit alignment.** Every number in a table is tabular. Tables get it from the
base layer; anything outside a table uses the `tnum` utility. A number column
that is not digit-aligned cannot be scanned.

**Null is not zero.** A parked deal with no forecast date renders `—`, not
today's date and not an empty cell. `formatCloseDate` exists for exactly this,
and `Deal.closeDate` is nullable because of it.

### Timestamps

Everything takes an **ISO string** — that is what the backend will send.
Formatting happens at the edge, never in the data layer.

---

## Terminology

One concept, one word, product-wide. A glossary drifts fastest at the seams
between CRM and ERP vocabulary.

| Concept | Word | Not |
| --- | --- | --- |
| A sales opportunity | Fırsat | Potansiyel, lead, satış |
| A price offer | Teklif | Fiyat teklifi, öneri |
| A pipeline stage | Aşama | Durum, statü |
| A record's status | Durum | Statü, hâl |
| Save | Kaydet | Sakla, güncelle |
| Cancel out of a flow | Vazgeç | İptal *(reserve "İptal" for cancelling a business record)* |

When a new domain word appears, add it here in the same pull request.

---

## Internationalisation readiness

There is no i18n framework today and none is planned near-term. What we do now
so that adding one is not a rewrite:

- **Never concatenate a sentence from fragments.** Turkish agglutination and
  vowel harmony make `label + ' ' + suffix` wrong more often than right.
  Write whole strings.
- **Never build a plural by appending.** Write the branches out.
- **Keep formatting in `Intl`**, not in string templates — that half is already
  locale-correct.
- **Keep copy out of shared components.** A shared component takes its strings
  as props (`title`, `description`, `label`), so the same component can serve
  two vocabularies. `EmptyState` and `StatusBadge` both do this correctly.
- **Copy lives next to the feature**, in a `data/` or registry file, not
  inlined deep in JSX — that is where an extractor will look.

---

## Checklist

- [ ] All user-facing strings Turkish; all code English
- [ ] Sentence case, no exclamation marks, no emoji
- [ ] Buttons carry the verb
- [ ] Errors say what would be right
- [ ] Empty ≠ no-results
- [ ] Every number, date and amount goes through `Intl` / a shared formatter
- [ ] Numbers are tabular (`tnum` outside tables)
- [ ] Null renders `—`
- [ ] No sentence assembled from fragments
- [ ] New domain terms added to the terminology table
