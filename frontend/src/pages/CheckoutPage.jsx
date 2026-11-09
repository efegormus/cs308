import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import PurchaseLines from "../components/PurchaseLines.jsx";
import { ErrorState, LoadingState } from "../components/RequestStates.js";
import { loginHref } from "../lib/auth-utils.js";
import { browserAttemptStore } from "../lib/checkout-attempt.js";
import { fetchQuote, recoverPurchase, submitCheckout } from "../lib/purchase-api.js";
import { formatPrice } from "../lib/product-display.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/request-states.css";

function Checkout({ account }) {
  const { csrfToken, refreshCsrf, cartMergeConflicts } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [attempts] = useState(() => browserAttemptStore(account.id));
  const read = useRequest();
  const payment = useRequest();
  const form = useRef(null);
  const submitting = useRef(false);
  const [needsReview, setNeedsReview] = useState(false);
  const [uncertain, setUncertain] = useState(false);

  const load = useCallback(async () => {
    setNeedsReview(false);
    const result = await read.run(async ({ signal }) => {
      const previous = attempts.get();
      if (previous?.submitted) {
        const receipt = await recoverPurchase(previous.key, { signal });
        if (receipt) return { receipt };
      }
      const quote = await fetchQuote({ signal });
      signal.throwIfAborted();
      attempts.forQuote(quote);
      return { quote };
    });
    if (result.status === "success" && result.data.receipt) {
      attempts.clear();
      navigate(`/order-confirmation?orderId=${result.data.receipt.orderId}`, { replace: true });
    }
  }, [attempts, navigate, read.run]);
  useEffect(() => { void load(); return read.cancel; }, [load, read.cancel]);

  async function purchase(event) {
    event.preventDefault();
    const quote = read.data?.quote;
    if (submitting.current || !quote || !form.current.reportValidity()) return;
    submitting.current = true;
    const inputs = form.current.elements;
    const card = { number: inputs.number.value.trim(), expiryMonth: Number(inputs.expiryMonth.value),
      expiryYear: Number(inputs.expiryYear.value), securityCode: inputs.securityCode.value };
    const attempt = attempts.forQuote(quote);
    attempts.submitted();
    try {
      const result = await payment.run(async ({ signal }) => {
        const token = await csrfToken();
        return submitCheckout(attempt, card, token, { signal });
      });
      if (result.status === "success") {
        attempts.clear();
        navigate(`/order-confirmation?orderId=${result.data.orderId}`);
      } else if (result.status === "error") {
        // A 400 can mean the CSRF token is stale. Fetch a new one so the shopper's next, deliberate submit can
        // succeed. The failed request is never sent again by itself (payment idempotency, SCRUM-113).
        if (result.error.status === 400) await refreshCsrf();
        const definite = [400, 402, 409].includes(result.error.status);
        if (definite) attempts.clear();
        setUncertain(!definite);
        if ([401, 403].includes(result.error.status)) read.reset();
        if ([401, 403, 409].includes(result.error.status)) setNeedsReview(true);
      }
      // A cancelled request (navigation/unmount) keeps its submitted key for recovery on the next visit.
    } finally {
      inputs.number.value = "";
      inputs.securityCode.value = "";
      submitting.current = false;
    }
  }
  const quote = read.data?.quote;
  const busy = payment.status === "loading";
  return <>
    {cartMergeConflicts.length > 0 && <p role="status">Some guest selections need review. <Link to="/cart">Review your cart</Link> before purchasing.</p>}
    {(read.status === "idle" || read.status === "loading") && <LoadingState label="Loading checkout…" />}
    <ErrorState error={read.error} onRetry={load} />
    <ErrorState error={payment.error} />
    {(read.error?.status === 401 || payment.error?.status === 401) && <p><Link to={loginHref(location)}>Log in again</Link></p>}
    {(uncertain || needsReview) && <p>
      <button id="retry" type="button" disabled={busy || read.status === "loading"} onClick={load}>
        {uncertain ? "Check purchase status" : "Reload checkout"}
      </button>
    </p>}
    {read.error?.status === 409 && <p><Link to="/cart">Review your cart</Link></p>}
    {quote && !needsReview && <form id="checkout-form" ref={form} onSubmit={purchase}>
      <section className="purchase-card" aria-labelledby="summary-heading">
        <h2 id="summary-heading">Your order</h2>
        <p id="customer">{quote.name} · {quote.email} · Tax ID: {quote.taxId}</p>
        <p id="address" className="snapshot-address">{quote.address}</p>
        <PurchaseLines items={quote.items} />
        <p className="purchase-total">Total: <span id="total">{formatPrice(quote.total)}</span></p>
      </section>
      <section className="purchase-card" aria-labelledby="payment-heading">
        <h2 id="payment-heading">Mock payment</h2>
        <p id="payment-help">Use test card 4242424242424242, a future expiry and a 3-digit code. No real payment is taken. Use 4000000000000002 for a declined payment.</p>
        <fieldset className="purchase-fields" disabled={busy}>
          <legend className="visually-hidden">Test payment details</legend>
          <div className="wide"><label htmlFor="number">Test card number</label>
            <input id="number" name="number" inputMode="numeric" autoComplete="off" pattern="[0-9]{13,19}" minLength="13" maxLength="19" aria-describedby="payment-help" required /></div>
          <div><label htmlFor="expiry-month">Expiry month</label><input id="expiry-month" name="expiryMonth" type="number" min="1" max="12" required /></div>
          <div><label htmlFor="expiry-year">Expiry year</label><input id="expiry-year" name="expiryYear" type="number" min={new Date().getFullYear()} max={new Date().getFullYear() + 20} required /></div>
          <div><label htmlFor="security-code">Test security code</label>
            <input id="security-code" name="securityCode" inputMode="numeric" autoComplete="cc-csc" pattern="[0-9]{3,4}" minLength="3" maxLength="4" required /></div>
        </fieldset>
        <button id="purchase" className="purchase-submit" type="submit" disabled={busy}>
          {busy ? "Confirming mock payment…" : "Confirm mock payment"}
        </button>
      </section>
    </form>}
  </>;
}

export default function CheckoutPage() {
  return <div className="route-page catalog-page purchase-page">
    <title>Checkout | Online Store</title>
    <main id="main-content" className="purchase-layout">
      <h1>Checkout</h1>
      <p className="intro">Review your items and delivery address before confirming your purchase.</p>
      <PurchaseAccess role="Customer">{account => <Checkout key={account.id} account={account} />}</PurchaseAccess>
    </main>
  </div>;
}
