import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { footerLinks } from "../lib/role-routes.js";
import "../styles/site-footer.css";

// The site footer (SCRUM-123, DESIGN.md Footer): a dark bar with the store name and a few links. There is no
// support link until the customer chat (requirement 13) exists; /support is the agents' panel.
export default function Footer() {
  const { account } = useAuth();
  return (
    <footer className="site-footer">
      <div className="site-footer-inner">
        <p className="site-footer-brand">Online Store</p>
        <nav aria-label="Footer">
          <ul className="site-footer-links">
            {footerLinks(account?.role ?? null).map(([to, label]) => <li key={to}><Link to={to}>{label}</Link></li>)}
          </ul>
        </nav>
        <p className="site-footer-legal">&copy; 2026 Online Store &middot; CS 308 Team</p>
      </div>
    </footer>
  );
}
