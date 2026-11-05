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
- Moving a guest cart into the account cart at login is a separate item (SCRUM-100).

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
| `DELETE /api/cart/items/{productId}` | Guest or customer + CSRF | `200` updated cart. Removes the whole line; a product that is not in the cart is not an error. |

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

### Errors (`application/problem+json`)

| Status | When |
| --- | --- |
| `400` | `quantity` not between 1 and 99, `productId` not positive, unknown JSON property, or missing/invalid CSRF token. |
| `403` | A staff account calls the cart. |
| `404` | The product does not exist or is not active. |
| `409` | The product is out of stock; the cart would hold more units than are in stock; the cart already has 100 different products; the customer has more than one open cart (merge required, as in `docs/purchasing.md`); or the cart changed while locking. The cart is left unchanged. |
| `503` | Database failure (generic message, no internal details). |
