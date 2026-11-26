// Star display for the average rating and a customer's own rating (SCRUM-137). Pure, so Node can test it.
// The text next to the stars ("4.5 / 5") stays the accessible value; the stars themselves are decoration.
export function starCount(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return 0;
  return Math.min(5, Math.max(0, Math.round(number)));
}

export function starRow(value) {
  const filled = starCount(value);
  return "★".repeat(filled) + "☆".repeat(5 - filled);
}
