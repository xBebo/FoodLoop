# FoodLoop shared foundation - handoff

Prepared in `D:\C#\FinalProject` on local branch `feature/shared-foundation`. Changes are uncommitted and have not been pushed.

## Added

- Eight shared Domain entities plus the Infrastructure Identity user, seven enums and Guid-based relationships.
- EF Core / Identity 10.0.12 dependencies, repository-local EF tool and .NET 10 SDK selection.
- SQL Server DbContext, mappings, initial migration, rowversions, active-claim uniqueness and handover uniqueness.
- Repository interfaces/implementations, UnitOfWork and staged audit service.
- Current-user service, Identity/cookie registration and MVC antiforgery filter.
- Development-only seed command with optional locally supplied demo password.
- Landing page, setup README, shared-contract proposal, first tasks and PR template.
- Fifteen SQL Server integration tests.

## Validation

- Solution build: zero warnings/errors.
- Tests: 15 passed, none skipped.
- Initial migration applied to a fresh isolated SQL Server test database; test database cleaned up.
- Local FoodLoop_Development created and migrated: four roles, three categories, no user accounts.
- Application startup and landing page: HTTP 200.
- Git diff whitespace check: passed.

## Before sharing

1. Review `docs/SHARED-CONTRACTS.md` with Alaa, Safa and Haneen. Lifecycle rules are proposed, not recorded as meeting-approved.
2. In Visual Studio, review Git Changes on `feature/shared-foundation`. Save any editor buffers before review; files were changed on disk.
3. Commit with a message such as `feat: add shared FoodLoop foundation`.
4. Push this feature branch, then open a Pull Request into `develop`. Do not push directly to develop.
5. After review and merge, teammates pull develop and follow `README.md`.

## Where to start

- `README.md`: framework, restore/build, database, seeding and run instructions.
- `docs/SHARED-CONTRACTS.md`: IDs, statuses, dependencies, invariants and limits.
- `docs/TEAM-START.md`: each member's first PR and acceptance criteria.

## Remaining feature work

Jana builds login/registration and approval screens/rules. Alaa builds donation and expiry use cases. Safa builds safe claiming/cancellation. Haneen builds courier/QR verification. Baraa builds audit views and dashboard. The underlying SQL safeguards are tested, but these application workflows are not implemented by the foundation.

Optional demo accounts require `Seed:DemoPassword` in the Web project's private user secrets, followed by the seed command. No password is committed.
