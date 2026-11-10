import { useEffect } from "react";
import { useAuth } from "./AuthProvider.jsx";

/**
 * Pass the errors of a page's private requests. A 401 means the session has ended, so the account is cleared and
 * the route guard sends the visitor to the login page. A 403 is a wrong role, not an expired session, and is left
 * to the page.
 */
export function useSessionExpiry(...errors) {
  const { expireSession } = useAuth();
  const expired = errors.some(error => error?.status === 401);
  useEffect(() => {
    if (expired) expireSession();
  }, [expired, expireSession]);
}
