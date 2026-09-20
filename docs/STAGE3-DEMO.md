# FoodLoop Stage 3 demo script

This script demonstrates the implemented Stage 3 functional flow on a fresh development database. It assumes SQL Server/LocalDB is available and uses the existing committed migration. Do not put real passwords, raw QR tokens, or connection-string secrets in screenshots or commits.

## 1. Fresh database

From the repository root:

```powershell
dotnet restore FoodLoop.slnx
dotnet tool restore
dotnet ef database update --project src/FoodLoop.Infrastructure --startup-project src/FoodLoop.Web -- --environment Development
dotnet run --project src/FoodLoop.Web -- --seed
```

Reference roles/categories are seeded. Demo accounts are created only when `Seed:DemoPassword` is configured privately.

For a faster expiry demonstration, the scheduler interval can be overridden for the current shell without changing source:

```powershell
$env:DonationExpiryScheduler__IntervalSeconds="5"
dotnet run --project src/FoodLoop.Web
```

The committed default remains 60 seconds.

## 2. Registration and approval

1. Register one Donor organization and one Beneficiary organization with unique test emails.
2. Confirm both are Pending and cannot use active-organization mutations.
3. Sign in as Admin and approve both organizations.
4. Confirm the Active accounts can sign in.
5. Open My Organization. Update Name or Address for an Active organization and refresh to confirm persistence.
6. Demonstrate that a Suspended Beneficiary profile is read-only and a direct mutation is rejected.

## 3. Donation and claim flow

1. As Donor, create a donation with positive quantity and a future UTC expiry.
2. Edit the Draft donation, then publish it.
3. Confirm it appears in Marketplace.
4. As Beneficiary, claim it.
5. Cancel that Booked, unassigned claim once.
6. Refresh history and confirm the cancelled claim remains visible.
7. Claim the donation again.

## 4. Courier flow

1. As Admin, assign the second claim to a valid Courier.
2. As the Donor organization, issue the Pickup code.
3. As the assigned Courier, verify the Pickup code.
4. Refresh Courier Task Details and confirm Pickup evidence is persisted and the claim is InTransit.
5. As the Beneficiary organization, issue the Delivery code.
6. As the assigned Courier, verify Delivery.
7. Refresh Task Details and confirm Delivery evidence is persisted and the task is Closed.
8. Reusing the consumed code or trying Delivery before Pickup must be rejected without additional handover or audit records.

## 5. Admin impact and audit

Open `/Admin` as Admin and confirm the read-only summary shows:

- Current Available donations.
- Closed operations.
- Cancelled claims.
- Expired donations.
- Pending organizations.

Open `/Admin/Audit` and demonstrate action, actor, and UTC date-range filters. Audit output must not contain passwords or raw QR/handover codes.

## 6. Automatic expiry

1. Create and publish another donation with a short future expiry.
2. Leave it unclaimed and in Available state.
3. Wait for the configured scheduler interval plus a small margin.
4. Refresh My Donations/Admin. The donation should be persisted as Expired.
5. Filter Audit for `DonationExpired`.
6. Wait for another scheduler run and confirm no duplicate `DonationExpired` audit is added for the same donation.

Request-time expiry checks still remain authoritative between scheduler runs, so an expired-by-time listing cannot be newly claimed while waiting for the background synchronization.

## 7. Release verification

Run:

```powershell
dotnet build FoodLoop.slnx -c Release
dotnet test FoodLoop.slnx -c Release --verbosity minimal
```

The SQL-backed tests create disposable `FoodLoop_FoundationTests_<random>` databases and delete only those generated databases.
