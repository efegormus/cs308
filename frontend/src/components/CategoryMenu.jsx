import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import { Link, useLocation, useNavigate } from "react-router";
import { fetchCategories, fetchProductPage, isValidProduct } from "../lib/catalog-api.js";
import { categoryProductsPath } from "../lib/catalog-query.js";
import { activeRootId, buildCategoryTree } from "../lib/category-tree.js";
import { formatPrice, safeImageUrl } from "../lib/product-display.js";
import { listIndexAfterKey } from "../lib/site-header.js";

// A category link filters /products to that category (SCRUM-165); it no longer scrolls to a section.
const categoryHref = id => categoryProductsPath(id);

// The category /products is filtered to, or null on any other page.
function chosenCategory(location) {
  if (location.pathname !== "/products") return null;
  const id = Number(new URLSearchParams(location.search).get("categoryId"));
  return Number.isSafeInteger(id) && id > 0 ? id : null;
}
// Hovering an item makes it active after this long, so a pointer passing over the list does not flicker.
const HOVER_DELAY = 150;
// Moving through the list with the keyboard waits this long before asking for the featured product.
const FEATURED_DELAY = 200;

// The "Categories" button of the header and its panel (SCRUM-164, DESIGN.md Header > Categories panel). The categories
// come from GET /api/categories (the request the product pages share). The panel lists every top-level category on the
// left; the active one shows its links ("All <category>" and its subcategories) and, when the catalog has one, its best
// seller. The button is a disclosure: aria-expanded and aria-controls; Escape or a click on the scrim closes the panel
// and gives the focus back to the button.
// - Items are buttons with one Tab stop between them; Up, Down, Home and End move, Enter or a click opens the category.
// - Hovering an item makes it active after 150ms (a quick pass does nothing); focus makes it active at once.
// - On a touch screen the first tap on another item only makes it active, so its subcategories can be reached; a tap
//   on the active item opens the category.
// onCategories receives the top-level names, which the search uses for its hint.
export default function CategoryMenu({ onCategories }) {
  const [tree, setTree] = useState([]);
  const [open, setOpen] = useState(false);
  const [activeId, setActiveId] = useState(null);
  const [pill, setPill] = useState(null);
  const [featured, setFeatured] = useState(null);
  const rootRef = useRef(null);
  const buttonRef = useRef(null);
  const listRef = useRef(null);
  const scrimRef = useRef(null);
  const hoverTimer = useRef(0);
  const focusActive = useRef(false);
  // How the current press started: the pointer type and whether the item was already active (focus makes it active
  // before the click arrives, so the click alone cannot tell).
  const press = useRef(null);
  const featuredCache = useRef(new Map());
  const location = useLocation();
  const navigate = useNavigate();

  useEffect(() => {
    const controller = new AbortController();
    fetchCategories({ signal: controller.signal })
      .then(categories => setTree(buildCategoryTree(categories)))
      .catch(() => { if (!controller.signal.aborted) setTree([]); });
    return () => controller.abort();
  }, []);

  useEffect(() => { onCategories?.(tree.map(item => item.name)); }, [tree, onCategories]);

  const currentCategory = chosenCategory(location);
  const currentRoot = activeRootId(tree, currentCategory);
  const active = tree.find(item => item.id === activeId) ?? null;

  function clearHover() {
    window.clearTimeout(hoverTimer.current);
    hoverTimer.current = 0;
  }

  function openPanel(moveFocus) {
    setActiveId(currentRoot ?? tree[0]?.id ?? null);
    focusActive.current = moveFocus;
    setOpen(true);
  }

  function closePanel(returnFocus) {
    clearHover();
    setOpen(false);
    if (returnFocus) buttonRef.current?.focus();
  }

  // A new page closes the panel.
  useEffect(() => { setOpen(false); clearHover(); }, [location.pathname, location.search, location.hash]);
  useEffect(() => clearHover, []);

  // Escape closes the panel wherever the focus is; a press outside the button and the panel closes it too. A press on
  // the scrim also gives the focus back to the button.
  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = event => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      closePanel(true);
    };
    const onPointerDown = event => {
      if (rootRef.current?.contains(event.target)) return;
      closePanel(false);
      // The press's own mousedown would move the focus to the page after this handler, so it goes back afterwards.
      if (event.target === scrimRef.current) window.setTimeout(() => buttonRef.current?.focus(), 0);
    };
    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, [open]);

  // Opening with the keyboard (Down arrow) moves the focus to the active item once the panel is shown.
  useEffect(() => {
    if (!open || !focusActive.current) return;
    focusActive.current = false;
    listRef.current?.querySelector('[data-active="true"]')?.focus();
  }, [open, activeId]);

  // The highlight pill follows the active item. It jumps there when the panel opens and slides after that.
  useLayoutEffect(() => {
    if (!open) {
      setPill(null);
      return;
    }
    const item = listRef.current?.querySelector('[data-active="true"]');
    if (!item) {
      setPill(null);
      return;
    }
    setPill(previous => ({ top: item.offsetTop, height: item.offsetHeight, instant: previous === null }));
  }, [open, activeId, tree]);

  // The featured card: the best seller of the active category (GET /api/products?sort=popularity), asked for only while
  // the panel is open and kept per category. No product, or a failed request, means no card.
  useEffect(() => {
    if (!open || activeId === null) {
      setFeatured(null);
      return undefined;
    }
    const cache = featuredCache.current;
    if (cache.has(activeId)) {
      setFeatured(cache.get(activeId));
      return undefined;
    }
    setFeatured(null);
    const controller = new AbortController();
    const timer = window.setTimeout(() => {
      fetchProductPage({ categoryId: activeId, sort: "popularity", page: 1, pageSize: 1, signal: controller.signal })
        .then(page => {
          const product = page.items.find(isValidProduct) ?? null;
          cache.set(activeId, product);
          setFeatured(product);
        })
        .catch(() => {});
    }, FEATURED_DELAY);
    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [open, activeId]);

  function toggle() {
    if (open) closePanel(false);
    else openPanel(false);
  }

  function onButtonKeyDown(event) {
    if (event.key !== "ArrowDown") return;
    event.preventDefault();
    if (open) listRef.current?.querySelector('[data-active="true"]')?.focus();
    else openPanel(true);
  }

  function onItemKeyDown(event, index) {
    const next = listIndexAfterKey(index, event.key, tree.length);
    if (next === null) return;
    event.preventDefault();
    listRef.current?.querySelectorAll(".category-panel-item")[next]?.focus();
  }

  function onItemPointerEnter(event, id) {
    if (event.pointerType !== "mouse") return;
    clearHover();
    hoverTimer.current = window.setTimeout(() => setActiveId(id), HOVER_DELAY);
  }

  function onItemClick(item) {
    clearHover();
    const { type, wasActive } = press.current ?? {};
    press.current = null;
    if ((type === "touch" || type === "pen") && !wasActive && item.children.length > 0) {
      setActiveId(item.id);
      return;
    }
    setActiveId(item.id);
    navigate(categoryHref(item.id));
  }

  // Tabbing out of the button and the panel closes it.
  function onBlur(event) {
    if (open && event.relatedTarget && !rootRef.current?.contains(event.relatedTarget)) closePanel(false);
  }

  const featuredImage = featured ? safeImageUrl(featured.imageUrl) : null;

  return (
    <div ref={rootRef} className="category-menu" onBlur={onBlur}>
      <button ref={buttonRef} id="category-button" type="button" className="category-toggle"
        aria-expanded={open} aria-controls="category-panel" onClick={toggle} onKeyDown={onButtonKeyDown}>
        <svg className="category-toggle-icon" viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
          <path d="M4 7h16M4 12h16M4 17h16" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
        <span className="category-toggle-label">Categories</span>
        <svg className="category-toggle-chevron" viewBox="0 0 12 12" width="12" height="12" aria-hidden="true" focusable="false">
          <path d="M2 4l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>

      <div id="category-panel" className="category-panel" data-open={open} role="region" aria-label="Categories">
        <div className="category-panel-inner">
          <nav className="category-panel-list" aria-label="Category list" ref={listRef}>
            {pill && <span className={pill.instant ? "category-panel-pill is-instant" : "category-panel-pill"} aria-hidden="true"
              style={{ top: `${pill.top}px`, height: `${pill.height}px` }} />}
            <ul>
              {tree.map((item, index) => {
                const isActive = item.id === activeId;
                return (
                  <li key={item.id}>
                    <button type="button" className="category-panel-item" data-active={isActive}
                      tabIndex={isActive || (activeId === null && index === 0) ? 0 : -1}
                      aria-current={currentRoot === item.id ? "page" : undefined}
                      onFocus={() => { clearHover(); setActiveId(item.id); }}
                      onPointerDown={event => { press.current = { type: event.pointerType, wasActive: isActive }; }}
                      onPointerEnter={event => onItemPointerEnter(event, item.id)} onPointerLeave={clearHover}
                      onKeyDown={event => onItemKeyDown(event, index)} onClick={() => onItemClick(item)}>
                      <span>{item.name}</span>
                      {item.children.length > 0 && (
                        <svg viewBox="0 0 12 12" width="12" height="12" aria-hidden="true" focusable="false">
                          <path d="M4 2l4 4-4 4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
                        </svg>
                      )}
                    </button>
                  </li>
                );
              })}
            </ul>
            <Link className="category-panel-all" to="/products"
              aria-current={location.pathname === "/products" && currentCategory === null ? "page" : undefined}>All products</Link>
          </nav>

          {active && (
            <div className="category-panel-links">
              <p className="category-panel-heading">{active.name}</p>
              {/* The key restarts the links' entrance when the panel opens or the active category changes. */}
              <ul key={open ? active.id : "closed"}>
                {[{ id: active.id, name: `All ${active.name}` }, ...active.children].map((link, index) => (
                  <li key={link.id} style={{ "--i": index }}>
                    <Link to={categoryHref(link.id)} aria-current={currentCategory === link.id ? "page" : undefined}>{link.name}</Link>
                  </li>
                ))}
              </ul>
            </div>
          )}

          {active && featured && (
            <Link className="category-panel-featured" to={`/product?id=${featured.id}`}>
              {featuredImage && (
                <span className="category-panel-featured-media">
                  <img src={featuredImage} alt="" loading="lazy" onError={event => { event.currentTarget.hidden = true; }} />
                </span>
              )}
              <span className="category-panel-featured-body">
                <span className="category-panel-featured-kicker">Popular in {active.name}</span>
                <span className="category-panel-featured-name">{featured.name}</span>
                <span className="category-panel-featured-price">{formatPrice(featured.price)}</span>
              </span>
            </Link>
          )}
        </div>
      </div>

      {createPortal(<div ref={scrimRef} className="site-header-scrim" data-open={open} aria-hidden="true" />, document.body)}
    </div>
  );
}
