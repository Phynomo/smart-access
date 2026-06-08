# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```powershell
# Run the API (port 5102)
dotnet run --project smart-access-api/smart-access-api.csproj

# Build
dotnet build

# Restore dependencies
dotnet restore
```

No test project exists yet. API docs are available at `http://localhost:5102/scalar/v1` when running locally.

## Architecture

**Stack:** .NET 10 ASP.NET Core Web API · Google Cloud Firestore · JWT Bearer auth · BCrypt · Scalar/OpenAPI

**Layer structure** inside `smart-access-api/`:

```
Controllers/    — Thin endpoints; authorization attributes; calls services; returns ApiResponse<T>
Services/       — All business logic; throw BusinessException on domain errors
Models/         — Firestore document entities ([FirestoreData] attributes)
DTOs/           — Request/response contracts with validation attributes
Persistence/    — FirestoreContext (typed collection references + transactions); CollectionNames
Common/         — ApiResponse<T>, BusinessException, GlobalExceptionHandler, QrToken
Config/         — Firebase service account JSON (firebase-smart-access.json)
```

### Uniform response envelope

Every endpoint returns `ApiResponse<T>` (see [Common/ApiResponse.cs](smart-access-api/Common/ApiResponse.cs)):
```json
{ "success": bool, "code": int, "message": string, "data": T, "errors": ["..."] }
```
Services throw `BusinessException` with an HTTP status; `GlobalExceptionHandler` converts it to the envelope. A raw `401` (no body) means the JWT middleware fired before the controller.

### Firestore patterns

- No ORM — manual mapping with `[FirestoreProperty]` attributes on models
- `FirestoreContext` (scoped) exposes typed `CollectionReference` properties; use it for all DB access
- Multi-document atomicity: use `db.RunTransactionAsync(...)` — see [Services/AccessService.cs](smart-access-api/Services/AccessService.cs) for the canonical example
- Soft-delete pattern: `IsActive = false` on User, Resident, Vehicle (never hard-delete)

### QR token signing

QR codes embed an HMACSHA256 token signed with `Qr:Key` (config). Validation always verifies the signature before any Firestore lookup to prevent forgery. See [Common/QrToken.cs](smart-access-api/Common/QrToken.cs).

### QR code types

| Type | Behavior |
|---|---|
| `permanent` | Auto-created at resident registration; never expires; non-revocable |
| `date` | Single-use; expires end-of-day; marked used atomically in transaction |
| `long_term` | Recurrent; expires after `Residency:LongTermDefaultDays` days; capped at `Residency:MaxLongTermQrPerResident` per resident |

### Roles

Three JWT claim roles: `admin`, `security`, `resident`. Enforced at the controller boundary (`[Authorize(Roles = "...")]`) and with ownership checks inside services. `ApiControllerBase` exposes `CurrentUserId` and `CurrentUserRole` helpers from claims.

| Action | admin | security | resident |
|---|:---:|:---:|:---:|
| Manage residents/vehicles (any) | ✅ | ❌ | ❌ |
| My profile / my vehicles / my QRs | ❌ | ❌ | ✅ |
| Validate QR / manual entry | ❌ | ✅ | ❌ |
| View full access log / dashboard | ✅ | ❌ | ❌ |

### Key configuration (`appsettings.json`)

```
Jwt:Key, Jwt:Issuer, Jwt:Audience      — JWT signing (8-hour expiry)
Qr:Key                                  — HMACSHA256 QR signing
Residency:MaxLongTermQrPerResident      — Default 5
Residency:LongTermDefaultDays           — Default 30
```

Firebase credentials are loaded from `Config/firebase-smart-access.json` (service account). This file must exist locally and should never be committed to version control.

### Access log immutability

`AccessEvent` documents are append-only — no update/delete endpoints exist in code. Enforce this at the Firestore security rules level in the Firebase console.

## Domain documentation

- [BUSINESS_RULES.md](smart-access-api/BUSINESS_RULES.md) — full API contract, endpoint table, and business rules (Spanish)
- [Models/MODELS.md](smart-access-api/Models/MODELS.md) — Firestore collection schemas
