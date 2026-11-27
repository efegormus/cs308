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
| `--header` | Header bar (dark in both themes since SCRUM-164) | `#0a0f17` | `#0a0f17` |
| `--header-fg` | Header text | `#ffffff` | `#ffffff` |
| `--header-fg-soft` | Secondary header text, panel and menu links | `#cfd5df` | `#cfd5df` |
| `--header-hover` | Header hover fill, panel highlight pill | `rgba(255, 255, 255, 0.12)` | same |
| `--header-panel` | Categories panel, search field, account menu | `#111826` | same |
| `--header-line` | Header bottom line, button and pill edges | `#2f3b50` | same |
| `--header-accent` | Search focus, avatar, cart count, header active lines | `#3b78f5` | same |
| `--header-accent-ring` | Search focus ring | `rgba(59, 120, 245, 0.28)` | same |
| `--header-danger` | "Sign out" | `#ff8a73` | same |
| `--header-scrim` | Veil behind the categories panel | `rgba(10, 15, 26, 0.5)` | same |
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

Motion tokens (SCRUM-164): `--ease-standard` `cubic-bezier(0.4, 0, 0.2, 1)`, `--dur-fast` 200ms and
`--dur-base` 320ms. The header uses them; see [Header motion](#header-motion).

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

- **Primary button:** the shared `Button`, 44px high, full width, radius 4px, `--accent`, Poppins 500.
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
| Login, register and profile heading | Poppins | 32px (`--type-heading-lg`) | 600 |
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
24px 500 Poppins, stock 12px 600 Open Sans. The rest of the catalog, the purchase pages, the product page, wishlist,
dashboard and support page use the scale too (`body-md` text, `heading-lg` titles, `heading-md` section titles,
`body-sm` product count) since SCRUM-134.

### Spacing

| Where | Value |
| --- | --- |
| Login and register card padding | 36px 32px (28px 20px up to 480px wide) |
| Login form: between fields, label to input | 18px, 6px |
| Home `main` | max-width 1280px, 36px top, 56px bottom, at least 16px at the sides |
| Home: between rows, row padding, bar side padding | 32px, 18px top and 16px bottom, 20px |
| Catalog `main` | max-width 1200px, 2.5rem 1.5rem (2rem 1rem up to 480px wide) |
| Product grid gap, product card padding | 20px, 20px |
| Purchase card padding | `clamp(16px, 2.5vw, 24px)` |
| Cart: between lines, line padding | 0.75rem, 0.75rem 1rem |

### Buttons, cards and forms

| Pattern | Rule |
| --- | --- |
| Primary button (login, register, profile) | The shared `Button` (44px, radius 4px, `--accent`, Poppins 16px 500, light hover `--accent-hover`), full width, plus the spotlight, ring glow, spinner and success fill from `auth.css` |
| Category links (catalog) | The secondary `Button`'s values: 44px, radius 4px, `--surface` background, 1px `--field-line` edge drawn inside, text `--link`, Poppins 16px 500. Every button on a page is the shared `Button`; the old plain-button rule is gone (SCRUM-134) |
| Checkout button (cart) | A link with the shared `Button` classes (`ui-button`): 44px, radius 4px, `--accent`, Poppins 16px 500 |
| Coverflow arrow | 44px circle, `--surface`, 1px `--line` border, fills with `--accent` on hover |
| Login card | `Card` with `variant="form"`: max-width 420px, radius 10px, 1px `--line` border, `--shadow` |
| Product card | `Card` (16px corners, 20px padding, `--shadow-product`, no border); see Product card |
| Purchase card | Radius 10px, 1px `--line` border, `--surface`, `--shadow` |
| Cart line | Radius 0.5rem, 1px `--line` border, `--surface` |
| Coverflow row, card | Radius 12px with `--shadow`, radius 10px with `--card-shadow` at the front |
| Login input | The shared `Input`: height 44px, radius 6px, 1px `--field-line` border, 16px text |
| Purchase input | The shared `Input` (44px, radius 6px, 1px `--field-line` border, 16px text) |
| Home sort dropdown | Height 40px, min-width 190px, radius 8px, 1px `--field-line` border, 14px text |
| Focus ring | 3px outline, `--focus`. Offset 2px on inputs and on `Button`, 3px on home, 4px on catalog links |

### Shared UI components

`frontend/src/components/ui/` holds the shared parts from DESIGN.md. Each has its own CSS
file next to it, and its classes start with `ui-`. The header search uses `Button` and
`Input` (with `hideLabel`, restyled dark in `site-header.css`); the product card uses `Card` and `Button`.

| Component | Rule |
| --- | --- |
| `Button` | Primary button: at least 44px high, 4px corners, `--accent`, Poppins 16px 500, not uppercase. Light hover and pressed fill `--accent-hover`; in the dark theme the fill stays `--accent` and a 4px `--ring-glow` ring shows. Focus 3px `--focus`, offset 2px. Disabled opacity 0.7. `variant="secondary"`: `--surface` fill, `--link` text, 1px `--field-line` edge drawn inside, padding 0 16px; hover `--bg` and a `--link` edge. A link uses the same classes. `unavailable` (such as "Out of stock") disables it and draws `--line` with `--muted` text at full opacity; a busy button keeps opacity 0.7 |
| `Input` | Label, input and optional field error. 44px, 6px corners, 1px `--field-line`, 16px Open Sans, padding 0 12px, focus 3px `--focus` offset 2px. With `error`: `aria-invalid`, `aria-describedby`, 2px `--danger` border, 13px 600 message. `hideLabel` keeps the label for screen readers only. `multiline` renders a textarea (at least 88px, padding 10px 12px, vertical resize); `hint` adds a `body-sm` `--muted` note (id `<id>-note`) the field points to; `errorRole` sets the message's role; read-only inputs get `--bg` and `--muted`. Login, register and profile use it. `select` renders a `<select>` with its children as options in the same 44px frame; the review form's rating uses it |
| `Card` | DESIGN.md's `card-product`: `--surface`, 16px corners, padding 20px, `--shadow-product`, flex column. `as` picks the element. `variant="form"` is the form card: 1px `--line`, `--shadow`, 10px corners, padding 36px 32px (28px 20px up to 480px); login, register and the profile cards use it. The wishlist items use the default (product) card |
| `Badge` | Discount badge: `--sale-bg`, `--sale-fg`, 3px corners, padding 4px 8px, Open Sans 14px 600 |
| `Table` | Data table (SCRUM-124). Plain `createElement` like `RequestStates.js`, so Node tests render it; the page imports `ui/Table.css`. Columns `{ key, header, render?, rowHeader?, align? }`; `th scope="col"`, `th scope="row"` for a `rowHeader` column. `loading`: skeleton rows (1.2s shimmer) in a `role="status"` box named by `loadingLabel`. No rows: `emptyText` in a `role="status"` box. Wide tables scroll inside a focusable, named region; the page never scrolls sideways |
| `Modal` | SCRUM-134. `<Modal open onClose title footer>`: a `<dialog>` opened with `showModal()`, `aria-modal`, named by its title. The parent owns `open`; Escape, the 44px close button and a click on the `--backdrop` veil call `onClose`. Focus starts on the first field or action, Tab stays inside (`modal-focus.js`, tested in Node) and returns to the opener. Form card look (`--surface`, 1px `--line`, 10px, `--shadow-menu`), up to 480px wide. Panel rises 8px in 200ms; none with reduced motion. No page uses it yet |
| `Toast` | SCRUM-134. `const toasts = useToasts(); toasts.show(text, { tone, duration })` and one `<ToastRegion toasts={toasts.items} onDismiss={toasts.dismiss} />`. Tones info, success, error (3px `--accent`, `--ok-fg`, `--danger` line on a `--surface` card). Bottom right, up to 360px, at most three. Closes after 5s, paused on hover or focus; `duration: 0` stays. Polite live region; an error's text is `role="alert"`. 44px close button. List logic in `toast-queue.js`, tested in Node. No page uses it yet |

### Header and footer

`components/Header.jsx` with `CategoryMenu.jsx` (Categories button and panel), `HeaderSearch.jsx` (search, hint,
Ctrl K) and `AccountMenu.jsx` (name pill and menu), `styles/site-header.css`, and the pure helpers
`lib/site-header.js` (scroll state, initials, list keys), `lib/search-hints.js` (hint terms and typewriter steps)
and `accountMenuLinks` / `canUseWishlist` in `lib/role-routes.js` (SCRUM-164). `components/Footer.jsx` and
`styles/site-footer.css` (SCRUM-123). DESIGN.md's Header and Footer sections hold the full rules.

| Part | Rule |
| --- | --- |
| Layout | One dark row: Siesta wordmark, Categories button, search (up to 560px), then Deliveries (product manager), wishlist, account and cart. Sticky; the bar is 72px and 56px while scrolling down (the `<header>` keeps 72px in the flow). Up to 1024px the Categories label is visually hidden and the pill shows only the avatar; up to 768px the search is a second row and the height stays; up to 480px Categories is a 44px icon |
| Categories panel | `#category-button` (`aria-expanded`, `aria-controls="category-panel"`) opens a full-width panel with a scrim (in `<body>`). Left: every top-level category of `GET /api/categories` as buttons (one Tab stop; Up/Down/Home/End; focus activates; hover activates after 150ms; Enter or click opens `/products#category-<id>`; on touch the first tap on another item only activates it), a sliding highlight pill and "All products". Middle: "All <category>" and its subcategories. Right (from 1025px): the category's best seller from `GET /api/products?categoryId=<id>&sort=popularity&pageSize=1`, asked only while open and cached per category; no product, no card. Escape or the scrim close it and refocus the button |
| Search | `Input` (44px, 10px corners, `--field-line` edge, `--header-accent` focus edge and ring) and a `--header-accent` submit `Button`; opens `/products?search=<text>`, the product list passes `search` to `useCatalog`. A typing hint "Search <term>" (`aria-hidden`) shows while empty and unfocused; the placeholder stays for screen readers. Ctrl+K / Cmd+K focuses it; a "Ctrl K" chip shows from 769px |
| Account area | Guest: `#auth-link` Login. Signed in: the name pill `#user-name` ("Account menu, <name>"), which opens the account menu joined under it: My orders, Wishlist, Profile for a customer; the panel and shortcuts for staff; then `#logout` "Sign out" |
| Wishlist, cart | 44px round icons for guests and customers. Cart count in a `--header-accent` bubble with `--header` text (hidden at 0). The count reloads on page change, login, logout and the `cart-changed` event that `cart-api.js` sends after every change |
| Current page | 2px `--header-accent` line (inset shadow) and `aria-current="page"` |
| Footer | `--footer` background, store name "Siesta", Products / Cart / Orders (Cart and Orders for guests and customers), copyright in `--footer-fg-soft`. Links `--footer-fg`, underlined, 44px high. No support link until the customer chat exists |
| Requests | The header and the page share one `GET /api/categories` when they load together (`fetchCategories`). The hint needs no request; the featured card asks only while the panel is open |

#### Header motion

`--ease-standard` everywhere; the header's own steps are custom properties on `.site-header`.
`prefers-reduced-motion: reduce` turns every header animation and transition off, and the hint shows one term.

| Effect | Duration | Delay |
| --- | --- | --- |
| Bar height 72 ↔ 56px | 240ms | — |
| Categories chevron turns 180° | 200ms (`--dur-fast`) | — |
| Panel reveal (`clip-path` from the top) | 260ms | — |
| Scrim fade | 240ms | — |
| Highlight pill `top` | 240ms | — |
| Panel links: 8px up and a fade | 240ms | index × 35ms |
| Link hover: `--header-fg`, 4px to the right | 200ms | — |
| Featured card: 4px rise, image zoom 1.06 | 320ms (`--dur-base`) | — |
| Search focus edge and ring | 200ms | — |
| Hint: type, hold, delete | 65ms a character, 1.5s, 28ms a character | — |
| Caret blink | 1s | — |
| Name pill width 44 → 210px | 320ms | — |
| Name fades in from 6px to the left | 240ms | 80ms |
| Account menu reveal (`clip-path`) | 200ms | — |
| Account menu entries: 6px down and a fade | 220ms | 60ms + index × 50ms |

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

### Product list data (SCRUM-165)

The category sections of `/products` (sidebar, sections with their own sort, search results) are built from the
existing endpoints; the API contract does not change. The pure logic is in `lib/catalog-sections.js`, the URL and
search helpers in `lib/catalog-query.js` and the loaders in `lib/catalog-api.js`. The page itself still uses the
SCRUM-109 layout until the sidebar and sections land.

| Part | Rule |
| --- | --- |
| Requests | `GET /api/categories` (shared with the header) and every product matching the search, brand and price: `fetchMatchingProducts` asks `GET /api/products?…&page=<n>&pageSize=100` (the API maximum) and follows `totalPages`; today's catalog is one request. It sends no `categoryId`, so the sidebar can count every category. A total that changes between pages, or a product seen twice, fails the load ("Try again") |
| Why not one request per section | `?categoryId=<id>` includes every subcategory, and parent categories have products of their own (Phones and Smartphones), so per-section totals would count products twice; the sidebar needs a count for every category, and "In stock only" has no API parameter |
| Sections | `buildSections`: one per category that has products, holding the products whose own category it is, in tree order (a top-level category, then the categories below it, as the API orders them). A chosen category shows itself and every category below it (`categoryScope`) |
| Sidebar counts | `categoryCounts`: a category counts its own products and those of every category below it. `sidebarCategories` lists the top-level categories, and the categories below the chosen one's top-level category only there; during a search, categories without a match are hidden, except the chosen one and its parents |
| In stock only | `inStock=1` in the URL; `filterInStock` applies it after loading. Out-of-stock products are otherwise always listed and found by search |
| Per-section sort | `SECTION_SORTS`: Name A to Z (the API's own order), Price low to high, Price high to low, Popularity. `sortSection` keeps the loaded order as the base: price sorts are stable on it, as the API breaks ties by name then id; Popularity uses the order of `GET /api/products?sort=popularity` with the same filters (`fetchPopularityOrder`, asked once, when a section first picks it). So every order matches the server's |
| View all | A section shows `SECTION_PREVIEW` (3) products; `sectionToggleLabel` gives "View all N" or "Show less", nothing for 3 or fewer |
| Heading | `catalogHeading`: "All products", the chosen category, or a search (`“query” search results` and "N results"). The breadcrumb is Home / All products / the category's parents / the category or the quoted query, the last item unlinked; links use `?categoryId=<id>` |
| Status and chips | `sectionsStatus`: "N products in M sections" (no search). `activeFilterChips`: category, brand, price, in stock, each with the URL parameters it removes (`removeCatalogParams`) |
| Price box | `applyPriceRange`: empty bounds are removed, a comma is a decimal point, negative or non-numeric values and a minimum above the maximum are refused with a message, so the API never gets a range it answers 400 to |
| Search | `searchFieldReducer`: typing changes only the field; the results change on submit (button or Enter) or on clear. `searchSubmitPath` opens a fresh `/products?search=<text>` (other filters are dropped), at most 100 characters |
| Old links | `queryForCategoryHash` turns `/products#category-<id>` into `?categoryId=<id>` |

### Loading, empty and error states

`components/RequestStates.js` and `styles/request-states.css` hold the shared parts.

| State | Rule |
| --- | --- |
| Loading | `LoadingState`: `role="status"`, a short label such as "Loading your cart…". The product list has its own status line, "Loading products…" |
| Error | `ErrorState`: `role="alert"`, a safe message from `normalizeRequestError`, and a "Try again" button (the shared secondary `Button` classes) when the page passes `onRetry`. It uses `--danger`, `--danger-bg` and `--danger-fg` |
| Empty | Plain text. "Your cart is empty." with a link to the products, "No deliveries yet.", "No products available in this category yet." on the product list, "No products in this category yet." on a home row, "No products available yet. Please check back soon." when the catalog has nothing |

## Admin panel

`pages/AdminPanelPage.jsx`, `components/admin/` (`AdminShell`, `KpiCard`, `ReviewApprovalRow`),
`styles/admin.css`, and the pure helpers `lib/admin-menu.js`, `lib/admin-api.js`, `lib/count-up.js`,
`lib/approval-row.js`, `lib/moderation-api.js` and `lib/motion.js` (SCRUM-124, SCRUM-141). It replaces the SCRUM-130 landing page at `/manage`;
DESIGN.md's Admin panel section holds the rules.

| Part | Rule |
| --- | --- |
| Routes | `/manage` (Overview) and `/manage/<section>`. Product manager: `deliveries`, `reviews`, `low-stock`. Sales manager: `invoices`, `discounts`. Another role's section sends back to `/manage`; other roles get the `PurchaseAccess` message |
| Data | `GET /api/product-management/dashboard` (one request serves Overview and Low stock), the moderation API for Reviews (below), `GET /api/product-management/deliveries` (the Deliveries table, read-only; status changes stay on `/deliveries`), `GET /api/sales/dashboard` for the last 30 days |
| Menu | 240px from 768px up; below 768px a 44px "Menu" disclosure (`aria-expanded`), closed by a choice or Escape. Hover: 3px `--accent` inset line; current page: the line, `aria-current="page"` and `color-mix(in srgb, var(--accent) 10%, var(--surface))` |
| KPI cards | `Card`; product manager: low-stock products, pending deliveries, reviews to approve; sales manager: gross revenue, refunds, net revenue, active discounts. tr-TR numbers, TRY amounts. A list at the API's cap says "The first N are shown." |
| Review moderation | Since SCRUM-141 the Reviews section uses the moderation API (`docs/reviews.md`): `GET /api/product-management/comments?status=Pending&afterId=…`, 20 a page, oldest first, "Load more reviews" with `nextAfterId`, rows merged by ID. A row shows product, author, stars ("No rating" when none), text and date, as React text only. Approve (primary) and Reject (secondary: `--surface` fill, `--link` text) `POST …/comments/{id}/decision` with `X-CSRF-TOKEN`; the row changes only after the 200, both buttons are blocked while one waits (15s limit). 401 ends the session; 403 says a product manager account is required; 404 closes the row; 409 says another manager decided first and reloads the list, never retrying; 400 and network errors leave the row with a message. A decided comment is dropped from the dashboard at once and the dashboard reloads, so the Overview stays in step |

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
| Approve: width 110px to 44px, `--ok` fill, 22px corners (Reject: the same with a `--muted` fill) | 200ms | after the server's 200 | ease |
| Approve: tick drawn (`stroke-dashoffset`; Reject draws a cross) | 250ms | 120ms | ease |
| Decided row waits (buttons blocked, `aria-label` "Review approved" or "Review rejected"; the other button is hidden) | 800ms | 0 | — |
| Row closes: opacity, `max-height`, `min-height`, `border-top` to 0, then removed | 250ms | 0 | ease |

With reduced motion the KPI value is final at once, the tick and green show at once, and the row goes
800ms later without a fade. When the removed row held the focus, it moves to the next row's Approve
button, or to the heading when the list is empty.

## Customer profile

`pages/ProfilePage.jsx`, `components/ProfileStates.js`, `lib/profile-form.js`, `lib/customer-profile-api.js` and
`styles/profile.css` (SCRUM-103). The page has both `auth-page` and `profile-page`, so `auth.css` gives it the
heading, banner, button and motion; the cards are `Card` (form) and the fields `Input` (SCRUM-134); `profile.css`
only adds the column, the gap between the cards and the note. DESIGN.md's Customer profile section holds the rules.

| Part | Rule |
| --- | --- |
| Route | `/profile`, customers only (`PurchaseAccess`, `role-routes.js`). A guest goes to `/login?returnUrl=…`; another role gets the role message, as on `/dashboard`. The dashboard's "Your details" has an "Edit details" link here; the header's account menu (Profile) opens `/dashboard` |
| Data | `GET /api/customer/profile`, then `PUT` with `{ name, taxId, homeAddress }` (trimmed) through `saveCustomerProfile`, which renews a refused CSRF token once (`withCsrfRetry`). The email is shown read-only and never sent |
| Form | "My profile", "These details appear on your invoice and delivery.", card "Account" (Full name, Email), card "Invoice and delivery" (Tax ID (TC kimlik no), Home address, 3 rows), "Save changes". `useAuthForm` with `redirectSignedIn: false` and the labels "Saving…" and "Saved" |
| Validation | Empty or too long: name 100, tax ID 64, address 1000 characters after trimming. The register page's TC kimlik check digits are not applied, so an older tax ID does not block a name change. Messages sit under the field with `role="alert"`, `aria-invalid` and `aria-describedby`; the fields shake and the first one gets focus, after `flushSync` renders the messages. A 400 from the server is mapped to its field; only the backend's known messages are shown, anything else reads "Check this field." |
| States | "Loading your profile…" (`role="status"`), a load error with "Try again", 401 back to login (`useSessionExpiry`/`expireSession`), 429, 503 and network errors in `useAuthForm`'s banner, 409 "Your profile was changed somewhere else at the same time. Please try again." |
| Incomplete profile | With a blank tax ID or address the note "Add your tax ID and home address before you pay." (`role="status"`) is shown and those fields stay empty |
| After a save | `role="status"` "Your changes are saved."; with a safe `returnUrl` (`safeReturnUrl`, not `/` or `/profile`) a link to it, "Continue to checkout" for `/checkout`, which takes the focus. The header's name follows at once (`renameAccount`) |
| Checkout | When `GET` or `POST /api/customer/checkout` answers 409 "Complete your customer profile before checkout.", checkout opens `/profile?returnUrl=%2Fcheckout`. The title is only compared (`needsCompleteProfile` in `purchase-api.js`), never shown; other 409s keep "Review your cart" |

### Profile motion

| Effect | Duration | Delay | Curve |
| --- | --- | --- | --- |
| Title, text, note, card titles, fields, button: `auth-reveal-up` in their masks | 0.85s | 120ms + i × 110ms | `cubic-bezier(0.22, 1, 0.36, 1)` |
| Card `profile-card-in`: 24px from below, opacity 0 to 1 | 0.85s | 120ms + card × 140ms | `cubic-bezier(0.22, 1, 0.36, 1)` |
| Shake, spotlight, spinner | As on login | — | — |
| Save: `--ok` fill, then the check and "Saved" | 0.95s, then 1.8s | — | As on login |

With reduced motion nothing animates, the masks come off and the check shows at once without the fill.

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
