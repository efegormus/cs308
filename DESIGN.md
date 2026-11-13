---
version: alpha
name: Online Store
description: The CS 308 online store's design system. A light catalog UI on a pale gray page with white surfaces, one blue action color (#024ad8), Poppins for headings, buttons and the header, Open Sans for text and forms, and an unchanged dark theme. It adapts parts of an HP design analysis (docs/design/reference-hp-DESIGN.md) to the project's existing tokens; HP brand elements are not used. This file is the target. The code does not fully match it yet; see "Migration status".

# Labels used in the comments below:
#   [HP]         taken from the HP reference as is
#   [Proje]      the project's current value, kept
#   [Uyarlandı]  adapted: an HP idea fitted to the project, or a decided value that is new to the project
#   [Belirsiz]   not decided yet
# Every color key is the name of the CSS custom property without "--" (accent -> --accent).
# Light theme values only. Dark values are in the Colors section.

colors:
  accent: "#024ad8"           # [HP] primary; primary button fill, active nav underline
  accent-hover: "#0e3191"     # [HP] primary-deep; pressed and hover fill (light theme), link hover
  accent-fg: "#ffffff"        # [Proje] text on accent
  bg: "#f6f7f9"               # [Proje] page background (HP cloud #f7f7f7)
  surface: "#ffffff"          # [Proje] cards, inputs
  fg: "#222222"               # [Proje] main text (HP ink #1a1a1a)
  muted: "#505966"            # [Proje] secondary text (HP graphite #636363 / charcoal #3d3d3d)
  line: "#dce0e6"             # [Proje] card borders, header bottom line (HP hairline #e8e8e8)
  field-line: "#6b7280"       # [Proje] input border
  link: "#174a8b"             # [Proje] links (HP uses primary)
  focus: "#b66d00"            # [Proje] focus ring
  danger: "#a32b12"           # [Proje] error border and field message (HP bloom-deep #b3262b)
  danger-bg: "#fdecea"        # [Proje] error banner background
  danger-fg: "#7a1f0d"        # [Proje] error banner text
  ok: "#1a7f37"               # [Proje] success fill
  ok-fg: "#1a7f37"            # [Uyarlandı] new; success text such as "In stock" (dark #3fb950)
  header: "#ffffff"           # [Uyarlandı] white nav bar
  header-fg: "#222222"        # [Uyarlandı] = fg
  header-fg-soft: "#505966"   # [Uyarlandı] = muted; signed-in name
  header-hover: "#f6f7f9"     # [Uyarlandı] = bg; logout hover
  footer: "#1f2937"           # [Uyarlandı] new; the old light --header value
  footer-fg: "#ffffff"        # [Uyarlandı] new; the old light --header-fg value
  footer-fg-soft: "#e5e7eb"   # [Uyarlandı] new; the old light --header-fg-soft value
  sale-bg: "#b3262b"          # [Uyarlandı] new; HP bloom-deep as the discount badge fill
  sale-fg: "#ffffff"          # [Uyarlandı] new; discount badge text

typography:
  heading-lg:                 # [Uyarlandı] page title (h1)
    fontFamily: Poppins
    fontSize: 32px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0
  heading-md:                 # [Uyarlandı] section title (h2)
    fontFamily: Poppins
    fontSize: 24px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0
  heading-sm:                 # [Uyarlandı] card and sub-section title (h3)
    fontFamily: Poppins
    fontSize: 20px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0
  body-md:                    # [Uyarlandı] default text; line height [Proje]
    fontFamily: Open Sans
    fontSize: 16px
    fontWeight: 400
    lineHeight: 1.5
    letterSpacing: 0
  body-sm:                    # [Uyarlandı] secondary text, spec rows
    fontFamily: Open Sans
    fontSize: 14px
    fontWeight: 400
    lineHeight: 1.5
    letterSpacing: 0
  label:                      # [Proje] form label, error banner
    fontFamily: Open Sans
    fontSize: 14px
    fontWeight: 600
    lineHeight: 1.5
    letterSpacing: 0
  caption:                    # [Uyarlandı] stock line, fine print
    fontFamily: Open Sans
    fontSize: 12px
    fontWeight: 600
    lineHeight: 1.5
    letterSpacing: 0
  field-error:                # [Proje] 13px is outside the scale, see Open questions
    fontFamily: Open Sans
    fontSize: 13px
    fontWeight: 600
    lineHeight: 1.5
    letterSpacing: 0
  logo:                       # [Proje]
    fontFamily: Poppins
    fontSize: 23px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0
  nav-link:                   # [Proje]
    fontFamily: Poppins
    fontSize: 16px
    fontWeight: 500
    lineHeight: 1.5
    letterSpacing: 0
  button:                     # [Proje] weight and size; no uppercase, no tracking
    fontFamily: Poppins
    fontSize: 16px
    fontWeight: 500
    lineHeight: 1.2
    letterSpacing: 0
  price:                      # [Uyarlandı] 24px decided; weight [Proje] (coverflow price)
    fontFamily: Poppins
    fontSize: 24px
    fontWeight: 500
    lineHeight: 1.2
    letterSpacing: 0
  badge:                      # [Uyarlandı] 14px bold; 600 is the heaviest loaded weight
    fontFamily: Open Sans
    fontSize: 14px
    fontWeight: 600
    lineHeight: 1.2
    letterSpacing: 0

rounded:
  badge: 3px                  # [HP] sm; discount badge
  button: 4px                 # [HP] md; buttons
  field: 6px                  # [Proje] text inputs, textarea, error banner
  tile: 8px                   # [Proje] sort select, cart line, coverflow track
  card: 10px                  # [Proje] form card, coverflow card
  panel: 12px                 # [Proje] coverflow row
  product: 16px               # [HP] xl; /products product card
  full: 9999px                # [Proje] round arrow buttons

spacing:
  xxs: 4px                    # [Proje]
  label-gap: 6px              # [Proje] label to input
  xs: 8px                     # [Proje]
  sm: 12px                    # [Proje] gap between cards in a coverflow and cart lines
  md: 16px                    # [Proje] page side gutter, minimum
  field-gap: 18px             # [Proje] between form fields
  lg: 20px                    # [Proje] product grid gap, product card padding (1.25rem)
  xl: 24px                    # [Proje]
  xxl: 32px                   # [Proje] header side padding, gap between home rows
  form-card-y: 36px           # [Proje] form card padding top and bottom
  header-height: 64px         # [Uyarlandı] minimum header height
  touch-target: 44px          # [Uyarlandı] minimum touch target
  form-max: 420px             # [Proje] form card max width
  catalog-max: 1200px         # [Proje] catalog main max width
  page-max: 1280px            # [Proje] header inner and home main max width

components:
  button-primary:             # [Uyarlandı]
    backgroundColor: "{colors.accent}"
    textColor: "{colors.accent-fg}"
    typography: "{typography.button}"
    rounded: "{rounded.button}"
    padding: 0 20px
    height: 44px
  button-primary-hover:       # [Uyarlandı] light theme; dark keeps accent, see Components
    backgroundColor: "{colors.accent-hover}"
    textColor: "{colors.accent-fg}"
  button-secondary:           # [Proje] the catalog's plain button
    backgroundColor: "{colors.surface}"
    textColor: "{colors.link}"
    typography: "{typography.button}"
    rounded: "{rounded.button}"
    padding: 0 16px
    height: 44px
  text-input:                 # [Proje]
    backgroundColor: "{colors.surface}"
    textColor: "{colors.fg}"
    typography: "{typography.body-md}"
    rounded: "{rounded.field}"
    padding: 0 12px
    height: 44px
  select:                     # [Proje] home sort dropdown
    backgroundColor: "{colors.surface}"
    textColor: "{colors.fg}"
    typography: "{typography.body-sm}"
    rounded: "{rounded.tile}"
    padding: 0 12px
    height: 40px
  error-banner:               # [Proje]
    backgroundColor: "{colors.danger-bg}"
    textColor: "{colors.danger-fg}"
    typography: "{typography.label}"
    rounded: "{rounded.field}"
    padding: 12px 14px
  field-error:                # [Proje]
    textColor: "{colors.danger}"
    typography: "{typography.field-error}"
  form-card:                  # [Proje]
    backgroundColor: "{colors.surface}"
    textColor: "{colors.fg}"
    rounded: "{rounded.card}"
    padding: 36px 32px
    width: 420px
  card-product:               # [Uyarlandı] /products grid only
    backgroundColor: "{colors.surface}"
    textColor: "{colors.fg}"
    rounded: "{rounded.product}"
    padding: 20px
  card-product-title:         # [Uyarlandı]
    textColor: "{colors.fg}"
    typography: "{typography.heading-sm}"
  card-product-spec:          # [Uyarlandı]
    textColor: "{colors.muted}"
    typography: "{typography.body-sm}"
  card-product-price:         # [Uyarlandı]
    textColor: "{colors.fg}"
    typography: "{typography.price}"
  stock-in:                   # [Uyarlandı] ok-fg since SCRUM-123
    textColor: "{colors.ok-fg}"
    typography: "{typography.caption}"
  stock-out:                  # [Proje]
    textColor: "{colors.danger}"
    typography: "{typography.caption}"
  badge-sale:                 # [Uyarlandı]
    backgroundColor: "{colors.sale-bg}"
    textColor: "{colors.sale-fg}"
    typography: "{typography.badge}"
    rounded: "{rounded.badge}"
    padding: 4px 8px
  nav-bar:                    # [Uyarlandı]
    backgroundColor: "{colors.header}"
    textColor: "{colors.header-fg}"
    typography: "{typography.nav-link}"
    padding: 0 32px
    height: 64px
  nav-logo:                   # [Proje]
    textColor: "{colors.header-fg}"
    typography: "{typography.logo}"
  nav-user-name:              # [Proje]
    textColor: "{colors.header-fg-soft}"
    typography: "{typography.body-md}"
  nav-logout-hover:           # [Proje]
    backgroundColor: "{colors.header-hover}"
    textColor: "{colors.header-fg}"
  nav-link-active:            # [Uyarlandı] plus a 2px accent underline
    textColor: "{colors.header-fg}"
    typography: "{typography.nav-link}"
  footer:                     # [Uyarlandı]
    backgroundColor: "{colors.footer}"
    textColor: "{colors.footer-fg}"
    typography: "{typography.body-sm}"
    padding: 24px 16px
  footer-soft:                # [Uyarlandı]
    backgroundColor: "{colors.footer}"
    textColor: "{colors.footer-fg-soft}"
    typography: "{typography.body-sm}"
  page:                       # [Proje]
    backgroundColor: "{colors.bg}"
    textColor: "{colors.fg}"
    typography: "{typography.body-md}"
  page-muted:                 # [Proje]
    backgroundColor: "{colors.bg}"
    textColor: "{colors.muted}"
    typography: "{typography.body-sm}"
  text-link:                  # [Proje]
    textColor: "{colors.link}"
  text-link-hover:            # [Proje]
    textColor: "{colors.accent-hover}"
  card-border:                # [Proje] 1px line on surface
    backgroundColor: "{colors.line}"
  field-border:               # [Proje] 1px field-line around inputs
    backgroundColor: "{colors.field-line}"
  focus-ring:                 # [Proje] 3px outline
    backgroundColor: "{colors.focus}"
  heading-page:               # [Uyarlandı]
    textColor: "{colors.fg}"
    typography: "{typography.heading-lg}"
  heading-section:            # [Uyarlandı]
    textColor: "{colors.fg}"
    typography: "{typography.heading-md}"
  success-fill:               # [Proje] login and register success
    backgroundColor: "{colors.ok}"
    textColor: "{colors.accent-fg}"
---

# Online Store design system

This file describes how the store should look. It is the target for the React frontend in
`frontend/`. It does not change any code: the "Migration status" table at the end lists every
place where the code still differs. `docs/ui-design.md` stays the record of what is built today,
including the home page coverflow, which this file does not change.

Every section and token carries a label:

| Label | Meaning |
| --- | --- |
| [HP] | Taken from the HP reference as it is |
| [Proje] | The project's current value, kept |
| [Uyarlandı] | An HP idea fitted to the project, or a decided value that is new to the project |
| [Belirsiz] | Not decided yet. Nothing is specified until the team decides |

The HP reference is [docs/design/reference-hp-DESIGN.md](docs/design/reference-hp-DESIGN.md), an
outside analysis of HP's site, copied unchanged. "Notes on the HP reference" below lists what
this project does not take from it and the mistakes in it.

## Overview [Uyarlandı]

A clean, light catalog. The page is pale gray (`--bg`), content sits on white surfaces
(`--surface`) with thin gray borders, and one blue (`--accent`, `#024ad8`) marks the main action
on a screen. Headings, buttons and the header use Poppins; text, labels and inputs use Open Sans.
Buttons are nearly square (4px corners) while product cards are soft (16px corners). Depth comes
from borders and one light shadow, not from heavy elevation.

- The header is a white bar with a thin line under it. The footer is dark.
- The home page keeps its coverflow rows exactly as they are [Proje].
- The `/products` grid uses the HP product card layout [Uyarlandı].
- The dark theme keeps every current dark token [Proje].
- There is no separate mobile design. The same layout wraps down to 375px.

## Colors

### Light theme

| Token | Value | Label | Use |
| --- | --- | --- | --- |
| `--accent` | `#024ad8` | [HP] | Primary button fill, active nav underline, coverflow arrow hover |
| `--accent-hover` | `#0e3191` | [HP] | Pressed and hover fill of the primary button, link hover |
| `--accent-fg` | `#ffffff` | [Proje] | Text on `--accent` and `--ok` |
| `--bg` | `#f6f7f9` | [Proje] | Page background |
| `--surface` | `#ffffff` | [Proje] | Cards, inputs, form card |
| `--fg` | `#222222` | [Proje] | Main text |
| `--muted` | `#505966` | [Proje] | Secondary text, spec rows, counts |
| `--line` | `#dce0e6` | [Proje] | Card borders, header bottom line |
| `--field-line` | `#6b7280` | [Proje] | Input, textarea and select border |
| `--link` | `#174a8b` | [Proje] | Links |
| `--focus` | `#b66d00` | [Proje] | Focus ring |
| `--danger` | `#a32b12` | [Proje] | Error border, field error, out-of-stock text |
| `--danger-bg` | `#fdecea` | [Proje] | Error banner background |
| `--danger-fg` | `#7a1f0d` | [Proje] | Error banner text |
| `--ok` | `#1a7f37` | [Proje] | Success fill |
| `--ok-fg` | `#1a7f37` | [Uyarlandı] | New. Success text on `--surface`, such as "In stock" (same as `--ok` in light) |
| `--header` | `#ffffff` | [Uyarlandı] | Header bar |
| `--header-fg` | `#222222` | [Uyarlandı] | Header text, same as `--fg` |
| `--header-fg-soft` | `#505966` | [Uyarlandı] | Signed-in name, same as `--muted` |
| `--header-hover` | `#f6f7f9` | [Uyarlandı] | Logout hover, same as `--bg` |
| `--footer` | `#1f2937` | [Uyarlandı] | New. Footer background (the old light `--header`) |
| `--footer-fg` | `#ffffff` | [Uyarlandı] | New. Footer text and links |
| `--footer-fg-soft` | `#e5e7eb` | [Uyarlandı] | New. Footer secondary text |
| `--sale-bg` | `#b3262b` | [Uyarlandı] | New. Discount badge fill (HP bloom-deep) |
| `--sale-fg` | `#ffffff` | [Uyarlandı] | New. Discount badge text |

Non-color tokens that stay as they are [Proje]: `--shadow` `0 1px 3px rgba(31, 41, 55, 0.08)`,
`--btn-glow` `rgba(255, 255, 255, 0.28)`, `--ring-glow` `rgba(23, 74, 139, 0.22)`. `--ring-glow`
and the home page's `--halo` are tints of the old accent `#174a8b`; see Open questions.

### Dark theme [Proje]

The dark values stay as they are. They live in `frontend/src/styles/design-system.css` with every
other token (moved from `auth.css` in SCRUM-123). Each dark value is written twice: in the
`prefers-color-scheme: dark` block and in the `[data-theme="dark"]` block. `--header-hover` is now
written in both dark blocks too, because its light value changed and the dark theme must keep
`rgba(255, 255, 255, 0.12)`.

| Token | Dark value | Label |
| --- | --- | --- |
| `--bg` | `#0e131a` | [Proje] |
| `--surface` | `#18202c` | [Proje] |
| `--header` | `#0a0f17` | [Proje] |
| `--header-fg` | `#ffffff` | [Proje] |
| `--header-fg-soft` | `#cfd5df` | [Proje] |
| `--header-hover` | `rgba(255, 255, 255, 0.12)` | [Proje] |
| `--fg` | `#e8ebf0` | [Proje] |
| `--muted` | `#a4aebc` | [Proje] |
| `--line` | `#2a3445` | [Proje] |
| `--field-line` | `#7b8799` | [Proje] |
| `--accent` | `#2d6bcf` | [Proje] |
| `--accent-hover` | `#4a84e0` | [Proje] |
| `--accent-fg` | `#ffffff` | [Proje] |
| `--link` | `#8db9ff` | [Proje] |
| `--focus` | `#f0a64a` | [Proje] |
| `--danger` | `#ff8a73` | [Proje] |
| `--danger-bg` | `#3a1c17` | [Proje] |
| `--danger-fg` | `#ffd1c7` | [Proje] |
| `--ok` | `#238636` | [Proje] |
| `--ok-fg` | `#3fb950` | [Uyarlandı] New; the value of the home page's `--home-ok` |
| `--shadow` | `0 1px 3px rgba(0, 0, 0, 0.4)` | [Proje] |
| `--ring-glow` | `rgba(77, 132, 224, 0.35)` | [Proje] |
| `--footer` | `#0a0f17` | [Uyarlandı] New; today's dark `--header` |
| `--footer-fg` | `#ffffff` | [Uyarlandı] New |
| `--footer-fg-soft` | `#cfd5df` | [Uyarlandı] New |
| `--sale-bg`, `--sale-fg` | `#b3262b`, `#ffffff` | [Uyarlandı] New; same as light, see Open questions |

Never use `#024ad8` on a dark surface: it measures 2.64:1 on `--bg` `#0e131a`, 2.32:1 on
`--surface` `#18202c` and 2.72:1 on `--header` `#0a0f17` (2.46:1 on HP's `#1a1a1a`). The dark
theme keeps `#2d6bcf`.

The home page tokens on `.home-page` (`--home-ok`, `--card-shadow`, `--placeholder`,
`--placeholder-fg`, `--halo`, `--floor`, `--view-bg`, `--view-fg`) stay as `docs/ui-design.md`
lists them [Proje].

### Contrast

Measured with the WCAG 2 formula. Text needs at least 4.5:1, interface parts (borders that
identify a control, focus rings, indicators) at least 3:1.

Method: take each sRGB channel of the hex value as c = value / 255 and linearize it (c / 12.92
when c ≤ 0.04045, otherwise ((c + 0.055) / 1.055)^2.4). The relative luminance is
L = 0.2126 R + 0.7152 G + 0.0722 B, and the ratio is (L1 + 0.05) / (L2 + 0.05) with L1 the
lighter color. Ratios are rounded to two decimals. Example, dark `--ok-fg` `#3fb950` on dark
`--surface` `#18202c`: L = 0.3633 and 0.0141, so (0.3633 + 0.05) / (0.0141 + 0.05) = 6.45:1.

| Pair | Ratio | Need | Result |
| --- | --- | --- | --- |
| `#ffffff` on `--accent` `#024ad8` (button text) | 7.06:1 | 4.5 | Pass |
| `#ffffff` on `--accent-hover` `#0e3191` (pressed button) | 11.25:1 | 4.5 | Pass |
| `--accent` `#024ad8` on `--surface` `#ffffff` (active underline, text) | 7.06:1 | 3 / 4.5 | Pass |
| `--accent` `#024ad8` on `--bg` `#f6f7f9` | 6.59:1 | 4.5 | Pass |
| `--accent-hover` `#0e3191` on `--bg` `#f6f7f9` (link hover) | 10.49:1 | 4.5 | Pass |
| `--sale-fg` `#ffffff` on `--sale-bg` `#b3262b` (badge text) | 6.50:1 | 4.5 | Pass |
| `--sale-bg` `#b3262b` on `--bg` `#f6f7f9` (badge edge) | 6.07:1 | 3 | Pass |
| `--sale-bg` `#b3262b` on dark `--surface` `#18202c` (badge edge) | 2.52:1 | 3 | Below 3; text inside still 6.50:1 |
| `--header-fg` `#222222` on `--header` `#ffffff` | 15.91:1 | 4.5 | Pass |
| `--header-fg-soft` `#505966` on `--header` `#ffffff` | 7.09:1 | 4.5 | Pass |
| `--header-fg` `#222222` on `--header-hover` `#f6f7f9` | 14.84:1 | 4.5 | Pass |
| `--footer-fg` `#ffffff` on `--footer` `#1f2937` | 14.68:1 | 4.5 | Pass |
| `--footer-fg-soft` `#e5e7eb` on `--footer` `#1f2937` | 11.86:1 | 4.5 | Pass |
| `--focus` `#b66d00` on `--footer` `#1f2937` | 3.62:1 | 3 | Pass |
| Dark `--footer-fg` `#ffffff` on dark `--footer` `#0a0f17` | 19.20:1 | 4.5 | Pass |
| Dark `--footer-fg-soft` `#cfd5df` on `#0a0f17` | 13.02:1 | 4.5 | Pass |
| Dark `--focus` `#f0a64a` on `#0a0f17` | 9.38:1 | 3 | Pass |
| Dark `--accent` `#2d6bcf` on dark `--header` `#0a0f17` (active underline) | 3.76:1 | 3 | Pass |
| `#ffffff` on dark `--accent` `#2d6bcf` (button text) | 5.10:1 | 4.5 | Pass |
| `#ffffff` on dark `--accent-hover` `#4a84e0` | 3.70:1 | 4.5 | Fail, so never a button fill |
| `--focus` `#b66d00` on `--surface` `#ffffff` / `--bg` `#f6f7f9` | 4.06:1 / 3.78:1 | 3 | Pass |
| `--focus` `#b66d00` on `--accent` `#024ad8` | 1.74:1 | 3 | Fail without offset; keep the 2px offset |
| `--field-line` `#6b7280` on `--surface` `#ffffff` | 4.83:1 | 3 | Pass |
| `--fg` `#222222` on `--surface` / `--bg` | 15.91:1 / 14.84:1 | 4.5 | Pass |
| `--muted` `#505966` on `--surface` / `--bg` | 7.09:1 / 6.61:1 | 4.5 | Pass |
| `--link` `#174a8b` on `--surface` / `--bg` | 8.79:1 / 8.20:1 | 4.5 | Pass |
| `--danger` `#a32b12` on `--surface` / `--danger-bg` | 7.21:1 / 6.31:1 | 4.5 | Pass |
| `--danger-fg` `#7a1f0d` on `--danger-bg` `#fdecea` | 9.05:1 | 4.5 | Pass |
| `--ok` `#1a7f37` on `--surface`; `#ffffff` on `--ok` | 5.08:1 | 4.5 | Pass |
| `--ok-fg` `#1a7f37` on `--surface` `#ffffff` / `--bg` `#f6f7f9` (in-stock text) | 5.08:1 / 4.74:1 | 4.5 | Pass |
| Dark `--ok-fg` `#3fb950` on dark `--surface` `#18202c` / `--bg` `#0e131a` | 6.45:1 / 7.34:1 | 4.5 | Pass |
| Dark `--ok` `#238636` on dark `--surface` `#18202c` | 3.54:1 | 4.5 | Fail for text, so `--ok` stays a fill and text uses `--ok-fg` |
| Dark `--danger` `#ff8a73` on dark `--surface` `#18202c` (out-of-stock text) | 7.13:1 | 4.5 | Pass |
| Dark `--fg` `#e8ebf0` / `--muted` `#a4aebc` on dark `--surface` `#18202c` (card name, spec row) | 13.71:1 / 7.30:1 | 4.5 | Pass |
| Dark `--link` `#8db9ff` on dark `--surface` `#18202c` ("View cart") | 8.20:1 | 4.5 | Pass |
| Dark `--focus` `#f0a64a` on dark `--surface` `#18202c` | 8.00:1 | 3 | Pass |
| `--line` `#dce0e6` on `--surface` `#ffffff` | 1.33:1 | — | Decorative only; never the only edge of a control |

## Typography

### Fonts [Proje]

The fonts come from the Google Fonts link in `frontend/index.html`, which loads Poppins 500 and
600 and Open Sans 400 and 600. Use only those weights.

| Use | Font | Fallback | Weights |
| --- | --- | --- | --- |
| Headings, buttons, header, logo, prices | Poppins | Arial, sans-serif | 500, 600 |
| Text, labels, inputs, badges | Open Sans | Arial, sans-serif | 400, 600 |

HP's font (Forma DJR Micro) is not used. Manrope could be considered later as a single family;
nothing is planned.

### Scale [Uyarlandı]

| Token | Font | Size | Weight | Line height | Use |
| --- | --- | --- | --- | --- | --- |
| `heading-lg` | Poppins | 32px | 600 | 1.2 | Page title (`h1`) |
| `heading-md` | Poppins | 24px | 600 | 1.2 | Section title (`h2`) |
| `heading-sm` | Poppins | 20px | 600 | 1.2 | Card and sub-section title (`h3`) |
| `body-md` | Open Sans | 16px | 400 | 1.5 | Default text, input text |
| `body-sm` | Open Sans | 14px | 400 | 1.5 | Secondary text, spec rows, footer |
| `label` | Open Sans | 14px | 600 | 1.5 | Form label, error banner [Proje] |
| `caption` | Open Sans | 12px | 600 | 1.5 | Stock line, fine print |
| `field-error` | Open Sans | 13px | 600 | 1.5 | Field error message [Proje] |
| `logo` | Poppins | 23px | 600 | 1.2 | Header logo [Proje] |
| `nav-link` | Poppins | 16px | 500 | 1.5 | Header links [Proje] |
| `button` | Poppins | 16px | 500 | 1.2 | Button labels [Proje] |
| `price` | Poppins | 24px | 500 | 1.2 | Product card and detail price |
| `badge` | Open Sans | 14px | 600 | 1.2 | Discount badge |

- Headings: 32 / 24 / 20. Text: 16 / 14 / 12. Price: 24.
- A heading that runs over more than one line uses line height 1.2.
- Weights stay as the project uses them today: 600 for headings and the logo, 500 for links,
  buttons and prices, 400 for text, 600 for labels and emphasis.
- Button labels are never uppercase and have no letter spacing.
- The home coverflow keeps its own sizes (32px panel title, 18px row heading, 16px card name,
  18px card price, 15px text) [Proje].

## Layout

### Containers [Proje]

| Area | Max width | Padding |
| --- | --- | --- |
| Header inner, home `main` | 1280px | 32px sides in the header; at least 16px sides on the page |
| Catalog `main` | 1200px | 40px 24px (32px 16px up to 480px) |
| Purchase layout | 920px | `clamp(16px, 3vw, 32px)` in the card |
| Form card (login, register) | 420px | 36px 32px (28px 20px up to 480px) |

HP's 1366px container is not used.

### Spacing [Proje]

The project's spacing stays: 4, 6, 8, 12, 16, 18, 20, 24, 32 and 36px, as the tokens in the front
matter list them. Form fields sit 18px apart, with 6px from label to input. The product grid gap
and product card padding are 20px. Home rows are 32px apart. HP's 80px section gap is not used.

### Page frame [Proje]

Header, then `main` taking the remaining height, then the footer (`.app-shell` in `layout.css`).
Each page has a wrapper class, and its CSS starts every selector with it, because all CSS files
load at once.

## Elevation [Uyarlandı]

| Level | Treatment | Use | Label |
| --- | --- | --- | --- |
| 0, flat | No border, no shadow | Page background, header, footer | [Proje] |
| 1, outline | 1px `--line` border | Cart lines, order cards, coverflow cards, request states | [Proje] |
| 2, card | 1px `--line` border and `--shadow` `0 1px 3px rgba(31, 41, 55, 0.08)` | Form card, coverflow row | [Proje] |
| 2, product card | `0 2px 8px rgba(26, 26, 26, 0.08)` | `/products` product card | [HP] |
| 3, front card | `--card-shadow` `0 22px 44px rgba(31, 41, 55, 0.18)` | Coverflow front card | [Proje] |
| 3, menu | `--shadow-menu` `0 10px 28px rgba(31, 41, 55, 0.18)`; dark `0 10px 28px rgba(0, 0, 0, 0.55)` | Header account menu, category mega menu panel | [Uyarlandı] |

The product card shadow is the token `--shadow-product`. It has no dark value yet, so the dark
theme uses the same value; see Open questions. HP's modal level
(`0 8px 24px`) has no use in the store yet.

## Shapes

| Token | Value | Use | Label |
| --- | --- | --- | --- |
| `rounded.badge` | 3px | Discount badge | [HP] |
| `rounded.button` | 4px | All buttons except the round arrows | [HP] |
| `rounded.field` | 6px | Text inputs, textarea, error banner | [Proje] |
| `rounded.tile` | 8px | Sort select, cart line, coverflow track | [Proje] |
| `rounded.card` | 10px | Form card, coverflow card | [Proje] |
| `rounded.panel` | 12px | Coverflow row | [Proje] |
| `rounded.product` | 16px | `/products` product card | [HP] |
| `rounded.full` | 9999px | Coverflow arrows | [Proje] |

Buttons stay nearly square, product cards stay soft. That split comes from HP.

## Components

### Buttons [Uyarlandı]

**Primary button.** Height 44px, 4px corners, `--accent` fill, `--accent-fg` text, Poppins 16px
500, no uppercase. Forms keep it full width.

- Hover and pressed in the light theme: fill `--accent-hover` (`#0e3191`).
- Hover and pressed in the dark theme: the fill stays `--accent` (`#2d6bcf`), because white on
  the dark `--accent-hover` is 3.70:1. Hover shows the current `--ring-glow` ring [Proje].
- Disabled: opacity 0.7 and the matching cursor [Proje].
- Focus: 3px `--focus` outline with a 2px offset [Proje].
- Loading and success follow `docs/ui-design.md` (spinner, then the `--ok` fill and a check)
  [Proje].

**Secondary button.** Height 44px, 4px corners, `--surface` fill, `--link` text [Proje colors].
Its border color is an open question.

### Inputs and forms [Proje]

- **Text input:** height 44px, 6px corners, 1px `--field-line` (`#6b7280`) border, 16px Open Sans
  text, padding 0 12px. Always has a `<label>`.
- **Focus:** 3px `--focus` outline with a 2px offset. HP's light gray border and its focus without
  a ring are not used.
- **Textarea:** same rules, at least 88px high, padding 10px 12px.
- **Select:** 40px high, 8px corners, 1px `--field-line` border, 14px text.
- Vertical gap between fields 18px, label to input 6px.

### Validation errors [Proje]

- The message sits below the field in `--danger`, 13px, weight 600.
- The input gets `aria-invalid="true"`, a 2px `--danger` border and `aria-describedby` pointing to
  the message.
- The first invalid field gets focus. An invalid field shakes once (0.45s).
- A form-level error is a banner: `role="alert"`, `--danger-bg` background, 1px `--danger` border,
  `--danger-fg` text, 14px, weight 600, 6px corners.

### Cart and checkout forms [Proje]

From `cart.css`, `purchases.css` and `docs/ui-design.md`:

- A cart line is a grid of image (72px, 56px below 576px), product info, line total and remove
  button, on `--surface` with a 1px `--line` border and 8px corners. Lines sit 12px apart.
- Quantity uses minus and plus buttons around an `<output>`. A line with a stock problem shows its
  warning in `--danger-fg`.
- The total row puts the label and amount on opposite sides. The checkout link is a primary
  button.
- Checkout fields sit in two columns, one column up to 480px; the address field spans both.
- The payment section collects the mock card details (requirement 15). The full card number and
  CVV must not be kept (`docs/project-requirements.md`).

### Header [Uyarlandı]

The first draft put the categories in the middle of one bar. Ten category names, the logo, the
search and the account area do not fit on one line at 1280px, so the header has two rows at every
width (decided in SCRUM-123):

- White bar (`--header`), at least 64px high, inner width up to 1280px with 32px side padding
  (16px up to 480px), and a 1px `--line` line at the bottom.
- Row 1: the logo on the left (Poppins 23px 600, `--header-fg`), then on the right the search, the
  account area and the cart.
- Row 2: the category bar: "All products", then every top-level category from
  `GET /api/categories` (`parentId` null), in the API's order.
- Up to 768px: row 1 holds the logo, the account area and the cart; the search takes the full
  width under it; the category bar wraps onto as many lines as it needs (four at 375px).
- **Search:** always open, a 200px `Input` (44px, hidden label "Search products") and a 44px
  primary `Button` with a search icon. It opens `/products?search=<text>`, the same name as the API
  parameter, and shows the current search on that page. At most 100 characters, as in the API.
- **Account area:** Login for a guest; for a signed-in visitor the 44px profile icon with its
  shortcut menu (SCRUM-130) and Logout (44px, 1px `--header-fg` border, 4px corners). A product
  manager also gets a Deliveries link. Home is the logo and Checkout starts from the cart, so
  neither is a header link.
- **Cart:** a 44px icon link to `/cart` with the item count in an `--accent` bubble (hidden at 0;
  the link's label says the count). Only guests and customers see it: the cart API answers 403 to
  staff. The count reloads on every page change, login, logout and cart change.
- **Mega menu:** a top-level category with subcategories is a button with a chevron and
  `aria-expanded`; it opens a full-width panel under the header listing "All <category>" and
  every category below it. A top-level category without subcategories is a plain link. All links
  go to `/products#category-<id>`.
  - Mouse: hovering opens the panel, leaving closes it. Touch: a tap opens it, a second tap or a
    tap outside closes it.
  - Keyboard: Enter, Space or Down arrow opens it (Down also moves into it); Tab alone never opens
    a panel. Up and Down move between the panel's links; Escape closes it and returns focus to the
    button; tabbing out closes it.
  - Without `parentId` (an older server, a test fixture) every category is a plain link.
- The current page's link has a 2px `--accent` line under it (an inset shadow, so the height does
  not change) and keeps `aria-current="page"`. On `/products#category-<id>` the category's
  top-level item is marked.
- Every link and button in the header is at least 44px high.
- No strip above the header.
- No hamburger menu or drawer. At 375px there is no sideways scroll; the browser check in CI
  enforces this.
- Dark theme: today's dark header tokens [Proje].

### Footer [Uyarlandı]

- Dark bar: `--footer` background, `--footer-fg` text and links, `--footer-fg-soft` secondary
  text, 14px Open Sans, padding 24px 16px.
- Links in the footer use `--footer-fg` with an underline, not `--link`, which is too dark on
  `--footer`. Each link is at least 44px high.
- Content: the store name (`heading-sm`), the links Products, Cart and Orders (Cart and Orders only
  for guests and customers; a guest is sent to login for Orders), and the copyright line in
  `--footer-fg-soft`.
- No support link until the customer chat (requirement 13) exists: `/support` is the agents'
  panel.
- No 5-column link grid or help band from HP.

### Home page [Proje]

The coverflow stays as it is: layout, geometry, motion, tokens and rules in `docs/ui-design.md`
and `home.css`. HP's hero, chevrons and category icon cards are not used on the home page.

### Product card on `/products` [Uyarlandı]

Top to bottom:

1. Image, 1:1, `object-fit: contain`, on `--bg` with 8px corners (`rounded.tile`), with the
   "No image available" placeholder when it is missing or fails [Proje].
2. Title, `heading-sm` (Poppins 20px 600), linking to `/product?id=<id>`.
3. One spec row, `body-sm` in `--muted`: the brand, or else the first line of the description.
   It stays on one line and ends with "…" when it is too long. No row when there is neither.
4. Price, `price` (Poppins 24px 500).
5. Stock line, `caption`: "In stock" or "Out of stock", without the count (see Stock).
6. "Add to cart", the shared primary `Button`, full width and 44px high, pinned to the bottom
   of the card. It adds one unit. Its description (`aria-describedby`) is the product name.

The card is the shared `Card`: `--surface`, 16px corners, shadow `0 2px 8px rgba(26, 26, 26,
0.08)`, padding 20px [Proje]. Cards in a row have equal heights, and the price, stock line and
button line up at the bottom. The grid has 4 columns, 3 up to 1024px, 2 up to 768px and 1 up to
480px, with a 20px gap.

- Out-of-stock products are still shown and still found by search (requirement 7), but their
  card offers no add-to-cart action: the button is disabled and reads "Out of stock", as on the
  product page. A disabled button keeps the cards' bottoms aligned and says why nothing can be
  added; the server checks the stock again.
- Staff accounts see no button, because the cart API refuses them (the header hides their cart
  too).
- After an add, a `role="status"` line "Added to your cart. View cart" appears above the button,
  and the header's cart count follows. A failure shows a `role="alert"` line in `--danger` with
  the product page's messages. A refused CSRF token is renewed and the add tried once more.
- The button sits in an actions row, so a second action (a wishlist button, for example) can go
  next to it later.
- No discount badge yet: the product API has no discount field (see Discount badge).

### Discount badge [Uyarlandı]

`--sale-bg` (`#b3262b`) fill, `--sale-fg` (`#ffffff`) text, 6.50:1. 3px corners, padding 4px 8px,
Open Sans 14px 600. It shows the discount (for example "−20%") next to the price or on the image
corner. Discounts are set by sales managers (requirement 11). The product API returns no
discount yet, so no page shows the badge.

### Stock and out of stock [Proje]

- Every product shows its stock (requirement 3).
- In stock: `--ok-fg` text ("In stock"). Out of stock: `--danger` text ("Out of stock").
- `--ok-fg` exists because in the dark theme `--ok` (`#238636`) is too dim for 12px text
  (3.54:1 on `--surface`). `--ok` stays the success fill.
- The product card shows the status only; the product page shows the count ("In stock · 12
  available").
- The home coverflow still uses its own `--home-ok`, which has the same values as `--ok-fg`.
- Out-of-stock products are listed and searchable but cannot be added to the cart; the server
  checks stock as well (requirement 7).

### Success [Proje]

`--ok` is a fill for success, as on the login and register buttons: the button fills with `--ok`
left to right, then shows a check and "Signed in". There is no separate welcome screen.

### Loading, empty and error states [Proje]

From `components/RequestStates.js` and `styles/request-states.css`:

| State | Rule |
| --- | --- |
| Loading | `role="status"` and a short label, such as "Loading your cart…" |
| Error | `role="alert"`, a safe message from `normalizeRequestError`, and "Try again" when the page passes `onRetry`. `--danger`, `--danger-bg`, `--danger-fg` |
| Empty | Plain text, such as "Your cart is empty." with a link to the products |
| Box | 1px `--line` border, `--surface`, padding 14px 16px, line height 1.5 |

Loading, error and empty coverflow rows keep the height of a row with cards, so the page does not
jump.

### Order status badges [Belirsiz]

Processing, in-transit and delivered (requirement 3). Decision pending. (`orders.css` has an
interim style today; it is not a design decision.)

### Rating stars and reviews [Belirsiz]

Requirement 5. Decision pending.

### Wishlist [Belirsiz]

Requirements 11 and 14. Decision pending.

### Admin panel [Uyarlandı]

The product and sales managers' panel at `/manage` (SCRUM-124; requirements 8, 11 and 12). Charts are
still [Belirsiz]: the sales overview has no revenue chart yet.

- **Frame.** From 768px up, a 240px menu on the left (`--surface`, 1px `--line`, 8px corners) and the
  content on the right. Page titles `heading-lg` (Poppins), text `body-md` (Open Sans). Inner width up to
  1280px.
- **Menu.** Product manager: Overview, Deliveries, Reviews, Low stock. Sales manager: Overview, Invoices,
  Discounts. Every item is 44px high. Hover shows a 3px `--accent` line on the left (an inset shadow, 200ms).
  The current page keeps `aria-current="page"`, the same line and a light `--accent` tint,
  `color-mix(in srgb, var(--accent) 10%, var(--surface))` (light `#e6edfb`, dark `#1a283c`). The tint is
  mixed from tokens, not a new token.
- **Below 768px** the menu is a disclosure at the top: a 44px "Menu" button with `aria-expanded` that shows
  the current section and opens the same list under it; choosing an item or Escape closes it. This is the
  one exception to "no hamburger drawer": it is not a drawer and does not cover the page, like the
  header's mega menu.
- **KPI cards.** `Card` with a 1px `--line` border, `--shadow` and 10px corners; label in `label`, value in
  `heading-md` with tabular numbers, tr-TR format (amounts in TRY). 4 columns, 2 up to 1024px, 1 up to
  480px.
- **Table.** The shared `Table` (`components/ui/Table.js`): an outline frame (1px `--line`, `--surface`,
  8px corners), a `label` caption, `th scope="col"` headers in `--muted`, a `th scope="row"` first column,
  `body-sm` cells, numbers right-aligned. A wide table scrolls sideways inside its frame, never the page.
  Loading shows skeleton rows in a `role="status"` box with an `aria-label` (and the same text for screen
  readers); empty shows its text in a `role="status"` box.
- **Review approval.** "Approve" is the shared `Button`, 110px wide. After the approval call succeeds it
  turns into a 44px `--ok` circle with a white tick and `aria-label` "Review approved", waits, then the row
  fades and closes; the list is `aria-live="polite"` and announces it. A failure brings the button back and
  shows a `--danger` message under the row. The fill is `--ok`, not `--ok-fg`: white on the dark `--ok-fg`
  is 2.54:1.

| Pair | Ratio | Need | Result |
| --- | --- | --- | --- |
| `--fg` `#222222` on the light tint `#e6edfb` | 13.54:1 | 4.5 | Pass |
| `--accent` `#024ad8` line on the light tint | 6.01:1 | 3 | Pass |
| `--focus` `#b66d00` on the light tint | 3.45:1 | 3 | Pass |
| Dark `--fg` `#e8ebf0` on the dark tint `#1a283c` | 12.44:1 | 4.5 | Pass |
| Dark `--accent` `#2d6bcf` line on the dark tint / on dark `--surface` | 2.91:1 / 3.21:1 | 3 | Below 3 on the tint; the tint and `aria-current` mark the item too |
| Dark `--focus` `#f0a64a` on the dark tint | 7.26:1 | 3 | Pass |
| `#ffffff` tick on `--ok` `#1a7f37` / dark `#238636` | 5.08:1 / 4.63:1 | 3 | Pass |

Motion is listed under "Motion and reduced motion".

### Customer profile [Uyarlandı]

The customer's `/profile` page (SCRUM-103), where name, tax ID and home address are corrected. It is built
from the login and register parts (`.auth-page`, `useAuthForm`) and keeps their sizes for now: a 28px title and a
48px button with 6px corners, like login, not the `heading-lg` and 44px targets (see Migration status).

- **Layout.** One column up to 420px (`--form-max`): the title, one line of `--muted` text, then two form cards in
  one form, "Account" and "Invoice and delivery", 24px apart, and one full-width "Save changes" button under them.
  Each card is the form card (`--surface`, 1px `--line`, `--shadow`, 10px corners, padding 36px 32px; 28px 20px up
  to 480px) with a `heading-sm` title.
- **Email.** Shown read-only for reference: `--bg` fill and `--muted` text in the normal input frame, with the note
  "Email cannot be changed here." in `body-sm` `--muted` under it.
- **Incomplete-profile note.** A legacy account without a tax ID or home address sees "Add your tax ID and home
  address before you pay." above the cards. It is a note, not an error: `role="status"`, `--surface` fill, 1px
  `--line` border, a 3px `--accent` line on the left (an inset shadow, as in the admin menu), `--fg` text in
  `label`, 6px corners. No new token. Errors keep the red error banner.
- **After a save** the button shows the `--ok` fill and "Saved", then returns to "Save changes" (see Motion), and a
  `role="status"` line "Your changes are saved." appears under it, with "Continue to checkout" when the page was
  opened from checkout.

| Pair | Ratio | Need | Result |
| --- | --- | --- | --- |
| `--fg` `#222222` on `--surface` `#ffffff` (note text) | 15.91:1 | 4.5 | Pass |
| `--accent` `#024ad8` line on `--surface` `#ffffff` | 7.06:1 | 3 | Pass |
| Dark `--fg` `#e8ebf0` on dark `--surface` `#18202c` | 13.71:1 | 4.5 | Pass |
| Dark `--accent` `#2d6bcf` line on dark `--surface` `#18202c` | 3.21:1 | 3 | Pass |
| `--muted` `#505966` on `--bg` `#f6f7f9` (read-only email) / dark `#a4aebc` on `#0e131a` | 6.61:1 / 8.31:1 | 4.5 | Pass |
| `--field-line` `#6b7280` on `--bg` `#f6f7f9` (read-only frame) / dark `#7b8799` on `#0e131a` | 4.51:1 / 5.12:1 | 3 | Pass |

### Live chat [Belirsiz]

Support chat for guests and customers (requirement 13). Decision pending.

### Toast [Belirsiz]

Decision pending.

## Motion and reduced motion [Proje]

- One curve for entrances: `cubic-bezier(0.22, 1, 0.36, 1)`. Keyframes are prefixed with the page
  (`auth-`, `home-`), because they cannot be scoped.
- Login and register: masked reveal-up, shake on an invalid field, button spotlight, spinner, and
  the success fill. Values in `docs/ui-design.md`.
- Home: row and card entrances, coverflow transitions and the VIEW cursor. Values in
  `docs/ui-design.md`.
- Admin panel (SCRUM-124): section content enters 8px from above (220ms), KPI cards rise one after another
  (220ms, 80ms apart) and count from 0 (700ms, easeOutCubic), skeleton rows shimmer (1.2s), and the
  review approval runs 200ms (button to circle), 120ms + 250ms (tick), 800ms (hold) and 250ms (close).
  The values live in `frontend/src/lib/motion.js`; keyframes start with `admin-`.
- Customer profile (SCRUM-103): the login and register motion (masked reveal of the title, text, note, every field
  and the button; shake; spotlight; spinner; the 0.95s `--ok` fill and the check), plus the two cards rising 24px
  in 0.85s on the same curve, 140ms apart (`profile-card-in`). After a save the check and "Saved" stay about 1.8s,
  then the button reads "Save changes" again.
- No animation library.
- `prefers-reduced-motion: reduce` turns these off and shows the final state at once. The reveal
  masks must come off in that mode too, or they clip the focus ring. On the home page side cards
  scale instead of turning and the VIEW circle sits on the pointer. In the admin panel KPI values
  show their final value at once and an approved review's tick and green show at once; the row still
  waits 800ms, then goes without a fade. On the profile page the cards do not rise, and after a save
  the check and "Saved" show at once without the fill.

## Accessibility patterns [Proje]

- Every input has a `<label>`. Errors use `aria-invalid` and `aria-describedby`, and the first
  invalid field gets focus.
- Every focusable element shows a 3px `--focus` outline with a 2px to 4px offset.
- Status messages use `role="status"`, errors `role="alert"`.
- The current page's nav link has `aria-current="page"`.
- The category mega menu follows the disclosure pattern: buttons with `aria-expanded` and
  `aria-controls`, opened by Enter, Space or Down arrow, closed by Escape (see Header).
- Every page has a "Skip to content" link (`Layout.jsx`) that appears on focus.
- Images have `alt` text; a missing image shows a text placeholder.
- Text contrast is at least 4.5:1 and control contrast at least 3:1 (see Contrast).
- Touch targets are at least 44px.
- The coverflow is fully usable with the keyboard; only the front card of a row is a Tab stop.
- No sideways scroll at 375px.

## Do's and Don'ts

### Do

- Use the tokens; never write a color directly in a rule [Proje].
- Keep `--accent` for the main action on a screen and the active nav underline [Uyarlandı].
- Use 4px corners on buttons and 16px on product cards [HP].
- Keep form fields at 44px with a visible `--field-line` border and the 3px focus ring [Proje].
- Change a dark value in both dark blocks [Proje].
- Check contrast for any new color pair and write the ratio here [Proje].

### Don't

- Don't use `#024ad8` on a dark surface [Uyarlandı].
- Don't uppercase button labels [Uyarlandı].
- Don't use HP brand elements: Forma DJR Micro, the chevron decoration, the HP logo rule, the
  storm colors, the printer plan cards, the "How can we help?" band or the strip above the header.
- Don't change the coverflow's geometry or motion here; `docs/ui-design.md` owns it [Proje].
- Don't add a hamburger drawer or a separate mobile layout [Uyarlandı]. The admin panel's menu
  disclosure below 768px is the one exception (see Admin panel).
- Don't load more font weights than `index.html` loads [Proje].

## Responsive

| Breakpoint | Width | Label |
| --- | --- | --- |
| Small phone | up to 480px | [Uyarlandı] |
| Phone | 481px to 768px | [Uyarlandı] |
| Tablet | 769px to 1024px | [Uyarlandı] |
| Desktop | 1025px to 1280px | [Uyarlandı] |
| Wide | above 1280px | [Uyarlandı] |

- Write media queries at 480, 768, 1024 and 1280px.
- Touch targets are at least 44 × 44px.
- No mobile-specific design: the header wraps, grids reflow, and there is no sideways scroll at
  375px. The product grid steps from 4 columns to 3 (up to 1024px), 2 (up to 768px) and 1 (up
  to 480px); other grids use `auto-fit`.
- Existing max widths stay (see Layout) [Proje].
- Thresholds that belong to a component stay as they are: the coverflow's 700px track width and
  733px status height [Proje]. Other current thresholds are in "Migration status".

## Iteration guide

1. Change one component at a time.
2. Name tokens and components as this file does (`--accent`, `card-product`); don't paraphrase
   them into raw hex or px in code. In CSS, a color key is `--<key>`, a typography key
   `--type-<key>` (for the `font` shorthand), a spacing key `--space-<key>` (except
   `--header-height`, `--touch-target` and the three max widths, which keep their names) and a
   rounded key `--radius-<key>`.
3. A new token goes on `:root` in `frontend/src/styles/design-system.css`; a new color also gets
   its dark value in both dark blocks there. Then it goes into this file with its label and, for a
   color, its contrast ratio.
4. When a [Belirsiz] item is decided, replace its "Decision pending" note and remove it from Open
   questions.
5. When a row of "Migration status" is done, update its current value in the same PR.
6. Run `npx --yes @google/design.md lint DESIGN.md` after editing this file.
7. Keep `docs/ui-design.md` in step: it describes what is built, this file what is wanted.

## Notes on the HP reference

Not taken from it: Forma DJR Micro, the chevron decoration, the HP logo rule, the storm colors,
the printer plan and pricing tier cards, the "How can we help?" band, the strip above the header,
the hamburger drawer, the uppercase button labels, the light gray input border with no focus ring,
the hero and category icon cards, and the 1366px container.

Mistakes in it, corrected here:

- Its breakpoint table speaks of a "hero serif". HP's system has no serif; every face is Forma
  DJR Micro.
- It calls the dark bands "dark navy". The color is `ink` `#1a1a1a`, a near-black with no blue.
- It credits Forma DJR Micro to "Commercial Type / Mark Caneso". The typeface is by David
  Jonathan Ross (DJR).
- It calls button labels both "capitalize" and "uppercase".
- It speaks of "six pre-defined surface modes" and lists four.
- It calls the 40px search field a pill but gives it 4px corners.

## Open questions

1. **Product card details.** Decided in SCRUM-123: "Add to cart" (disabled "Out of stock" when
   there is none), the spec row shows the brand or the first line of the description, and the
   padding stays 20px [Proje] (24px in HP). Model and warranty can join the spec row when the
   list API returns them. The number stays so the numbers below do not change.
2. **Input size.** Inputs stay 44px high with 6px corners [Proje]; HP uses 4px corners, the same
   as buttons. Purchase inputs (padding 12px, 4.8px corners) and the 40px sort select differ too.
3. **`--danger` and `--sale-bg`.** Keep two reds (`#a32b12` for errors, `#b3262b` for discounts)
   or merge them into one.
4. **`--link`.** Stays `#174a8b` [Proje]; HP uses the primary color for links. Changing it to
   `#024ad8` would be 7.06:1 on white.
5. **Tints of the old accent.** `--ring-glow` (`rgba(23, 74, 139, 0.22)`) and `--halo`
   (`rgba(23, 74, 139, 0.13)`) are built from `#174a8b`. Retint them to `#024ad8` or keep them.
6. **Neutral colors from HP.** `--fg` `#222222` vs HP ink `#1a1a1a`, `--bg` `#f6f7f9` vs HP cloud
   `#f7f7f7`, `--line` `#dce0e6` vs HP hairline `#e8e8e8`, `--muted` `#505966` vs HP graphite
   `#636363` / charcoal `#3d3d3d`. All kept [Proje].
7. **Dark theme for new parts.** The product card shadow has no dark value: on the dark `--bg`
   it is barely visible, and the card stands out by its `--surface` fill alone (SCRUM-123
   screenshots). The discount badge uses the light colors and its edge is 2.52:1 on the dark
   surface.
8. **Dark theme button hover.** The fill stays `--accent` and the ring shows hover. A separate
   pressed token for dark could come later.
9. **Secondary button border.** Today a hard-coded `#b9c4d2`; HP uses a 1px primary border.
10. **Sizes outside the scale.** Field errors are 13px and home text 15px [Proje]; the scale is
    16 / 14 / 12.
11. **Badge weight.** "Bold" is written as 600, the heaviest loaded weight. 700 would need another
    font weight in `index.html`.
12. **Body line height.** 1.5 [Proje]; the login lead uses 1.55, the catalog 1.6, HP 1.38.
13. **Breakpoints in the code.** The cart switches at 576px (36rem) and the product detail at
    700px. Move them to 480 / 768 or keep them.
14. **Touch targets under 44px.** Cart quantity buttons (36px) and the sort select (40px). Logout
    and the profile icon are 44px since SCRUM-123.
15. **Header search and mega menu.** Decided in SCRUM-123: two header rows, a 200px search field,
    and a full-width panel per top-level category (see Header). The number stays so the numbers
    below do not change.
16. **Manrope.** Could be considered later; nothing planned.
17. **Disabled button look.** A disabled `Button` is the `--accent` fill at opacity 0.7, so the
    "Out of stock" button on a card still looks blue. A gray disabled fill would read more
    clearly as unavailable; it needs a token and a contrast check.

## Migration status

Target is this file. "Current" is what the code has on this branch. SCRUM-123 moved every token to
`frontend/src/styles/design-system.css`, set the light tokens to their target values and added the
type scale (`--type-*`), spacing (`--space-*`) and corner (`--radius-*`) tokens and the shared
`Button`, `Input`, `Card` and `Badge` in `frontend/src/components/ui/`. Step 2 rebuilt the header
and footer on those tokens; the header search uses `Button` and `Input`. Step 3 moved the
`/products` product card to `Card`, `Button` and the type, spacing and corner tokens, and
`products.css` (product list, product page, wishlist) to the color tokens, so those pages follow
the dark theme too.

### Colors

| Token | Target (light) | Current (light) | File |
| --- | --- | --- | --- |
| `--accent` | `#024ad8` | `#024ad8`; hard-coded `#174a8b` in purchase rules; fallbacks `#174a8b`, `#1d4ed8` | `design-system.css`; `purchases.css`; `cart.css`, `orders.css`, `add-to-cart.css` |
| `--accent-hover` | `#0e3191` | `#0e3191` | `design-system.css` |
| `--accent-fg` | `#ffffff` | `#ffffff`; hard-coded `white`/`#fff` | `design-system.css`; `purchases.css`, `add-to-cart.css` |
| `--bg` | `#f6f7f9` | `#f6f7f9` | `design-system.css`, `products.css` |
| `--surface` | `#ffffff` | `#ffffff`; hard-coded `white` | `design-system.css`, `products.css`; `purchases.css` |
| `--fg` | `#222222` | `#222222` | `design-system.css`, `products.css` |
| `--muted` | `#505966` | `#505966`; fallbacks `#5d6877` | `design-system.css`, `products.css`; `cart.css`, `orders.css` |
| `--line` | `#dce0e6` | `#dce0e6`; hard-coded `#dce0e6`, `#e3e7eb` | `design-system.css`, `products.css`; `purchases.css` |
| `--field-line` | `#6b7280` | `#6b7280`; hard-coded `#9ba9b9` (purchase inputs), `#b9c4d2` (catalog buttons) | `design-system.css`; `purchases.css`, `products.css` |
| `--link` | `#174a8b` | `#174a8b` | `design-system.css`, `products.css` |
| `--focus` | `#b66d00` | `#b66d00`; hard-coded in purchase rules | `design-system.css`, `products.css`; `purchases.css` |
| `--danger` | `#a32b12` | `#a32b12`; hard-coded `#7c231d`; fallback `#b00020` | `design-system.css`, `products.css`; `purchases.css`; `orders.css`, `add-to-cart.css` |
| `--ok` | `#1a7f37` | `#1a7f37` | `design-system.css` |
| `--ok-fg` | `#1a7f37` (dark `#3fb950`) | Matches; in-stock text on the product list, product page and wishlist | `design-system.css`, `products.css` |
| `--header` | `#ffffff` | `#ffffff` | `design-system.css`, `site-header.css` |
| `--header-fg` | `#222222` | `#222222` | `design-system.css`, `site-header.css` |
| `--header-fg-soft` | `#505966` | `#505966` | `design-system.css` |
| `--header-hover` | `#f6f7f9` | `#f6f7f9` | `design-system.css` |
| `--footer` | `#1f2937` | `#1f2937` | `design-system.css`, `site-footer.css` |
| `--footer-fg` | `#ffffff` | `#ffffff` | `design-system.css`, `site-footer.css` |
| `--footer-fg-soft` | `#e5e7eb` | `#e5e7eb` | `design-system.css`, `site-footer.css` |
| `--sale-bg` | `#b3262b` | `#b3262b`; used by `Badge`, which no page shows yet | `design-system.css`, `components/ui/Badge.css` |
| `--sale-fg` | `#ffffff` | `#ffffff`; used by `Badge` | `design-system.css`, `components/ui/Badge.css` |
| Dark tokens | Unchanged | As listed in Colors | `design-system.css`, `home.css` |
| Dark `--footer`, `--footer-fg`, `--footer-fg-soft` | `#0a0f17`, `#ffffff`, `#cfd5df` | `#0a0f17`, `#ffffff`, `#cfd5df` | `design-system.css` |

### Typography

| Item | Target | Current | File |
| --- | --- | --- | --- |
| Catalog and purchase font | Poppins / Open Sans | Arial | `products.css` |
| Page title | 32px 600, line height 1.2 | Login, register and profile 28px; home `clamp(24px, 3vw, 30px)`; catalog browser default (1.8rem up to 480px) | `auth.css`, `home.css`, `products.css` |
| Product card title | 20px 600 Poppins | Matches | `products.css` |
| Price | 24px 500 Poppins | Product card matches; 1.2rem bold Arial (product page, wishlist); 1.2rem bold (purchase total) | `products.css`, `purchases.css` |
| Order card title | 20px | 1.1rem | `orders.css` |
| Stock line | 12px 600 | Product card matches; browser default size on the product page | `products.css` |
| Button label | Poppins 16px 500 | Login and product card Poppins 16px 500; others inherit body font, 600 | `auth.css`, `components/ui/Button.css`; `cart.css`, `add-to-cart.css`, `products.css` |

### Components

| Item | Target | Current | File |
| --- | --- | --- | --- |
| Primary button height | 44px | `Button` 44px, used by the header search and the product card; 48px (login, register, profile); padding-based elsewhere (about 40px) | `components/ui/Button.css`; `auth.css`; `cart.css`, `add-to-cart.css` |
| Primary button corners | 4px | `Button` 4px; 6px (login); 0.4rem (6.4px) | `components/ui/Button.css`; `auth.css`; `cart.css`, `add-to-cart.css` |
| Primary button hover | Fill `--accent-hover` (light) | `Button` matches; login: ring glow and spotlight, fill unchanged | `components/ui/Button.css`; `auth.css` |
| Secondary button | 44px, 4px corners | Padding 0.75rem 1rem, 0.4rem corners | `products.css` |
| Header | White, at least 64px, 1px `--line` bottom, two rows | Matches | `site-header.css`, `Header.jsx` |
| Header content | Logo, API categories with mega menu, search, account, cart count | Matches | `Header.jsx`, `CategoryMenu.jsx` |
| Active nav link | 2px `--accent` underline | Matches | `site-header.css` |
| Footer | `--footer` dark | Matches | `site-footer.css`, `Footer.jsx` |
| Product card | 16px corners, product shadow, 1:1 image, CTA at the bottom | Matches: `Card`, 1:1 image, "Add to cart" `Button` | `components/ui/Card.css`, `products.css`, `ProductCard.jsx` |
| Product card spec row | One `body-sm` row | Matches: brand, or the first line of the description | `ProductCard.jsx`, `lib/product-display.js` |
| Discount badge | `--sale-bg`, 3px, 14px 600 | `Badge` matches; no page shows a discount yet | `components/ui/Badge.css` |
| Admin panel | Menu, KPI cards, `Table`, review approval | Matches (SCRUM-124) | `pages/AdminPanelPage.jsx`, `components/admin/`, `components/ui/Table.js`, `styles/admin.css` |
| Customer profile | Form cards, neutral note, 32px title and 44px button | Cards and note match; title 28px and button 48px with 6px corners, as on login (SCRUM-103) | `pages/ProfilePage.jsx`, `styles/profile.css`, `auth.css` |
| Purchase card | `--surface`, `--line` | Hard-coded `white`, `#dce0e6`, 0.6rem | `purchases.css` |
| Purchase input | 44px, 6px, `--field-line` | `Input` matches, used by the header search; purchase inputs: padding 0.75rem, 0.3rem, `#9ba9b9` | `components/ui/Input.css`; `purchases.css` |
| Purchase message | `--danger-bg` / `--danger-fg` for errors | Hard-coded `#e8f0fa`, `#ffebea`, `#7c231d` | `purchases.css` |

### Layout and responsive

| Item | Target | Current | File |
| --- | --- | --- | --- |
| Breakpoints | 480 / 768 / 1024 / 1280 | 480 (auth, catalog, purchase), 480 / 768 / 1024 (product grid), 576 (cart), 700 (product detail), 733 (home status) | `auth.css`, `products.css`, `purchases.css`, `cart.css`, `home.css` |
| Touch targets | 44px | Header, footer and card buttons 44px; cart quantity buttons 2.25rem (36px); sort select 40px; catalog filter inputs 41px and selects 42px; card title links 28px high (inline text links) | `cart.css`, `home.css`, `products.css` |
| Max widths | Kept | 1280 / 1200 / 920 / 420px | `auth.css`, `home.css`, `products.css`, `purchases.css` |
| Product card shadow | `0 2px 8px rgba(26, 26, 26, 0.08)` | Matches, through `Card` | `design-system.css`, `components/ui/Card.css` |
