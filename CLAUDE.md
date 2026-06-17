# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository layout

This is a two-part monorepo for **Smart Access**, a residential access-control system (programación web class project). The two projects are developed and run independently:

| Path | Stack | Detailed guide |
|---|---|---|
| `smart-access-api/` | ASP.NET Core (.NET 10) Web API · Firestore · JWT | [smart-access-api/CLAUDE.md](smart-access-api/CLAUDE.md) |
| `smart-access-app/` | Angular 21 + PrimeNG 21 + Tailwind v4 + Capacitor 8 (mobile) | [smart-access-app/CLAUDE.md](smart-access-app/CLAUDE.md) |

**Always read the relevant subproject's CLAUDE.md before working in it** — they hold the layer structure, conventions, and gotchas. This file only covers what spans both.

## How the two connect

- The Angular app calls the API through `ApiService` (`smart-access-app/src/app/core/services/api.service.ts`), with the base URL in `src/environments/environment.ts` — defaults to `http://localhost:5102/api`.
- The API listens on **port 5102** (`smart-access-api/smart-access-api/Properties/launchSettings.json`); the app dev server runs on **4200**. Start the API first so the app has something to talk to.
- Both sides share the same response envelope: the API returns `ApiResponse<T>` and the app types it as `ApiResponse` in `core/models/auth.models.ts`. Keep these two definitions in sync when changing the shape.
- Auth is JWT Bearer: the app stores the token and sends it via `Authorization: Bearer ...`; the API validates it and exposes `admin`, `security`, `resident` roles. Role semantics live in the API guide.

## Required local setup

The system will not run without a Firebase service-account file. Per the README, add your own credentials as:

```
smart-access-api/smart-access-api/Config/firebase-smart-access.json
```

This file (and `appsettings.json`) is git-ignored — never commit it.

## Run both sides

```powershell
# Terminal 1 — API (http://localhost:5102, docs at /scalar/v1)
dotnet run --project smart-access-api/smart-access-api/smart-access-api.csproj

# Terminal 2 — Angular app (http://localhost:4200)
cd smart-access-app; npm start
```

For mobile builds, `npm run build` must run before syncing the Capacitor native projects — see the app guide.

## Domain reference

- [smart-access-api/smart-access-api/BUSINESS_RULES.md](smart-access-api/smart-access-api/BUSINESS_RULES.md) — full API contract, endpoint table, business rules (Spanish)
- [smart-access-api/smart-access-api/Models/MODELS.md](smart-access-api/smart-access-api/Models/MODELS.md) — Firestore collection schemas
- Core concepts: permanent vs. date vs. long-term QR codes, immutable append-only access log, soft deletes. Details in the API guide.
