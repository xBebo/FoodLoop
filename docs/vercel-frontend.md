# Frontend on Vercel, API on Azure

## Project setup after PR approval and merge

If no frontend project exists, manually import `xBebo/FoodLoop` into Vercel
after merge and explicit deployment approval. Importing/deploying is a separate
step; this routing PR does not create a project or deploy either application.

| Setting | Value |
| --- | --- |
| Production branch | `develop` |
| Root Directory | `frontend` |
| Framework Preset | Vite |
| Node.js | 24.x (matches CI) |
| Install Command | `npm ci` |
| Build Command | `npm run build` |
| Output Directory | `dist` |

The config file is `frontend/vercel.json`, relative to that project root.
Do not import the repository root as a services project or copy its config into
`frontend`: it describes the older Vercel Docker backend and maintenance cron.
The frontend project has no cron; Azure F1 expiry scheduling remains a separate
operational task. No API base URL environment variable or frontend secret is
needed. Existing Azure runtime configuration and secrets remain unchanged.

## Routing and session behavior

The first rewrite sends `/api/:path(.*)?` to
`https://foodloop-api-xbebo-d5bxbzgpagdwfyfm.uaenorth-01.azurewebsites.net/api/:path`.
It preserves the `/api` prefix exactly once, nested paths and query parameters,
including an empty or trailing-slash suffix (`/api` maps to `/api/`). The optional
regex capture covers these boundary cases as well as nested paths without
matching `/apix`. API responses (including 400/401/403/404/500) must reach
the browser as API responses, never the subsequent `/index.html` fallback.
Other client routes retain the existing SPA fallback; built static assets are
served by Vercel. This is a rewrite, not a redirect to Azure.

React continues fetching relative `/api` URLs with `credentials: 'include'`.
Identity and antiforgery cookies have no explicit Domain override and use the
default root path. Responses through the proxy therefore establish host-only
cookies on the frontend hostname; cookies previously obtained by visiting Azure
directly do not sign the user into the frontend. Production cookies remain
Secure/HttpOnly with their existing SameSite defaults. Do not set an Azure
cookie Domain, switch to cross-origin fetches, or relax CORS/SameSite settings.

The client obtains `{ token }` from `/api/auth/antiforgery`, keeps it in memory,
and sends `X-XSRF-TOKEN` on unsafe requests. It refreshes that token after login
and logout. Auth endpoints return JSON/204 rather than MVC redirects; protected
API requests use 401/403 ProblemDetails. Creation Location headers are relative
`/api/...` paths. No host or Location rewriting is required for these routes.
Auth responses already prohibit caching; do not add CDN caching for API traffic.

The upstream destination uses HTTPS. On Windows App Service, native IIS hosting
supplies the request scheme. Keep `ReverseProxy__Enabled=false` or absent as
documented for Azure: that existing option clears proxy trust restrictions.
Do not enable it, enable broad forwarded-header environment switches, or trust
arbitrary `X-Forwarded-*` headers to make this proxy work. Vercel forwarding
visitor headers does not itself establish a trusted proxy boundary. If a loop
or Azure-host redirect appears, inspect the actual scheme/Location and hosting
configuration before changing trust. Direct Azure API access remains possible.

## Checks on the approved deployment

Local lint/build and backend integration tests cannot prove Vercel's deployed
cookie/header forwarding. Before considering the frontend ready:

1. Refresh a nested SPA route and load built JS/CSS assets successfully.
2. Request `/api/auth/antiforgery` through the frontend hostname: expect 200
   JSON, no-store, a nonempty token and a Secure/HttpOnly host-only antiforgery
   cookie. Do not paste token or cookie values into logs or reports.
3. Confirm anonymous session JSON and an unknown API path's JSON 404 (not SPA
   HTML); protected endpoints should return JSON 401/403, with no login redirect.
4. Using an authorized test account, sign in through the UI, refresh to recover
   the session, exercise an authorized action, then sign out. Check Identity
   cookie issuance/removal, XSRF header forwarding and token refresh. A request
   without a valid XSRF token must fail with `antiforgery.invalid`.
5. Check query strings, browser console/network errors, redirects and cookie
   attributes. There should be no request redirected to the Azure hostname or
   repeated HTTPS redirects. Preview and production hostnames have separate
   host-only sessions, but both target the same live Azure database; avoid
   destructive preview actions.

The existing Azure deployment workflow automatically deploys after successful
`develop` push CI. Merging this PR will therefore also trigger that workflow;
merge/deploy approval must account for it. PR validation only publishes an IIS
package and does not deploy Azure. Vercel Git auto-deploy may run on a PR if a
project is already connected; configure deployment approvals before connecting
one when manual release control is required.

## References

- [Vercel rewrites and external origins](https://vercel.com/docs/routing/rewrites)
- [Vercel project Root Directory](https://vercel.com/docs/builds/configure-a-build#root-directory)
- [Azure prerequisites and release workflow](azure-deployment.md)
