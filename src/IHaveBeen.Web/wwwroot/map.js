import { localTilePattern } from '/offline/policy.js';
let map, countries, markers, bridge, observer;
let selectedCountry = '';
let currentLogs = [], currentOnSelect = null;
export const CREATE_ITEM_MIN_ZOOM = 8;

export async function initialize(id, callback = null, options = {}) {
  if (map) dispose();
  bridge = callback;
  map = L.map(id, { center: [20, 0], zoom: 2, minZoom: 2, maxZoom: 16, worldCopyJump: true, zoomControl: false });
  L.control.zoom({ position: 'bottomleft' }).addTo(map);
  const settings = await fetch('/map-settings.json').then(response => {
    if (!response.ok) throw new Error('Map settings could not be loaded.');
    return response.json();
  });
  const tiles = settings.tiles;
  // Offline tiles must be self-hosted; the public OSM tile service disallows offline downloads.
  const localTiles = localTilePattern(tiles) !== null;
  if (!options.offline || (localTiles && tiles.offlineCache === true)) {
    L.tileLayer(tiles.url, { attribution: tiles.attribution, maxZoom: 19 }).addTo(map);
  } else L.control.attribution().addTo(map).addAttribution('Offline country boundaries');
  const response = await fetch('/assets/countries.geo.json');
  if (!response.ok) throw new Error('Country boundaries could not be loaded.');
  countries = L.geoJSON(await response.json(), {
    bubblingMouseEvents: false,
    style: countryStyle,
    onEachFeature(feature, layer) {
      const name = feature.properties.name;
      layer.bindTooltip(name, { sticky: true });
      layer.on('click', event => handleCountryClick(name, layer, event.latlng));
      layer.on('add', () => {
        const path = layer.getElement?.();
        if (!path) return;
        path.dataset.country = name;
        path.setAttribute('role', 'button');
        path.setAttribute('aria-label', `Select ${name}`);
        path.setAttribute('aria-pressed', 'false');
        path.setAttribute('tabindex', '0');
        path.addEventListener('keydown', event => {
          if (event.key !== 'Enter' && event.key !== ' ') return;
          event.preventDefault(); event.stopPropagation();
          selectCountry(name, layer);
        });
      });
    }
  }).addTo(map);
  markers = L.layerGroup().addTo(map);
  map.on('zoomend', updateMapStatus);
  map.on('click', event => {
    if (bridge && selectedCountry && map.getZoom() >= CREATE_ITEM_MIN_ZOOM) choosePlace(event.latlng);
    else updateMapStatus();
  });
  document.addEventListener('ihb:themechange', refreshTheme);
  observer = new ResizeObserver(() => requestAnimationFrame(() => map?.invalidateSize()));
  observer.observe(document.getElementById(id));
  updateMapStatus();
}

function selectCountry(name, layer) {
  selectedCountry = name;
  updateCountryStyles();
  // Selection keeps the current zoom; placing a pin requires a separate deliberate click.
  const bounds = layer.getBounds();
  if (bounds.isValid()) map.panTo(bounds.getCenter(), { animate: true });
  updateMapStatus();
  bridge?.invokeMethodAsync('CountrySelected', name);
}

function handleCountryClick(name, layer, latlng) {
  if (selectedCountry !== name) { selectCountry(name, layer); return; }
  if (!bridge || map.getZoom() < CREATE_ITEM_MIN_ZOOM) { updateMapStatus(); return; }
  choosePlace(latlng);
}

function choosePlace(latlng) {
  bridge.invokeMethodAsync('ChoosePlace', latlng.lat, latlng.lng, selectedCountry);
}

function updateMapStatus() {
  const status = document.getElementById('map-status');
  if (!status || !map) return;
  status.textContent = !selectedCountry ? '' : !bridge
    ? `${selectedCountry} selected. Drag or zoom to explore.`
    : map.getZoom() < CREATE_ITEM_MIN_ZOOM
      ? `${selectedCountry} selected. Zoom in closer, then click again to place a pin.`
      : `${selectedCountry} selected. Click the exact place to add a memory.`;
}

function countryStyle(feature) {
  const name = feature.properties.name;
  const selected = name === selectedCountry;
  const dark = document.documentElement.dataset.theme === 'dark';
  const visited = currentLogs.some(x => !x.isWishlist && x.country === name);
  const wished = currentLogs.some(x => x.isWishlist && x.country === name);
  return {
    className: 'country-shape',
    color: selected ? (dark ? '#8fb8ff' : '#173f7a') : visited ? (dark ? '#36c57e' : '#1f8a5b') : wished ? '#d6a51c' : (dark ? '#52675c' : '#536d60'),
    weight: selected ? 2 : visited || wished ? 1.2 : 0.8,
    opacity: selected ? 0.95 : 0.7,
    fillColor: visited ? (dark ? '#36c57e' : '#2fbf75') : wished ? '#f5ca42' : selected ? (dark ? '#24466f' : '#dbeafe') : (dark ? '#16211d' : '#f7f9f8'),
    fillOpacity: visited || wished ? 0.23 : selected ? 0.34 : dark ? 0.08 : 0.03
  };
}

function updateCountryStyles() {
  countries?.setStyle(countryStyle);
  countries?.eachLayer(layer => layer.getElement?.()?.setAttribute('aria-pressed', String(layer.feature.properties.name === selectedCountry)));
}

function renderMarkers() {
  markers?.clearLayers();
  const dark = document.documentElement.dataset.theme === 'dark';
  for (const log of currentLogs) {
    const title = document.createElement('span'); title.textContent = log.title;
    L.circleMarker([log.latitude, log.longitude], {
      radius: 7, color: '#fff', weight: 2, bubblingMouseEvents: false,
      fillColor: log.isWishlist ? '#f5ca42' : dark ? '#36c57e' : '#1f8a5b', fillOpacity: 1, className: log.isWishlist ? 'wishlist-pin' : 'visited-pin'
    })
      .bindTooltip(title).on('click', () => bridge ? bridge.invokeMethodAsync('SelectLog', log.id) : currentOnSelect?.(log)).addTo(markers);
  }
}

function refreshTheme() { updateCountryStyles(); renderMarkers(); }
export function setLogs(logs, onSelect = null) {
  currentLogs = logs; currentOnSelect = onSelect;
  updateCountryStyles(); renderMarkers(); updateMapStatus();
}
export function focusLog(log) {
  selectedCountry = log.country;
  updateCountryStyles(); updateMapStatus();
  map?.panTo([log.latitude, log.longitude], { animate: true });
}
export function clearMap() {
  markers?.clearLayers(); currentLogs = []; selectedCountry = '';
  updateCountryStyles(); updateMapStatus();
}
export function dispose() {
  document.removeEventListener('ihb:themechange', refreshTheme);
  observer?.disconnect(); map?.remove(); map = null; bridge = null;
  countries = markers = observer = null; currentLogs = []; currentOnSelect = null; selectedCountry = '';
}
export function confirmAction(message) { return window.confirm(message); }
export function copyText(value) { return navigator.clipboard.writeText(value); }
export function toUtc(localDate) { return new Date(localDate).toISOString(); }
async function csrf() {
  const response = await fetch('/api/csrf', { credentials: 'same-origin', cache: 'no-store' });
  if (!response.ok) throw new Error('Your session could not be checked. Reload the page.');
  return (await response.json()).token;
}
async function read(response) {
  if (!response.ok) {
    if (response.status === 401) { window.location.assign('/login'); throw new Error('Your session expired. Please sign in.'); }
    if (response.status === 429) throw new Error('Too many requests. Try again in a minute.');
    if (response.status === 404) throw new Error('This item or invitation is no longer available.');
    let message = 'The request could not be completed.';
    try { const problem = await response.json(); message = problem.detail || problem.error || message; } catch { }
    throw new Error(message);
  }
  return response.status === 204 ? null : response.json();
}
export async function request(method, path, data = null) {
  const headers = {};
  if (method !== 'GET') headers['X-CSRF-TOKEN'] = await csrf();
  if (data !== null) headers['Content-Type'] = 'application/json';
  return read(await fetch(path, { method, headers, credentials: 'same-origin', cache: 'no-store', ...(data === null ? {} : { body: JSON.stringify(data) }) }));
}
export async function uploadFiles(inputId, logId) {
  const input = document.getElementById(inputId);
  const files = Array.from(input.files);
  try {
    const { validateFiles } = await import('/media-preflight.js');
    await validateFiles(files, await request('GET', '/api/media/usage'));
    for (const file of files) {
      const form = new FormData(); form.append('file', file);
      await read(await fetch(`/api/logs/${logId}/media`, { method: 'POST', body: form, headers: { 'X-CSRF-TOKEN': await csrf() }, credentials: 'same-origin', cache: 'no-store' }));
    }
  } finally { input.value = ''; }
}
