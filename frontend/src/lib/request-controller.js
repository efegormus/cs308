import { normalizeRequestError } from "./request-errors.js";

const idle = () => ({ status: "idle", data: null, error: null });
const cancelled = () => ({ status: "cancelled" });

// Framework-independent lifecycle core used by useRequest and tested with node --test.
export function createRequestController() {
  let state = idle();
  let active = true;
  let generation = 0;
  let pending = null;
  const listeners = new Set();

  function publish(next) {
    state = next;
    listeners.forEach(listener => listener());
  }

  function invalidate() {
    generation += 1;
    pending?.abort();
    pending = null;
  }

  return {
    getSnapshot: () => state,
    subscribe(listener) {
      listeners.add(listener);
      return () => listeners.delete(listener);
    },
    activate() {
      active = true;
    },
    deactivate() {
      active = false;
      invalidate();
      // StrictMode may activate this controller again. Never restore stale loading/data.
      state = idle();
    },
    cancel() {
      invalidate();
      if (active) publish(idle());
    },
    reset() {
      invalidate();
      if (active) publish(idle());
    },
    async run(task, { timeoutMs = 15000, validate } = {}) {
      if (!active) return cancelled();
      invalidate();
      const current = generation;
      const controller = new AbortController();
      pending = controller;
      publish({ status: "loading", data: null, error: null });

      let timer;
      let onAbort;
      try {
        const aborted = new Promise((_, reject) => {
          onAbort = () => reject(controller.signal.reason);
          controller.signal.addEventListener("abort", onAbort, { once: true });
        });
        if (Number.isFinite(timeoutMs) && timeoutMs > 0 && timeoutMs <= 2147483647) {
          timer = setTimeout(() => controller.abort(new DOMException("Request timed out.", "TimeoutError")), timeoutMs);
        }
        const work = Promise.resolve().then(() => {
          controller.signal.throwIfAborted();
          return task({ signal: controller.signal });
        });
        const data = await Promise.race([work, aborted]);
        if (!active || current !== generation) return cancelled();
        if (validate && validate(data) !== true) throw { kind: "invalid-response" };
        const next = { status: "success", data, error: null };
        publish(next);
        return next;
      } catch (failure) {
        if (!active || current !== generation) return cancelled();
        const error = normalizeRequestError(failure);
        const next = error.kind === "cancelled" ? idle() : { status: "error", data: null, error };
        publish(next);
        return next;
      } finally {
        clearTimeout(timer);
        controller.signal.removeEventListener("abort", onAbort);
        if (current === generation) pending = null;
      }
    }
  };
}
