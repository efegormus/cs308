# Sprint 2 purchasing MVP

Sprint: SCRUM Sprint 2 (ID 3). Dates remain unchanged. The goal is a responsive
store where a guest can browse/search and keep a cart through login, complete a
mock payment, receive a PDF invoice/email and track delivery, with a customer
profile and real MySQL verification. This is the purchasing slice of the course
requirements, not a claim that all final-demo features are complete.

## Added existing work items

| Issue | Owner | Deliverable |
| --- | --- | --- |
| [SCRUM-58](https://sabancics308project.atlassian.net/browse/SCRUM-58) | Efe Görmüş | Core transaction, lifecycle, stock, snapshot and permission rules |
| [SCRUM-72](https://sabancics308project.atlassian.net/browse/SCRUM-72) | Efe Görmüş | Authenticated checkout/confirmation wiring, atomic stock and order snapshots |
| [SCRUM-73](https://sabancics308project.atlassian.net/browse/SCRUM-73) | Taha Ünal | Mock payment form/gateway, safe validation and idempotency |
| [SCRUM-74](https://sabancics308project.atlassian.net/browse/SCRUM-74) | Rümeysa Şahin | Immutable, authorized PDF invoice and confirmation display |
| [SCRUM-75](https://sabancics308project.atlassian.net/browse/SCRUM-75) | Taha Ünal | Retry-safe invoice email with a testable transport |
| [SCRUM-76](https://sabancics308project.atlassian.net/browse/SCRUM-76) | Onur Utku Batmaz | Owner-only order history and invoice/status links |
| [SCRUM-79](https://sabancics308project.atlassian.net/browse/SCRUM-79) | Efe Görmüş | Product-manager delivery list and guarded status updates |
| [SCRUM-100](https://sabancics308project.atlassian.net/browse/SCRUM-100) | Onur Utku Batmaz | Transactional, idempotent guest-cart retention/merge |
| [SCRUM-103](https://sabancics308project.atlassian.net/browse/SCRUM-103) | Altan Kemer | Private profile screen and completion of checkout details |

Existing estimates are preserved; the owners should size remaining unestimated
work during planning. These assignments do not imply the nine features are
already implemented. SCRUM-97 remains Tuna's schema implementation and is a
dependency; its first integration slice must support checkout, payments,
invoices/email state and deliveries. The existing shared auth/customer/order
types must be extended, not recreated in parallel.

## Integration order

1. Agree SCRUM-58 and merge/reconcile the requirements-alignment branch with
   SCRUM-97. Keep incremental migrations compatible with databases already used.
2. Finish the shared responsive UI, canonical catalog search/sort/detail, guest
   cart and login UI already in Sprint 2 (45/47/49/50/51/56/67/68/69/70/98).
3. Wire guest-cart retention (100), required profile details (103) and the shared
   account cookie/CSRF contract. Do not build another identity store.
4. Integrate payment (73) with checkout (72); verify transactional stock guards,
   immutable price/cost/customer/address snapshots and duplicate-request behavior.
5. Add invoice/email (74/75), owner history (76) and manager delivery updates (79).
6. Extend SCRUM-55's catalog smoke coverage through this complete browser flow,
   including failed payment, insufficient stock, duplicate checkout, forbidden
   access, PDF totals and an email test transport. Run MySQL tests explicitly.

## Acceptance rules

- Guests browse and change their own cart without a login. Customer identity
  and guest-cart ownership are server-validated; login cannot lose selections.
- Adding/merging cart lines does not permanently decrement stock. Validate on
  writes and again at checkout; explain stale/out-of-stock conflicts explicitly.
- Two requests for the last unit cannot both succeed. Payment failure or rollback
  cannot leave stock reduced or an order paid; replay cannot charge/decrement twice.
- Order history/invoices use original paid values and address snapshots. Email
  failure is retried without undoing the purchase. Private documents stay outside
  wwwroot and require ownership/authorized roles.
- Only ProductManager performs delivery transitions; the existing role groups
  and customer ownership checks remain mandatory.
- Proposed popularity for SCRUM-51 is confirmed sold quantity, with deterministic
  name/ID tie-breaking; future refund accounting must define how refunded units
  affect it. Do not mix payment and fulfillment enums in that query.

## Avoid duplicate implementations

Backlog 99/102 are covered by 49/69's guest cart and totals; 101 is covered by
70's stock guard; 104 is covered by 71's existing customer fields; 105 overlaps
65's product/search tests. Keep these references rather than adding duplicate
implementations to Sprint 2. SCRUM-64 reviews already updated schema docs against
the new migrations; SCRUM-55 reuses the existing browser/MySQL checks.

SCRUM-89's return rule is corrected to 30 days from purchase, with delivery as a
prerequisite. Refund implementation remains separate from this purchasing slice.
Before the PDF's Progress Demo, also finish the required delivered-purchase
rating/comment/moderation work (80/81). External staging (61) is a separate
deployment decision; it is not necessary to prove a local purchasing flow.
