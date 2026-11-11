import { useId } from "react";
import "./Input.css";

// Text input with its label and an optional field error (DESIGN.md, Inputs and forms and Validation errors;
// token text-input). With an error the input gets aria-invalid and points to the message with aria-describedby.
// Every other prop goes to the <input>.
export default function Input({ label, error, id, type = "text", className = "", "aria-describedby": describedBy, ...props }) {
  const generatedId = useId();
  const inputId = id ?? generatedId;
  const errorId = `${inputId}-error`;
  const describedByIds = [describedBy, error ? errorId : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className={className ? `ui-field ${className}` : "ui-field"}>
      <label className="ui-field-label" htmlFor={inputId}>{label}</label>
      <input {...props} id={inputId} type={type} className="ui-input"
        aria-invalid={error ? "true" : undefined} aria-describedby={describedByIds} />
      {error && <p id={errorId} className="ui-field-error">{error}</p>}
    </div>
  );
}
