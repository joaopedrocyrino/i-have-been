# Account sessions

- Login and registration offer **Trust this device for 15 days**, unchecked by
  default. The server treats only `trustDevice=true` as opt-in; absent or invalid
  values use a browser session. Registration signs in using the same choice.
- Trusted sign-ins create a persistent, protected Identity cookie expiring
  15 days after sign-in. Activity and security-stamp revalidation never extend
  that deadline. After expiry, sign in again.
- Untrusted sign-ins create a session cookie without `Expires` or `Max-Age`.
  It works across tabs and reloads until the browser session ends. Its protected
  server ticket also has a 15-day safety maximum, even if the browser stays open.
- Browsers determine when a session ends. Session restoration can preserve
  session cookies across a browser restart. Use **Sign out** on shared devices
  when immediate revocation matters; individual tab closure is not sign-out.
- Cookies remain HttpOnly, SameSite Strict and Secure in production. No account
  credentials or tokens are placed in localStorage, sessionStorage or URLs.
- Identity validates the security stamp on every HTTP request. Signing out
  rotates that stamp and invalidates all account sessions, including copied
  trusted cookies on another device. Password/security-stamp changes also
  invalidate existing tickets. Failed sign-ins retain existing lockout and
  request limits; all account forms retain antiforgery protection.
- A separately saved encrypted offline journal still needs its local passphrase
  and locks on idle/page closure. Device trust does not automatically unlock it.
  See [offline access](offline.md).
- Existing cookies keep the lifetime encoded when issued. Sign out and back in
  to choose the new policy. Data Protection keys survive app rebuilds in their
  existing private Docker volume; no database migration is needed.

## Why there is no refresh-token endpoint

The frontend is a Blazor server monolith using ASP.NET Core Identity's protected
cookie tickets. Identity handles cookie validation and principal refresh while
the ticket retains its original expiration. A separate access/refresh-token
pair would duplicate that session mechanism. A future independent mobile/API
client may need a different authentication design.

## Verification

Run `make auth-check PROJECT=ihb-auth-check` on a disposable stack after installing Playwright. The
suite creates isolated test accounts and verifies form defaults, browser-session
cookies, trusted persistence across browser restart, fixed expiration through
stamp revalidation, CSRF, invalid credentials, cookie tampering and revocation.
CI runs it through the `auth-browser` Compose tool service. Never run fixture
suites against a personal or production database.

## References

- [ASP.NET Core cookie authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0)
- [Identity security-stamp validator](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.identity.securitystampvalidatoroptions?view=aspnetcore-10.0)
- [Browser session-cookie behavior](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Set-Cookie)
