import unittest

from verify_catalog_browser import (
    assert_page_response,
    is_products_api_url,
    mock_products_response,
)


class CatalogBrowserHelpersTests(unittest.TestCase):
    def setUp(self):
        self.products = [
            {"id": 1, "categoryId": 2, "brandId": 4, "name": "Pixel A", "description": "phone",
             "price": 100, "stockQuantity": 2},
            {"id": 2, "categoryId": 2, "brandId": 4, "name": "Pixel B", "description": "phone",
             "price": 200, "stockQuantity": 1},
            {"id": 4, "categoryId": 2, "brandId": 4, "name": "Pixel C", "description": "phone",
             "price": 400, "stockQuantity": 5},
            {"id": 3, "categoryId": 3, "brandId": 5, "name": "Tablet", "description": "device",
             "price": 300, "stockQuantity": 0},
        ]

    def test_api_matcher_accepts_paginated_list_but_not_product_details(self):
        self.assertTrue(is_products_api_url("http://localhost/api/products?page=1&pageSize=24"))
        self.assertFalse(is_products_api_url("http://localhost/api/products/7"))
        self.assertFalse(is_products_api_url("http://localhost/api/products/7?page=1"))

    def test_unpaged_mock_preserves_legacy_array_and_combines_filters(self):
        legacy = mock_products_response(self.products, "http://localhost/api/products")
        self.assertIsInstance(legacy, list)
        filtered = mock_products_response(
            self.products,
            "http://localhost/api/products?categoryId=2&brandId=4&minPrice=150&maxPrice=400"
            "&search=phone&sort=price&order=desc",
        )
        self.assertEqual([item["id"] for item in filtered], [4, 2])

    def test_paged_mock_returns_the_contract_and_correct_page(self):
        page = mock_products_response(self.products, "http://localhost/api/products?page=2&pageSize=2")
        self.assertEqual(
            page,
            {
                "items": [self.products[2], self.products[3]],
                "totalCount": 4,
                "page": 2,
                "pageSize": 2,
                "totalPages": 2,
            },
        )
        assert_page_response(page, self.products, 2, 2)

    def test_malformed_mock_remains_invalid_inside_paged_envelope(self):
        response = mock_products_response(
            {"unexpected": "object"},
            "http://localhost/api/products?page=1&pageSize=24",
        )
        self.assertEqual(response["items"], {"unexpected": "object"})
        with self.assertRaises(AssertionError):
            assert_page_response(response, self.products, 1, 24)


if __name__ == "__main__":
    unittest.main()
