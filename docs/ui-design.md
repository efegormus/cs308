# UI design

This records the SCRUM-56 design decisions, so cart, catalog and later pages use the
same look. The login page (SCRUM-68) is the first page built with it. The source is
`OnlineStore/wwwroot/css/auth.css`.

## Change a color in one place

Every color is a custom property on `:root` at the top of `auth.css`. Edit the value
there and nothing else. A dark value appears twice, once in the
`prefers-color-scheme: dark` block and once in the `[data-theme="dark"]` block, so
change both. Never write a color directly in a rule.

## Add the login header to a new page

Put the first two lines in `<head>` and the block at the end of the page's `<nav>`:

```html
<link rel="stylesheet" href="/css/auth.css">
<script type="module" src="/js/auth.js"></script>

<a id="auth-link" href="/login.html">Login</a><span id="user-name" hidden></span><button id="logout" type="button" hidden>Logout</button>
```

`auth.js` then handles the signed-in state, the return link and logout. The nav must be
a flex container, as it is on the existing pages. A page that needs the signed-in
account uses the [frontend contract](authentication.md#frontend-contract).

`auth.css` is safe on any page: only custom properties sit on `:root`, and every other
rule is scoped to `#auth-link`, `#user-name`, `#logout` or `body.auth-page`. Keep it
that way. Pages without the Google Fonts link show the header text in Arial.

## Fonts

| Use | Font | Weights |
| --- | --- | --- |
| Header, headings, buttons | Poppins, Arial fallback | 500, 600 |
| Descriptions, labels, inputs | Open Sans, Arial fallback | 400, 600 |

## Colors

| Token | Purpose | Light | Dark |
| --- | --- | --- | --- |
| `--bg` | Page background | `#f6f7f9` | `#0e131a` |
| `--surface` | Card, input, footer | `#ffffff` | `#18202c` |
| `--header` | Header bar | `#1f2937` | `#0a0f17` |
| `--header-fg` | Header text | `#ffffff` | `#ffffff` |
| `--header-fg-soft` | Signed-in name | `#e5e7eb` | `#cfd5df` |
| `--header-hover` | Logout hover | `rgba(255, 255, 255, 0.12)` | same |
| `--fg` | Main text | `#222222` | `#e8ebf0` |
| `--muted` | Secondary text | `#505966` | `#a4aebc` |
| `--line` | Card and footer border | `#dce0e6` | `#2a3445` |
| `--field-line` | Input border | `#6b7280` | `#7b8799` |
| `--accent` | Primary button | `#174a8b` | `#2d6bcf` |
| `--accent-hover` | Link hover | `#0f3363` | `#4a84e0` |
| `--accent-fg` | Primary button text | `#ffffff` | `#ffffff` |
| `--link` | Links | `#174a8b` | `#8db9ff` |
| `--focus` | Focus ring | `#b66d00` | `#f0a64a` |
| `--danger` | Error border and field message | `#a32b12` | `#ff8a73` |
| `--danger-bg` | Error banner background | `#fdecea` | `#3a1c17` |
| `--danger-fg` | Error banner text | `#7a1f0d` | `#ffd1c7` |
| `--ok` | Success fill | `#1a7f37` | `#238636` |
| `--shadow` | Card shadow | `0 1px 3px rgba(31, 41, 55, 0.08)` | `0 1px 3px rgba(0, 0, 0, 0.4)` |
| `--btn-glow` | Button spotlight | `rgba(255, 255, 255, 0.28)` | same |
| `--ring-glow` | Button hover ring | `rgba(23, 74, 139, 0.22)` | `rgba(77, 132, 224, 0.35)` |

Dark values follow the visitor's system setting. Only pages with
`<body class="auth-page">` use the dark background; `index.html` and `products.html`
have no dark theme yet.

## Layout and spacing

- Centered white card: max-width 420px, padding 36px 32px (28px 20px up to 480px wide),
  radius 10px, one column from top to bottom.
- Page: header, then the card centered in the remaining height, then the footer.
- Header padding 20px 32px, wrapping on small screens. There must be no sideways scroll
  at 375px, which the catalog browser check enforces.
- Vertical gap between fields: 18px. Label to input: 6px.

## Patterns

- **Primary button:** height 48px, full width, radius 6px, `--accent`, Poppins 500.
- **Input:** height 44px, radius 6px, 1px `--field-line` border, 16px text. Always has a
  `<label>`.
- **Focus:** 3px `--focus` outline with a 2px offset on inputs, buttons and links.
- **Field error:** message below the field in `--danger`, 13px, weight 600. The input gets
  `aria-invalid="true"`, a 2px `--danger` border and `aria-describedby` pointing to the
  message. The first invalid field gets focus.
- **Error banner:** `role="alert"`, `--danger-bg` background, `--danger` border,
  `--danger-fg` text, 14px, weight 600.
- **Success:** the button fills with `--ok`, then shows a check and "Signed in". There is
  no separate welcome screen.

## Motion

Motion is CSS plus small JS helpers (`js/login.js`). Keyframes are prefixed `auth-`.

| Effect | How it works |
| --- | --- |
| Reveal-up on load | Each line sits in an overflow-hidden mask `.r`. Its inner `.ri` drifts up with a soft blur over 0.85s, `cubic-bezier(0.22, 1, 0.36, 1)`, staggered 110ms per line. JS sets the index with `element.style.setProperty('--i', n)`, never as an HTML attribute. When a line finishes it gets `.done`, so the mask does not clip the focus ring. |
| Shake | An invalid field moves left and right once for 0.45s, then stops. Both fields shake on wrong credentials. |
| Button spotlight | While the cursor is over the button, a soft glow follows it (`--mx`, `--my` set from `pointermove`), with a light ring. Only on the button, never on the card. |
| Loading | The button is disabled and shows a small spinner and "Logging in…". |
| Success | Green fills the button left to right over 0.95s, then a check pops in with "Signed in", then the page redirects. |

### Reduced motion

`prefers-reduced-motion: reduce` turns off the reveal, the shake, the spinner rotation
and the fill, and the final state is shown immediately. The masks must also come off in
that mode: with no animation there is no `animationend`, so `.done` is never added and
the mask would clip the focus ring.
