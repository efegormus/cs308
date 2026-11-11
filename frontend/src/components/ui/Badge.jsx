import "./Badge.css";

// Discount badge (DESIGN.md, Components > Discount badge; token badge-sale), for example "−20%" next to the price
// or on the image corner. Every other prop goes to the <span>.
export default function Badge({ className = "", ...props }) {
  return <span className={className ? `ui-badge ${className}` : "ui-badge"} {...props} />;
}
