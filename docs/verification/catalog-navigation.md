# SCRUM-146 navigation regression coverage — 26 November 2026

Category links now share categoryProductsPath: selecting a category fetches its
inventory rather than scrolling to a partial global page. Tests verify pagination
reset, retained search/price/brand/sort controls, immutable saved parameters,
refreshable URLs and invalid IDs. All 293 frontend tests pass. The published
55-product MySQL catalog browser check covers off-first-page categories, legacy
hash clearing, genuine empty states, sorting, refresh and mobile overflow.
