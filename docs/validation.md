# Verification

Validated on an isolated Docker stack on 2026-10-02, using .NET 10.0.401, PostgreSQL 17 and Garage 2.4.1.

- Locked restore and Release build: zero warnings and errors.
- 45 domain/application/architecture tests passed.
- 83 integration checks passed against actual SQL and object storage: owner isolation, CSRF, original bytes/ranges, shared/private filtering, expiry/revocation/logout, experience validation/ownership, wishlist creation/conversion and Problem Details.
- 25 Chromium checks passed: detailed draggable map, original multi-file uploads, editing, 0/5 star input, city/category display, country/city wishes, yellow countries/pins, conversion to green, filters, shared experiences/media, revocation, mobile layout and sign out.
- Upgrade fixture seeded through the previous application image, then verified after the new migration: existing account session, memory ID/date/content, default Visited status, owner/shared original bytes and invitation continued working.
- EF tooling reported no pending model changes after `ExperiencesAndWishlist`.

Reproduce domain/architecture checks with `make check`, integration with `make smoke`, and UI checks with `make browser` after installing the development dependency. Integration/browser/upgrade scripts create disposable test data; use an isolated Compose project. Browser previews and the sensitive upgrade fixture stay under ignored `output/`.

## Map behavior and theme update

- Release build passed with zero warnings or errors.
- The expanded Chromium suite passed 43 checks, including country-first selection, no rectangular focus outline, preserving zoom, no pin editor at level 7, precise clicked-pixel coordinates at level 8, selecting a different country at high zoom, keyboard polygon focus, system/saved/URL theme preferences, storage-disabled fallback, dark tile/pin styling, unfiltered original photos, shared viewer themes and light/dark mobile layouts.
- The same run covered existing journal creation/editing, multiple original uploads, experiences/ratings, wishlist colors/conversion, invitation access/revocation and logout.
- Light/dark desktop, shared viewer and mobile previews were reviewed from the disposable stack.
- This frontend change adds no database migration.

## Component CSS update

- Locked restore and Release publish passed. The output contains 12 separate
  component stylesheets and no combined application stylesheet.
- 56 Chromium checks passed on an isolated Docker stack. Added request checks
  confirm that login omits journal/Leaflet CSS, the initial journal omits dialogs
  and star controls, opening editors loads nested styles, and the shared viewer
  loads reused details/experience/rating styles only when a memory opens.
- Computed-style checks cover server-rendered auth layout, interactive dialogs,
  reopened dialogs, star inputs and JavaScript-generated shared content. All
  requested component stylesheet responses succeeded.
- Existing themes, zoom gating, journal/media flows, experiences, wishes,
  invitation revocation, mobile layouts and sign out passed in the same run.
- Desktop dark mode, mobile light mode and the shared viewer screenshots were
  reviewed. Original photo colors remain unchanged.
- Global CSS decreased from 16,312 to 5,431 bytes; component-specific rules now
  live in companion files and are loaded according to rendered components.
- No database schema, authentication or media-storage configuration changes.

## Mobile journal update

- 20 standalone Chromium layout checks passed during development, covering
  compact metrics, expansion/collapse, keyboard/focus behavior, selection
  collapse, short-screen scrolling, measured controls, desktop restoration and
  light/dark layouts. Three previews were reviewed.
- The initial Docker run was interrupted by an unresponsive engine. Subsequent
  account/upload-protection verification built the final Razor markup and
  completed the 73-check application browser suite, including owner/shared
  metrics, filtering, saving while collapsed, media and revoked invitations.
- A focused 64-check UI regression passed after the upload timeout adjustment.
  Desktop, owner/shared mobile and dark layouts were reviewed.
- The journal presentation itself adds no database migration.

## Account types and upload protection

Validated on an isolated Docker stack on 2026-10-04 with real PostgreSQL,
private Garage and ClamAV 1.5.4 with freshly downloaded signatures.

- Locked restore, Release build and container publish passed with zero warnings
  or errors. All 66 domain/application/architecture/infrastructure tests passed.
  EF tooling reported no pending model changes for `AccountTypesAndMediaQuota`.
- Six JavaScript precheck tests passed: original-byte preservation, selection
  quotas, manager count bypass, byte caps, matching types and active/empty files.
- 34 security integration checks passed: injected registration types, SQL type
  constraints, account-wide counts, concurrent last-slot inserts, deletion and
  slot reuse, manual promotions/downgrades, Identity saves preserving types,
  photo caps, actual ClamAV rejection of a structured harmless EICAR fixture,
  byte-exact clean uploads, scanner outages, simultaneous upload/HTTP limits,
  account/upload/IP throttling and forwarded-address spoofing.
- 88 integration checks passed against the final image, including all previous
  access/media/sharing checks plus bounded incomplete multipart uploads. The
  one-second test policy canceled an unfinished request without publishing
  media; actual deployment uses a five-minute deadline.
- The complete application browser suite passed 73 checks. A subsequent focused
  64-check regression verified the upload interop timeout and journal flows,
  reusing existing UI checks without repeating unchanged map interaction.
- The app runs without root. Its quarantine directory was verified as mode 0700;
  handler temporary files use mode 0600, outside webroot and object storage.
- A private backup of the existing local database was created before applying
  the migration. Production/local accounts are not automatically promoted.

Use `make check`, `node --test scripts/media-preflight.test.mjs`, `make smoke`
and `make browser`. Run `make security PROJECT=ihb-ci` only from a disposable
checkout whose `.env` points at that isolated stack. It creates test fixtures,
briefly stops its scanner and exhausts request budgets; never run it against
personal or production data. CI runs the account/security and browser suites.

## Offline journal validation

- Release build/publish and 66 .NET tests passed; no compiler warnings/errors.
- 17 JavaScript checks passed (11 offline cryptography/policy checks and 6 upload prechecks).
- 88 integration checks passed against the actual PostgreSQL/Garage/ClamAV stack.
- 37 offline browser checks passed on native localhost and again in the CI Docker
  service using the app's loopback network namespace. This includes disconnected
  reload/navigation, exact originals, encrypted storage, mobile/theme behavior,
  account isolation, cross-tab lock, logout cleanup, and atomic storage/epoch checks.
- The existing full 73-check online browser suite passed in native Chromium.
  The Docker renderer crashed during the large-country zoom sequence on this
  development machine; the full native run verified those same interactions.
- The offline mobile screenshot was visually reviewed. All fixtures lived in the
  isolated `ihb-offline-check` stack; the real account and journal were not used.

## Trusted device authentication

Validated on the disposable `ihb-auth-check` stack on 2026-10-05.

- Locked restore, Release build/publish and all 66 .NET tests passed with zero
  compiler warnings or errors. All 17 JavaScript policy/preflight tests passed.
- 50 new Chromium authentication checks passed: login/registration defaults,
  antiforgery, fixed 15-day persistent cookies, real browser restart persistence,
  stamp revalidation retaining the original deadline, browser-session cookies
  surviving reload/tab closure and ending on a clean browser restart without
  restoration, invalid credentials, tampering and copied-cookie revocation.
- All 88 PostgreSQL/Garage/ClamAV integration checks and all 37 offline browser
  checks passed against the updated image, including encrypted journal access,
  account changes, cross-tab locking and sign-out cleanup.
- The authentication suite is included in CI and has an explicit disposable
  project/endpoint guard. No database migration, media changes or new packages.

Use `make auth-check PROJECT=ihb-auth-check` from an isolated checkout/stack.
Browser session restoration can retain session cookies; the clean-restart check
proves normal browser-session behavior, not forced deletion in every browser.

## Production-only GitHub Actions

Validated locally on 2026-10-05; no live GitHub/droplet deployment was performed.

- Actionlint validates the workflow, including main-only push filtering, job
  dependencies, permissions and the single production environment. Official
  actions are pinned to verified release commits and managed by Dependabot.
- Production Compose renders with exactly six services, no host ports, no build
  instructions or test containers, the external proxy route and private state
  services. App/migration image references and persistent key volume match.
- Twelve Linux deployment tests pass with mocked Docker/SSH endpoints: ordering,
  scoped operations, backup/migration failure, HTTPS readiness and rollback, first
  release, exact proxy detection, pinned image/SSH host, private configuration,
  blocked credential drift and safe configuration updates. These tests verify
  orchestration; they do not simulate a live registry, certificate or droplet.
- Bash syntax/ShellCheck validation and Python helper parsing pass. Both local
  and production environment generation preserve mode 0600 and refuse overwrite.
- Docker locked restore/Release publish succeeds for the production image; the
  OCI repository source label is supplied through the build argument. All 17
  existing JavaScript policy/preflight tests pass. Application code is unchanged.
- The workflow runs the full existing .NET, real infrastructure/security and
  browser suites before publishing the exact tested image artifact.

See [deployment](deployment.md) for activation settings and recovery limits.

## Observability

See [monitoring, privacy controls, Grafana setup and operational runbooks](observability.md). Production telemetry is opt-in through the droplet-owned `/opt/i-have-been/.env`; runtime secrets are not supplied to GitHub Actions.
