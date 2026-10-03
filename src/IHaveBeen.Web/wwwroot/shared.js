import {initialize, setLogs, focusLog, clearMap, request} from './map.js';
import {loadComponentStyle} from './component-styles.js';
const root = document.getElementById('shared-root');
if (root) {
  const id = root.dataset.shareId;
  const status = document.getElementById('shared-status');
  const list = document.getElementById('shared-logs');
  const detail = document.getElementById('shared-detail');
  let timer, expirationTimer, selectedId, selectionSignature, selectionVersion = 0;
  function locked(message) {
    selectionVersion++; selectedId = null;
    clearInterval(timer); clearTimeout(expirationTimer); clearMap();
    list.replaceChildren(); detail.replaceChildren(); detail.hidden = true;
    updateMetrics([]);
    document.getElementById('shared-title').textContent = 'This journal is unavailable.';
    status.textContent = message;
    root.querySelector('[data-journal-panel]').open = true;
  }
  function node(tag, text, css) { const n = document.createElement(tag); if (text) n.textContent = text; if (css) n.className = css; return n; }
  function updateMetrics(logs) {
    const visited = logs.filter(log => !log.isWishlist);
    document.getElementById('shared-countries').textContent = new Set(visited.map(log => log.country)).size;
    document.getElementById('shared-visited').textContent = visited.length;
    document.getElementById('shared-wishlist').textContent = logs.length - visited.length;
  }
  async function select(log) {
    const version = ++selectionVersion;
    const styles = [loadComponentStyle('Journal/MemoryDetails')];
    if (log.experiences?.length) styles.push(loadComponentStyle('Experiences/ExperienceList'), loadComponentStyle('Experiences/StarRating'));
    await Promise.all(styles);
    if (version !== selectionVersion) return;
    selectedId = log.id; selectionSignature = JSON.stringify(log);
    detail.replaceChildren(); detail.hidden = false;
    const close = node('button', '×', 'quiet'); close.setAttribute('aria-label','Close memory'); close.onclick = () => {selectionVersion++; detail.hidden = true; detail.replaceChildren(); selectedId = null;};
    detail.append(close, node('p', `${log.city} · ${log.country}`, 'eyebrow'), node('h2', log.title), node('p', log.isWishlist ? `Wishlist · ${log.plannedOn || 'someday'}` : log.visitedOn, 'hint'), node('p', log.description, 'description'));
    if (log.experiences?.length) {
      const experiences = node('section', '', 'experiences'); experiences.setAttribute('ihb-experience-list', ''); experiences.append(node('h3', 'City experiences'));
      for (const item of log.experiences) {
        const card = node('article', '', 'experience-card');
        const rating = node('span', '★'.repeat(item.rating) + '☆'.repeat(5-item.rating), 'star-display');
        rating.setAttribute('ihb-star-rating', '');
        rating.setAttribute('role', 'img'); rating.setAttribute('aria-label', `${item.rating} out of 5 stars`);
        card.append(node('strong', item.title), node('small', `${item.category}${item.visitedOn ? ' · ' + item.visitedOn : ''}`), rating, node('p', item.description, 'description'), node('small', item.address)); experiences.append(card);
      }
      detail.append(experiences);
    }
    const grid = node('div', '', 'media-grid');
    for (const file of log.media) {
      const figure = node('figure');
      if (file.contentType.startsWith('image/')) { const img = node('img'); img.src = file.url; img.alt = file.caption || file.originalName; img.loading = 'lazy'; figure.append(img); }
      else { const video = node('video'); video.src = file.url; video.controls = true; video.preload = 'metadata'; figure.append(video); }
      figure.append(node('figcaption', file.caption)); grid.append(figure);
    }
    detail.append(grid); focusLog(log);
  }
  function openMemory(log) {
    select(log).catch(() => { status.textContent = 'Unable to open this memory. Please reload and try again.'; });
  }
  async function refresh() {
    const data = await request('GET', `/api/shared/${id}/logs`);
    updateMetrics(data.logs);
    document.getElementById('shared-name').textContent = `${data.name}'s world`;
    document.getElementById('shared-title').textContent = 'Every place has a story.';
    status.textContent = `${data.logs.length} shared memories${data.expiresAt ? ` · available until ${new Date(data.expiresAt).toLocaleString()}` : ''}`;
    list.replaceChildren();
    for (const log of data.logs) { const button = node('button', '', log.isWishlist ? 'log-card wishlist' : 'log-card'); const span = node('span'); span.append(node('strong', log.title), node('small', `${log.city ? log.city + ", " : ""}${log.country}`), node('small', log.isWishlist ? `Wishlist · ${log.plannedOn || 'someday'}` : log.visitedOn)); button.append(span); button.onclick = () => openMemory(log); list.append(button); }
    // Preserve playback unless the memory or its media actually changes.
    if (selectedId) {
      const selected = data.logs.find(log => log.id === selectedId);
      if (!selected) { selectionVersion++; detail.replaceChildren(); detail.hidden = true; selectedId = null; }
      else if (JSON.stringify(selected) !== selectionSignature) await select(selected);
    }
    setLogs(data.logs, openMemory);
    clearTimeout(expirationTimer);
    if (data.expiresAt) expirationTimer = setTimeout(() => locked('Your invitation has expired.'), Math.min(2147483647, Math.max(0, new Date(data.expiresAt) - Date.now())));
  }
  const token = window.location.hash.slice(1);
  history.replaceState(null, '', window.location.pathname);
  try {
    await initialize('map');
    if (token) await request('POST', `/api/shared/${id}/session`, {token});
    await refresh();
    timer = setInterval(() => refresh().catch(() => locked('This invitation has expired, was revoked, or could not be verified.')), 15000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh().catch(() => locked('This invitation could not be verified.')); });
  } catch { locked('Ask the owner for a complete, valid invitation link.'); }
}
