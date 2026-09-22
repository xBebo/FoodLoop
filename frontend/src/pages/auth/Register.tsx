import { AlertCircle, ArrowRight, CircleCheck } from 'lucide-react'
import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { PATHS } from '../../app/routes'
import { BotanicalBranch, BotanicalCorner } from '../../components/brand/Botanical'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { RevealGroup, RevealItem } from '../../components/motion/Reveal'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { register, type OrganizationType } from '../../lib/api/auth'
import { ApiError } from '../../lib/api/client'
import { AuthLayout } from './AuthLayout'
import { Field } from '../../components/ui/Field'
import { apiErrors, rules, useAuthForm } from './useAuthForm'

// Organizations self-register as Donor or Beneficiary only; courier and admin accounts are created by FoodLoop.
const PARTICIPATION: { value: OrganizationType; label: string; text: string }[] = [
  { value: 'Donor', label: 'Donor', text: 'We have surplus food to share.' },
  { value: 'Beneficiary', label: 'Beneficiary', text: 'We receive food for our community.' },
]

const NEXT_STEPS = ['Tell us about your organization', 'Create your sign-in', 'We review and welcome you in']

const get = (data: FormData, key: string) => String(data.get(key) ?? '')

export function Register() {
  const [submitted, setSubmitted] = useState(false)
  const doneRef = useRef<HTMLHeadingElement>(null)
  const { errors, submitting, onSubmit } = useAuthForm(
    (data) => ({
      organizationName: rules.required(get(data, 'organizationName'), 'Enter your organization’s name.'),
      licenseNumber: rules.required(get(data, 'licenseNumber'), 'Enter your organization’s license number.'),
      organizationType: get(data, 'organizationType') ? '' : 'Choose how your organization takes part.',
      email: rules.email(get(data, 'email')),
      password: rules.password(get(data, 'password')),
      // Acknowledged in the browser only; it is not sent.
      terms: get(data, 'terms') ? '' : 'Please accept the terms to continue.',
    }),
    async (data) => {
      try {
        await register({
          organizationName: get(data, 'organizationName').trim(),
          licenseNumber: get(data, 'licenseNumber').trim(),
          organizationType: get(data, 'organizationType') as OrganizationType,
          email: get(data, 'email').trim(),
          password: get(data, 'password'),
        })
        // No session: the organization is Pending until an administrator approves it.
        setSubmitted(true)
        requestAnimationFrame(() => doneRef.current?.focus())
      } catch (error) {
        if (error instanceof ApiError && error.code === 'auth.account_exists')
          return { email: 'An account with this email already exists. Try logging in instead.' }
        if (error instanceof ApiError && error.code === 'organization.license_exists')
          return { licenseNumber: 'An organization with this license number is already registered.' }
        return apiErrors(error)
      }
    },
  )

  return (
    <AuthLayout
      variant="register"
      art={
        <>
          <BotanicalCorner position="bottom-right" className="auth-panel__contours auth-panel__contours--low" />
          <BotanicalBranch className="auth-panel__branch auth-panel__branch--register" />
        </>
      }
      panel={
        <>
          <span className="auth-panel__mark">
            <FoodLoopMark />
          </span>
          <div className="auth-panel__statement">
            <p className="display auth-panel__headline">
              Join the <em>loop</em>.
            </p>
            <ol className="auth-steps" role="list" aria-label="What happens next">
              {NEXT_STEPS.map((s, i) => (
                <li key={s}>
                  <span className="auth-steps__index">{String(i + 1).padStart(2, '0')}</span>
                  {s}
                </li>
              ))}
            </ol>
          </div>
          <p className="t-label auth-panel__tagline">Donors · Beneficiaries</p>
        </>
      }
    >
      {submitted ? (
        <RevealGroup className="auth-form-wrap">
          <RevealItem>
            <SectionEyebrow>Request received</SectionEyebrow>
          </RevealItem>
          <RevealItem>
            <h1 ref={doneRef} tabIndex={-1} className="auth-title">
              Thanks — you’re <em>almost in.</em>
            </h1>
            <p className="auth-lead">
              Your organization is waiting for approval. The FoodLoop team reviews every organization before it can
              sign in. Once it’s approved, log in with the email and password you just chose.
            </p>
          </RevealItem>
          <RevealItem>
            <p className="auth-status">
              <CircleCheck aria-hidden="true" />
              You won’t be able to log in until the review is complete.
            </p>
          </RevealItem>
          <RevealItem className="auth-form__submit">
            <Button to={PATHS.login} size="lg" iconEnd={<ArrowRight />}>
              Go to log in
            </Button>
          </RevealItem>
        </RevealGroup>
      ) : (
        <RevealGroup className="auth-form-wrap auth-form-wrap--wide">
          <RevealItem>
            <SectionEyebrow>Create an account</SectionEyebrow>
          </RevealItem>
          <RevealItem>
            <h1 className="auth-title">
              Bring your organization <em>into the loop.</em>
            </h1>
            <p className="auth-lead">
              Two short parts. It takes a few minutes, and our team reviews every organization before it can sign in.
            </p>
          </RevealItem>

          <form className="auth-form" noValidate onSubmit={onSubmit}>
            <RevealItem>
              <fieldset className="form-group">
                <legend className="form-group__legend">
                  <span className="form-group__index">01</span>
                  Organization
                </legend>
                <div className="form-group__grid">
                  <Field
                    id="organizationName"
                    label="Organization name"
                    autoComplete="organization"
                    maxLength={200}
                    required
                    error={errors.organizationName}
                  />
                  <Field id="licenseNumber" label="License number" maxLength={100} required error={errors.licenseNumber} />
                  <fieldset
                    className={`choice span-2${errors.organizationType ? ' has-error' : ''}`}
                    aria-describedby={errors.organizationType ? 'organizationType-error' : undefined}
                  >
                    <legend className="field__label">How do you take part?</legend>
                    <div className="choice__options">
                      {PARTICIPATION.map((p) => (
                        <label key={p.value} className="choice__option">
                          <input type="radio" name="organizationType" value={p.value} className="choice__input" required />
                          <span className="choice__card">
                            <span className="choice__label">{p.label}</span>
                            <span className="choice__text">{p.text}</span>
                          </span>
                        </label>
                      ))}
                    </div>
                    {errors.organizationType && (
                      <p id="organizationType-error" className="field__error">
                        {errors.organizationType}
                      </p>
                    )}
                  </fieldset>
                </div>
              </fieldset>
            </RevealItem>

            <RevealItem>
              <fieldset className="form-group">
                <legend className="form-group__legend">
                  <span className="form-group__index">02</span>
                  Account
                </legend>
                <div className="form-group__grid">
                  <Field
                    id="email"
                    label="Work email"
                    type="email"
                    autoComplete="email"
                    maxLength={256}
                    required
                    error={errors.email}
                    className="span-2"
                  />
                  <Field
                    id="password"
                    label="Password"
                    type="password"
                    autoComplete="new-password"
                    hint="At least 10 characters, with upper- and lowercase letters, a number and a symbol."
                    required
                    error={errors.password}
                    className="span-2"
                  />
                  <div className={`span-2 check-field${errors.terms ? ' has-error' : ''}`}>
                    <label className="check">
                      <input
                        type="checkbox"
                        name="terms"
                        className="check__input"
                        required
                        aria-invalid={errors.terms ? true : undefined}
                        aria-describedby={errors.terms ? 'terms-error' : undefined}
                      />
                      <span className="check__box" aria-hidden="true" />
                      <span>
                        I agree to the FoodLoop{' '}
                        <Link to="/terms" className="link-underline">
                          terms
                        </Link>{' '}
                        and{' '}
                        <Link to="/privacy" className="link-underline">
                          privacy policy
                        </Link>
                        .
                      </span>
                    </label>
                    {errors.terms && (
                      <p id="terms-error" className="field__error">
                        {errors.terms}
                      </p>
                    )}
                  </div>
                </div>
              </fieldset>
            </RevealItem>

            <RevealItem className="auth-form__submit auth-form__submit--split">
              <MagneticButton>
                <Button type="submit" size="lg" loading={submitting} iconEnd={<ArrowRight />}>
                  Create account
                </Button>
              </MagneticButton>
              <p className="auth-switch">
                Already have an account?{' '}
                <Link to={PATHS.login} className="link-underline">
                  Log in
                </Link>
              </p>
            </RevealItem>
            <div role="alert" className="auth-status-slot">
              {errors.form && (
                <p className="auth-status auth-status--error">
                  <AlertCircle aria-hidden="true" />
                  {errors.form}
                </p>
              )}
            </div>
          </form>
        </RevealGroup>
      )}
    </AuthLayout>
  )
}
