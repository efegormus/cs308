import { useEffect, useRef } from "react";
import { lerp } from "../lib/coverflow.js";

const FOLLOW = 0.28;

// The VIEW circle that grows out of the pointer on a row's front card and shrinks back when it leaves.
// One per page. It looks for the front card every frame, so it also reacts when a card slides under a still pointer.
export default function ViewCursor() {
  const cursorRef = useRef(null);

  useEffect(() => {
    if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return undefined;
    const cursor = cursorRef.current;
    const root = document.documentElement;
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const pointer = { x: -999, y: -999, type: "mouse", inside: false };
    const current = { x: -999, y: -999, on: false };

    const onMove = event => {
      pointer.x = event.clientX;
      pointer.y = event.clientY;
      pointer.type = event.pointerType;
      pointer.inside = true;
    };
    const onLeave = () => { pointer.inside = false; };

    let frame = 0;
    function tick() {
      let want = false;
      if (pointer.inside && pointer.type !== "touch") {
        const card = document.elementFromPoint(pointer.x, pointer.y)?.closest(".cf-card.is-front");
        want = !!card && !card.closest(".cf-row.dragging");
      }
      // Grow out of the pointer itself.
      if (want && !current.on) {
        current.x = pointer.x;
        current.y = pointer.y;
      }
      if (want !== current.on) {
        current.on = want;
        cursor.classList.toggle("on", want);
      }
      const follow = reducedMotion.matches ? 1 : FOLLOW;
      current.x = lerp(current.x, pointer.x, follow);
      current.y = lerp(current.y, pointer.y, follow);
      cursor.style.transform = `translate3d(${current.x}px, ${current.y}px, 0)`;
      frame = requestAnimationFrame(tick);
    }

    document.addEventListener("pointermove", onMove, { passive: true });
    root.addEventListener("pointerleave", onLeave);
    window.addEventListener("blur", onLeave);
    frame = requestAnimationFrame(tick);

    return () => {
      cancelAnimationFrame(frame);
      document.removeEventListener("pointermove", onMove);
      root.removeEventListener("pointerleave", onLeave);
      window.removeEventListener("blur", onLeave);
    };
  }, []);

  return (
    <div ref={cursorRef} className="view-cursor" aria-hidden="true">
      <div className="dot"><span className="label">VIEW</span></div>
    </div>
  );
}
