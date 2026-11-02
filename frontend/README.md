# FoodLoop frontend

React, TypeScript and Vite client for the FoodLoop ITI course project.

- Live demonstration: https://foodloop-eg.vercel.app
- Setup, team attribution and backend prerequisites: [root README](../README.md).
- Production routing and deployment settings: [Vercel guide](../docs/vercel-frontend.md).

Use Node.js 24.x and run `npm ci`, then `npm run dev`. Run the ASP.NET Core backend with its `http` launch profile; Vite proxies `/api` to `http://localhost:5179`.

Verification: `npm run lint` and `npm run build` (TypeScript followed by a production Vite build). Do not put secrets in frontend environment variables. Production uses relative `/api` requests through the Vercel rewrite to Azure.

The style playground is available only in development. Screenshots and product previews use illustrative content; use fictional details when trying the live demonstration.

## Dependency maintenance

The lockfile uses patched development tools. `package.json` temporarily overrides transitive `minimatch` to 11.0.4 because upstream lint packages still request affected earlier majors (GHSA-g3hv-75wg-m249). This changes a tooling dependency across a major boundary: validate `npm ci`, lint and build when updating it, and remove the override once upstream supports a patched version. Node.js 24.x is required for the supported development setup.
