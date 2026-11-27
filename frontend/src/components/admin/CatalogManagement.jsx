import { useCallback, useEffect, useMemo, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../../auth/useSessionExpiry.js";
import {
  createCategory, createProduct, deleteCategory, editCategory, editProduct, fetchCategories, fetchProducts, removeProduct
} from "../../lib/catalog-management-api.js";
import {
  buildCategoryRequest, buildProductRequest, catalogFailureMessage, categoryOptions, EMPTY_PRODUCT_FORM, mergeById, parentName,
  productToForm
} from "../../lib/catalog-management.js";
import { formatPrice } from "../../lib/product-display.js";
import { useRequest } from "../../lib/useRequest.js";
import { ErrorState } from "../RequestStates.js";
import Button from "../ui/Button.jsx";
import Input from "../ui/Input.jsx";
import Table from "../ui/Table.js";

const WRITE_TIMEOUT_MS = 15000;
const EMPTY_CATEGORY_FORM = { name: "", description: "", parentId: "" };

// The product manager's catalog screen (SCRUM-77; API: docs/catalog-management.md). Categories and products are created,
// edited and removed here. The server checks everything again: a row changes on screen only after the server answered,
// and a write is never retried by itself. "Remove" takes a product off sale, so old orders keep their product.
export default function CatalogManagement() {
  const { csrfToken } = useAuth();
  const categoriesRead = useRequest();
  const productsRead = useRequest();
  const [expired, setExpired] = useState(null);
  useSessionExpiry(categoriesRead.error ?? productsRead.error, expired);

  const [categories, setCategories] = useState(null);
  const [products, setProducts] = useState(null);
  const [cursor, setCursor] = useState(null);
  const [notice, setNotice] = useState({ kind: "", text: "" });

  const [categoryForm, setCategoryForm] = useState(EMPTY_CATEGORY_FORM);
  const [categoryErrors, setCategoryErrors] = useState({});
  const [editingCategory, setEditingCategory] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(null);
  const [savingCategory, setSavingCategory] = useState(false);

  const [productForm, setProductForm] = useState(EMPTY_PRODUCT_FORM);
  const [productErrors, setProductErrors] = useState({});
  const [editingProduct, setEditingProduct] = useState(null);
  const [savingProduct, setSavingProduct] = useState(false);
  const [busyProduct, setBusyProduct] = useState(null);

  const loadCategories = useCallback(async () => {
    const result = await categoriesRead.run(({ signal }) => fetchCategories({ signal }));
    if (result.status === "success") setCategories(result.data);
  }, [categoriesRead.run]);

  const loadProducts = useCallback(async (afterId = 0) => {
    const result = await productsRead.run(({ signal }) => fetchProducts({ afterId, signal }));
    if (result.status !== "success") return;
    setProducts(previous => afterId === 0 || !previous ? result.data.products : mergeById(previous, result.data.products));
    setCursor(result.data.nextAfterId);
  }, [productsRead.run]);

  useEffect(() => { void loadCategories(); void loadProducts(); return () => { categoriesRead.cancel(); productsRead.cancel(); }; },
    [loadCategories, loadProducts, categoriesRead.cancel, productsRead.cancel]);

  const options = useMemo(() => categoryOptions(categories ?? []), [categories]);
  const write = async (action, subject) => {
    try {
      const token = await csrfToken();
      return { ok: true, value: await action(token, { signal: AbortSignal.timeout(WRITE_TIMEOUT_MS) }) };
    } catch (error) {
      if (error?.status === 401) setExpired(error);
      if (error?.status === 404) { void loadCategories(); void loadProducts(); }
      setNotice({ kind: "error", text: catalogFailureMessage(error, subject) });
      return { ok: false, error };
    }
  };
  const done = text => setNotice({ kind: "success", text });

  // ---- categories ----
  function startEditCategory(category) {
    setEditingCategory(category);
    setCategoryForm({ name: category.name, description: category.description ?? "", parentId: category.parentId ? String(category.parentId) : "" });
    setCategoryErrors({});
  }
  function resetCategoryForm() { setEditingCategory(null); setCategoryForm(EMPTY_CATEGORY_FORM); setCategoryErrors({}); }

  async function submitCategory(event) {
    event.preventDefault();
    setNotice({ kind: "", text: "" });
    const built = buildCategoryRequest(categoryForm, { editing: editingCategory !== null });
    setCategoryErrors(built.ok ? {} : built.errors);
    if (!built.ok) return;
    setSavingCategory(true);
    const result = await write((token, options) => editingCategory
      ? editCategory(editingCategory.id, built.request, token, options) : createCategory(built.request, token, options), "category");
    setSavingCategory(false);
    if (!result.ok) return;
    setCategories(previous => mergeById(previous, [result.value]));
    done(editingCategory ? `Category saved: ${result.value.name}.` : `Category created: ${result.value.name}.`);
    resetCategoryForm();
  }

  async function removeCategory(category) {
    setConfirmDelete(null);
    const result = await write((token, options) => deleteCategory(category.id, token, options), "category");
    if (!result.ok) return;
    setCategories(previous => previous.filter(row => row.id !== category.id));
    done(`Category deleted: ${category.name}.`);
  }

  // ---- products ----
  function startEditProduct(product) {
    setEditingProduct(product);
    setProductForm(productToForm(product));
    setProductErrors({});
    document.getElementById("catalog-product-form")?.scrollIntoView({ block: "nearest" });
  }
  function resetProductForm() { setEditingProduct(null); setProductForm(EMPTY_PRODUCT_FORM); setProductErrors({}); }

  async function submitProduct(event) {
    event.preventDefault();
    setNotice({ kind: "", text: "" });
    const built = buildProductRequest(productForm, { editing: editingProduct !== null, isActive: editingProduct?.isActive ?? true });
    setProductErrors(built.ok ? {} : built.errors);
    if (!built.ok) return;
    setSavingProduct(true);
    const result = await write((token, options) => editingProduct
      ? editProduct(editingProduct.id, built.request, token, options) : createProduct(built.request, token, options), "product");
    setSavingProduct(false);
    if (!result.ok) {
      if (result.error?.status === 409) setProductErrors({ sku: catalogFailureMessage(result.error, "product") });
      return;
    }
    setProducts(previous => mergeById(previous, [result.value]).sort((a, b) => a.id - b.id));
    void loadCategories(); // the product counts changed
    done(editingProduct ? `Product saved: ${result.value.name}.` : `Product created: ${result.value.name}.`);
    resetProductForm();
  }

  async function takeOffSale(product) {
    setBusyProduct(product.id);
    const result = await write((token, options) => removeProduct(product.id, token, options), "product");
    setBusyProduct(null);
    if (!result.ok) return;
    setProducts(previous => mergeById(previous, [result.value]));
    done(`Taken off sale: ${result.value.name}. Past orders keep it.`);
  }

  async function putBackOnSale(product) {
    setBusyProduct(product.id);
    const built = buildProductRequest(productToForm(product), { editing: true, isActive: true });
    const result = built.ok ? await write((token, options) => editProduct(product.id, built.request, token, options), "product") : { ok: false };
    setBusyProduct(null);
    if (!result.ok) { if (!built.ok) setNotice({ kind: "error", text: "Open the product with Edit and complete its details first." }); return; }
    setProducts(previous => mergeById(previous, [result.value]));
    done(`Back on sale: ${result.value.name}.`);
  }

  const categoryColumns = [
    { key: "name", header: "Category", rowHeader: true },
    { key: "parent", header: "Parent", render: row => parentName(row, categories ?? []) },
    { key: "productCount", header: "Products", align: "end" },
    { key: "childCount", header: "Subcategories", align: "end" },
    { key: "actions", header: "", render: row => <div className="admin-row-actions">
      <Button variant="secondary" onClick={() => startEditCategory(row)} aria-label={`Edit category ${row.name}`}>Edit</Button>
      {confirmDelete === row.id
        ? <>
          <Button onClick={() => removeCategory(row)}>Confirm delete</Button>
          <Button variant="secondary" onClick={() => setConfirmDelete(null)}>Keep</Button>
        </>
        : <Button variant="secondary" onClick={() => setConfirmDelete(row.id)} aria-label={`Delete category ${row.name}`}>Delete</Button>}
    </div> }
  ];

  const productColumns = [
    { key: "name", header: "Product", rowHeader: true, render: row => `${row.name} (${row.model})` },
    { key: "sku", header: "SKU" },
    { key: "categoryName", header: "Category" },
    { key: "price", header: "Price", align: "end", render: row => formatPrice(row.price) },
    { key: "stockQuantity", header: "In stock", align: "end" },
    { key: "isActive", header: "Status", render: row => row.isActive ? "On sale" : "Off sale" },
    { key: "actions", header: "", render: row => <div className="admin-row-actions">
      <Button variant="secondary" onClick={() => startEditProduct(row)} aria-label={`Edit product ${row.name}`}>Edit</Button>
      {row.isActive
        ? <Button variant="secondary" disabled={busyProduct === row.id} onClick={() => takeOffSale(row)}
          aria-label={`Take ${row.name} off sale`}>{busyProduct === row.id ? "Working…" : "Take off sale"}</Button>
        : <Button variant="secondary" disabled={busyProduct === row.id} onClick={() => putBackOnSale(row)}
          aria-label={`Put ${row.name} back on sale`}>{busyProduct === row.id ? "Working…" : "Put back on sale"}</Button>}
    </div> }
  ];

  const set = (setter, field) => event => setter(previous => ({ ...previous, [field]: event.target.value }));
  const productsLoading = productsRead.status === "loading";
  const categoriesLoading = categoriesRead.status === "loading";

  return <>
    <header className="admin-section-head">
      <h1 tabIndex={-1}>Catalog</h1>
      <p className="admin-lead">Add, edit and remove categories and products. A removed product leaves the shop but stays in
        past orders. Stock counts are managed on their own screen.</p>
    </header>
    <p className={notice.kind === "error" ? "request-state request-state--error" : "admin-sr"} role={notice.kind === "error" ? "alert" : "status"}>
      {notice.text}
    </p>
    {notice.kind === "success" && <p className="admin-pricing-notice" aria-hidden="true">{notice.text}</p>}

    <section aria-labelledby="catalog-categories">
      <div className="admin-panel-head"><h2 id="catalog-categories">Categories</h2></div>
      <ErrorState error={categoriesRead.error} onRetry={loadCategories} retrying={categoriesLoading} />
      <Table columns={categoryColumns} rows={categories ?? []} loading={categoriesLoading && !categories} caption="Categories"
        loadingLabel="Loading categories…" emptyText="No category yet. Add the first one below." />
      <form className="admin-pricing-form" onSubmit={submitCategory} noValidate>
        <h3>{editingCategory ? `Edit category: ${editingCategory.name}` : "Add a category"}</h3>
        <Input label="Name" value={categoryForm.name} maxLength={100} error={categoryErrors.name} onChange={set(setCategoryForm, "name")} />
        <Input label="Description (optional)" multiline value={categoryForm.description} error={categoryErrors.description}
          onChange={set(setCategoryForm, "description")} />
        <Input label="Parent category (optional)" select value={categoryForm.parentId} disabled={editingCategory !== null}
          hint={editingCategory ? "A category keeps its parent." : undefined} onChange={set(setCategoryForm, "parentId")}>
          <option value="">None (top level)</option>
          {options.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}
        </Input>
        <div className="admin-row-actions">
          <Button type="submit" disabled={savingCategory}>{savingCategory ? "Saving…" : editingCategory ? "Save category" : "Add category"}</Button>
          {editingCategory && <Button variant="secondary" onClick={resetCategoryForm}>Cancel</Button>}
        </div>
      </form>
    </section>

    <section aria-labelledby="catalog-products">
      <div className="admin-panel-head"><h2 id="catalog-products">Products</h2></div>
      <ErrorState error={productsRead.error} onRetry={() => loadProducts(0)} retrying={productsLoading} />
      <Table columns={productColumns} rows={products ?? []} loading={productsLoading && !products} caption="All products"
        loadingLabel="Loading products…" emptyText="No product yet. Add the first one below." />
      {cursor !== null && products && <Button variant="secondary" className="admin-more" disabled={productsLoading}
        onClick={() => loadProducts(cursor)}>Load more products</Button>}
      <form id="catalog-product-form" className="admin-pricing-form" onSubmit={submitProduct} noValidate>
        <h3>{editingProduct ? `Edit product: ${editingProduct.name}` : "Add a product"}</h3>
        <Input label="Category" select value={productForm.categoryId} error={productErrors.categoryId} onChange={set(setProductForm, "categoryId")}>
          <option value="">Choose a category…</option>
          {options.map(option => <option key={option.id} value={option.id}>{option.label}</option>)}
        </Input>
        <Input label="Name" value={productForm.name} maxLength={200} error={productErrors.name} onChange={set(setProductForm, "name")} />
        <Input label="Model" value={productForm.model} maxLength={100} error={productErrors.model} onChange={set(setProductForm, "model")} />
        <Input label="SKU" value={productForm.sku} maxLength={64} error={productErrors.sku} hint="Unique, no spaces."
          onChange={set(setProductForm, "sku")} />
        <Input label="Serial number" value={productForm.serialNumber} maxLength={100} error={productErrors.serialNumber}
          onChange={set(setProductForm, "serialNumber")} />
        <Input label="Description" multiline value={productForm.description} error={productErrors.description}
          onChange={set(setProductForm, "description")} />
        <Input label="Price (TRY)" inputMode="decimal" autoComplete="off" value={productForm.price} error={productErrors.price}
          hint="For example 1999.90" onChange={set(setProductForm, "price")} />
        {editingProduct
          ? <p className="ui-field-hint">In stock: {editingProduct.stockQuantity}. Change the count on the <Link to="/manage/stock">Stock screen</Link>.</p>
          : <Input label="Stock quantity" inputMode="numeric" autoComplete="off" value={productForm.stockQuantity}
            error={productErrors.stockQuantity} onChange={set(setProductForm, "stockQuantity")} />}
        <Input label="Warranty" value={productForm.warrantyStatus} maxLength={100} error={productErrors.warrantyStatus}
          hint="For example 2 years" onChange={set(setProductForm, "warrantyStatus")} />
        <Input label="Distributor" value={productForm.distributorName} maxLength={150} error={productErrors.distributorName}
          onChange={set(setProductForm, "distributorName")} />
        <Input label="Distributor contact" value={productForm.distributorContact} maxLength={500} error={productErrors.distributorContact}
          onChange={set(setProductForm, "distributorContact")} />
        <div className="admin-row-actions">
          <Button type="submit" disabled={savingProduct}>{savingProduct ? "Saving…" : editingProduct ? "Save product" : "Add product"}</Button>
          {editingProduct && <Button variant="secondary" onClick={resetProductForm}>Cancel</Button>}
        </div>
      </form>
    </section>
  </>;
}
