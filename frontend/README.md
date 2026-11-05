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
| `src/components/` | `Layout`, `Header` and `Footer` |
| `src/auth/AuthProvider.jsx` | `AuthProvider` and the `useAuth()` hook |
| `src/lib/auth-utils.js` | Pure helpers such as `safeReturnUrl` and `loginHref` |
| `src/pages/` | One component per route |
| `src/styles/` | The CSS |

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
