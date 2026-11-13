import { useEffect, useState } from "react";
import Card from "../ui/Card.jsx";
import { formatKpi, startCountUp } from "../../lib/count-up.js";
import { MOTION, reducedMotion } from "../../lib/motion.js";

// One KPI card (SCRUM-124). Card `index` rises in index × kpiStagger ms after the first, and its value counts from 0
// once the card starts to rise. Reduced motion shows the final value at once. Screen readers get the final value only:
// the counting number is aria-hidden.
export default function KpiCard({ label, value, kind = "count", hint, index = 0 }) {
  const [shown, setShown] = useState(() => reducedMotion() ? value : 0);
  useEffect(() => {
    const reduced = reducedMotion();
    let cancel = () => {};
    const timer = setTimeout(() => { cancel = startCountUp(value, setShown, { reduced }); },
      reduced ? 0 : index * MOTION.kpiStagger);
    return () => { clearTimeout(timer); cancel(); };
  }, [value, index]);

  return <Card as="article" className="admin-kpi" style={{ "--i": index }}>
    <h3 className="admin-kpi-label">{label}</h3>
    <p className="admin-kpi-value">
      <span aria-hidden="true">{formatKpi(shown, kind)}</span>
      <span className="admin-sr">{formatKpi(value, kind)}</span>
    </p>
    {hint && <p className="admin-kpi-hint">{hint}</p>}
  </Card>;
}
