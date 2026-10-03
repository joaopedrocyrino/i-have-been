import { setLogs, focusLog, clearMap } from '/map.js';
export const $ = id => document.getElementById(id);
export function node(tag, text = '', css = '') {
  const item = document.createElement(tag); item.textContent = text; if (css) item.className = css; return item;
}
let urls = [], selection = 0, filter = 'all', allLogs = [];
function revoke() { for (const url of urls) URL.revokeObjectURL(url); urls = []; }
export function closeDetail() { selection++; revoke(); $('detail').replaceChildren(); $('detail').hidden = true; }
export function clearJournal() {
  closeDetail(); allLogs = []; setLogs([]); clearMap(); $('logs').replaceChildren(); $('journal').hidden = true;
  $('snapshot-status').textContent = ''; $('search').value = '';
  for (const metric of ['countries', 'visited', 'wishlist']) $('count-' + metric).textContent = '0';
}
export function showJournal(snapshot, open) {
  closeDetail(); allLogs = snapshot.logs; $('journal').hidden = false;
  const visited = allLogs.filter(log => !log.isWishlist);
  $('count-countries').textContent = new Set(visited.map(log => log.country)).size;
  $('count-visited').textContent = visited.length; $('count-wishlist').textContent = allLogs.length - visited.length;
  $('snapshot-status').textContent = `Read-only · saved ${new Date(snapshot.savedAt).toLocaleString()}`;
  function render() {
    const query = $('search').value.toLocaleLowerCase(); $('logs').replaceChildren();
    for (const log of allLogs.filter(log => (filter === 'all' || (filter === 'wishlist') === log.isWishlist)
      && `${log.title} ${log.city} ${log.country}`.toLocaleLowerCase().includes(query))) {
      const button = node('button', '', 'log-card' + (log.isWishlist ? ' wishlist' : ''));
      const body = node('span'); body.append(node('strong', log.title), node('small', `${log.city ? log.city + ', ' : ''}${log.country}`), node('small', `${log.isWishlist ? 'Wishlist' : log.visitedOn || 'Visited'} · ${log.experiences.length} experiences`));
      button.append(node('span', log.media.length ? '▧' : '↗', 'log-icon'), body); button.onclick = () => open(log); $('logs').append(button);
    }
    if (!$('logs').children.length) $('logs').append(node('p', allLogs.length ? 'No memories match this filter.' : 'Your saved journal is empty.', 'hint'));
  }
  $('search').oninput = render;
  for (const button of document.querySelectorAll('[data-filter]')) button.onclick = () => {
    filter = button.dataset.filter;
    for (const item of document.querySelectorAll('[data-filter]')) item.setAttribute('aria-pressed', String(item === button));
    render();
  };
  render(); setLogs(allLogs, open);
}
export async function showDetail(log, getMedia) {
  closeDetail(); const token = selection, detail = $('detail'); detail.hidden = false;
  const close = node('button', '×', 'quiet'); close.setAttribute('aria-label', 'Close memory'); close.onclick = closeDetail;
  detail.append(close, node('p', `${log.city ? log.city + ' · ' : ''}${log.country}`, 'eyebrow'), node('h2', log.title),
    node('p', log.isWishlist ? `Wishlist · ${log.plannedOn || 'someday'}` : log.visitedOn || '', 'hint'), node('p', log.description, 'description'));
  if (log.experiences.length) {
    const section = node('section', '', 'experiences'); section.setAttribute('ihb-experience-list', ''); section.append(node('h3', 'City experiences'));
    for (const experience of log.experiences) {
      const card = node('article', '', 'experience-card'), rating = Math.max(0, Math.min(5, experience.rating));
      const stars = node('span', '★'.repeat(rating) + '☆'.repeat(5 - rating), 'star-display'); stars.setAttribute('ihb-star-rating', ''); stars.setAttribute('role', 'img'); stars.setAttribute('aria-label', `${rating} out of 5 stars`);
      card.append(node('strong', experience.title), node('small', `${experience.category}${experience.visitedOn ? ' · ' + experience.visitedOn : ''}`), stars, node('p', experience.description, 'description'), node('small', experience.address)); section.append(card);
    }
    detail.append(section);
  }
  const grid = node('div', '', 'media-grid'); detail.append(grid); focusLog(log);
  for (const file of log.media) {
    const figure = node('figure'); const caption = node('figcaption', file.caption || file.originalName); figure.append(caption); grid.append(figure);
    try {
      const blob = await getMedia(file);
      if (token !== selection) return;
      if (!blob) { figure.append(node('p', 'This original was not saved for offline use.', 'hint')); continue; }
      const url = URL.createObjectURL(blob); urls.push(url);
      const media = node(file.contentType.startsWith('image/') ? 'img' : 'video'); media.src = url;
      if (media.tagName === 'IMG') { media.alt = file.caption || file.originalName; media.loading = 'lazy'; }
      else { media.controls = true; media.preload = 'metadata'; }
      figure.prepend(media);
      const download = node('a', 'Original ↗'); download.href = url; download.download = file.originalName; caption.append(' ', download);
    } catch { if (token === selection) figure.append(node('p', 'Unable to unlock this original. Refresh your saved copy online.', 'notice')); }
  }
}
export function showSelection(logs, selectedIds) {
  const selected = new Set(selectedIds); $('media-selection').replaceChildren();
  for (const log of logs.filter(log => log.media.length)) {
    const label = node('label', '', 'check'), input = node('input'); input.type = 'checkbox'; input.value = log.id; input.checked = selected.has(log.id);
    const text = node('span'); text.append(node('strong', log.title), node('small', `${log.city ? log.city + ', ' : ''}${log.country} · ${log.media.length} originals`));
    label.append(input, text); $('media-selection').append(label);
  }
  if (!$('media-selection').children.length) $('media-selection').append(node('p', 'No media in your journal yet.', 'hint'));
}
