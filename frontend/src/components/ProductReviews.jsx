import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../auth/useSessionExpiry.js";
import { fetchCustomerReview, fetchProductReviews, reviewInput, submitReview } from "../lib/reviews-api.js";
import { useRequest } from "../lib/useRequest.js";
import { starRow } from "../lib/stars.js";
import { ErrorState, LoadingState } from "./RequestStates.js";
import Button from "./ui/Button.jsx";
import "../styles/reviews.css";

function Reviews({ productId, account, authReady }) {
  const { csrfToken, refreshCsrf } = useAuth();
  const publicRead = useRequest(); const privateRead = useRequest(); const mutation = useRequest();
  useSessionExpiry(privateRead.error, mutation.error);
  const [comments, setComments] = useState([]); const [cursor, setCursor] = useState(null);
  const [rating, setRating] = useState(""); const [comment, setComment] = useState("");
  const [notice, setNotice] = useState(""); const [validation, setValidation] = useState("");
  const locked = useRef(false);
  const load = useCallback(async (afterId = 0) => {
    const result = await publicRead.run(({ signal }) => fetchProductReviews(productId, { signal, afterId }));
    if (result.status === "success") {
      setComments(previous => afterId === 0 ? result.data.comments : [...previous, ...result.data.comments]);
      setCursor(result.data.nextAfterId);
    }
  }, [productId, publicRead.run]);
  const loadOwn = useCallback(() => privateRead.run(({ signal }) => fetchCustomerReview(productId, { signal })), [productId, privateRead.run]);
  useEffect(() => { void load(); return () => publicRead.cancel(); }, [load, publicRead.cancel]);
  useEffect(() => {
    if (authReady && account?.role === "Customer") void loadOwn();
    return () => privateRead.cancel();
  }, [account?.id, account?.role, authReady, loadOwn, privateRead.cancel]);
  async function send(event) {
    event.preventDefault(); if (locked.current) return;
    const input = reviewInput(rating, comment, privateRead.data);
    if (!input) { setValidation("Choose a rating or enter a comment of at most 2000 characters."); return; }
    locked.current = true; setValidation(""); setNotice("");
    const result = await mutation.run(async ({ signal }) => submitReview(productId, input, await csrfToken(), { signal }));
    if (result.status === "success") {
      setRating(""); setComment("");
      setNotice(input.comment ? "Rating saved if provided. Your comment is waiting for approval." : "Your rating is now visible.");
      await Promise.all([loadOwn(), load()]);
    } else if (result.error?.status === 400) await refreshCsrf();
    else if (result.error?.status === 409) await loadOwn();
    locked.current = false;
  }
  const own = privateRead.status === "success" ? privateRead.data : null;
  const busy = mutation.status === "loading";
  return <section id="reviews" className="product-reviews" aria-labelledby="reviews-title">
    <h2 id="reviews-title">Customer reviews</h2>
    {publicRead.status === "loading" && <LoadingState label="Loading reviews…" />}
    <ErrorState error={publicRead.error} onRetry={() => load()} />
    {publicRead.data && <p className="review-summary" aria-live="polite">{publicRead.data.ratingCount === 0 ? "No ratings yet."
      : <><span className="review-stars" aria-hidden="true">{starRow(publicRead.data.averageRating)}</span>
        Average rating: <strong>{publicRead.data.averageRating.toFixed(1)} / 5</strong>{" · "}{publicRead.data.ratingCount} ratings</>}</p>}
    <ul className="review-comments">{comments.map(c => <li key={c.id} className="review-comment">
      <strong>{c.authorName}</strong><span className="review-date"> · {new Date(c.createdAt).toLocaleDateString()}</span><p>{c.body}</p>
    </li>)}</ul>
    {publicRead.status === "success" && comments.length === 0 && <p>No approved comments yet.</p>}
    {cursor !== null && <Button disabled={publicRead.status === "loading"} onClick={() => load(cursor)}>More comments</Button>}
    {authReady && !account && <p><Link to={`/login?returnUrl=${encodeURIComponent(`/product?id=${productId}#reviews`)}`}>Log in</Link> to review a delivered purchase.</p>}
    {account?.role === "Customer" && <>
      {privateRead.status === "loading" && <LoadingState label="Checking your delivered purchases…" />}
      <ErrorState error={privateRead.error} onRetry={loadOwn} />
      <ErrorState error={mutation.error} />
      {notice && <p role="status">{notice}</p>}
      {own?.rating !== null && own?.rating !== undefined && <p className="own-rating"><span className="review-stars" aria-hidden="true">{starRow(own.rating)}</span>Your rating: {own.rating} / 5</p>}
      {own?.comment && <div className="own-comment"><p>Your comment: {own.comment.body}</p>
        <p className={`review-status review-status--${own.comment.status.toLowerCase()}`}>Status: {own.comment.status === "Pending" ? "Waiting for approval" : own.comment.status}</p></div>}
      {own && !own.eligible && <p>You can rate or comment after your purchase has been delivered.</p>}
      {own && (own.canRate || own.canComment) && <form onSubmit={send} className="review-form">
        <h3>Rate / Comment</h3>
        {own.canRate && <><label htmlFor="review-rating">Rating</label><select id="review-rating" value={rating} onChange={e => setRating(e.target.value)} disabled={busy}>
          <option value="">Choose stars (optional)</option>{[1,2,3,4,5].map(n => <option key={n} value={n}>{n} {n === 1 ? "star" : "stars"}</option>)}
        </select></>}
        {own.canComment && <><label htmlFor="review-comment">Comment (optional)</label><textarea id="review-comment" maxLength={2000} value={comment} onChange={e => setComment(e.target.value)} disabled={busy} /></>}
        <p>Ratings appear immediately. Comments appear publicly after product manager approval.</p>
        {validation && <p role="alert">{validation}</p>}
        <Button type="submit" className="review-submit" disabled={busy}>{busy ? "Submitting…" : "Submit review"}</Button>
      </form>}
    </>}
  </section>;
}

export default function ProductReviews({ productId }) {
  const { account, authReady } = useAuth();
  return <Reviews key={`${productId}:${account?.id ?? "guest"}`} productId={productId} account={account} authReady={authReady} />;
}
