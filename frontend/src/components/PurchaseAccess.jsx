import { Navigate, useLocation } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { loginHref } from "../lib/auth-utils.js";
import { ErrorState, LoadingState } from "./RequestStates.js";

export default function PurchaseAccess({ role, children }) {
  const { account, authReady, cartMergeError, restoreGuestCart, cartMergeBusy } = useAuth();
  const location = useLocation();
  if (!authReady) return <LoadingState label="Checking your session…" />;
  if (!account) return <Navigate replace to={loginHref(location)} />;
  const roles = [].concat(role);
  if (!roles.includes(account.role)) return <p role="alert">A {roles.join(" or ")} account is required for this page.</p>;
  if (account.role === "Customer" && cartMergeError)
    return <ErrorState error={cartMergeError} onRetry={restoreGuestCart} retrying={cartMergeBusy} />;
  return children(account);
}
