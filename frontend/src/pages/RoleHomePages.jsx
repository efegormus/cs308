// Landing pages of the staff roles after login (SCRUM-130). They are deliberately small: SCRUM-124 (admin panel)
// replaces the content by swapping the element of the route in App.jsx. The customer's page is DashboardPage.jsx.
// The role check and the loading, login-redirect and wrong-role states come from PurchaseAccess.
import { Link } from "react-router";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { roleLinks } from "../lib/role-routes.js";
import "../styles/role-home.css";

const ROLE_LABEL = {
  Customer: "Customer",
  ProductManager: "Product manager",
  SalesManager: "Sales manager",
  SupportAgent: "Support agent"
};

function RoleHome({ title, role }) {
  return <div className="route-page catalog-page purchase-page role-home-page">
    <title>{`${title} | Online Store`}</title>
    <main id="main-content" className="purchase-layout">
      <PurchaseAccess role={role}>{account => <>
        <header className="role-home-head">
          <p className="role-home-badge">{ROLE_LABEL[account.role] ?? account.role}</p>
          <h1 id="welcome">Welcome, {account.name}</h1>
          <p className="role-home-sub">{title} · {account.email}</p>
        </header>
        <nav aria-label={title}>
          <ul className="role-home-grid">
            {roleLinks(account.role).map(([to, label, text]) => <li key={to}>
              <Link className="role-home-card" to={to}>
                <span className="role-home-card-title">{label}</span>
                <span className="role-home-card-text">{text}</span>
              </Link>
            </li>)}
          </ul>
        </nav>
      </>}</PurchaseAccess>
    </main>
  </div>;
}

export function ManagePage() {
  return <RoleHome title="Management" role={["ProductManager", "SalesManager"]} />;
}

export function SupportPage() {
  return <RoleHome title="Support" role="SupportAgent" />;
}
