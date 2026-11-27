import "./Button.css";

// Primary button (DESIGN.md, Components > Buttons; token button-primary): 44px high, 4px corners,
// Poppins 16px 500, never uppercase. variant="secondary" is token button-secondary: --surface fill, --link text and a
// 1px --field-line edge, for the actions next to the main one (Remove, Refresh, Load more). A link styled as a button
// uses the same classes: "ui-button" or "ui-button ui-button--secondary". Every other prop goes to the <button>.
export default function Button({ type = "button", variant = "primary", className = "", ...props }) {
  const classes = ["ui-button", variant === "secondary" ? "ui-button--secondary" : null, className].filter(Boolean).join(" ");
  return <button type={type} className={classes} {...props} />;
}
