# Insaaf — Runbook (draft)

**Status:** Draft prepared in Phase 10 (2026-11-09). **Nothing is hosted yet.** Hosting is chosen in Phase 12 (ADR-007, ADR-019, ADR-021). Everything below has been checked locally only, unless it says otherwise.

---

## 1. What runs
| Part | What it is | Notes |
|---|---|---|
| API | ASP.NET Core 9 (`src/backend/Insaaf.API`) | Stateless except for photo files; container recipe in `src/backend/Dockerfile` (not built yet: Docker wasn't available) |
| Database | SQL Server | Schema from EF Core migrations; **migrations are applied by the founder** |
| Photo files | A folder per photo under `Storage:ReviewPhotos:RootPath` | Must be persistent storage (a mounted volume); profile photos live in its `avatars/` folder (Phase 11); cloud storage comes in Phase 12 |
| Mobile app | Expo / React Native | Points at the API via `EXPO_PUBLIC_API_BASE_URL` at build time |

---

## 2. Configuration (production)
`appsettings.Production.json` holds **no secrets**. Each REQUIRED value comes from environment variables or the host's secret store.

| Setting | Environment variable | Required | Notes |
|---|---|---|---|
| Connection string | `ConnectionStrings__DefaultConnection` | ✅ | |
| JWT signing key | `Jwt__SigningKey` | ✅ | At least 32 bytes and private. **The API refuses to start outside Development with a short key or the committed development key** |
| Allowed hosts | `AllowedHosts` | ✅ | e.g. `api.example.com` |
| Photo folder | `Storage__ReviewPhotos__RootPath` | ✅ | Persistent volume, outside the app folder (the API refuses a root inside its content root) |
| Reverse proxies | `ReverseProxy__KnownProxies__0`, `__1`… | If behind a proxy | Enables forwarded headers only for these IPs (correct client IP for rate limits) |
| Admin seed | `Identity__SeedAdmin__Enabled` | ❌ | Keep `false` in production |
| Email (Phase 11) | `Email__Provider` (`file` or `smtp`), `Email__From`, `Email__SmtpHost`, `Email__SmtpPort`, `Email__SmtpUserName`, `Email__SmtpPassword`, `Email__SmtpEnableSsl` | ✅ for password reset | `file` writes to `Email__OutboxPath` (default `%LOCALAPPDATA%\Insaaf\outbox`) and is for Development only; production needs `smtp` |
| Environment | `ASPNETCORE_ENVIRONMENT=Production` | ✅ | Turns off Swagger, turns on HSTS |

**Verified locally (2026-11-09):** with `ASPNETCORE_ENVIRONMENT=Production` and environment-variable configuration:
- The development key is refused at startup.
- With a strong key, `/health/live` and `/health/ready` return 200 `Healthy`.
- Swagger returns 404.
- The security headers are present.

---

## 3. Health
| Endpoint | Meaning | Body |
|---|---|---|
| `GET /health/live` | The process answers | `Healthy` |
| `GET /health/ready` | The process can reach the database | `Healthy` / `Unhealthy`, with no details |
| `GET /api/v1/health` | Legacy envelope check (Phase 1) | `{ status: "healthy" }` |

---

## 4. Routine operations
- **Release a new version:**
  1. Build and test (CI).
  2. Back up the database.
  3. The founder applies any new migration.
  4. Deploy the API.
  5. Check `/health/ready`.
- **Migrations:**
  - Review the generated migration and its SQL. Check it with `dotnet ef migrations script` and then `SET PARSEONLY ON` (as done in Phase 9A).
  - Then `dotnet ef database update` against the target database.
- **SQL Server integration tests** (`tests/Insaaf.IntegrationTests`, Phase 10):
  - They run against a **separate** database, whose name must contain `IntegrationTests`. Any other database is refused.
  - Without a connection string they are skipped, which is how CI runs them.
  - **One-time setup:** apply the migrations to the test database yourself (`dotnet ef database update --connection "...Database=Insaaf_IntegrationTests..."`). Do this again after every new migration; the tests report missing migrations and never apply them.
  - **Configure** with `dotnet user-secrets set "IntegrationTests:ConnectionString" "Server=localhost;Database=Insaaf_IntegrationTests;Trusted_Connection=True;TrustServerCertificate=True" --project tests/Insaaf.IntegrationTests`, or the environment variable `IntegrationTests__ConnectionString`.
  - **Run** with `dotnet test tests/Insaaf.IntegrationTests`.
  - Each run tags its data (`it-{run}-…@example.com`, restaurants `IT-{run} …`) and deletes it at the end. The seeded restaurants and categories stay.
- **Password-reset codes in Development:** read them from the outbox folder above; delete old files when you like.
- **Report queue (Phase 11):** `GET /api/v1/admin/reports`; hide the review if needed (`PUT /admin/reviews/{id}/visibility`), then `PUT /admin/reports/{id}/resolve`.
- **Official reply (Phase 11):** `PUT /api/v1/admin/reviews/{id}/reply { text }`; `DELETE` removes it.
- **Photo cleanup:** automatic, hourly (`Storage:ReviewPhotos:CleanupIntervalMinutes`). Logs carry counts only.
- **Admin actions:** through the Admin API (Swagger in Development, or an HTTP client). Every action is logged as `Admin action {Action} by {AdminId} on {TargetType} {TargetId} at {Timestamp}: {Result}`.

---

## 5. Incidents (first checks)
| Symptom | Check |
|---|---|
| App shows network errors | `/health/live`; is the API reachable from the phone's network? Does `EXPO_PUBLIC_API_BASE_URL` match? |
| `/health/ready` unhealthy | Database reachable? Connection string? Firewall? |
| API won't start | Logs: a weak or development signing key, a photo root inside the content root, or a missing connection string |
| Many 429 responses | Rate limits (`RateLimiting:*`); behind a proxy, is `ReverseProxy:KnownProxies` set (otherwise every user shares the proxy's IP)? |
| Abusive content | Admin API: hide the review, delete the comment, remove the photo, suspend the account |
| A suspended user is still active | Expected for up to 15 minutes (access-token lifetime, ADR-017) |

---

## 6. Open for Phase 12
Hosting choice, TLS certificates, backups and restore drills, log retention (and an audit table if needed), monitoring and alerts, cloud photo storage, CI/CD deployment, iOS builds.
