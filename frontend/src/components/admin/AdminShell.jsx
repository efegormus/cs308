import { useEffect, useId, useRef, useState } from "react";
import { NavLink, useLocation } from "react-router";
import { adminSections } from "../../lib/admin-menu.js";
import { motionVars } from "../../lib/motion.js";

// The admin panel frame (SCRUM-124): a 240px menu on the left from 768px up; below 768px the menu is a disclosure
// button at the top that opens the same list (DESIGN.md, Admin panel). The section's content is keyed by its id,
// so a new section replays the entrance animation.
export default function AdminShell({ account, section, children }) {
  const items = adminSections(account.role);
  const [open, setOpen] = useState(false);
  const toggle = useRef(null);
  const listId = useId();
  const { pathname } = useLocation();

  // A chosen item closes the mobile menu.
  useEffect(() => { setOpen(false); }, [pathname]);

  function onKeyDown(event) {
    if (event.key === "Escape" && open) {
      setOpen(false);
      toggle.current?.focus();
    }
  }

  return <div className="admin-shell" style={motionVars()}>
    <nav className="admin-nav" aria-label="Management sections" onKeyDown={onKeyDown}>
      <button ref={toggle} type="button" className="admin-nav-toggle" aria-expanded={open} aria-controls={listId}
        onClick={() => setOpen(value => !value)}>
        <span>Menu</span><span className="admin-nav-current">{section.label}</span>
        <svg className="admin-nav-chevron" viewBox="0 0 16 16" width="16" height="16" aria-hidden="true" focusable="false">
          <path d="M3 6l5 5 5-5" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      <ul id={listId} className={open ? "admin-nav-list is-open" : "admin-nav-list"}>
        {items.map(item => <li key={item.id}>
          <NavLink to={item.path} end className="admin-nav-link">{item.label}</NavLink>
        </li>)}
      </ul>
    </nav>
    <div className="admin-content">
      <p className="admin-account">{account.name} · {account.role === "SalesManager" ? "Sales manager" : "Product manager"}</p>
      <div key={section.id} className="admin-section">{children}</div>
    </div>
  </div>;
}
