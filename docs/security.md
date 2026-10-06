# Security and deployment

## Access model

- All journal, city-experience, original-media and share-management endpoints require ASP.NET Core Identity authentication. Owners are taken from server-validated claims, never request JSON. Every query scopes resources to that owner. Foreign IDs return 404.
- Account passwords use the framework's salted adaptive hash, not application encryption. Passwords need 12+ characters; 5 failed attempts lock an account for 15 minutes. Authentication/share exchange is also rate-limited per source IP. Session cookies are HttpOnly, SameSite Strict and Secure in production; trusted sign-ins persist for a fixed 15 days without sliding renewal. Untrusted sign-ins use nonpersistent browser-session cookies, with the same 15-day maximum ticket lifetime. Browsers may restore session cookies when restoring a previous session; see [authentication](authentication.md). Logout rotates the security stamp, invalidating copied account cookies.
- Every HTTP API call checks the current cookie/security stamp. Blazor uses browser fetch for data operations so an old interactive circuit cannot bypass API authorization. Circuit state can contain already-viewed data, but it confers no further access.
- Unsafe account/API methods validate ASP.NET antiforgery tokens, including uploads and anonymous invitation exchanges. There is no permissive CORS policy. The CSP limits scripts and media to this origin, denies framing/objects and preserves tile/image compatibility. User content is rendered as text; uploads reject HTML/SVG and MIME mismatches.
- Accepted binary media types are header-checked JPEG, PNG, WebP, MP4/MOV and
  WebM. Header checks recognize containers, not full codec validity. When scanning is enabled (the default), every new
  upload also requires a clean ClamAV verdict before Garage storage; originals
  are preserved without decoding, recompression or transcoding.

## Upload scanning and quotas

- Regular accounts may store 100 files across all their journal entries, with
  photo/video limits of 20/100 MiB by default. Managers have no count limit but
  still use a 100 MiB safety cap, scanning when enabled and request limits. Types
  are manually assigned in PostgreSQL; application routes never write them.
- Quotas are checked early and enforced atomically by PostgreSQL on publication.
  Database type changes affect later requests; downgrades retain existing files.
- The handler bounds actual copied bytes rather than trusting declared lengths.
  Uploads use a private mode-0700 quarantine directory in the app container;
  handler temporary files use mode 0600 and delete on close. Multipart buffers
  also stay in that private directory, outside webroot and Garage.
- The scanner receives original bytes through private `INSTREAM` TCP requests.
  No scanner port is published. It has no database/Garage credentials, and it
  alone joins `scanner_updates` for signature downloads; database and storage
  do not join that network. Use a protected transport if services move to
  separate hosts: ClamAV TCP has no built-in authentication/encryption.
- Only `stream: OK` admits a file. Detections and exceeded scan limits reject
  it; connection errors, malformed replies, timeouts, or definitions older than
  72 hours fail closed with `503`. Readiness includes signature freshness.
- `infra/clamav/clamd.conf` aligns stream/file limits with upload limits and
  enables `AlertExceedsMax` and encrypted-content alerts so skipped scans are
  not treated as clean. Scan workers, queues, recursion and expanded bytes are
  bounded. The official 1.5.4 image runs as amd64, including emulation on ARM
  development machines; update the pinned image as security releases ship.
- The browser checks selection count, sizes and allowed headers before any
  file is sent. It is not an antivirus engine or a trusted gate. A WebAssembly
  scanner could add early feedback but a client can bypass it, so server
  scanning is the trusted gate when enabled. No private originals or hashes are sent to
  third-party scanning services.
- Malware signatures reduce risk; a clean verdict cannot prove that every
  unknown threat is absent. Files stay passive private originals, and patched
  scanner/codec components and restricted access remain necessary.
- Previously stored originals are retained; this change scans new uploads and
  does not label or retroactively scan the existing collection.

## Explicit scanning-disabled mode

- The operator requested a temporary deployment without ClamAV on the 1 GiB
  shared droplet. `staging` selects `MALWARE_SCANNING_ENABLED=false`; `main`
  selects `true`. Both branches deploy the same public domain and database.
- .NET's `MalwareScanning:Enabled` defaults to true. Explicit false registers
  `DisabledMalwareScanner`, returning `Skipped` and omitting scanner health and
  dependency metrics. Skipped is not a clean antivirus verdict; startup logs
  identify the mode. Scanner code/configuration remain available.
- Files receive no server-side antivirus inspection in this mode. Header and
  size validation, auth/CSRF/ownership checks, per-account quotas, private Garage
  and abuse limits remain enforced. Client validation does not establish safety.
- Re-enabling scanning protects subsequent uploads; it does not scan originals
  uploaded during the disabled period. No existing media are labeled as scanned.
- The setting is a nonsecret deployment parameter, recorded in each release.
  Server runtime credentials remain only in the droplet-owned `.env`.

## Request limits

`RequestLimits` values in `appsettings.json` can be overridden with .NET
configuration variables such as `RequestLimits__IpPerMinute`:

| Budget | Default |
| --- | --- |
| Requests per IP per minute, including static assets | 600 |
| Requests per authenticated account per minute | 300 |
| Concurrent HTTP requests, before authentication/database access | 64 |
| Login, registration and invitation operations per IP per minute | 25 |
| Upload attempts per account per minute | 12 |
| Concurrent uploads globally / per account | 8 / 2 |
| Concurrent Blazor WebSockets globally / per account | 128 / 5 |
| Maximum upload request duration, including body parsing and scanning | 300 seconds |

- Limits use no waiting queue and return `429` Problem Details with
  `Retry-After`. Authenticated budgets key on server-validated user IDs; IP
  budgets use only the address from trusted forwarded-header processing.
- Upload budgets span journal IDs, preventing bypass by varying request paths.
  Slow uploads are canceled at their deadline. A writable response returns
  `504` Problem Details; cancellation while reading an incomplete body can
  terminate the connection instead.
  Managers remain subject to abuse controls. Long-lived WebSockets have a
  separate connection budget, leaving HTTP permits available for the UI.
- These rate budgets are per application process and reset on restart.
  PostgreSQL media quotas hold across replicas. A multi-instance deployment
  needs shared rate budgets at the ingress or a distributed limiter.
- Application limits cannot absorb a network-level distributed attack. Enforce
  corresponding body, connection, timeout and abuse limits at the public proxy
  or CDN before traffic reaches the monolith.

## Garage is private

- The bucket is **not public**. No website endpoint is configured; website access is explicitly disabled by provisioning.
- The app key has read/write on this application's bucket, without bucket ownership or create-bucket permission. Admin credentials are only passed to the Garage service and one-shot provisioning container.
- Docker publishes no Garage S3, admin, RPC or PostgreSQL ports. The services live on a private internal network. The app talks to Garage over that network; Internet media requests go through app authentication. Use encrypted/private networking if splitting services across hosts.
- Credentials are generated independently by `scripts/init-env.py`, saved in mode-0600 `.env`, and ignored by Git. Never commit, print or send them to frontend code. Production database/storage/Grafana credentials stay in the droplet deployment directory's mode-0600 `.env`. Deployment validates and reads this file without uploading it from GitHub or replacing it.
- Images/videos stay in Garage, never `wwwroot` or a static CDN. Download/view routes return `Cache-Control: no-store, private` and `nosniff`; the app returns bytes rather than redirects or presigned URLs. Any proxy/CDN must also bypass caching for `/api/*`, `/s/*`, accounts and authenticated pages.

## Invitation links

- `/s/<random-uuid>#<256-bit-secret>` is a capability link. The secret is hashed in PostgreSQL and returned only when created. The fragment is not sent in HTTP URLs, access logs or referrers; browser code removes it immediately and exchanges it in a CSRF-protected POST.
- The viewer receives a Data Protection authenticated/encrypted HttpOnly cookie scoped to that invitation's API path. Other shares cannot use it. The cookie lasts at most 30 days or the invitation's earlier expiration; reopening a non-expiring link can renew the viewer session.
- Every shared log/media request checks the cookie, database token hash, owner scope, expiration, revocation and memory-sharing flag. An owner login alone does not authorize another user's invitation.
- A link is read-only; it does not create an owner account session. Different links can have different expirations and be revoked independently. The UI supports custom local expiration times, presets and no expiration.
- Friends can forward a valid full link, and authorized viewers can save files or screenshots they already receive. Expiration/revocation stops **future server requests**; it cannot erase copies already delivered. A shared page rechecks access every 15 seconds and clears its displayed memories when invalidated; server denial is immediate on the next request.

## Publishing the source publicly

- Source visibility does not change runtime authentication or make Garage public.
  The production workflow runs only on pushes to `main`. The `production`
  environment supplies SSH/registry deployment credentials; database, storage and
  Grafana runtime secrets remain in the droplet's `.env`.
  Opening a fork pull request does not run this workflow or grant its author
  access to secrets. Review all workflow/deployment changes before merging them.
- Git and Docker exclude dotenv files, common private keys/credential stores and
  database backups. Example dotenv files contain placeholders only. These ignore
  rules do not remove files already committed or prevent `git add --force`.
- After committing and **before pushing**, run `make secrets-check`. It scans the
  complete reachable Git history with digest-pinned Gitleaks, default provider
  rules and an additional rule for generated database/Garage credentials. The
  scanner runs without network access and redacts findings. It does not scan
  uncommitted files; commit the intended changes before running this check.
- The `verify` job fetches full history and runs the same check before building,
  publishing or deploying. CI runs after a push, so it cannot prevent a pushed
  secret from already becoming public. Enable GitHub's repository push protection
  before the first push as an additional gate for supported secret types.
- In GitHub Settings, keep production environment access restricted to `main`,
  restrict repository write/admin access to trusted people, enable push protection
  and protect `main` from deletion/force pushes. Public Actions logs/artifacts must
  never contain production configuration, keys, database dumps or real user media.
- A clean scan is not a guarantee that every credential pattern or security flaw
  has been found. If a credential is exposed, revoke/rotate it immediately; deleting
  the file or rewriting history cannot revoke copies someone already obtained.
- Registration is enabled by default. For a personal deployment, create your owner
  account and then set `ALLOW_REGISTRATION=false` in the droplet `.env` if other
  accounts are unnecessary. Per-account upload quotas do not prevent abuse through
  many separately registered accounts.

## Production configuration

The current unrestricted deployment SSH access is intentionally retained. The
workflow never requests or uploads the runtime `.env`, but possession of that SSH
key can still grant access to server files. A restricted deployment key/command
would be a separate change.

The production-only GitHub Actions pipeline deploys onto the shared Caddy
droplet using an immutable, tested image. See [deployment](deployment.md) for
triggers, scoped credentials, private networks, backups and rollback behavior.

Local development intentionally uses HTTP on loopback. For online use:

1. Set `ASPNETCORE_ENVIRONMENT=Production`, `ALLOWED_HOSTS=your-real-domain.example` and terminate HTTPS at a trusted reverse proxy. Production requests that are not recognized as HTTPS fail closed with 503, and account/share cookies use Secure.
2. Set `TRUSTED_PROXY_IP` to the exact proxy address seen by the app. Forward `X-Forwarded-Proto` and `X-Forwarded-For`; never clear ASP.NET's known-proxy restrictions or trust arbitrary Internet headers. Keep app ingress private/loopback and keep every storage/database port closed.
3. Reverse proxy both HTTP and Blazor WebSockets. Allow uploads of at least 101 MiB, suitable upload timeouts and streamed video responses. Preserve security headers, no-store rules and range requests.
4. Decide whether registration should be open. Set `ALLOW_REGISTRATION=false` after creating an account for personal deployment. Email confirmation, account recovery, passkeys/MFA and verified email delivery are not enabled in this initial slice; add them before wider public account onboarding. There is no default administrator or password.
5. Persist and protect the `protection_keys` volume across restarts; it secures cookies/circuits. Local keys are stored on a private volume without application-level at-rest encryption. Restrict host access and protect backups; configure a key-encryption provider/secret store for production.
6. Keep .NET/PostgreSQL/Garage images and NuGet packages patched, review migrations, run the integration suite on an isolated stack and monitor failed auth, errors, storage growth and backups.

Garage here is single-node with replication factor 1: persistent storage, not high availability or a backup. Back up PostgreSQL plus Garage **metadata and data**, and the Data Protection keys, to an independently protected destination. Coordinate snapshots with a stopped/quiesced app or another consistency strategy; test restoring both database records and original files. Plan redundancy before promising durable public service.

## Verification

`make check` builds locked dependencies with warnings as errors and runs domain, application and architecture tests. `make smoke` exercises real PostgreSQL/Garage: authentication, CSRF rejection, owner isolation, private memories, valid/invalid links, expiry, revocation, tampered cookies, media deletion, byte-exact originals, byte ranges anonymous Garage denial, plaintext production rejection and untrusted forwarded-header rejection. It writes isolated test users in its selected database; CI provisions a disposable stack. The browser test exercises real Blazor creation/editing/uploads/sharing, city experience star input, country/city wishlist creation, yellow map styling and conversion to visited, map polygons/dragging and invalidated viewer behavior.

This is an initial implementation with tested access controls, not a claim of absolute security or an independent penetration test.

## Primary references

- [.NET 10 support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)
- [Blazor authentication and authorization](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0)
- [Npgsql EF Core provider](https://www.npgsql.org/efcore/)
- [Garage S3 compatibility](https://garagehq.deuxfleurs.fr/documentation/reference-manual/s3-compatibility/)
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [ClamAV Docker and memory requirements](https://docs.clamav.net/manual/Installing/Docker.html)
- [ClamAV streaming protocol](https://docs.clamav.net/manual/Usage/ClamdProtocol.html)
- [OWASP file upload protection](https://cheatsheetseries.owasp.org/cheatsheets/File_Upload_Cheat_Sheet.html)

## Offline device copies

Owners can explicitly save an encrypted offline snapshot protected by a separate
local passphrase. Server APIs and Garage access remain authenticated and no-store;
private responses never enter CacheStorage. Shared invitations require a live
connection. A saved owner copy uses local unlock while disconnected, so remote
revocation cannot erase it. Sign-out/account changes purge device copies. See
[offline.md](offline.md) for cryptography, locking, quotas, and the device trust model.
