# 07 · Forms

Stack: `react-hook-form` + `zod` via `@hookform/resolvers/zod`.
Controls: `src/components/common/inputs/` (rendered live at `/demo/forms`).

---

## Structure

```
src/features/<name>/
  schema.ts                 ← the zod schema, colocated with the feature
  components/<Form>.tsx     ← the form
```

- **The schema lives beside the feature**, never in a shared schema folder.
  It is domain knowledge, not infrastructure.
- **Form state never goes into a zustand store.** Stores hold what outlives the
  form. This is not a preference — a store-backed form re-renders the tree on
  every keystroke and loses RHF's dirty/touched tracking.
- **A form spanning several cards shares one `FormProvider`**, and the `<form>`
  element sits on whichever component owns the submit.

---

## The control contract

Every shared input is controlled and takes `value` + `onValueChange`. `Field`'s
render prop hands a control exactly the props it needs:

```tsx
<Field label="Tutar" hint="KDV hariç" error={errors.amount?.message}>
  {(props) => <MoneyInput {...props} currency="TRY" />}
</Field>
```

If `<Field>{(p) => <X {...p} />}</Field>` does not work, the control is not
finished. See [04 Component Standards](./04-component-standards.md).

### The kit, by group

| Group | Controls |
| --- | --- |
| **Money** | `MoneyInput` (amount + currency), `DiscountInput` (fixed or %), `PercentInput`, `QuantityInput` (+ unit), `NumberRangeInput`, `ExchangeRateInput`, `AllocationInput` (must close on its total) |
| **Identity** | `PhoneInput` (dial codes, as-you-type, E.164 out), `MaskedInput` (IBAN, VKN/TCKN, card, expiry, CVC — each checks its own check digits), `BarcodeInput` (tells a wedge scanner from a human) |
| **Selection** | `InlineSelect`, `RichSelect`, `ComboboxInput`, `AsyncCombobox`, `MultiSelect`, `TagInput`, `CascadingSelect`, `TreeSelect` |
| **Time** | `DatePicker`, `Calendar`, `TimeRangeInput`, `DurationInput` (stored as minutes), `QuarterPicker` |
| **Content** | `RichTextInput` (tiptap), `SignaturePad`, `ImageUpload` (in-place crop), `FileDropzone`, `ColorInput`, `RatingInput`, `OtpInput`, `TextareaCounter` |

Composites that exist because the primitives alone were not enough:
`LineItemsEditor` (a quote's line table) and `InstallmentPlanEditor`.

---

## Validation

### Where

Zod owns validation. Not the control, not the submit handler. A control may
*format* (phone, IBAN) and may reject impossible keystrokes, but it never
decides whether a value is acceptable.

### When

| Moment | Behaviour |
| --- | --- |
| While typing, field never blurred | Silent. Never show an error the user has not had a chance to avoid. |
| On blur | Validate that field. |
| After the first failed submit | Switch to validate-on-change for the fields that failed. |
| On submit | Validate everything, focus the first invalid field, announce the count. |

RHF's `mode: 'onTouched'` with `reValidateMode: 'onChange'` gives this.

### Error copy

Errors say what is wrong **and** what would be right:

| ❌ | ✅ |
| --- | --- |
| "Geçersiz" | "IBAN 26 karakter olmalı" |
| "Hata" | "Bu e-posta zaten kayıtlı" |
| "Zorunlu alan" | "Müşteri seçin" |

See [12 Content & Formatting](./12-content-and-formatting.md).

### Server-side validation

`ApiError.fields` (`src/types/index.ts`) is the 422 contract: `Record<string,
string[]>`, field path to messages. It matches Laravel's
`ValidationException` response shape (`{ message, errors: { field: string[]
} }`) — the primary backend stack these forms will eventually talk to — and
it matches zod's own `error.flatten().fieldErrors` on the client, so a
client-side pre-check and a real 422 land in the same shape. `useAppMutation`
(`src/lib/mutations/useAppMutation.ts`) throws exactly this shape when its
optional `schema` fails to parse, before `mutationFn` ever runs.

Map it onto RHF the same way regardless of source:

```ts
Object.entries(error.fields ?? {}).forEach(([path, messages]) => {
  setError(path as Path<FormValues>, { message: messages[0] })
})
```

Only the first message per field reaches the UI — the `Field` error slot
shows one line (see "When" above). A field path the form doesn't have a
control for (a cross-field or record-level rule) has nowhere to land; surface
those from `error.message` instead, e.g. as a form-level `Alert`.

### Marking required

Mark **optional** fields when most are required; mark required fields when most
are optional. Never both, never neither. Whichever you choose, say so once at
the top of the form rather than decorating every label.

---

## Layout

- **One column by default.** A form is read top to bottom; two columns double
  the eye's travel and break tab order expectations.
- Two columns only for genuinely paired fields (start/end, city/postcode) —
  and they stay on one row at every width where both fit.
- Group into `Card` sections with an `h2` when a form runs past ~8 fields.
- Labels sit above their control. Placeholder is never the label.
- Helper text sits below the label, before the control. Error text replaces it.

---

## Saving

Two modes, chosen by consequence — never mixed within one surface:

| Mode | When | Affordance |
| --- | --- | --- |
| **Explicit commit** | The change has downstream effects: money, status, anything another user sees. | A `SaveBar` with the primary action and a discard. Dirty state is visible. |
| **Autosave** | Safe, personal, easily reversed: preferences, filters, notes. | A save-state chip (`idle → saving → saved`) in an `aria-live="polite"` region. |

`SaveBar` in `src/features/settings/components/` is the reference implementation
and already carries the live region.

**Never autosave a field whose change is not obviously reversible.** When in
doubt, explicit commit.

### Destructive actions

- Reversible → do it, and offer undo. (Principle 9.)
- Irreversible → `AlertDialog`, where clicking outside does **not** dismiss.
- The confirm button carries the verb, not "OK": "Sil", "Sıfırla", "Gönder".
- Type-to-confirm only for genuinely catastrophic, tenant-level actions.

---

## Keyboard

- Tab order follows visual order. No `tabIndex` above 0, ever.
- Enter submits a single-line form. In a multi-field form Enter inside a text
  input must not submit unless that is the only reasonable action.
- Escape closes the overlay a form sits in — after warning about unsaved
  changes, if the form is dirty.
- ⌘/Ctrl+Enter is reserved as the "complete this" chord (backlog item 10).

---

## Checklist for a new form

- [ ] Zod schema in `features/<name>/schema.ts`
- [ ] No form field in a zustand store
- [ ] Every control goes through `Field` and takes `value` / `onValueChange`
- [ ] Errors say what would be right
- [ ] Required/optional marked one way, stated once
- [ ] Save mode matches consequence; autosave has a live region
- [ ] First invalid field receives focus on failed submit
- [ ] Verified in both themes; controls follow density if inside a dense region
