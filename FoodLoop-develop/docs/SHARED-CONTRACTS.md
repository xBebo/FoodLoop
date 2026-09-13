# Shared contracts - foundation proposal

This schema is a starting contract for review, not a record of decisions already approved in the meeting. Confirm the lifecycle/cancellation rules with Alaa, Safa and Haneen before implementing workflows. This foundation does not execute these transitions yet.

## Common conventions

- IDs are Guid, including Identity users. SQL relationships use Guid foreign keys.
- Timestamps are DateTimeOffset and named `...Utc`; use TimeProvider.GetUtcNow in use cases. Render in the user's local timezone.
- Quantity uses decimal(12,3), with a separate Meals / Kilograms / Packages unit. Reports never sum different units into one number.
- Organization is Donor or Beneficiary, and begins Pending. Admin and Courier users may have no organization.
- ApplicationUser lives in Infrastructure; Domain references user IDs only. Assign roles through Identity's UserManager, never by trusting registration input for Admin/Courier.
- No partial claims: one beneficiary reserves an entire donation.
- Entity setters do not enforce the business lifecycle. Feature services must validate it server-side; controllers must use request models rather than binding entities.
- All foreign-key deletes are restricted. Explicitly design lifecycle deletion if needed; don't cascade-delete operational evidence.

## Proposed lifecycle and ownership

DonationStatus: Draft=0, Available=1, Claimed=2, PickupPending=3, PickedUp=4, InTransit=5, Delivered=6, Closed=7, Expired=8, Cancelled=9, Failed=10.

ClaimStatus: Booked=0, PickupPending=1, PickedUp=2, InTransit=3, Delivered=4, Closed=5, Cancelled=6, Failed=7.

| Operation | Donation | Claim | Owner |
|---|---|---|---|
| Publish | Draft -> Available | None | Alaa |
| Claim | Available -> Claimed | New Booked | Safa |
| Assign courier | Claimed -> PickupPending | Booked -> PickupPending | Haneen |
| Verify pickup | PickupPending -> PickedUp | PickupPending -> PickedUp | Haneen |
| Start transport | PickedUp -> InTransit | PickedUp -> InTransit | Haneen |
| Verify delivery | InTransit -> Delivered | InTransit -> Delivered | Haneen |
| Close documented delivery | Delivered -> Closed | Delivered -> Closed | Haneen |
| Cancel claim before pickup | Claimed/PickupPending -> Available if unexpired, otherwise Expired | -> Cancelled | Safa with Alaa/Haneen |
| Donor withdraws unclaimed donation | Draft/Available -> Cancelled | None | Alaa |
| Expiry before pickup | Available/Claimed/PickupPending -> Expired | Active claim -> Failed, if present | Alaa coordinates with Safa/Haneen |
| Expiry/failure after pickup | PickedUp/InTransit -> Failed | -> Failed with reason | Alaa/Haneen |

Changing both donation and claim states is the responsibility of the operation's owner in one unit of work. Donation is the listing/lifecycle summary; Claim is the reservation and fulfillment record. They are not independently editable through generic endpoints.

Open decisions to confirm: cancellation authority, admin-only assignment, QR lifetime, closing automatically at verified delivery or separately. No ordinary cancellation after pickup in this proposal. Expired/failed transport never counts as delivered impact. Do not expire an already completed delivery just because its original deadline later passes.

## Booking contract - Safa + Alaa

- Recheck authenticated Beneficiary, current Active organization, donation availability and ExpiresAtUtc at operation time.
- Change the tracked donation status, add the claim and stage audit before one SaveChangesAsync. Use an explicit transaction when multiple saves are unavoidable.
- RowVersion detects stale writes; SQL unique index UX_Claim_ActiveDonation independently prevents a second active claim.
- **Active means Booked, PickupPending, PickedUp, InTransit or Delivered (0 through 4).** Closed/Cancelled/Failed are historical. This is encoded in both ClaimRepository and the filtered index; enum changes require reviewing both and a new migration.
- A completed donation must never be republished simply because its old claim is Closed. The available-status validation is still required.
- On PersistenceConflictException, stop that attempt and show a controlled conflict. Do not retry stale tracked changes in the same context. HTTP 409 is appropriate for JSON endpoints; MVC can render a clear validation message.
- Cancellation and pickup both update the claim/donation concurrency tokens so a race cannot silently produce contradictory states. Invalidate outstanding task tokens and assignment on cancellation.

## QR/handover contract - Haneen

- Donor displays the pickup code; beneficiary displays delivery code; assigned courier submits it.
- Use cryptographically random tokens. Store a SHA-256 hex hash only, never the raw token. Bind ClaimId, CourierUserId, Purpose, ExpiresAtUtc.
- Check assigned courier, task state, operation-time expiry, token expiry and UsedAtUtc on the server.
- Consume token + advance claim/donation + insert handover + stage audit in one atomic save/transaction.
- QrVerificationToken, DonationClaim and FoodDonation have RowVersion. One handover per claim/type is a SQL unique constraint.
- These database protections do NOT implement verification or prevent sequential replay by themselves. Haneen must check UsedAtUtc and the workflow state every time.
- No delivery before pickup. The database does not enforce cross-row handover ordering; test it in the use case.
- Define token regeneration so old outstanding tokens are revoked; do not allow multiple independently usable tokens for one step.

## Current user - Jana

ICurrentUserService exposes authenticated user ID, roles and a database-backed organization ID. It does not grant authorization or guarantee the organization is Active. Each protected use case must check current organization status and ownership. Do not accept organization/courier IDs from the client as proof of identity.

Identity services, cookie middleware and antiforgery protection for unsafe MVC methods are registered. Account screens, access-denied responses and authorization policies remain Jana's feature work. Any JSON endpoints added later must preserve the corresponding cookie/antiforgery protection.

## Audit and transactions - Baraa + all feature owners

IAuditService.Record stages a record in the same scoped DbContext; it does not save, send messages or commit. The actor comes from the authenticated context (null for system jobs). Use bounded event names, e.g. DonationPublished, ClaimCreated, PickupVerified. Do not include secrets or raw tokens in Details.

IUnitOfWork.SaveChangesAsync persists repositories and audit together. Its optional transaction wrapper requires an explicit SaveChangesAsync before CommitAsync. Disposing an uncommitted transaction rolls it back. Notify clients only after the outer transaction commits.

The DbContext blocks editing/deleting tracked audit entries. This is an application guard, not a database security boundary: raw SQL/bulk operations/database administrators can bypass it. Do not expose audit mutation operations.

Background jobs must create their own dependency-injection scope before resolving scoped repositories, DbContext or audit service.

## What is deliberately not here

No login UI, approval use case, donation CRUD controllers, booking service, QR generation/verification, expiry scheduler, report/dashboard queries, Notifications or SignalR. No generic service/controller scaffolding for every feature. Owners build these on the reviewed contracts.

## Reference documentation

- https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- https://learn.microsoft.com/en-us/ef/core/saving/concurrency
- https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0
