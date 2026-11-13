import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PRODUCTS = json.loads((ROOT / "SeedData" / "seed-products.json").read_text(encoding="utf-8"))["products"]


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
            text = (ROOT / "frontend" / "public" / product["imageUrl"].lstrip("/")).read_text(encoding="utf-8")
            view_box = re.search(r'viewBox="0 0 (\d+) (\d+)"', text)
            self.assertIsNotNone(view_box, product["seedKey"])
            self.assertEqual(view_box.group(1), view_box.group(2), product["seedKey"] + " is not square")
            self.assertNotRegex(text, r"<script|onload=|href=\"http", product["seedKey"])

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
