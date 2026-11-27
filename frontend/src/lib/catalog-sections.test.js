import test from "node:test";
import assert from "node:assert/strict";
import {
  SECTION_PREVIEW, SECTION_SORTS, activeFilterChips, buildSections, catalogHeading, catalogTree, categoryCounts,
  categoryPath, categoryScope, filterInStock, isSectionSort, noResults, rankById, sectionToggleLabel, sectionsStatus,
  sidebarCategories, sortSection, visibleProducts
} from "./catalog-sections.js";

// A small catalog shaped like the seed: top-level categories with products of their own and one subcategory each, in
// the API's order (by name). Electronics is an old top-level category with no products.
const categories = [
  { id: 4, name: "Electronics", parentId: null },
  { id: 1, name: "Phones", parentId: null, description: "Phones of every kind" },
  { id: 11, name: "Smartphones", parentId: 1 },
  { id: 2, name: "Tablets", parentId: null },
  { id: 12, name: "Android Tablets", parentId: 2 }
];

const product = (id, name, categoryId, price, stockQuantity = 5) => ({ id, name, categoryId, price, stockQuantity });
// The API's name order (GET /api/products without sort).
const products = [
  product(30, "Galaxy Tab S9", 12, 30000),
  product(31, "Lenovo Tab P12", 12, 15000, 0),
  product(10, "Nova Phone A", 1, 9000),
  product(11, "Nova Phone B", 1, 7000),
  product(12, "Nova Phone C", 1, 9000),
  product(13, "Nova Phone D", 1, 5000, 0),
  product(20, "Pixel 9", 11, 43000),
  product(21, "Tab Mini", 2, 12000)
];
const tree = catalogTree(categories);
const names = list => list.map(item => item.name);

test("the tree lists every category depth first and counts a broken parent as top-level", () => {
  assert.deepEqual(tree.ordered, [4, 1, 11, 2, 12]);
  assert.deepEqual(tree.roots, [4, 1, 2]);
  const broken = catalogTree([{ id: 1, name: "A", parentId: 2 }, { id: 2, name: "B", parentId: 1 }, { id: 3, name: "C", parentId: 99 }]);
  assert.deepEqual(broken.roots, [1, 2, 3]);
  assert.deepEqual(catalogTree(null).ordered, []);
});

test("products are grouped by their own category, so a parent and its subcategory are separate sections", () => {
  const sections = buildSections(tree, products);
  assert.deepEqual(sections.map(section => [section.name, section.products.length]),
    [["Phones", 4], ["Smartphones", 1], ["Tablets", 1], ["Android Tablets", 2]]);
  // Electronics has no products, so it has no section; a product keeps the API's order inside its section.
  assert.deepEqual(names(sections[0].products), ["Nova Phone A", "Nova Phone B", "Nova Phone C", "Nova Phone D"]);
  assert.equal(sections[0].description, "Phones of every kind");
});

test("a chosen category shows itself and its subcategories, and a subcategory shows only itself", () => {
  assert.deepEqual([...categoryScope(tree, "1")], [1, 11]);
  assert.deepEqual(buildSections(tree, products, { categoryId: "1" }).map(section => section.name), ["Phones", "Smartphones"]);
  assert.deepEqual(buildSections(tree, products, { categoryId: "11" }).map(section => section.name), ["Smartphones"]);
  // An unknown category id means every category, like no category.
  assert.equal(buildSections(tree, products, { categoryId: "999" }).length, 4);
  assert.deepEqual(names(categoryPath(tree, "12")), ["Tablets", "Android Tablets"]);
});

test("sidebar counts include every category below, and subcategories are listed only under the chosen parent", () => {
  const counts = categoryCounts(tree, products);
  assert.deepEqual(Object.fromEntries(counts), { 4: 0, 1: 5, 11: 1, 2: 3, 12: 2 });

  const plain = sidebarCategories(tree, counts);
  assert.deepEqual(plain.map(row => [row.name, row.count, row.depth]), [["Electronics", 0, 0], ["Phones", 5, 0], ["Tablets", 3, 0]]);

  const chosen = sidebarCategories(tree, counts, { categoryId: "11" });
  assert.deepEqual(chosen.map(row => [row.name, row.depth, row.selected]),
    [["Electronics", 0, false], ["Phones", 0, false], ["Smartphones", 1, true], ["Tablets", 0, false]]);
});

test("during a search the sidebar hides categories without a match but keeps the chosen one", () => {
  const matches = products.filter(item => item.name.startsWith("Nova"));
  const counts = categoryCounts(tree, matches);
  assert.deepEqual(sidebarCategories(tree, counts, { searching: true }).map(row => row.name), ["Phones"]);
  // Smartphones has no match, but it is the chosen category, so it and its parent stay.
  assert.deepEqual(sidebarCategories(tree, counts, { categoryId: "11", searching: true }).map(row => [row.name, row.count]),
    [["Phones", 4], ["Smartphones", 0]]);
});

test("each section sorts on its own: name, price both ways and popularity, ties in name order", () => {
  const phones = buildSections(tree, products)[0].products;
  assert.deepEqual(names(sortSection(phones, "name")), ["Nova Phone A", "Nova Phone B", "Nova Phone C", "Nova Phone D"]);
  assert.deepEqual(names(sortSection(phones, "price-asc")), ["Nova Phone D", "Nova Phone B", "Nova Phone A", "Nova Phone C"]);
  // A and C cost the same: they keep name order in both directions, as the API breaks price ties by name.
  assert.deepEqual(names(sortSection(phones, "price-desc")), ["Nova Phone A", "Nova Phone C", "Nova Phone B", "Nova Phone D"]);
  const popularity = rankById([12, 21, 10, 30, 11]);
  assert.deepEqual(names(sortSection(phones, "popularity", popularity)), ["Nova Phone C", "Nova Phone A", "Nova Phone B", "Nova Phone D"]);
  // Until the popularity order arrives the section keeps its name order.
  assert.deepEqual(names(sortSection(phones, "popularity")), names(phones));
  // Sorting returns a new list and leaves the section (and every other section) as it was.
  assert.deepEqual(names(phones), ["Nova Phone A", "Nova Phone B", "Nova Phone C", "Nova Phone D"]);
});

test("the sort options are the four of the section header, with name first", () => {
  assert.deepEqual(SECTION_SORTS.map(option => option.label), ["Name A to Z", "Price low to high", "Price high to low", "Popularity"]);
  assert.equal(isSectionSort("price-desc"), true);
  assert.equal(isSectionSort("price"), false);
  assert.deepEqual(rankById([{ id: 7 }, { id: 3 }]), new Map([[7, 0], [3, 1]]));
});

test("a section shows three products first and offers View all N only when it has more", () => {
  const phones = buildSections(tree, products)[0].products;
  assert.equal(SECTION_PREVIEW, 3);
  assert.equal(visibleProducts(phones, false).length, 3);
  assert.equal(visibleProducts(phones, true).length, 4);
  assert.equal(sectionToggleLabel(4, false), "View all 4");
  assert.equal(sectionToggleLabel(4, true), "Show less");
  assert.equal(sectionToggleLabel(3, false), null);
});

test("out-of-stock products are kept unless In stock only is on, and then sections and counts follow", () => {
  assert.equal(filterInStock(products, false).length, 8);
  assert.deepEqual(filterInStock(products, true).map(item => item.id), [30, 10, 11, 12, 20, 21]);
  const sections = buildSections(tree, products, { inStockOnly: true });
  assert.deepEqual(sections.map(section => [section.name, section.products.length]),
    [["Phones", 3], ["Smartphones", 1], ["Tablets", 1], ["Android Tablets", 1]]);
  assert.equal(sectionsStatus(sections), "6 products in 4 sections");
  assert.equal(sectionsStatus(buildSections(tree, products, { categoryId: "11" })), "1 product in 1 section");
  // A search for a sold-out product still finds it.
  const found = buildSections(tree, products.filter(item => item.name === "Lenovo Tab P12"));
  assert.deepEqual(found.map(section => names(section.products)), [["Lenovo Tab P12"]]);
});

test("the heading follows the category and the search, with a breadcrumb that ends unlinked", () => {
  assert.deepEqual(catalogHeading(tree), {
    kind: "all", query: "", title: "All products", results: null,
    crumbs: [{ label: "Home", to: "/" }, { label: "All products", to: null }]
  });
  const phones = catalogHeading(tree, { categoryId: "11" });
  assert.equal(phones.title, "Smartphones");
  assert.deepEqual(phones.crumbs.map(crumb => [crumb.label, crumb.to]),
    [["Home", "/"], ["All products", "/products"], ["Phones", "/products?categoryId=1"], ["Smartphones", null]]);

  const search = catalogHeading(tree, { search: "  nova ", resultCount: 1 });
  assert.equal(search.kind, "search");
  assert.equal(search.query, "nova");
  assert.equal(search.results, "1 result");
  assert.deepEqual(search.crumbs.map(crumb => [crumb.label, crumb.to]), [["Home", "/"], ["All products", "/products"], ["“nova”", null]]);
  assert.equal(catalogHeading(tree, { search: "nova", resultCount: 4 }).results, "4 results");
  assert.deepEqual(noResults(" xyz "), {
    message: "No results for “xyz”.", hint: "Check the spelling, try a shorter or more general word, or clear the filters."
  });
});

test("active filters become chips in a fixed order, each removing its own URL parameters", () => {
  const brands = [{ id: 4, name: "Nova" }];
  const format = amount => `₺${amount}`;
  assert.deepEqual(activeFilterChips(tree, { categoryId: "1", brandId: "4", brands, minPrice: "100", maxPrice: "500", inStock: true }, format), [
    { key: "category", label: "Phones", remove: ["categoryId"] },
    { key: "brand", label: "Nova", remove: ["brandId"] },
    { key: "price", label: "₺100 – ₺500", remove: ["minPrice", "maxPrice"] },
    { key: "inStock", label: "In stock only", remove: ["inStock"] }
  ]);
  assert.equal(activeFilterChips(tree, { minPrice: "100" }, format)[0].label, "From ₺100");
  assert.equal(activeFilterChips(tree, { maxPrice: "0" }, format)[0].label, "Up to ₺0");
  // An unknown category or brand makes no chip; nothing active means no chips.
  assert.deepEqual(activeFilterChips(tree, { categoryId: "999", brandId: "8", brands }), []);
  assert.deepEqual(activeFilterChips(tree), []);
});
