(() => {
  const key = 'i-have-been:theme';
  const root = document.documentElement;
  const system = window.matchMedia('(prefers-color-scheme: dark)');
  const valid = value => value === 'light' || value === 'dark';
  const requested = new URLSearchParams(location.search).get('theme');
  let stored;
  try { stored = localStorage.getItem(key); } catch { /* Preferences also work when browser storage is unavailable. */ }
  let preference = valid(requested) ? requested : valid(stored) ? stored : null;

  function refreshControls() {
    document.querySelectorAll('[data-theme-choice]').forEach(button => {
      const pressed = String(button.dataset.themeChoice === root.dataset.theme);
      if (button.getAttribute('aria-pressed') !== pressed) button.setAttribute('aria-pressed', pressed);
    });
  }
  function apply(theme) {
    root.dataset.theme = theme;
    refreshControls();
    document.dispatchEvent(new CustomEvent('ihb:themechange', {detail: {theme}}));
  }
  apply(preference || (system.matches ? 'dark' : 'light'));
  document.addEventListener('click', event => {
    const button = event.target instanceof Element ? event.target.closest('[data-theme-choice]') : null;
    const theme = button?.dataset.themeChoice;
    if (!valid(theme)) return;
    preference = theme;
    try { localStorage.setItem(key, theme); } catch { /* Keep the selected theme for this page. */ }
    apply(theme);
  });
  system.addEventListener('change', event => { if (!preference) apply(event.matches ? 'dark' : 'light'); });
  window.addEventListener('storage', event => {
    if (event.key !== key) return;
    preference = valid(event.newValue) ? event.newValue : null;
    apply(preference || (system.matches ? 'dark' : 'light'));
  });
  // Blazor navigation/hydration can introduce new controls without reloading the document.
  new MutationObserver(refreshControls).observe(root, {subtree: true, childList: true});
})();
