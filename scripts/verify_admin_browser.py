#!/usr/bin/env python3
"""Check the admin panel (SCRUM-124) at /manage in a real browser against a disposable local server.

It registers a customer, a product manager and a sales manager, gives the managers their roles with the trusted
operator command (no HTTP route sets roles), then checks each role's menu, the sections, the skeleton while loading,
the review approval flow, reduced motion, the mobile menu and that no page scrolls sideways.
"""
import argparse
import json
import os
from pathlib import Path
import re
import secrets
import subprocess
import time
from urllib.parse import urlparse
import uuid

from playwright.sync_api import expect, sync_playwright

PM_MENU = ["Overview", "Deliveries", "Reviews", "Low stock"]
SM_MENU = ["Overview", "Invoices", "Discounts"]
# Two pending reviews for the approval check. The real dashboard is fetched first and only its comment list is
# replaced, because the seed has no comments and SCRUM-81 (approval API) does not exist yet.
REVIEWS = [
    {"id": 900001, "productId": 1, "productName": "Browser check product A", "body": "First review", "createdAt": "2026-11-09T13:00:00Z"},
    {"id": 900002, "productId": 2, "productName": "Browser check product B", "body": "Second review", "createdAt": "2026-11-10T13:00:00Z"},
]


def no_sideways_scroll(page, where):
    assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), f"{where} scrolls sideways"


def menu(page):
    return page.get_by_role("navigation", name="Management sections")


def menu_link(page, label):
    # Scoped to the panel menu: the header has its own Deliveries link for the product manager.
    return menu(page).get_by_role("link", name=label, exact=True)


def menu_labels(page):
    return [text.strip() for text in menu(page).locator(".admin-nav-link").all_inner_texts()]


def verify(url, assembly, executable=None):
    if urlparse(url).hostname not in ("localhost", "127.0.0.1"):
        raise ValueError("Admin verification is restricted to a disposable local server.")
    url = url.rstrip("/")
    assembly = Path(assembly).resolve()
    if not assembly.is_file():
        raise ValueError("Supply the built or published OnlineStore.dll for the trusted role-assignment fixture.")
    suffix = uuid.uuid4().hex
    password = secrets.token_urlsafe(24)
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        contexts = {
            "customer": browser.new_context(viewport={"width": 1280, "height": 900}),
            "pm": browser.new_context(viewport={"width": 1280, "height": 900}),
            "sm": browser.new_context(viewport={"width": 1280, "height": 900}, reduced_motion="reduce"),
        }
        errors = []

        def post(context, path, body):
            # The login and register limit is 10 a minute per address and other checks share it: wait once on 429.
            for attempt in range(2):
                token = context.request.get(url + "/api/auth/csrf").json()["token"]
                response = context.request.post(url + path, data=body, headers={"X-CSRF-TOKEN": token})
                if response.status != 429 or attempt:
                    return response
                time.sleep(61)

        def new_page(context):
            page = context.new_page()
            page.on("pageerror", lambda error: errors.append(str(error)))
            return page

        try:
            for name, context in contexts.items():
                response = post(context, "/api/auth/register", {
                    "name": f"Admin check {name}", "email": f"admin-{name}-{suffix}@example.com",
                    "taxId": "BROWSER-TAX", "homeAddress": "Browser check address", "password": password})
                assert response.status == 201, f"Fixture registration failed for {name}: {response.status}"
            for name, role in [("pm", "ProductManager"), ("sm", "SalesManager")]:
                subprocess.run(["dotnet", str(assembly), "--set-role", f"admin-{name}-{suffix}@example.com", role],
                               cwd=assembly.parent, env=os.environ.copy(), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=True)
            # Registration does not sign in.
            for name in contexts:
                response = post(contexts[name], "/api/auth/login", {"email": f"admin-{name}-{suffix}@example.com", "password": password})
                assert response.status == 200, f"Login failed for {name}: {response.status}"

            # A customer cannot open the panel or any of its sections.
            page = new_page(contexts["customer"])
            for path in ["/manage", "/manage/reviews", "/manage/invoices"]:
                page.goto(url + path)
                expect(page.get_by_role("alert")).to_contain_text("ProductManager or SalesManager account is required")
                expect(page.locator(".admin-shell")).to_have_count(0)
            print("PASS: a customer gets the role message on /manage and its sections")

            # Product manager: menu, skeleton while the dashboard loads, overview, sections.
            page = new_page(contexts["pm"])
            held = []
            page.route("**/api/product-management/dashboard", lambda route: held.append(route))
            page.goto(url + "/manage")
            expect(page.get_by_role("heading", level=1, name="Overview")).to_be_visible()
            assert menu_labels(page) == PM_MENU, menu_labels(page)
            expect(menu_link(page, "Overview")).to_have_attribute("aria-current", "page")
            skeleton = page.get_by_role("status", name="Loading low-stock products…")
            expect(skeleton).to_be_visible()
            expect(skeleton.locator(".ui-table-skeleton").first).to_be_visible()
            assert held, "The dashboard request was not made."
            held[0].continue_()
            page.unroute("**/api/product-management/dashboard")
            expect(skeleton).to_have_count(0)
            expect(page.locator(".admin-kpi")).to_have_count(3)
            assert page.locator(".admin-panels section").count() == 3
            no_sideways_scroll(page, "PM overview")
            print("PASS: product manager menu, aria-current, skeleton rows while loading, KPI cards and lists")

            for label, path, heading in [("Deliveries", "/manage/deliveries", "Deliveries"), ("Low stock", "/manage/low-stock", "Low stock")]:
                menu_link(page, label).click()
                expect(page).to_have_url(url + path)
                expect(page.get_by_role("heading", level=1, name=heading)).to_be_visible()
                expect(menu_link(page, label)).to_have_attribute("aria-current", "page")
                expect(menu_link(page, "Overview")).not_to_have_attribute("aria-current", "page")
                expect(page.locator(".ui-table-frame").first).to_be_visible()
                expect(page.locator(".ui-table-frame--loading")).to_have_count(0)
                no_sideways_scroll(page, path)
            page.goto(url + "/manage/invoices")
            expect(page).to_have_url(url + "/manage")
            print("PASS: product manager sections load, the menu marks the current one, and a sales section sends back to the overview")

            # Review approval with two pending reviews.
            def with_reviews(route):
                data = route.fetch().json()
                data["commentsWaitingForApproval"] = REVIEWS
                route.fulfill(status=200, json=data)

            page.route("**/api/product-management/dashboard", with_reviews)
            page.goto(url + "/manage/reviews")
            rows = page.locator(".admin-review")
            expect(rows).to_have_count(2)
            first = rows.first.get_by_role("button", name="Approve review")
            first.click()
            approved = rows.first.get_by_role("button", name="Review approved")
            expect(approved).to_be_visible()
            expect(approved).to_have_attribute("aria-disabled", "true")
            approved.click(force=True)  # a second click must not start another approval
            expect(page.locator(".admin-reviews[aria-live='polite']")).to_contain_text("Review approved")
            expect(rows).to_have_count(1)
            expect(rows.first).to_contain_text("Browser check product B")
            print("PASS: approving a review turns the button into 'Review approved', announces it and removes only that row")

            # Mobile: the menu is a disclosure at the top.
            page.set_viewport_size({"width": 375, "height": 812})
            page.goto(url + "/manage")
            toggle = page.get_by_role("button", name=re.compile("^Menu"))
            expect(toggle).to_have_attribute("aria-expanded", "false")
            expect(menu_link(page, "Low stock")).to_be_hidden()
            box = toggle.bounding_box()
            assert box["height"] >= 44, box
            toggle.click()
            expect(toggle).to_have_attribute("aria-expanded", "true")
            menu_link(page, "Low stock").click()
            expect(page).to_have_url(url + "/manage/low-stock")
            expect(toggle).to_have_attribute("aria-expanded", "false")
            for path in ["/manage", "/manage/deliveries", "/manage/reviews", "/manage/low-stock"]:
                page.goto(url + path)
                expect(page.locator(".ui-table-frame--loading")).to_have_count(0)
                no_sideways_scroll(page, f"375px {path}")
            print("PASS: below 768px the menu is a 44px disclosure that closes after a choice; no sideways scroll at 375px")

            # Sales manager, with reduced motion: menu, KPIs at their final value at once, sections.
            page = new_page(contexts["sm"])
            page.goto(url + "/manage")
            expect(page.get_by_role("heading", level=1, name="Overview")).to_be_visible()
            assert menu_labels(page) == SM_MENU, menu_labels(page)
            kpis = page.locator(".admin-kpi")
            expect(kpis).to_have_count(4)
            for index in range(4):
                shown = kpis.nth(index).locator(".admin-kpi-value [aria-hidden='true']").inner_text()
                final = kpis.nth(index).locator(".admin-kpi-value .admin-sr").inner_text()
                assert shown == final, (shown, final)
            assert "₺" in kpis.first.inner_text()
            assert page.locator(".admin-kpi").first.evaluate("element => getComputedStyle(element).animationName") == "none"
            no_sideways_scroll(page, "SM overview")
            for label, path in [("Invoices", "/manage/invoices"), ("Discounts", "/manage/discounts")]:
                menu_link(page, label).click()
                expect(page).to_have_url(url + path)
                expect(menu_link(page, label)).to_have_attribute("aria-current", "page")
                expect(page.locator(".ui-table-frame--loading")).to_have_count(0)
                no_sideways_scroll(page, path)
            page.goto(url + "/manage/reviews")
            expect(page).to_have_url(url + "/manage")
            print("PASS: sales manager menu, final KPI values at once with reduced motion, invoices and discounts")

            assert not errors, errors
            print("PASS: no browser exceptions")
        finally:
            browser.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:5200")
    parser.add_argument("--operator-assembly", required=True)
    parser.add_argument("--browser-executable")
    args = parser.parse_args()
    verify(args.url, args.operator_assembly, args.browser_executable)


if __name__ == "__main__":
    main()
