# FoodLoop shared contracts — integration revision

This document describes the implemented integration branch. Failure recovery and scheduled expiry remain future work; do not infer that an enum value has a working endpoint.

## Common conventions
- Keep the four projects: Domain, Application, Infrastructure, Web.
- IDs are Guid, including Identity users. Domain stores user IDs; Identity remains in Infrastructure.
- Register Application services explicitly through AddApplication(). Register EF repositories and Identity adapters through AddInfrastructure().
- Use the existing SQL Server schema, scoped DbContext, repositories and IUnitOfWork. No migration is required by this integration.
- Persist related state changes and audit records together. Catch PersistenceConflictException and end the attempt; never retry stale tracked changes in the same request.
- Use UTC DateTimeOffset values and TimeProvider in use cases. Quantity units must not be added together across different units.

## Organizations and access
- Public registration allows Donor or Beneficiary only, assigns that Identity role and creates a Pending organization.
- User/organization creation and role assignment share a transaction. Seed roles before registration.
- Admin approval/rejection accepts Guid and only Pending organizations. Both use OrganizationApprovalService and actor-aware audit.
- Admin organization management is server-paginated at 20 rows with stable Name/Id ordering and an optional OrganizationStatus filter applied before Count/Skip/Take.
- New management transitions are allowlisted: Active -> Suspended and Suspended -> Active only. Pending/Rejected remain on the existing approval workflow.
- Suspension/reactivation records exactly one OrganizationSuspended/OrganizationReactivated audit with the current Admin actor in the same SaveChanges operation. Organization RowVersion provides controlled conflict handling. Identity roles/accounts are not changed or deleted.
- Forms post to OrganizationsController. AuthController has no approval/rejection or suspension actions.
- Pending and Rejected organizations cannot log in. Suspended Beneficiaries may log in for read-only history; Suspended Donors cannot log in.
- Every operation rechecks its permissions and organization state. Login permission does not grant mutation permission. Suspension does not automatically cancel existing claims.
- My Claims/history permits Active or Suspended Beneficiaries, only for their own organization. Pending/Rejected are forbidden. Suspended history is read-only and shows no Cancel action.
- Cookies use /Auth/Login and /Admin/AccessDenied. Unsafe MVC requests require antiforgery.

## Donations and claims
- Only an Active Donor may create or publish its own donation.
- Active Donors may edit only their own Draft donations. Edit reuses create-field validation, requires the submitted RowVersion to match, and records DonationUpdated in the same save. Published/claimed/delivery-stage donations and foreign donations are not editable.
- Publish permits Draft -> Available, positive quantity and a future donor-entered expiry date.
- Marketplace includes Available, unexpired donations from Active Donor organizations. Optional title search and category filters are applied before fixed 20-row server pagination with stable expiry/Id ordering; filters persist across navigation.
- Claim requires an Active Beneficiary, an Active Donor, Available/unexpired donation and no existing active claim.
- One claim reserves the entire donation. RowVersion and the existing filtered unique index protect concurrent reservations.
- Successful claim creates one ClaimCreated audit on DonationClaim; Details includes DonationId and Available -> Claimed. No duplicate FoodDonation event.
- Large paging inputs return empty results when the offset exceeds the supported integer range.

## Claim cancellation before assignment
- Every cancel POST is authorized on the server: authenticated Beneficiary role, Beneficiary organization with status Active. Suspended, Pending and Rejected Beneficiaries cannot cancel; hiding the button is not the control.
- Claim lookup is scoped to the current Beneficiary organization. A foreign claim ID and a nonexistent ID are indistinguishable: both return ClaimNotFound / HTTP 404 and disclose no other organization's data.
- Allowlist: claim status exactly Booked, AssignedCourierUserId null, donation status exactly Claimed. PickupPending, PickedUp, InTransit, Delivered, Closed, Cancelled and Failed claims are not cancellable.
- Claim Booked -> Cancelled; the claim stays in history and no record is deleted.
- Donation Claimed -> Available only when the donor organization exists, is type Donor, is Active and ExpiresAtUtc is later than the current UTC time. Otherwise Claimed -> Draft (expired donation, or Pending/Rejected/Suspended donor). Cancellation never writes Expired.
- Exactly one ClaimCancelled audit on DonationClaim; Details includes DonationId and Claimed->Available or Claimed->Draft. Claim status, donation status and audit are staged and persisted in one SaveChangesAsync, so no explicit transaction and no orphan audit.
- A repeat request after a successful cancellation is rejected as not cancellable. A truly concurrent duplicate may lose on RowVersion and return a controlled conflict. Neither path creates a second ClaimCancelled audit.
- Cancel and Admin assignment cannot both succeed: both rely on the existing DonationClaim and FoodDonation RowVersions. The loser gets a controlled PersistenceConflictException result; do not retry the stale tracked DbContext.

## Claim and courier lifecycle implemented in this integration
| Operation | Donation | Claim |
|---|---|---|
| Claim | Available -> Claimed | New Booked |
| Cancel before assignment (Active owning Beneficiary, no AssignedCourierUserId, donation Claimed) | Claimed -> Available \| Draft | Booked -> Cancelled |
| Admin assignment | Claimed -> PickupPending | Booked -> PickupPending |
| Verified pickup | PickupPending -> InTransit | PickupPending -> InTransit |
| Verified delivery and closure | InTransit -> Closed | InTransit -> Closed |

For the basic demo, verified pickup starts transport and verified delivery closes the task in the same save. PickedUp/Delivered enum values remain unchanged for future workflow expansion. Closed delivery counts require the Delivery HandoverRecord as evidence.

- Only Admin assigns a user who currently has the Courier role. Assignment/reassignment is allowed only before pickup and revokes outstanding codes.
- Only the assigned Courier may view that task's verification page or perform handover. My Tasks filters by current user ID.
- Both organizations must be Active and the donation unexpired at assignment, code issue and verification. Suspended Beneficiary delivery is blocked.
- Donor organization issues the Pickup code; Beneficiary organization issues the Delivery code. No cross-organization issuance.
- Codes use 32 cryptographically random bytes, displayed as 64 hexadecimal characters. Store only SHA-256 hashes bound to claim, courier and purpose.
- Integration default: codes live at most 15 minutes, capped by donation expiry. Regeneration invalidates outstanding codes and touches the claim's RowVersion to serialize races.
- The raw code is displayed only on the no-store issuance response. That page renders an in-app QR for the same token, provides a Copy action, and shows the actual server expiry. No raw code is stored in localStorage, the database or Audit; text copy/paste remains a complete verification path.
- Verify purpose, expiry, unused status, assignment, ownership and exact workflow state. Delivery also requires persisted pickup evidence.
- Consume the code, update BOTH entities, add HandoverRecord with timestamp and stage actor-aware audit in one atomic save.
- Repeated/concurrent handover must yield one successful operation; no duplicate handover or orphan audit.

## Admin
- Admin dashboard, organization management, assignment and Audit List enforce Admin on the server.
- Counts: Pending organizations; Available unexpired donations from Active Donors; Closed claims whose donation is Closed and which have Delivery evidence.
- Audit is read-only and paginated (page size 20). Never store raw codes, passwords or secrets in Details.
- Audit supports optional server-side filters applied before Count/Skip/Take: exact Action match, substring Actor match on the displayed actor name (including System and Unknown user), and a UTC `[FromUtc, ToUtc)` range (from inclusive, to exclusive). FromUtc must be earlier than ToUtc; invalid ranges render a validation message rather than an HTTP 500.
- Audit ordering is stable: CreatedAtUtc descending, then Id descending. Previous/Next links preserve `actionName`, `actor`, `from`, and `to`.
- The Admin dashboard links directly to organization management, pending organization review, courier assignment, and the Audit List.

## Outside this integration
No LLM, automatic expiry job, SignalR, advanced reports, cancellation after courier assignment or camera-based QR scanner. Existing historical enum values and database indexes are preserved.

## Stage two status
[Tasks 2 of 3](TASKS-02.md) contains the acceptance rules used for Stage 2. The Stage 2 slices for Jana, Alaa, Safa, Haneen and Baraa are integrated as documented above.

## Stage three release-candidate scope
[Tasks 3 of 3](TASKS-03.md) is the current team plan. Stage 3 is final validation, UX consistency, responsive/mobile hardening, regression testing, documentation and demo readiness.

- Do not add a new lifecycle, enum, schema migration or large feature unless Baraa and the affected feature owner explicitly approve a necessary change.
- LLM, SignalR, automatic expiry jobs, advanced reports, cancellation after courier assignment and camera-based QR scanning remain deferred.
- Every changed screen must be checked on Desktop and approximately 390×844 Mobile.
- Server-side authorization/ownership/state checks remain authoritative even when UI actions are hidden.
- New/changed time displays must state UTC clearly.
- PRs must include a success scenario, a rejected scenario, manual mobile result, Release build/test result and screenshots for changed UI.
- Shared layout/global CSS/README changes are coordinated by Baraa after feature-specific PRs to minimize merge conflicts.
- Final documentation deliverables include the current ERD and physical database diagram, and must describe the implemented schema/behavior rather than the original proposal.
