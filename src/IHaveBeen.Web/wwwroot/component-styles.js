// The shared journal builds read-only presentation nodes in JavaScript. Reuse
// the same companion styles that the corresponding Razor components load.
const pending = new Map();

export function loadComponentStyle(component) {
  const href = `/Components/${component}.razor.rz.scp.css`;
  if (pending.has(href)) return pending.get(href);

  const promise = new Promise((resolve, reject) => {
    const existing = [...document.querySelectorAll('link[rel="stylesheet"]')]
      .find(link => link.getAttribute('href') === href);
    if (existing?.sheet) { resolve(); return; }
    const link = existing || document.createElement('link');
    link.addEventListener('load', resolve, { once: true });
    link.addEventListener('error', () => {
      pending.delete(href);
      if (!existing) link.remove();
      reject(new Error('Unable to load component styles.'));
    }, { once: true });
    if (!existing) {
      link.rel = 'stylesheet';
      link.href = href;
      link.dataset.componentStyle = href;
      document.head.append(link);
    }
  });
  pending.set(href, promise);
  return promise;
}
