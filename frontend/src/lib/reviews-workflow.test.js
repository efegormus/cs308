import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { isCustomerReview, isModerationPage, reviewInput, fetchCustomerReview, submitReview, fetchModeration, moderateComment } from "./reviews-api.js";

const oldFetch = globalThis.fetch; afterEach(() => { globalThis.fetch = oldFetch; });
const own = { productId:1,eligible:true,orderItemId:3,rating:null,comment:null,canRate:true,canComment:true };
const pending = { id:8,productId:1,productName:"Phone",customerName:"Buyer",rating:4,body:"Text",createdAt:"2026-11-20T10:00:00Z",version:"123" };

test("private review validation keeps unknown status and invalid ratings out", () => {
  assert.ok(isCustomerReview(own)); assert.equal(isCustomerReview({...own,rating:6}),false);
  assert.equal(isCustomerReview({...own,comment:{id:1,body:"text",status:"Public"}}),false);
});
test("review input allows independent components but never resubmits an existing component", () => {
  assert.deepEqual(reviewInput("5","  Delivered  ",own),{rating:5,comment:"Delivered"});
  assert.deepEqual(reviewInput("5","new",{...own,canRate:false}),{comment:"new"});
  assert.deepEqual(reviewInput("4","ignored",{...own,canComment:false}),{rating:4});
  assert.equal(reviewInput("","",own),null); assert.equal(reviewInput("6","",own),null);
  assert.equal(reviewInput("5","x".repeat(2001),own),null);
});
test("review submit only sends rating/comment and CSRF", async () => {
  globalThis.fetch = async (url,options) => {
    assert.equal(url,"/api/customer/products/1/review"); assert.equal(options.headers["X-CSRF-TOKEN"],"current");
    assert.deepEqual(JSON.parse(options.body),{rating:5,comment:"Text"});
    return Response.json({...own,rating:5,canRate:false});
  };
  await submitReview(1,{rating:5,comment:"Text",userId:99,status:"Approved"},"current");
});
test("private reads retain authorization status without leaking body",async()=>{
  globalThis.fetch=async()=>new Response("PRIVATE",{status:401});
  await assert.rejects(fetchCustomerReview(1),error=>error.status===401&&!JSON.stringify(error).includes("PRIVATE"));
});
test("moderation pages validate queue fields and optional rating",()=>{
  assert.ok(isModerationPage({items:[pending],nextAfterId:null}));
  assert.ok(isModerationPage({items:[{...pending,rating:null}],nextAfterId:8}));
  assert.equal(isModerationPage({items:[{...pending,version:"invalid"}],nextAfterId:null}),false);
  assert.equal(isModerationPage({items:[{...pending,rating:7}],nextAfterId:null}),false);
});
test("moderation writes forward only the decision and the current version",async()=>{
  globalThis.fetch=async(url,options)=>{
    assert.equal(url,"/api/product-management/comments/8/moderation");
    assert.deepEqual(JSON.parse(options.body),{status:"Rejected",version:"123"});
    assert.equal(options.headers["X-CSRF-TOKEN"],"csrf");return Response.json({id:8,status:"Rejected",version:"124"});
  };
  await moderateComment({...pending,userId:99},"Rejected","csrf");
});
test("unknown decisions and negative queue cursors do not send a request",async()=>{
  globalThis.fetch=()=>{throw new Error("must not fetch");};
  await assert.rejects(moderateComment(pending,"Pending","csrf"),{kind:"invalid-request"});
  await assert.rejects(fetchModeration(-1),{kind:"invalid-request"});
});
test("a stale moderation decision reports 409 without forwarding private details",async()=>{
  globalThis.fetch=async()=>new Response("PRIVATE",{status:409});
  await assert.rejects(moderateComment(pending,"Approved","csrf"),error=>error.status===409&&!JSON.stringify(error).includes("PRIVATE"));
});
