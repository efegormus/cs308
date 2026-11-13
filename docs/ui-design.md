# UI design

This records the SCRUM-56 design decisions, so cart, catalog and later pages use the
same look. The login page (SCRUM-68) is the first page built with it. The source is
`frontend/src/styles/`.

## Change a color in one place

In the React frontend every token (colors, shadows, the type scale, spacing and corner
radii) is a custom property on `:root` in `frontend/src/styles/design-system.css`, which
`main.jsx` loads before every other stylesheet. Edit the value there and nothing else. A
dark value appears twice, once in the `prefers-color-scheme: dark` block and once in the
`[data-theme="dark"]` block, so change both. Never write a color directly in a rule.
The target values and their reasons are in [DESIGN.md](../DESIGN.md).

## Add a new page

Add its `<Route>` inside the `Layout` route in `frontend/src/App.jsx`. `Layout` renders the
shared header and footer, so the page gets the Login link, the signed-in state, the return
link and logout without extra code (see [Header and footer](#header-and-footer)). A page
that needs the signed-in account uses the [frontend contract](authentication.md#frontend-contract).
Give the page its wrapper class, as described in [Shared rules](#shared-rules).

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
| `--header` | Header bar | `#ffffff` | `#0a0f17` |
| `--header-fg` | Header text | `#222222` | `#ffffff` |
| `--header-fg-soft` | Signed-in name | `#505966` | `#cfd5df` |
| `--header-hover` | Logout hover | `#f6f7f9` | `rgba(255, 255, 255, 0.12)` |
| `--footer` | Footer bar | `#1f2937` | `#0a0f17` |
| `--footer-fg` | Footer text and links | `#ffffff` | `#ffffff` |
| `--footer-fg-soft` | Footer copyright line | `#e5e7eb` | `#cfd5df` |
| `--fg` | Main text | `#222222` | `#e8ebf0` |
| `--muted` | Secondary text | `#505966` | `#a4aebc` |
| `--line` | Card and footer border | `#dce0e6` | `#2a3445` |
| `--field-line` | Input border | `#6b7280` | `#7b8799` |
| `--accent` | Primary button | `#024ad8` | `#2d6bcf` |
| `--accent-hover` | Link hover, light primary button hover | `#0e3191` | `#4a84e0` |
| `--accent-fg` | Primary button text | `#ffffff` | `#ffffff` |
| `--link` | Links | `#174a8b` | `#8db9ff` |
| `--focus` | Focus ring | `#b66d00` | `#f0a64a` |
| `--danger` | Error border and field message | `#a32b12` | `#ff8a73` |
| `--danger-bg` | Error banner background | `#fdecea` | `#3a1c17` |
| `--danger-fg` | Error banner text | `#7a1f0d` | `#ffd1c7` |
| `--ok` | Success fill | `#1a7f37` | `#238636` |
| `--ok-fg` | Success text, such as "In stock" | `#1a7f37` | `#3fb950` |
| `--sale-bg` | Discount badge | `#b3262b` | same |
| `--sale-fg` | Discount badge text | `#ffffff` | same |
| `--shadow` | Card shadow | `0 1px 3px rgba(31, 41, 55, 0.08)` | `0 1px 3px rgba(0, 0, 0, 0.4)` |
| `--shadow-product` | `Card` (product card) shadow | `0 2px 8px rgba(26, 26, 26, 0.08)` | same, no dark value yet |
| `--shadow-menu` | Account menu and category panel | `0 10px 28px rgba(31, 41, 55, 0.18)` | `0 10px 28px rgba(0, 0, 0, 0.55)` |
| `--btn-glow` | Button spotlight | `rgba(255, 255, 255, 0.28)` | same |
| `--ring-glow` | Button hover ring | `rgba(23, 74, 139, 0.22)` | `rgba(77, 132, 224, 0.35)` |

Dark values follow the visitor's system setting. The login and register pages
(`.auth-page`), the home page, the cart page and, since SCRUM-123, the catalog pages
(`products.css`: product list, product page, wishlist) use the dark background; the
purchase pages (`purchases.css`) have no dark theme yet.

## Layout and spacing

- Centered white card: max-width 420px, padding 36px 32px (28px 20px up to 480px wide),
  radius 10px, one column from top to bottom.
- Page: header, then the card centered in the remaining height, then the footer.
- The header and footer are described under "Header and footer" below. There must be no
  sideways scroll at 375px, which the catalog browser check enforces.
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

## Shared rules

What the React pages have in common today, as written in `frontend/src/styles/`. Every
page has a wrapper class, and its CSS file starts every selector with it, because all CSS
files load at once. The login, register, home and cart pages and the request states use
the color tokens above. `products.css` uses them too, except the plain button border
`#b9c4d2`. `purchases.css` came from the old pages and still writes its colors directly.

### Type scale

| Use | Font | Size | Weight |
| --- | --- | --- | --- |
| Logo | Poppins | 23px | 600 |
| Header links | Poppins | 16px | 500 |
| Login and register heading | Poppins | 28px | 600 |
| Home heading | Poppins | `clamp(24px, 3vw, 30px)` | 600 |
| Home category title | Poppins | 32px | 600 |
| Home small row heading | Poppins | 18px | 600 |
| Coverflow product name | Poppins | 16px | 600 |
| Coverflow price | Poppins | 18px | 500 |
| Login and register text | Open Sans | 16px | 400 |
| Home text | Open Sans | 15px, line height 1.5 | 400 |
| Form label, error banner | Open Sans | 14px | 600 |
| Field error | Open Sans | 13px | 600 |
| Coverflow stock | Open Sans | 12px | 600 |

The product card uses the scale: name 20px 600 Poppins, spec row 14px Open Sans, price
24px 500 Poppins, stock 12px 600 Open Sans. The rest of the catalog and the purchase pages
still use Arial: product page price 1.2rem bold, product count 0.9rem.

### Spacing

| Where | Value |
| --- | --- |
| Login and register card padding | 36px 32px (28px 20px up to 480px wide) |
| Login form: between fields, label to input | 18px, 6px |
| Home `main` | max-width 1280px, 36px top, 56px bottom, at least 16px at the sides |
| Home: between rows, row padding, bar side padding | 32px, 18px top and 16px bottom, 20px |
| Catalog `main` | max-width 1200px, 2.5rem 1.5rem (2rem 1rem up to 480px wide) |
| Product grid gap, product card padding | 20px, 20px |
| Purchase card padding | `clamp(1rem, 3vw, 2rem)` |
| Cart: between lines, line padding | 0.75rem, 0.75rem 1rem |

### Buttons, cards and forms

| Pattern | Rule |
| --- | --- |
| Primary button (login, register) | Height 48px, full width, radius 6px, `--accent`, Poppins 16px 500 |
| Plain button (catalog) | Padding 0.75rem 1rem, radius 0.4rem, `--surface` background, 1px `#b9c4d2` border, text `--link`. Not applied to `Button` |
| Checkout button (cart) | Padding 0.625rem 1.25rem, radius 0.4rem, `--accent`, weight 600 |
| Coverflow arrow | 44px circle, `--surface`, 1px `--line` border, fills with `--accent` on hover |
| Login card | Max-width 420px, radius 10px, 1px `--line` border, `--shadow` |
| Product card | `Card` (16px corners, 20px padding, `--shadow-product`, no border); see Product card |
| Purchase card | Radius 0.6rem, 1px `#dce0e6` border, white |
| Cart line | Radius 0.5rem, 1px `--line` border, `--surface` |
| Coverflow row, card | Radius 12px with `--shadow`, radius 10px with `--card-shadow` at the front |
| Login input | Height 44px, radius 6px, 1px `--field-line` border, 16px text |
| Purchase input | Padding 0.75rem, radius 0.3rem, 1px `#9ba9b9` border |
| Home sort dropdown | Height 40px, min-width 190px, radius 8px, 1px `--field-line` border, 14px text |
| Focus ring | 3px outline, `--focus` (`#b66d00` written directly on purchase pages). Offset 2px on login and purchase inputs and on `Button`, 3px on home, cart and request states, 4px on catalog links and plain buttons |

### Shared UI components

`frontend/src/components/ui/` holds the shared parts from DESIGN.md. Each has its own CSS
file next to it, and its classes start with `ui-`. The header search uses `Button` and
`Input` (with `hideLabel`); the product card uses `Card` and `Button`.

| Component | Rule |
| --- | --- |
| `Button` | Primary button: at least 44px high, 4px corners, `--accent`, Poppins 16px 500, not uppercase. Light hover and pressed fill `--accent-hover`; in the dark theme the fill stays `--accent` and a 4px `--ring-glow` ring shows. Focus 3px `--focus`, offset 2px. Disabled opacity 0.7 |
| `Input` | Label, input and optional field error. 44px, 6px corners, 1px `--field-line`, 16px Open Sans, padding 0 12px, focus 3px `--focus` offset 2px. With `error`: `aria-invalid`, `aria-describedby`, 2px `--danger` border, 13px 600 message. `hideLabel` keeps the label for screen readers only |
| `Card` | DESIGN.md's `card-product`: `--surface`, 16px corners, padding 20px, `--shadow-product`, flex column. `as` picks the element |
| `Badge` | Discount badge: `--sale-bg`, `--sale-fg`, 3px corners, padding 4px 8px, Open Sans 14px 600 |
| `Table` | Data table (SCRUM-124). Plain `createElement` like `RequestStates.js`, so Node tests render it; the page imports `ui/Table.css`. Columns `{ key, header, render?, rowHeader?, align? }`; `th scope="col"`, `th scope="row"` for a `rowHeader` column. `loading`: skeleton rows (1.2s shimmer) in a `role="status"` box named by `loadingLabel`. No rows: `emptyText` in a `role="status"` box. Wide tables scroll inside a focusable, named region; the page never scrolls sideways |

### Header and footer

`components/Header.jsx`, `components/CategoryMenu.jsx` and `styles/site-header.css`;
`components/Footer.jsx` and `styles/site-footer.css` (SCRUM-123). DESIGN.md's Header and
Footer sections hold the full rules.

| Part | Rule |
| --- | --- |
| Layout | Two rows at every width: logo, search, account area and cart; then the category bar. Up to 768px the search moves under the logo row; the category bar wraps. Inner width 1280px, 32px sides (16px up to 480px), min-height 64px, 1px `--line` bottom |
| Category bar | "All products" and the top-level categories of `GET /api/categories` (`parentId` null). With subcategories: a button with `aria-expanded` that opens a full-width panel ("All <category>" and its subcategories). Without `parentId` every category is a plain link to `/products#category-<id>` |
| Mega menu input | Hover opens and leaving closes (mouse); tap toggles, tap outside closes (touch); Enter, Space or Down opens, Up and Down move, Escape closes and refocuses the button, Tab never opens (keyboard) |
| Search | 200px `Input` and 44px `Button`, opens `/products?search=<text>`; the product list passes `search` to `useCatalog` |
| Account area | Login, or the 44px profile icon with its menu and Logout (44px, 4px corners). Deliveries link for the product manager only |
| Cart | 44px icon, count bubble in `--accent` (hidden at 0). Guests and customers only. Reloads on page change, login, logout and the `cart-changed` event that `cart-api.js` sends after every change |
| Current page | 2px `--accent` line (inset shadow) and `aria-current="page"` |
| Footer | `--footer` background, store name, Products / Cart / Orders (Cart and Orders for guests and customers), copyright in `--footer-fg-soft`. Links `--footer-fg`, underlined, 44px high. No support link until the customer chat exists |
| Requests | The header and the page share one `GET /api/categories` when they load together (`fetchCategories`) |

### Product card

`components/ProductCard.jsx` and the product grid rules in `styles/products.css` (SCRUM-123).
DESIGN.md's Product card section holds the full rules.

| Part | Rule |
| --- | --- |
| Grid | 4 columns, 3 up to 1024px, 2 up to 768px, 1 up to 480px; 20px gap |
| Frame | `Card` as `<article class="product-card">`, full height of the row |
| Image | 1:1, `object-fit: contain`, `--bg`, 8px corners; "No image available" when missing or broken |
| Text | Name `h3` (link to `/product?id=<id>`), spec row (`specRow`: brand, else the first description line, one line with "…"), price, stock status (`stockStatus`: "In stock" in `--ok-fg`, "Out of stock" in `--danger`) |
| Add to cart | Full-width `Button`, one unit, `withCsrfRetry` (one retry after a 400). Success: `role="status"` "Added to your cart. View cart"; failure: `role="alert"` with `addToCartError`. Out of stock: disabled "Out of stock". Staff: no button |
| Alignment | Price, stock, message and button sit at the bottom, so they line up across a row |
| Actions row | `.product-card-actions`, room for a second action such as a wishlist button |

### Loading, empty and error states

`components/RequestStates.js` and `styles/request-states.css` hold the shared parts.

| State | Rule |
| --- | --- |
| Loading | `LoadingState`: `role="status"`, a short label such as "Loading your cart…". The product list has its own status line, "Loading products…" |
| Error | `ErrorState`: `role="alert"`, a safe message from `normalizeRequestError`, and a "Try again" button when the page passes `onRetry`. It uses `--danger`, `--danger-bg` and `--danger-fg` |
| Empty | Plain text. "Your cart is empty." with a link to the products, "No deliveries yet.", "No products available in this category yet." on the product list, "No products in this category yet." on a home row, "No products available yet. Please check back soon." when the catalog has nothing |

## Admin panel

`pages/AdminPanelPage.jsx`, `components/admin/` (`AdminShell`, `KpiCard`, `ReviewApprovalRow`),
`styles/admin.css`, and the pure helpers `lib/admin-menu.js`, `lib/admin-api.js`, `lib/count-up.js`,
`lib/approval-row.js` and `lib/motion.js` (SCRUM-124). It replaces the SCRUM-130 landing page at `/manage`;
DESIGN.md's Admin panel section holds the rules.

| Part | Rule |
| --- | --- |
| Routes | `/manage` (Overview) and `/manage/<section>`. Product manager: `deliveries`, `reviews`, `low-stock`. Sales manager: `invoices`, `discounts`. Another role's section sends back to `/manage`; other roles get the `PurchaseAccess` message |
| Data | `GET /api/product-management/dashboard` (one request serves Overview, Reviews and Low stock), `GET /api/product-management/deliveries` (the Deliveries table, read-only; status changes stay on `/deliveries`), `GET /api/sales/dashboard` for the last 30 days |
| Menu | 240px from 768px up; below 768px a 44px "Menu" disclosure (`aria-expanded`), closed by a choice or Escape. Hover: 3px `--accent` inset line; current page: the line, `aria-current="page"` and `color-mix(in srgb, var(--accent) 10%, var(--surface))` |
| KPI cards | `Card`; product manager: low-stock products, pending deliveries, reviews to approve; sales manager: gross revenue, refunds, net revenue, active discounts. tr-TR numbers, TRY amounts. A list at the API's cap says "The first N are shown." |
| Review approval | `ReviewApprovalRow` takes `onApprove(review)`. There is no approval API until SCRUM-81, so the page passes a placeholder that resolves at once and the section says that nothing is saved yet |

### Admin motion

The values are in `lib/motion.js`. `AdminShell` puts them on `.admin-shell` as custom properties
(`--admin-section-ms` and so on), so CSS and JS read the same numbers. `prefers-reduced-motion: reduce`
turns all of it off.

| Effect | Duration | Delay | Curve |
| --- | --- | --- | --- |
| Section content `admin-section-in`: 8px from above, opacity 0 to 1 | 220ms | 0 | `cubic-bezier(0.22, 1, 0.36, 1)` |
| Menu item line (`box-shadow`) | 200ms | 0 | ease |
| KPI card `admin-rise`: 8px from below | 220ms | index × 80ms | `cubic-bezier(0.22, 1, 0.36, 1)` |
| KPI value counts from 0 (`requestAnimationFrame`) | 700ms | with its card | easeOutCubic |
| Skeleton shimmer | 1.2s, repeats | 0 | linear |
| Approve: width 110px to 44px, `--ok` fill, 22px corners | 200ms | after `onApprove` resolves | ease |
| Approve: tick drawn (`stroke-dashoffset`) | 250ms | 120ms | ease |
| Approved row waits (button blocked, `aria-label` "Review approved") | 800ms | 0 | — |
| Row closes: opacity, `max-height`, `min-height`, `border-top` to 0, then removed | 250ms | 0 | ease |

With reduced motion the KPI value is final at once, the tick and green show at once, and the row goes
800ms later without a fade. When the removed row held the focus, it moves to the next row's Approve
button, or to the heading when the list is empty.

## Home page coverflow

The home page (SCRUM-56) stacks one row per category, and each row sorts on its own. The
approved source for its behavior and motion is the
[prototype](design/home-coverflow-prototype.html), a single file to open in a browser. The
code is `components/CategoryCoverflow.jsx`, `components/ViewCursor.jsx`, `lib/coverflow.js`
and `styles/home.css`.

### Layout

- "Shop by category" and one line of text, then the rows.
- A row is a bar (small heading, sort dropdown), the track with the arrows on top of it,
  and a count line such as "2 / 5 · iPhone 15".
- At rest the first card sits beside the category panel: icon, name, product count and a
  "Browse tablets" link to `/products#category-<id>`. Moving forward, the cards slide over
  the panel, the front card reaches the center at the second product, and the small heading
  appears in the bar.
- When the track is narrower than 700px there is no panel, so no Browse link either; the
  header's Products link stays. The small heading is always shown.

### Sorting

| Choice | Request | Order |
| --- | --- | --- |
| Best sellers (default) | `?categoryId=<id>&sort=popularity` | Units sold, highest first, then name |
| Price: low to high | `?categoryId=<id>&sort=price&order=asc` | Then name |
| Price: high to low | `?categoryId=<id>&sort=price&order=desc` | Then name |

The server sorts, because popularity only exists there. A new sort cancels the row's
previous request. The cards stay in place until the new order arrives, then slide to it one
after another and the row returns to its first product. If that request fails, the error
state replaces the cards. A failure in one row leaves the others alone.

### Interactions

| Input | Result |
| --- | --- |
| Arrow button | Moves one product. Invisible and disabled at the end of the row |
| Click on a side card | Brings it to the front, stays on the page |
| Click or Enter on the front card | Opens `/product?id=<id>` |
| Touchpad swipe, Shift + wheel | The row follows in real time, with no threshold, and settles on the nearest product. A vertical wheel scrolls the page |
| Mouse drag, finger swipe | The row follows after the threshold, and a quick flick carries on. The click after a drag is swallowed |
| Tab | Sort dropdown, track, Browse link while it is uncovered, front card, arrows. Only the front card of a row is a Tab stop |
| Left, Right, Home, End on the track or a card | Move the row. With the focus on a card, it moves to the new front card |

### Following values

| Value | Setting |
| --- | --- |
| One product of movement | Card width + 12px |
| Touchpad settles | 140ms after the last wheel event |
| Past either end | Moves at 0.3 of the input, raw wheel value limited to 0.6 past the end |
| Drag threshold | 6px |
| Flick | Position − velocity × 160ms, at most 2 products from where the drag started |

### Geometry

| Value | Wide | Track under 700px |
| --- | --- | --- |
| Card | 220 × 312px | 168 × 252px |
| Gap to the neighbor | 12px | 12px |
| Spacing of cards further away | 72px | 40px |
| First card's rest position | 290px | 16px |
| Track height | Card height + 64px | Card height + 64px |
| Card top | 28px | 28px |

`pos` is the row's position as a real number, and `d` is a card's distance from it.

| Value | Formula |
| --- | --- |
| Anchor | `lerp(rest, center, clamp(pos, 0, 1))` |
| Card x, `\|d\|` up to 1 | `anchor + d × (cardW + 12)` |
| Card x, further away | `anchor + sign(d) × ((cardW + 12) + (\|d\| − 1) × spacing)` |
| Turn | `rotateY(−sign(d) × 55° × min(\|d\|, 1)) scale(1 − 0.04 × min(\|d\|, 1))` |
| Turning edge | Left for `d ≥ 0`, right for `d < 0` |
| Brightness | `clamp(1 − 0.06 × min(\|d\|, 1) − 0.035 × max(\|d\| − 1, 0), 0.76, 1)` |
| Z-index | `1000 − round(\|d\| × 20)` |
| Panel | Opacity `1 − cover × 1.6`, moves `−28px × cover`, where `cover = clamp(pos, 0, 1)` |
| Small heading | Opacity `(cover − 0.25) × 2`, comes in from −10px |
| Halo, floor | Halo 2.2 × card width, centered on the anchor. Floor card width − 20px, 6px under the card |

### Motion

`ease-out` is `cubic-bezier(0.22, 1, 0.36, 1)`. Keyframes start with `home-`, because they
cannot be scoped to `.home-page`. No animation library is used.

| Effect | Duration | Delay | Curve |
| --- | --- | --- | --- |
| Heading and text `home-reveal-up` | 0.85s | 120ms + i × 110ms | ease-out |
| Row `home-row-in` | 0.7s | 260ms + row × 160ms | ease-out |
| Card `home-card-in`, i in id order | 0.8s | 420ms + row × 160ms + i × 80ms | ease-out |
| Card `left`, `transform` | 0.55s | After a new sort, position × 45ms | ease-out |
| Card `filter` | 0.55s | After a new sort, position × 45ms | ease |
| Halo `left`, `width`; floor `left` | 0.55s | 0 | ease-out |
| Panel `opacity`, `transform` | 0.35s, 0.55s | 0 | ease, ease-out |
| Small heading `opacity`, `transform` | 0.32s, 0.45s | 0 | ease, ease-out |
| Card face `box-shadow`, `border-color` | 0.5s, 0.3s | 0 | ease |
| Count line `home-count-in` | 0.4s | 0 | ease-out |
| Arrow colors, `opacity`, `transform` | 0.18s, 0.2s, 0.18s | 0 | ease, ease, ease-out |

While a finger or touchpad moves the row (`.dragging`), cards, halo, floor, panel and small
heading have no transition and follow directly. Card entrances start when the row's
products arrive, so a slow answer delays them.

The front card's border turns `--accent` on hover. The `translateY(-4px)` rule beside it
has no visible effect, in the prototype as well: the entrance animation fills forwards, and
its `transform: none` wins over the hover rule.

### VIEW cursor

One 76px circle per page. It only runs on `(hover: hover) and (pointer: fine)` devices,
replaces the normal pointer on a row's front card, and is hidden during a drag. The login
page's button spotlight is not used here.

| Part | Grows | Shrinks |
| --- | --- | --- |
| Circle `transform` | 0.42s `cubic-bezier(0.34, 1.45, 0.5, 1)` | 0.26s `cubic-bezier(0.4, 0, 1, 1)` |
| Word `opacity` | 0.2s ease, after 0.14s | 0.12s ease |
| Word `transform` | 0.32s ease-out, after 0.12s | 0.2s ease |

It follows the pointer with a lerp of 0.28 per frame. It looks for the front card every
frame, so it also reacts when a card slides under a still pointer.

### Reduced motion

`prefers-reduced-motion: reduce` turns off every animation and transition on the page. Side
cards scale with `scale(1 − 0.08 × min(|d|, 1))` instead of turning, and the VIEW circle
sits on the pointer (lerp 1).

### Home tokens

They sit on `.home-page` in `home.css`. `--home-ok` is separate from `--ok` because `--ok`
is a button fill. On a dark card, `#238636` is too dim for 12px text. The shared `--ok-fg`
(SCRUM-123) has the same values; the home page keeps its own token for now.

| Token | Purpose | Light | Dark |
| --- | --- | --- | --- |
| `--home-ok` | In-stock text | `#1a7f37` | `#3fb950` |
| `--card-shadow` | Front card shadow | `0 22px 44px rgba(31, 41, 55, 0.18)` | `0 22px 44px rgba(0, 0, 0, 0.55)` |
| `--placeholder` | Image area, icon background | `#eef0f3` | `#222c3a` |
| `--placeholder-fg` | Placeholder icon and text | `#7b8594` | `#8794a6` |
| `--halo` | Light behind the front card | `rgba(23, 74, 139, 0.13)` | `rgba(77, 132, 224, 0.22)` |
| `--floor` | Shadow under the front card | `rgba(31, 41, 55, 0.22)` | `rgba(0, 0, 0, 0.45)` |
| `--view-bg` | VIEW circle | `#111418` | `#f3f5f8` |
| `--view-fg` | VIEW word | `#ffffff` | `#111418` |

### Rules for changing the code

- **No renders while moving.** The position is a real number outside React state, and
  styles are written directly in `requestAnimationFrame`. A wheel gesture or drag causes 0
  renders while it moves and 1 when the row settles on another product.
- **Only real changes reach React.** A `setState` with the same value stays queued and
  causes an empty extra commit at the next update, so the row checks first.
- **Cards keep their DOM order.** They render in id order, and only their positions follow
  the sort. Moving a node in the DOM cancels its transition and replays its entrance.
- **Every card is always a link.** Changing the element when a card comes to the front
  would recreate the node, and it would jump instead of sliding.

### Differences from the prototype

- The panel link says "Browse tablets", not "See all tablets", because
  `scripts/verify_catalog_browser.py` follows that link.
- The prototype's `press` animation is gone: in the store the front card opens the product.
- Added because the prototype has no such cases: real product images (`object-fit: contain`,
  and the placeholder when one is missing or fails), a neutral icon for other categories,
  and loading, error and empty rows that keep the row's height.
- Keyboard and pointer fixes with no visual change: Tab skips a covered Browse link, a
  disabled arrow hands its focus to the track, Alt + arrow stays the browser's shortcut, and
  a mouse button released outside the row ends the drag.
- The store's real header and footer replace the prototype's copies.
