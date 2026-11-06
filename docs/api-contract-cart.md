# Shopping cart API contract (SCRUM-49)

Guests and customers use the same routes. All answers are JSON (camelCase) and are sent with
`Cache-Control: no-store`. Staff accounts (sales manager, product manager, support agent) are not
shoppers and receive `403`.

## Who owns the cart

- **Customer (logged in):** the cart belongs to the account (`Carts.UserId`). One open cart per user.
- **Guest:** the first successful add creates a cart and sets the HttpOnly cookie `OnlineStore.Cart`
  (random 64-character token, `SameSite=Lax`, 30 days, `Secure` outside Development). Only the
  SHA-256 hash of the token is stored (`Carts.GuestTokenHash`). A missing, malformed or unknown
  cookie behaves like an empty cart; reading the cart never creates one.
- Moving a guest cart into the account cart after login is `POST /api/cart/merge` (SCRUM-100, below).

## Concurrency (matches checkout, `docs/purchasing.md`)

On MySQL every add/remove runs in a ReadCommitted transaction. A customer's row is locked first,
then the cart row (`SELECT ... FOR UPDATE`), and only an `Open` cart is changed. This is the same
order checkout uses, so a purchase and a cart change for the same cart cannot interleave. A cart may
hold at most 100 different products, the limit checkout accepts. The in-memory database used by the
unit tests has no row locks, so the locking itself is only exercised against real MySQL.

## Endpoints

| Endpoint | Access | Result |
| --- | --- | --- |
| `GET /api/cart` | Guest or customer | `200` cart (empty when there is none). |
| `POST /api/cart/items` | Guest or customer + CSRF | Body `{ "productId": 10, "quantity": 2 }`. `200` updated cart. Adding a product already in the cart increases that line. |
| `PUT /api/cart/items/{productId}` | Guest or customer + CSRF, body `{ "quantity": 1..99 }` | `200` updated cart. Sets the quantity of a line already in the cart (the cart page's minus and plus buttons). `404` if the product is not in the cart, `409` if the product is inactive or the quantity is above the stock. Empty a line with `DELETE`, not with `0`. |
| `DELETE /api/cart/items/{productId}` | Guest or customer + CSRF | `200` updated cart. Removes the whole line; a product that is not in the cart is not an error. |
| `POST /api/cart/merge` | Customer + CSRF, no body | `200` `{ cart, merged, conflicts }`. Moves the guest cart (found through the `OnlineStore.Cart` cookie) into the account cart. |

Every `POST`/`DELETE` needs the CSRF header (`GET /api/auth/csrf`, header `X-CSRF-TOKEN`), as in
`docs/authentication.md`. Fetch a fresh token after login or logout.

### Cart response

```json
{
  "items": [
    { "productId": 10, "name": "Phone Ten", "unitPrice": 100.50, "quantity": 2,
      "stockQuantity": 5, "imageUrl": null, "lineTotal": 201.00, "available": true }
  ],
  "itemCount": 2,
  "total": 201.00
}
```

`available` is false when the product was deactivated or its stock is now lower than `quantity`.
Adding to the cart never reduces stock; stock is checked again at checkout (SCRUM-72).

### Merging the guest cart at login (SCRUM-100)

The React auth provider calls `POST /api/cart/merge` after login/bootstrap (`docs/react-hosting.md`), after the
session and the new CSRF token exist. The redirect can cut the request off, so it is safe to repeat: with no
guest cart it only returns the account cart (`merged: false`).

- The customer has **no open cart:** the guest cart becomes the account cart.
- The customer **has an open cart:** quantities of the same product are added, other lines are moved.
  A quantity above the stock is **kept**, not cut, and reported; checkout checks the stock again.
- The account cart already has 100 lines: the extra lines **stay in the guest cart** and are reported
  (`CartFull`). The cookie is kept, so a later merge can try again.
- When every line moved, the guest cart is set to `Abandoned` and the cookie is deleted.
- Locks follow checkout: customer row, the account cart, then the guest cart.

```json
{
  "cart": { "items": [], "itemCount": 0, "total": 0 },
  "merged": true,
  "conflicts": [
    { "productId": 11, "name": "Phone Eleven", "reason": "NotEnoughStock", "quantity": 2, "stockQuantity": 1 }
  ]
}
```

`reason` is `Unavailable` (product deactivated), `NotEnoughStock` or `CartFull`. A staff account gets `403`,
a guest `401`, a customer with more than one open cart `409` (merge required, the guest cart is untouched).

### Errors (`application/problem+json`)

| Status | When |
| --- | --- |
| `400` | `quantity` not between 1 and 99, `productId` not positive, unknown JSON property, or missing/invalid CSRF token. |
| `403` | A staff account calls the cart. |
| `404` | The product does not exist or is not active. |
| `409` | The product is out of stock; the cart would hold more units than are in stock; the cart already has 100 different products; the customer has more than one open cart (merge required, as in `docs/purchasing.md`); or the cart changed while locking. The cart is left unchanged. |
| `503` | Database failure (generic message, no internal details). |
