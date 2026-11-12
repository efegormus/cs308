#!/usr/bin/env python3
"""Focused SCRUM-54 verification: real API/MySQL first, then controlled UI failure cases."""
import argparse
import math
import re
from urllib.parse import parse_qs, urlparse

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


def verify(base_url, executable=None, expect_seeded=False):
    from playwright.sync_api import expect, sync_playwright

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
        assert isinstance(products, list), "Legacy GET /api/products must remain a JSON array."
        page_size = 24
        paged_response = page.request.get(base_url + f"/api/products?page=1&pageSize={page_size}")
        assert paged_response.ok
        first_page = paged_response.json()
        assert_page_response(first_page, products, 1, page_size)
        if expect_seeded:
            assert len(categories) >= 20 and len(products) >= 55
        page.goto(base_url)
        if categories:
            tablets = next((category for category in categories if category["name"] == "Tablets"), categories[0])
            if tablets["name"] == "Tablets":
                expect(page.get_by_role("link", name="Browse tablets")).to_have_attribute("href", f'/products#category-{tablets["id"]}')
                with page.expect_response(
                    lambda response: is_products_api_url(response.url)
                    and parse_qs(urlparse(response.url).query).get("page") == ["1"]
                    and parse_qs(urlparse(response.url).query).get("pageSize") == [str(page_size)]
                ) as response:
                    page.get_by_role("link", name="Browse tablets").click()
                assert response.value.ok
            else:
                page.goto(base_url + "/products")
        else:
            page.goto(base_url + "/products")
        expect(page.locator("#catalog")).to_have_attribute("aria-busy", "false")
        count_label = "product" if first_page["totalCount"] == 1 else "products"
        expect(page.locator("#catalog-status")).to_have_text(
            f'{first_page["totalCount"]} {count_label} found.'
        )
        for page_number in range(1, first_page["totalPages"] + 1):
            current_page = first_page
            if page_number > 1:
                with page.expect_response(
                    lambda response, number=page_number: is_products_api_url(response.url)
                    and parse_qs(urlparse(response.url).query).get("page") == [str(number)]
                ) as response:
                    page.get_by_role("button", name="Next").click()
                assert response.value.ok
                current_page = response.value.json()
                assert_page_response(current_page, products, page_number, page_size)
            expect(page.locator(".product-card")).to_have_count(len(current_page["items"]))
            count_label = "product" if current_page["totalCount"] == 1 else "products"
            expect(page.locator("#catalog-status")).to_have_text(
                f'{current_page["totalCount"]} {count_label} found.'
            )
            expect(page.get_by_role("navigation", name="Product pages")).to_contain_text(
                f'Page {page_number} of {current_page["totalPages"]}'
            )
            visible_links = {
                link.get_attribute("href")
                for link in page.locator(".product-card h3 a").all()
            }
            assert visible_links == {f'/product?id={item["id"]}' for item in current_page["items"]}
            for category in categories:
                section = page.locator(f'#category-{category["id"]}')
                expected = [product for product in current_page["items"] if product["categoryId"] == category["id"]]
                expect(section.locator(".product-card")).to_have_count(len(expected))
                assert section.locator(".product-card h3").all_text_contents() == [product["name"] for product in expected]
            for product in current_page["items"]:
                if product["stockQuantity"] == 0:
                    card = page.locator(".product-card").filter(
                        has=page.get_by_role("heading", name=product["name"], exact=True)
                    )
                    expect(card.locator(".stock")).to_have_text("Out of stock")
            if page_number < current_page["totalPages"]:
                expect(page.get_by_role("button", name="Next")).to_be_enabled()
            else:
                expect(page.get_by_role("button", name="Next")).to_be_disabled()
            if page_number > 1:
                expect(page.get_by_role("button", name="Previous")).to_be_enabled()
            else:
                expect(page.get_by_role("button", name="Previous")).to_be_disabled()
        page.set_viewport_size({"width": 375, "height": 812})
        assert page.evaluate("document.documentElement.scrollWidth <= window.innerWidth")
        print(f"PASS: legacy array and paginated API; real browser → MySQL; {len(products)} products in {len(categories)} categories; all pages, stock, navigation, mobile layout")

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
        page.goto(base_url + "/products?verification=render#category-99")
        expect(page.locator("#catalog-status")).to_have_text("3 products found.")
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
        page.unroute(PRODUCTS_API)
        page.route(PRODUCTS_API, lambda route: route.abort())
        page.reload()
        expect(page.get_by_role("button", name="Try again")).to_be_visible()
        expect(page.locator("#catalog")).to_have_attribute("aria-busy", "false")
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
