import { useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { accountMenuLinks } from "../lib/role-routes.js";
import { initials } from "../lib/site-header.js";

// The signed-in account area of the header (SCRUM-164, DESIGN.md Header > Account area): a "name pill" (initials
// avatar, the full name cut with "…", a chevron) that opens the account menu right under it, as one shape. The pill is
// a disclosure button (aria-expanded); Escape closes the menu and returns the focus to it, a press outside closes it,
// and so does tabbing out. Down arrow opens it and moves to the first entry. The ids #user-name (the pill) and #logout
// (Sign out) stay, because older tests and docs know them.
export default function AccountMenu({ account, loggingOut, logoutFailed, onLogout }) {
  const [open, setOpen] = useState(false);
  const rootRef = useRef(null);
  const buttonRef = useRef(null);
  const focusFirst = useRef(false);
  const location = useLocation();
  const links = accountMenuLinks(account.role);

  useEffect(() => setOpen(false), [location.pathname, location.search]);

  useEffect(() => {
    if (!open) return undefined;
    const onKeyDown = event => {
      if (event.key !== "Escape") return;
      event.preventDefault();
      setOpen(false);
      buttonRef.current?.focus();
    };
    const onPointerDown = event => { if (!rootRef.current?.contains(event.target)) setOpen(false); };
    document.addEventListener("keydown", onKeyDown);
    document.addEventListener("pointerdown", onPointerDown);
    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.removeEventListener("pointerdown", onPointerDown);
    };
  }, [open]);

  useEffect(() => {
    if (!open || !focusFirst.current) return;
    focusFirst.current = false;
    rootRef.current?.querySelector(".account-menu-list :is(a, button)")?.focus();
  }, [open]);

  function onButtonKeyDown(event) {
    if (event.key !== "ArrowDown") return;
    event.preventDefault();
    if (open) rootRef.current?.querySelector(".account-menu-list :is(a, button)")?.focus();
    else {
      focusFirst.current = true;
      setOpen(true);
    }
  }

  function onBlur(event) {
    if (open && event.relatedTarget && !rootRef.current?.contains(event.relatedTarget)) setOpen(false);
  }

  return (
    <div ref={rootRef} className={open ? "account-menu is-open" : "account-menu"} onBlur={onBlur}>
      <button ref={buttonRef} id="user-name" type="button" className="account-pill" title={account.name}
        aria-label={`Account menu, ${account.name}`} aria-expanded={open} aria-controls="account-menu-list"
        onClick={() => setOpen(current => !current)} onKeyDown={onButtonKeyDown}>
        <span className="account-pill-avatar" aria-hidden="true">{initials(account.name)}</span>
        <span className="account-pill-name" aria-hidden="true">{account.name}</span>
        <svg className="account-pill-chevron" viewBox="0 0 12 12" width="12" height="12" aria-hidden="true" focusable="false">
          <path d="M2 4l4 4 4-4" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      </button>
      <div id="account-menu-list" className="account-menu-list">
        <ul aria-label="Account">
          {links.map(([to, label], index) => (
            <li key={to} style={{ "--i": index }}>
              <Link to={to} aria-current={location.pathname === to ? "page" : undefined} onClick={() => setOpen(false)}>{label}</Link>
            </li>
          ))}
          <li className="account-menu-signout" style={{ "--i": links.length }}>
            <button id="logout" type="button" disabled={loggingOut} onClick={onLogout}>
              {logoutFailed ? "Sign out failed, retry" : "Sign out"}
            </button>
          </li>
        </ul>
      </div>
    </div>
  );
}
