import "./Button.css";

// Primary button (DESIGN.md, Components > Buttons; token button-primary): 44px high, 4px corners,
// Poppins 16px 500, never uppercase. variant="secondary" is token button-secondary: --surface fill, --link text and a
// 1px --field-line edge, for the actions next to the main one (Remove, Refresh, Load more). A link styled as a button
// uses the same classes: "ui-button" or "ui-button ui-button--secondary". unavailable marks an action that cannot be
// taken at all, such as "Out of stock": the button is disabled and gray (DESIGN.md, open question 17, option A),
// while a busy button ("Adding…") keeps the 0.7 opacity. Every other prop goes to the <button>.
export default function Button({ type = "button", variant = "primary", unavailable = false, className = "", disabled, ...props }) {
  const classes = ["ui-button", variant === "secondary" ? "ui-button--secondary" : null,
    unavailable ? "ui-button--unavailable" : null, className].filter(Boolean).join(" ");
  return <button type={type} className={classes} disabled={unavailable || disabled} {...props} />;
}
