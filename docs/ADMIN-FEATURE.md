# Admin dashboard and audit list

## How the code fits together

1. AdminController checks the Admin role before serving either data page.
2. AdminService also checks the current authenticated role, chooses a page size of 20, and supplies TimeProvider's current UTC time.
3. IAdminReadRepository is defined in Application; AdminReadRepository in Infrastructure runs read-only SQL queries and returns small DTOs, not tracked entities.
4. Razor views render the DTOs using the existing Bootstrap layout. Razor encodes database text; audit Details are not exposed by this screen.

The audit list orders by CreatedAtUtc descending and Id descending, so tied timestamps have a consistent order. The repository clamps page numbers to the available range, including negative/extreme inputs. Empty data gets an explicit empty state. System events have no actor ID; user events show the name and ID. Names reflect current user profile values, not a historical name snapshot. Like ordinary offset pagination, new inserts between page requests can shift records; this is not a snapshot export.

## Count definitions

- Pending organizations: Organization.Status = Pending.
- Available donations: Available, ExpiresAtUtc strictly later than current time, and donor organization Active with type Donor.
- Closed deliveries: one count per Closed claim whose donation is also Closed and has a Delivery HandoverRecord. Pickup and delivery rows are not counted twice. No food quantities or incompatible units are summed.

Counts are read sequentially from one DbContext. They reflect live operational data, not a cross-query transaction snapshot. Reload to refresh.

## Authentication integration

Jana's remote branch feature/jana-sharaf defines AuthController.Login; its Infrastructure configuration still used /Account/Login when inspected. This feature corrects the cookie challenge to /Auth/Login and adds /Admin/AccessDenied returning 403. It does not merge or rewrite Jana's branch and does not add a substitute login system. A browser login is available only after her authentication code is integrated. The Admin navigation appears for a signed-in Admin.

Jana's current Login redirects to Home on success rather than honoring ReturnUrl, so click Admin dashboard after logging in. Her organization-status validation and registration role assignment remain separate review items. No non-Admin may read these admin pages regardless of organization status.

Coordinate AddApplication and Program.cs edits when integrating other feature branches; preserve every owner's registrations. Do not replace an existing extension file wholesale.

## Verification

Build: dotnet build FoodLoop.slnx
Tests: dotnet test FoodLoop.slnx

The added tests use actual SQL Server migrations in a generated disposable database. HTTP tests host the real MVC app with a test-only authentication handler: this verifies Admin access, Donor/Beneficiary/Courier denial, anonymous redirect, access-denied status, Razor rendering, extreme page inputs and output encoding. The handler exists only in the test project and is never registered by production code. It does not test Jana's password-login implementation.

Repository tests verify empty results, two-page pagination without tied-order overlap, actor display, expiry boundary, donor status, and closed-delivery evidence. Existing foundation tests still verify migration consistency.

After authentication integration, manually sign in as Admin and visit /Admin and /Admin/Audit; sign in as each non-Admin role and verify denial. Signed-out access must redirect to /Auth/Login. If no audit data exists, the empty message is expected; feature owners must stage audit records in their business transactions.

No schema migration, push, or merge is part of this change.
