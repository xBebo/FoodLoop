# FoodLoop

ITI .NET course team project for managing surplus-food donations from listing and claiming through courier pickup and verified delivery.

**Live demonstration:** [foodloop-eg.vercel.app](https://foodloop-eg.vercel.app) · **Default branch:** `develop`

The current application uses React + TypeScript + Vite on Vercel, an ASP.NET Core API on Azure Windows App Service F1, and Azure SQL. The repository also retains the original MVC interface. This is a course demonstration: use fictional organization details and do not submit sensitive information.

Azure F1 can cold-start or stop at its quota. Periodic expiry is not provisioned by the current Azure deployment workflow; request-time expiry checks still prevent expired food from being claimed. See [Azure deployment](docs/azure-deployment.md) and [Vercel frontend routing](docs/vercel-frontend.md).

## Team ownership

| Owner | Feature |
|---|---|
| Jana Sharaf | Identity UI, organizations, approval and authorization |
| Alaa Ahmed | Donations, marketplace and expiry |
| Safa Nasr Eldin | Claims, cancellation and concurrency-safe booking |
| Haneen Ali | Courier assignment, QR verification and handover |
| Baraa - Team Leader | Audit, admin dashboard and system integration |

Start by reviewing [the implemented shared contracts](docs/SHARED-CONTRACTS.md) and the current [Tasks 3 of 3 — Functional Completion, Hardening & Demo Readiness](docs/TASKS-03.md). [Tasks 2 of 3](docs/TASKS-02.md) and [TEAM-START.md](docs/TEAM-START.md) remain as Stage 2 and Stage 1 history.

## Prerequisites

- .NET 10 SDK (global.json allows installed .NET 10 feature bands, including 10.0.401).
- Visual Studio supporting .NET 10 with ASP.NET development tools, or the .NET CLI.
- SQL Server LocalDB on Windows, or a SQL Server instance you can access.
- EF Core / Identity packages and the repository-local EF tool are pinned to 10.0.13.

Run commands below from the repository root. In Visual Studio, set **FoodLoop.Web** as the startup project.

## First run

```powershell
dotnet restore FoodLoop.slnx
dotnet tool restore
dotnet build FoodLoop.slnx
```

In Development, the default connection uses your own Windows identity:

```text
Server=(localdb)\MSSQLLocalDB;Database=FoodLoop_Development;Trusted_Connection=True;TrustServerCertificate=True
```

Each developer has a local database. Do not share MDF files or commit real credentials. For a different SQL Server, use Visual Studio **Manage User Secrets** on FoodLoop.Web and set `ConnectionStrings:DefaultConnection`. A non-secret integrated-auth example:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Database=FoodLoop_Development;Trusted_Connection=True;TrustServerCertificate=True" --project src/FoodLoop.Web
```

Apply the committed migration, then seed reference data:

```powershell
dotnet ef database update --project src/FoodLoop.Infrastructure --startup-project src/FoodLoop.Web -- --environment Development
dotnet run --project src/FoodLoop.Web -- --seed
```

Seeding creates roles and food categories. To also create demo users, privately add `Seed:DemoPassword` in **Manage User Secrets**, then run the seed command again. Choose at least 10 characters including uppercase, lowercase, a digit, and a symbol. Do not copy passwords into commits, screenshots or PR descriptions.

Demo emails (new accounts use Seed:DemoPassword at creation; changing the secret later does NOT reset an existing password):

| Email | Role | Organization |
|---|---|---|
| admin@foodloop.test | Admin | None |
| donor@foodloop.test | Donor | Active |
| beneficiary-a@foodloop.test | Beneficiary | Active A |
| beneficiary-b@foodloop.test | Beneficiary | Active B |
| courier@foodloop.test | Courier | None |
| pending-donor@foodloop.test | Donor | Pending |

Seeding is explicit and Development-only. Re-running does not duplicate seed data, reset existing passwords or change existing account roles. It does not generate donations, claims or handovers.

```powershell
dotnet run --project src/FoodLoop.Web
```

Open the URL printed by the application. Login and registration are available at `/Auth/Login` and `/Auth/Register`. Pending organizations need Admin approval before login. Access-denied responses use `/Admin/AccessDenied`. Follow the integration demo below to test the complete journey.

Normal app startup does not migrate or create a database. Outside Development a connection string must be configured explicitly. HTTPS certificate trust, if required locally, can be set with `dotnet dev-certs https --trust`.

## React development

Use Node.js 24.x. Start the backend with its HTTP profile, then run the frontend in a separate terminal:

```powershell
dotnet run --project src/FoodLoop.Web --launch-profile http
```

```powershell
cd frontend
npm ci
npm run dev
```

Open the URL printed by Vite. Its `/api` proxy targets `http://localhost:5179`. React login and registration are `/login` and `/register`; the `/Auth/*` URLs above belong to MVC. Check the frontend with `npm run lint` and `npm run build`.

## Authentication protection

Both MVC and API password sign-in share Identity lockout: five failed passwords lock the account for 15 minutes, with the same generic invalid-credentials response. Login and registration POSTs also share a fixed-window rate limit of 60 requests per minute per observed remote IP. No forwarding header is trusted just to identify a visitor. Behind Vercel, multiple visitors may share a bucket. This in-memory budget resets on app restart and is not distributed across instances.

`AuthRateLimit__PermitLimit` and `AuthRateLimit__WindowSeconds` configure the budget. API rejections return HTTP 429 with `auth.rate_limited` and `Retry-After`; MVC rejections return a short message. For larger traffic, provision a trusted edge/distributed limiter rather than enabling unrestricted forwarded headers.

## Attribution and reuse

This is a team project; preserve the ownership table when sharing it. No project-level open-source license has been selected. Third-party license notices remain with bundled libraries. Image provenance and a project license require confirmation by the team.

## Architecture

```text
Domain                           Entities and enums; no EF or Identity dependency
Application (Business Logic L)   Repository/current-user/audit contracts and application exceptions
Infrastructure (Data Access L)   EF mappings, DbContext, repositories, Identity and seed support
Web (Presentation L)             MVC composition root, authentication middleware and views
```

Web references Infrastructure to register services; feature controllers should call Application use cases rather than implementing business workflows directly in controllers.

## Database changes

Coordinate migrations with Baraa. Review shared entity/state changes with the affected owner before creating a migration. Pull/merge the latest develop first. Commit the migration, designer and snapshot together. Do not delete a migration that teammates have already applied.

```powershell
dotnet ef migrations add DescriptiveChangeName --project src/FoodLoop.Infrastructure --startup-project src/FoodLoop.Web --output-dir Persistence/Migrations -- --environment Development
dotnet ef database update --project src/FoodLoop.Infrastructure --startup-project src/FoodLoop.Web -- --environment Development
```

## Verification

Release verification:

```powershell
dotnet build FoodLoop.slnx -c Release
dotnet test FoodLoop.slnx -c Release --verbosity minimal
```

For a faster local edit/build loop, omitting `-c Release` uses the normal Debug configuration.

Tests need SQL Server, not EF InMemory or SQLite. By default they use LocalDB. For a different server, set `FOODLOOP_TEST_SQLSERVER` privately. The login must be able to create/drop a test database. Tests **replace any database name in that connection** with their own `FoodLoop_FoundationTests_<random>` name, apply migrations and remove only that generated database afterward. They never target FoodLoop_Development.

Coverage includes migration/model consistency, SQL constraints, competing rowversion updates, one active claim, QR rowversion, unique handovers, transaction rollback, audit immutability through tracked saves, marketplace filtering, repeatable Identity seeding, claim cancellation, organization suspension/reactivation, and admin audit filtering. The integrated suite also covers feature authorization, real MVC forms, registration/login, concurrency, token replay and the complete delivery lifecycle.

## Working together

1. Pull the merged baseline from develop and run it locally.
2. Create a feature branch for one small task.
3. Implement a reviewable slice, including its UI and appropriate tests.
4. Open a PR into develop. The affected owner reviews shared contracts.
5. Merge and demonstrate the integrated application nightly.

`develop` is the default and current deployment branch. `master` is an older branch, not the source for the current live demonstration. Confirm required checks and reviews in GitHub; source files do not configure branch protection.

## Admin dashboard, impact and audit list

- `/Admin`: Admin-only read-only impact counts for Pending organizations, Current Available donations, Closed operations, Cancelled claims and Expired donations.
- `/Admin/Audit`: Admin-only audit records, 20 per page, newest first (UTC timestamps).
- `/Admin/AccessDenied`: public explanatory page returning HTTP 403; contains no admin data.

`AddApplication()` in Application/DependencyInjection.cs explicitly registers application services. Keep the existing Infrastructure registrations in AddInfrastructure(); coordinate shared registration-file changes with the other owners.

Read [the admin feature guide](docs/ADMIN-FEATURE.md) for the count definitions, count semantics and manual checks. No migration is required for these read-only screens.

## Automatic donation expiry

When enabled, the Web project runs a small `BackgroundService` that creates a fresh DI scope for each expiry run and calls Alaa's `IDonationExpiryService`. The worker contains no donation state-transition rules.

Committed defaults:

```json
"DonationExpiryScheduler": {
  "Enabled": true,
  "IntervalSeconds": 60
}
```

Override these values through normal ASP.NET Core configuration, for example `DonationExpiryScheduler__IntervalSeconds` in the process environment. Request-time Marketplace/Claim checks remain authoritative between scheduler runs.

## Integrated team demo and release docs

Follow [STAGE3-DEMO.md](docs/STAGE3-DEMO.md) for the final Stage 3 journey. [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) covers common local/demo failures, and [ERD.md](docs/ERD.md) contains the current conceptual and physical database diagrams.

[INTEGRATION-HANDOFF.md](docs/INTEGRATION-HANDOFF.md) remains the earlier integrated baseline. The implemented lifecycle and security contracts are in [SHARED-CONTRACTS.md](docs/SHARED-CONTRACTS.md). No Stage 3 schema migration is required.
