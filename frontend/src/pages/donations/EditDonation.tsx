import { ArrowLeft, Lock } from 'lucide-react'
import { useParams } from 'react-router-dom'
import { PATHS, donationPath } from '../../app/routes'
import { Button } from '../../components/ui/Button'
import { PageLoading, PageMessage } from '../../components/workspace/PageState'
import { codeOf } from '../../lib/api/client'
import { getDonationForEdit } from '../../lib/api/donations'
import { useLoad } from '../../lib/api/useLoad'
import { DonationForm } from './DonationForm'

/** Draft-only editing. The backend decides: a non-draft answers 409 donation.not_editable, a foreign or missing one 404. */
export function EditDonation() {
  const { id = '' } = useParams()
  const load = useLoad(id, (signal) => getDonationForEdit(id, signal))
  const code = codeOf(load.error)

  if (code === 'donation.not_editable')
    return (
      <PageMessage
        title="This donation can’t be edited."
        action={
          <Button to={donationPath(id)} iconStart={<ArrowLeft />}>
            View donation
          </Button>
        }
      >
        <Lock aria-hidden="true" width={18} height={18} /> Only drafts can be changed. Once a donation is published, its details stay fixed so
        claims and deliveries remain trustworthy.
      </PageMessage>
    )
  if (code === 'donation.not_found')
    return (
      <PageMessage
        title="There’s no donation to edit here."
        action={
          <Button to={PATHS.donations} iconStart={<ArrowLeft />}>
            Back to my donations
          </Button>
        }
      >
        It may have been removed, or the link is incomplete.
      </PageMessage>
    )
  if (load.error !== undefined) return <PageMessage title="We couldn’t load this draft." onRetry={load.reload} />
  if (!load.fresh || !load.data) return <PageLoading label="Loading draft…" />

  // key: a reload with a newer version remounts the form with the latest saved values.
  return <DonationForm key={load.data.version} mode="edit" donation={load.data} onReload={load.reload} />
}
