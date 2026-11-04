// Login page behaviour. The API contract is in docs/authentication.md, the motion rules in docs/ui-design.md.
import { authReady, completeLogin, invalidateCsrf } from "./auth.js";
import {
  NETWORK_ERROR, loginErrorMessage, parseAccount, parseRetryAfter, registerHref,
  safeReturnUrl, validateEmail, validatePassword
} from "./auth-utils.js";

const LOGIN_TIMEOUT_MS = 15000;
const FILL_MS = 950; // Keep equal to the fill-x animation in auth.css.
const CHECK_MS = 500; // How long "Signed in" stays visible when the hooks finish first.

const form = document.getElementById("login-form");
const email = document.getElementById("email");
const password = document.getElementById("password");
const emailError = document.getElementById("email-error");
const passwordError = document.getElementById("password-error");
const banner = document.getElementById("banner");
const submit = document.getElementById("submit");

const returnUrl = safeReturnUrl(new URLSearchParams(window.location.search).get("returnUrl"));
const prefersReducedMotion = () => window.matchMedia("(prefers-reduced-motion: reduce)").matches;
const wait = ms => new Promise(resolve => setTimeout(resolve, ms));
let busy = false;

// Staggered reveal-up of every line (.r). The stagger index is set from JS, never as an HTML attribute.
function reveal() {
  form.querySelectorAll(".r").forEach((line, index) => {
    line.classList.remove("done");
    line.style.setProperty("--i", index);
  });
  form.classList.add("play");
}

function shake(element) {
  element.classList.remove("shake");
  void element.offsetWidth; // Restarts the animation when the same field shakes again.
  element.classList.add("shake");
}

function setFieldError(input, box, message) {
  box.textContent = message;
  box.hidden = !message;
  if (message) input.setAttribute("aria-invalid", "true");
  else input.removeAttribute("aria-invalid");
}

function setBanner(message) {
  banner.textContent = message;
  banner.hidden = !message;
}

function setButton(text, ...classes) {
  submit.classList.remove("is-loading", "fill", "is-ok");
  submit.classList.add(...classes);
  submit.textContent = text;
}

function validate() {
  const emailMessage = validateEmail(email.value);
  const passwordMessage = validatePassword(password.value);
  setFieldError(email, emailError, emailMessage);
  setFieldError(password, passwordError, passwordMessage);
  if (emailMessage) shake(email);
  if (passwordMessage) shake(password);
  if (emailMessage) email.focus();
  else if (passwordMessage) password.focus();
  return !emailMessage && !passwordMessage;
}

function unlock() {
  busy = false;
  submit.disabled = false;
  setButton("Log in");
}

function startLockout(seconds) {
  let left = seconds;
  setButton(`Try again in ${left}s`);
  const timer = setInterval(() => {
    left -= 1;
    if (left > 0) {
      submit.textContent = `Try again in ${left}s`;
      return;
    }
    clearInterval(timer);
    unlock();
    setBanner("");
  }, 1000);
}

async function playSuccess() {
  if (!prefersReducedMotion()) {
    setButton("Signing you in…", "fill");
    await wait(FILL_MS);
  }
  setButton("Signed in", "is-ok");
  if (!prefersReducedMotion()) await wait(CHECK_MS);
}

async function succeed(account) {
  await Promise.all([playSuccess(), completeLogin(account)]);
  window.location.replace(returnUrl);
}

function fail(status, retryAfter) {
  setBanner(status === null ? NETWORK_ERROR : loginErrorMessage(status));
  if (status === 429) {
    startLockout(parseRetryAfter(retryAfter));
    return;
  }
  unlock();
  if (status === 401) {
    password.value = "";
    shake(email);
    shake(password);
    password.focus();
    return;
  }
  if (status === 400) invalidateCsrf();
  submit.focus();
}

async function logIn() {
  let response;
  try {
    response = await fetch("/api/auth/login", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Accept: "application/json",
        "X-CSRF-TOKEN": await window.StoreAuth.csrfToken()
      },
      body: JSON.stringify({ email: email.value.trim(), password: password.value }),
      signal: AbortSignal.timeout(LOGIN_TIMEOUT_MS)
    });
  } catch {
    fail(null);
    return;
  }
  if (!response.ok) {
    fail(response.status, response.headers.get("Retry-After"));
    return;
  }
  let account = null;
  try {
    account = parseAccount(await response.json());
  } catch {
    // Handled below, same as a response with the wrong shape.
  }
  if (account) await succeed(account);
  else fail(response.status);
}

form.addEventListener("submit", event => {
  event.preventDefault();
  if (busy) return;
  setBanner("");
  if (!validate()) return;
  busy = true;
  submit.disabled = true;
  setButton("Logging in…", "is-loading");
  logIn();
});

email.addEventListener("input", () => setFieldError(email, emailError, ""));
password.addEventListener("input", () => setFieldError(password, passwordError, ""));

// Spotlight position on the primary button, relative to the button.
submit.addEventListener("pointermove", event => {
  const box = submit.getBoundingClientRect();
  submit.style.setProperty("--mx", `${event.clientX - box.left}px`);
  submit.style.setProperty("--my", `${event.clientY - box.top}px`);
});

// When a line has finished, drop the mask clipping so focus rings are not cut off.
form.addEventListener("animationend", event => {
  if (event.target.classList.contains("ri")) event.target.parentElement.classList.add("done");
});

// A page restored by the Back button may be out of date, so run the signed-in check again.
window.addEventListener("pageshow", event => {
  if (event.persisted) window.location.reload();
});

document.getElementById("register-link").href = registerHref(returnUrl);

await authReady;
if (window.StoreAuth.getAccount()) {
  window.location.replace(returnUrl);
} else {
  form.hidden = false;
  reveal();
}
