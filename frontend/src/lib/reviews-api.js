const id = value => Number.isSafeInteger(value) && value > 0;

export function isProductReviews(value) {
  return id(value?.productId) && Number.isSafeInteger(value.ratingCount) && value.ratingCount >= 0
    && (value.ratingCount === 0 ? value.averageRating === null
      : Number.isFinite(value.averageRating) && value.averageRating >= 1 && value.averageRating <= 5)
    && (value.nextAfterId === null || id(value.nextAfterId))
    && Array.isArray(value.comments) && value.comments.length <= 20
    && value.comments.every(c => id(c?.id) && typeof c.authorName === "string"
      && typeof c.body === "string" && typeof c.createdAt === "string" && Number.isFinite(Date.parse(c.createdAt)));
}

export async function fetchProductReviews(productId, { signal, afterId = 0 } = {}) {
  if (!id(productId) || !Number.isSafeInteger(afterId) || afterId < 0) throw { kind: "invalid-request" };
  signal?.throwIfAborted();
  const response = await fetch(`/api/products/${productId}/reviews?afterId=${afterId}`,
    { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status };
  let data;
  try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isProductReviews(data) || data.productId !== productId) throw { kind: "invalid-response" };
  return data;
}

export function isCustomerReview(value) {
  return id(value?.productId) && typeof value.eligible === "boolean" && typeof value.canRate === "boolean"
    && typeof value.canComment === "boolean" && (value.orderItemId === null || id(value.orderItemId))
    && (value.rating === null || Number.isInteger(value.rating) && value.rating >= 1 && value.rating <= 5)
    && (value.comment === null || id(value.comment?.id) && typeof value.comment.body === "string"
      && ["Pending", "Approved", "Rejected"].includes(value.comment.status));
}

async function read(response, validate) {
  if (!response.ok) throw { status: response.status };
  let value; try { value = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!validate(value)) throw { kind: "invalid-response" };
  return value;
}

export async function fetchCustomerReview(productId, { signal } = {}) {
  if (!id(productId)) throw { kind: "invalid-request" };
  return read(await fetch(`/api/customer/products/${productId}/review`, { signal, headers: { Accept: "application/json" } }),
    value => isCustomerReview(value) && value.productId === productId);
}

export function reviewInput(rating, comment, eligibility) {
  const input = {};
  if (eligibility?.canRate && rating !== "") {
    const number = Number(rating);
    if (!Number.isInteger(number) || number < 1 || number > 5) return null;
    input.rating = number;
  }
  if (eligibility?.canComment && typeof comment === "string" && comment.trim()) {
    if (comment.trim().length > 2000) return null;
    input.comment = comment.trim();
  }
  return Object.keys(input).length ? input : null;
}

export async function submitReview(productId, { rating, comment }, token, { signal } = {}) {
  if (!id(productId)) throw { kind: "invalid-request" };
  return read(await fetch(`/api/customer/products/${productId}/review`, {
    method: "POST", signal, headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token },
    body: JSON.stringify({ rating, comment })
  }), value => isCustomerReview(value) && value.productId === productId);
}

export function isModerationPage(value) {
  return Array.isArray(value?.items) && value.items.length <= 20 && (value.nextAfterId === null || id(value.nextAfterId))
    && value.items.every(c => id(c?.id) && id(c.productId) && typeof c.productName === "string"
      && typeof c.customerName === "string" && typeof c.body === "string" && typeof c.version === "string"
      && /^\d+$/.test(c.version) && Number.isFinite(Date.parse(c.createdAt))
      && (c.rating === null || Number.isInteger(c.rating) && c.rating >= 1 && c.rating <= 5));
}

export async function fetchModeration(afterId = 0, { signal } = {}) {
  if (!Number.isSafeInteger(afterId) || afterId < 0) throw { kind: "invalid-request" };
  return read(await fetch(`/api/product-management/comments?afterId=${afterId}`, { signal }), isModerationPage);
}

export async function moderateComment(comment, status, token, { signal } = {}) {
  if (!id(comment?.id) || !["Approved", "Rejected"].includes(status) || !/^\d+$/.test(comment.version))
    throw { kind: "invalid-request" };
  return read(await fetch(`/api/product-management/comments/${comment.id}/moderation`, {
    method: "POST", signal, headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token },
    body: JSON.stringify({ status, version: comment.version })
  }), value => value?.id === comment.id && value.status === status && typeof value.version === "string");
}
