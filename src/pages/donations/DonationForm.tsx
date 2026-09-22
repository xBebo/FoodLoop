import { motion } from 'framer-motion'
import { AlertCircle, ArrowLeft, ArrowRight, Check, RotateCcw } from 'lucide-react'
import { useRef, useState, type FormEvent } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { PATHS, donationPath } from '../../app/routes'
import { DonationCard } from '../../components/food/DonationCard'
import { categoryVisual, formatUnitQuantity, type Listing } from '../../components/food/presentation'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { Button } from '../../components/ui/Button'
import { Field, FieldFrame } from '../../components/ui/Field'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { ApiError, codeOf } from '../../lib/api/client'
import { createDonation, updateDonation, type DonationForEdit, type DonationInput } from '../../lib/api/donations'
import { getCategories, type Category, type QuantityUnit } from '../../lib/api/marketplace'
import { useLoad } from '../../lib/api/useLoad'
import { cn } from '../../lib/cn'
import { toLocalInput } from '../../lib/expiry'
import { spring } from '../../lib/motion'
import { useSession } from '../../lib/session/context'
import './donation-form.css'

type Values = {
  title: string
  categoryId: string | null
  quantity: string
  unit: QuantityUnit
  preparedAt: string // datetime-local value
  expiresAt: string // datetime-local value
  pickupAddress: string
  storageInstructions: string
  description: string
}
type Key = keyof Values
type Errors = Partial<Record<Key, string>>

/** FoodLoop.Domain.Enums.QuantityUnit. */
const UNITS: QuantityUnit[] = ['Meals', 'Kilograms', 'Packages']

// The backend's own limits (DonationService.ValidateAndNormalize), mirrored for immediate feedback only.
const TITLE_MAX = 200
const DESCRIPTION_MAX = 2000
const STORAGE_MAX = 1000
const ADDRESS_MAX = 500

// Browser-side checks only (presence, shape). The API is the authority and re-validates everything.
function validate(v: Values): Errors {
  const errors: Errors = {}
  const qty = Number(v.quantity)
  if (!v.title.trim()) errors.title = 'Give the donation a short title.'
  else if (v.title.trim().length > TITLE_MAX) errors.title = `Keep the title under ${TITLE_MAX} characters.`
  if (!v.categoryId) errors.categoryId = 'Choose the category that fits best.'
  if (!v.quantity) errors.quantity = 'Enter a quantity.'
  else if (!Number.isFinite(qty) || qty <= 0) errors.quantity = 'Use a number greater than zero.'
  if (!v.preparedAt) errors.preparedAt = 'When was this food prepared?'
  if (!v.expiresAt) errors.expiresAt = 'Choose when this food should be collected by.'
  else if (v.preparedAt && new Date(v.expiresAt).getTime() <= new Date(v.preparedAt).getTime())
    errors.expiresAt = 'Collect-by must be after the preparation time.'
  if (!v.pickupAddress.trim()) errors.pickupAddress = 'Enter the pickup address.'
  else if (v.pickupAddress.trim().length > ADDRESS_MAX) errors.pickupAddress = `Keep it under ${ADDRESS_MAX} characters.`
  if (v.storageInstructions.length > STORAGE_MAX) errors.storageInstructions = `Keep it under ${STORAGE_MAX} characters.`
  if (v.description.length > DESCRIPTION_MAX) errors.description = `Keep it under ${DESCRIPTION_MAX} characters.`
  return errors
}

const SECTIONS: { id: string; index: string; title: string; keys: Key[] }[] = [
  { id: 'sec-food', index: '01', title: 'Food', keys: ['title', 'categoryId'] },
  { id: 'sec-timing', index: '02', title: 'Quantity & timing', keys: ['quantity', 'preparedAt', 'expiresAt'] },
  { id: 'sec-pickup', index: '03', title: 'Pickup', keys: ['pickupAddress', 'storageInstructions'] },
  { id: 'sec-details', index: '04', title: 'Details', keys: ['description'] },
]

const toInput = (v: Values): DonationInput => ({
  categoryId: v.categoryId!,
  title: v.title.trim(),
  description: v.description.trim(),
  quantity: Number(v.quantity),
  unit: v.unit,
  preparedAt: new Date(v.preparedAt).toISOString(),
  expiresAt: new Date(v.expiresAt).toISOString(),
  storageInstructions: v.storageInstructions.trim(),
  pickupAddress: v.pickupAddress.trim(),
})

type Submit = { status: 'idle' } | { status: 'submitting' } | { status: 'failed'; message: string; stale?: boolean }

/** Copy for a failed save, from the stable code (plus the backend's own rule text for donation.invalid). */
function saveFailure(error: unknown): Extract<Submit, { status: 'failed' }> {
  switch (codeOf(error)) {
    case 'donation.invalid':
      return { status: 'failed', message: (error as ApiError).detail ?? 'Please check the details and try again.' }
    case 'validation':
      return { status: 'failed', message: 'Some details are missing or not in the expected format.' }
    case 'donation.stale':
      return {
        status: 'failed',
        stale: true,
        message: 'This draft was changed somewhere else since you opened it. Reload to get the latest version — your edits here were not saved.',
      }
    case 'donation.invalid_state':
    case 'donation.not_found':
      return { status: 'failed', message: 'This donation is no longer a draft, so it can’t be changed.' }
    case 'organization.not_active':
      return { status: 'failed', message: 'Your organization must be active before it can list donations.' }
    case 'antiforgery.invalid':
      return { status: 'failed', message: 'Your session changed in the meantime. Nothing was saved — please try again.' }
    case 'network':
      return { status: 'failed', message: 'We couldn’t reach FoodLoop. Check My donations before trying again — the save may have gone through.' }
    default:
      return { status: 'failed', message: 'Something went wrong on our side and nothing was saved. Please try again in a moment.' }
  }
}

type DonationFormProps = { mode: 'create' } | { mode: 'edit'; donation: DonationForEdit; onReload: () => void }

/** Create / Edit share this form. Create saves a Draft; Edit is Draft-only and sends the opaque version back. */
export function DonationForm(props: DonationFormProps) {
  const navigate = useNavigate()
  const { state } = useSession()
  const orgName = (state.status === 'authenticated' && state.session.organization?.name) || 'Your organization'
  const editing = props.mode === 'edit' ? props.donation : null
  const categories = useLoad('categories', getCategories)
  const [values, setValues] = useState<Values>(() =>
    editing
      ? {
          title: editing.title,
          categoryId: editing.categoryId,
          quantity: String(editing.quantity),
          unit: editing.unit,
          preparedAt: toLocalInput(editing.preparedAtUtc),
          expiresAt: toLocalInput(editing.expiresAtUtc),
          pickupAddress: editing.pickupAddress,
          storageInstructions: editing.storageInstructions,
          description: editing.description,
        }
      : {
          title: '',
          categoryId: null,
          quantity: '',
          unit: 'Meals',
          preparedAt: toLocalInput(new Date().toISOString()),
          expiresAt: '',
          pickupAddress: '',
          storageInstructions: '',
          description: '',
        },
  )
  const [errors, setErrors] = useState<Errors>({})
  const [submit, setSubmit] = useState<Submit>({ status: 'idle' })
  const formRef = useRef<HTMLFormElement>(null)
  const inFlight = useRef(false)

  const complete = validate(values)
  const sectionDone = (keys: Key[]) => keys.every((k) => !complete[k])
  const doneCount = SECTIONS.filter((s) => sectionDone(s.keys)).length
  const selectedCategory = categories.data?.find((c) => c.id === values.categoryId)

  function set<K extends Key>(key: K, value: Values[K]) {
    setValues((v) => ({ ...v, [key]: value }))
    if (errors[key])
      setErrors((prev) => {
        const next = { ...prev }
        delete next[key]
        return next
      })
    if (submit.status === 'failed' && !submit.stale) setSubmit({ status: 'idle' })
  }

  async function onSubmit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault()
    if (inFlight.current) return
    const found = validate(values)
    setErrors(found)
    const first = (Object.keys(found) as Key[])[0]
    if (first) {
      setSubmit({ status: 'idle' })
      formRef.current?.querySelector<HTMLElement>(`[name="${first}"]`)?.focus()
      return
    }
    inFlight.current = true
    setSubmit({ status: 'submitting' })
    try {
      if (editing) {
        await updateDonation(editing.id, toInput(values), editing.version)
        navigate(donationPath(editing.id))
      } else {
        const { id } = await createDonation(toInput(values))
        navigate(donationPath(id))
      }
    } catch (error) {
      setSubmit(saveFailure(error))
    } finally {
      inFlight.current = false
    }
  }

  // Live preview: the same card the marketplace renders, fed by the form state.
  const preview: Listing = {
    id: editing?.id ?? 'preview',
    title: values.title.trim() || 'Your donation title',
    description: values.description.trim(),
    category: categoryVisual(selectedCategory ?? { name: 'Category' }),
    quantityLabel: Number(values.quantity) > 0 ? formatUnitQuantity(Number(values.quantity), values.unit) : '—',
    expiresAt: values.expiresAt && !Number.isNaN(Date.parse(values.expiresAt)) ? new Date(values.expiresAt).toISOString() : '',
    pickupAddress: values.pickupAddress.trim() || '—',
    status: 'Available',
    donorName: orgName,
  }

  const sectionState = (keys: Key[]) => (keys.some((k) => errors[k]) ? 'error' : sectionDone(keys) ? 'done' : 'todo')
  const submitting = submit.status === 'submitting'

  return (
    <div className="container ws-page dform">
      <Link to={editing ? donationPath(editing.id) : PATHS.donations} className="ws-back">
        <ArrowLeft aria-hidden="true" />
        {editing ? 'Back to donation' : 'My donations'}
      </Link>

      <header className="ws-intro dform__intro">
        <SectionEyebrow>
          {editing ? `Donor workspace · Draft #${editing.id.slice(0, 8).toUpperCase()}` : 'Donor workspace · New listing'}
        </SectionEyebrow>
        <h1 className="ws-intro__title">
          {editing ? (
            <>
              Edit <em>draft</em>
            </>
          ) : (
            <>
              Create <em>donation</em>
            </>
          )}
        </h1>
        <p className="t-lead ws-intro__lead">
          {editing
            ? 'Update the draft before you publish it. Changes appear in the preview as you type.'
            : 'Four short sections. It saves as a draft — you review it, then publish it to the marketplace.'}
        </p>
      </header>

      <div className="dform__grid">
        <form ref={formRef} className="dform__form" noValidate onSubmit={onSubmit} aria-label={editing ? 'Edit donation' : 'Create donation'}>
          {SECTIONS.map((section) => {
            const state = sectionState(section.keys)
            return (
              <fieldset key={section.id} id={section.id} className={cn('dsec', `is-${state}`)}>
                <legend className="dsec__legend">
                  <span className="dsec__index">{section.index}</span>
                  <span className="dsec__title">{section.title}</span>
                  <span className="dsec__state">
                    {state === 'done' ? (
                      <>
                        <Check aria-hidden="true" /> Complete
                      </>
                    ) : state === 'error' ? (
                      <>
                        <AlertCircle aria-hidden="true" /> Needs attention
                      </>
                    ) : (
                      'To do'
                    )}
                  </span>
                </legend>

                <div className="dsec__fields">
                  {section.id === 'sec-food' && (
                    <>
                      <Field
                        id="title"
                        label="Title"
                        hint={`What is it, in a few words — e.g. “Fresh vegetable crate”. ${values.title.length}/${TITLE_MAX}`}
                        value={values.title}
                        onChange={(e) => set('title', e.target.value)}
                        error={errors.title}
                        required
                      />
                      <CategoryPicker
                        categories={categories.data}
                        failed={categories.error !== undefined}
                        onRetry={categories.reload}
                        value={values.categoryId}
                        error={errors.categoryId}
                        onChange={(c) => set('categoryId', c)}
                      />
                    </>
                  )}

                  {section.id === 'sec-timing' && (
                    <>
                      <div className="dsec__row">
                        <Field
                          id="quantity"
                          label="Quantity"
                          type="number"
                          inputMode="decimal"
                          min={0}
                          step="any"
                          value={values.quantity}
                          onChange={(e) => set('quantity', e.target.value)}
                          error={errors.quantity}
                          required
                        />
                        <FieldFrame id="unit" label="Unit">
                          {(describedBy) => (
                            <select
                              id="unit"
                              name="unit"
                              className="field__input field__select"
                              value={values.unit}
                              aria-describedby={describedBy}
                              onChange={(e) => set('unit', e.target.value as QuantityUnit)}
                            >
                              {UNITS.map((u) => (
                                <option key={u} value={u}>
                                  {u}
                                </option>
                              ))}
                            </select>
                          )}
                        </FieldFrame>
                      </div>
                      <Field
                        id="preparedAt"
                        label="Prepared at"
                        type="datetime-local"
                        value={values.preparedAt}
                        onChange={(e) => set('preparedAt', e.target.value)}
                        error={errors.preparedAt}
                        required
                      />
                      <Field
                        id="expiresAt"
                        label="Collect by"
                        hint="The latest time this food can be picked up."
                        type="datetime-local"
                        value={values.expiresAt}
                        onChange={(e) => set('expiresAt', e.target.value)}
                        error={errors.expiresAt}
                        required
                      />
                    </>
                  )}

                  {section.id === 'sec-pickup' && (
                    <>
                      <Field
                        id="pickupAddress"
                        label="Pickup address"
                        hint="Where the courier collects the food."
                        autoComplete="street-address"
                        value={values.pickupAddress}
                        onChange={(e) => set('pickupAddress', e.target.value)}
                        error={errors.pickupAddress}
                        required
                      />
                      <Field
                        id="storageInstructions"
                        label="Storage (optional)"
                        hint="e.g. “Keep chilled below 5 °C”."
                        value={values.storageInstructions}
                        onChange={(e) => set('storageInstructions', e.target.value)}
                        error={errors.storageInstructions}
                      />
                    </>
                  )}

                  {section.id === 'sec-details' && (
                    <FieldFrame
                      id="description"
                      label="Description (optional)"
                      hint={`Contents, packaging, allergens. ${values.description.length}/${DESCRIPTION_MAX}`}
                      error={errors.description}
                    >
                      {(describedBy) => (
                        <textarea
                          id="description"
                          name="description"
                          className="field__input field__textarea"
                          rows={5}
                          value={values.description}
                          aria-describedby={describedBy}
                          aria-invalid={errors.description ? true : undefined}
                          onChange={(e) => set('description', e.target.value)}
                        />
                      )}
                    </FieldFrame>
                  )}
                </div>
              </fieldset>
            )
          })}

          <div className="dform__submit">
            <MagneticButton>
              <Button type="submit" size="lg" loading={submitting} disabled={submitting} iconEnd={<ArrowRight />}>
                {submitting ? 'Saving…' : editing ? 'Save draft' : 'Save as draft'}
              </Button>
            </MagneticButton>
            <Button variant="ghost" size="lg" to={editing ? donationPath(editing.id) : PATHS.donations}>
              Cancel
            </Button>
          </div>
          <div role="status" className="dform__status">
            {submit.status === 'failed' && (
              <p className="ws-notice">
                <AlertCircle aria-hidden="true" />
                <span>{submit.message}</span>
              </p>
            )}
            {submit.status === 'failed' && submit.stale && props.mode === 'edit' && (
              <Button variant="outline" size="sm" iconStart={<RotateCcw />} onClick={props.onReload}>
                Reload latest version
              </Button>
            )}
          </div>
        </form>

        <aside className="dform__aside" aria-labelledby="preview-title">
          <div className="readiness">
            <p className="readiness__head">
              <span className="t-label">Listing readiness</span>
              <span className="readiness__count t-data">
                {doneCount}/{SECTIONS.length}
              </span>
            </p>
            <ol role="list" className="readiness__steps">
              {SECTIONS.map((s) => {
                const done = sectionDone(s.keys)
                return (
                  <li key={s.id} className={cn('readiness__step', done && 'is-done')}>
                    <span className="readiness__bar" aria-hidden="true" />
                    <span className="readiness__label">
                      {s.title}
                      <span className="visually-hidden">{done ? ' — complete' : ' — to do'}</span>
                    </span>
                  </li>
                )
              })}
            </ol>
          </div>

          <h2 id="preview-title" className="dform__preview-title t-label">
            Live preview
          </h2>
          <DonationCard donation={preview} preview className="dform__preview" />
          <p className="dform__preview-note">How organizations will see this listing once you publish it.</p>
        </aside>
      </div>
    </div>
  )
}

function CategoryPicker({
  categories,
  failed,
  onRetry,
  value,
  error,
  onChange,
}: {
  categories?: Category[]
  failed: boolean
  onRetry: () => void
  value: string | null
  error?: string
  onChange: (id: string) => void
}) {
  return (
    <fieldset className={cn('cat-pick', error && 'has-error')}>
      <legend className="field__label">Category</legend>
      {categories ? (
        <div className="cat-pick__grid">
          {categories.map((c) => {
            const meta = categoryVisual(c)
            const Icon = meta.icon
            const checked = value === c.id
            return (
              <label key={c.id} className={cn('cat-pick__option', checked && 'is-checked')}>
                <input
                  type="radio"
                  name="categoryId"
                  value={c.id}
                  checked={checked}
                  onChange={() => onChange(c.id)}
                  className="cat-pick__input"
                  aria-describedby={error ? 'category-error' : undefined}
                />
                {checked && <motion.span layoutId="cat-pick-indicator" className="cat-pick__indicator" transition={spring.indicator} />}
                <Icon aria-hidden="true" className="cat-pick__icon" />
                <span className="cat-pick__label">{meta.label}</span>
              </label>
            )
          })}
        </div>
      ) : failed ? (
        <div className="field__hint">
          Categories couldn’t be loaded.{' '}
          <Button variant="ghost" size="sm" iconStart={<RotateCcw />} onClick={onRetry}>
            Try again
          </Button>
        </div>
      ) : (
        <p className="field__hint" role="status">
          Loading categories…
        </p>
      )}
      {error && (
        <p id="category-error" className="field__error">
          <AlertCircle aria-hidden="true" />
          {error}
        </p>
      )}
    </fieldset>
  )
}
