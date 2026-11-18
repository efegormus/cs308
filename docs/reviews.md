# Product ratings and comments (SCRUM-80)

Course Project requirement 5: only a customer who purchased and received a product
may review it. Ratings are public immediately. Written comments start Pending;
only Approved comments are returned publicly. The moderator API belongs to
SCRUM-81, its admin screen to SCRUM-141, and the full customer form to SCRUM-137.
The product detail page already reads the public average supplied here.

## API for SCRUM-137

All IDs are JSON integers, dates are UTC ISO-8601 strings, and responses use
camelCase. Public and private review responses have `Cache-Control: no-store`.

* `GET /api/products/{productId}/reviews?afterId=0` is public. Returns
  `{productId, averageRating, ratingCount, comments, nextAfterId}`.
  An unrated product has `averageRating: null`, `ratingCount: 0`. The average/count
  cover all ratings, independently of the comment page. Only approved comments
  appear, each `{id, authorName, body, createdAt}`. Comments are ordered by ascending
  ID, 20 per page. Use `nextAfterId` until null. Unknown/inactive product: 404;
  negative cursor: 400. No private email, address, tax, moderation or session fields.
* `GET /api/customer/products/{productId}/review` requires Customer. Returns
  `{productId, eligible, orderItemId, rating, comment, canRate, canComment}` for the
  signed-in customer. `comment` is null or `{id, body, status, createdAt}`, where
  status is Pending/Approved/Rejected. Other customers cannot inspect this record.
  A not-yet-eligible customer gets 200 with false eligibility flags. Staff get 403;
  unauthenticated callers get 401. Display Pending as “Waiting for approval”.
* `POST /api/customer/products/{productId}/review` requires Customer and the
  identity's CSRF cookie plus `X-CSRF-TOKEN`. Body:
  `{ "rating": 5, "comment": "Arrived safely." }`.
  Either component may be omitted/null, but at least one is required. Rating is an
  integer 1–5; a supplied comment must be non-blank and at most 2000 trimmed UTF-16
  characters. Unknown fields (including userId/orderItemId/status) are rejected.
  The server chooses the eligible delivered order line; client ownership claims
  are never trusted. New data returns 201 and the customer review response.
  Exact retries return 200 without duplicates. One rating and one comment per
  customer/product: either may be submitted first, then the missing component.
  Changing an already submitted component returns 409, with no partial insertion.
  Editing/deletion are outside this version; do not auto-retry a conflict.

Submission requires a delivered order with a purchase timestamp, delivery
timestamp, and a positive delivered quantity on the owning customer's product
line. Confirmed and subsequently refunded purchases count as purchases; refunding
a received product does not erase the right to review it. Processing, InTransit,
Cancelled, unpaid and another customer's lines cannot authorize a submission.
Existing reviews remain visible after inventory or profile changes.

Use the existing order receipt's line product IDs to fetch per-product eligibility
from order history. `canRate` and `canComment` control the two form components.
After submitting, refresh both the private response and public summary. Render
comment/author values as React text, never injected HTML. Do not interpret user
text as markup. Public comments contain only approved text; the private response
is the source for an author's waiting-for-approval message.

## Integrity and tests

MySQL row-locks the customer before checking/inserting reviews; existing unique
user/product keys also prevent duplicates. Rating plus comment are atomic.
Timestamps are normalized to MySQL microseconds. In-memory tests cover validation,
eligibility, ownership, moderation filtering, paging and retries. Real MySQL tests
cover identical/conflicting concurrent submissions and real cookie/CSRF HTTP
requests, including all three denied staff roles and public/private isolation.

Run `dotnet test cs308.sln` with `ONLINESTORE_TEST_MYSQL` configured for a disposable
local `_tests` database. Without it, MySQL tests skip and do not prove integrity.
`npm test` in frontend covers public response validation and safe error handling.
