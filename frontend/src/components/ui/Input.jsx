import { useId } from "react";
import "./Input.css";

// Text input with its label and an optional field error (DESIGN.md, Inputs and forms and Validation errors;
// token text-input). With an error the input gets aria-invalid and points to the message with aria-describedby.
// hideLabel keeps the label for screen readers but hides it on screen, for a field whose purpose the layout already
// shows, such as the header search. multiline renders a <textarea> (at least 88px high) instead of an <input>.
// hint is a note under the field that the field points to as well (id "<id>-note"); errorRole gives the error message
// a role, such as "alert" on the profile page. select renders a <select> with the children as its options, in the same
// 44px frame. Every other prop goes to the <input>, <textarea> or <select>.
export default function Input({
  label, hideLabel = false, error, hint, errorRole, multiline = false, select = false, id, type = "text", className = "",
  "aria-describedby": describedBy, ...props
}) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const hintId = `${inputId}-note`;
  const describedByIds = [describedBy, hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;
  const Control = select ? "select" : multiline ? "textarea" : "input";

  return (
    <div className={className ? `ui-field ${className}` : "ui-field"}>
      <label className={hideLabel ? "ui-field-label ui-visually-hidden" : "ui-field-label"} htmlFor={inputId}>{label}</label>
      <Control {...props} id={inputId} type={select || multiline ? undefined : type}
        className={multiline ? "ui-input ui-input--multiline" : select ? "ui-input ui-input--select" : "ui-input"}
        aria-invalid={error ? "true" : undefined} aria-describedby={describedByIds} />
      {hint && <p id={hintId} className="ui-field-hint">{hint}</p>}
      {error && <p id={errorId} className="ui-field-error" role={errorRole}>{error}</p>}
    </div>
  );
}
