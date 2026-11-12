import { useEffect, useRef, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useCartCount } from "../hooks/useCartCount.js";
import { loginHref } from "../lib/auth-utils.js";
import { accountLabel, accountPath, canUseCart, headerLinks, isPublicPath, landingLabel, roleLinks } from "../lib/role-routes.js";
import CategoryMenu from "./CategoryMenu.jsx";
import Button from "./ui/Button.jsx";
import Input from "./ui/Input.jsx";
import "../styles/site-header.css";

// The same limit as GET /api/products (ProductEndpoints.MaxSearchLength).
const MAX_SEARCH_LENGTH = 100;

// The site header (SCRUM-123, DESIGN.md Header). Two rows at every width: the logo, the search, the account area and
// the cart, then the category bar. Below 769px the search moves under the logo and the categories wrap; there is no
// hamburger menu. auth.css no longer styles the header, but tests and older docs know #auth-link, #user-name and
// #logout, so those ids stay.
export default function Header() {
  const { account, authReady, logout, cartMergeVersion } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutFailed, setLogoutFailed] = useState(false);
  const [search, setSearch] = useState("");
  const authLink = useRef(null);
  const focusAuthLink = useRef(false);
  const role = account?.role ?? null;
  const showCart = canUseCart(role);
  // Wait for the first account check, so a signed-in visitor's page load does not also ask for a guest cart.
  const cartCount = useCartCount(authReady && showCart, `${location.pathname}|${account?.id ?? "guest"}|${cartMergeVersion ?? 0}`);

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
    <header className="site-header">
      <div className="site-header-inner">
        <div className="site-header-top">
          <Link className="site-logo" to="/">Online Store</Link>
          <form className="site-search" role="search" action="/products" onSubmit={submitSearch}>
            <Input id="site-search" className="site-search-field" label="Search products" hideLabel type="search"
              name="search" placeholder="Search products" autoComplete="off" maxLength={MAX_SEARCH_LENGTH}
              value={search} onChange={event => setSearch(event.target.value)} />
            <Button type="submit" className="site-search-button" aria-label="Search">
              <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
                <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
                <path d="M16.5 16.5L21 21" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
              </svg>
            </Button>
          </form>
          <nav className="site-actions" aria-label="Account">
            {headerLinks(role).map(([to, label]) => (
              <NavLink key={to} className="site-header-link" to={to} end>{label}</NavLink>
            ))}
            {account ? (
              <>
                {/* A profile icon (SCRUM-130): it opens the page of the role, and hovering or focusing it lists shortcuts. */}
                {landingLabel(account.role)
                  ? <div className="account-menu">
                    <NavLink id="user-name" className="account-icon" to={accountPath(account.role)} end
                      aria-label={`${account.name}, your ${landingLabel(account.role).toLowerCase()} page`}>
                      <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
                        <circle cx="12" cy="8" r="4" fill="none" stroke="currentColor" strokeWidth="2" />
                        <path d="M4 21c0-4.4 3.6-7 8-7s8 2.6 8 7" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                      </svg>
                    </NavLink>
                    <ul className="account-menu-list" aria-label="Account shortcuts">
                      <li className="account-menu-name">{account.name}</li>
                      <li><Link to={accountPath(account.role)}>{accountLabel(account.role)}</Link></li>
                      {roleLinks(account.role).map(([to, label]) => <li key={to}><Link to={to}>{label}</Link></li>)}
                      <li><button type="button" className="account-menu-logout" disabled={loggingOut} onClick={handleLogout}>Logout</button></li>
                    </ul>
                  </div>
                  : <span id="user-name" title={account.name}>{account.name}</span>}
                <button id="logout" type="button" disabled={loggingOut} onClick={handleLogout}>
                  {logoutFailed ? "Logout failed, retry" : "Logout"}
                </button>
              </>
            ) : (
              <NavLink id="auth-link" className="site-header-link" ref={authLink} to={loginHref(location)} end>Login</NavLink>
            )}
            {showCart && (
              <NavLink className="site-cart" to="/cart" end aria-label={cartLabel}>
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
        <CategoryMenu />
      </div>
    </header>
  );
}
