import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useCartCount } from "../hooks/useCartCount.js";
import { loginHref } from "../lib/auth-utils.js";
import { canUseCart, canUseWishlist, headerLinks, isPublicPath } from "../lib/role-routes.js";
import { searchHintTerms } from "../lib/search-hints.js";
import { headerScroll } from "../lib/site-header.js";
import AccountMenu from "./AccountMenu.jsx";
import CategoryMenu from "./CategoryMenu.jsx";
import HeaderSearch from "./HeaderSearch.jsx";
import "../styles/site-header.css";

// The Siesta header (SCRUM-164, DESIGN.md Header). One dark row, sticky at the top: the logo, the Categories button
// (its panel lists every category), the search, then wishlist, account and cart. It is 72px high and shrinks to 56px
// while the visitor scrolls down, back to 72px when they scroll up. Below 769px the search moves under the first row.
// Tests and older docs know #auth-link, #user-name, #logout, .account-menu, .site-cart and .site-search, so they stay.
export default function Header() {
  const { account, authReady, logout, cartMergeVersion } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutFailed, setLogoutFailed] = useState(false);
  const [search, setSearch] = useState("");
  const [compact, setCompact] = useState(false);
  const [categoryNames, setCategoryNames] = useState([]);
  const authLink = useRef(null);
  const focusAuthLink = useRef(false);
  const role = account?.role ?? null;
  const showCart = canUseCart(role);
  // Wait for the first account check, so a signed-in visitor's page load does not also ask for a guest cart.
  const cartCount = useCartCount(authReady && showCart, `${location.pathname}|${account?.id ?? "guest"}|${cartMergeVersion ?? 0}`);
  const onCategories = useCallback(names => {
    setCategoryNames(current => (current.join("\n") === names.join("\n") ? current : names));
  }, []);
  const hintTerms = useMemo(() => searchHintTerms(categoryNames), [categoryNames]);

  // The Login link only exists after React has rendered the signed-out header, so focus moves to it here.
  useEffect(() => {
    if (account || !focusAuthLink.current) return;
    focusAuthLink.current = false;
    authLink.current?.focus();
  }, [account]);

  // On the product list the field shows the current search; elsewhere it starts empty.
  useEffect(() => {
    setSearch(location.pathname === "/products" ? new URLSearchParams(location.search).get("search") ?? "" : "");
  }, [location.pathname, location.search]);

  // Shrink while scrolling down, grow while scrolling up (one measurement per frame).
  useEffect(() => {
    let state = headerScroll(undefined, window.scrollY);
    let frame = 0;
    setCompact(state.compact);
    const onScroll = () => {
      if (frame) return;
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        const next = headerScroll(state, window.scrollY);
        if (next.compact !== state.compact) setCompact(next.compact);
        state = next;
      });
    };
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => {
      window.removeEventListener("scroll", onScroll);
      window.cancelAnimationFrame(frame);
    };
  }, []);

  async function handleLogout() {
    setLoggingOut(true);
    setLogoutFailed(false);
    focusAuthLink.current = true;
    try {
      // A private page is no use to a signed-out visitor, and the login page would send them back to it.
      // Leaving first (instead of after the logout) means the route guard never gets to redirect to login.
      if (!isPublicPath(location.pathname)) navigate("/", { replace: true });
      await logout();
    } catch {
      focusAuthLink.current = false;
      setLogoutFailed(true);
    } finally {
      setLoggingOut(false);
    }
  }

  function submitSearch(event) {
    event.preventDefault();
    const text = search.trim();
    navigate(text ? `/products?${new URLSearchParams({ search: text })}` : "/products");
  }

  const cartLabel = cartCount === null ? "Cart" : `Cart, ${cartCount} ${cartCount === 1 ? "item" : "items"}`;

  return (
    <header className={compact ? "site-header is-compact" : "site-header"}>
      <div className="site-header-bar">
        <div className="site-header-inner">
          <Link className="site-logo" to="/">
            <span className="site-logo-mark" aria-hidden="true" />
            <span className="site-logo-word">Siesta</span>
          </Link>
          <CategoryMenu onCategories={onCategories} />
          <HeaderSearch value={search} onChange={setSearch} onSubmit={submitSearch} terms={hintTerms} />
          <nav className="site-actions" aria-label="Account">
            {headerLinks(role).map(([to, label]) => (
              <NavLink key={to} className="site-header-link" to={to} end>{label}</NavLink>
            ))}
            {canUseWishlist(role) && (
              <NavLink className="site-icon-link site-wishlist" to="/wishlist" end aria-label="Wishlist">
                <svg viewBox="0 0 24 24" width="22" height="22" aria-hidden="true" focusable="false">
                  <path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z" fill="none" stroke="currentColor"
                    strokeWidth="2" strokeLinejoin="round" />
                </svg>
              </NavLink>
            )}
            {account
              ? <AccountMenu account={account} loggingOut={loggingOut} logoutFailed={logoutFailed} onLogout={handleLogout} />
              : <NavLink id="auth-link" className="site-header-link" ref={authLink} to={loginHref(location)} end>Login</NavLink>}
            {showCart && (
              <NavLink className="site-icon-link site-cart" to="/cart" end aria-label={cartLabel}>
                <svg viewBox="0 0 24 24" width="24" height="24" aria-hidden="true" focusable="false">
                  <path d="M3 4h2.2l2.1 10.2a1 1 0 0 0 1 .8h8.9a1 1 0 0 0 1-.8L20 7H6.2" fill="none" stroke="currentColor"
                    strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" />
                  <circle cx="9.5" cy="19.5" r="1.5" fill="currentColor" />
                  <circle cx="17" cy="19.5" r="1.5" fill="currentColor" />
                </svg>
                {cartCount > 0 && <span className="site-cart-count" aria-hidden="true">{cartCount > 99 ? "99+" : cartCount}</span>}
              </NavLink>
            )}
          </nav>
        </div>
      </div>
    </header>
  );
}
