// Which page each role lands on after login, and which pages each role may open (SCRUM-130).
// Pure functions with no DOM and no network, so Node can test them. The server still enforces every role itself;
// this only decides where the browser sends the visitor.
import { safeReturnUrl } from "./auth-utils.js";

// The page of each role: the customer's account page and the staff panels. SCRUM-125 (dashboards) and
// SCRUM-124 (admin panel) fill these pages. The signed-in name in the header links here.
const ACCOUNT = {
  Customer: "/dashboard",
  ProductManager: "/manage",
  SalesManager: "/manage",
  SupportAgent: "/support"
};

// Where a visitor goes right after logging in. A customer goes back to the shop (the team's decision);
// staff go to their panel.
const LANDING = { ...ACCOUNT, Customer: "/" };

const LANDING_LABEL = {
  Customer: "Dashboard",
  ProductManager: "Manage",
  SalesManager: "Manage",
  SupportAgent: "Support"
};

// Pages that need a signed-in visitor with one of these roles. A path that is not listed is public.
const PROTECTED = [
  ["/dashboard", ["Customer"]],
  ["/checkout", ["Customer"]],
  ["/orders", ["Customer"]],
  ["/order-confirmation", ["Customer"]],
  ["/deliveries", ["ProductManager"]],
  ["/manage", ["ProductManager", "SalesManager"]],
  ["/support", ["SupportAgent"]]
];

function pathOf(url) {
  const path = String(url ?? "").split(/[?#]/, 1)[0].toLowerCase();
  return path.length > 1 ? path.replace(/\/+$/, "") : path;
}

function rulesFor(url) {
  const path = pathOf(url);
  return PROTECTED.find(([prefix]) => path === prefix || path.startsWith(`${prefix}/`)) ?? null;
}

export function landingPath(role) {
  return LANDING[role] ?? "/";
}

/** The role's own page, for the link on the signed-in name. */
export function accountPath(role) {
  return ACCOUNT[role] ?? "/";
}

export function landingLabel(role) {
  return LANDING_LABEL[role] ?? null;
}

// The text of the account menu's first entry (SCRUM-125), the link to accountPath(role).
const ACCOUNT_LABEL = {
  Customer: "My account",
  ProductManager: "Management",
  SalesManager: "Management",
  SupportAgent: "Support"
};

export function accountLabel(role) {
  return ACCOUNT_LABEL[role] ?? null;
}

/** True for a page that anyone, signed in or not, may open. */
export function isPublicPath(url) {
  return rulesFor(url) === null;
}

/** Whether a visitor with this role (null for a guest) may open the page. */
export function canAccess(role, url) {
  const rules = rulesFor(url);
  return rules === null || (typeof role === "string" && rules[1].includes(role));
}

/**
 * Where to send a visitor who has just logged in. The returnUrl is honoured only when it is a safe local path
 * that this role may open; otherwise the visitor lands on the page for their role.
 */
export function postLoginPath(account, returnUrl) {
  const safe = safeReturnUrl(returnUrl);
  if (safe !== "/" && canAccess(account?.role, safe)) return safe;
  return landingPath(account?.role);
}

// The shortcuts of each role: [path, label, description]. The header's account menu and the role's page both use them.
const ROLE_LINKS = {
  Customer: [
    ["/orders", "My orders", "Follow your orders from processing to delivery and download invoices."],
    ["/cart", "Cart", "Review the items you are about to buy."],
    ["/products", "Browse products", "Find phones, laptops, accessories and more."]
  ],
  ProductManager: [
    ["/deliveries", "Deliveries", "Check paid orders and move them to in transit or delivered."],
    ["/products", "Product catalog", "See the products customers see."]
  ],
  SalesManager: [
    ["/products", "Product catalog", "See the products customers see."]
  ],
  SupportAgent: [
    ["/products", "Product catalog", "Look up products while you help customers."]
  ]
};

/** The shortcuts a role sees. A link to a page the role may not open is never offered. */
export function roleLinks(role) {
  return (ROLE_LINKS[role] ?? []).filter(([to]) => canAccess(role, to));
}

/** Guests and customers have a cart; the cart API answers 403 to staff. role is null for a guest. */
export function canUseCart(role) {
  return role === null || role === undefined || role === "Customer";
}

/**
 * The text links in the header next to the account area (SCRUM-123). The logo is the home link, categories have
 * their own bar and checkout starts from the cart, so only the product manager's Deliveries page is left here.
 */
export function headerLinks(role) {
  return role === "ProductManager" ? [["/deliveries", "Deliveries"]] : [];
}

/** The footer links (SCRUM-123). Cart and Orders only for guests and customers; a guest is asked to log in for Orders. */
export function footerLinks(role) {
  return [["/products", "Products"], ...(canUseCart(role) ? [["/cart", "Cart"], ["/orders", "Orders"]] : [])];
}
