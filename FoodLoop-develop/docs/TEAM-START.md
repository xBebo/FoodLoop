# First tasks after pulling the foundation

Baraa merges the reviewed foundation into develop before the team starts from it. Everyone follows README setup, verifies a successful build and uses their own SQL database.

| Owner | First PR | Acceptance |
|---|---|---|
| Jana | Account login/logout + pending organization approval | Seeded user logs in; only Admin approves; pending donor is blocked by the server. Use existing ApplicationUser/DbContext/roles, not a second Identity setup. |
| Alaa | Donation create + publish + marketplace | Active donor publishes a valid donation; expired/invalid listings are rejected. Review entity/status/expiry contract with Safa. |
| Safa | Create claim use case + My Claims | Available unexpired donation gets one active claim; concurrent attempts produce one winner with no orphan audit. Use the shared UnitOfWork and repository contracts. |
| Haneen | Assign courier + My Tasks | Admin assigns a courier; only that courier reads the task. Review task/QR state rules before token work. |
| Baraa | Audit list + basic admin dashboard | Admin sees persisted events and real counts; do not sum incompatible quantity units. Coordinate PRs without taking ownership of teammates' feature logic. |

## Definition of reviewable

- Small scope; solution builds.
- Server-side permission, ownership and validation checks.
- Relevant tests and a working screen or request example.
- Shared contract changes reviewed by the affected owner.
- Migration/designer/snapshot committed together if changed.
- No secret values or generated build/database files.

Nightly: demonstrate the merged journey, report blockers and assign next tasks. A feature working only on one laptop is not integrated.
