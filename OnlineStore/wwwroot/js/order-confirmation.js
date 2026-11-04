import { api, date, errorMessage, money, renderLines, requireRole, showMessage } from "./purchase-ui.js";

const reload = document.getElementById("retry");
async function load() {
  reload.disabled = true;
  showMessage("Loading order…");
  try {
    const id = new URLSearchParams(location.search).get("orderId");
    if (!/^[1-9]\d*$/.test(id ?? "")) throw Object.assign(new Error("Invalid order ID."), { status: 400 });
    const receipt = await api(`/api/customer/orders/${encodeURIComponent(id)}`);
    document.getElementById("order-heading").textContent = `Order #${receipt.orderId}`;
    document.getElementById("order-status").textContent = `${receipt.status} · Payment: ${receipt.paymentStatus} · ${date(receipt.purchasedAt)}`;
    document.getElementById("order-address").textContent = `${receipt.name} · ${receipt.address}`;
    renderLines(document.getElementById("lines"), receipt.items);
    document.getElementById("total").textContent = money(receipt.total);
    const invoice = document.getElementById("invoice");
    invoice.hidden = receipt.invoiceStatus !== "Ready";
    if (!invoice.hidden) invoice.href = `/api/customer/orders/${receipt.orderId}/invoice`;
    document.getElementById("invoice-pending").hidden = !invoice.hidden;
    document.getElementById("receipt").hidden = false;
    showMessage("");
  } catch (error) { showMessage(errorMessage(error), true); }
  finally { reload.disabled = false; reload.hidden = false; }
}
reload.addEventListener("click", load);
if (await requireRole("Customer")) await load();
