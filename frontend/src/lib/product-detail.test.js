import test from "node:test";
import assert from "node:assert/strict";
import { isCompleteProduct, isLoadedProduct, parseProductId, specRows } from "./product-detail.js";

test("parseProductId accepts plain positive integers", () => {
  assert.equal(parseProductId("2147483648"),2147483648);
  assert.equal(parseProductId("9007199254740991"),Number.MAX_SAFE_INTEGER);
  assert.equal(parseProductId("3"), 3);
  assert.equal(parseProductId("20"), 20);
  assert.equal(parseProductId("3000000000"), 3000000000);
  assert.equal(parseProductId("123456789"), 123456789);
});

test("the product specification block exposes ID and serial number",()=>{
  assert.deepEqual(specRows({id:20,serialNumber:"SERIAL-20"}),[{label:"Product ID",value:"20"},{label:"Serial number",value:"SERIAL-20"}]);
});

test("every required product detail field is validated",()=>{
  const complete={id:1,name:"Phone",model:"Model",serialNumber:"Serial",description:"Description",stockQuantity:0,price:0,warrantyStatus:"2 years",distributorInformation:"Distributor"};
  assert.ok(isCompleteProduct(complete));
  for(const key of Object.keys(complete))assert.equal(isCompleteProduct({...complete,[key]:undefined}),false,key);
});

test("parseProductId rejects everything else", () => {
  for (const value of [null, undefined, "", "0", "00", "-1", "+1", "1.5", "1e3", " 3", "3 ", "abc", "3abc",
    "9007199254740992", "0x10", "٣", ["3"], 3]) {
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
