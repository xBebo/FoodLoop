# Team integration fixes

## Branch and safety
Local branch: feature/team-integration-fixes.
It combines Safa, Jana, Haneen and Alaa with the existing Admin feature, preserving their source commit histories. No remote branch was pushed and develop was not changed by this work.

The separate checkout is under the Codex workspace, work/integration. The original D:\C#\FinalProject checkout remains untouched.

## What changed
- Removed the duplicate FoodLoop-develop tree from the integration result.
- Resolved Application registration/startup conflicts without dropping Admin or Claims.
- Connected approval forms to one approval service, with Pending-only transitions, actor audit and controlled concurrency.
- Made registration/role assignment transactional and retained Suspended Beneficiary login for read-only access.
- Fixed donation pagination overflow and concurrent publish handling; registered DonationService through AddApplication().
- Rebuilt courier operations around Application services and repository/Identity adapters.
- Added organization-owned code issuance, expiry/purpose/ownership/replay checks and atomic delivery evidence.
- Connected the marketplace Claim button and added role-specific navigation.
- Kept the schema unchanged.

## Demo
1. Follow README for a separate local database and explicit development role/account seeding.
2. Register a Donor and a Beneficiary; Admin approves both from Organizations.
3. Donor creates and publishes a donation. Beneficiary claims it from Marketplace.
4. Admin opens Assign courier and selects a Courier.
5. Donor opens Handover codes and generates Pickup code.
6. Assigned Courier opens My tasks, pastes code and verifies pickup.
7. Beneficiary generates Delivery code. Assigned Courier verifies it.
8. Admin confirms the Closed deliveries count and actor-aware Audit List.

Codes are manually copied/pasted, not rendered as QR images. They last at most 15 minutes. Verified delivery closes both the donation and claim; see SHARED-CONTRACTS.md.

## PR description
Title: Integrate team features and fix approval, donation and secure handover workflows

Integrates the four feature branches with the existing Admin dashboard. Approval forms now use one Pending-only audited service; donation publishing handles concurrency and large pagination inputs; courier assignment, code issuance and handover enforce roles, ownership and lifecycle state. Verified handover updates the claim, donation, token, evidence and audit atomically.

Application services share AddApplication(), the duplicate source tree is removed, and navigation connects the full donor-to-beneficiary journey. No database migration is required.

Validation: 142 tests passed, including SQL-backed concurrent pickup/publish/approval, real MVC forms with antiforgery, Identity registration/login, and the complete publish-to-closed-delivery service flow. Tests use generated disposable SQL databases; no production or team development database is migrated or deleted.

## Before publishing
Review this local integration diff and its documented lifecycle defaults. Push/merge is deliberately left to Baraa's explicit next instruction.
