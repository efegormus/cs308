"""Real browser/API acceptance checks shared by the purchasing demo verifier."""
from email import policy
from email.parser import BytesParser
import json
import time
from urllib.parse import urlparse
from urllib.request import urlopen
from playwright.sync_api import expect


def invoice_proof(page, customer, manager, browser, url, order, product, smtp_url, recipient):
    details = customer.request.get(f"{url}/api/customer/orders/{order}/invoice/details")
    assert details.status == 200
    invoice = details.json()
    expect(page.locator("#invoice-details")).to_contain_text(invoice["invoiceNumber"])
    expect(page.locator("#invoice-details")).to_contain_text(product["name"])
    assert invoice["orderId"] == order and invoice["currency"] == "TRY"
    line = next(row for row in invoice["lines"] if row["productName"] == product["name"])
    assert line["unitPrice"] == product["price"]
    pdf = customer.request.get(f"{url}/api/customer/orders/{order}/invoice")
    assert pdf.status == 200 and pdf.body().startswith(b"%PDF")
    guest = browser.new_context()
    try:
        for path in ["invoice", "invoice/details"]:
            assert guest.request.get(f"{url}/api/customer/orders/{order}/{path}").status == 401
            assert manager.request.get(f"{url}/api/customer/orders/{order}/{path}").status in (401, 403)
    finally:
        guest.close()
    if smtp_url:
        if urlparse(smtp_url).hostname not in ("localhost", "127.0.0.1"):
            raise ValueError("The SMTP evidence API must be a disposable local test inbox.")
        deadline = time.monotonic() + 40
        matching = []
        while time.monotonic() < deadline:
            with urlopen(smtp_url.rstrip("/") + "/api/v1/messages", timeout=10) as response:
                messages = json.load(response)["messages"]
            matching = [m for m in messages if m["Subject"] == f"Invoice for order {order}"
                        and any(to["Address"] == recipient for to in m["To"])]
            if matching:
                break
            time.sleep(1)
        assert len(matching) == 1, "The order must produce exactly one SMTP message for its buyer."
        with urlopen(smtp_url.rstrip("/") + "/api/v1/message/" + matching[0]["ID"] + "/raw", timeout=10) as response:
            message = BytesParser(policy=policy.default).parsebytes(response.read())
        attachments = list(message.iter_attachments())
        assert len(attachments) == 1
        assert attachments[0].get_content_type() == "application/pdf"
        assert attachments[0].get_filename() == invoice["invoiceNumber"] + ".pdf"
        assert attachments[0].get_payload(decode=True) == pdf.body(), "Emailed PDF differs from the protected download."
        print("PASS: real SMTP delivery contains the exact protected invoice PDF; invoice is rendered on screen")
    print("PASS: invoice screen and PDF use paid snapshots and deny anonymous/staff access")


def review_proof(page, staff, customer, manager, browser, url, order, products, suffix, post):
    guest = browser.new_context()
    try:
        for product, decision in zip(products, ["Approved", "Rejected"]):
            product_id = product["id"]
            public_url = f"{url}/api/products/{product_id}/reviews"
            own_url = f"{url}/api/customer/products/{product_id}/review"
            before = guest.request.get(public_url).json()
            page.goto(url + "/orders")
            order_card = page.locator(".order-card").filter(has=page.get_by_role("heading", name=f"Order #{order}", exact=True))
            expect(order_card).to_contain_text("Delivered")
            order_card.get_by_role("link", name="Rate / Comment: " + product["name"], exact=True).click()
            expect(page.locator(".review-form")).to_be_visible()
            detail = customer.request.get(f"{url}/api/products/{product_id}").json()
            specs = page.locator(".product-specs")
            for label, value in [("Product ID", str(product_id)), ("Serial number", detail["serialNumber"]),
                                 ("Model", detail["model"]), ("Warranty", detail["warrantyStatus"]),
                                 ("Distributor", detail["distributorInformation"])]:
                expect(specs).to_contain_text(label)
                expect(specs).to_contain_text(value)
            expect(page.locator(".product-description")).to_have_text(detail["description"])
            assert guest.request.get(own_url).status == 401
            assert post(guest, f"/api/customer/products/{product_id}/review", {"rating": 5}).status == 401
            assert post(customer, "/api/product-management/comments/1/moderation", {"status": "Approved", "version": "1"}).status == 403
            assert manager.request.get(own_url).status == 403
            body = f"Browser acceptance {suffix} {decision} <img src=x onerror=alert(1)>"
            page.get_by_label("Rating", exact=True).select_option("5")
            page.get_by_label("Comment (optional)").fill(body)
            page.get_by_role("button", name="Submit review", exact=True).click()
            expect(page.locator(".own-comment")).to_contain_text("Waiting for approval")
            expect(page.locator(".own-comment")).to_contain_text(body)
            assert page.locator(".own-comment img").count() == 0
            own = customer.request.get(own_url).json()
            assert own["rating"] == 5 and own["comment"]["status"] == "Pending"
            assert not own["canRate"] and not own["canComment"]
            public = guest.request.get(public_url).json()
            assert public["ratingCount"] == before["ratingCount"] + 1
            assert all(c["body"] != body for c in public["comments"])
            staff.goto(url + "/manage/reviews")
            row = staff.locator("tbody tr").filter(has_text=body)
            expect(row).to_have_count(1)
            row.get_by_role("button", name="Approve" if decision == "Approved" else "Reject", exact=True).click()
            expect(row).to_have_count(0)
            expect(staff.get_by_role("status")).to_contain_text(decision.lower())
            page.reload()
            expect(page.locator(".own-comment")).to_contain_text("Status: " + decision)
            public = guest.request.get(public_url).json()
            assert any(c["body"] == body for c in public["comments"]) == (decision == "Approved")
            view = guest.new_page()
            view.goto(f"{url}/product?id={product_id}#reviews")
            if decision == "Approved":
                expect(view.locator(".review-comments")).to_contain_text(body)
            else:
                expect(view.locator(".review-comments")).not_to_contain_text(body)
            assert view.locator(".review-comments img").count() == 0
            for width in (1280, 480, 375):
                page.set_viewport_size({"width": width, "height": 900})
                assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), f"Review page overflows at {width}px"
                staff.set_viewport_size({"width": width, "height": 900})
                assert staff.evaluate("document.documentElement.scrollWidth <= innerWidth"), f"Moderation page overflows at {width}px"
            view.close()
            print(f"PASS: delivered order → real rating/comment form → {decision.lower()} moderation → correct public/private visibility; nine product fields and mobile layouts")
    finally:
        guest.close()
