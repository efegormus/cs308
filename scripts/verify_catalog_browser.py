#!/usr/bin/env python3
"""Focused SCRUM-54 verification, updated for the SCRUM-165 All products page (sidebar and category sections):
real API/MySQL first, then controlled UI failure cases."""
import argparse
import math
import re
from urllib.parse import parse_qs, urlencode, urlparse

PRODUCTS_API = re.compile(r".*/api/products(?:\?.*)?$")


def is_products_api_url(url):
    return urlparse(url).path == "/api/products"


def assert_page_response(body, all_products, page_number, page_size):
    assert isinstance(body, dict), "Paginated products response must be an object."
    assert set(body) == {"items", "totalCount", "page", "pageSize", "totalPages"}
    assert isinstance(body["items"], list)
    assert body["totalCount"] == len(all_products)
    assert body["page"] == page_number
    assert body["pageSize"] == page_size
    assert body["totalPages"] == math.ceil(len(all_products) / page_size)
    expected = all_products[(page_number - 1) * page_size:page_number * page_size]
    assert [item["id"] for item in body["items"]] == [item["id"] for item in expected]


def mock_products_response(products_body, url):
    query = parse_qs(urlparse(url).query)
    if not isinstance(products_body, list):
        return products_body if "page" not in query and "pageSize" not in query else {
            "items": products_body, "totalCount": 0, "page": int(query.get("page", ["1"])[0]),
            "pageSize": int(query.get("pageSize", ["24"])[0]), "totalPages": 0
        }

    products = list(products_body)
    if "categoryId" in query:
        products = [item for item in products if item.get("categoryId") == int(query["categoryId"][0])]
    if "brandId" in query:
        products = [item for item in products if item.get("brandId") == int(query["brandId"][0])]
    if "minPrice" in query:
        products = [item for item in products if item.get("price", 0) >= float(query["minPrice"][0])]
    if "maxPrice" in query:
        products = [item for item in products if item.get("price", 0) <= float(query["maxPrice"][0])]
    if "search" in query:
        term = query["search"][0].casefold()
        products = [item for item in products if term in (item.get("name", "") + " " + item.get("description", "")).casefold()]

    sort = query.get("sort", [""])[0]
    order = query.get("order", [""])[0]
    if sort == "price":
        products.sort(key=lambda item: (item.get("name", "").casefold(), item["id"]))
        products.sort(key=lambda item: item.get("price", 0), reverse=order == "desc")
    elif sort == "popularity":
        products.sort(key=lambda item: (item.get("name", "").casefold(), item["id"]))
        products.sort(key=lambda item: item.get("popularity", 0), reverse=order != "asc")
    else:
        products.sort(key=lambda item: (item.get("name", "").casefold(), item["id"]))

    if "page" not in query and "pageSize" not in query:
        return products
    page_number = int(query.get("page", ["1"])[0])
    page_size = int(query.get("pageSize", ["24"])[0])
    return {
        "items": products[(page_number - 1) * page_size:page_number * page_size],
        "totalCount": len(products),
        "page": page_number,
        "pageSize": page_size,
        "totalPages": math.ceil(len(products) / page_size)
    }


def category_order(categories):
    """Category ids depth first (a top-level category, then everything below it), as lib/catalog-sections.js orders
    the sections. A category whose parent is missing counts as top-level."""
    ids = {category["id"] for category in categories}
    children = {category["id"]: [] for category in categories}
    roots = []
    for category in categories:
        parent = category.get("parentId")
        if parent in ids and parent != category["id"]:
            children[parent].append(category["id"])
        else:
            roots.append(category["id"])
    ordered = []

    def visit(category_id):
        ordered.append(category_id)
        for child in children[category_id]:
            visit(child)

    for root in roots:
        visit(root)
    return ordered, children


def subtree(children, category_id):
    found = [category_id]
    for child in children.get(category_id, []):
        found.extend(subtree(children, child))
    return found


def format_price(value):
    """A price as the page writes it into the URL (JavaScript's String(Number(text))): 1100.0 -> "1100"."""
    number = float(value)
    return str(int(number)) if number.is_integer() else repr(number)


def products_label(count):
    return f'{count} {"product" if count == 1 else "products"}'


def status_text(product_count, section_count):
    return f'{products_label(product_count)} in {section_count} {"section" if section_count == 1 else "sections"}'


def verify(base_url, executable=None, expect_seeded=False):
    from playwright.sync_api import expect, sync_playwright

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        page = browser.new_page(viewport={"width": 1440, "height": 1000})
        errors = []
        dialogs = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.on("dialog", lambda dialog: (dialogs.append(dialog.message), dialog.dismiss()))

        def api(path):
            response = page.request.get(base_url + path)
            assert response.ok, path
            return response.json()

        def loaded():
            expect(page.locator("#catalog")).to_have_attribute("aria-busy", "false")

        def section(category_id):
            return page.locator(f"#category-{category_id}")

        def card_names(category_id):
            return section(category_id).locator(".product-card h3").all_text_contents()

        # No request interception in this section: these responses come from the actual backend/database.
        categories, products = api("/api/categories"), api("/api/products")
        assert isinstance(products, list), "Legacy GET /api/products must remain a JSON array."
        page_size = 24
        first_page = api(f"/api/products?page=1&pageSize={page_size}")
        assert_page_response(first_page, products, 1, page_size)
        assert_page_response(api("/api/products?page=1&pageSize=100"), products, 1, 100)
        if expect_seeded:
            assert len(categories) >= 20 and len(products) >= 55
        by_id = {category["id"]: category for category in categories}
        ordered, children = category_order(categories)
        own = {category_id: [p for p in products if p["categoryId"] == category_id] for category_id in ordered}
        with_products = [category_id for category_id in ordered if own[category_id]]

        # The home page's "Browse tablets" link opens the page filtered to Tablets and its subcategories (SCRUM-165:
        # the page loads every match in pages of 100 and groups them itself).
        page.goto(base_url)
        tablets = next((category for category in categories if category["name"] == "Tablets"), None)
        if tablets:
            link = page.get_by_role("link", name="Browse tablets")
            expect(link).to_have_attribute("href", f'/products?categoryId={tablets["id"]}')
            with page.expect_response(
                lambda response: is_products_api_url(response.url)
                and parse_qs(urlparse(response.url).query).get("pageSize") == ["100"]
                and "categoryId" not in parse_qs(urlparse(response.url).query)
            ) as response:
                link.click()
            assert response.value.ok
            loaded()
            expect(page.locator("h1")).to_have_text("Tablets")
            scope = subtree(children, tablets["id"])
            shown = [category_id for category_id in ordered if category_id in scope and own[category_id]]
            expect(page.locator(".category-section")).to_have_count(len(shown))
            expect(page.locator("#catalog-status")).to_have_text(
                status_text(sum(len(own[category_id]) for category_id in shown), len(shown)))
            print("PASS: the home page's Browse link opens /products filtered to the category and its subcategories")

        # All products: one section per category that has products of its own, in tree order, three cards each first.
        page.goto(base_url + "/products")
        loaded()
        expect(page.locator("h1")).to_have_text("All products")
        expect(page.locator(".catalog-breadcrumb")).to_have_text(re.compile(r"Home\s*/?\s*All products"))
        expect(page.locator("#catalog-status")).to_have_text(status_text(len(products), len(with_products)))
        expect(page.locator(".category-section")).to_have_count(len(with_products))
        assert page.locator(".category-section").evaluate_all("nodes => nodes.map(node => node.id)") == [
            f"category-{category_id}" for category_id in with_products]
        for category_id in with_products:
            expected = own[category_id]
            expect(section(category_id).locator(".product-count")).to_have_text(f" · {products_label(len(expected))}")
            assert card_names(category_id) == [p["name"] for p in expected[:3]], by_id[category_id]["name"]
            toggle = section(category_id).locator(".category-section-toggle")
            if len(expected) > 3:
                expect(toggle).to_have_text(f"View all {len(expected)}")
            else:
                expect(toggle).to_have_count(0)
        for category_id in ordered:
            row = page.locator(".catalog-sidebar").get_by_role("link", name=re.compile("^" + re.escape(by_id[category_id]["name"]) + r"\s*\d+$"))
            if by_id[category_id].get("parentId") in by_id:
                expect(row).to_have_count(0)  # Subcategories show only under the chosen category.
            else:
                count = sum(len(own[c]) for c in subtree(children, category_id))
                expect(row.locator(".catalog-category-count")).to_have_text(str(count))
        for product in products:
            if product["stockQuantity"] == 0 and product in own[product["categoryId"]][:3]:
                card = page.locator(".product-card").filter(has=page.get_by_role("heading", name=product["name"], exact=True))
                expect(card.locator(".stock")).to_have_text("Out of stock")
                expect(card.get_by_role("button", name="Out of stock", exact=True)).to_be_disabled()
        print(f"PASS: {len(products)} products in {len(with_products)} sections, tree order, 3 cards first, sidebar counts, out-of-stock cards")

        # View all and per-section sorting against the API's own orders.
        big = next((category_id for category_id in with_products if len(own[category_id]) > 3), None)
        if big:
            expected = own[big]
            toggle = section(big).locator(".category-section-toggle")
            toggle.click()
            expect(toggle).to_have_text("Show less")
            expect(toggle).to_have_attribute("aria-expanded", "true")
            expect(section(big).locator(".product-card")).to_have_count(len(expected))
            other = next(category_id for category_id in with_products if category_id != big)
            for value, sort, order in [("price-asc", "price", "asc"), ("price-desc", "price", "desc"),
                                       ("popularity", "popularity", ""), ("name", "", "")]:
                section(big).get_by_label(f'Sort {by_id[big]["name"]}').select_option(value)
                query = f"categoryId={big}" + (f"&sort={sort}" if sort else "") + (f"&order={order}" if order else "")
                api_order = [p["name"] for p in api(f"/api/products?{query}") if p["categoryId"] == big]
                expect(section(big).locator(".product-card h3")).to_have_text(api_order)
                # Sorting one section leaves every other section in name order.
                assert card_names(other) == [p["name"] for p in own[other][:3]]
            toggle.click()
            expect(toggle).to_have_text(f"View all {len(expected)}")
            expect(section(big).locator(".product-card")).to_have_count(3)
            print("PASS: View all / Show less, and each section sorts on its own in the API's name, price and popularity order")

        # Choosing a category in the sidebar: that category and its subcategories only, heading, breadcrumb and chip.
        parent = next((category_id for category_id in with_products
                       if children[category_id] and any(own[c] for c in children[category_id])), None)
        if parent:
            name = by_id[parent]["name"]
            page.locator(".catalog-sidebar").get_by_role("link", name=re.compile("^" + re.escape(name) + r"\s*\d+$")).click()
            loaded()
            expect(page).to_have_url(re.compile(r"[?&]categoryId=" + str(parent) + r"(&|$)"))
            expect(page.locator("h1")).to_have_text(name)
            expect(page.locator(".catalog-breadcrumb")).to_contain_text(name)
            expect(page.locator(".catalog-breadcrumb").get_by_role("link", name="All products")).to_have_attribute("href", "/products")
            scope = [category_id for category_id in subtree(children, parent) if own[category_id]]
            expect(page.locator(".category-section")).to_have_count(len(scope))
            child = next(c for c in children[parent] if own[c])
            child_link = page.locator(".catalog-sidebar").get_by_role("link", name=re.compile("^" + re.escape(by_id[child]["name"]) + r"\s*\d+$"))
            expect(child_link).to_have_class(re.compile("is-sub"))
            expect(page.get_by_role("button", name=f"Remove filter: {name}")).to_be_visible()
            page.reload()
            loaded()
            expect(page.locator(".category-section")).to_have_count(len(scope))
            child_link.click()
            loaded()
            expect(page.locator("h1")).to_have_text(by_id[child]["name"])
            expect(page.locator(".category-section")).to_have_count(1)
            expect(page.locator(".catalog-breadcrumb").get_by_role("link", name=name)).to_have_attribute("href", f"/products?categoryId={parent}")
            page.get_by_role("button", name=f'Remove filter: {by_id[child]["name"]}').click()
            loaded()
            expect(page.locator("h1")).to_have_text("All products")
            print("PASS: a sidebar category shows itself and its subcategories, with heading, breadcrumb, chip and refresh")

            # The header's Categories panel filters the same page (it used to scroll to #category-<id>).
            page.locator("#category-button").click()
            page.locator(".category-panel-item", has_text=name).click()
            loaded()
            expect(page).to_have_url(re.compile(r"/products\?categoryId=" + str(parent) + "$"))
            expect(page.locator("h1")).to_have_text(name)
            page.locator("#category-button").click()
            expect(page.locator(".category-panel-links").get_by_role("link", name=f"All {name}")).to_have_attribute(
                "href", f"/products?categoryId={parent}")
            page.keyboard.press("Escape")
            print("PASS: the header's Categories panel links filter /products with ?categoryId=")

            # An old #category-<id> link chooses the category; Clear filters keeps unrelated query values.
            page.goto(base_url + f"/products?verification=legacy#category-{parent}")
            loaded()
            expect(page.locator("h1")).to_have_text(name)
            assert urlparse(page.url).fragment == ""
            assert parse_qs(urlparse(page.url).query).get("categoryId") == [str(parent)]
            page.get_by_role("button", name="Clear filters", exact=True).click()
            loaded()
            expect(page.locator("#catalog-status")).to_have_text(status_text(len(products), len(with_products)))
            assert parse_qs(urlparse(page.url).query) == {"verification": ["legacy"]}
            print("PASS: old #category-<id> links choose the category; Clear filters keeps other query values")

        # In stock only, price (Enter applies) and brand, each a chip; all checked against the real API.
        page.goto(base_url + "/products")
        loaded()
        page.get_by_label("In stock only").check()
        in_stock = [p for p in products if p["stockQuantity"] > 0]
        sections_in_stock = [c for c in ordered if any(p["categoryId"] == c for p in in_stock)]
        expect(page.locator("#catalog-status")).to_have_text(status_text(len(in_stock), len(sections_in_stock)))
        expect(page.get_by_role("button", name="Remove filter: In stock only")).to_be_visible()
        assert parse_qs(urlparse(page.url).query).get("inStock") == ["1"]
        page.get_by_role("button", name="Remove filter: In stock only").click()
        expect(page.get_by_label("In stock only")).not_to_be_checked()
        prices = sorted(p["price"] for p in products)
        low, high = (format_price(prices[len(prices) // 4]), format_price(prices[len(prices) * 3 // 4]))
        page.get_by_label("Min", exact=True).fill(str(low))
        page.get_by_label("Max", exact=True).fill(str(high))
        page.get_by_label("Max", exact=True).press("Enter")
        loaded()
        ranged = api(f"/api/products?minPrice={low}&maxPrice={high}")
        expect(page.locator("#catalog-status")).to_have_text(status_text(len(ranged), len({p["categoryId"] for p in ranged})))
        expect(page.locator(".catalog-chip").filter(has_text="–")).to_have_count(1)
        page.get_by_label("Min", exact=True).fill(format_price(float(high) + 1))
        page.get_by_role("button", name="Apply", exact=True).click()
        expect(page.locator("#catalog-price-error")).to_have_text("The minimum price cannot be more than the maximum.")
        assert parse_qs(urlparse(page.url).query).get("minPrice") == [low]
        brands = api("/api/brands")
        if brands:
            page.get_by_role("button", name="Clear filters", exact=True).click()
            loaded()
            page.get_by_label("Brand", exact=True).select_option(str(brands[0]["id"]))
            loaded()
            branded = api(f'/api/products?brandId={brands[0]["id"]}')
            expect(page.locator("#catalog-status")).to_have_text(status_text(len(branded), len({p["categoryId"] for p in branded})))
            expect(page.get_by_role("button", name=f'Remove filter: {brands[0]["name"]}')).to_be_visible()
        print("PASS: In stock only, the price range (Enter and Apply, refused when min > max) and brand, each with a chip")

        page.set_viewport_size({"width": 375, "height": 812})
        page.goto(base_url + "/products")
        loaded()
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.set_viewport_size({"width": 1440, "height": 1000})

        # Search through the header (results change on submit), including an out-of-stock product.
        stocked = next(p for p in products if p["stockQuantity"] > 0)
        detail = api(f'/api/products/{stocked["id"]}')
        for term in [stocked["name"], detail["description"].split()[0]]:
            page.goto(base_url + "/products")
            loaded()
            page.locator("#site-search").fill(term)
            page.locator("#site-search").press("Enter")
            loaded()
            expect(page.locator(".product-card").filter(has=page.get_by_role("heading", name=stocked["name"], exact=True))).to_have_count(1)
        empty = next((p for p in products if p["stockQuantity"] == 0), None)
        if empty:
            page.goto(base_url + "/products?" + urlencode({"search": empty["name"]}))
            loaded()
            card = page.locator(".product-card").filter(has=page.get_by_role("heading", name=empty["name"], exact=True))
            expect(card).to_have_count(1)
            expect(card.get_by_role("button", name="Out of stock", exact=True)).to_be_disabled()
            page.goto(base_url + f'/product?id={empty["id"]}')
            expect(page.locator(".stock")).to_have_text("Out of stock")
            expect(page.get_by_role("button", name="Out of stock", exact=True)).to_be_disabled()
        print("PASS: real name/description search through the header, and out-of-stock products found but not addable")

        fixture_categories = [{"id": 41, "name": "Phones", "description": None}, {"id": 99, "name": "Tablets", "description": None}]
        fixture_products = [
            {"id": 7, "categoryId": 41, "name": "<img src=x onerror=alert(1)>", "description": "<script>alert(1)</script>",
             "price": 43000.50, "stockQuantity": 0, "inStock": False, "imageUrl": "javascript:alert(1)"},
            {"id": 8, "categoryId": 41, "name": "Invalid image URL", "description": None,
             "price": 1.25, "stockQuantity": 1, "inStock": True, "imageUrl": "https://["},
            {"id": 9, "categoryId": 41, "name": "Missing image", "description": None,
             "price": 5, "stockQuantity": 2, "inStock": True, "imageUrl": "/missing-image.jpg"},
        ]

        def mock(categories_body, products_body, category_status=200):
            page.unroute_all(behavior="wait")
            page.route("**/api/categories", lambda route: route.fulfill(status=category_status, json=categories_body))
            page.route("**/api/brands", lambda route: route.fulfill(json=[]))
            page.route(PRODUCTS_API, lambda route: route.fulfill(json=mock_products_response(products_body, route.request.url)))

        mock(fixture_categories, fixture_products)
        page.goto(base_url + "/products?verification=render")
        expect(page.locator("#catalog-status")).to_have_text("3 products in 1 section")
        expect(page.locator("#category-99")).to_have_count(0)
        expect(page.locator(".product-card h3").first).to_have_text(fixture_products[0]["name"])
        expect(page.locator(".price").first).to_contain_text("43.000,50")
        expect(page.locator(".image-placeholder")).to_have_count(3)
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        page.locator(".catalog-sidebar").get_by_role("link", name=re.compile(r"^Tablets\s*0$")).click()
        expect(page.locator("#catalog-status")).to_have_text("No products available in this category yet.")
        page.get_by_role("button", name="Clear filters", exact=True).click()
        expect(page.locator(".product-card")).to_have_count(3)
        page.get_by_label("In stock only").check()
        page.get_by_label("Min", exact=True).fill("100")
        page.get_by_label("Min", exact=True).press("Enter")
        expect(page.locator("#catalog-status")).to_have_text("No products match these filters.")
        expect(page.locator(".empty-hint")).to_be_visible()
        page.get_by_role("button", name="Clear filters", exact=True).click()
        expect(page.locator(".product-card")).to_have_count(3)
        print("PASS: real category IDs, empty category, no-match filters, TRY prices, safe text and image fallbacks")

        mock([], [])
        page.reload()
        expect(page.locator("#catalog-status")).to_contain_text("No products available yet.")
        expect(page.locator(".catalog-category-link")).to_have_count(1)  # Only "All products".
        print("PASS: empty catalog")

        mock({"title": "Unavailable"}, fixture_products, category_status=503)
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        expect(page.locator("#catalog .category-section")).to_have_count(0)
        mock(fixture_categories, fixture_products)
        page.get_by_role("button", name="Try again").click()
        expect(page.locator(".product-card")).to_have_count(3)
        expect(page.get_by_role("button", name="Try again")).to_be_hidden()
        print("PASS: partial API failure clears results; retry recovers")

        mock(fixture_categories, {"unexpected": "object"})
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        print("PASS: malformed API response")

        mock(fixture_categories, fixture_products)
        page.unroute(PRODUCTS_API)
        page.route(PRODUCTS_API, lambda route: route.abort())
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        loaded()
        print("PASS: network failure")

        page.unroute_all(behavior="wait")
        pending = []
        page.route("**/api/categories", lambda route: pending.append((route, fixture_categories)))
        page.route("**/api/brands", lambda route: route.fulfill(json=[]))
        page.route(PRODUCTS_API, lambda route: pending.append(
            (route, mock_products_response(fixture_products, route.request.url))
        ))
        page.goto(base_url + "/products", wait_until="domcontentloaded")
        expect(page.locator("#catalog-status")).to_have_text("Loading products…")
        expect(page.locator("#catalog")).to_have_attribute("aria-busy", "true")
        # Route callbacks run while the Playwright event loop is pumped by locator checks.
        expect(page.locator("#retry")).to_be_hidden()
        assert len(pending) == 2
        for route, body in pending:
            route.fulfill(json=body)
        expect(page.locator(".product-card")).to_have_count(3)
        print("PASS: loading state transitions to populated catalog")
        assert not errors and not dialogs, (errors, dialogs)
        browser.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:5200")
    parser.add_argument("--browser-executable")
    parser.add_argument("--expect-seeded", action="store_true")
    args = parser.parse_args()
    verify(args.url.rstrip("/"), args.browser_executable, args.expect_seeded)
