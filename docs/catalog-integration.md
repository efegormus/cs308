# Catalog integration (SCRUM-54)

The catalog now reads SCRUM-43's `/api/categories` and `/api/products` directly.
`wwwroot/js/catalog-api.js` loads both lists from the same origin and groups them
using integer category IDs, preserving empty categories and server ordering.
Description is an additive nullable product DTO field so existing cards keep
their description without a second live inventory source. `/api/catalog` remains
available for compatible clients.

Homepage phone/tablet links resolve the actual category IDs and open the matching
catalog anchor. They fall back to the working catalog page if the categories
request fails; no sample inventory is embedded in the page. Both pages are served
by ASP.NET Core, so no configurable external API host or CORS allowance is needed.

The page has loading/empty/error/retry states, a 15-second request timeout, and
clears partial results if either request fails. If concurrent category changes
make the two responses inconsistent, it offers a retry instead of losing products
silently. Prices use TRY, out-of-stock products stay visible, text uses textContent,
and invalid/missing images have fallbacks. Cart operations are still SCRUM-49/70.

## Reproduce verification

Apply migrations, seed and start the backend using the
[local setup](local-development.md). `python3 scripts/dev.py check` verifies the
real database-backed APIs. For focused browser integration verification:

```sh
python3 -m venv .local/browser-venv
.local/browser-venv/bin/pip install -r scripts/requirements-browser.txt
.local/browser-venv/bin/playwright install chromium
.local/browser-venv/bin/python scripts/verify_catalog_browser.py --expect-seeded
```

Alternatively pass `--browser-executable` pointing to an installed Chrome binary.
The first part runs with **no mocked responses**: it checks live categories and
products, category navigation, per-category cards, sold-out stock and mobile
overflow. `--expect-seeded` additionally requires the fresh sample's 2/10 counts.
Omit that flag when testing a database containing other inventory.

Only the later sections intercept HTTP responses to exercise loading, empty,
partial 503, malformed data, network failure, retry, safe rendering and image
fallback cases. These cases do not modify the database. SCRUM-55 can reuse this
verification while building the broader end-to-end smoke suite.
