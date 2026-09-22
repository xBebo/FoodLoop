import { AlertCircle, ArrowRight } from 'lucide-react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { PATHS } from '../../app/routes'
import { BotanicalBranch, BotanicalCorner } from '../../components/brand/Botanical'
import { FoodLoopMark } from '../../components/brand/FoodLoopMark'
import { MagneticButton } from '../../components/motion/MagneticButton'
import { RevealGroup, RevealItem } from '../../components/motion/Reveal'
import { Button } from '../../components/ui/Button'
import { SectionEyebrow } from '../../components/ui/SectionEyebrow'
import { AuthLayout } from './AuthLayout'
import { Field } from '../../components/ui/Field'
import { returnTo, useSession, type LoginState } from '../../lib/session/context'
import { apiErrors, rules, useAuthForm } from './useAuthForm'

export function Login() {
  const { login } = useSession()
  const navigate = useNavigate()
  const location = useLocation()
  const expired = (location.state as LoginState | null)?.expired === true
  const { errors, submitting, onSubmit } = useAuthForm(
    (data) => ({
      email: rules.email(String(data.get('email') ?? '')),
      password: String(data.get('password') ?? '') ? '' : 'Enter your password.',
    }),
    async (data) => {
      try {
        // The role, and so the landing page, comes from the server's session, never from the form.
        const session = await login(String(data.get('email')).trim(), String(data.get('password')))
        navigate(returnTo(location.state, session), { replace: true })
      } catch (error) {
        // One message for unknown email and wrong password: the API never says which.
        return apiErrors(error, {
          401: 'Invalid email or password. Check them and try again.',
          403: 'This account can’t sign in right now. Contact the FoodLoop team if you think this is a mistake.',
        })
      }
    },
  )

  return (
    <AuthLayout
      variant="login"
      art={
        <>
          <BotanicalCorner position="top-right" className="auth-panel__contours" />
          <BotanicalBranch className="auth-panel__branch" />
        </>
      }
      panel={
        <>
          <span className="auth-panel__mark">
            <FoodLoopMark />
          </span>
          <div className="auth-panel__statement">
            <p className="display auth-panel__headline">
              Pick up where the <em>loop</em> left&nbsp;off.
            </p>
            <p className="auth-panel__copy">
              Every listing, claim and handoff on FoodLoop belongs to a real organization. Signing in keeps that chain
              of trust intact — for the food, and for the people waiting for it.
            </p>
          </div>
          <p className="t-label auth-panel__tagline">Rescue · Redistribute · Repeat</p>
        </>
      }
    >
      <RevealGroup className="auth-form-wrap">
        <RevealItem>
          <SectionEyebrow>Log in</SectionEyebrow>
        </RevealItem>
        <RevealItem>
          <h1 className="auth-title">
            Welcome <em>back.</em>
          </h1>
          <p className="auth-lead">Sign in to share surplus, claim food for your organization or pick up your next run.</p>
        </RevealItem>

        <form className="auth-form" noValidate onSubmit={onSubmit}>
          <RevealItem>
            <Field id="email" label="Email" type="email" autoComplete="email" required error={errors.email} />
          </RevealItem>
          <RevealItem>
            <Field
              id="password"
              label="Password"
              type="password"
              autoComplete="current-password"
              required
              error={errors.password}
            />
          </RevealItem>
          <RevealItem className="auth-form__submit">
            <MagneticButton>
              <Button type="submit" size="lg" loading={submitting} iconEnd={<ArrowRight />}>
                Log in
              </Button>
            </MagneticButton>
          </RevealItem>
          <div role="alert" className="auth-status-slot">
            {expired && !errors.form && (
              <p className="auth-status">
                <AlertCircle aria-hidden="true" />
                Your session ended. Sign in again to continue where you left off.
              </p>
            )}
            {errors.form && (
              <p className="auth-status auth-status--error">
                <AlertCircle aria-hidden="true" />
                {errors.form}
              </p>
            )}
          </div>
        </form>

        <RevealItem>
          <p className="auth-switch">
            New to FoodLoop?{' '}
            <Link to={PATHS.register} className="link-underline">
              Create an account
            </Link>
          </p>
        </RevealItem>
      </RevealGroup>
    </AuthLayout>
  )
}
