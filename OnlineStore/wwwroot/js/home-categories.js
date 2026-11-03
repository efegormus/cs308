import { fetchCategories } from "./catalog-api.js";

async function connectCategoryLinks() {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const categories = await fetchCategories(controller.signal);
    for (const link of document.querySelectorAll("[data-category-name]")) {
      const category = categories.find(item => typeof item.name === "string"
        && item.name.toLowerCase() === link.dataset.categoryName.toLowerCase());
      if (category && Number.isInteger(category.id)) link.href = `/products.html#category-${category.id}`;
    }
  } catch {
    // The working fallback opens the catalog, whose loading/error/retry UI handles database availability.
  } finally {
    clearTimeout(timeout);
  }
}

connectCategoryLinks();
