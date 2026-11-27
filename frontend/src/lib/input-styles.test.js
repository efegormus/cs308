import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

// A field's hidden label is position: absolute. A field inside a table row sits in a scroll box (ui-table-scroll). The
// absolute label is clipped by that box only when it has a positioned ancestor inside the box, so the field must be
// positioned. Without it, on a phone the labels of the Stock screen's rows widened the whole page (SCRUM-78).
const css = readFileSync(new URL("../components/ui/Input.css", import.meta.url), "utf8");
const rule = selector => css.match(new RegExp(`${selector.replace(/[.]/g, "\\.")}\\s*\\{([^}]*)\\}`))?.[1] ?? "";

test("the field is a positioned box, so its hidden label stays inside it", () => {
  assert.match(rule(".ui-field"), /position:\s*relative/);
});

test("the hidden label is absolutely positioned and one pixel in size", () => {
  const hidden = rule(".ui-visually-hidden");
  assert.match(hidden, /position:\s*absolute/);
  assert.match(hidden, /width:\s*1px/);
  assert.match(hidden, /height:\s*1px/);
});

test("the table scrolls inside its own box", () => {
  const table = readFileSync(new URL("../components/ui/Table.css", import.meta.url), "utf8");
  assert.match(table, /\.ui-table-scroll\s*\{[^}]*overflow-x:\s*auto/);
});
