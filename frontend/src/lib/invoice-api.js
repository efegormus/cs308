const id = value => Number.isSafeInteger(value) && value > 0;
const money = value => Number.isFinite(value) && value >= 0;
const instant = value => typeof value === "string" && Number.isFinite(Date.parse(value));

export function isInvoice(value) {
  return id(value?.orderId) && typeof value.invoiceNumber === "string" && /^INV-[A-Z0-9-]+$/.test(value.invoiceNumber)
    && instant(value.purchasedAt) && instant(value.issuedAt)
    && [value.customerName, value.taxId, value.address].every(v => typeof v === "string") && value.currency === "TRY"
    && money(value.total) && Array.isArray(value.lines) && value.lines.length > 0 && value.lines.length <= 100
    && value.lines.every(l => typeof l?.productName === "string" && id(l.quantity) && money(l.unitPrice) && money(l.total));
}

export async function fetchInvoice(orderId, { signal } = {}) {
  if (!id(orderId)) throw { kind: "invalid-request" };
  const response = await fetch(`/api/customer/orders/${orderId}/invoice/details`, { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw { status: response.status };
  let data; try { data = await response.json(); } catch { throw { kind: "invalid-response" }; }
  if (!isInvoice(data) || data.orderId !== orderId) throw { kind: "invalid-response" };
  return data;
}
