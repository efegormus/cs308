import test from "node:test";
import assert from "node:assert/strict";
import { adminSections, resolveSection } from "./admin-menu.js";
import { canAccess } from "./role-routes.js";

test("the product manager's menu is Overview, Deliveries, Reviews and Low stock, in that order", () => {
  assert.deepEqual(adminSections("ProductManager").map(item => [item.label, item.path]), [
    ["Overview", "/manage"], ["Deliveries", "/manage/deliveries"], ["Reviews", "/manage/reviews"], ["Low stock", "/manage/low-stock"]
  ]);
});

test("the sales manager's menu is Overview, Invoices and Discounts, in that order", () => {
  assert.deepEqual(adminSections("SalesManager").map(item => [item.label, item.path]), [
    ["Overview", "/manage"], ["Invoices", "/manage/invoices"], ["Discounts", "/manage/discounts"]
  ]);
});

test("customers, support agents, guests and unknown roles get no admin menu", () => {
  for (const role of ["Customer", "SupportAgent", null, undefined, "", "productmanager", "Admin"]) {
    assert.deepEqual(adminSections(role), [], String(role));
    assert.equal(resolveSection(role, undefined), null, String(role));
  }
});

test("every menu path is a protected page that only its role (of the two managers) may open", () => {
  for (const role of ["ProductManager", "SalesManager"]) {
    for (const item of adminSections(role)) {
      assert.equal(canAccess(role, item.path), true, `${role} ${item.path}`);
      assert.equal(canAccess("Customer", item.path), false, `Customer ${item.path}`);
      assert.equal(canAccess(null, item.path), false, `guest ${item.path}`);
    }
  }
});

test("a section segment resolves only to the role's own sections", () => {
  assert.equal(resolveSection("ProductManager", undefined).id, "overview");
  assert.equal(resolveSection("SalesManager", "").id, "overview");
  assert.equal(resolveSection("ProductManager", "reviews").id, "reviews");
  assert.equal(resolveSection("ProductManager", "Low-Stock").id, "low-stock");
  assert.equal(resolveSection("SalesManager", "invoices").id, "invoices");
  // Another role's section, an unknown one and the overview's own id under /manage/ are not sections.
  assert.equal(resolveSection("ProductManager", "invoices"), null);
  assert.equal(resolveSection("SalesManager", "reviews"), null);
  assert.equal(resolveSection("SalesManager", "nope"), null);
  assert.equal(resolveSection("ProductManager", "overview"), null);
});
