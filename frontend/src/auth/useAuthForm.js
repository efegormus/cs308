// What the login and register forms share: the button, the requests and the motion.
// The API contract is in docs/authentication.md, the motion rules in docs/ui-design.md.
import { useCallback, useEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { useNavigate } from "react-router";
import { NETWORK_ERROR, loginErrorMessage, parseRetryAfter } from "../lib/auth-utils.js";
import { postLoginPath } from "../lib/role-routes.js";
import { useAuth } from "./AuthProvider.jsx";

const REQUEST_TIMEOUT_MS = 15000;
const FILL_MS = 950; // Keep equal to the auth-fill-x animation in auth.css.
const CHECK_MS = 500; // How long "Signed in" stays visible when the hooks finish first.

const BUTTON_CLASSES = {
  idle: "submit",
  loading: "submit is-loading",
  fill: "submit fill",
  ok: "submit is-ok",
  locked: "submit"
};

const prefersReducedMotion = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));

export function shake(element) {
  element.classList.remove("shake");
  void element.offsetWidth; // Restarts the animation when the same field shakes again.
  element.classList.add("shake");
}

// The style of reveal line number `index`, top to bottom. The line itself has the class "r".
export const revealLine = index => ({ "--i": index });

// When a line has finished, drop the mask clipping so focus rings are not cut off.
// React leaves the "done" class alone because a line's className never changes.
function markLineDone(event) {
  if (event.target.classList.contains("ri")) event.target.parentElement.classList.add("done");
}

export function useAuthForm({ returnUrl, idleLabel, loadingLabel }) {
  const { account, authReady, completeLogin, csrfToken, refreshCsrf } = useAuth();
  const navigate = useNavigate();
  const [buttonState, setButtonState] = useState("idle");
  const [secondsLeft, setSecondsLeft] = useState(0);
  const [banner, setBanner] = useState("");
  const buttonRef = useRef(null);
  const lockoutTimer = useRef(null);
  const mounted = useRef(false);

  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      clearInterval(lockoutTimer.current);
    };
  }, []);

  // completeLogin sets the account while the success animation plays. The button is not idle then, so only a
  // visitor who was already signed in goes straight to returnUrl.
  const alreadySignedIn = authReady && account !== null && buttonState === "idle";
  useEffect(() => {
    if (alreadySignedIn) navigate(postLoginPath(account, returnUrl), { replace: true });
  }, [alreadySignedIn, account, navigate, returnUrl]);

  // Never rejects. status is null for a network error or a timeout, data is null for a body that is not JSON.
  const post = useCallback(async (path, body) => {
    try {
      const response = await fetch(path, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json",
          "X-CSRF-TOKEN": await csrfToken()
        },
        body: JSON.stringify(body),
        signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS)
      });
      let data = null;
      try {
        data = await response.json();
      } catch {
        // The caller treats it like a response with the wrong shape.
      }
      return { ok: response.ok, status: response.status, retryAfter: response.headers.get("Retry-After"), data };
    } catch {
      return { ok: false, status: null, retryAfter: null, data: null };
    }
  }, [csrfToken]);

  // A disabled button cannot take focus, so the callers need this rendered before they move focus.
  const unlock = useCallback(() => {
    flushSync(() => setButtonState("idle"));
  }, []);

  const startLockout = useCallback(seconds => {
    if (!mounted.current) return; // The visitor left while the request was running, so nothing would clear the timer.
    let left = seconds;
    setSecondsLeft(left);
    setButtonState("locked");
    lockoutTimer.current = setInterval(() => {
      left -= 1;
      if (left > 0) {
        setSecondsLeft(left);
        return;
      }
      clearInterval(lockoutTimer.current);
      setButtonState("idle");
      setBanner("");
    }, 1000);
  }, []);

  // The failures both forms handle the same way.
  const fail = useCallback((status, retryAfter) => {
    setBanner(status === null ? NETWORK_ERROR : loginErrorMessage(status));
    if (status === 429) {
      startLockout(parseRetryAfter(retryAfter));
      return;
    }
    unlock();
    if (status === 400) refreshCsrf();
    buttonRef.current?.focus();
  }, [refreshCsrf, startLockout, unlock]);

  // For a 200 from POST /api/auth/login: the fill, the check and the hooks run together, then the page redirects.
  const finishLogin = useCallback(async signedInAccount => {
    async function playSuccess() {
      if (!prefersReducedMotion()) {
        setButtonState("fill");
        await wait(FILL_MS);
      }
      setButtonState("ok");
      if (!prefersReducedMotion()) await wait(CHECK_MS);
    }
    await Promise.all([playSuccess(), completeLogin(signedInAccount)]);
    if (mounted.current) navigate(postLoginPath(signedInAccount, returnUrl), { replace: true });
  }, [completeLogin, navigate, returnUrl]);

  // Spotlight position on the primary button, relative to the button. Only the style changes, so nothing re-renders.
  const moveSpotlight = useCallback(event => {
    const button = buttonRef.current;
    const box = button.getBoundingClientRect();
    button.style.setProperty("--mx", `${event.clientX - box.left}px`);
    button.style.setProperty("--my", `${event.clientY - box.top}px`);
  }, []);

  const buttonLabels = {
    idle: idleLabel,
    loading: loadingLabel,
    fill: "Signing you in…",
    ok: "Signed in",
    locked: `Try again in ${secondsLeft}s`
  };

  return {
    // The form stays on screen during the success animation, when the account is already set.
    showForm: authReady && !alreadySignedIn,
    banner,
    setBanner,
    idle: buttonState === "idle",
    startRequest: () => setButtonState("loading"),
    post,
    unlock,
    fail,
    finishLogin,
    formProps: { className: "card play", noValidate: true, onAnimationEnd: markLineDone },
    buttonProps: {
      ref: buttonRef,
      className: BUTTON_CLASSES[buttonState],
      type: "submit",
      disabled: buttonState !== "idle",
      onPointerMove: moveSpotlight,
      children: buttonLabels[buttonState]
    }
  };
}
