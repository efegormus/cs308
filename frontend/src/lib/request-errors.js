// Presentation only: no fetching, authentication, response-body logging or retries.
const normalized = new WeakSet();
const messages = {
  400: "The request is not valid. Check your details and try again.",
  401: "Your session ended. Log in again before continuing.",
  402: "The mock payment was declined. Your cart was kept.",
  403: "Your account does not have access to this action.",
  404: "The requested resource could not be found.",
  409: "The request conflicts with the current state. Reload before trying again.",
  429: "Too many requests. Please try again shortly.",
  500: "The request could not be completed. Please try again.",
  503: "The service is temporarily unavailable. Please try again shortly."
};
const maxRetryAfterSeconds = 24 * 60 * 60;

function boundedDelay(seconds) {
  return Number.isSafeInteger(seconds) && seconds >= 0 && seconds <= maxRetryAfterSeconds ? seconds : null;
}

export function parseRetryAfter(value, now = Date.now()) {
  if (typeof value !== "string" || value.length > 128) return null;
  const text = value.trim();
  if (/^\d+$/.test(text)) {
    const seconds = Number(text);
    return boundedDelay(seconds);
  }
  // HTTP-date (IMF-fixdate), not Date.parse's permissive numeric/date shortcuts.
  if (!/^(Mon|Tue|Wed|Thu|Fri|Sat|Sun), \d{2} (Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec) \d{4} \d{2}:\d{2}:\d{2} GMT$/.test(text)) return null;
  const instant = Date.parse(text);
  if (!Number.isFinite(instant) || new Date(instant).toUTCString() !== text || !Number.isFinite(now)) return null;
  return boundedDelay(Math.max(0, Math.ceil((instant - now) / 1000)));
}

function httpStatus(value) {
  return Number.isInteger(value) && value >= 400 && value <= 599 ? value : null;
}

function retryHeader(headers) {
  if (typeof headers?.get === "function") return headers.get("Retry-After");
  if (headers && typeof headers === "object") {
    const key = Object.keys(headers).find(name => name.toLowerCase() === "retry-after");
    return key ? headers[key] : null;
  }
  return null;
}

/** Accept a thrown error, a Response, or { status, problem, headers } from the API client.
 * The transport status takes precedence over ProblemDetails.status. Never echo title,
 * detail, validation values, URLs, stack traces or an arbitrary Error.message.
 */
export function normalizeRequestError(input, { now = Date.now() } = {}) {
  if (input && normalized.has(input)) return input;
  const transportStatus = input?.response?.status ?? input?.status;
  const status = httpStatus(transportStatus ?? input?.problem?.status);
  let kind = "unexpected";
  let message = "The request failed. Please try again.";
  let retryAfterSeconds = null;

  if (status !== null) {
    kind = "http";
    message = messages[status] ?? message;
    if (status === 429) {
      retryAfterSeconds = parseRetryAfter(retryHeader(input?.response?.headers ?? input?.headers), now);
      if (retryAfterSeconds !== null) {
        message = retryAfterSeconds === 0 ? "Too many requests. Please try again."
          : `Too many requests. Try again in ${retryAfterSeconds} ${retryAfterSeconds === 1 ? "second" : "seconds"}.`;
      }
    }
  } else if (input?.name === "TimeoutError" || input?.kind === "timeout") {
    kind = "timeout";
    message = "The request timed out. Please try again.";
  } else if (input?.name === "AbortError" || input?.kind === "cancelled") {
    kind = "cancelled";
    message = "The request was cancelled.";
  } else if (input?.name === "SyntaxError" || input?.kind === "invalid-response") {
    kind = "invalid-response";
    message = "The server returned an unexpected response. Please try again.";
  } else if (input?.name === "TypeError" || input?.kind === "network") {
    kind = "network";
    message = "Could not reach the server. Check your connection and try again.";
  }

  const error = Object.freeze({ kind, status, message, retryAfterSeconds });
  normalized.add(error);
  return error;
}
