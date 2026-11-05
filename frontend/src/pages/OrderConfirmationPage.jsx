import { useSearchParams } from "react-router";

// Skeleton. The receipt is still only in wwwroot/order-confirmation.html and js/order-confirmation.js.
export default function OrderConfirmationPage() {
  const [searchParams] = useSearchParams();
  // /order-confirmation?orderId=5, validated as in js/order-confirmation.js.
  const rawId = searchParams.get("orderId");
  const id = /^[1-9]\d*$/.test(rawId ?? "") ? rawId : null;

  return (
    <div className="route-page catalog-page purchase-page">
      <title>Order confirmation | Online Store</title>
      <main id="main-content" className="purchase-layout">
        <h1>{id === null ? "Order confirmation" : `Order #${id}`}</h1>
      </main>
    </div>
  );
}
