#!/usr/bin/env python3
"""Check the admin panel (SCRUM-124) at /manage in a real browser against a disposable local server.

It registers a customer, a product manager and a sales manager, gives the managers their roles with the trusted
operator command (no HTTP route sets roles), then checks each role's menu, the sections, the skeleton while loading,
the review moderation flow (approve, reject, 409, empty queue), who may call the moderation API,
reduced motion, the mobile menu and that no page scrolls sideways.
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
SM_MENU = ["Overview", "Invoices", "Pricing", "Discounts"]
# Controlled moderation UI cases (SCRUM-141) use a small route-mocked queue, 3 to a page.
# verify_purchasing_browser.py separately exercises real customer submissions and decisions against MySQL.
def moderation_comment(id, product, author, rating, body):
    return {"id": id, "productId": id - 900000, "productName": product, "authorName": author, "rating": rating,
            "body": body, "status": "Pending", "createdAt": f"2026-11-{id - 899990:02d}T13:00:00Z",
            "moderatedAt": None, "moderationNote": None}


REVIEWS = [
    moderation_comment(900001, "Browser check product A", "Browser Ayşe", 5, "First review"),
    moderation_comment(900002, "Browser check product B", "Browser Can", None, "Second review"),
    moderation_comment(900003, "Browser check product C", "Browser Deniz", 2, "Third review"),
    moderation_comment(900004, "Browser check product D", "Browser Ece", 4, "<b>not bold</b> <img src=x onerror=alert(1)>"),
]
MODERATION_PAGE = 3
LIST_ROUTE = re.compile(r"/api/product-management/comments\?")
DECISION_ROUTE = re.compile(r"/api/product-management/comments/(\d+)/decision$")


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

            # The moderation API itself (SCRUM-81): guests get 401, other roles 403, the product manager 200.
            comments = url + "/api/product-management/comments?status=Pending&afterId=0"
            guest = browser.new_context()
            assert guest.request.get(comments).status == 401, "a guest reached the moderation list"
            guest.close()
            for name in ["customer", "sm"]:
                assert contexts[name].request.get(comments).status == 403, f"{name} reached the moderation list"
                token = contexts[name].request.get(url + "/api/auth/csrf").json()["token"]
                response = contexts[name].request.post(url + "/api/product-management/comments/1/decision",
                                                        data={"decision": "Approve"}, headers={"X-CSRF-TOKEN": token})
                assert response.status == 403, f"{name} could post a decision: {response.status}"
            listed = contexts["pm"].request.get(comments)
            assert listed.status == 200 and set(listed.json()) == {"status", "comments", "nextAfterId"}, listed.status
            print("PASS: the moderation API answers 401 to a guest, 403 to a customer and a sales manager, 200 to a product manager")

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

            # Review moderation (SCRUM-141) against a mocked queue: list pages, approve, reject, 409, empty queue.
            queue = {review["id"]: dict(review) for review in REVIEWS}
            decisions = []

            def list_comments(route):
                query = dict(part.split("=", 1) for part in route.request.url.split("?", 1)[1].split("&"))
                assert query.get("status") == "Pending", query
                after = int(query["afterId"])
                rest = sorted((review for id, review in queue.items() if id > after), key=lambda review: review["id"])
                page_rows = rest[:MODERATION_PAGE]
                more = len(rest) > MODERATION_PAGE
                route.fulfill(status=200, json={"status": "Pending", "comments": page_rows,
                                                "nextAfterId": page_rows[-1]["id"] if more else None})

            def decide_comment(route):
                request = route.request
                id = int(DECISION_ROUTE.search(request.url).group(1))
                decisions.append((id, request.method, request.post_data_json, bool(request.headers.get("x-csrf-token"))))
                if id == 900003:  # another manager decided it first
                    queue.pop(id, None)
                    route.fulfill(status=409, json={"title": "SECRET conflict detail"})
                    return
                review = queue.pop(id)
                status = "Approved" if request.post_data_json["decision"] == "Approve" else "Rejected"
                route.fulfill(status=200, json={**review, "status": status, "moderatedAt": "2026-11-20T10:00:00Z"})

            def dashboard_from_queue(route):
                data = route.fetch().json()
                data["commentsWaitingForApproval"] = [
                    {key: review[key] for key in ("id", "productId", "productName", "body", "createdAt")}
                    for review in sorted(queue.values(), key=lambda review: review["id"])]
                route.fulfill(status=200, json=data)

            page.route(LIST_ROUTE, list_comments)
            page.route(DECISION_ROUTE, decide_comment)
            page.route("**/api/product-management/dashboard", dashboard_from_queue)
            page.goto(url + "/manage/reviews")
            rows = page.locator(".admin-review")
            expect(rows).to_have_count(3)
            expect(rows.nth(0)).to_contain_text("Browser Ayşe")
            expect(rows.nth(0)).to_contain_text("5 out of 5 stars")
            expect(rows.nth(1)).to_contain_text("No rating")
            page.get_by_role("button", name="Load more reviews").click()
            expect(rows).to_have_count(4)
            expect(page.get_by_role("button", name="Load more reviews")).to_have_count(0)
            assert len({rows.nth(index).get_attribute("data-id") for index in range(4)}) == 4, "a review is listed twice"
            expect(rows.nth(3).locator(".admin-review-body")).to_have_text("<b>not bold</b> <img src=x onerror=alert(1)>")
            assert rows.nth(3).locator("b, img").count() == 0, "comment text was rendered as HTML"
            print("PASS: the queue lists product, author, rating or 'No rating', text and date; 'Load more' adds the next page once; text is never HTML")

            live = page.locator(".admin-reviews[aria-live='polite']")
            row = lambda id: page.locator(f".admin-review[data-id='{id}']")
            row(900001).get_by_role("button", name="Approve review").click()
            approved = row(900001).get_by_role("button", name="Review approved")
            expect(approved).to_be_visible()
            expect(approved).to_have_attribute("aria-disabled", "true")
            expect(row(900001).locator(".admin-reject")).to_be_hidden()
            approved.click(force=True)  # a second click must not start another decision,
            row(900001).locator(".admin-reject").dispatch_event("click")  # nor a click on the other button
            expect(live).to_contain_text("Review approved: Browser check product A")
            expect(row(900001)).to_have_count(0)
            expect(rows).to_have_count(3)
            assert decisions == [(900001, "POST", {"decision": "Approve"}, True)], decisions
            print("PASS: Approve sends one POST with X-CSRF-TOKEN, turns into 'Review approved', announces it and removes only that row")

            row(900002).get_by_role("button", name="Reject review").click()
            expect(row(900002).get_by_role("button", name="Review rejected")).to_be_visible()
            expect(row(900002).locator(".admin-accept")).to_be_hidden()
            expect(live).to_contain_text("Review rejected: Browser check product B")
            expect(row(900002)).to_have_count(0)
            expect(rows).to_have_count(2)
            assert decisions[1:] == [(900002, "POST", {"decision": "Reject"}, True)], decisions
            print("PASS: Reject runs the same flow, says 'Review rejected' and removes only that row")

            row(900003).get_by_role("button", name="Approve review").click()
            expect(page.get_by_role("alert").filter(has_text="Another manager already decided this review")).to_be_visible()
            expect(rows).to_have_count(1)
            expect(row(900004)).to_have_count(1)
            assert [decision[0] for decision in decisions].count(900003) == 1, "a 409 was retried"
            assert "SECRET" not in page.content()
            print("PASS: a 409 says another manager decided first, refreshes the list and is not retried; the error body is not shown")

            menu_link(page, "Overview").click()
            expect(page.locator(".admin-kpi").nth(2).locator(".admin-kpi-value .admin-sr")).to_have_text("1")
            reviews_panel = page.locator("section[aria-labelledby='pm-reviews']")
            expect(reviews_panel).to_contain_text("Browser check product D")
            for product in ["Browser check product A", "Browser check product B", "Browser check product C"]:
                expect(reviews_panel).not_to_contain_text(product)
            print("PASS: the Overview's 'Reviews to approve' card and panel no longer show decided reviews")

            menu_link(page, "Reviews").click()
            row(900004).get_by_role("button", name="Reject review").click()
            expect(page.get_by_role("status").filter(has_text="No review is waiting for approval.")).to_be_visible()
            page.reload()
            expect(page.get_by_role("status").filter(has_text="No review is waiting for approval.")).to_be_visible()
            expect(rows).to_have_count(0)
            for pattern in [LIST_ROUTE, DECISION_ROUTE, "**/api/product-management/dashboard"]:
                page.unroute(pattern)
            print("PASS: deciding the last review, or opening an empty queue, shows 'No review is waiting for approval.'")

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
            for label, path in [("Invoices", "/manage/invoices"), ("Pricing", "/manage/pricing"), ("Discounts", "/manage/discounts")]:
                menu_link(page, label).click()
                expect(page).to_have_url(url + path)
                expect(menu_link(page, label)).to_have_attribute("aria-current", "page")
                expect(page.locator(".ui-table-frame--loading")).to_have_count(0)
                no_sideways_scroll(page, path)
            page.goto(url + "/manage/reviews")
            expect(page).to_have_url(url + "/manage")
            print("PASS: sales manager menu, final KPI values at once with reduced motion, invoices, pricing and discounts")

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
