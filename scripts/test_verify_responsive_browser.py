import unittest

from verify_responsive_browser import (
    APP,
    ADMIN_MENU,
    admin_sections,
    app_routes,
    format_table,
    group_small,
    missing_routes,
    small_target_report,
    visit_plan,
)


class ResponsiveBrowserHelpersTests(unittest.TestCase):
    def test_reads_the_route_paths_from_app_jsx(self):
        source = """<Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/manage/:section?" element={<AdminPanelPage />} />
        </Route>"""
        self.assertEqual(app_routes(source), ["/", "/manage/:section?"])

    def test_reads_every_admin_section_with_the_shared_overview(self):
        source = """const OVERVIEW = { id: "overview", path: "/manage", label: "Overview" };
const SECTIONS = {
  ProductManager: [
    OVERVIEW,
    { id: "deliveries", path: "/manage/deliveries", label: "Deliveries" }
  ],
  SalesManager: [
    OVERVIEW,
    { id: "invoices", path: "/manage/invoices", label: "Invoices" }
  ]
};"""
        self.assertEqual(admin_sections(source), {
            "ProductManager": ["/manage", "/manage/deliveries"],
            "SalesManager": ["/manage", "/manage/invoices"],
        })

    def test_every_route_of_the_real_app_has_a_visit_plan(self):
        routes = app_routes(APP.read_text(encoding="utf-8"))
        sections = admin_sections(ADMIN_MENU.read_text(encoding="utf-8"))
        self.assertGreater(len(routes), 10)
        self.assertEqual(missing_routes(routes, visit_plan(7, 9, sections)), [])
        manage = visit_plan(7, 9, sections)["/manage/:section?"]
        self.assertIn(("productmanager", "/manage/reviews"), manage)
        self.assertIn(("salesmanager", "/manage/invoices"), manage)

    def test_a_new_route_without_a_plan_is_reported(self):
        plan = visit_plan(7, 9, {})
        self.assertEqual(missing_routes(["/", "/new-page", "/manage/:section?"], plan), ["/new-page", "/manage/:section?"])

    def test_plan_uses_the_real_ids(self):
        plan = visit_plan(7, 9, {})
        self.assertIn((None, "/product?id=7"), plan["/product"])
        self.assertEqual(plan["/order-confirmation"], [("customer", "/order-confirmation?orderId=9")])

    def test_table_names_the_widest_element_only_on_overflow(self):
        table = format_table([
            {"path": "/cart", "account": None, "width": 375, "ok": True, "scrollWidth": 375, "clientWidth": 375,
             "widest": None},
            {"path": "/orders", "account": "customer", "width": 375, "ok": False, "scrollWidth": 412,
             "clientWidth": 375, "widest": "ul.order-list > li.order-card"},
        ]).splitlines()
        self.assertTrue(table[0].startswith("Route"))
        self.assertRegex(table[2], r"^/cart\s+guest\s+375\s+ok$")
        self.assertRegex(table[3], r"/orders\s+customer\s+375\s+OVERFLOW 412>375\s+ul.order-list > li.order-card$")

    def test_small_controls_are_grouped_per_page_and_kind(self):
        found = {}
        title = lambda index, height: {"selector": f"h3#_r_{index}_ > a", "pattern": "h3 > a", "label": f"Product {index}",
                                       "width": 200, "height": height}
        group_small(found, "/products", None, [title(1, 28), title(2, 28)])
        group_small(found, "/products", None, [title(1, 26)])  # the same link at another width
        report = small_target_report(found)
        self.assertEqual(report, '/products (guest): h3#_r_1_ > a (x2) "Product 1" 200x26')
        self.assertEqual(small_target_report({}), "")


if __name__ == "__main__":
    unittest.main()
