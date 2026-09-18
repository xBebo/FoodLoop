# FoodLoop

ASP.NET Core MVC graduation project with integrated registration/approval, donations, claims, courier handover, Admin dashboard and audit workflows.

## Team ownership

| Owner | Feature |
|---|---|
| Jana | Identity UI, organizations, approval and authorization |
| Alaa | Donations, marketplace and expiry |
| Safa | Claims, cancellation and concurrency-safe booking |
| Haneen | Courier assignment, QR verification and handover |
| Baraa | Audit, admin dashboard and integration |

Start by reviewing [the implemented shared contracts](docs/SHARED-CONTRACTS.md) and the current [Tasks 3 of 3 — Release Candidate & Demo Readiness](docs/TASKS-03.md). [Tasks 2 of 3](docs/TASKS-02.md) and [TEAM-START.md](docs/TEAM-START.md) remain as Stage 2 and Stage 1 history.

## Prerequisites

- .NET 10 SDK (global.json allows installed .NET 10 feature bands, including 10.0.401).
- Visual Studio supporting .NET 10 with ASP.NET development tools, or the .NET CLI.
- SQL Server LocalDB on Windows, or a SQL Server instance you can access.
- EF Core / Identity packages and the repository-local EF tool are pinned to 10.0.12.

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

```powershell
dotnet build FoodLoop.slnx
dotnet test FoodLoop.slnx --verbosity minimal
```

Tests need SQL Server, not EF InMemory or SQLite. By default they use LocalDB. For a different server, set `FOODLOOP_TEST_SQLSERVER` privately. The login must be able to create/drop a test database. Tests **replace any database name in that connection** with their own `FoodLoop_FoundationTests_<random>` name, apply migrations and remove only that generated database afterward. They never target FoodLoop_Development.

Coverage includes migration/model consistency, SQL constraints, competing rowversion updates, one active claim, QR rowversion, unique handovers, transaction rollback, audit immutability through tracked saves, marketplace filtering, repeatable Identity seeding, claim cancellation, organization suspension/reactivation, and admin audit filtering. The integrated suite also covers feature authorization, real MVC forms, registration/login, concurrency, token replay and the complete delivery lifecycle.

## Working together

1. Pull the merged baseline from develop and run it locally.
2. Create a feature branch for one small task.
3. Implement a reviewable slice, including its UI and appropriate tests.
4. Open a PR into develop. The affected owner reviews shared contracts.
5. Merge and demonstrate the integrated application nightly.

The existing release branch is named `master`; do not assume `main` exists. Confirm GitHub branch protection in the repository UI; it is not configured by these source files.

## Admin dashboard and audit list

- `/Admin`: Admin-only operational counts.
- `/Admin/Audit`: Admin-only audit records, 20 per page, newest first (UTC timestamps).
- `/Admin/AccessDenied`: public explanatory page returning HTTP 403; contains no admin data.

`AddApplication()` in Application/DependencyInjection.cs explicitly registers application services. Keep the existing Infrastructure registrations in AddInfrastructure(); coordinate shared registration-file changes with the other owners.

Read [the admin feature guide](docs/ADMIN-FEATURE.md) for the count definitions, count semantics and manual checks. No migration is required for these read-only screens.

## Integrated team demo

For the integrated baseline, follow [INTEGRATION-HANDOFF.md](docs/INTEGRATION-HANDOFF.md) for the complete registration, approval, donation, claim and handover demo. The implemented lifecycle and code-expiry defaults are in [SHARED-CONTRACTS.md](docs/SHARED-CONTRACTS.md). Application services are registered through AddApplication(); no integration migration is required.
