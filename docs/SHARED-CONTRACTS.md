# FoodLoop shared contracts — integration revision

This document describes the implemented integration branch. Cancellation, failure recovery and scheduled expiry remain future work; do not infer that an enum value has a working endpoint.

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
- Forms post to OrganizationsController. AuthController has no approval/rejection actions.
- Pending and Rejected organizations cannot log in. Suspended Beneficiaries may log in for read-only history; Suspended Donors cannot log in.
- Every operation rechecks its permissions and organization state. Login permission does not grant mutation permission.
- My Claims/history permits Active or Suspended Beneficiaries, only for their own organization. Pending/Rejected are forbidden. Suspension does not automatically cancel old claims.
- Cookies use /Auth/Login and /Admin/AccessDenied. Unsafe MVC requests require antiforgery.

## Donations and claims
- Only an Active Donor may create or publish its own donation.
- Publish permits Draft -> Available, positive quantity and a future donor-entered expiry date.
- Marketplace includes Available, unexpired donations from Active Donor organizations.
- Claim requires an Active Beneficiary, an Active Donor, Available/unexpired donation and no existing active claim.
- One claim reserves the entire donation. RowVersion and the existing filtered unique index protect concurrent reservations.
- Successful claim creates one ClaimCreated audit on DonationClaim; Details includes DonationId and Available -> Claimed. No duplicate FoodDonation event.
- Large paging inputs return empty results when the offset exceeds the supported integer range.

## Courier lifecycle implemented in this integration
| Operation | Donation | Claim |
|---|---|---|
| Claim | Available -> Claimed | New Booked |
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
- The code is displayed once in a no-store response and manually pasted by the courier. A graphical QR/scanner UI is not included.
- Verify purpose, expiry, unused status, assignment, ownership and exact workflow state. Delivery also requires persisted pickup evidence.
- Consume the code, update BOTH entities, add HandoverRecord with timestamp and stage actor-aware audit in one atomic save.
- Repeated/concurrent handover must yield one successful operation; no duplicate handover or orphan audit.

## Admin
- Admin dashboard and Audit List enforce Admin on the server.
- Counts: Pending organizations; Available unexpired donations from Active Donors; Closed claims whose donation is Closed and which have Delivery evidence.
- Audit is read-only and paginated (page size 20). Never store raw codes, passwords or secrets in Details.
- Audit supports optional filters, all applied server-side before Count/Skip/Take so TotalCount and pagination reflect the filtered set: exact Action match, substring match on the displayed Actor name (including "System"/"Unknown user"), and a UTC `[FromUtc, ToUtc)` range (from inclusive, to exclusive). FromUtc must be earlier than ToUtc or the request is rejected with a validation message; no 500. Ordering stays CreatedAtUtc desc, then Id desc. Previous/Next links carry the active filters via query string (`actionName`, `actor`, `from`, `to`); the parameter is named `actionName`, not `action`, to avoid colliding with the MVC route value.

## Outside this integration
No LLM, automatic expiry job, SignalR, advanced reports, ordinary cancellation workflow or graphical QR scanner. Existing historical enum values and database indexes are preserved.

## Planned stage two
[Tasks 2 of 3](TASKS-02.md) defines the next assignments and their acceptance rules. Those additions are not implemented by this integration commit; the lifecycle above remains the currently working behavior.
