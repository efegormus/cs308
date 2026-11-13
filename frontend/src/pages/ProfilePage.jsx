// The customer's profile page (SCRUM-103): name, tax ID and home address, which appear on invoices and deliveries.
// It reuses the login/register machinery: useAuthForm (button states, requests, failures), shake and revealLine,
// and the .auth-page styles. profile.css only lays out the two cards.
import { useCallback, useEffect, useRef, useState } from "react";
import { flushSync } from "react-dom";
import { useSearchParams } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { revealLine, shake, useAuthForm } from "../auth/useAuthForm.js";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import PurchaseAccess from "../components/PurchaseAccess.jsx";
import { ProfileLoad, ProfileNotice, SavedStatus } from "../components/ProfileStates.js";
import { safeReturnUrl } from "../lib/auth-utils.js";
import { fetchCustomerProfile, saveCustomerProfile } from "../lib/customer-profile-api.js";
import {
  PROFILE_FIELDS, continueLink, isProfileIncomplete, profileFieldErrors, profileRequestBody, profileValues,
  validateProfile
} from "../lib/profile-form.js";
import { useRequest } from "../lib/useRequest.js";
import "../styles/auth-textarea.css";
import "../styles/request-states.css";
import "../styles/profile.css";

const CONFLICT_MESSAGE = "Your profile was changed somewhere else at the same time. Please try again.";

// No maxLength: a pasted value that is too long gets a message instead of being cut without a word (as on register).
const CONTROL_PROPS = {
  name: { id: "name", label: "Full name", type: "text", autoComplete: "name" },
  taxId: { id: "tax-id", label: "Tax ID (TC kimlik no)", type: "text", autoComplete: "off", inputMode: "numeric" },
  homeAddress: { id: "home-address", label: "Home address", autoComplete: "street-address", rows: 3 }
};

function ProfileEditor({ account, returnUrl }) {
  const { csrfToken, refreshCsrf, expireSession, renameAccount } = useAuth();
  const form = useAuthForm({
    returnUrl, idleLabel: "Save changes", loadingLabel: "Saving…", fillLabel: "Saving…", okLabel: "Saved",
    redirectSignedIn: false
  });
  const read = useRequest();
  useSessionExpiry(read.error);
  const [profile, setProfile] = useState(null); // The last profile the server confirmed.
  const [values, setValues] = useState(null);
  const [errors, setErrors] = useState({});
  const [saved, setSaved] = useState(false);
  const controls = useRef({});
  const continueRef = useRef(null);
  const link = continueLink(returnUrl);

  const load = useCallback(async () => {
    const result = await read.run(({ signal }) => fetchCustomerProfile({ signal }));
    if (result.status === "success") {
      setProfile(result.data);
      setValues(profileValues(result.data));
    }
  }, [read.run]);
  useEffect(() => { void load(); return read.cancel; }, [load, read.cancel]);

  function updateField(field, value) {
    setValues(current => ({ ...current, [field]: value }));
    setErrors(current => ({ ...current, [field]: "" }));
    setSaved(false);
  }

  // Returns true if any field has a message.
  function showFieldErrors(found) {
    // Rendered first, so a screen reader hears the message when focus moves to the field.
    flushSync(() => setErrors(found));
    const invalid = PROFILE_FIELDS.filter(field => found[field]);
    invalid.forEach(field => shake(controls.current[field]));
    if (invalid.length > 0) controls.current[invalid[0]].focus();
    return invalid.length > 0;
  }

  async function handleSubmit(event) {
    event.preventDefault();
    if (!form.idle) return;
    form.setBanner("");
    setSaved(false);
    if (showFieldErrors(validateProfile(values))) return;
    form.startRequest();
    let updated;
    try {
      updated = await saveCustomerProfile(profileRequestBody(values), { csrfToken, refreshCsrf, signal: form.requestSignal() });
    } catch (failure) {
      if (failure?.kind === "validation") {
        const found = profileFieldErrors(failure.errors);
        form.unlock();
        if (!showFieldErrors(found)) form.fail(400, null);
        return;
      }
      if (failure?.status === 401) {
        expireSession(); // The route guard sends the visitor to the login page, which brings them back here.
        return;
      }
      if (failure?.status === 409) {
        form.fail(409, null); // Unlocks and focuses the button; the message below replaces the general one.
        form.setBanner(CONFLICT_MESSAGE);
        return;
      }
      // A network error or the time limit has no status. An unreadable answer gets the general message.
      const status = failure?.kind === "invalid-response" ? 500 : (failure?.status ?? null);
      form.fail(status, failure?.headers?.get?.("Retry-After") ?? null);
      return;
    }
    setProfile(updated);
    setValues(profileValues(updated));
    renameAccount(account.id, updated.name);
    flushSync(() => setSaved(true));
    continueRef.current?.focus();
    await form.finishSave();
  }

  if (!values) {
    return <ProfileLoad loading={read.status === "idle" || read.status === "loading"} error={read.error} onRetry={load} />;
  }

  const incomplete = isProfileIncomplete(profile);
  const first = incomplete ? 3 : 2; // The reveal line of the first card's heading.
  const field = (name, line) => {
    const { id, label, ...controlProps } = CONTROL_PROPS[name];
    const Control = name === "homeAddress" ? "textarea" : "input";
    const errorId = `${id}-error`;
    return (
      <div className="r" style={revealLine(line)}>
        <div className="ri field">
          <label htmlFor={id}>{label}</label>
          <Control
            id={id}
            ref={element => { controls.current[name] = element; }}
            name={name}
            value={values[name]}
            aria-required="true"
            aria-invalid={errors[name] ? "true" : undefined}
            aria-describedby={errors[name] ? errorId : undefined}
            onChange={event => updateField(name, event.target.value)}
            {...controlProps}
          />
          {errors[name] && <div id={errorId} className="field-error" role="alert">{errors[name]}</div>}
        </div>
      </div>
    );
  };

  return (
    <form id="profile-form" {...form.formProps} className="profile-form play" onSubmit={handleSubmit}>
      <h1 className="r" style={revealLine(0)}><span className="ri">My profile</span></h1>
      <p className="lead r" style={revealLine(1)}>
        <span className="ri">These details appear on your invoice and delivery.</span>
      </p>
      <ProfileNotice incomplete={incomplete} index={2} />
      {form.banner && <div id="banner" className="banner" role="alert">{form.banner}</div>}
      <div className="profile-cards">
        <section className="profile-card" style={{ "--card": 0 }} aria-labelledby="account-heading">
          <div className="stack">
            <h2 className="r" style={revealLine(first)}><span className="ri" id="account-heading">Account</span></h2>
            {field("name", first + 1)}
            <div className="r" style={revealLine(first + 2)}>
              <div className="ri field">
                <label htmlFor="email">Email</label>
                <input id="email" type="email" value={profile.email} readOnly aria-describedby="email-note" />
                <div id="email-note" className="field-note">Email cannot be changed here.</div>
              </div>
            </div>
          </div>
        </section>
        <section className="profile-card" style={{ "--card": 1 }} aria-labelledby="invoice-heading">
          <div className="stack">
            <h2 className="r" style={revealLine(first + 3)}><span className="ri" id="invoice-heading">Invoice and delivery</span></h2>
            {field("taxId", first + 4)}
            {field("homeAddress", first + 5)}
          </div>
        </section>
      </div>
      <div className="r profile-actions" style={revealLine(first + 6)}>
        <div className="ri"><button id="submit" {...form.buttonProps} /></div>
      </div>
      <SavedStatus saved={saved} link={link} linkRef={continueRef} />
    </form>
  );
}

// /profile, for customers. A guest is sent to the login page with a returnUrl back here; other roles get the
// role message, as on /dashboard. ?returnUrl= (safeReturnUrl) is where "Continue" leads after a save.
export default function ProfilePage() {
  const [searchParams] = useSearchParams();
  const returnUrl = safeReturnUrl(searchParams.get("returnUrl"));
  return (
    <div className="route-page auth-page profile-page">
      <title>My profile | Online Store</title>
      <main id="main-content">
        <PurchaseAccess role="Customer">
          {account => <ProfileEditor key={account.id} account={account} returnUrl={returnUrl} />}
        </PurchaseAccess>
      </main>
    </div>
  );
}
