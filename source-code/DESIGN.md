# Design System — «нахлебная крыша»

Living styleguide: run in Development and open `/design`. Every example is rendered
with the production classes, so the page doubles as a visual reference and a
regression baseline.

## Identity

- Lowercase wordmark «нахлебная крыша» + CSS-only roof mark (borders, no SVG).
- Graphite/terracotta palette, one accent, calm paper background.
- Real photos in product screens; no decorative drawn graphics.
- Copy is minimal and informative; Russian, sentence case.

## Tokens (`wwwroot/css/site.css`, `:root`)

| Group | Tokens |
|---|---|
| Palette | `--paper --surface --ink --muted --line --accent --accent-strong --accent-soft --accent-ink --success-bg/ink --error-bg/ink --danger --warn-bg/ink --done-bg/ink` |
| Shape | `--radius-s: 8px --radius: 10px` |
| Elevation | `--shadow-card --shadow-pop` |
| Focus | `--focus-ring --focus-offset` |

Rules: never hardcode colors in components — reference tokens. Accent is the only
saturated color; success/warn/error/done exist only for statuses and feedback.

## Type scale

System stack (`Segoe UI` → system-ui). h1 clamp(1.6–2.2rem), h2 1.25rem, h3 1.05rem,
body 16px/1.5, `.listing-price` 1.05rem bold, `.price-line` 1.5rem + muted small.

## Components

- **Buttons**: `.button` (primary), `.button-secondary`, `.button-danger`,
  `.button-small`, `.link-button`; `disabled` state built in.
- **Badges**: `.badge` + `StatusBadge.For(status)` maps Listing/Request/Agreement/
  Complaint statuses to `badge--accent|warn|danger|success|done|muted`.
- **Cards**: `.card`, `.listing-card` (+ `.listing-card-media.is-empty` placeholder).
- **Forms**: `.field` + label; error input uses `.input-error`, message `.validation`;
  inputs also self-report with `:user-invalid` after interaction.
- **Alerts**: `.alert-success` / `.alert-error` inside `aria-live` region (`_Alerts`).
- **Empty states**: `_EmptyState` partial (message + optional action link).
- **Tables**: wrap in `.table-wrap[role=region][tabindex=0][aria-label]` for mobile.
- **Messages / history**: `.message`, `.message-mine`, `.message-meta`, `.history`.
- **Status pages**: friendly error page at `/Home/StatusCode` (re-executed by
  `UseStatusCodePagesWithReExecute`); `/media/*` and evidence stay empty 404.

## Accessibility floor

Single skip-link to `#content`; `:focus-visible` ring on all interactive elements;
`aria-current="page"` on active nav; tables get region semantics; reduced motion
respected; alert regions use `aria-live`.
