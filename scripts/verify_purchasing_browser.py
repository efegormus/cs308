#!/usr/bin/env python3
"""Exercise the hosted React purchasing flow against a disposable local development database."""
import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import time
from progress_demo_checks import invoice_proof, review_proof
from urllib.parse import urlparse, urlencode
import uuid

from playwright.sync_api import expect, sync_playwright


def verify(url, assembly, executable=None, report=None, smtp_url=None):
    if urlparse(url).hostname not in ("localhost", "127.0.0.1"):
        raise ValueError("Purchasing verification is restricted to a disposable local server.")
    url = url.rstrip("/")
    assembly = Path(assembly).resolve()
    if not assembly.is_file():
        raise ValueError("Supply the published OnlineStore.dll for the trusted role-assignment fixture.")
    suffix = uuid.uuid4().hex
    password = secrets.token_urlsafe(24)
    fixture = {"users": [], "orders": []}
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        customer = browser.new_context(viewport={"width": 375, "height": 812})
        manager = browser.new_context(viewport={"width": 1440, "height": 1000})

        def post(context, path, body):
            for attempt in range(2):
                token = context.request.get(url + "/api/auth/csrf").json()["token"]
                response = context.request.post(url + path, data=body, headers={"X-CSRF-TOKEN": token})
                if response.status != 429 or attempt:
                    return response
                time.sleep(61)


        try:
            for context, prefix in [(customer, "buyer"), (manager, "manager")]:
                response = post(context, "/api/auth/register", {
                    "name": f"{prefix} Browser", "email": f"{prefix}-{suffix}@example.com",
                    "taxId": "BROWSER-TAX", "homeAddress": "<img src=x onerror=alert(1)> Original browser address", "password": password
                })
                assert response.status == 201, "Fixture registration failed."
                fixture["users"].append(response.json()["id"])
            # No role-setting HTTP route exists. Use the existing trusted operator command, as documented.
            subprocess.run(["dotnet", str(assembly), "--set-role", f"manager-{suffix}@example.com", "ProductManager"],
                           cwd=assembly.parent, env=os.environ.copy(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
            products = customer.request.get(url + "/api/products").json()
            product = next((item for item in products if item["stockQuantity"] >= 3 and item.get("listPrice", item["price"]) > item["price"]),
                           next(item for item in products if item["stockQuantity"] >= 3))
            second = next(item for item in products if item["stockQuantity"] >= 3 and item["id"] != product["id"])
            fixture.update(productId=product["id"], originalStock=product["stockQuantity"])
            assert post(customer, "/api/cart/items", {"productId": product["id"], "quantity": 1}).status == 200
            assert post(customer, "/api/cart/items", {"productId": second["id"], "quantity": 1}).status == 200
            page = customer.new_page()
            errors, dialogs, checkout_posts = [], [], []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("dialog", lambda dialog: (dialogs.append(dialog.message), dialog.dismiss()))
            page.on("request", lambda request: checkout_posts.append(1)
                    if request.method == "POST" and request.url.split("?")[0].endswith("/api/customer/checkout") else None)
            # Compare the same server-owned price across all published shopping surfaces.
            money = lambda value: page.evaluate("value => new Intl.NumberFormat('tr-TR', {style:'currency', currency:'TRY'}).format(value)", value)
            page.goto(url + "/products?" + urlencode({"search": product["name"]}))
            tile = page.locator(".product-card").filter(has=page.get_by_role("heading", name=product["name"], exact=True))
            expect(tile.locator(".price-current")).to_have_text(money(product["price"]))
            if product.get("listPrice", product["price"]) > product["price"]:
                expect(tile.locator(".price-original")).to_have_text(money(product["listPrice"]))
            page.goto(url + f'/product?id={product["id"]}')
            expect(page.locator(".product-detail .price-current")).to_have_text(money(product["price"]))
            page.goto(url + "/cart")
            cart_row = page.locator(".cart-line").filter(has=page.get_by_role("link", name=product["name"], exact=True))
            expect(cart_row.locator(".price-current")).to_have_text(money(product["price"]))
            page.goto(url + "/checkout")
            expect(page).to_have_url(re.compile(r"/login\?returnUrl="))
            expect(page.locator("#login-form")).to_be_visible()
            page.locator("#email").fill(f"buyer-{suffix}@example.com")
            page.locator("#password").fill(password)
            page.locator("#submit").click()
            expect(page).to_have_url(url + "/checkout")
            expect(page.locator("#checkout-form")).to_be_visible()
            expect(page.locator("#lines")).to_contain_text(product["name"])
            cart = customer.request.get(url + "/api/cart").json()
            quote = customer.request.get(url + "/api/customer/checkout").json()
            assert next(line for line in cart["items"] if line["productId"] == product["id"])["unitPrice"] == product["price"]
            assert next(line for line in quote["items"] if line["productId"] == product["id"])["unitPrice"] == product["price"]
            assert cart["total"] == quote["total"]
            expect(page.locator("#total")).to_have_text(money(quote["total"]))
            print("PASS: published catalog/detail/cart/checkout agree on the effective unit price and total")
            expect(page.locator("#address")).to_contain_text("<img src=x onerror=alert(1)>")
            assert page.locator("#address img").count() == 0
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth")

            def fill(number):
                page.locator("#number").fill(number)
                page.locator("#expiry-month").fill("12")
                page.locator("#expiry-year").fill(str(datetime.now().year + 1))
                page.locator("#security-code").fill("123")

            assert post(customer, f'/api/customer/products/{product["id"]}/review', {"rating": 5}).status == 403
            fill("4000000000000002")
            page.locator("#purchase").click()
            expect(page.get_by_role("alert")).to_contain_text("declined")
            expect(page.locator("#number")).to_have_value("")
            fill("4242424242424242")
            # Two immediate submits must still produce just one confirmed checkout request.
            page.locator("#checkout-form").evaluate("form => { form.requestSubmit(); form.requestSubmit(); }")
            expect(page).to_have_url(re.compile(r"/order-confirmation\?orderId="))
            expect(page.locator("#receipt")).to_be_visible()
            expect(page.locator("#order-status")).to_contain_text("Processing · Payment: Confirmed")
            order = int(page.url.split("orderId=")[1])
            fixture["orders"].append(order)
            assert len(checkout_posts) == 2, "A double submit duplicated the purchase POST."
            expect(page.locator("#invoice")).to_be_visible()
            pdf = customer.request.get(url + page.locator("#invoice").get_attribute("href"))
            assert pdf.status == 200 and pdf.headers["content-type"] == "application/pdf"
            assert pdf.body().startswith(b"%PDF")
            invoice_proof(page, customer, manager, browser, url, order, product, smtp_url, f"buyer-{suffix}@example.com")
            page.reload()
            expect(page.locator("#receipt")).to_be_visible()
            assert page.evaluate("document.documentElement.scrollWidth <= innerWidth")
            print("PASS: hosted React guest cart → login → automatic merge → decline/success → protected PDF; double-submit protection, mobile layout and deep-link refresh")

            assert post(manager, "/api/auth/login", {"email": f"manager-{suffix}@example.com", "password": password}).status == 200
            staff = manager.new_page()
            staff.on("pageerror", lambda error: errors.append(str(error)))
            staff.goto(url + "/deliveries")
            card = staff.locator(".purchase-card").filter(has=staff.get_by_role("heading", name=re.compile(f"Order #{order}$")))
            expect(card).to_be_visible()
            staff.route("**/api/product-management/deliveries/*/status", lambda route: route.fulfill(status=409, json={"title": "stale version"}))
            card.get_by_role("button", name="Mark in transit").click()
            expect(staff.get_by_role("alert")).to_contain_text("conflicts")
            expect(card.get_by_role("button", name="Mark in transit")).to_be_disabled()
            staff.unroute("**/api/product-management/deliveries/*/status")
            staff.locator("#retry").click()
            expect(card.get_by_role("button", name="Mark in transit")).to_be_enabled()
            card.get_by_role("button", name="Mark in transit").click()
            expect(card).to_contain_text("InTransit")
            page.locator("#retry").click()
            expect(page.locator("#order-status")).to_contain_text("InTransit")
            card.get_by_role("button", name="Mark delivered").click()
            expect(card).to_contain_text("Delivered")
            expect(card.locator("button")).to_have_count(0)
            page.locator("#retry").click()
            expect(page.locator("#order-status")).to_contain_text("Delivered")
            print("PASS: React ProductManager delivery actions update the customer's receipt and remove terminal actions")

            review_proof(page, staff, customer, manager, browser, url, order, [product, second], suffix, post)
            assert post(customer, "/api/cart/items", {"productId": product["id"], "quantity": 1}).status == 200
            page.goto(url + "/checkout")
            expect(page.locator("#checkout-form")).to_be_visible()

            def lose_response(route):
                if route.request.method == "POST":
                    result = route.fetch()
                    assert result.status == 201
                    fixture["orders"].append(result.json()["orderId"])
                    route.abort("failed")
                else:
                    route.continue_()

            page.route("**/api/customer/checkout", lose_response)
            fill("4242424242424242")
            page.locator("#purchase").click()
            expect(page.get_by_role("button", name="Check purchase status")).to_be_visible()
            saved = page.evaluate("Object.fromEntries(Object.entries(sessionStorage))")
            assert "4242424242424242" not in json.dumps(saved)
            assert "securityCode" not in json.dumps(saved)
            page.unroute("**/api/customer/checkout", lose_response)
            page.reload()
            expect(page).to_have_url(re.compile(r"/order-confirmation\?orderId="))
            expect(page.locator("#receipt")).to_be_visible()
            assert int(page.url.split("orderId=")[1]) == fixture["orders"][-1]
            assert len(checkout_posts) == 3
            print("PASS: a lost committed response recovers on reload with no duplicate POST and no persisted card input")
            page.goto(url + "/deliveries")
            expect(page.get_by_role("alert")).to_contain_text("ProductManager account is required")
            page.goto(url + "/order-confirmation?orderId=invalid")
            expect(page.get_by_role("alert")).to_contain_text("valid order ID")
            # SCRUM-148: the route accepts every positive safe integer (orders use long IDs). The API answers are controlled
            # responses for an ID above the signed 32-bit range, so no shared data changes; the browser only has to open it.
            large_id = 2147483648
            receipt = {
                "orderId": large_id, "cartId": None, "currency": "TRY", "status": "Processing", "paymentStatus": "Confirmed",
                "purchasedAt": "2026-11-20T10:00:00Z", "deliveredAt": None, "name": "Buyer", "taxId": "Tax",
                "address": "Address", "invoiceStatus": "Pending",
                "items": [{"productId": 1, "name": "Phone", "quantity": 1, "unitPrice": 90, "total": 90}], "total": 90,
            }
            large_requests = []

            def large_order(route):
                large_requests.append(route.request.url)
                route.fulfill(status=200, content_type="application/json", body=json.dumps(receipt))

            page.route(f"**/api/customer/orders/{large_id}", large_order)
            page.goto(url + f"/order-confirmation?orderId={large_id}")
            expect(page.locator("#order-heading")).to_have_text(f"Order #{large_id}")
            expect(page.locator("#receipt")).to_be_visible()
            assert large_requests, "the large order ID must reach the owner-only API"
            page.unroute(f"**/api/customer/orders/{large_id}", large_order)
            for rejected in ("0", "-1", "1.5", "9007199254740992", "1e3"):
                page.goto(url + f"/order-confirmation?orderId={rejected}")
                expect(page.get_by_role("alert")).to_contain_text("valid order ID")
            print("PASS: order ID 2147483648 opens its receipt and zero, negative, fractional and unsafe IDs stay rejected")
            assert not errors, errors
            assert not dialogs, dialogs
            print("PASS: role isolation, invalid order IDs and literal snapshot rendering without browser exceptions")
        finally:
            if report:
                Path(report).write_text(json.dumps(fixture))
            browser.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:5200")
    parser.add_argument("--operator-assembly", required=True)
    parser.add_argument("--browser-executable")
    parser.add_argument("--report", help="optional local fixture IDs for cleanup; contains no credentials")
    parser.add_argument("--smtp-api-url", help="optional disposable Mailpit API for exact PDF attachment evidence")
    args = parser.parse_args()
    verify(args.url, args.operator_assembly, args.browser_executable, args.report, args.smtp_api_url)


if __name__ == "__main__":
    main()
