# Stage 3 owner release validation

Validated on 2026-09-21 from `develop` at `a01f726` (merged PR #28), using `feature/baraa-stage3-release`. The scheduler, Admin impact metrics and ERD/release documentation were already implemented in that merge. This follow-up adds executable release evidence without duplicating those features.

## Runtime contracts verified

- `DonationExpiryBackgroundService` waits the configured interval, runs serially and resolves `IDonationExpiryService` in a fresh asynchronously disposed scope. Default interval is 60 seconds; configuration is bounded to 1–86,400 seconds. A controlled conflict or unexpected exception ends the current run, and the next run gets a new scope. Unexpected failures log only the exception type. Shutdown cancellation reaches the active service and interrupts the delay without an error log.
- Expiry rules remain in `DonationExpiryService`; request-time expiry checks remain intact. This is a per-process scheduler, not a distributed singleton. Existing RowVersion concurrency handling protects persisted transitions when processes race.
- Admin Current Available counts only Available, unexpired donations from active Donor organizations. Closed counts completed claims with a Closed donation and Delivery evidence; Cancelled counts claims; Expired counts persisted donations. No quantities are aggregated.
- No schema, migration, enum or global UI changes. The existing [ERD](ERD.md) remains applicable.

## Added regression evidence

`DonationExpirySchedulerTests` now starts the hosted loop and verifies non-overlap while work exceeds the interval, cancellation during an active run, asynchronous scope disposal, recovery after a controlled conflict or unexpected exception, fresh scopes after failures, secret-free logging, disabled scheduling and shutdown during the interval.

`StageThreeReleaseJourneyTests` runs one fresh SQL database through real Identity cookie login, MVC routing, rendered forms and antiforgery checks:

1. Donor creates and publishes; Beneficiary sees the marketplace listing.
2. Foreign Donor cannot read the donation; foreign Beneficiary cannot read/cancel the claim.
3. Beneficiary claims, cancels and claims again; Admin assigns a real Courier role member.
4. Foreign Courier cannot read the task or verify its token; foreign Donor cannot issue its code; non-admin dashboard access is rejected.
5. Pickup code is returned only by the authorized no-store response. Delivery-before-pickup is rejected without evidence/audit changes. Pickup succeeds once; replay is rejected.
6. Delivery closes the task; both persisted evidence records render. Delivery replay creates no additional audit/evidence. Stored token hashes and audit output do not contain either raw token.
7. An overdue listing cannot be claimed. The actual hosted scheduler persists Expired with one system audit, including after a later interval.
8. Impact totals are exactly Available=1, Closed=1, Cancelled=1, Expired=1. Suspending the donor removes the remaining available listing from the count.
9. A suspended Beneficiary can read claim history/details and the read-only profile, while claim creation and direct profile updates are rejected.

The journey seeds identities/active organizations rather than registering them through the UI, sets expiry/suspension directly for deterministic setup, and uses HTTP rather than a graphical browser. Existing registration, approval, concurrency, role and ownership regressions run in the full suite.

## Local verification

Windows, .NET SDK 10.0.401, SQL Server LocalDB. Tests migrate uniquely named disposable databases using the existing committed migration.

```powershell
dotnet build FoodLoop.slnx -c Release
dotnet test FoodLoop.slnx -c Release
```

Both commands passed: Release build had **0 warnings and 0 errors**; the full suite had **322 passed, 0 failed, 0 skipped**. The automated journey can also be run independently using the command in [the demo script](STAGE3-DEMO.md#8-repeatable-automated-http-demo). Remote CI is reported separately in the follow-up PR.
