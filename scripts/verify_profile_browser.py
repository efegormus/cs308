#!/usr/bin/env python3
"""Check the customer profile page (SCRUM-103) at /profile in a real browser against a disposable local server.

It registers one customer and logs in once (2 of the shared 10-a-minute login/register attempts), then checks the
guest redirect, the loading and error states, field validation, a real save, the incomplete-profile note, the
checkout redirect for an incomplete profile and the way back, the returnUrl rules, the dashboard link, the
motion on desktop (1280px) and the reduced-motion and phone layout (480px and 375px).
"""
import argparse
import json
from pathlib import Path
import re
import secrets
import time
from urllib.parse import urlparse
import uuid

from playwright.sync_api import expect, sync_playwright

INCOMPLETE = "Add your tax ID and home address before you pay."
PROFILE_INCOMPLETE_TITLE = "Complete your customer profile before checkout."


def no_sideways_scroll(page, where):
    assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), f"{where} scrolls sideways"


def wait_for_reveal(page):
    # Every masked line has finished once each one carries "done" (or the page has none).
    page.wait_for_function("[...document.querySelectorAll('.profile-form .r')].every(line => line.classList.contains('done'))")


def wait_until(page, condition, message, timeout_seconds=10):
    # page.wait_for_timeout lets Playwright deliver pending events, such as a route handler call, while it waits.
    deadline = time.monotonic() + timeout_seconds
    while not condition():
        assert time.monotonic() < deadline, message
        page.wait_for_timeout(50)


def verify(url, executable=None, screenshots=None):
    if urlparse(url).hostname not in ("localhost", "127.0.0.1"):
        raise ValueError("Profile verification is restricted to a disposable local server.")
    url = url.rstrip("/")
    shots = Path(screenshots) if screenshots else None
    if shots:
        shots.mkdir(parents=True, exist_ok=True)
    email = f"profile-{uuid.uuid4().hex}@example.com"
    password = secrets.token_urlsafe(24)
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True, **({"executable_path": executable} if executable else {}))
        desktop = browser.new_context(viewport={"width": 1280, "height": 900})
        guest = browser.new_context(viewport={"width": 1280, "height": 900})
        errors = []

        def post(context, path, body):
            # The login and register limit is 10 a minute per address and other checks share it: wait once on 429.
            for attempt in range(2):
                token = context.request.get(url + "/api/auth/csrf").json()["token"]
                response = context.request.post(url + path, data=body, headers={"X-CSRF-TOKEN": token})
                if response.status != 429 or attempt:
                    return response
                print(f"INFO: {path} answered 429 (shared login/register limit); waiting 61 s once")
                time.sleep(61)

        def new_page(context):
            page = context.new_page()
            page.on("pageerror", lambda error: errors.append(str(error)))
            return page

        def shot(page, name):
            if shots:
                page.screenshot(path=str(shots / f"{name}.png"), full_page=True)

        def submit(page):
            return page.get_by_role("button", name=re.compile("^(Save changes|Saving…|Saved)$"))

        try:
            response = post(desktop, "/api/auth/register", {
                "name": "Profile check", "email": email, "taxId": "10000000146",
                "homeAddress": "1 Browser Street", "password": password})
            assert response.status == 201, f"Registration failed: {response.status}"
            response = post(desktop, "/api/auth/login", {"email": email, "password": password})
            assert response.status == 200, f"Login failed: {response.status}"

            # A guest is sent to the login page and brought back afterwards.
            page = new_page(guest)
            page.goto(url + "/profile?returnUrl=%2Fcheckout")
            expect(page).to_have_url(re.compile(r"/login\?returnUrl=%2Fprofile%3FreturnUrl%3D%252Fcheckout$"))
            print("PASS: a guest opening /profile goes to the login page with a returnUrl back to it")

            # Loading state while GET /api/customer/profile is held, then the form with both cards.
            page = new_page(desktop)
            held = []
            page.route("**/api/customer/profile", lambda route: held.append(route) if route.request.method == "GET" else route.continue_())
            page.goto(url + "/profile")
            # The page shows the loading line before it sends the GET, and the sync API only runs a route handler
            # inside a later Playwright call, so wait for the handler itself before checking the held request.
            wait_until(page, lambda: held, "The profile request was not made.")
            expect(page.get_by_role("status").filter(has_text="Loading your profile…")).to_be_visible()
            held[0].continue_()
            page.unroute("**/api/customer/profile")
            expect(page.get_by_role("heading", level=1, name="My profile")).to_be_visible()
            expect(page.get_by_text("These details appear on your invoice and delivery.")).to_be_visible()
            expect(page.get_by_role("heading", level=2, name="Account")).to_be_visible()
            expect(page.get_by_role("heading", level=2, name="Invoice and delivery")).to_be_visible()
            expect(page.get_by_label("Full name")).to_have_value("Profile check")
            expect(page.get_by_label("Email")).to_have_value(email)
            expect(page.get_by_label("Email")).to_have_attribute("readonly", "")
            expect(page.get_by_text("Email cannot be changed here.")).to_be_visible()
            expect(page.get_by_label("Tax ID (TC kimlik no)")).to_have_value("10000000146")
            expect(page.get_by_label("Home address")).to_have_value("1 Browser Street")
            expect(page.get_by_text(INCOMPLETE)).to_have_count(0)
            print("PASS: 'Loading your profile…' while loading, then the Account and Invoice and delivery cards")

            # Motion on desktop: masked reveal on every line, the cards rise 140ms apart, masks come off when done.
            reveal = page.locator(".profile-form .ri").first.evaluate("e => getComputedStyle(e).animationName")
            assert reveal == "auth-reveal-up", reveal
            cards = page.locator(".profile-card")
            delays = [cards.nth(i).evaluate("e => [getComputedStyle(e).animationName, getComputedStyle(e).animationDelay]") for i in range(2)]
            assert delays == [["profile-card-in", "0.12s"], ["profile-card-in", "0.26s"]], delays
            wait_for_reveal(page)
            shot(page, "profile-desktop")
            no_sideways_scroll(page, "profile 1280px")
            print("PASS: masked reveal on every line, cards rise in 140ms apart, masks are removed when done")

            # Client validation: an empty name and a too long tax ID shake, get messages and focus the first one.
            page.get_by_label("Full name").fill("   ")
            page.get_by_label("Tax ID (TC kimlik no)").fill("1" * 65)
            submit(page).click()
            name = page.get_by_label("Full name")
            expect(name).to_have_attribute("aria-invalid", "true")
            expect(name).to_be_focused()
            expect(page.locator("#name-error[role='alert']")).to_have_text("Enter your full name.")
            expect(page.locator("#tax-id-error[role='alert']")).to_have_text("Use at most 64 characters.")
            expect(name).to_have_attribute("aria-describedby", "name-error")
            assert "shake" in (name.get_attribute("class") or "")
            print("PASS: empty and too long fields shake, show messages under the field and focus the first one")

            # Server field errors are mapped to their field.
            page.get_by_label("Full name").fill("Profile check")
            page.get_by_label("Tax ID (TC kimlik no)").fill("10000000146")
            page.route("**/api/customer/profile", lambda route: route.fulfill(status=400, json={
                "title": "SECRET", "errors": {"homeAddress": ["This value is too long when encoded as UTF-8."]}})
                if route.request.method == "PUT" else route.continue_())
            submit(page).click()
            expect(page.locator("#home-address-error")).to_have_text("This value is too long when encoded as UTF-8.")
            expect(page.get_by_label("Home address")).to_be_focused()
            expect(page.get_by_text("SECRET")).to_have_count(0)
            page.unroute("**/api/customer/profile")
            print("PASS: a 400 from the server puts its field message under the field, never other text")

            # A real save: spinner, green fill, "Saved", then back to "Save changes"; the server has the new values.
            page.get_by_label("Full name").fill("  Profile Renamed  ")
            page.get_by_label("Home address").fill("2 Saved Street\nIstanbul")
            submit(page).click()
            expect(page.locator("#submit.fill, #submit.is-ok")).to_have_count(1, timeout=5000)
            expect(submit(page)).to_have_text("Saved", timeout=5000)
            expect(page.get_by_role("status").filter(has_text="Your changes are saved.")).to_be_visible()
            expect(page.locator("#continue")).to_have_count(0)
            expect(submit(page)).to_have_text("Save changes", timeout=5000)
            expect(submit(page)).to_be_enabled()
            stored = page.request.get(url + "/api/customer/profile").json()
            assert stored["name"] == "Profile Renamed" and stored["homeAddress"] == "2 Saved Street\nIstanbul", stored
            assert stored["email"] == email and "passwordHash" not in json.dumps(stored), stored
            expect(page.get_by_label("Full name")).to_have_value("Profile Renamed")
            print("PASS: saving fills the button, shows 'Saved', returns to 'Save changes' and stores trimmed values")

            # The dashboard links to the page.
            page.goto(url + "/dashboard")
            page.get_by_role("link", name="Edit details").click()
            expect(page).to_have_url(url + "/profile")
            print("PASS: 'Edit details' on the dashboard opens /profile")

            # Checkout with an incomplete profile goes to /profile?returnUrl=/checkout; after a save, back to checkout.
            def blank_profile(route):
                if route.request.method != "GET":
                    route.continue_()
                    return
                data = route.fetch().json()
                route.fulfill(status=200, json={**data, "taxId": "", "homeAddress": ""})

            page.route("**/api/customer/profile", blank_profile)
            page.route("**/api/customer/checkout", lambda route: route.fulfill(status=409, json={"status": 409, "title": PROFILE_INCOMPLETE_TITLE})
                       if route.request.method == "GET" else route.continue_())
            page.goto(url + "/checkout")
            expect(page).to_have_url(url + "/profile?returnUrl=%2Fcheckout")
            expect(page.get_by_role("status").filter(has_text=INCOMPLETE)).to_be_visible()
            expect(page.get_by_label("Tax ID (TC kimlik no)")).to_have_value("")
            expect(page.get_by_label("Home address")).to_have_value("")
            wait_for_reveal(page)
            shot(page, "profile-incomplete-desktop")
            page.get_by_label("Tax ID (TC kimlik no)").fill("10000000146")
            page.get_by_label("Home address").fill("3 Complete Street")
            page.unroute("**/api/customer/profile")
            submit(page).click()
            back = page.get_by_role("link", name="Continue to checkout")
            expect(back).to_be_visible(timeout=5000)
            expect(back).to_have_attribute("href", "/checkout")
            expect(page.get_by_text(INCOMPLETE)).to_have_count(0)
            page.unroute("**/api/customer/checkout")
            back.click()
            expect(page).to_have_url(url + "/checkout")
            expect(page.get_by_role("heading", level=1, name="Checkout")).to_be_visible()
            print("PASS: checkout's incomplete-profile 409 opens /profile?returnUrl=/checkout and the saved page links back")

            # Another 409 from checkout keeps its message and does not leave the page.
            page.route("**/api/customer/checkout", lambda route: route.fulfill(status=409, json={"status": 409, "title": "Your cart is empty. Add products before checkout."}))
            page.goto(url + "/checkout")
            expect(page.get_by_role("link", name="Review your cart")).to_be_visible()
            expect(page).to_have_url(url + "/checkout")
            page.unroute("**/api/customer/checkout")
            print("PASS: other checkout 409s stay on checkout with their own message")

            # An external returnUrl never becomes a link.
            page.goto(url + "/profile?returnUrl=https%3A%2F%2Fevil.example%2Fcheckout")
            expect(page.get_by_label("Full name")).to_have_value("Profile Renamed")
            submit(page).click()
            expect(page.get_by_role("status").filter(has_text="Your changes are saved.")).to_be_visible(timeout=5000)
            expect(page.locator("#continue")).to_have_count(0)
            print("PASS: an external returnUrl is ignored after a save")

            # Load error with Try again.
            failed = []
            page.route("**/api/customer/profile", lambda route: (failed.append(1), route.fulfill(status=503, body="SECRET")) if not failed else route.continue_())
            page.goto(url + "/profile")
            alert = page.get_by_role("alert")
            expect(alert).to_contain_text("temporarily unavailable")
            expect(page.get_by_text("SECRET")).to_have_count(0)
            page.get_by_role("button", name="Try again").click()
            expect(page.get_by_label("Full name")).to_have_value("Profile Renamed")
            page.unroute("**/api/customer/profile")
            print("PASS: a failed load shows a safe alert with Try again, which loads the form")

            # Reduced motion and phone widths: no animation, masks off, no sideways scroll.
            reduced = browser.new_context(viewport={"width": 480, "height": 900}, reduced_motion="reduce",
                                          storage_state=desktop.storage_state())
            phone = new_page(reduced)
            phone.goto(url + "/profile")
            expect(phone.get_by_label("Full name")).to_have_value("Profile Renamed")
            state = phone.evaluate("""() => ({
                reveal: getComputedStyle(document.querySelector('.profile-form .ri')).animationName,
                card: getComputedStyle(document.querySelector('.profile-card')).animationName,
                masks: [...document.querySelectorAll('.profile-form .r')].every(line => getComputedStyle(line).overflow === 'visible'),
                padding: getComputedStyle(document.querySelector('.profile-card')).paddingLeft })""")
            assert state == {"reveal": "none", "card": "none", "masks": True, "padding": "20px"}, state
            no_sideways_scroll(phone, "profile 480px")
            shot(phone, "profile-480-reduced")
            phone.get_by_label("Full name").fill("Profile Reduced")
            submit(phone).click()
            expect(submit(phone)).to_have_text("Saved", timeout=5000)
            assert "fill" not in (phone.locator("#submit").get_attribute("class") or ""), "reduced motion skips the fill"
            expect(submit(phone)).to_have_text("Save changes", timeout=5000)
            phone.set_viewport_size({"width": 375, "height": 812})
            no_sideways_scroll(phone, "profile 375px")
            print("PASS: reduced motion shows everything at once with masks off; 480px and 375px have no sideways scroll")

            assert not errors, errors
            print("PASS: no browser exceptions")
        finally:
            browser.close()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:5200")
    parser.add_argument("--browser-executable")
    parser.add_argument("--screenshots", help="Folder for full-page screenshots (optional).")
    args = parser.parse_args()
    verify(args.url, args.browser_executable, args.screenshots)


if __name__ == "__main__":
    main()
