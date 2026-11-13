// The React version of window.StoreAuth from wwwroot/js/auth.js. The API contract is in docs/authentication.md.
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { parseAccount, runHooks } from "../lib/auth-utils.js";
import { mergeGuestCart } from "../lib/purchase-api.js";

const REQUEST_TIMEOUT_MS = 10000;
const LOGIN_HOOK_LIMIT_MS = 5000;

const AuthContext = createContext(null);

async function fetchToken() {
  const response = await fetch("/api/auth/csrf", {
    headers: { Accept: "application/json" }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
  });
  if (!response.ok) throw new Error("CSRF request failed.");
  const { token } = await response.json();
  if (typeof token !== "string" || !token) throw new Error("Invalid CSRF response.");
  return token;
}

// Never rejects.
async function fetchAccount() {
  try {
    const response = await fetch("/api/auth/me", {
      headers: { Accept: "application/json" }, signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
    });
    // 401 is the normal answer for a guest.
    return response.ok ? parseAccount(await response.json()) : null;
  } catch {
    return null;
  }
}

async function postLogout(token) {
  const response = await fetch("/api/auth/logout", {
    method: "POST",
    headers: { "X-CSRF-TOKEN": token },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
  });
  return response.status;
}

export function AuthProvider({ children }) {
  const [account, setAccount] = useState(null);
  const [authReady, setAuthReady] = useState(false); // True after the first /me check, whatever its result.
  const tokenRequest = useRef(null);
  const loginHooks = useRef([]);
  const sessionChecked = useRef(false);
  const merging = useRef(null);
  const [cartMergeError, setCartMergeError] = useState(null);
  const [cartMergeConflicts, setCartMergeConflicts] = useState([]);
  const [cartMergeVersion, setCartMergeVersion] = useState(0);
  const [cartMergeBusy, setCartMergeBusy] = useState(false);

  // The token is bound to the current identity, so it changes after login and logout.
  const csrfToken = useCallback(() => {
    tokenRequest.current ??= fetchToken().catch(error => {
      tokenRequest.current = null; // The next caller tries again.
      throw error;
    });
    return tokenRequest.current;
  }, []);

  // Never rejects.
  const refreshCsrf = useCallback(async () => {
    tokenRequest.current = null;
    try {
      await csrfToken();
    } catch {
      // csrfToken() tries again the next time something needs a token.
    }
  }, [csrfToken]);

  // Guest ownership stays in the HttpOnly cookie. Transfers are idempotent and use the fresh identity's CSRF token.
  const restoreGuestCart = useCallback(() => {
    if (merging.current) return merging.current;
    setCartMergeBusy(true);
    setCartMergeError(null);
    merging.current = (async () => {
      try {
        // The same limit as the login hooks: a request that hangs must not hold up the login or the page load.
        const result = await mergeGuestCart(await csrfToken(), { signal: AbortSignal.timeout(LOGIN_HOOK_LIMIT_MS) });
        setCartMergeConflicts(result.conflicts);
        setCartMergeVersion(version => version + 1);
      } catch (error) {
        setCartMergeError(error);
      } finally {
        merging.current = null;
        setCartMergeBusy(false);
      }
    })();
    return merging.current;
  }, [csrfToken]);

  // For the login page, after a 200 from POST /api/auth/login. Never rejects.
  const completeLogin = useCallback(async signedInAccount => {
    await refreshCsrf(); // Before the hooks, because they may POST with the new token.
    if (signedInAccount?.role === "Customer") await restoreGuestCart();
    setAccount(signedInAccount);
    await runHooks(loginHooks.current, signedInAccount, LOGIN_HOOK_LIMIT_MS);
  }, [refreshCsrf, restoreGuestCart]);

  // Rejects if the visitor could not be signed out.
  const logout = useCallback(async () => {
    let status = await postLogout(await csrfToken());
    // A token from before a login in another tab is stale. Get a new one and try once more.
    if (status === 400) {
      tokenRequest.current = null;
      status = await postLogout(await csrfToken());
    }
    // 401 means the session had already ended, so the visitor is signed out either way.
    if (status !== 204 && status !== 401) throw new Error("Logout failed.");
    await refreshCsrf();
    setAccount(null);
    setCartMergeError(null);
    setCartMergeConflicts([]);
  }, [csrfToken, refreshCsrf]);

  // For a 401 from a private request: the session ended on the server, so forget the account. The pages that
  // need it unmount (which aborts their requests) and the route guard sends the visitor to the login page.
  const expireSession = useCallback(() => {
    setAccount(null);
    setCartMergeError(null);
    setCartMergeConflicts([]);
    void refreshCsrf();
  }, [refreshCsrf]);

  // After the customer saves a new name on /profile (SCRUM-103), so the header shows it without a reload.
  // Only the name of the same account changes; the server stays the source of the role and the email.
  const renameAccount = useCallback((id, name) => {
    if (typeof name !== "string" || !name) return;
    setAccount(current => (current && current.id === id && current.name !== name ? { ...current, name } : current));
  }, []);

  // Returns a function that removes the hook, so a component can use it as an effect cleanup:
  // useEffect(() => onLogin(hook), [onLogin]). Without it, StrictMode would register the hook twice.
  const onLogin = useCallback(fn => {
    if (typeof fn !== "function") return () => {};
    loginHooks.current.push(fn);
    return () => {
      loginHooks.current = loginHooks.current.filter(hook => hook !== fn);
    };
  }, []);

  useEffect(() => {
    // StrictMode runs effects twice in development. As on the old pages, there is one check per page load.
    if (sessionChecked.current) return;
    sessionChecked.current = true;
    fetchAccount().then(async found => {
      if (found?.role === "Customer") await restoreGuestCart();
      setAccount(found);
      setAuthReady(true);
    });
  }, [restoreGuestCart]);

  const value = useMemo(
    () => ({ account, authReady, csrfToken, refreshCsrf, completeLogin, logout, expireSession, renameAccount, onLogin, restoreGuestCart, cartMergeError, cartMergeConflicts, cartMergeVersion, cartMergeBusy }),
    [account, authReady, csrfToken, refreshCsrf, completeLogin, logout, expireSession, renameAccount, onLogin, restoreGuestCart, cartMergeError, cartMergeConflicts, cartMergeVersion, cartMergeBusy]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error("useAuth must be used inside AuthProvider.");
  return auth;
}
