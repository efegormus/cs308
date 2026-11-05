import { Link, NavLink } from "react-router";

// The header from login.html. The .auth-page wrapper is what its rules in auth.css are scoped to.
export default function Header() {
  return (
    <div className="auth-page">
      <header className="site-header">
        <div className="site-header-inner">
          <Link className="logo" to="/">Online Store</Link>
          <nav className="nav" aria-label="Main navigation">
            <NavLink to="/" end>Home</NavLink>
            <NavLink to="/products" end>Products</NavLink>
            <a href="#">Cart</a>
            <NavLink to="/checkout" end>Checkout</NavLink>
            <NavLink id="auth-link" to="/login" end>Login</NavLink>
          </nav>
        </div>
      </header>
    </div>
  );
}
