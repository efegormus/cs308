import "./Card.css";

// Card frame. This is DESIGN.md's card-product (Components > Product card on /products): 16px corners, 20px padding,
// --shadow-product. It is not the 10px form card. `as` picks the element (div by default, for example "article");
// the card is a flex column, so a child with margin-top: auto sits at the bottom.
export default function Card({ as: Element = "div", className = "", ...props }) {
  return <Element className={className ? `ui-card ${className}` : "ui-card"} {...props} />;
}
