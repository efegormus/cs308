import assert from "node:assert/strict";
import test from "node:test";
import { createElement } from "react";
import { renderToStaticMarkup } from "react-dom/server";
import Table from "./Table.js";

const render = props => renderToStaticMarkup(createElement(Table, props));
const columns = [
  { key: "name", header: "Product", rowHeader: true },
  { key: "sku", header: "SKU" },
  { key: "stock", header: "In stock", align: "end", render: row => `${row.stock} left` }
];
const rows = [{ id: 1, name: "Pixel 9", sku: "PX-9", stock: 2 }, { id: 2, name: "<b>Tab</b>", sku: "TB-1", stock: 0 }];

test("loading shows skeleton rows in a named role=status box and no data", () => {
  const html = render({ columns, rows, loading: true, loadingLabel: "Loading low-stock products…", skeletonRows: 3 });
  assert.match(html, /role="status"/);
  assert.match(html, /aria-label="Loading low-stock products…"/);
  assert.match(html, /aria-busy="true"/);
  assert.equal(html.match(/class="ui-table-skeleton-row"/g).length, 3);
  assert.equal(html.match(/class="ui-table-skeleton"/g).length, 9);
  // The skeleton table itself is hidden from screen readers; the label is read instead.
  assert.match(html, /<table class="ui-table" aria-hidden="true">/);
  assert.match(html, /<span class="ui-table-sr">Loading low-stock products…<\/span>/);
  assert.doesNotMatch(html, /Pixel 9/);
});

test("an empty table says so in a role=status box", () => {
  const html = render({ columns, rows: [], emptyText: "No product is low on stock.", caption: "Low stock" });
  assert.match(html, /<p class="ui-table-empty" role="status">No product is low on stock\.<\/p>/);
  assert.match(html, /Low stock/);
  assert.doesNotMatch(html, /<table/);
});

test("a filled table has column headers with scope=col, row headers with scope=row and escaped cells", () => {
  const html = render({ columns, rows, caption: "Low-stock products" });
  assert.match(html, /<caption class="ui-table-caption">Low-stock products<\/caption>/);
  assert.equal(html.match(/<th scope="col"/g).length, 3);
  assert.equal(html.match(/<th scope="row"/g).length, 2);
  assert.match(html, /<th scope="row" class="ui-table-cell">Pixel 9<\/th>/);
  assert.match(html, /<td class="ui-table-cell ui-table-cell--end">2 left<\/td>/);
  assert.match(html, /&lt;b&gt;Tab&lt;\/b&gt;/);
  assert.doesNotMatch(html, /role="status"/);
});

test("the table scrolls sideways inside a focusable, named region", () => {
  const html = render({ columns, rows, caption: "Invoices" });
  assert.match(html, /<div class="ui-table-scroll" tabindex="0" role="region" aria-label="Invoices">/);
  assert.match(render({ columns, rows, className: "extra" }), /^<div class="ui-table-frame extra">/);
});

test("defaults: a generic loading label and empty text, and row keys from id", () => {
  assert.match(render({ columns, loading: true }), /aria-label="Loading…"/);
  assert.match(render({ columns }), /Nothing to show yet\./);
  assert.equal(render({ columns, rows, rowKey: row => `k${row.id}` }).match(/<tr>/g).length, 3);
});
