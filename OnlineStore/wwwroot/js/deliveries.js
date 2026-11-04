import { api, date, errorMessage, money, requireRole, showMessage, text } from "./purchase-ui.js";

const list = document.getElementById("deliveries");
const reload = document.getElementById("retry");
const more = document.getElementById("more");
let cursor = null;
let busy = false;

function card(delivery) {
  const section = text("section", "", "purchase-card");
  section.append(text("h2", `Delivery #${delivery.id} · Order #${delivery.orderId}`),
    text("p", `Customer #${delivery.customerId} · ${delivery.status} · Payment: ${delivery.paymentStatus}`),
    text("p", delivery.address, "snapshot-address"),
    text("p", `Created: ${date(delivery.createdAt)}${delivery.deliveredAt ? ` · Delivered: ${date(delivery.deliveredAt)}` : ""}`));
  const items = text("ul", "", "purchase-lines");
  for (const item of delivery.items) items.append(text("li", `Product #${item.productId} · ${item.name} · Quantity ${item.quantity} · ${money(item.total)}`));
  section.append(items, text("p", `Original total: ${money(delivery.total)}`, "purchase-total"));
  if (delivery.canShip || delivery.canDeliver) {
    const target = delivery.canShip ? "InTransit" : "Delivered";
    const button = text("button", delivery.canShip ? "Mark in transit" : "Mark delivered");
    button.type = "button";
    button.addEventListener("click", async () => {
      if (busy) return;
      busy = true;
      button.disabled = true;
      reload.disabled = true;
      more.disabled = true;
      try {
        const updated = await api(`/api/product-management/deliveries/${delivery.id}/status`, { status: target, version: delivery.version });
        section.replaceWith(card(updated));
        showMessage(`Delivery #${delivery.id} updated.`);
      } catch (error) { showMessage(errorMessage(error), true); }
      finally { busy = false; button.disabled = false; reload.disabled = false; more.disabled = false; }
    });
    section.append(button);
  } else if (delivery.status === "Processing" || delivery.status === "InTransit") {
    section.append(text("p", "This delivery needs payment or shipment reconciliation before it can continue."));
  }
  return section;
}

async function load(append = false) {
  if (busy) return;
  busy = true;
  reload.disabled = true;
  more.disabled = true;
  showMessage("Loading deliveries…");
  try {
    const page = await api(`/api/product-management/deliveries${append && cursor ? `?afterId=${cursor}` : ""}`);
    if (!append) list.replaceChildren();
    list.append(...page.items.map(card));
    cursor = page.nextAfterId;
    more.hidden = cursor === null;
    showMessage(list.childElementCount ? "" : "No deliveries yet.");
  } catch (error) { showMessage(errorMessage(error), true); }
  finally { busy = false; reload.disabled = false; reload.hidden = false; more.disabled = false; }
}
reload.addEventListener("click", () => load());
more.addEventListener("click", () => load(true));
if (await requireRole("ProductManager")) await load();
