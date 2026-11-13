import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PUBLIC = ROOT / "frontend" / "public"
PRODUCTS = json.loads((ROOT / "SeedData" / "seed-products.json").read_text(encoding="utf-8"))["products"]


def jpeg_size(path):
    """(width, height) of a JPEG file, read from its start-of-frame marker."""
    data = path.read_bytes()
    assert data[:2] == b"\xff\xd8", path.name + " is not a JPEG"
    position = 2
    while position + 9 < len(data):
        assert data[position] == 0xFF, "broken JPEG: " + path.name
        marker, length = data[position + 1], int.from_bytes(data[position + 2:position + 4], "big")
        if marker in (0xC0, 0xC1, 0xC2):
            return int.from_bytes(data[position + 7:position + 9], "big"), int.from_bytes(data[position + 5:position + 7], "big")
        position += 2 + length
    raise AssertionError("no size found in " + path.name)


class SeedProductImageTests(unittest.TestCase):
    """SCRUM-127: every seed product has its own square picture and a real description."""

    def test_every_product_has_its_own_image_file(self):
        urls = [product["imageUrl"] for product in PRODUCTS]
        self.assertEqual(len(urls), len(set(urls)), "two products share one image")
        for product in PRODUCTS:
            path = ROOT / "frontend" / "public" / product["imageUrl"].lstrip("/")
            self.assertTrue(path.is_file(), product["seedKey"] + " image is missing")

    def test_product_cards_show_images_at_1_to_1_so_every_image_is_square(self):
        for product in PRODUCTS:
            path = PUBLIC / product["imageUrl"].lstrip("/")
            if path.suffix == ".svg":
                text = path.read_text(encoding="utf-8")
                view_box = re.search(r'viewBox="0 0 (\d+) (\d+)"', text)
                self.assertIsNotNone(view_box, product["seedKey"])
                self.assertEqual(view_box.group(1), view_box.group(2), product["seedKey"] + " is not square")
                self.assertNotRegex(text, r"<script|onload=|href=\"http", product["seedKey"])
            else:
                self.assertEqual(path.suffix, ".jpg", product["seedKey"])
                width, height = jpeg_size(path)
                self.assertEqual(width, height, product["seedKey"] + " is not square")
                self.assertLessEqual(path.stat().st_size, 150 * 1024, product["seedKey"] + " is larger than 150 KB")

    def test_every_photo_is_listed_in_the_image_credits(self):
        credits = (ROOT / "docs" / "IMAGE-CREDITS.md").read_text(encoding="utf-8")
        photos = [path.name for path in (PUBLIC / "assets" / "catalog" / "products").glob("*.jpg")]
        self.assertTrue(photos)
        for name in photos:
            self.assertIn(name, credits, name + " is not listed in docs/IMAGE-CREDITS.md")

    def test_descriptions_are_written_for_each_product_and_not_placeholders(self):
        descriptions = [product["description"] for product in PRODUCTS]
        self.assertEqual(len(descriptions), len(set(descriptions)), "two products share one description")
        for product in PRODUCTS:
            description = product["description"]
            self.assertGreaterEqual(len(description), 80, product["seedKey"])
            self.assertLessEqual(len(description), 400, product["seedKey"])
            self.assertIn(product["model"], description, product["seedKey"])
            self.assertNotIn("demonstration item", description, product["seedKey"])

    def test_product_names_are_unique_and_spelled_correctly(self):
        names = [product["name"] for product in PRODUCTS]
        self.assertEqual(len(names), len(set(names)))
        self.assertFalse([name for name in names if "Smartwatche " in name])

    def test_every_product_has_brand_model_warranty_and_distributor(self):
        for product in PRODUCTS:
            for field in ("brand", "model", "warrantyStatus", "distributorInformation"):
                self.assertTrue(product[field].strip(), product["seedKey"] + " has no " + field)


if __name__ == "__main__":
    unittest.main()
