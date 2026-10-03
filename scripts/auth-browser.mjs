import assert from 'node:assert/strict';
import {randomUUID} from 'node:crypto';
import {mkdtemp, rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {chromium} from 'playwright';

// Fixture suite: only the disposable local/CI endpoints used by this project.
assert.match(process.env.AUTH_TEST_PROJECT || '', /^ihb-[a-z0-9-]*(?:ci|check|test)[a-z0-9-]*$/,
  'AUTH_TEST_PROJECT must name a disposable ihb-...ci/check/test project.');
const base = process.env.BASE_URL || 'http://localhost:4193';
const url = new URL(base);
assert.ok(url.protocol === 'http:' &&
  ((url.hostname === 'localhost' && url.port === '4193') ||
   (url.hostname === 'ihb.test' && url.port === '8080')),
  'Use the disposable test stack at localhost:4193 or ihb.test:8080.');

for (let attempt = 0; attempt < 60; attempt++) {
  try { if ((await fetch(base + '/health/ready')).status === 200) break; } catch {}
  await new Promise(resolve => setTimeout(resolve, 1000));
}
const profile = await mkdtemp(join(tmpdir(), 'ihb-auth-browser-'));
const email = `${randomUUID()}@example.test`;
const password = 'Travel-Test!2026';
const lifetime = 15 * 24 * 60 * 60;
const options = {headless: true, baseURL: base};
let context, extra, checks = 0;
function check(value, message) {assert.ok(value, message); checks++;}
const call = (path, options) => context.request.get(path, options);
async function cookie() {
  return (await context.cookies(base)).find(c => c.name === 'ihb.account');
}
async function post(path, form, csrf = true) {
  const headers = {};
  if (csrf) headers['X-CSRF-TOKEN'] = (await (await call('/api/csrf')).json()).token;
  return context.request.post(path, {form, headers, maxRedirects: 0});
}
async function signedIn(response, label) {
  check(response.status() === 302 && response.headers().location === '/', label);
  const account = await cookie();
  check(account?.httpOnly && account.sameSite === 'Strict', 'Account cookie is HttpOnly and SameSite Strict');
  check((await call('/api/me')).status() === 200, 'Session authorizes the account API');
  check((await call('/api/offline/session')).status() === 200, 'Session authorizes live offline-owner validation');
  return account;
}
function cookieHeader(response) {
  return response.headersArray().find(h => h.name.toLowerCase() === 'set-cookie' && h.value.startsWith('ihb.account='))?.value;
}
function checkPersistent(account, started) {
  check(Math.abs(account.expires - (started + lifetime)) <= 5, 'Trusted cookie expires 15 days after sign-in');
}
try {
  context = await chromium.launchPersistentContext(profile, options);
  check((await call('/health/ready')).status() === 200, 'Disposable stack is ready');
  check((await call('/api/me')).status() === 401, 'Anonymous API access is denied');
  const page = await context.newPage();
  for (const path of ['/login', '/register']) {
    await page.goto(base + path);
    const trust = page.getByRole('checkbox', {name: 'Trust this device for 15 days'});
    check(await trust.count() === 1 && !await trust.isChecked(), `${path} has unchecked device trust`);
    check(await trust.getAttribute('name') === 'trustDevice' && await trust.getAttribute('value') === 'true', 'Trust control submits explicit opt-in');
    check(await page.locator('#trust-device-help').isVisible(), 'Browser-session explanation is visible');
  }
  check((await post('/account/register', {name: 'Auth fixture', email, password, trustDevice: 'true'}, false)).status() === 400,
    'Trusted registration still requires antiforgery protection');
  const started = Date.now() / 1000;
  const registration = await post('/account/register', {name: 'Auth fixture', email, password, trustDevice: 'true'});
  const trusted = await signedIn(registration, 'Trusted registration signs in');
  checkPersistent(trusted, started);
  check(/expires=/i.test(cookieHeader(registration)), 'Trusted registration emits a persistent cookie');
  await context.close();
  context = await chromium.launchPersistentContext(profile, options);
  check((await call('/api/me')).status() === 200, 'Trusted sign-in survives a real browser close/reopen');
  check((await cookie()).expires === trusted.expires, 'Browser restart retains the original deadline');
  await new Promise(resolve => setTimeout(resolve, 2100));
  const revalidated = await call('/api/me');
  check(revalidated.status() === 200 && /expires=/i.test(cookieHeader(revalidated)), 'Security-stamp revalidation renews the trusted cookie');
  check((await cookie()).expires === trusted.expires, 'Revalidation never extends the 15-day deadline');
  const copied = await cookie();
  check((await post('/account/logout', {})).status() === 302, 'Trusted sign-out succeeds');
  check(!await cookie(), 'Sign-out removes the browser account cookie');
  extra = await chromium.launchPersistentContext(await mkdtemp(join(profile, 'copy-')), options);
  await extra.addCookies([copied]);
  check((await extra.request.get('/api/me')).status() === 401, 'Sign-out invalidates a copied trusted ticket');
  await extra.close(); extra = undefined;

  const untrustedResponse = await post('/account/login', {email, password});
  const untrusted = await signedIn(untrustedResponse, 'Login without trust signs in');
  check(untrusted.expires === -1 && !/(expires|max-age)=/i.test(cookieHeader(untrustedResponse)), 'Untrusted cookie is browser-session only');
  const tab = await context.newPage();
  await tab.goto(base + '/login');
  await tab.reload();
  check((await call('/api/me')).status() === 200, 'Untrusted sign-in survives reloads');
  await tab.close();
  check((await call('/api/me')).status() === 200, 'Closing one tab keeps the browser session signed in');
  const renewedSession = await call('/api/me');
  check(!/(expires|max-age)=/i.test(cookieHeader(renewedSession) || '') && (await cookie()).expires === -1,
    'Security-stamp revalidation does not persist an untrusted cookie');
  await context.close();
  context = await chromium.launchPersistentContext(profile, options);
  check(!await cookie() && (await call('/api/me')).status() === 401,
    'Untrusted sign-in ends on clean browser restart without session restoration');

  check((await post('/account/login', {email, password: 'Wrong-Password!2026', trustDevice: 'true'})).headers().location === '/login?error=1',
    'Invalid credentials are rejected even when device trust is requested');
  check(!await cookie(), 'Failed sign-in issues no account cookie');
  const loginStarted = Date.now() / 1000;
  const login = await post('/account/login', {email, password, trustDevice: 'true'});
  const trustedLogin = await signedIn(login, 'Trusted login signs in');
  checkPersistent(trustedLogin, loginStarted);
  check(/expires=/i.test(cookieHeader(login)), 'Trusted login emits a persistent cookie');
  await context.clearCookies();
  await context.addCookies([{...trustedLogin, value: trustedLogin.value + 'tampered'}]);
  check((await call('/api/logs')).status() === 401, 'Tampered persistent cookies are rejected');
  await context.clearCookies();
  const untrustedRegistration = await post('/account/register', {name: 'Session fixture', email: `${randomUUID()}@example.test`, password, trustDevice: 'false'});
  check((await signedIn(untrustedRegistration, 'Untrusted registration signs in')).expires === -1,
    'Registration with trust false uses a browser session');
  await post('/account/logout', {});
  const malformed = await post('/account/login', {email, password, trustDevice: 'on'});
  check((await signedIn(malformed, 'Login accepts credentials with an invalid trust value')).expires === -1,
    'Invalid trust values cannot enable persistence');
  await post('/account/logout', {});
  console.log(`Passed ${checks} authentication browser checks.`);
} finally {
  await extra?.close();
  await context?.close();
  await rm(profile, {recursive: true, force: true});
}
