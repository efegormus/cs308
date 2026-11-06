import { useRef, useState } from "react";
import { flushSync } from "react-dom";
import { Link, useSearchParams } from "react-router";
import { revealLine, shake, useAuthForm } from "../auth/useAuthForm.js";
import {
  loginErrorMessage, parseAccount, registerHref, safeReturnUrl, validateEmail, validatePassword
} from "../lib/auth-utils.js";

// The password policy is not checked here, so the page does not reveal it (docs/authentication.md).
export default function LoginPage() {
  const [searchParams] = useSearchParams();
  const returnUrl = safeReturnUrl(searchParams.get("returnUrl"));
  const form = useAuthForm({ returnUrl, idleLabel: "Log in", loadingLabel: "Logging in…" });
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [emailError, setEmailError] = useState("");
  const [passwordError, setPasswordError] = useState("");
  const emailRef = useRef(null);
  const passwordRef = useRef(null);

  function validate() {
    const emailMessage = validateEmail(email);
    const passwordMessage = validatePassword(password);
    // Rendered first, so a screen reader hears the message when focus moves to the field.
    flushSync(() => {
      setEmailError(emailMessage);
      setPasswordError(passwordMessage);
    });
    if (emailMessage) shake(emailRef.current);
    if (passwordMessage) shake(passwordRef.current);
    if (emailMessage) emailRef.current.focus();
    else if (passwordMessage) passwordRef.current.focus();
    return !emailMessage && !passwordMessage;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!form.idle) return;
    form.setBanner("");
    if (!validate()) return;
    form.startRequest();
    const { ok, status, retryAfter, data } = await form.post("/api/auth/login", { email: email.trim(), password });
    const account = ok ? parseAccount(data) : null;
    if (account) {
      await form.finishLogin(account);
      return;
    }
    if (status === 401) {
      // The same message for a wrong password and for an unknown account.
      form.setBanner(loginErrorMessage(status));
      form.unlock();
      setPassword("");
      shake(emailRef.current);
      shake(passwordRef.current);
      passwordRef.current.focus();
      return;
    }
    form.fail(status, retryAfter);
  }

  return (
    <div className="route-page auth-page">
      <title>Log in | Online Store</title>
      <main id="main-content">
        {form.showForm && (
          <form id="login-form" {...form.formProps} onSubmit={handleSubmit}>
            <h1 className="r" style={revealLine(0)}><span className="ri">Log in</span></h1>
            <p className="lead r" style={revealLine(1)}>
              <span className="ri">Welcome back. Log in to see your cart and your orders.</span>
            </p>
            {form.banner && <div id="banner" className="banner" role="alert">{form.banner}</div>}
            <div className="stack">
              <div className="r" style={revealLine(2)}>
                <div className="ri field">
                  <label htmlFor="email">Email</label>
                  <input
                    id="email" ref={emailRef} name="email" type="email" autoComplete="email" maxLength={254}
                    value={email}
                    aria-invalid={emailError ? "true" : undefined}
                    aria-describedby={emailError ? "email-error" : undefined}
                    onChange={event => {
                      setEmail(event.target.value);
                      setEmailError("");
                    }}
                  />
                  {emailError && <div id="email-error" className="field-error">{emailError}</div>}
                </div>
              </div>
              <div className="r" style={revealLine(3)}>
                <div className="ri field">
                  <label htmlFor="password">Password</label>
                  <input
                    id="password" ref={passwordRef} name="password" type="password" autoComplete="current-password"
                    maxLength={128}
                    value={password}
                    aria-invalid={passwordError ? "true" : undefined}
                    aria-describedby={passwordError ? "password-error" : undefined}
                    onChange={event => {
                      setPassword(event.target.value);
                      setPasswordError("");
                    }}
                  />
                  {passwordError && <div id="password-error" className="field-error">{passwordError}</div>}
                </div>
              </div>
              <div className="r" style={revealLine(4)}>
                <div className="ri"><button id="submit" {...form.buttonProps} /></div>
              </div>
            </div>
            <p className="switch r" style={revealLine(5)}>
              <span className="ri">New here? <Link id="register-link" to={registerHref(returnUrl)}>Create an account</Link></span>
            </p>
          </form>
        )}
      </main>
    </div>
  );
}
