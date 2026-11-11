import { useEffect, useState } from "react";
import { CART_CHANGED_EVENT, fetchCart } from "../lib/cart-api.js";

// The number of items in the cart, for the header's cart link (SCRUM-123). It loads again when refreshKey changes
// (the header passes the page, the account and the cart merge) and after every cart change on any page.
// null means "unknown": loading, disabled (staff have no cart) or failed. A failure is never shown as an error;
// the cart page itself reports cart problems.
export function useCartCount(enabled, refreshKey) {
  const [count, setCount] = useState(null);
  const [changes, setChanges] = useState(0);

  useEffect(() => {
    const onChange = () => setChanges(value => value + 1);
    window.addEventListener(CART_CHANGED_EVENT, onChange);
    return () => window.removeEventListener(CART_CHANGED_EVENT, onChange);
  }, []);

  useEffect(() => {
    if (!enabled) {
      setCount(null);
      return undefined;
    }
    const controller = new AbortController();
    fetchCart({ signal: controller.signal })
      .then(cart => setCount(cart.itemCount))
      .catch(() => { if (!controller.signal.aborted) setCount(null); });
    return () => controller.abort();
  }, [enabled, refreshKey, changes]);

  return count;
}
