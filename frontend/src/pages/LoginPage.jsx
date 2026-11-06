import { useEffect } from "react";
import { useSearchParams } from "react-router";
import { safeReturnUrl } from "../lib/auth-utils.js";
import { LoadingState } from "../components/RequestStates.js";

// The authentication form has not been ported yet. Keep its working cookie/CSRF flow during migration.
export default function LoginPage() {
  const [params] = useSearchParams();
  const target = `/login.html?returnUrl=${encodeURIComponent(safeReturnUrl(params.get("returnUrl")))}`;
  useEffect(() => { window.location.replace(target); }, [target]);
  return <main id="main-content"><LoadingState label="Opening login…" /><a href={target}>Continue to login</a></main>;
}
