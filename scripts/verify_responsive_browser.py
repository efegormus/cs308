#!/usr/bin/env python3
"""Check that no route of the React app scrolls sideways (SCRUM-134).

Every route in frontend/src/App.jsx is opened at 375, 768, 1024 and 1280px wide, as a guest or as the seeded demo
account that may see it (docs/demo-data.md), and the check asserts
document.documentElement.scrollWidth <= clientWidth. /manage/:section? is opened for every section of
frontend/src/lib/admin-menu.js. A route without a visit plan fails the run, so a new route cannot slip through.

It prints one table row per route and width (route, account, width, ok/overflow, widest element) and exits with 1
on any overflow. It also lists controls smaller than the 44px touch target (DESIGN.md); that list is for
information and never fails the run.

The demo customer's cart gets one product while /cart and /checkout are checked when it is empty, and loses it
again afterwards. Run it against a disposable local server only.
"""
import argparse
import json
from pathlib import Path
import re
import sys
import time
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent.parent
APP = ROOT / "frontend" / "src" / "App.jsx"
ADMIN_MENU = ROOT / "frontend" / "src" / "lib" / "admin-menu.js"
DEFAULT_ACCOUNTS = ROOT / ".local" / "db-redesign" / "demo-accounts.json"
WIDTHS = (375, 768, 1024, 1280)
TOUCH_TARGET = 44
ACCOUNTS = {
    "customer": "customer@onlinestore.invalid",
    "productmanager": "productmanager@onlinestore.invalid",
    "salesmanager": "salesmanager@onlinestore.invalid",
    "supportagent": "supportagent@onlinestore.invalid",
}

# Measured in the page. The widest element is the one reaching furthest right that is not clipped by a scrolling or
# clipping ancestor (a wide table inside its own scroll box does not make the page scroll).
MEASURE = """() => {
  const root = document.documentElement, width = root.clientWidth;
  const clipped = element => {
    for (let parent = element.parentElement; parent && parent !== document.body; parent = parent.parentElement) {
      if (getComputedStyle(parent).overflowX !== "visible") return true;
    }
    return false;
  };
  const name = element => {
    let text = element.tagName.toLowerCase();
    if (element.id) return `${text}#${element.id}`;
    const classes = [...element.classList].slice(0, 2);
    if (classes.length) text += "." + classes.join(".");
    const parent = element.parentElement;
    if (parent) {
      const same = [...parent.children].filter(child => child.tagName === element.tagName);
      if (same.length > 1) text += `:nth-of-type(${same.indexOf(element) + 1})`;
    }
    return text;
  };
  const selector = element => {
    const parts = [];
    for (let node = element; node && node !== document.body && parts.length < 4; node = node.parentElement) {
      parts.unshift(name(node));
      if (node.id) break;
    }
    return parts.join(" > ");
  };
  let widest = null, right = width;
  for (const element of document.body.querySelectorAll("*")) {
    const box = element.getBoundingClientRect();
    if (box.width === 0 && box.height === 0) continue;
    if (box.right + scrollX > right + 0.5 && !clipped(element)) { right = box.right + scrollX; widest = element; }
  }
  const small = [];
  const controls = "a[href], button, input:not([type=hidden]), select, textarea, summary, [role=button], [tabindex]:not([tabindex='-1'])";
  for (const element of document.querySelectorAll(controls)) {
    const box = element.getBoundingClientRect(), style = getComputedStyle(element);
    if (box.width <= 1 || box.height <= 1 || box.bottom < 0 || style.visibility === "hidden") continue;
    // A link inside a sentence is exempt (WCAG 2.5.8): an inline link whose parent has more text than the link.
    if (element.tagName === "A" && style.display === "inline"
        && element.parentElement.textContent.trim().length > element.textContent.trim().length) continue;
    // Too low, or too narrow without visible text (an icon control). A short text link that is 44px high passes.
    const text = (element.textContent || "").trim();
    if (box.height < TOUCH || (box.width < TOUCH && !text)) {
      const path = selector(element);
      small.push({ selector: path, label: (element.getAttribute("aria-label") || text || element.value || "").slice(0, 40),
        // Repeated controls (one per card or row) share a pattern: React ids, positions and numbers removed.
        pattern: path.replace(/#_r_\\w+_/g, "").replace(/:nth-of-type\\(\\d+\\)/g, "").replace(/-\\d+\\b/g, "-N"),
        width: Math.round(box.width), height: Math.round(box.height) });
    }
  }
  return { scrollWidth: root.scrollWidth, clientWidth: width, widest: widest ? selector(widest) : null,
    right: Math.round(right), small };
}""".replace("TOUCH", str(TOUCH_TARGET))

# Running finite animations (reveal-up, coverflow entrance, KPI rise) must end before measuring; endless ones
# (spinner, skeleton shimmer) never do and are ignored.
SETTLED = """() => document.getAnimations().every(animation => animation.playState !== "running"
  || animation.effect?.getComputedTiming().iterations === Infinity)"""


def app_routes(source):
    """The path of every <Route path="..."> in App.jsx, in order."""
    return re.findall(r'<Route\s+path="([^"]+)"', source)


def admin_sections(source):
    """{role: [path, ...]} from admin-menu.js. The shared OVERVIEW entry is /manage for every role."""
    overview = re.search(r'const OVERVIEW = \{[^}]*path: "([^"]+)"', source)
    sections = {}
    for role, body in re.findall(r"(\w+): \[(.*?)\]", source, re.S):
        paths = [overview.group(1)] if "OVERVIEW" in body and overview else []
        paths += re.findall(r'path: "([^"]+)"', body)
        sections[role] = paths
    return sections


def visit_plan(product_id, order_id, sections):
    """{route pattern: [(account or None for a guest, path), ...]}."""
    manage = [("productmanager", path) for path in sections.get("ProductManager", [])]
    manage += [("salesmanager", path) for path in sections.get("SalesManager", [])]
    product = f"/product?id={product_id}"
    return {
        "/": [(None, "/")],
        "/products": [(None, "/products")],
        "/product": [(None, product), ("customer", product)],
        "/cart": [(None, "/cart"), ("customer", "/cart")],
        "/login": [(None, "/login")],
        "/register": [(None, "/register")],
        "/checkout": [("customer", "/checkout")],
        "/order-confirmation": [("customer", f"/order-confirmation?orderId={order_id}")],
        "/orders": [("customer", "/orders")],
        "/wishlist": [("customer", "/wishlist")],
        "/deliveries": [("productmanager", "/deliveries")],
        "/dashboard": [("customer", "/dashboard")],
        "/profile": [("customer", "/profile")],
        "/manage/:section?": manage,
        "/support": [("supportagent", "/support")],
    }


def missing_routes(routes, plan):
    return [route for route in routes if not plan.get(route)]


def format_table(rows):
    """rows: dicts with path, account, width, ok, widest. Returns the table as text."""
    header = ("Route", "Account", "Width", "Result", "Widest element")
    lines = [(row["path"], row["account"] or "guest", str(row["width"]),
              "ok" if row["ok"] else f"OVERFLOW {row['scrollWidth']}>{row['clientWidth']}",
              "" if row["ok"] else (row["widest"] or "(not found)")) for row in rows]
    sizes = [max(len(item[index]) for item in [header, *lines]) for index in range(len(header))]
    render = lambda item: "  ".join(value.ljust(size) for value, size in zip(item, sizes)).rstrip()
    return "\n".join([render(header), render(tuple("-" * size for size in sizes)), *map(render, lines)])


def group_small(found, path, account, controls):
    """Adds one page's small controls to found: {(path, account): {pattern: {"example", "label", "size", "selectors"}}}.
    The same control seen at several widths counts once; the smallest height seen is kept."""
    page = found.setdefault((path, account), {})
    for control in controls:
        group = page.setdefault(control["pattern"], {"example": control["selector"], "label": control["label"],
                                                     "size": (control["width"], control["height"]), "selectors": set()})
        group["selectors"].add(control["selector"])
        if control["height"] < group["size"][1]:
            group["size"] = (control["width"], control["height"])


def small_target_report(found):
    """One line per kind of small control and page, with how many there are. Empty when nothing is small."""
    lines = []
    for (path, account), groups in found.items():
        for pattern, group in sorted(groups.items()):
            count = len(group["selectors"])
            width, height = group["size"]
            many = f" (x{count})" if count > 1 else ""
            lines.append(f"{path} ({account or 'guest'}): {group['example']}{many} \"{group['label']}\" {width}x{height}")
    return "\n".join(lines)


def verify(url, accounts_file, executable=None, widths=WIDTHS, screenshot_dir=None):
    from playwright.sync_api import sync_playwright

    if urlparse(url).hostname not in ("localhost", "127.0.0.1"):
        raise ValueError("The responsive check signs in demo accounts and changes a cart: local servers only.")
    passwords = json.loads(Path(accounts_file).read_text(encoding="utf-8"))
    routes = app_routes(APP.read_text(encoding="utf-8"))
    sections = admin_sections(ADMIN_MENU.read_text(encoding="utf-8"))
    if screenshot_dir:
        Path(screenshot_dir).mkdir(parents=True, exist_ok=True)

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        contexts = {None: browser.new_context()}
        errors = []

        def post(context, path, body):
            # Login shares a 10-a-minute limit per address with the other browser checks: wait once on 429.
            for attempt in range(2):
                token = context.request.get(url + "/api/auth/csrf").json()["token"]
                response = context.request.post(url + path, data=body, headers={"X-CSRF-TOKEN": token})
                if response.status != 429 or attempt:
                    return response, token
                print("Login limit reached; waiting 61 s.")
                time.sleep(61)

        def context_for(account):
            if account not in contexts:
                context = browser.new_context()
                response, _ = post(context, "/api/auth/login", {"email": ACCOUNTS[account], "password": passwords[ACCOUNTS[account]]})
                assert response.status == 200, f"Login failed for the demo {account} account: {response.status}"
                contexts[account] = context
            return contexts[account]

        # Real ids: the product with the longest name, and the customer's newest order.
        products = contexts[None].request.get(url + "/api/products").json()
        items = products["items"] if isinstance(products, dict) else products
        assert items, "The catalog is empty: seed the demo data first (docs/demo-data.md)."
        product = max(items, key=lambda item: (item["stockQuantity"] > 0, len(item["name"])))
        customer = context_for("customer")
        orders = customer.request.get(url + "/api/customer/orders").json()["orders"]
        assert orders, "The demo customer has no orders: run the full demo seed (--seed-demo)."
        plan = visit_plan(product["id"], orders[0]["orderId"], sections)
        missing = missing_routes(routes, plan)
        assert not missing, f"Routes in App.jsx without a visit plan: {missing}. Add them to visit_plan()."

        # Give the customer's cart a line, so /cart and /checkout show their full layout.
        added = None
        if not customer.request.get(url + "/api/cart").json()["items"]:
            token = customer.request.get(url + "/api/auth/csrf").json()["token"]
            response = customer.request.post(url + "/api/cart/items", data={"productId": product["id"], "quantity": 1},
                                             headers={"X-CSRF-TOKEN": token})
            assert response.ok, f"Could not add a product to the demo customer's cart: {response.status}"
            added = product["id"]

        rows, small = [], {}
        try:
            for route in routes:
                for account, path in plan[route]:
                    page = context_for(account).new_page()
                    page.on("pageerror", lambda error, where=path: errors.append(f"{where}: {error}"))
                    for width in widths:
                        page.set_viewport_size({"width": width, "height": 900})
                        page.goto(url + path, wait_until="networkidle")
                        landed = urlparse(page.url).path
                        expected = urlparse(path).path
                        assert landed == expected, f"{path} as {account or 'guest'} ended on {landed}"
                        try:
                            page.wait_for_function(SETTLED, timeout=5000)
                        except Exception:  # A page that keeps animating is still measured.
                            pass
                        result = page.evaluate(MEASURE)
                        ok = result["scrollWidth"] <= result["clientWidth"]
                        rows.append({"path": path, "account": account, "width": width, "ok": ok, **result})
                        group_small(small, path, account, result["small"])
                        if screenshot_dir:
                            name = re.sub(r"[^a-z0-9]+", "-", f"{path}-{account or 'guest'}-{width}".lower()).strip("-")
                            page.screenshot(path=str(Path(screenshot_dir) / f"{name}.png"), full_page=True)
                    page.close()
        finally:
            if added is not None:
                token = customer.request.get(url + "/api/auth/csrf").json()["token"]
                customer.request.delete(url + f"/api/cart/items/{added}", headers={"X-CSRF-TOKEN": token})
            browser.close()

    print(format_table(rows))
    report = small_target_report(small)
    if report:
        print(f"\nControls under {TOUCH_TARGET}px (information only; inline links in text are left out):")
        print(report)
    if errors:
        print("\nPage errors:\n" + "\n".join(errors))
    overflows = [row for row in rows if not row["ok"]]
    print(f"\n{len(rows)} checks, {len(overflows)} overflow(s), {len(errors)} page error(s).")
    return not overflows


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--url", default="http://127.0.0.1:5200")
    parser.add_argument("--accounts-file", default=str(DEFAULT_ACCOUNTS),
                        help="the demo credential file written by --seed-demo")
    parser.add_argument("--browser-executable")
    parser.add_argument("--widths", default=",".join(map(str, WIDTHS)))
    parser.add_argument("--screenshot-dir", help="save a full-page screenshot of every route and width here")
    args = parser.parse_args()
    passed = verify(args.url.rstrip("/"), args.accounts_file, args.browser_executable,
                    tuple(int(width) for width in args.widths.split(",")), args.screenshot_dir)
    sys.exit(0 if passed else 1)
