// The states of the profile page (SCRUM-103) around its form. Plain createElement like RequestStates.js, so Node
// tests can render them.
import { createElement as h } from "react";
import { Link } from "react-router";
import { ErrorState, LoadingState } from "./RequestStates.js";

export const LOADING_LABEL = "Loading your profile…";
export const INCOMPLETE_MESSAGE = "Add your tax ID and home address before you pay.";
export const SAVED_MESSAGE = "Your changes are saved.";

/** Before the form: the loading line, or the error with "Try again". Nothing once the profile is there. */
export function ProfileLoad({ loading, error, onRetry }) {
  if (loading) return h(LoadingState, { label: LOADING_LABEL });
  return h(ErrorState, { error, onRetry, retrying: false });
}

/** The note for a legacy account without a tax ID or home address. A masked reveal line, number `index`. */
export function ProfileNotice({ incomplete, index }) {
  if (!incomplete) return null;
  return h("div", { className: "r", style: { "--i": index } },
    h("p", { id: "profile-notice", className: "ri profile-notice", role: "status" }, INCOMPLETE_MESSAGE));
}

/** After a successful save: a status line and, with a safe returnUrl, the link back (continueLink). */
export function SavedStatus({ saved, link, linkRef }) {
  if (!saved) return null;
  return h("p", { id: "profile-saved", className: "profile-saved", role: "status" },
    SAVED_MESSAGE,
    link ? " " : null,
    link ? h(Link, { id: "continue", ref: linkRef, to: link.href }, link.label) : null);
}
