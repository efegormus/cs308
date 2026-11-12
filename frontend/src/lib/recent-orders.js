// The customer dashboard shows only the newest orders (SCRUM-125); the full history is the orders page.
export const RECENT_ORDER_COUNT = 3;

/** The newest orders first, at most `limit` of them. The input is not changed. */
export function recentOrders(orders, limit = RECENT_ORDER_COUNT) {
  return [...orders].sort((a, b) => b.orderId - a.orderId).slice(0, Math.max(0, limit));
}
