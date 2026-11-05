import { Outlet } from "react-router";
import Footer from "./Footer.jsx";
import Header from "./Header.jsx";

export default function Layout() {
  return (
    <div className="app-shell">
      <div className="catalog-page">
        <a className="skip-link" href="#main-content">Skip to content</a>
      </div>
      <Header />
      <Outlet />
      <Footer />
    </div>
  );
}
