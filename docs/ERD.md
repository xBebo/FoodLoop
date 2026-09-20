# FoodLoop ERD and physical database diagram

These diagrams describe the implemented SQL Server model used by the application. They are documentation only; Stage 3 adds no schema or migration.

## Conceptual ERD

```mermaid
flowchart LR
    Organization[Organization]
    User[Application User]
    Category[Food Category]
    Donation[Food Donation]
    Claim[Donation Claim]
    Token[QR Verification Token]
    Handover[Handover Record]
    Audit[Audit Log]

    Organization -- "has users" --> User
    Organization -- "donates" --> Donation
    Category -- "classifies" --> Donation
    Donation -- "is reserved by" --> Claim
    Organization -- "beneficiary owns" --> Claim
    User -- "courier assigned to" --> Claim
    Claim -- "has temporary verification tokens" --> Token
    User -- "courier bound to" --> Token
    Claim -- "has persisted handover evidence" --> Handover
    User -- "courier performs" --> Handover
    User -- "may act in" --> Audit
```

Core lifecycle state lives on `FoodDonation` and `DonationClaim`. `HandoverRecord` is evidence, not a parallel task state. Raw QR values are never stored; `QrVerificationToken` stores the hash and binding metadata.

## Physical SQL Server diagram

```mermaid
erDiagram
    Organizations ||--o{ AspNetUsers : "OrganizationId"
    Organizations ||--o{ FoodDonations : "DonorOrganizationId"
    FoodCategories ||--o{ FoodDonations : "FoodCategoryId"
    FoodDonations ||--o{ DonationClaims : "FoodDonationId"
    Organizations ||--o{ DonationClaims : "BeneficiaryOrganizationId"
    AspNetUsers o|--o{ DonationClaims : "AssignedCourierUserId"
    DonationClaims ||--o{ QrVerificationTokens : "DonationClaimId"
    AspNetUsers ||--o{ QrVerificationTokens : "CourierUserId"
    DonationClaims ||--o{ HandoverRecords : "DonationClaimId"
    AspNetUsers ||--o{ HandoverRecords : "CourierUserId"
    AspNetUsers o|--o{ AuditLogs : "ActorUserId"

    Organizations {
        uniqueidentifier Id PK
        nvarchar Name
        nvarchar LicenseNumber UK
        int Type
        int Status
        nvarchar Address
        rowversion RowVersion
    }

    AspNetUsers {
        uniqueidentifier Id PK
        uniqueidentifier OrganizationId FK
        nvarchar UserName
        nvarchar Email
        nvarchar DisplayName
    }

    FoodCategories {
        uniqueidentifier Id PK
        nvarchar Name
    }

    FoodDonations {
        uniqueidentifier Id PK
        uniqueidentifier DonorOrganizationId FK
        uniqueidentifier FoodCategoryId FK
        nvarchar Title
        decimal Quantity
        int Unit
        datetimeoffset PreparedAtUtc
        datetimeoffset ExpiresAtUtc
        int Status
        rowversion RowVersion
    }

    DonationClaims {
        uniqueidentifier Id PK
        uniqueidentifier FoodDonationId FK
        uniqueidentifier BeneficiaryOrganizationId FK
        uniqueidentifier AssignedCourierUserId FK
        int Status
        nvarchar FailureReason
        rowversion RowVersion
    }

    QrVerificationTokens {
        uniqueidentifier Id PK
        uniqueidentifier DonationClaimId FK
        uniqueidentifier CourierUserId FK
        int Purpose
        nvarchar TokenHash
        datetimeoffset CreatedAtUtc
        datetimeoffset ExpiresAtUtc
        datetimeoffset UsedAtUtc
        rowversion RowVersion
    }

    HandoverRecords {
        uniqueidentifier Id PK
        uniqueidentifier DonationClaimId FK
        uniqueidentifier CourierUserId FK
        int Type
        datetimeoffset CompletedAtUtc
        nvarchar Notes
    }

    AuditLogs {
        uniqueidentifier Id PK
        uniqueidentifier ActorUserId FK
        nvarchar Action
        nvarchar EntityType
        uniqueidentifier EntityId
        nvarchar Details
        datetimeoffset CreatedAtUtc
    }
```

ASP.NET Core Identity also owns its standard supporting tables such as roles, user-role joins, claims, logins and tokens. They are omitted from the diagram above so the FoodLoop business relationships remain readable.

## Important constraints

- Organization LicenseNumber is unique.
- Food donation quantity and prepared/expiry ordering are protected by database constraints.
- Donation/Claim/QR concurrency-sensitive rows use RowVersion.
- Only one active claim slot is allowed for a donation by the existing filtered unique index.
- Handover evidence is unique per claim/type.
- QR purpose and lifecycle/status values are constrained to the committed enum ranges.
