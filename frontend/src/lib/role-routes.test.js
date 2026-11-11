import test from "node:test";
import assert from "node:assert/strict";
import { accountPath, canAccess, canUseCart, footerLinks, headerLinks, isPublicPath, landingLabel, landingPath, postLoginPath, roleLinks } from "./role-routes.js";

const customer = { id: 1, role: "Customer" };
const productManager = { id: 2, role: "ProductManager" };
const salesManager = { id: 3, role: "SalesManager" };
const support = { id: 4, role: "SupportAgent" };

test("a customer lands on the home page after login and staff on their panel; an unknown role falls back to the home page", () => {
  assert.equal(landingPath("Customer"), "/");
  assert.equal(landingPath("ProductManager"), "/manage");
  assert.equal(landingPath("SalesManager"), "/manage");
  assert.equal(landingPath("SupportAgent"), "/support");
  for (const role of ["Admin", "", null, undefined, "customer"]) assert.equal(landingPath(role), "/");
  assert.equal(accountPath("Customer"), "/dashboard");
  assert.equal(accountPath("ProductManager"), "/manage");
  assert.equal(accountPath("SalesManager"), "/manage");
  assert.equal(accountPath("SupportAgent"), "/support");
  assert.equal(accountPath("Nobody"), "/");
  assert.equal(landingLabel("Customer"), "Dashboard");
  assert.equal(landingLabel("Nobody"), null);
});

test("public pages are open to everyone, including guests", () => {
  for (const url of ["/", "/products", "/product?id=7", "/cart", "/login", "/register", "/products#x"]) {
    assert.equal(isPublicPath(url), true, url);
    assert.equal(canAccess(null, url), true, url);
    assert.equal(canAccess("Customer", url), true, url);
  }
});

test("protected pages are closed to guests and to the wrong role", () => {
  const matrix = [
    ["/dashboard", ["Customer"]],
    ["/checkout", ["Customer"]],
    ["/orders", ["Customer"]],
    ["/order-confirmation?orderId=3", ["Customer"]],
    ["/deliveries", ["ProductManager"]],
    ["/manage", ["ProductManager", "SalesManager"]],
    ["/support", ["SupportAgent"]]
  ];
  for (const [url, allowed] of matrix) {
    assert.equal(canAccess(null, url), false, `guest ${url}`);
    assert.equal(canAccess(undefined, url), false, `undefined ${url}`);
    for (const role of ["Customer", "ProductManager", "SalesManager", "SupportAgent"]) {
      assert.equal(canAccess(role, url), allowed.includes(role), `${role} ${url}`);
    }
  }
});

test("path matching ignores case, query, hash and a trailing slash, and does not match look-alike paths", () => {
  assert.equal(canAccess("ProductManager", "/Deliveries/"), true);
  assert.equal(canAccess("Customer", "/DELIVERIES"), false);
  assert.equal(canAccess("Customer", "/orders/12?x=1#top"), true);
  assert.equal(canAccess("ProductManager", "/orders/12"), false);
  assert.equal(isPublicPath("/ordersX"), true);
  assert.equal(isPublicPath("/managers"), true);
  assert.equal(isPublicPath("/manage/products"), false);
});

test("after login the visitor goes to the page for their role when there is no usable returnUrl", () => {
  assert.equal(postLoginPath(customer, "/"), "/");
  assert.equal(postLoginPath(customer, undefined), "/");
  assert.equal(postLoginPath(productManager, "/"), "/manage");
  assert.equal(postLoginPath(salesManager, undefined), "/manage");
  assert.equal(postLoginPath(support, null), "/support");
});

test("a returnUrl that the role may open is honoured, including its query and hash", () => {
  assert.equal(postLoginPath(customer, "/checkout"), "/checkout");
  assert.equal(postLoginPath(customer, "/orders?x=1"), "/orders?x=1");
  assert.equal(postLoginPath(customer, "/dashboard"), "/dashboard");
  assert.equal(postLoginPath(customer, "/product?id=7#reviews"), "/product?id=7#reviews");
  assert.equal(postLoginPath(productManager, "/deliveries"), "/deliveries");
});

test("a returnUrl that the role may not open is replaced by the landing page", () => {
  assert.equal(postLoginPath(customer, "/deliveries"), "/");
  assert.equal(postLoginPath(productManager, "/checkout"), "/manage");
  assert.equal(postLoginPath(support, "/orders"), "/support");
});

test("unsafe or looping returnUrl values never leave the site and never return to the login page", () => {
  for (const value of ["//evil.com", "https://evil.com", "javascript:alert(1)", "/\\evil.com", "/login", "/login?returnUrl=%2F", "evil"]) {
    assert.equal(postLoginPath(customer, value), "/", value);
    assert.equal(postLoginPath(productManager, value), "/manage", value);
  }
});

test("a missing account lands on the home page", () => {
  assert.equal(postLoginPath(null, "/"), "/");
  assert.equal(postLoginPath(undefined, "/orders"), "/");
});

test("each role gets shortcuts it may open, and an unknown role gets none", () => {
  assert.deepEqual(roleLinks("Customer").map(([to]) => to), ["/orders", "/cart", "/products"]);
  assert.deepEqual(roleLinks("ProductManager").map(([to]) => to), ["/deliveries", "/products"]);
  assert.deepEqual(roleLinks("SalesManager").map(([to]) => to), ["/products"]);
  assert.deepEqual(roleLinks("SupportAgent").map(([to]) => to), ["/products"]);
  assert.deepEqual(roleLinks("Nobody"), []);
  for (const role of ["Customer", "ProductManager", "SalesManager", "SupportAgent"]) {
    for (const [to, label, text] of roleLinks(role)) {
      assert.equal(canAccess(role, to), true, `${role} ${to}`);
      assert.ok(label && text, `${role} ${to} has a label and a description`);
    }
  }
});

test("only guests and customers get the cart; only the product manager gets a Deliveries link in the header", () => {
  assert.equal(canUseCart(null), true);
  assert.equal(canUseCart(undefined), true);
  assert.equal(canUseCart("Customer"), true);
  for (const role of ["ProductManager", "SalesManager", "SupportAgent", "Admin", ""]) assert.equal(canUseCart(role), false, role);
  assert.deepEqual(headerLinks("ProductManager"), [["/deliveries", "Deliveries"]]);
  for (const role of [null, "Customer", "SalesManager", "SupportAgent"]) assert.deepEqual(headerLinks(role), [], String(role));
});

test("the footer offers Cart and Orders only to guests and customers, and never a support link", () => {
  const shopper = [["/products", "Products"], ["/cart", "Cart"], ["/orders", "Orders"]];
  assert.deepEqual(footerLinks(null), shopper);
  assert.deepEqual(footerLinks("Customer"), shopper);
  for (const role of ["ProductManager", "SalesManager", "SupportAgent"]) assert.deepEqual(footerLinks(role), [["/products", "Products"]], role);
});
