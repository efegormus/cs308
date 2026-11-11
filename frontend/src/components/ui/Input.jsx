import { useId } from "react";
import "./Input.css";

// Text input with its label and an optional field error (DESIGN.md, Inputs and forms and Validation errors;
// token text-input). With an error the input gets aria-invalid and points to the message with aria-describedby.
// hideLabel keeps the label for screen readers but hides it on screen, for a field whose purpose the layout already
// shows, such as the header search. Every other prop goes to the <input>.
export default function Input({ label, hideLabel = false, error, id, type = "text", className = "", "aria-describedby": describedBy, ...props }) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const describedByIds = [describedBy, error ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className={className ? `ui-field ${className}` : "ui-field"}>
      <label className={hideLabel ? "ui-field-label ui-visually-hidden" : "ui-field-label"} htmlFor={inputId}>{label}</label>
      <input {...props} id={inputId} type={type} className="ui-input"
        aria-invalid={error ? "true" : undefined} aria-describedby={describedByIds} />
      {error && <p id={errorId} className="ui-field-error">{error}</p>}
    </div>
  );
}
