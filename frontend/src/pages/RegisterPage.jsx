import { useRef, useState } from "react";
import { flushSync } from "react-dom";
import { Link, useSearchParams } from "react-router";
import { revealLine, shake, useAuthForm } from "../auth/useAuthForm.js";
import {
  REGISTER_FIELDS, loginHref, parseAccount, registerFieldErrors, safeReturnUrl, validateRegistration
} from "../lib/auth-utils.js";
import "../styles/auth-textarea.css";

const CONFLICT_MESSAGE = "An account with this email already exists.";
const SIGN_IN_FAILED_MESSAGE = "Your account was created, but we could not sign you in. Please log in.";

// No maxLength: a pasted value that is too long gets a message instead of being cut without a word.
const CONTROL_PROPS = {
  name: { id: "name", label: "Full name", type: "text", autoComplete: "name" },
  email: { id: "email", label: "Email", type: "email", autoComplete: "email" },
  password: { id: "password", label: "Password", type: "password", autoComplete: "new-password" },
  confirmPassword: { id: "confirm-password", label: "Confirm password", type: "password", autoComplete: "new-password" },
  taxId: { id: "tax-id", label: "Tax ID (TC kimlik no)", type: "text", autoComplete: "off", inputMode: "numeric" },
  homeAddress: { id: "home-address", label: "Home address", autoComplete: "street-address", rows: 3 }
};

const EMPTY_VALUES = Object.fromEntries(REGISTER_FIELDS.map(field => [field, ""]));

export default function RegisterPage() {
  const [searchParams] = useSearchParams();
  const returnUrl = safeReturnUrl(searchParams.get("returnUrl"));
  const form = useAuthForm({ returnUrl, idleLabel: "Create account", loadingLabel: "Creating account…" });
  const [values, setValues] = useState(EMPTY_VALUES);
  const [errors, setErrors] = useState({});
  const controls = useRef({});
  const loginLink = useRef(null);

  function updateField(field, value) {
    setValues(current => ({ ...current, [field]: value }));
    setErrors(current => ({ ...current, [field]: "" }));
  }

  // Returns true if any field has a message.
  function showFieldErrors(found) {
    // Rendered first, so a screen reader hears the message when focus moves to the field.
    flushSync(() => setErrors(found));
    const invalid = REGISTER_FIELDS.filter(field => found[field]);
    invalid.forEach(field => shake(controls.current[field]));
    if (invalid.length > 0) controls.current[invalid[0]].focus();
    return invalid.length > 0;
  }

  // POST /api/auth/register does not open a session, so the new account logs in straight away.
  async function signIn(email) {
    const { ok, data } = await form.post("/api/auth/login", { email, password: values.password });
    const account = ok ? parseAccount(data) : null;
    if (account) {
      await form.finishLogin(account);
      return;
    }
    // Sending the form again would only answer 409, so this points to the login page instead.
    form.setBanner(SIGN_IN_FAILED_MESSAGE);
    form.unlock();
    loginLink.current?.focus();
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!form.idle) return;
    form.setBanner("");
    if (showFieldErrors(validateRegistration(values))) return;
    form.startRequest();
    const email = values.email.trim();
    // Only these five: the API rejects unknown properties, and confirmPassword never leaves the page.
    const { status, retryAfter, data } = await form.post("/api/auth/register", {
      name: values.name.trim(),
      email,
      password: values.password,
      taxId: values.taxId.trim(),
      homeAddress: values.homeAddress.trim()
    });
    if (status === 201) {
      await signIn(email);
      return;
    }
    const fieldErrors = status === 400 ? registerFieldErrors(data) : {};
    if (Object.keys(fieldErrors).length > 0) {
      form.unlock();
      showFieldErrors(fieldErrors);
      return;
    }
    if (status === 409) {
      form.setBanner(CONFLICT_MESSAGE);
      form.unlock();
      shake(controls.current.email);
      controls.current.email.focus();
      return;
    }
    form.fail(status, retryAfter);
  }

  return (
    <div className="route-page auth-page">
      <title>Create account | Online Store</title>
      <main id="main-content">
        {form.showForm && (
          <form id="register-form" {...form.formProps} onSubmit={handleSubmit}>
            <h1 className="r" style={revealLine(0)}><span className="ri">Create account</span></h1>
            <p className="lead r" style={revealLine(1)}>
              <span className="ri">Create an account to keep your cart and follow your orders.</span>
            </p>
            {form.banner && <div id="banner" className="banner" role="alert">{form.banner}</div>}
            <div className="stack">
              {REGISTER_FIELDS.map((field, index) => {
                const { id, label, ...controlProps } = CONTROL_PROPS[field];
                const Control = field === "homeAddress" ? "textarea" : "input";
                const errorId = `${id}-error`;
                return (
                  <div className="r" style={revealLine(index + 2)} key={field}>
                    <div className="ri field">
                      <label htmlFor={id}>{label}</label>
                      <Control
                        id={id}
                        ref={element => { controls.current[field] = element; }}
                        name={field}
                        value={values[field]}
                        aria-invalid={errors[field] ? "true" : undefined}
                        aria-describedby={errors[field] ? errorId : undefined}
                        onChange={event => updateField(field, event.target.value)}
                        {...controlProps}
                      />
                      {errors[field] && <div id={errorId} className="field-error">{errors[field]}</div>}
                    </div>
                  </div>
                );
              })}
              <div className="r" style={revealLine(REGISTER_FIELDS.length + 2)}>
                <div className="ri"><button id="submit" {...form.buttonProps} /></div>
              </div>
            </div>
            <p className="switch r" style={revealLine(REGISTER_FIELDS.length + 3)}>
              <span className="ri">
                Already have an account? <Link ref={loginLink} to={loginHref({ pathname: returnUrl })}>Log in</Link>
              </span>
            </p>
          </form>
        )}
      </main>
    </div>
  );
}
