import { formatPrice } from "../lib/product-display.js";

export default function PurchaseLines({ items }) {
  return <ul id="lines" className="purchase-lines">{items.map(item => (
    <li key={item.productId} className="purchase-line">
      <span>{item.name} · {item.quantity} × {formatPrice(item.unitPrice)}</span>
      <strong>{formatPrice(item.total)}</strong>
    </li>
  ))}</ul>;
}
