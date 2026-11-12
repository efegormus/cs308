import assert from "node:assert/strict";
import test from "node:test";
import { RECENT_ORDER_COUNT, recentOrders } from "./recent-orders.js";

const orders = ids => ids.map(orderId => ({ orderId }));

test("only the newest three orders are kept, newest first", () => {
  assert.equal(RECENT_ORDER_COUNT, 3);
  assert.deepEqual(recentOrders(orders([2, 5, 1, 4, 3])).map(order => order.orderId), [5, 4, 3]);
});

test("fewer orders than the limit are all kept and an empty list stays empty", () => {
  assert.deepEqual(recentOrders(orders([1, 2])).map(order => order.orderId), [2, 1]);
  assert.deepEqual(recentOrders([]), []);
});

test("the list that was passed in is not reordered", () => {
  const input = orders([1, 3, 2]);
  recentOrders(input);
  assert.deepEqual(input.map(order => order.orderId), [1, 3, 2]);
});

test("a limit of zero or less gives no orders", () => {
  assert.deepEqual(recentOrders(orders([1, 2]), 0), []);
  assert.deepEqual(recentOrders(orders([1, 2]), -1), []);
});
