# Product ratings and comments (SCRUM-80)

Course Project requirement 5: only a customer who purchased and received a product
may review it. Ratings are public immediately. Written comments start Pending;
only Approved comments are returned publicly. The moderator API is SCRUM-81
(section below), its admin screen is SCRUM-141, and the full customer form is
SCRUM-137. The product detail page already reads the public average supplied here.

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

## Moderation API for SCRUM-141 (SCRUM-81)

Only the ProductManager role can call these. Guests get 401. Customers, SalesManager and
SupportAgent get 403. Responses have `Cache-Control: no-store`. They are the only moderation
endpoints, so SCRUM-141 should not add its own.

* `GET /api/product-management/comments?status=Pending&afterId=0` returns
  `{status, comments, nextAfterId}`. `status` is `Pending` (default), `Approved` or
  `Rejected`, case sensitive. Comments are ordered by ascending ID (oldest first), 20 per
  page. Use `nextAfterId` until it is null. An unknown status or a negative cursor gives 400.
  Each comment is `{id, productId, productName, authorName, rating, body, status, createdAt,
  moderatedAt, moderationNote}`. `rating` is the author's own star rating or null. It has no
  e-mail, address, tax ID or other account data.
* `POST /api/product-management/comments/{commentId}/decision` needs the identity's CSRF
  cookie plus `X-CSRF-TOKEN`. Body: `{ "decision": "Approve", "note": "optional" }`.
  `decision` is `Approve` or `Reject`. `note` is optional, at most 500 characters, and is
  stored trimmed (blank becomes null). Unknown fields, such as `status`, are rejected with 400.
  Success returns 200 and the updated comment in the same shape as the list.

Behaviour the screen can rely on:

* Only a Pending comment changes. It records the moderator, the time and the note.
* Repeating the same decision returns 200 and the stored comment unchanged. Use it to retry a
  request whose answer was lost.
* The opposite decision on a decided comment returns 409. Another manager decided it first, so
  refresh the list. Do not retry automatically.
* An unknown comment ID returns 404. A missing CSRF token or an invalid body returns 400.
* The decision changes comments only. Ratings and the public average and count never change.
* An approved comment appears in `GET /api/products/{id}/reviews`. A rejected one never does.
  The author's private response shows the new status, and the UI shows Pending as
  "Waiting for approval".
* The list can be filtered by `status`, so the same screen can show decided comments.
* Not in this version: changing a decision after it was made, editing a comment, and
  e-mailing the author about the result.

## Integrity and tests

MySQL row-locks the customer before checking/inserting reviews; existing unique
user/product keys also prevent duplicates. Rating plus comment are atomic. A moderation
decision row-locks the comment (`SELECT ... FOR UPDATE`), so two managers deciding at the same
time take turns: one wins and the other gets 409, or 200 if it is the same decision.
Timestamps are normalized to MySQL microseconds. In-memory tests cover validation,
eligibility, ownership, moderation filtering, paging and retries. Real MySQL tests
cover identical/conflicting concurrent submissions and real cookie/CSRF HTTP
requests, including all three denied staff roles and public/private isolation.

Run `dotnet test cs308.sln` with `ONLINESTORE_TEST_MYSQL` configured for a disposable
local `_tests` database. Without it, MySQL tests skip and do not prove integrity.
`npm test` in frontend covers public response validation and safe error handling.

## Customer and product-manager screens (SCRUM-145)

Delivered order cards at `/orders` link to each product's `#reviews` section.
The product page shows the public average and approved comments, with cursor
paging. A Customer sees their own rating and Pending/Approved/Rejected comment;
only missing components are offered in the form. A guest sees a login link and
an undelivered purchase cannot submit. The client refreshes eligibility and the
public summary after success, keeps failed mutations explicit, and clears private
views when the session expires or the identity/product changes.

Product managers use `/manage/reviews`. `GET /api/product-management/comments?afterId=0`
uses the shared SCRUM-81 queue contract `{status,comments,nextAfterId}`, 20 pending
comments per page. Each comment contains `id`, `productId`, `productName`,
`authorName`, nullable `rating`, `body`, `status`, `createdAt`, moderation metadata
and an opaque string `version`. The React client maps authorName to its customer
column. No email, tax ID or address is exposed.
`POST /api/product-management/comments/{id}/moderation` requires ProductManager
and CSRF, with `{status:"Approved"|"Rejected",version}`. It returns `{id,status,version}`.
Both this versioned UI action and SCRUM-81’s `/decision` action share
`ModerationService`; there is one queue route and one locking/decision implementation.
A row lock and version check prevent competing decisions from overwriting each
other. An exact repeated decision succeeds; an opposite terminal decision or
stale pending version returns 409. Only Approved text becomes public. Ratings
remain public for both decisions. Moderation records the acting manager/time.

`ReviewMySqlTests` checks real cookie/CSRF/role boundaries and concurrent opposing
decisions. `verify_purchasing_browser.py` exercises real delivered purchases,
customer submissions, immediate ratings, both moderation decisions and public/
private text isolation against MySQL; `verify_admin_browser.py` additionally uses
a controlled queue for focused loading/row-removal/empty-state UI cases.
