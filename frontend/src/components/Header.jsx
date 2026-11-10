import { useEffect, useRef, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { loginHref } from "../lib/auth-utils.js";
import { accountPath, isPublicPath, landingLabel, roleLinks } from "../lib/role-routes.js";

// The header from login.html. The .auth-page wrapper is what its rules in auth.css are scoped to.
// auth.css also targets #auth-link, #user-name and #logout, so those ids stay, as direct children of the nav.
export default function Header() {
  const { account, logout } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const [loggingOut, setLoggingOut] = useState(false);
  const [logoutFailed, setLogoutFailed] = useState(false);
  const authLink = useRef(null);
  const focusAuthLink = useRef(false);

  // The Login link only exists after React has rendered the signed-out header, so focus moves to it here.
  useEffect(() => {
    if (account || !focusAuthLink.current) return;
    focusAuthLink.current = false;
    authLink.current?.focus();
  }, [account]);

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

  return (
    <div className="auth-page">
      <header className="site-header">
        <div className="site-header-inner">
          <Link className="logo" to="/">Online Store</Link>
          <nav className="nav" aria-label="Main navigation">
            <NavLink to="/" end>Home</NavLink>
            <NavLink to="/products" end>Products</NavLink>
            <NavLink to="/cart" end>Cart</NavLink>
            <NavLink to="/checkout" end>Checkout</NavLink>
            {account?.role === "Customer" && <NavLink to="/orders" end>My orders</NavLink>}
            {account?.role === "ProductManager" && <NavLink to="/deliveries" end>Deliveries</NavLink>}
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
                      {roleLinks(account.role).map(([to, label]) => <li key={to}><Link to={to}>{label}</Link></li>)}
                    </ul>
                  </div>
                  : <span id="user-name" title={account.name}>{account.name}</span>}
                <button id="logout" type="button" disabled={loggingOut} onClick={handleLogout}>
                  {logoutFailed ? "Logout failed, retry" : "Logout"}
                </button>
              </>
            ) : (
              <NavLink id="auth-link" ref={authLink} to={loginHref(location)} end>Login</NavLink>
            )}
          </nav>
        </div>
      </header>
    </div>
  );
}
