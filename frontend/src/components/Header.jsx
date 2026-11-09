import { useEffect, useRef, useState } from "react";
import { Link, NavLink, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { loginHref } from "../lib/auth-utils.js";

// The header from login.html. The .auth-page wrapper is what its rules in auth.css are scoped to.
// auth.css also targets #auth-link, #user-name and #logout, so those ids stay, as direct children of the nav.
export default function Header() {
  const { account, logout } = useAuth();
  const location = useLocation();
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
                <span id="user-name" title={account.name}>{account.name}</span>
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
