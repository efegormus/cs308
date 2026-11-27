import { useEffect, useRef, useState } from "react";
import { HINT_START, HINT_TIMING, hintStep, hintText } from "../lib/search-hints.js";
import Button from "./ui/Button.jsx";
import Input from "./ui/Input.jsx";

// The same limit as GET /api/products (ProductEndpoints.MaxSearchLength).
const MAX_SEARCH_LENGTH = 100;
const REDUCED_MOTION = "(prefers-reduced-motion: reduce)";

function useReducedMotion() {
  const [reduced, setReduced] = useState(() => typeof window.matchMedia === "function" && window.matchMedia(REDUCED_MOTION).matches);
  useEffect(() => {
    if (typeof window.matchMedia !== "function") return undefined;
    const query = window.matchMedia(REDUCED_MOTION);
    const onChange = () => setReduced(query.matches);
    query.addEventListener("change", onChange);
    return () => query.removeEventListener("change", onChange);
  }, []);
  return reduced;
}

// The header search (SCRUM-164, DESIGN.md Header > Search). Submitting works as before: the parent opens
// /products?search=<text>. While the field is empty and not focused, a hint types search terms over it ("Search
// iPhone 16"); it is aria-hidden and the real placeholder stays for screen readers. Focus or text stops and hides it;
// it goes on from where it was when the field is empty and blurred again. With reduced motion it shows one term and
// does not move. Ctrl+K (Cmd+K on a Mac) focuses the field from anywhere on the page.
export default function HeaderSearch({ value, onChange, onSubmit, terms }) {
  const inputRef = useRef(null);
  const step = useRef(HINT_START);
  const [focused, setFocused] = useState(false);
  const [hint, setHint] = useState(HINT_START);
  const reduced = useReducedMotion();
  const showHint = !focused && value === "" && terms.length > 0;

  useEffect(() => {
    if (!showHint || reduced) return undefined;
    let timer = 0;
    const tick = () => {
      const next = hintStep(step.current, terms);
      step.current = next.state;
      setHint(next.state);
      if (next.delay !== null) timer = window.setTimeout(tick, next.delay);
    };
    timer = window.setTimeout(tick, HINT_TIMING.type);
    return () => window.clearTimeout(timer);
  }, [showHint, reduced, terms]);

  useEffect(() => {
    const onKeyDown = event => {
      if (event.key?.toLowerCase() !== "k" || !(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return;
      event.preventDefault();
      inputRef.current?.focus();
      inputRef.current?.select();
    };
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, []);

  const text = reduced ? terms[0] ?? "" : hintText(hint, terms);
  const classes = ["site-search", showHint ? "has-hint" : null, focused ? "is-focused" : null].filter(Boolean).join(" ");

  return (
    <form className={classes} role="search" action="/products" onSubmit={onSubmit}>
      <div className="site-search-box">
        <Input ref={inputRef} id="site-search" className="site-search-field" label="Search products" hideLabel type="search"
          name="search" placeholder="Search products" autoComplete="off" maxLength={MAX_SEARCH_LENGTH}
          aria-keyshortcuts="Control+K Meta+K" value={value} onChange={event => onChange(event.target.value)}
          onFocus={() => setFocused(true)} onBlur={() => setFocused(false)} />
        {showHint && (
          <span className="site-search-hint" aria-hidden="true">
            <span className="site-search-hint-label">Search</span>{" "}
            <span className="site-search-hint-term">{text}</span>
            {!reduced && <span className="site-search-caret" />}
          </span>
        )}
        {!focused && value === "" && <kbd className="site-search-kbd" aria-hidden="true">Ctrl K</kbd>}
      </div>
      <Button type="submit" className="site-search-button" aria-label="Search">
        <svg viewBox="0 0 24 24" width="20" height="20" aria-hidden="true" focusable="false">
          <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
          <path d="M16.5 16.5L21 21" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
        </svg>
      </Button>
    </form>
  );
}
