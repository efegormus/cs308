// Client for the product manager's comment moderation API (SCRUM-141; contract: docs/reviews.md, "Moderation API for
// SCRUM-141 (SCRUM-81)"). Responses are checked before they are shown; nothing from an error body is forwarded.
const id = value => Number.isSafeInteger(value) && value > 0;
const text = value => typeof value === "string";
const instant = value => text(value) && Number.isFinite(Date.parse(value));
const optional = (value, check) => value === null || check(value);

export const COMMENT_STATUSES = Object.freeze(["Pending", "Approved", "Rejected"]);
export const DECISIONS = Object.freeze(["Approve", "Reject"]);
export const PAGE_SIZE = 20;

/** One comment as the list and the decision return it. rating is the author's own 1-5 stars or null. */
export function isModerationComment(value) {
  return id(value?.id) && id(value.productId) && text(value.productName) && text(value.authorName)
    && optional(value.rating, rating => Number.isSafeInteger(rating) && rating >= 1 && rating <= 5)
    && text(value.body) && COMMENT_STATUSES.includes(value.status) && instant(value.createdAt)
    && optional(value.moderatedAt, instant) && optional(value.moderationNote, text);
}

/** A page of the given status. The cursor must move forward, so "Load more" can never loop. */
export function isModerationPage(value, status = "Pending", afterId = 0) {
  return value?.status === status && Array.isArray(value.comments) && value.comments.length <= PAGE_SIZE
    && value.comments.every(comment => isModerationComment(comment) && comment.status === status && comment.id > afterId)
    && (value.nextAfterId === null || (id(value.nextAfterId) && value.nextAfterId > afterId));
}

async function request(path, { body, token, signal } = {}, validate) {
  signal?.throwIfAborted();
  const response = await fetch(path, {
    method: body === undefined ? "GET" : "POST", signal,
    headers: { Accept: "application/json", ...(body === undefined ? {} : { "Content-Type": "application/json", "X-CSRF-TOKEN": token }) },
    ...(body === undefined ? {} : { body: JSON.stringify(body) })
  });
  if (!response.ok) throw { status: response.status, headers: response.headers };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(data)) throw { kind: "invalid-response" };
  return data;
}

export function fetchComments({ status = "Pending", afterId = 0, signal } = {}) {
  if (!COMMENT_STATUSES.includes(status) || !Number.isSafeInteger(afterId) || afterId < 0) throw { status: 400 };
  const query = new URLSearchParams({ status, afterId: String(afterId) });
  return request(`/api/product-management/comments?${query}`, { signal }, data => isModerationPage(data, status, afterId));
}

/** Approve or Reject one comment. Resolves with the updated comment only after the server answered 200. */
export function decideComment(commentId, decision, token, { signal } = {}) {
  if (!id(commentId) || !DECISIONS.includes(decision)) throw { status: 400 };
  const expected = decision === "Approve" ? "Approved" : "Rejected";
  return request(`/api/product-management/comments/${commentId}/decision`, { signal, token, body: { decision } },
    data => isModerationComment(data) && data.id === commentId && data.status === expected);
}
