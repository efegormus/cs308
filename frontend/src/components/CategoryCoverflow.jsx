import { useLayoutEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router";
import { categoryAnchorId, productCountLabel } from "../lib/category-display.js";
import {
  anchorAt, cardEnterDelay, categoryIconKind, clamp, DRAG_THRESHOLD_PX, geometry, panelOpacity, place, rubber,
  settleIndex, SORT_CHOICES, SORT_STAGGER_MS, subOpacity, WHEEL_SETTLE_MS, wheelDeltaPx, wheelTarget
} from "../lib/coverflow.js";
import { formatPrice, safeImageUrl, stockLabel } from "../lib/product-display.js";
import { ErrorState, LoadingState } from "./RequestStates.js";

const ICON_SHAPES = {
  phone: <><rect x="6.5" y="2.5" width="11" height="19" rx="2.5" /><path d="M11 18.5h2" /></>,
  tablet: <><rect x="3.5" y="3" width="17" height="18" rx="2.5" /><path d="M11 18h2" /></>,
  other: <><path d="M21 8l-9-5-9 5v8l9 5 9-5z" /><path d="M3 8l9 5 9-5" /><path d="M12 13v8" /></>
};

function CategoryIcon({ kind, size, stroke }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={stroke}
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      {ICON_SHAPES[kind]}
    </svg>
  );
}

function Chevron({ path }) {
  return (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
      strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d={path} />
    </svg>
  );
}

function cardLabel(product, position, total, isFront) {
  const stock = product.stockQuantity > 0 ? "" : ", out of stock";
  const action = isFront ? "Opens the product page" : "Brings it to the front";
  return `${product.name}, ${formatPrice(product.price)}${stock}, ${position + 1} of ${total}. ${action}`;
}

// Everything that moves while a finger or touchpad drives the row. The position is a real number kept here, not in React
// state, and the styles go straight to the DOM, so a wheel event never causes a render. React only hears which product
// the row settled on. Same rule as the login page's button spotlight.
function createRowMotion(dom, cards, commitFront) {
  const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
  const state = {
    order: [], pos: 0, front: -1, committed: 0, g: null, frame: 0,
    wheelRaw: 0, wheelTimer: 0, relayoutTimer: 0, drag: null, suppressClick: false
  };
  const last = () => state.order.length - 1;

  // Only a real change goes to React. A setState with the same value stays in React's queue and causes an empty
  // extra commit at the next real update.
  function commit(front) {
    if (front === state.committed) return;
    state.committed = front;
    commitFront(front);
  }

  function writeFront(front) {
    state.front = front;
    const total = state.order.length;
    state.order.forEach((product, position) => {
      const card = cards.get(product.id);
      if (!card) return;
      const isFront = position === front;
      card.classList.toggle("is-front", isFront);
      card.tabIndex = isFront ? 0 : -1;
      card.setAttribute("aria-current", isFront ? "true" : "false");
      card.setAttribute("aria-label", cardLabel(product, position, total, isFront));
    });
    dom.prev.current.disabled = front === 0;
    dom.next.current.disabled = front === last();
    // A new element each time, so the count's entrance plays again.
    const line = document.createElement("span");
    line.textContent = `${front + 1} / ${total} · ${state.order[front].name}`;
    dom.count.current.replaceChildren(line);
  }

  function paint() {
    const { g, pos } = state;
    const anchor = anchorAt(pos, g);
    state.order.forEach((product, position) => {
      const card = cards.get(product.id);
      if (!card) return;
      const placed = place(position - pos, anchor, g, reducedMotion.matches);
      card.style.left = `${placed.x}px`;
      card.style.width = `${g.cardW}px`;
      card.style.height = `${g.cardH}px`;
      card.style.transform = placed.transform;
      card.style.transformOrigin = placed.origin;
      card.style.zIndex = placed.z;
      card.style.filter = `brightness(${placed.light})`;
    });

    // The category panel fades as the cards cover it; the small heading takes its place.
    const cover = clamp(pos, 0, 1);
    if (!g.narrow) {
      dom.panel.current.style.opacity = String(panelOpacity(cover));
      dom.panel.current.style.transform = `translateX(${-28 * cover}px)`;
      dom.panel.current.style.pointerEvents = cover > 0.5 ? "none" : "";
      // A covered link cannot be seen, so Tab must not land on it either.
      dom.panelLink.current.tabIndex = cover > 0.5 ? -1 : 0;
    }
    const subOn = g.narrow ? 1 : subOpacity(cover);
    dom.sub.current.style.opacity = String(subOn);
    dom.sub.current.style.transform = `translateX(${-10 * (1 - subOn)}px)`;

    const haloWidth = g.cardW * 2.2;
    dom.halo.current.style.left = `${anchor + g.cardW / 2 - haloWidth / 2}px`;
    dom.halo.current.style.width = `${haloWidth}px`;
    dom.floor.current.style.left = `${anchor + 10}px`;
    dom.floor.current.style.width = `${g.cardW - 20}px`;
    dom.floor.current.style.top = `${28 + g.cardH + 6}px`;

    const front = clamp(Math.round(pos), 0, last());
    if (front !== state.front) writeFront(front);
  }

  function schedulePaint() {
    if (state.frame) return;
    state.frame = requestAnimationFrame(() => {
      state.frame = 0;
      paint();
    });
  }

  function measure() {
    const g = geometry(dom.track.current.clientWidth);
    state.g = g;
    dom.section.current.classList.toggle("is-narrow", g.narrow);
    dom.track.current.style.setProperty("--track-h", `${g.cardH + 64}px`);
  }

  // Ends any following and lets the transitions carry the row to the product.
  function settle(index) {
    cancelAnimationFrame(state.frame);
    state.frame = 0;
    clearTimeout(state.wheelTimer);
    dom.section.current.classList.remove("dragging");
    state.pos = clamp(index, 0, last());
    paint();
    commit(state.front);
  }

  function setDelays(delayOf) {
    state.order.forEach((product, position) => {
      cards.get(product.id)?.style.setProperty("--delay", `${delayOf(position)}ms`);
    });
  }

  function startFollow() {
    setDelays(() => 0);
    dom.section.current.classList.add("dragging");
  }

  function go(index) {
    setDelays(() => 0);
    settle(index);
  }

  return {
    // Takes the products in their sorted order. The first list appears without moving, a later one slides into place.
    setOrder(next) {
      if (next === state.order) return;
      const first = state.order.length === 0;
      state.order = next;
      state.front = -1;
      if (first) {
        const section = dom.section.current;
        section.classList.add("no-anim");
        measure();
        settle(0);
        requestAnimationFrame(() => requestAnimationFrame(() => section.classList.remove("no-anim")));
      } else {
        // Cards travel to their new places one after another, left to right.
        setDelays(position => position * SORT_STAGGER_MS);
        settle(0);
      }
    },
    clear() {
      cancelAnimationFrame(state.frame);
      state.frame = 0;
      clearTimeout(state.wheelTimer);
      state.order = [];
      state.pos = 0;
      state.front = -1;
      state.drag = null;
      commit(0);
    },
    stop() {
      cancelAnimationFrame(state.frame);
      state.frame = 0;
      clearTimeout(state.wheelTimer);
      clearTimeout(state.relayoutTimer);
      dom.section.current?.classList.remove("no-anim", "dragging");
    },
    // Re-measures without animating the jump.
    relayout() {
      if (!state.order.length) return;
      dom.section.current.classList.add("no-anim");
      measure();
      paint();
      clearTimeout(state.relayoutTimer);
      state.relayoutTimer = setTimeout(() => dom.section.current?.classList.remove("no-anim"), 60);
    },
    widthChanged: () => dom.track.current?.clientWidth !== state.g?.width,
    reducedMotion,

    // Touchpad: a two-finger sideways swipe moves the row with the fingers, then it settles on the nearest product.
    wheel(event) {
      const px = wheelDeltaPx(event);
      if (!px) return;
      event.preventDefault();
      if (!dom.section.current.classList.contains("dragging")) {
        startFollow();
        state.wheelRaw = state.pos;
      }
      state.wheelRaw = wheelTarget(state.wheelRaw, px, state.g.side, last());
      state.pos = rubber(state.wheelRaw, last());
      schedulePaint();
      clearTimeout(state.wheelTimer);
      state.wheelTimer = setTimeout(() => settle(Math.round(state.pos)), WHEEL_SETTLE_MS);
    },

    // Mouse drag and finger swipe: the row follows the pointer, a quick flick carries on to the next product.
    pointerDown(event) {
      if (event.button !== 0) return;
      state.drag = {
        x: event.clientX, start: state.pos, moved: false, lastX: event.clientX,
        lastT: performance.now(), v: 0, id: event.pointerId
      };
    },
    pointerMove(event) {
      const { drag } = state;
      if (!drag || event.pointerId !== drag.id) return;
      // The button went up outside the row, so no pointerup reached it.
      if (event.pointerType === "mouse" && event.buttons === 0) {
        this.endDrag(event);
        return;
      }
      const dx = event.clientX - drag.x;
      if (!drag.moved) {
        if (Math.abs(dx) < DRAG_THRESHOLD_PX) return;
        drag.moved = true;
        startFollow();
        try {
          dom.track.current.setPointerCapture(event.pointerId);
        } catch {
          // The pointer is already gone. The row still follows it until the next event.
        }
      }
      const now = performance.now();
      drag.v = (event.clientX - drag.lastX) / Math.max(1, now - drag.lastT);
      drag.lastX = event.clientX;
      drag.lastT = now;
      state.pos = rubber(drag.start - dx / state.g.side, last());
      schedulePaint();
    },
    endDrag(event) {
      const { drag } = state;
      if (!drag || event.pointerId !== drag.id) return;
      state.drag = null;
      if (!drag.moved) return;
      // The click that ends a drag must not open or select a card.
      state.suppressClick = true;
      setTimeout(() => { state.suppressClick = false; }, 0);
      settle(settleIndex(state.pos, drag.v, state.g.side, drag.start, last()));
    },
    clickCapture(event) {
      if (!state.suppressClick) return;
      event.preventDefault();
      event.stopPropagation();
    },

    // The front card is a link and opens the product. A side card only comes to the front.
    cardClick(event, product) {
      const position = state.order.findIndex(item => item.id === product.id);
      if (position === state.front) return;
      event.preventDefault();
      go(position);
    },
    arrow(delta, button) {
      go(state.front + delta);
      // A disabled button drops the focus, so hand it to the row.
      if (button.disabled && document.activeElement === button) dom.track.current.focus({ preventScroll: true });
    },
    keyDown(event) {
      if (event.altKey || event.ctrlKey || event.metaKey) return;
      const moves = { ArrowRight: state.front + 1, ArrowLeft: state.front - 1, Home: 0, End: last() };
      if (!(event.key in moves)) return;
      event.preventDefault();
      go(moves[event.key]);
      if (document.activeElement !== dom.track.current) {
        cards.get(state.order[state.front].id)?.focus({ preventScroll: true });
      }
    }
  };
}

function CoverflowCard({ product, position, total, isFront, enterDelay, iconKind, cardRef, onClick }) {
  const [imageFailed, setImageFailed] = useState(false);
  const imageUrl = safeImageUrl(product.imageUrl);
  const stock = stockLabel(product.stockQuantity);

  // Always a link, so that coming to the front keeps the same DOM node and the card slides there.
  return (
    <Link ref={cardRef} className={isFront ? "cf-card is-front" : "cf-card"} to={`/product?id=${product.id}`}
      tabIndex={isFront ? 0 : -1} aria-current={isFront ? "true" : "false"}
      aria-label={cardLabel(product, position, total, isFront)} draggable={false} onClick={onClick}>
      <span className="cf-face" style={{ "--enter": `${enterDelay}ms` }}>
        <span className="cf-img">
          {imageUrl && !imageFailed ? (
            <img src={imageUrl} alt="" loading="lazy" draggable={false} onError={() => setImageFailed(true)} />
          ) : (
            <>
              <CategoryIcon kind={iconKind} size={46} stroke={1.3} />
              <span>No image available</span>
            </>
          )}
        </span>
        <span className="cf-info">
          <span className="cf-name">{product.name}</span>
          <span className="cf-price">{formatPrice(product.price)}</span>
          <span className={stock.inStock ? "cf-stock" : "cf-stock out"}>{stock.text}</span>
        </span>
      </span>
    </Link>
  );
}

// One category row: its sort dropdown and its 3D coverflow.
export default function CategoryCoverflow({
  category, products, sort, onSortChange, loading = false, error = null, onRetry, rowIndex = 0
}) {
  const [frontIndex, setFrontIndex] = useState(0);
  const dom = {
    section: useRef(null), track: useRef(null), halo: useRef(null), floor: useRef(null), panel: useRef(null),
    panelLink: useRef(null), sub: useRef(null), count: useRef(null), prev: useRef(null), next: useRef(null)
  };
  const cards = useRef(new Map()).current;
  const motionRef = useRef(null);
  if (motionRef.current === null) motionRef.current = createRowMotion(dom, cards, setFrontIndex);
  const motion = motionRef.current;

  const list = !error && products?.length ? products : null;
  const showsCards = list !== null;

  // Cards stay in id order in the DOM. Moving a node in the DOM cancels its transition and replays its entrance,
  // so a new sort only changes where each card is placed, never where its element sits.
  const stable = useMemo(() => (list ? [...list].sort((a, b) => a.id - b.id) : []), [list]);

  useLayoutEffect(() => {
    if (list) motion.setOrder(list);
    else motion.clear();
  }, [list, motion]);

  // A layout effect, so the cleanup runs before React empties the refs. A resize or timer cannot arrive in between.
  useLayoutEffect(() => {
    if (!showsCards) return undefined;
    const track = dom.track.current;
    const onResize = () => { if (motion.widthChanged()) motion.relayout(); };
    const onReducedMotion = () => motion.relayout();
    // React's onWheel is passive, where preventDefault does nothing.
    track.addEventListener("wheel", motion.wheel, { passive: false });
    motion.reducedMotion.addEventListener("change", onReducedMotion);
    const observer = new ResizeObserver(onResize);
    observer.observe(track);
    return () => {
      track.removeEventListener("wheel", motion.wheel);
      motion.reducedMotion.removeEventListener("change", onReducedMotion);
      observer.disconnect();
      motion.stop();
    };
  // The refs in dom are the same objects on every render.
  }, [showsCards, motion]);

  const name = category.name;
  const headingId = `coverflow-heading-${category.id}`;
  const selectId = `coverflow-sort-${category.id}`;
  const singular = name.toLowerCase().replace(/s$/, "");
  const iconKind = categoryIconKind(name);
  const total = list?.length ?? 0;
  // The panel holds the heading while there are cards. Without them, the small heading in the bar is the real one.
  const Sub = showsCards ? "div" : "h2";

  return (
    <section ref={dom.section} className="cf-row" style={{ "--row": rowIndex }} aria-labelledby={headingId} aria-busy={loading}>
      <div className="cf-bar">
        <Sub ref={dom.sub} className="cf-sub" {...(showsCards ? { "aria-hidden": "true" } : { id: headingId })}>
          <span className="icon"><CategoryIcon kind={iconKind} size={18} stroke={1.8} /></span>
          <span className="name">{name}</span>
        </Sub>
        <label className="cf-sort" htmlFor={selectId}>
          Sort by
          <select id={selectId} value={sort} onChange={event => onSortChange(event.target.value)}>
            {SORT_CHOICES.map(({ value, label }) => <option key={value} value={value}>{label}</option>)}
          </select>
        </label>
      </div>

      {showsCards ? (
        <>
          <div className="cf-stage">
            <div ref={dom.track} className="cf-track" tabIndex={0} role="group" aria-roledescription="carousel"
              aria-label={`${name} products. Use the left and right arrow keys.`}
              onPointerDown={event => motion.pointerDown(event)} onPointerMove={event => motion.pointerMove(event)}
              onPointerUp={event => motion.endDrag(event)} onPointerCancel={event => motion.endDrag(event)}
              onClickCapture={event => motion.clickCapture(event)} onKeyDown={event => motion.keyDown(event)}
              onDragStart={event => event.preventDefault()}>
              <div ref={dom.halo} className="cf-halo" />
              <div ref={dom.floor} className="cf-floor" />
              <div ref={dom.panel} className="cf-panel">
                <h2 id={headingId}>
                  <span className="icon"><CategoryIcon kind={iconKind} size={34} stroke={1.6} /></span>
                  <span className="title">{name}</span>
                </h2>
                <p>{productCountLabel(total)}</p>
                <Link ref={dom.panelLink} to={`/products#${categoryAnchorId(category.id)}`}>Browse {name.toLowerCase()}</Link>
              </div>
              {stable.map((product, stableIndex) => {
                const position = list.indexOf(product);
                return (
                  <CoverflowCard key={product.id} product={product} position={position} total={total}
                    isFront={position === frontIndex} enterDelay={cardEnterDelay(rowIndex, stableIndex)} iconKind={iconKind}
                    cardRef={element => {
                      cards.set(product.id, element);
                      return () => cards.delete(product.id);
                    }}
                    onClick={event => motion.cardClick(event, product)} />
                );
              })}
            </div>
            <button ref={dom.prev} className="cf-arrow prev" type="button" aria-label={`Previous ${singular}`}
              disabled={frontIndex === 0} onClick={event => motion.arrow(-1, event.currentTarget)}>
              <Chevron path="M15 6l-6 6 6 6" />
            </button>
            <button ref={dom.next} className="cf-arrow next" type="button" aria-label={`Next ${singular}`}
              disabled={frontIndex === total - 1} onClick={event => motion.arrow(1, event.currentTarget)}>
              <Chevron path="M9 6l6 6-6 6" />
            </button>
          </div>
          {/* Written directly while the row moves, so React renders no children here. */}
          <p ref={dom.count} className="cf-count" aria-live="polite" />
        </>
      ) : (
        <div className="cf-status">
          {error && <ErrorState error={error} onRetry={onRetry} />}
          {!error && loading && <LoadingState label={`Loading ${name.toLowerCase()}…`} />}
          {!error && !loading && <p className="cf-empty">No products in this category yet.</p>}
        </div>
      )}
    </section>
  );
}
