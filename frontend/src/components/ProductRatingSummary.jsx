import { useCallback, useEffect } from "react";
import { ErrorState, LoadingState } from "./RequestStates.js";
import { fetchProductReviews } from "../lib/reviews-api.js";
import { useRequest } from "../lib/useRequest.js";

// SCRUM-80 supplies the public average; SCRUM-137 adds the review form and comment section.
export default function ProductRatingSummary({ productId }) {
  const read = useRequest();
  const load = useCallback(() => read.run(({ signal }) => fetchProductReviews(productId, { signal })), [productId, read.run]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  return <section aria-label="Customer ratings" aria-live="polite">
    {read.status === "loading" && <LoadingState label="Loading ratings…" />}
    <ErrorState error={read.error} onRetry={load} />
    {read.status === "success" && (read.data.ratingCount === 0 ? <p>No ratings yet.</p>
      : <p>Average rating: <strong>{read.data.averageRating.toFixed(1)} / 5</strong>
        {" · "}{read.data.ratingCount} {read.data.ratingCount === 1 ? "rating" : "ratings"}</p>)}
  </section>;
}
