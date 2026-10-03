// Native details/summary provides click, Enter/Space, and accessible expanded
// state. It stays outside Blazor UI state so the static shared page can reuse it.
const mobile = window.matchMedia('(max-width: 600px)');
const panels = new Map();

function measure(panel) {
  if (!mobile.matches) return;
  const summary = panel.querySelector('.journal-summary');
  const border = getComputedStyle(panel);
  const height = summary.getBoundingClientRect().height
    + parseFloat(border.borderTopWidth) + parseFloat(border.borderBottomWidth);
  document.documentElement.style.setProperty('--journal-card-height', `${Math.ceil(height)}px`);
}

function initialize(panel) {
  if (panels.has(panel)) return;
  panel.open = !mobile.matches;
  panel.dataset.journalReady = '';
  const observer = new ResizeObserver(() => measure(panel));
  observer.observe(panel.querySelector('.journal-summary'));
  panels.set(panel, observer);
  measure(panel);
}

document.querySelectorAll('[data-journal-panel]').forEach(initialize);
new MutationObserver(records => {
  for (const record of records) {
    for (const node of record.addedNodes) {
      if (!(node instanceof Element)) continue;
      if (node.matches('[data-journal-panel]')) initialize(node);
      node.querySelectorAll('[data-journal-panel]').forEach(initialize);
    }
  }
  for (const [panel, observer] of panels) {
    if (!panel.isConnected) { observer.disconnect(); panels.delete(panel); }
  }
}).observe(document.body, {childList: true, subtree: true});

mobile.addEventListener('change', () => {
  for (const panel of panels.keys()) {
    panel.open = !mobile.matches;
    measure(panel);
  }
});

function collapse(panel, restoreFocus = false) {
  if (!mobile.matches || !panel?.open) return;
  panel.open = false;
  if (restoreFocus) panel.querySelector('.journal-summary').focus({preventScroll: true});
}

document.addEventListener('click', event => {
  if (!(event.target instanceof Element)) return;
  if (event.target.closest('.journal-content .log-card, .journal-content .empty-state button'))
    collapse(event.target.closest('[data-journal-panel]'), true);
});

document.addEventListener('keydown', event => {
  if (event.key !== 'Escape' || !(event.target instanceof Element)) return;
  const panel = event.target.closest('[data-journal-panel]');
  if (mobile.matches && panel?.open) {
    event.preventDefault();
    collapse(panel, true);
  }
});
