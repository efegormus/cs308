import test from "node:test";
import assert from "node:assert/strict";
import { isLoadedProduct, parseProductId, specRows } from "./product-detail.js";

test("parseProductId accepts plain positive integers", () => {
  assert.equal(parseProductId("3"), 3);
  assert.equal(parseProductId("20"), 20);
  assert.equal(parseProductId("123456789"), 123456789);
});

test("parseProductId rejects everything else", () => {
  for (const value of [null, undefined, "", "0", "00", "-1", "+1", "1.5", "1e3", " 3", "3 ", "abc", "3abc",
    "1234567890", "0x10", "٣", ["3"], 3]) {
    assert.equal(parseProductId(value), null, String(value));
  }
});

test("specRows lists the customer fields and skips empty ones", () => {
  const rows = specRows({ model: "GP9", warrantyStatus: "2 years", distributorInformation: "Example Ltd." });
  assert.deepEqual(rows, [
    { label: "Model", value: "GP9" },
    { label: "Warranty", value: "2 years" },
    { label: "Distributor", value: "Example Ltd." }
  ]);
  assert.deepEqual(specRows({ model: "", warrantyStatus: "   ", distributorInformation: null }), []);
  assert.deepEqual(specRows({ model: "GP9" }), [{ label: "Model", value: "GP9" }]);
});

test("isLoadedProduct accepts a wrapped product and a wrapped not-found, nothing else", () => {
  assert.equal(isLoadedProduct({ product: { id: 1 } }), true);
  assert.equal(isLoadedProduct({ product: null }), true);
  assert.equal(isLoadedProduct(null), false);
  assert.equal(isLoadedProduct(undefined), false);
  assert.equal(isLoadedProduct("product"), false);
  assert.equal(isLoadedProduct({}), false);
});
