import { fetchProduct } from "./catalog-api.js";

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

// Only http(s) image URLs are used; anything else falls back to the placeholder.
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
  image.addEventListener("error", () => image.replaceWith(imagePlaceholder()), { once: true });
  image.src = url.href;
  return image;
}

function detailRow(list, label, value) {
  if (!value) return;
  list.append(element("dt", "", label), element("dd", "", value));
}

export function renderProduct(product, container) {
  const info = element("div", "product-info");
  const category = element("p", "product-category");
  const categoryLink = element("a", "", product.categoryName);
  categoryLink.href = `/products.html#category-${product.categoryId}`;
  category.append(categoryLink);

  const inStock = product.stockQuantity > 0;
  const stock = element("p", inStock ? "stock" : "stock out-of-stock",
    inStock ? `In stock · ${product.stockQuantity} available` : "Out of stock");

  const details = element("dl", "product-specs");
  detailRow(details, "Model", product.model);
  detailRow(details, "Warranty", product.warrantyStatus);
  detailRow(details, "Distributor", product.distributorInformation);

  info.append(category, element("h1", "", product.name));
  if (product.description) info.append(element("p", "product-description", product.description));
  info.append(element("p", "price", priceFormat.format(product.price)), stock, details);
  container.replaceChildren(productImage(product), info);
}

const container = document.querySelector("#product");
const status = document.querySelector("#product-status");
const retry = document.querySelector("#retry");

// The id comes from the address bar (?id=3), so it is validated before it is used.
function readProductId() {
  const raw = new URLSearchParams(window.location.search).get("id");
  if (!/^\d{1,9}$/.test(raw ?? "")) return null;
  return Number(raw);
}

async function loadProduct() {
  const id = readProductId();
  if (id === null) {
    status.textContent = "This product could not be found.";
    return;
  }
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  container.setAttribute("aria-busy", "true");
  retry.hidden = true;
  status.textContent = "Loading product…";
  try {
    const product = await fetchProduct(id, controller.signal);
    if (product === null) {
      container.hidden = true;
      status.textContent = "This product could not be found.";
      return;
    }
    renderProduct(product, container);
    container.hidden = false;
    document.title = `${product.name} | Online Store`;
    status.textContent = "";
  } catch {
    controller.abort();
    container.hidden = true;
    status.textContent = "We couldn't load this product. Please try again.";
    retry.hidden = false;
  } finally {
    clearTimeout(timeout);
    container.setAttribute("aria-busy", "false");
  }
}

retry.addEventListener("click", loadProduct);
loadProduct();
