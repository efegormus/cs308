import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// SCRUM-138 UI polish: the cart, checkout and order pages use the shared primary button and theme tokens only.
const read = name => readFileSync(new URL(`../${name}`, import.meta.url), "utf8");
const css = name => read(`styles/${name}.css`);

test("checkout and cart primary actions use the shared ui-button", () => {
  assert.match(read("pages/CheckoutPage.jsx"), /className="ui-button purchase-submit"/);
  assert.match(read("pages/CartPage.jsx"), /className="ui-button cart-checkout"/);
});

test("order history and dashboard load-more buttons use the shared ui-button", () => {
  assert.match(read("pages/OrdersPage.jsx"), /id="more-orders" className="ui-button"/);
});

test("route pages give ui-button the accent fill so page styles cannot override it", () => {
  assert.match(css("purchases"), /\.route-page \.ui-button \{[^}]*background: var\(--accent\)/);
});

test("cart quantity controls meet the 44px touch target", () => {
  assert.match(css("cart"), /\.cart-quantity button \{[^}]*min-height: 2\.75rem/);
});

test("order review links are chips, not a bulleted list", () => {
  assert.match(css("orders"), /\.order-review-links \{[^}]*list-style: none/);
});

test("the polish rules add no fixed colors to purchases.css", () => {
  assert.deepEqual(css("purchases").match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], []);
});

test("the invoice panel imports the table styles and right-aligns the money columns", () => {
  const panel = read("components/InvoicePanel.jsx");
  assert.match(panel, /import "\.\/ui\/Table\.css"/);
  assert.equal((panel.match(/align: "end"/g) ?? []).length, 3);
});

test("a pending invoice says that the payment is not repeated and keeps the #retry refresh button", () => {
  const page = read("pages/OrderConfirmationPage.jsx");
  assert.match(page, /id="invoice-pending"[^>]*role="status"/);
  assert.match(page, /will not be charged again/);
  assert.match(page, /id="retry" className="ui-button"/);
});
