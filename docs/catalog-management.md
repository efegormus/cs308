# Product and category management (SCRUM-77)

The product manager creates, edits and removes products and categories. Course Project requirement 3 lists the fields a
product has; this API and the Catalog screen (`/manage/catalog`) keep them. Prices stay a sales manager decision after
creation (`PUT /api/sales/products/{id}/price`, docs/sales-pricing-api.md). Stock counts after creation are changed with
the stock route below (SCRUM-78, screen `/manage/stock`). Everything is under `/api/product-management`.

## Access and safety

* Only the `ProductManager` role reaches these routes. Guests get 401. Customers, SalesManager and SupportAgent get 403.
  The service checks the role and an active account again.
* Every change (POST, PUT, DELETE) needs the identity's CSRF cookie plus `X-CSRF-TOKEN`. Without it: 400.
* Request bodies reject unknown fields (400). Validation errors are 400 `application/problem+json` with the field names.
* Responses have `Cache-Control: no-store`. IDs are JSON integers.

## Categories

* `GET /categories` returns every category: `{id, parentId, name, slug, description, isActive, productCount, childCount}`.
  `productCount` counts all its products, also those off sale.
* `POST /categories` with `{name, description?, parentId?}` returns 201 and the category. The name is 1 to 100
  characters, the description at most 1000. The name is unique under the same parent (409). An unknown parent is 400. The
  slug is made from the name (`Akıllı Saatler` becomes `akilli-saatler`) and gets `-2`, `-3`, ... when it is taken.
* `PUT /categories/{id}` with `{name, description?}` returns 200. The slug and the parent do not change, so public links
  stay valid. A name taken under the same parent is 409. Unknown id: 404.
* `DELETE /categories/{id}` returns 204 only for an empty category. A category that still has products (even off sale)
  or subcategories is 409. Unknown id: 404.

## Products

* `GET /products?afterId=0` lists all products, also those off sale, by ascending ID, 50 per page. Use `nextAfterId` until
  it is null. Each product is `{id, categoryId, categoryName, brandId, sku, name, model, serialNumber, description, price,
  stockQuantity, warrantyStatus, distributorName, distributorContact, isActive}`. A negative cursor is 400.
* `POST /products` returns 201 and the product. Body (all required except `brandId`):
  `{categoryId, brandId?, sku, name, model, serialNumber, description, price, stockQuantity, warrantyStatus,
  distributorName, distributorContact}`.
* `PUT /products/{id}` returns 200. Same fields without `stockQuantity`, and with `isActive` (true or false) required.
  Send `isActive: true` to put a removed product back on sale. Send the current `brandId` to keep it, because an edit
  sets exactly what it is given.
* `DELETE /products/{id}` takes the product off sale and returns 200 with the product (`isActive: false`). Repeating it
  is safe. The row is kept, because past orders, ratings and comments point to it. The shop stops showing it (the
  public product detail is 404) and it cannot be added to a cart.

Validation: `price` is 0 to 9,999,999.99 with at most 2 decimals. `stockQuantity` is a whole number from 0 to 1,000,000.
The SKU is 1 to 64 characters without spaces and unique (409 when used). `name` up to 200, `model`, `serialNumber` and
`warrantyStatus` up to 100, `description` up to 5000, `distributorName` up to 150, `distributorContact` up to 500, all
non-blank. The category must exist and the brand must exist and be active (400).

A distributor is found by name, so all products of one distributor share one row. Saving a product with that name sets
the distributor's contact text to the latest one entered.

## Stock (SCRUM-78)

* `PUT /products/{id}/stock` with `{quantity, expectedQuantity}` returns 200 and the product (as in the list). `quantity`
  is the new count, a whole number from 0 to 1,000,000: **the stock cannot go below zero** (400). `expectedQuantity` is the
  count the manager saw on screen (required, 0 or more). CSRF and the ProductManager role are needed like for every change.
  Unknown fields are 400. Unknown product: 404. A product that is off sale can be changed too.
* The change is made only while the stock is still `expectedQuantity`, in one UPDATE. When a sale, a cancellation or
  another manager changed it in between, the answer is **409** and nothing is written. The client reads the product again and
  shows the new count. This keeps a customer's purchase from being overwritten by a stale screen.
* Sending the same change again (the answer was lost) is safe: when the stock already equals `quantity`, the answer is 200
  and nothing changes.
* Shoppers see the new count at once, because the product pages read the stock from the database on every request. At 0 the
  product is shown as out of stock and the cart API refuses it (409).
* Not in this version: a history of who changed the stock and when (see `docs/db-redesign/schema-future.sql`, StockMovements)
  and bulk changes.

## Races

Two requests that save the same SKU or the same category name at once: the unique key in the database decides. One gets
201 and the other 409. Two stock changes with the same `expectedQuantity` at once: one gets 200, the other 409. A conflict
is never retried by the client.

## Tests

`CatalogManagementTests` (in memory) covers validation, roles, conflicts, delete rules and paging.
`CatalogManagementMySqlTests` covers the real routes, the role policy, CSRF and the SKU and name races on MySQL; it runs in
the MySQL CI job and is skipped without `ONLINESTORE_TEST_MYSQL`. The screen's logic and API client are tested in
`frontend/src/lib/catalog-management*.test.js`.
