import "./Button.css";

// Primary button (DESIGN.md, Components > Buttons; token button-primary): 44px high, 4px corners,
// Poppins 16px 500, never uppercase. Every other prop goes to the <button>.
export default function Button({ type = "button", className = "", ...props }) {
  return <button type={type} className={className ? `ui-button ${className}` : "ui-button"} {...props} />;
}
