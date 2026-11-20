import { useCallback, useEffect, useRef, useState } from "react";
import { Link } from "react-router";
import { useAuth } from "../../auth/AuthProvider.jsx";
import { useSessionExpiry } from "../../auth/useSessionExpiry.js";
import { fetchModeration, moderateComment } from "../../lib/reviews-api.js";
import { useRequest } from "../../lib/useRequest.js";
import { ErrorState } from "../RequestStates.js";
import Table from "../ui/Table.js";
import Button from "../ui/Button.jsx";

export default function CommentModeration({ onChanged }) {
  const { csrfToken, refreshCsrf } = useAuth();
  const read = useRequest(); const mutation = useRequest(); useSessionExpiry(read.error, mutation.error);
  const [items, setItems] = useState([]); const [cursor, setCursor] = useState(null); const [notice, setNotice] = useState("");
  const busy = useRef(false);
  const load = useCallback(async (afterId = 0) => {
    mutation.reset();
    const result = await read.run(({ signal }) => fetchModeration(afterId, { signal }));
    if (result.status === "success") {
      setItems(previous => afterId === 0 ? result.data.items : [...previous, ...result.data.items]); setCursor(result.data.nextAfterId);
    } else if ([401,403].includes(result.error?.status)) setItems([]);
  }, [read.run, mutation.reset]);
  useEffect(() => { void load(); return () => read.cancel(); }, [load, read.cancel]);
  async function decide(comment, status) {
    if (busy.current) return; busy.current = true; setNotice("");
    const result = await mutation.run(async ({ signal }) => moderateComment(comment, status, await csrfToken(), { signal }));
    if (result.status === "success") {
      setItems(previous => previous.filter(item => item.id !== comment.id)); setNotice(`Comment #${comment.id} ${status.toLowerCase()}.`);
      void onChanged?.();
    } else if ([401,403].includes(result.error?.status)) setItems([]);
    else if (result.error?.status === 400) await refreshCsrf();
    busy.current = false;
  }
  const disabled = read.status === "loading" || mutation.status === "loading" || !!mutation.error;
  const columns = [
    { key: "productName", header: "Product", rowHeader: true, render: row => <Link to={`/product?id=${row.productId}#reviews`}>{row.productName}</Link> },
    { key: "customerName", header: "Customer" },
    { key: "rating", header: "Rating", render: row => row.rating === null ? "No rating" : `${row.rating} / 5` },
    { key: "body", header: "Comment" },
    { key: "createdAt", header: "Written", render: row => new Date(row.createdAt).toLocaleDateString() },
    { key: "actions", header: "Decision", render: row => <div className="review-actions">
      <Button disabled={disabled} onClick={() => decide(row, "Approved")}>Approve</Button>
      <Button disabled={disabled} onClick={() => decide(row, "Rejected")}>Reject</Button>
    </div> }
  ];
  return <>
    <header className="admin-section-head"><h1>Reviews</h1><p>Approve comments for publication or reject them. Ratings are already public.</p></header>
    <ErrorState error={read.error} onRetry={() => load()} /><ErrorState error={mutation.error} onRetry={() => load()} />
    {notice && <p role="status">{notice}</p>}
    <Button onClick={() => load()} disabled={read.status === "loading" || mutation.status === "loading"}>Refresh comments</Button>
    <Table caption="Pending comments" columns={columns} rows={items} loading={read.status === "loading" && items.length === 0}
      loadingLabel="Loading pending comments…" emptyText="No comment is waiting for approval." />
    {cursor !== null && <Button disabled={disabled} onClick={() => load(cursor)}>More pending comments</Button>}
  </>;
}
