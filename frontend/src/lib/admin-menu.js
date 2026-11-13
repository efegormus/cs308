// The admin panel's sections per role (SCRUM-124). Pure, so Node can test it. The server still checks every role;
// this only decides what the menu offers. Every path is under /manage, which role-routes.js already protects.

const OVERVIEW = { id: "overview", path: "/manage", label: "Overview" };

const SECTIONS = {
  ProductManager: [
    OVERVIEW,
    { id: "deliveries", path: "/manage/deliveries", label: "Deliveries" },
    { id: "reviews", path: "/manage/reviews", label: "Reviews" },
    { id: "low-stock", path: "/manage/low-stock", label: "Low stock" }
  ],
  SalesManager: [
    OVERVIEW,
    { id: "invoices", path: "/manage/invoices", label: "Invoices" },
    { id: "discounts", path: "/manage/discounts", label: "Discounts" }
  ]
};

/** The menu items of a role, in menu order. Any other role gets none. */
export function adminSections(role) {
  return SECTIONS[role] ?? [];
}

/**
 * The section for the route's :section segment (undefined for /manage itself), or null when the role has no such
 * section, so the page can send the visitor back to the overview.
 */
export function resolveSection(role, segment) {
  const sections = adminSections(role);
  if (sections.length === 0) return null;
  if (segment === undefined || segment === null || segment === "") return sections[0];
  const id = String(segment).toLowerCase();
  return sections.find(section => section.id === id && section.id !== OVERVIEW.id) ?? null;
}
