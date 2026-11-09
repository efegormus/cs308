// The fulfillment steps of an order as the course project names them: processing, in-transit and delivered.
export const ORDER_STEPS = ["Processing", "InTransit", "Delivered"];

const LABELS = { Processing: "Processing", InTransit: "In transit", Delivered: "Delivered", Cancelled: "Cancelled" };

// The text shown for an order status. An unknown status is shown as is rather than hidden.
export function orderStatusLabel(status) {
  return LABELS[status] ?? String(status);
}

// Where the order is on the way to the customer: "done", "current" or "todo" for each step.
// A cancelled order is not on that way, so none of its steps is marked.
export function stepStates(status) {
  const index = ORDER_STEPS.indexOf(status);
  return ORDER_STEPS.map((step, position) => ({
    step,
    label: LABELS[step],
    state: index === -1 ? "todo" : position < index ? "done" : position === index ? "current" : "todo"
  }));
}
