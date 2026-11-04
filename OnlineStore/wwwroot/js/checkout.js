import { api, errorMessage, money, renderLines, requireRole, showMessage } from "./purchase-ui.js";

const form = document.getElementById("checkout-form");
const retry = document.getElementById("retry");
const submit = document.getElementById("purchase");
const storageKey = "store.checkout.attempt";
let quote;
let attempt;
let busy = false;

function remember() {
  try { sessionStorage.setItem(storageKey, JSON.stringify(attempt)); } catch { /* The current page still retains the retry key. */ }
}
function forget() {
  attempt = null;
  try { sessionStorage.removeItem(storageKey); } catch { /* Storage may be disabled. */ }
}
function confirmation(receipt) {
  forget();
  location.assign(`/order-confirmation.html?orderId=${encodeURIComponent(receipt.orderId)}`);
}
async function load() {
  if (busy) return;
  busy = true;
  retry.hidden = true;
  form.hidden = true;
  showMessage("Loading checkout…");
  try {
    if (attempt?.key) {
      try {
        confirmation(await api(`/api/customer/checkout/attempts/${encodeURIComponent(attempt.key)}`));
        return;
      } catch (error) { if (error.status !== 404) throw error; }
    }
    quote = await api("/api/customer/checkout");
    if (!attempt || attempt.cartId !== quote.cartId || attempt.quoteToken !== quote.quoteToken) {
      attempt = { cartId: quote.cartId, quoteToken: quote.quoteToken, key: crypto.randomUUID() };
      remember();
    }
    document.getElementById("customer").textContent = `${quote.name} · ${quote.email} · Tax ID: ${quote.taxId}`;
    document.getElementById("address").textContent = quote.address;
    renderLines(document.getElementById("lines"), quote.items);
    document.getElementById("total").textContent = money(quote.total);
    form.hidden = false;
    showMessage("");
  } catch (error) {
    showMessage(errorMessage(error), true);
    retry.hidden = false;
  } finally { busy = false; }
}

form.addEventListener("submit", async event => {
  event.preventDefault();
  if (busy || !quote || !form.reportValidity()) return;
  busy = true;
  submit.disabled = true;
  retry.hidden = true;
  showMessage("Confirming mock payment…");
  try {
    // Only the cart reference, change detector and bounded mock inputs leave the page; totals/owner/paid flags never do.
    const receipt = await api("/api/customer/checkout", {
      cartId: attempt.cartId, quoteToken: attempt.quoteToken, idempotencyKey: attempt.key,
      payment: {
        number: form.elements.number.value.trim(), expiryMonth: Number(form.elements.expiryMonth.value),
        expiryYear: Number(form.elements.expiryYear.value), securityCode: form.elements.securityCode.value
      }
    });
    confirmation(receipt);
  } catch (error) {
    showMessage(errorMessage(error), true);
    if (error.status === 409) {
      forget();
      form.hidden = true;
      retry.hidden = false;
    } else if (error.status === 400 || error.status === 402) {
      // These are definite non-purchases. A corrected payment gets a new attempt key.
      attempt.key = crypto.randomUUID();
      remember();
    }
    // Network/timeout/503 outcomes retain the same attempt key, even on reload.
  } finally {
    form.elements.number.value = "";
    form.elements.securityCode.value = "";
    submit.disabled = false;
    busy = false;
  }
});
retry.addEventListener("click", load);
try { attempt = JSON.parse(sessionStorage.getItem(storageKey)); } catch { attempt = null; }
if (await requireRole("Customer")) await load();
