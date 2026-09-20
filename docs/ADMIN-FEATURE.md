# Admin dashboard and audit list

## How the code fits together

1. `AdminController` enforces the Admin role on the dashboard and Audit list.
2. `AdminService` repeats the Admin check, uses a fixed Audit page size of 20, and supplies `TimeProvider`'s current UTC time.
3. `IAdminReadRepository` is defined in Application; `AdminReadRepository` in Infrastructure executes read-only SQL projections/counts.
4. Razor views render small DTOs through the existing Bootstrap layout. Razor encodes database text; Audit `Details` are not exposed by the list.

The Audit list orders by `CreatedAtUtc` descending then `Id` descending. Filters are applied before `Count/Skip/Take`. System events have no actor ID; user events show a safe display name. Audit is operational history, not a snapshot export.

## Stage 3 count definitions

- Pending organizations: `Organization.Status == Pending`.
- Current Available donations: `FoodDonation.Status == Available`, `ExpiresAtUtc > current UTC`, and donor organization Active with type Donor.
- Closed operations: one count per Closed claim whose donation is also Closed and which has Delivery `HandoverRecord` evidence.
- Cancelled claims: `DonationClaim.Status == Cancelled`.
- Expired donations: `FoodDonation.Status == Expired` persisted by the expiry use case/scheduler.

No food quantities or incompatible units are summed. Counts are read sequentially from one scoped `DbContext` and refresh when the page reloads.

## Authentication and authorization

- Signed-out requests to Admin data pages challenge to `/Auth/Login` with a local ReturnUrl.
- Donor, Beneficiary and Courier roles are denied Admin pages.
- `/Admin/AccessDenied` returns HTTP 403 and exposes no Admin data.
- Login ReturnUrl validation is local-only; external and scheme-relative targets are ignored.
- Admin authorization is server-side; hiding links is not considered a security control.

## Verification

Release commands:

```powershell
dotnet build FoodLoop.slnx -c Release
dotnet test FoodLoop.slnx -c Release --verbosity minimal
```

The SQL-backed Admin tests verify:
- anonymous challenge and non-Admin denial;
- dashboard rendering and workflow links;
- current-Available, Closed-operation, Cancelled-claim and Expired-donation count semantics;
- stable Audit pagination;
- action/actor/UTC date-range filters;
- output encoding for untrusted database text.

No schema migration is required for the Stage 3 impact summary.
