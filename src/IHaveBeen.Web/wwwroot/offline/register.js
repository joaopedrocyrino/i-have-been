export async function registerWorker() {
  if (!('serviceWorker' in navigator) || !window.isSecureContext) return null;
  const existing = await navigator.serviceWorker.getRegistration('/');
  if (existing) { existing.update().catch(() => {}); return existing; }
  return navigator.serviceWorker.register('/service-worker.js', { scope: '/', type: 'module', updateViaCache: 'none' });
}
// The first install happens only when the user opens Offline, preserving component CSS loading.
if (!location.pathname.startsWith('/offline/') && 'serviceWorker' in navigator)
  navigator.serviceWorker.getRegistration('/').then(registration => registration?.update()).catch(() => {});
let banner;
function connectivity() {
  if (navigator.onLine) { banner?.remove(); banner = null; return; }
  if (banner || location.pathname.startsWith('/offline/')) return;
  banner = document.createElement('a'); banner.href = '/offline/index.html';
  banner.className = 'notice'; banner.textContent = 'You are offline. Open your saved journal ↗';
  Object.assign(banner.style, { position: 'fixed', top: '120px', left: '10px', right: '10px', zIndex: '10001' });
  document.body.append(banner);
}
window.addEventListener('online', connectivity); window.addEventListener('offline', connectivity); connectivity();
// Enhanced navigation must not keep the server circuit around on the standalone page.
document.addEventListener('click', event => {
  const link = event.target instanceof Element ? event.target.closest('a[href="/offline/index.html"]') : null;
  if (link) { event.preventDefault(); location.assign(link.href); }
});
