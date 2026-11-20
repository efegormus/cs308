import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { fetchInvoice, isInvoice } from "./invoice-api.js";
const oldFetch=globalThis.fetch;afterEach(()=>{globalThis.fetch=oldFetch;});
const invoice={orderId:2147483648,invoiceNumber:"INV-2026-2147483648",purchasedAt:"2026-11-20T10:00:00Z",issuedAt:"2026-11-20T10:01:00Z",customerName:"Buyer",taxId:"Tax",address:"Address",currency:"TRY",lines:[{productName:"Phone",quantity:1,unitPrice:90,total:90}],total:90};
test("valid invoices accept safe order IDs above the signed 32-bit range",()=>assert.ok(isInvoice(invoice)));
test("invoice details reject absent snapshots, invalid amounts and malformed lines",()=>{
  for(const value of [null,{}, {...invoice,lines:[]},{...invoice,currency:"USD"},{...invoice,total:-1},{...invoice,taxId:undefined}])assert.equal(isInvoice(value),false);
});
test("invoice fetch is owner scoped and checks the returned order",async()=>{
  globalThis.fetch=async url=>{assert.equal(url,"/api/customer/orders/2147483648/invoice/details");return Response.json(invoice);};
  assert.deepEqual(await fetchInvoice(2147483648),invoice);
  globalThis.fetch=async()=>Response.json({...invoice,orderId:1});
  await assert.rejects(fetchInvoice(2147483648),{kind:"invalid-response"});
});
test("invoice failures do not expose private response contents",async()=>{
  globalThis.fetch=async()=>new Response("PRIVATE",{status:404});
  await assert.rejects(fetchInvoice(1),error=>error.status===404&&!JSON.stringify(error).includes("PRIVATE"));
});
