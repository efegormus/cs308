#!/usr/bin/env python3
"""Focused SCRUM-54 verification: real API/MySQL first, then controlled UI failure cases."""
import argparse
from playwright.sync_api import expect, sync_playwright


def verify(base_url, executable=None, expect_seeded=False):
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        page = browser.new_page(viewport={"width": 1440, "height": 1000})
        errors = []
        dialogs = []
        page.on("pageerror", lambda error: errors.append(str(error)))
        page.on("dialog", lambda dialog: (dialogs.append(dialog.message), dialog.dismiss()))

        # No request interception in this section: these responses come from the actual backend/database.
        categories_response = page.request.get(base_url + "/api/categories")
        products_response = page.request.get(base_url + "/api/products")
        assert categories_response.ok and products_response.ok
        categories, products = categories_response.json(), products_response.json()
        if expect_seeded:
            assert len(categories) >= 20 and len(products) >= 55
        page.goto(base_url)
        if categories:
            tablets = next((category for category in categories if category["name"] == "Tablets"), categories[0])
            if tablets["name"] == "Tablets":
                expect(page.get_by_role("link", name="Browse tablets")).to_have_attribute("href", f'/products#category-{tablets["id"]}')
                with page.expect_response("**/api/products") as response:
                    page.get_by_role("link", name="Browse tablets").click()
                assert response.value.ok
            else:
                page.goto(base_url + "/products")
        else:
            page.goto(base_url + "/products")
        expect(page.locator("#catalog")).to_have_attribute("aria-busy", "false")
        expect(page.locator(".product-card")).to_have_count(len(products))
        for category in categories:
            section = page.locator(f'#category-{category["id"]}')
            expected = [product for product in products if product["categoryId"] == category["id"]]
            expect(section.locator(".product-card")).to_have_count(len(expected))
            assert section.locator(".product-card h3").all_text_contents() == [product["name"] for product in expected]
        for product in products:
            if product["stockQuantity"] == 0:
                card = page.locator(".product-card").filter(has=page.get_by_role("heading", name=product["name"], exact=True))
                expect(card.locator(".stock")).to_have_text("Out of stock")
        page.set_viewport_size({"width": 375, "height": 812})
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        print(f"PASS: real browser → API → MySQL; {len(products)} products in {len(categories)} categories; stock, navigation, mobile layout")

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
            page.route("**/api/products", lambda route: route.fulfill(json=products_body))

        mock(fixture_categories, fixture_products)
        page.goto(base_url + "/products?verification=render#category-99")
        expect(page.locator("#catalog-status")).to_have_text("3 products in 2 categories.")
        expect(page.locator("#category-99")).to_contain_text("No products available in this category yet.")
        expect(page.locator(".product-card h3").first).to_have_text(fixture_products[0]["name"])
        expect(page.locator(".price").first).to_contain_text("43.000,50")
        expect(page.locator(".image-placeholder")).to_have_count(3)
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        print("PASS: real category IDs, empty category, TRY prices, safe text and invalid/missing image fallbacks")

        mock([], [])
        page.reload()
        expect(page.locator("#catalog-status")).to_contain_text("No products available yet.")
        expect(page.locator("#category-nav")).to_be_hidden()
        print("PASS: empty catalog")

        mock({"title": "Unavailable"}, fixture_products, category_status=503)
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        expect(page.locator("#catalog")).to_be_empty()
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
        page.unroute("**/api/products")
        page.route("**/api/products", lambda route: route.abort())
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        expect(page.locator("#catalog")).to_have_attribute("aria-busy", "false")
        print("PASS: network failure")

        page.unroute_all(behavior="wait")
        pending = []
        page.route("**/api/categories", lambda route: pending.append((route, fixture_categories)))
        page.route("**/api/products", lambda route: pending.append((route, fixture_products)))
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
