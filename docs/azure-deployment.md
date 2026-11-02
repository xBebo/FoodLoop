# Azure backend deployment

Target: `foodloop-api-xbebo`, Windows App Service, .NET 10, Free F1.

## Workflow and trust boundary

`.github/workflows/ci.yml` remains unchanged. It builds .NET 10 and runs the
foundation tests against a disposable SQL Server database, alongside frontend
lint/build. `deploy-api.yml` listens for successful **push** runs of `CI` on
`develop` in `xBebo/FoodLoop`, publishes that run's exact `head_sha`, and deploys
only if the commit is still the current `develop` head. Deployment runs are
serialized; a newer commit arriving after the head check will be deployed by its
own successful CI run. Re-running CI for the current develop commit retries a
deployment. The workflow must first be merged into the default branch.

PR runs only validate publishing. They have `contents: read`, no Azure login and
no OIDC permission. Only the guarded deployment job has `id-token: write`.
Artifacts are produced by this same workflow run, never consumed from PR CI.
Actions are pinned to commit SHAs. No publish profile, client secret, SQL
credentials, migrations or production bootstrap are included.

Azure must trust the managed identity `foodloop-github-deployer` with:

- Issuer: `https://token.actions.githubusercontent.com`
- Audience: `api://AzureADTokenExchange`
- Intended immutable branch subject:
  `repo:xBebo@180184183/FoodLoop@1367518337:ref:refs/heads/develop`
- Website Contributor scoped only to `foodloop-api-xbebo`.

Repository secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID` and
`AZURE_SUBSCRIPTION_ID` must contain the managed identity client ID, tenant ID
and subscription ID respectively. Secret values and the live Azure federation
configuration cannot be verified from repository contents. An `AADSTS700213`
or equivalent subject mismatch requires checking the actual subject against
the Azure federated credential. Do not broaden trust to PRs or wildcard refs.
No GitHub environment is declared because an environment would change the OIDC
subject and require a corresponding federation change.

## App Service prerequisites

- Windows .NET 10 runtime and a 32-bit worker. Publish uses `win-x86`, framework
  dependent, `UseAppHost=false`; Web SDK generates an in-process IIS `web.config`
  launching `dotnet .\FoodLoop.Web.dll`. The installed runtime must include x86
  ASP.NET Core 10. A Linux container or x64 executable is not used.
- Production environment (`ASPNETCORE_ENVIRONMENT=Production`; leave
  `DOTNET_ENVIRONMENT` unset or also Production).
- Connection strings: `DefaultConnection`, type **SQLAzure**, using the limited
  `foodloop_app` SQL user. .NET maps this to `ConnectionStrings:DefaultConnection`.
  Keep the value in App Service, never the repository or workflow.
- Existing migrations and production initialization completed separately.
  The SQL user needs normal application read/write access, including
  `DataProtectionKeys`; startup does not create or migrate the database.
- `DonationExpiryScheduler__Enabled=false` for F1. No scheduler is provisioned
  by this workflow; expiry automation remains a separate operational task.
- `ReverseProxy__Enabled=false` or absent. Its existing Vercel-oriented option
  clears trusted proxy restrictions. Windows IIS hosting handles the native
  request scheme; do not enable unrestricted forwarded headers for this target.
- HTTPS Only enabled; SCM endpoint reachable from GitHub-hosted runners.
  Prebuilt publish output is deployed; no App Service build is needed.

The final smoke check uses the deployment action's returned hostname (including
any Azure-assigned suffix), forces HTTPS and requests `/api/auth/antiforgery`.
It checks for a token without printing tokens/cookies. This exercises startup
and SQL-backed Data Protection key access, but does not verify user login,
authorization or every database operation. Generating Data Protection keys may
persist a key through the application's normal mechanism. Smoke failure does
not roll back the deployed package; inspect App Service logs before retrying.

## F1 and routing assessment

F1 is viable for a demonstration with a small workload, subject to live startup
and memory checks. It has shared compute, 60 CPU minutes/day, 1 GB memory and
1 GB storage, no Always On or deployment slots. Expect cold starts, idle unloads,
quota stops and deployment interruptions. It is not a reliable production tier;
an always-running scheduler needs different hosting. No paid upgrade is made.

Root `vercel.json` is Vercel configuration, outside the backend publish root;
Azure IIS does not apply its services, rewrites or cron. The backend has no
Vercel-only route gate: `/api/*` controllers and existing MVC routes remain
available on Azure. The React frontend is not included in the backend publish.
For the Azure-backed SPA, use a separate Vercel project rooted at `frontend`.
Its `frontend/vercel.json` proxies `/api` and `/api/*` to Azure before the SPA
fallback. The repository-root Vercel services/cron configuration is the older
Vercel backend setup and is preserved; do not use it for this frontend project.
See [Frontend on Vercel](vercel-frontend.md) for settings and release checks.

## References

- [App Service GitHub Actions and OIDC](https://learn.microsoft.com/en-us/azure/app-service/deploy-github-actions?tabs=openid)
- [Immutable GitHub subjects in Entra](https://learn.microsoft.com/en-us/entra/workload-id/workload-identities-github-immutable-subjects)
- [GitHub workflow_run semantics and security](https://docs.github.com/en/actions/reference/workflows-and-actions/events-that-trigger-workflows#workflow_run)
- [F1 limits](https://azure.microsoft.com/en-us/pricing/details/app-service/windows/)
- [Free/Shared 32-bit workers](https://learn.microsoft.com/en-us/troubleshoot/azure/app-service/worker-process-percent-memory-limit-message)
