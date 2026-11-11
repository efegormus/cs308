import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { fetchCategories } from "../lib/catalog-api.js";
import { activeRootId, buildCategoryTree } from "../lib/category-tree.js";
import { categoryAnchorId, categoryIdFromHash } from "../lib/category-display.js";

const categoryHref = id => `/products#${categoryAnchorId(id)}`;

// The category bar of the header with its mega menu (SCRUM-123, DESIGN.md Header). The categories come from
// GET /api/categories; a top-level category with subcategories is a button that opens a full-width panel.
// - Mouse: hovering opens the panel, leaving closes it. Touch and click: the button toggles it.
// - Keyboard: Enter, Space or Down arrow opens it (Down also moves into it); Tab alone never opens a panel.
//   In the panel Up and Down move between links. Escape closes the panel and returns focus to its button.
// When the categories cannot be loaded the bar shows only "All products"; the product pages report the error.
export default function CategoryMenu() {
  const [tree, setTree] = useState([]);
  const [openId, setOpenId] = useState(null);
  const openedByHover = useRef(false);
  const focusFirstLink = useRef(false);
  const navRef = useRef(null);
  const location = useLocation();

  useEffect(() => {
    const controller = new AbortController();
    fetchCategories({ signal: controller.signal })
      .then(categories => setTree(buildCategoryTree(categories)))
      .catch(() => { if (!controller.signal.aborted) setTree([]); });
    return () => controller.abort();
  }, []);

  // A new page closes the panel.
  useEffect(() => setOpenId(null), [location.pathname, location.search, location.hash]);

  // Down arrow on a button opens its panel and moves into it, once the panel is shown.
  useEffect(() => {
    if (openId === null || !focusFirstLink.current) return;
    focusFirstLink.current = false;
    navRef.current?.querySelector(`#category-panel-${openId} a`)?.focus();
  }, [openId]);

  // A click or tap outside the bar closes the panel.
  useEffect(() => {
    if (openId === null) return undefined;
    const onPointerDown = event => { if (!navRef.current?.contains(event.target)) setOpenId(null); };
    document.addEventListener("pointerdown", onPointerDown);
    return () => document.removeEventListener("pointerdown", onPointerDown);
  }, [openId]);

  const onProducts = location.pathname === "/products";
  const hashCategory = onProducts ? categoryIdFromHash(location.hash) : null;
  const activeId = activeRootId(tree, hashCategory);

  function button(id) {
    return navRef.current?.querySelector(`#category-button-${id}`);
  }

  function panelLinks(id) {
    return [...(navRef.current?.querySelectorAll(`#category-panel-${id} a`) ?? [])];
  }

  function openWithHover(event, id) {
    if (event.pointerType !== "mouse") return;
    openedByHover.current = true;
    setOpenId(id);
  }

  function closeAfterHover(event, id) {
    if (event.pointerType !== "mouse" || !openedByHover.current) return;
    setOpenId(current => (current === id ? null : current));
  }

  function toggle(id) {
    // A mouse click on a panel that hovering opened keeps it open instead of closing it under the pointer.
    if (openId === id && openedByHover.current) {
      openedByHover.current = false;
      return;
    }
    openedByHover.current = false;
    setOpenId(current => (current === id ? null : id));
  }

  function onButtonKeyDown(event, id) {
    if (event.key !== "ArrowDown") return;
    event.preventDefault();
    openedByHover.current = false;
    if (openId === id) panelLinks(id)[0]?.focus();
    else {
      focusFirstLink.current = true;
      setOpenId(id);
    }
  }

  function onPanelKeyDown(event, id) {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    event.preventDefault();
    const links = panelLinks(id);
    const index = links.indexOf(document.activeElement);
    const next = event.key === "ArrowDown" ? Math.min(index + 1, links.length - 1) : index - 1;
    if (next < 0) button(id)?.focus();
    else links[next]?.focus();
  }

  function onKeyDown(event) {
    if (event.key !== "Escape" || openId === null) return;
    const id = openId;
    setOpenId(null);
    button(id)?.focus();
  }

  // Tabbing out of an item closes its panel.
  function onItemBlur(event, id) {
    if (!event.currentTarget.contains(event.relatedTarget)) setOpenId(current => (current === id ? null : current));
  }

  return (
    <nav ref={navRef} className="category-menu" aria-label="Categories" onKeyDown={onKeyDown}>
      <ul className="category-menu-bar">
        <li>
          <Link className="category-menu-link" to="/products"
            aria-current={onProducts && hashCategory === null ? "page" : undefined}>All products</Link>
        </li>
        {tree.map(item => item.children.length === 0
          ? (
            <li key={item.id}>
              <Link className="category-menu-link" to={categoryHref(item.id)}
                aria-current={activeId === item.id ? "page" : undefined}>{item.name}</Link>
            </li>
          )
          : (
            <li key={item.id} onPointerEnter={event => openWithHover(event, item.id)}
              onPointerLeave={event => closeAfterHover(event, item.id)} onBlur={event => onItemBlur(event, item.id)}>
              <button id={`category-button-${item.id}`} type="button"
                className={activeId === item.id ? "category-menu-link is-active" : "category-menu-link"}
                aria-expanded={openId === item.id} aria-controls={`category-panel-${item.id}`}
                onClick={() => toggle(item.id)} onKeyDown={event => onButtonKeyDown(event, item.id)}>
                {item.name}
                <svg className="category-menu-chevron" viewBox="0 0 12 12" width="12" height="12" aria-hidden="true" focusable="false">
                  <path d="M2 4l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
                </svg>
              </button>
              <div id={`category-panel-${item.id}`} className="category-menu-panel" hidden={openId !== item.id}
                onKeyDown={event => onPanelKeyDown(event, item.id)}>
                <ul className="category-menu-panel-list">
                  <li><Link to={categoryHref(item.id)} className="category-menu-all">All {item.name}</Link></li>
                  {item.children.map(child => (
                    <li key={child.id}>
                      <Link to={categoryHref(child.id)} aria-current={hashCategory === child.id ? "page" : undefined}>{child.name}</Link>
                    </li>
                  ))}
                </ul>
              </div>
            </li>
          ))}
      </ul>
    </nav>
  );
}
