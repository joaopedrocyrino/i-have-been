# Map assets

- `wwwroot/assets/countries.geo.json` copied from `/Users/cyrino/code/been-web/public/assets/countries.geo.json`, containing 298 country/territory features. No personal maps, logs, images or videos were copied.
- Leaflet 1.9.4 is vendored locally from Been Web's installed dependency; its BSD-2-Clause license is included beside it.
- Leaflet attribution is retained. Base-map tiles come from OpenStreetMap; the browser sends its requested tile coordinates/IP to that provider, without media credentials or journal details. Review the [OSM tile usage policy](https://operations.osmfoundation.org/policies/tiles/) before increasing production traffic; choose a suitable commercial/self-hosted tile provider when needed.
- Boundary data reflects the existing Been UI rather than a political assertion. Original boundary-source provenance is not documented in Been Web; verify its source/license before public commercial release.
- All user content is rendered as text, never as raw HTML or Leaflet HTML popup strings.

## Interaction parity

The reference uses `CREATE_ITEM_MIN_ZOOM = 8`, country-first selection, manual zoom, and a deliberate second click for a pin. I Have Been now follows that flow. Selection changes the polygon's border; keyboard focus uses that same shape rather than its bounding box. Existing pin clicks do not bubble into map creation.

`wwwroot/theme.js` applies browser preferences before styles render and updates map colors through a theme-change event. The reusable ThemeSwitcher works on owner, invitation and account pages. Device preferences and saved light/dark choices are supported, including when browser storage is unavailable. CSS filters target the Leaflet tile pane exclusively, so journal media is unaffected.
