# Online Store frontend

React and Vite, in JavaScript. Needs Node.js 22.22 or later.

## Run it

```sh
npm install
npm run dev
```

The dev server is at http://localhost:5173. It sends `/api` requests to the backend on
http://localhost:5200, so start the backend first. See
[local development](../docs/local-development.md).

## Build and test

```sh
npm run build
npm test
```

`npm run build` writes to `dist/`. `npm test` runs the unit tests with Node's built-in
test runner.

## Where things are

| Path | What it holds |
| --- | --- |
| `src/App.jsx` | The routes |
| `src/components/` | `Layout`, `Header` and `Footer`, and the product list parts `ProductCard`, `CategorySection` and `CategoryNav` |
| `src/auth/AuthProvider.jsx` | `AuthProvider` and the `useAuth()` hook |
| `src/hooks/useCatalog.js` | `useCatalog()`, which loads the categories and products and takes the search and sort values |
| `src/lib/auth-utils.js` | Pure helpers such as `safeReturnUrl` and `loginHref` |
| `src/lib/catalog-api.js` | The catalog API client: `fetchCatalog`, `fetchProducts`, `fetchCategories` and `fetchProduct` |
| `src/lib/product-display.js`, `src/lib/category-display.js` | Pure helpers for prices, image URLs, stock text, category ids and count labels |
| `src/pages/` | One component per route |
| `src/styles/` | The CSS |

## The product list

`/products` shows the products grouped by category. A link such as `/products#category-3` scrolls to that
category. The page calls `useCatalog()`, and the catalog loads again whenever the values it receives change:

```js
useCatalog({ search: "pixel", sort: "price", order: "desc", categoryId: 2 });
```

`sort` is `price` or `popularity` and `order` is `asc` or `desc`, as in `GET /api/products`. The search bar and the
sort dropdown (SCRUM-67) go where `ProductsPage` marks them. The API client throws a `CatalogError` with the HTTP
status when a request fails. Out-of-stock products stay in the list with an Out of stock label.

## Styles

All CSS files load at once, so a global rule would reach every page. Colors, fonts and
motion are described in [UI design](../docs/ui-design.md).

- `auth.css` holds the color tokens, the header and the login page. Its rules are scoped
  to `.auth-page` or the header ids.
- `products.css`, `purchases.css` and `home.css` are scoped to `.catalog-page`,
  `.purchase-page` and `.home-page`. Give a new page its wrapper class, and start every new
  selector in these files with it.
- `layout.css` holds the page frame.

Until the old pages are removed, `OnlineStore/wwwroot/css/auth.css` is a second copy of
`auth.css`. A change to a color has to go in both.
