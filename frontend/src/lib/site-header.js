// Pure helpers of the Siesta header (SCRUM-164, DESIGN.md Header), so Node can test them.

/** Below this scroll position the header is always full height. */
export const COMPACT_AFTER = 72;
/** Scroll moves smaller than this are ignored, so a trackpad's jitter does not flip the header. */
export const SCROLL_SLACK = 6;

/**
 * Whether the header is compact (56px) after a scroll to y. previous is { compact, y }, where y is the position the
 * last direction change was measured from. Scrolling down past COMPACT_AFTER shrinks it, scrolling up grows it.
 */
export function headerScroll(previous, y) {
  const position = Number.isFinite(y) ? Math.max(y, 0) : 0;
  const last = previous ?? { compact: false, y: 0 };
  if (position <= COMPACT_AFTER) return { compact: false, y: position };
  if (Math.abs(position - last.y) < SCROLL_SLACK) return last;
  return { compact: position > last.y, y: position };
}

/** One or two capital letters for the account avatar: the first letters of the first and last word of the name. */
export function initials(name) {
  const words = String(name ?? "").trim().split(/\s+/).filter(Boolean);
  if (words.length === 0) return "?";
  const first = Array.from(words[0])[0];
  const last = words.length > 1 ? Array.from(words[words.length - 1])[0] : "";
  return `${first}${last}`.toLocaleUpperCase("tr-TR");
}

/** The item index after an arrow, Home or End key in a list of count items; wraps at both ends. null for other keys. */
export function listIndexAfterKey(index, key, count) {
  if (!Number.isInteger(count) || count <= 0) return null;
  const current = Number.isInteger(index) && index >= 0 && index < count ? index : 0;
  switch (key) {
    case "ArrowDown": return (current + 1) % count;
    case "ArrowUp": return (current - 1 + count) % count;
    case "Home": return 0;
    case "End": return count - 1;
    default: return null;
  }
}
