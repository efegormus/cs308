import "./Card.css";

// Card frame. By default this is DESIGN.md's card-product (Components > Product card on /products): 16px corners,
// 20px padding, --shadow-product. variant="form" is the form card (token form-card): 1px --line border, --shadow,
// 10px corners, padding 36px 32px (28px 20px up to 480px), as on login, register and profile. `as` picks the element
// (div by default, for example "article" or "form"); the card is a flex column, so a child with margin-top: auto sits
// at the bottom.
export default function Card({ as: Element = "div", variant = "product", className = "", ...props }) {
  const classes = ["ui-card", variant === "form" ? "ui-card--form" : null, className].filter(Boolean).join(" ");
  return <Element className={classes} {...props} />;
}
