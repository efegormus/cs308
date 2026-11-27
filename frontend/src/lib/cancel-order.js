// The customer's "Cancel order" action (SCRUM-158; API: POST /api/customer/orders/{id}/cancel, docs/purchasing.md).
// Only a paid order that is still Processing can be cancelled; the server decides again, this only hides the button.
const id = value => Number.isSafeInteger(value) && value > 0;

export function canCancelOrder(order) {
  return order?.status === "Processing" && order.paymentStatus === "Confirmed";
}

/** The server's answer to a cancellation. orderId must be the order that was asked for. */
export function isCancelledOrder(value, orderId) {
  return value?.orderId === orderId && id(value.orderId) && value.status === "Cancelled"
    && typeof value.cancelledAt === "string" && Number.isFinite(Date.parse(value.cancelledAt))
    && typeof value.alreadyCancelled === "boolean"
    && Number.isSafeInteger(value.itemsRestored) && value.itemsRestored >= 0
    && Number.isFinite(value.refunded) && value.refunded >= 0;
}

export async function cancelOrder(orderId, token, { signal } = {}) {
  if (!id(orderId)) throw { kind: "invalid-request" };
  signal?.throwIfAborted();
  const response = await fetch(`/api/customer/orders/${orderId}/cancel`, {
    method: "POST", signal, headers: { Accept: "application/json", "X-CSRF-TOKEN": token }
  });
  if (!response.ok) throw { status: response.status };
  let value;
  try { value = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isCancelledOrder(value, orderId)) throw { kind: "invalid-response" };
  return value;
}

/** What the customer reads when a cancellation fails. Nothing from the server's error body is shown. */
export function cancelErrorMessage(error) {
  if (error?.status === 409) return "This order can no longer be cancelled because its status has changed. The list was refreshed.";
  if (error?.status === 404) return "This order could not be found.";
  if (error?.status === 400) return "Your session needed a refresh. Please try again.";
  if ([401, 403].includes(error?.status)) return "Please log in again to cancel this order.";
  return "The order could not be cancelled. Please try again.";
}
