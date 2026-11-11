import test from "node:test";
import assert from "node:assert/strict";
import { activeRootId, buildCategoryTree } from "./category-tree.js";

const seeded = [
  { id: 4, name: "Android Tablets", parentId: 2 },
  { id: 1, name: "Phones", parentId: null },
  { id: 3, name: "Smartphones", parentId: 1 },
  { id: 2, name: "Tablets", parentId: null }
];

test("top-level categories form the bar and their subcategories the panels, in API order", () => {
  assert.deepEqual(buildCategoryTree(seeded), [
    { id: 1, name: "Phones", children: [{ id: 3, name: "Smartphones" }] },
    { id: 2, name: "Tablets", children: [{ id: 4, name: "Android Tablets" }] }
  ]);
});

test("without parentId (an older server or a test fixture) every category is a flat top-level item", () => {
  const flat = [{ id: 41, name: "Phones", description: null }, { id: 99, name: "Tablets", description: null }];
  assert.deepEqual(buildCategoryTree(flat), [
    { id: 41, name: "Phones", children: [] },
    { id: 99, name: "Tablets", children: [] }
  ]);
});

test("deeper categories are listed under their top-level category", () => {
  const tree = buildCategoryTree([
    { id: 1, name: "Audio", parentId: null },
    { id: 2, name: "Headphones", parentId: 1 },
    { id: 3, name: "Wireless headphones", parentId: 2 }
  ]);
  assert.deepEqual(tree, [{ id: 1, name: "Audio", children: [{ id: 2, name: "Headphones" }, { id: 3, name: "Wireless headphones" }] }]);
});

test("a missing parent, a loop, invalid rows and a non-array answer never break the menu", () => {
  const tree = buildCategoryTree([
    { id: 1, name: "Orphan", parentId: 999 },
    { id: 2, name: "Loop A", parentId: 3 },
    { id: 3, name: "Loop B", parentId: 2 },
    { id: "x", name: "Bad id" },
    { id: 5 },
    null
  ]);
  assert.deepEqual(tree.map(item => item.name), ["Orphan", "Loop A", "Loop B"]);
  assert.deepEqual(buildCategoryTree(undefined), []);
  assert.deepEqual(buildCategoryTree({ items: [] }), []);
});

test("the active top-level category is found from itself or from one of its subcategories", () => {
  const tree = buildCategoryTree(seeded);
  assert.equal(activeRootId(tree, 1), 1);
  assert.equal(activeRootId(tree, 4), 2);
  assert.equal(activeRootId(tree, 77), null);
  assert.equal(activeRootId(tree, null), null);
});
