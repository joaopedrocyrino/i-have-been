# Offline journal

## Using it

1. Sign in online and choose **Offline** in the map header.
2. Create a separate device passphrase (at least 12 characters). Log details,
   dates, descriptions, wishlist entries, coordinates, and city experiences are
   encrypted and saved immediately. There is no passphrase recovery.
3. In **Saved copy**, choose memories and photos/videos, then **Update saved copy**.
   Wait for the “Ready for offline use” message. Photos are selected by default;
   videos require an explicit choice. The limit is 512 MiB of encrypted records.
4. Open `/offline/index.html` without a connection and unlock with the passphrase.
   After setup, navigating to `/` while the server cannot be reached opens the
   locked offline shell. Browsing, filtering, stars, themes, country polygons,
   visited/wishlist pins, and selected original media work without a server circuit.
5. Changes, uploads, sharing, and account operations require a connection. Refresh
   the saved copy manually online; it is a dated snapshot, not a live database.

The standalone offline page runs JavaScript in the browser alongside the .NET
monolith. Blazor Server continues to power the online editor. Caching a server
circuit would not make its controls run offline.

## Private data

- Offline saving is opt-in. The main journal does not automatically copy API
  responses, media, account HTML, CSRF tokens, cookies, or invitation content.
- The service worker caches an explicit allowlist of public app files. IndexedDB
  stores encrypted journal/original records. Only opaque account/record IDs,
  encryption parameters, an epoch, and ciphertext byte counts remain unencrypted.
- AES-256-GCM uses a fresh 96-bit nonce per write. Additional authenticated data
  binds each record to its owner, purpose, and ID. A 32-byte random salt and
  PBKDF2-HMAC-SHA256 with 600,000 iterations derive a non-exportable key.
  Passphrases/keys are never saved or sent to the server.
- Keys and decrypted object URLs exist only in the unlocked page. Closing/reloading
  requires another unlock. **Lock**, 15 minutes of inactivity, or locking another
  tab clears visible private data and revokes object URLs. A back/forward restored
  page locks again. Keeping the app actively in use extends the idle interval.
- Sign-out clears the encrypted device copy and locks other controlled tabs.
  A successful sign-in/register under another owner also removes the previous
  account copy. IndexedDB epoch checks prevent a late save from recreating a
  removed vault. **Remove saved copy** works without contacting the server.
- An online unlock checks the authenticated owner. When the server cannot be
  reached, the offline passphrase is the local access credential. Browser
  storage is tied to this origin and browser profile, not shared links.
- Saving offline deliberately creates an owner-authorized device copy. A server
  cannot remotely revoke a disconnected device or erase files already downloaded
  elsewhere. Use personal devices and a strong separate passphrase. Encryption at
  rest does not protect an unlocked browser from malicious same-origin scripts or
  someone already controlling that device.
- Shared invitations never fall back to an owner snapshot or persist their media.
  They require live expiration/revocation checks on the server.

## Originals, storage, and refresh

- Downloads remain owner-authorized, private, and `no-store`. Media URL/type/size
  metadata is allowlisted; full-body byte counts and SHA-256 are checked against
  the journal. The encrypted copy decrypts to the exact original, without resizing,
  recompression, or transcoding. Only saved media is available offline.
- Downloads are sequential and cancel on lock/removal. Photos and videos are
  selectable separately per memory. Individual originals are capped at 100 MiB.
- Quota estimates warn before downloading; a transactional 512 MiB bound also
  applies to encrypted records. Previous and new copies may temporarily need extra
  room during replacement. Failed refreshes preserve the last committed journal;
  completed media from an interrupted refresh can be reused later.
- A successful refresh atomically commits the new journal and removes unselected
  or deleted media. Refreshing/removing a device copy never changes server data.
- Persistence is requested on save, but browsers can decline, evict data, or have
  smaller quotas. Clearing site data or losing the passphrase loses the local copy.
  The server remains the source of truth; this is not a backup.
- Public app files have a revision derived from file bytes, worker code, and the
  published assembly. Installation requires the complete allowlist. A new worker
  waits for existing tabs to close, then removes previous public asset versions.
  Online journal assets still come from the network when available, so code/CSS
  changes can be refreshed without serving a stale online UI.
- Production requires HTTPS; localhost is allowed for development. Browsers without
  service workers, IndexedDB, or Web Crypto show an explicit setup error.

## Map tiles

Country boundaries are shipped with the app and work offline at every map zoom.
The default public OSM raster service **prohibits offline downloads**. Its tiles
are network-only; the offline page uses the country map and saved pins.

For detailed offline tiles, supply a licensed/self-hosted raster layer under
`wwwroot/tiles/` (or a same-origin `/tiles/` reverse-proxy route) and edit
`wwwroot/map-settings.json`:

```json
{
  "tiles": {
    "url": "/tiles/world/{z}/{x}/{y}.png",
    "attribution": "© OpenStreetMap contributors · your tile provider",
    "offlineCache": true
  }
}
```

Only PNG/JPEG/WebP tiles with numeric coordinates, this explicit opt-in, and a
same-origin `/tiles/` URL enter the cache. Tiles are saved when actually viewed;
there is no city/country bulk downloader. The separate public tile cache holds
128 tiles of at most 512 KiB each (at most 64 MiB), in addition to the private
journal budget. Missing tiles leave country polygons/pins available. Sign-out,
account replacement, and removal clear the tile cache too. Provisioning a street
map dataset/provider is a deployment choice; this repository does not include one.

## Verification

- `node --test scripts/offline.test.mjs scripts/media-preflight.test.mjs`
- In an isolated checkout/Compose project: `make offline-check PROJECT=your-test-project`.
- `scripts/offline-browser.mjs` checks real PostgreSQL/Garage/ClamAV uploads,
  encrypted storage, exact originals, disconnected reload/navigation, themes,
  mobile journal behavior, invitation exclusion, failed refresh preservation,
  atomic byte/epoch limits, cross-tab locks, account changes, and sign-out cleanup.
- CI runs this suite on the app's loopback interface by sharing its network
  namespace. Localhost is a genuine secure development context, without disabling
  browser security checks. The suite refuses a non-test Compose project;
  production must use HTTPS.

References: [Blazor render modes](https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes?view=aspnetcore-10.0),
[Web Crypto encryption](https://developer.mozilla.org/en-US/docs/Web/API/SubtleCrypto/encrypt),
[browser quotas and eviction](https://developer.mozilla.org/en-US/docs/Web/API/Storage_API/Storage_quotas_and_eviction_criteria),
[OSM tile policy](https://operations.osmfoundation.org/policies/tiles/).

## Online sign-in and device trust

The online “Trust this device” option controls the account cookie (fixed 15 days
or a nonpersistent browser session). It does not unlock a saved offline journal
or extend its local idle lock. The separate offline passphrase and encrypted
snapshot remain unchanged. See [authentication](authentication.md).
