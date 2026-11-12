import { useState } from "react";
import { Link, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { loginHref } from "../lib/auth-utils.js";
import { addWishlistItem, WishlistError } from "../lib/wishlist-api.js";

export default function WishlistButton({ productId }) {
  const { account, authReady, csrfToken, expireSession } = useAuth();
  const location = useLocation();
  const [status, setStatus] = useState("idle");
  const [error, setError] = useState("");
  const [sessionExpired, setSessionExpired] = useState(false);

  async function save() {
    setStatus("loading");
    setError("");
    setSessionExpired(false);
    try {
      await addWishlistItem(productId, await csrfToken());
      setStatus("saved");
    } catch (requestError) {
      setStatus("idle");
      const expired = requestError instanceof WishlistError && requestError.status === 401;
      setSessionExpired(expired);
      if (expired) expireSession();
      setError(expired
        ? "Your session expired. Log in again to save this product."
        : requestError instanceof WishlistError && requestError.status === 403
          ? "A customer account is required to use a wishlist."
          : "We couldn't update your wishlist. Please try again.");
    }
  }

  if (!authReady) return <button type="button" disabled>Checking your account…</button>;
  if (!account) return <p><Link to={loginHref(location)}>Log in to save to your wishlist</Link></p>;
  if (account.role !== "Customer") return <p>A customer account is required to use a wishlist.</p>;

  return (
    <div className="wishlist-action">
      <button type="button" disabled={status !== "idle"} onClick={save}>
        {status === "loading" ? "Saving…" : status === "saved" ? "Saved to wishlist" : "Save to wishlist"}
      </button>
      {error && <p role="alert">{error}{sessionExpired && <> <Link to={loginHref(location)}>Log in</Link></>}</p>}
      {status === "saved" && <p role="status"><Link to="/wishlist">View your wishlist</Link></p>}
    </div>
  );
}
