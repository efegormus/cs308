const priceFormat = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function imagePlaceholder() {
  return element("div", "image-placeholder", "No image available");
}

function productImage(product) {
  if (!product.imageUrl) return imagePlaceholder();
  let url;
  try {
    url = new URL(product.imageUrl, window.location.href);
  } catch {
    return imagePlaceholder();
  }
  if (!["http:", "https:"].includes(url.protocol)) return imagePlaceholder();

  const image = element("img", "product-image");
  image.alt = product.name;
  image.loading = "lazy";
  image.addEventListener("error", () => image.replaceWith(imagePlaceholder()), { once: true });
  image.src = url.href;
  return image;
}

function productCard(product) {
  const item = element("li");
  const card = element("article", "product-card");
  card.append(productImage(product), element("h3", "", product.name));
  if (product.description) card.append(element("p", "product-description", product.description));
  card.append(element("p", "price", priceFormat.format(product.price)));
  const inStock = product.stockQuantity > 0;
  card.append(element("p", inStock ? "stock" : "stock out-of-stock",
    inStock ? `In stock · ${product.stockQuantity} available` : "Out of stock"));
  item.append(card);
  return item;
}

export function renderCatalog(categories, catalog, navigation) {
  const sections = document.createDocumentFragment();
  const links = document.createDocumentFragment();
  for (const category of categories) {
    const id = `category-${category.id}`;
    const section = element("section", "category-section");
    section.id = id;
    section.setAttribute("aria-labelledby", `${id}-heading`);
    const heading = element("h2", "", category.name);
    heading.id = `${id}-heading`;
    const count = category.products.length;
    heading.append(element("span", "product-count", ` · ${count} ${count === 1 ? "product" : "products"}`));
    section.append(heading);
    if (category.description) section.append(element("p", "category-description", category.description));
    if (count === 0) {
      section.append(element("p", "", "No products available in this category yet."));
    } else {
      const grid = element("ul", "product-grid");
      grid.append(...category.products.map(productCard));
      section.append(grid);
    }
    sections.append(section);
    const link = element("a", "", category.name);
    link.href = `#${id}`;
    links.append(link);
  }
  catalog.replaceChildren(sections);
  navigation.replaceChildren(links);
  navigation.hidden = categories.length === 0;
}

const catalog = document.querySelector("#catalog");
const navigation = document.querySelector("#category-nav");
const status = document.querySelector("#catalog-status");
const retry = document.querySelector("#retry");

async function loadCatalog() {
  catalog.setAttribute("aria-busy", "true");
  retry.hidden = true;
  status.textContent = "Loading products…";
  try {
    const response = await fetch("/api/catalog", { headers: { Accept: "application/json" } });
    if (!response.ok) throw new Error("Catalog request failed.");
    const categories = await response.json();
    renderCatalog(categories, catalog, navigation);
    const count = categories.reduce((total, category) => total + category.products.length, 0);
    status.textContent = count === 0 ? "No products available yet. Please check back soon."
      : `${count} ${count === 1 ? "product" : "products"} in ${categories.length} ${categories.length === 1 ? "category" : "categories"}.`;
    // Category links remain usable when this page is opened with a saved fragment.
    const target = document.getElementById(window.location.hash.slice(1));
    if (target) target.scrollIntoView();
  } catch {
    catalog.replaceChildren();
    navigation.replaceChildren();
    navigation.hidden = true;
    status.textContent = "We couldn't load the products. Please try again.";
    retry.hidden = false;
  } finally {
    catalog.setAttribute("aria-busy", "false");
  }
}

retry.addEventListener("click", loadCatalog);
loadCatalog();
