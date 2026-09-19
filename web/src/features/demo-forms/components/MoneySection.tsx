import { Coins } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  AllocationInput,
  DiscountInput,
  ExchangeRateInput,
  MoneyInput,
  NumberRangeInput,
  NumberInput,
  PercentInput,
  QuantityInput,
  SliderField,
  convertedAmount,
  formatMoney,
  resolveDiscount,
  type CurrencyCode,
  type Discount,
  type ExchangeValue,
  type NumberRange,
  type Unit,
} from '@/components/common/inputs'
import { ControlDemo, show } from '@/features/demo-forms/components/ControlDemo'
import { DemoSection } from '@/components/common/DemoSection'
import { COST_CENTERS, SUGGESTED_RATES } from '@/features/demo-forms/data/options'

export function MoneySection() {
  const { t } = useTranslation('demo-forms')
  const [unitPrice, setUnitPrice] = useState<number | null>(4800)
  const [currency, setCurrency] = useState<CurrencyCode>('TRY')
  const [listPrice, setListPrice] = useState<number | null>(129900.5)
  const [discount, setDiscount] = useState<Discount>({ mode: 'percent', value: 12.5 })
  const [margin, setMargin] = useState<number | null>(28)
  const [quantity, setQuantity] = useState<number | null>(24)
  const [unit, setUnit] = useState<Unit>('adet')
  const [term, setTerm] = useState<number | null>(30)
  const [ceiling, setCeiling] = useState(15)
  const [budget, setBudget] = useState<number[]>([50000, 250000])
  const [exchange, setExchange] = useState<ExchangeValue>({
    amount: 12500,
    currency: 'EUR',
    rate: SUGGESTED_RATES.EUR,
  })
  const [priceRange, setPriceRange] = useState<NumberRange>({ min: 5000, max: null })
  const [allocation, setAllocation] = useState<Record<string, number | null>>({
    satis: 40,
    pazarlama: 25,
    urun: 25,
    destek: 10,
  })
  const [radius, setRadius] = useState(35)

  const discountBase = (unitPrice ?? 0) * (quantity ?? 0)

  return (
    <DemoSection
      id="sayisal"
      title={t('moneySection.title')}
      description={t('moneySection.description')}
      icon={Coins}
    >
      <ControlDemo
        name="<MoneyInput/>"
        title={t('moneySection.unitPriceMulti.title')}
        description={t('moneySection.unitPriceMulti.description')}
        value={`${show(unitPrice)} · ${currency}`}
      >
        <MoneyInput
          value={unitPrice}
          onValueChange={setUnitPrice}
          currency={currency}
          onCurrencyChange={setCurrency}
        />
      </ControlDemo>

      <ControlDemo
        name="<MoneyInput/>"
        title={t('moneySection.unitPriceSingle.title')}
        description={t('moneySection.unitPriceSingle.description')}
        value={`${show(listPrice)} → ${listPrice === null ? '—' : formatMoney(listPrice)}`}
      >
        <MoneyInput value={listPrice} onValueChange={setListPrice} />
      </ControlDemo>

      <ControlDemo
        name="<DiscountInput/>"
        title={t('moneySection.discount.title')}
        description={t('moneySection.discount.description')}
        value={`${show(discount)} → ${formatMoney(resolveDiscount(discountBase, discount))}`}
      >
        <DiscountInput
          discount={discount}
          onDiscountChange={setDiscount}
          currency={currency === 'TRY' ? 'TRY' : currency}
          base={discountBase}
        />
      </ControlDemo>

      <ControlDemo
        name="<PercentInput/>"
        title={t('moneySection.percent.title')}
        description={t('moneySection.percent.description')}
        value={show(margin)}
      >
        <PercentInput value={margin} onValueChange={setMargin} />
      </ControlDemo>

      <ControlDemo
        name="<QuantityInput/>"
        title={t('moneySection.quantity.title')}
        description={t('moneySection.quantity.description')}
        value={`${show(quantity)} ${unit}`}
      >
        <QuantityInput
          value={quantity}
          onValueChange={setQuantity}
          unit={unit}
          onUnitChange={setUnit}
        />
      </ControlDemo>

      <ControlDemo
        name="<NumberInput/>"
        title={t('moneySection.integer.title')}
        description={t('moneySection.integer.description')}
        value={show(term)}
      >
        <NumberInput
          value={term}
          onValueChange={setTerm}
          min={0}
          max={180}
          steppers
          suffix={t('moneySection.integer.suffix')}
        />
      </ControlDemo>

      <ControlDemo
        name="<SliderField/>"
        title={t('moneySection.ceiling.title')}
        description={t('moneySection.ceiling.description')}
        value={show(ceiling)}
      >
        <SliderField
          value={ceiling}
          onValueChange={setCeiling}
          max={40}
          format={{ style: 'percent', maximumFractionDigits: 0 }}
          thumbLabels={[t('moneySection.ceiling.thumbLabel')]}
        />
      </ControlDemo>

      <ControlDemo
        name="<SliderField/>"
        title={t('moneySection.budget.title')}
        description={t('moneySection.budget.description')}
        value={show(budget)}
      >
        <SliderField
          value={budget}
          onValueChange={setBudget}
          min={0}
          max={500000}
          step={10000}
          format={{ style: 'currency', currency: 'TRY', maximumFractionDigits: 0 }}
          thumbLabels={[t('moneySection.budget.lowerLabel'), t('moneySection.budget.upperLabel')]}
        />
      </ControlDemo>
      <ControlDemo
        name="<ExchangeRateInput/>"
        title={t('moneySection.exchange.title')}
        description={t('moneySection.exchange.description')}
        value={`${show(exchange)} → ${formatMoney(convertedAmount(exchange, 'TRY'))}`}
        wide
      >
        <ExchangeRateInput
          aria-label={t('moneySection.exchange.aria')}
          value={exchange}
          onValueChange={setExchange}
          suggestedRates={SUGGESTED_RATES}
        />
      </ControlDemo>

      <ControlDemo
        name="<NumberRangeInput/>"
        title={t('moneySection.priceRange.title')}
        description={t('moneySection.priceRange.description')}
        value={show(priceRange)}
      >
        <NumberRangeInput
          aria-label={t('moneySection.priceRange.aria')}
          value={priceRange}
          onValueChange={setPriceRange}
          min={0}
          format={{ maximumFractionDigits: 0 }}
          prefix="₺"
        />
      </ControlDemo>

      <ControlDemo
        name="<SliderField/>"
        title={t('moneySection.radius.title')}
        description={t('moneySection.radius.description')}
        value={`${show(radius)} km`}
      >
        <SliderField
          value={radius}
          onValueChange={setRadius}
          min={5}
          max={250}
          step={5}
          format={{ style: 'unit', unit: 'kilometer', unitDisplay: 'short' }}
          thumbLabels={[t('moneySection.radius.thumbLabel')]}
        />
      </ControlDemo>

      <ControlDemo
        name="<AllocationInput/>"
        title={t('moneySection.allocation.title')}
        description={t('moneySection.allocation.description')}
        value={show(allocation)}
        wide
      >
        <AllocationInput
          aria-label={t('moneySection.allocation.aria')}
          targets={COST_CENTERS}
          value={allocation}
          onValueChange={setAllocation}
          total={100}
          mode="percent"
        />
      </ControlDemo>
    </DemoSection>
  )
}
