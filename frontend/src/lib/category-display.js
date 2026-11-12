// Labels and ids for the category sections of the product list. Pure helpers, so Node can test them.

// The id of a category section. The home page and the category links point at /products#category-<id>.
export function categoryAnchorId(categoryId) {
  return `category-${categoryId}`;
}

// The category id in a URL hash such as "#category-3", or null for any other hash.
export function categoryIdFromHash(hash) {
  const match = /^#category-([1-9]\d{0,15})$/.exec(hash ?? "");
  const id = match ? Number(match[1]) : NaN;
  return Number.isSafeInteger(id) ? id : null;
}

export function productCountLabel(count) {
  return `${count} ${count === 1 ? "product" : "products"}`;
}

export function visibleCategories(categories, search) {
  return typeof search === "string" && search.trim()
    ? categories.filter(category => category.products.length > 0)
    : categories;
}

// The line under the heading of the list, which a screen reader announces after loading.
export function catalogSummary(categories, search) {
  const count = categories.reduce((total, category) => total + category.products.length, 0);
  if (count === 0) {
    return typeof search === "string" && search.trim()
      ? "No products found."
      : "No products available yet. Please check back soon.";
  }
  const categoryCount = categories.length;
  return `${productCountLabel(count)} in ${categoryCount} ${categoryCount === 1 ? "category" : "categories"}.`;
}
