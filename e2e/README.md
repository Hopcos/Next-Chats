# Internal Auth E2E

End-to-end verification for the **Internal Auth (内部鉴权)** feature: admin CRUD of auth-center configs (acs / ucs…), login-method discovery, internal sign-in that auto-provisions users, composite-user uniqueness, and role-based catalog filtering.

See also [`run-refresh-e2e.ps1`](#refresh-token-e2e) below for the dual-token (JWT + refresh) suite.

## Files

| File | Purpose |
| --- | --- |
| `mock-auth.js` | Minimal mock auth center on `127.0.0.1:53131` — `POST /login` (password `secret123` → `{"sessionID":"S-…"}`), `GET /login-check` (equals-rule / dotted-path case), `GET /login-badjson` (non-JSON case) |
| `run-internal-auth-e2e.ps1` | The assertion suite (Windows PowerShell 5.1 compatible, `-UseBasicParsing`) |
| `run-refresh-e2e.ps1` | Dual-token suite: rotation / replay rejection / disable-revokes / logout-revokes (pass the current admin password via `-AdminPassword`; it is **not** the long-promoted seeded default once changed) |

## Prerequisites

- API running on `http://localhost:5210` (seeded `admin / admin123`)
- `node` (for the mock auth center)

## Run

```bash
# 1. start the mock auth center
node e2e/mock-auth.js

# 2. run the suite (from the repo root)
powershell -NoProfile -ExecutionPolicy Bypass -File e2e/run-internal-auth-e2e.ps1

# 3. stop the mock auth center when done (Ctrl+C, or kill the node process)
```

The script cleans up after itself (test users and auth configs are deleted); a leftover `iae_*` user or provider indicates a failed run — delete it in **管理后台 → 用户管理 / 内部鉴权管理** or via the admin API.

## What is asserted

1. Admin `default` login still works.
2. Create `acs` provider (POST `/api/admin/internal-auth`) with a `NotEmpty` rule on `sessionID`.
3. Anonymous `GET /api/auth/providers` lists `acs`.
4. Internal sign-in (`authType: acs`, correct password) returns a JWT; the user is **auto-provisioned** (authType=`acs`, displayName=username, status=Active, bound to the provider's default role).
5. Repeating the sign-in does **not** duplicate the user (same user id is returned).
6. Wrong password → `401`; unknown provider (`ucs` not configured) → `401`; non-JSON success response → `401`.
7. `GET /api/me` for the internal user reports `authType=acs`; `/api/me/catalog` is filtered by role bindings (narrower than the admin catalog; all models within the role's bound model ids).
8. Composite uniqueness `(AuthType, Username)`: a `default` user and an `acs` user may share the same username; each signs in with its own credential path.
9. `Equals` rule + dotted JSON path (`data.sessionID == "S-abc"`) via a `GET` provider works.
10. Cleanup leaves no `iae_*` users or test providers.

## Refresh-Token E2E

Verifies the **dual-token session** (Plan B): access = 30 min, refresh persisted (SHA-256 hash), rotation on every use, and revocation on re-login / logout / user disable.

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File e2e/run-refresh-e2e.ps1 -AdminPassword 'your-admin-password'
```

Asserted:

1. Admin login returns `token` + `refreshToken` + `expiresIn` (3600 → 1800 s).
2. `POST /api/auth/refresh` returns a new access + new refresh; the new access works on `/api/me`.
3. Replaying the **old** refresh token → `401 REFRESH_TOKEN_REVOKED` (rotation).
4. Garbage refresh token → `401 REFRESH_TOKEN_INVALID`.
5. The previous access token stays valid until its 30-min expiry (stateless access — expected residual window).
6. Re-login revokes previously issued refresh tokens.
7. Admin **disables** a user → that user's refresh tokens are revoked immediately (`RefreshTokensForUser`); refresh → `401` (`AUTH_USER_DISABLED` or `REFRESH_TOKEN_REVOKED` — disable both revokes and rejects).
8. Re-enable → re-login → refresh works again; **logout** revokes the user's refresh tokens.
9. Audit trail contains `REFRESH.SUCCESS` / `REFRESH.FAILED` records.
10. Cleanup removes the test user (refresh rows cascade-delete).
