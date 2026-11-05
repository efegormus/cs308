import { useEffect, useRef, useSyncExternalStore } from "react";
import { createRequestController } from "./request-controller.js";

/** Explicit execution only. run(task, options) calls task({ signal }) once.
 * The caller owns API calls, response validation and any deliberate retry.
 */
export function useRequest() {
  const ref = useRef(null);
  if (ref.current === null) ref.current = createRequestController();
  const controller = ref.current;
  const state = useSyncExternalStore(controller.subscribe, controller.getSnapshot, controller.getSnapshot);

  useEffect(() => {
    controller.activate();
    return () => controller.deactivate();
  }, [controller]);

  return { ...state, run: controller.run, cancel: controller.cancel, reset: controller.reset };
}
