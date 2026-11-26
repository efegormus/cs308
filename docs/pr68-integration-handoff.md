# SCRUM-162 integration and handoff — 26 November 2026

PR #68 is merged: https://github.com/efegormus/cs308/pull/68
Merge commit: ccbb6d45631865ae3201ca0b9cddbe461dc3d0eb.
Both the PR head 94134c4 and merge commit have successful Backend build and test,
MySQL integration tests, Browser end-to-end, Frontend and script checks, and
Security hygiene checks on GitHub. The former conflicts were resolved before merge;
rewriting or rebasing the already-merged history is unnecessary.

Onur / SCRUM-137: build on frontend/src/components/ProductReviews.jsx and
frontend/src/lib/reviews-api.js, already merged, for eligibility, private pending
status and approved comments. The customer API is /api/customer/products/{id}/review;
public reviews use /api/products/{id}/reviews. Do not add a second submission flow.

Altan / SCRUM-141: the merged ReviewsSection in AdminPanelPage.jsx uses
moderation-api.js and ReviewApprovalRow.jsx. The shared ModerationService backs
the unique GET /api/product-management/comments queue and /decision; the versioned
/moderation compatibility action delegates to the same decision implementation.
Do not register a second queue endpoint or duplicate moderation DTO type names.

ReviewRouteIntegrationTests now starts the actual application routing pipeline,
asserts a single queue GET and both decision routes, and verifies guest access
returns 401 rather than an ambiguous-route failure. The full suite passes 511
backend tests on real MySQL, with no skips. Additional scope in this branch is
verification/handoff only; the original feature merge remains PR #68.
