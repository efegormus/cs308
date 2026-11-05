// The React version of window.StoreAuth from wwwroot/js/auth.js. The API contract is in docs/authentication.md.
import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { parseAccount, runHooks } from "../lib/auth-utils.js";

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

  // For the login page, after a 200 from POST /api/auth/login. Never rejects.
  const completeLogin = useCallback(async signedInAccount => {
    await refreshCsrf(); // Before the hooks, because they may POST with the new token.
    setAccount(signedInAccount);
    await runHooks(loginHooks.current, signedInAccount, LOGIN_HOOK_LIMIT_MS);
  }, [refreshCsrf]);

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
  }, [csrfToken, refreshCsrf]);

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
    fetchAccount().then(found => {
      setAccount(found);
      setAuthReady(true);
    });
  }, []);

  const value = useMemo(
    () => ({ account, authReady, csrfToken, refreshCsrf, completeLogin, logout, onLogin }),
    [account, authReady, csrfToken, refreshCsrf, completeLogin, logout, onLogin]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const auth = useContext(AuthContext);
  if (!auth) throw new Error("useAuth must be used inside AuthProvider.");
  return auth;
}
