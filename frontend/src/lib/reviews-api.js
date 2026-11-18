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
